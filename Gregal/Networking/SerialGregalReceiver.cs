using Gregal.Config;
using Gregal.Data;
using Gregal.Parsing;
using Gregal.Protocol;
using Microsoft.Extensions.Options;
using System.IO.Ports;
using System.Text;
using System.Threading.Channels;
using static Gregal.Protocol.GregalProtocol;

namespace Gregal.Networking {
    /// <summary>
    /// Receptor RS232. Implementa IGregalReceiver igual que TcpGregalReceiver
    /// pero usando SerialPort como transporte.
    ///
    /// Arquitectura de concurrencia:
    ///   - OnDataReceived (hilo ThreadPool) solo acumula bytes y extrae mensajes
    ///     completos, escribiéndolos en un Channel.
    ///   - ListenAsync (hilo principal de la tarea) lee del Channel y procesa
    ///     cada mensaje de forma async sin bloquear el ThreadPool.
    ///   - Así evitamos condiciones de carrera sobre _accumulator y no tragamos
    ///     excepciones en Fire & Forget.
    /// </summary>
    public class SerialGregalReceiver : IGregalReceiver {
        private readonly ILogger<SerialGregalReceiver> _logger;
        private readonly IOptionsMonitor<NetworkSettings> _network;
        private readonly GregalParserEngine _parser;
        private readonly HistoricoRepository _repo;
        private readonly NetworkSettings networkSettings;

        private SerialPort _port;
        private string _accumulator = string.Empty;

        // Canal SPSC: OnDataReceived escribe, ListenAsync lee
        private readonly Channel<string> _messageChannel =
            Channel.CreateUnbounded<string>(new UnboundedChannelOptions {
                SingleReader = true,
                SingleWriter = false   // DataReceived puede dispararse en varios hilos
            });

        private char Delimiter => _network.CurrentValue.SerialDelimiter;

        public SerialGregalReceiver(
            ILogger<SerialGregalReceiver> logger,
            IOptionsMonitor<NetworkSettings> network,
            GregalParserEngine parser,
            HistoricoRepository repo) {
            _logger = logger;
            _network = network;
            _parser = parser;
            _repo = repo;


            networkSettings = _network.CurrentValue;

            _port = new SerialPort(networkSettings.ComPort, networkSettings.BaudRate) {
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                Encoding = Encoding.UTF8,
                ReadTimeout = SerialPort.InfiniteTimeout,
                WriteTimeout = 2000
            };

            _port.DataReceived += OnDataReceived;
        }

        // ── Arranque ──────────────────────────────────────────────────
        public async Task ListenAsync(CancellationToken ct) {
            
            try {
                _port.Open(); 
                _logger.LogInformation("Puerto serie abierto: {port} a {baud} baudios.", networkSettings.ComPort, networkSettings.BaudRate);

                // Procesa mensajes del canal hasta que se cancele
                await ProcessChannelAsync(ct);
            }
            catch (OperationCanceledException) {
                _logger.LogInformation("Receptor serie detenido.");
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error al abrir el puerto serie {port}.", networkSettings.ComPort);
            }
            finally {
                Stop();
            }
        }

        public void Stop() {
            _messageChannel.Writer.TryComplete();

            if (_port != null && _port.IsOpen) {
                _port.DataReceived -= OnDataReceived;
                _port.Close();
                _logger.LogInformation("Puerto serie cerrado.");
            }
            _port?.Dispose();
            _port = null;
        }

        // ── Recepción: solo acumula y extrae mensajes al canal ────────
        // Este método corre en el ThreadPool. No hace nada async.
        // Solo extrae tramas completas y las mete en el canal.
        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e) {
            if (_port == null || !_port.IsOpen) return;
            try {
                string incoming = _port.ReadExisting();
                if (string.IsNullOrEmpty(incoming)) return;

                _logger.LogDebug("Bytes recibidos serie ({len} chars).", incoming.Length);

                _accumulator += incoming;

                if (_accumulator.Length > _network.CurrentValue.TcpMaxBufferChars) {
                    _logger.LogWarning("Buffer serie excedido ({len} chars). Limpiando.", _accumulator.Length);
                    TryWriteResponse(GregalProtocolFactory.CreateError(
                        ErrorCode.BUFFER_EXCEEDED, "unknown", "Buffer excedido"));
                    _accumulator = string.Empty;
                    return;
                }

                // Extraer mensajes completos y enviarlos al canal
                while (_accumulator.Contains(Delimiter)) {
                    int idx = _accumulator.IndexOf(Delimiter);
                    string rawMessage = _accumulator[..idx].Trim();
                    _accumulator = _accumulator[(idx + 1)..];

                    if (!string.IsNullOrWhiteSpace(rawMessage)) {
                        _logger.LogDebug("Trama completa extraída: {msg}", rawMessage);
                        _messageChannel.Writer.TryWrite(rawMessage);
                    }
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error leyendo del puerto serie.");
            }
        }

        // ── Procesamiento: lee del canal y ejecuta async ──────────────
        // Este método corre en el hilo de ListenAsync, con await real.
        private async Task ProcessChannelAsync(CancellationToken ct) {
            await foreach (string rawMessage in _messageChannel.Reader.ReadAllAsync(ct)) {
                try {
                    _logger.LogDebug("Procesando trama serie: {msg}", rawMessage);
                    string response = await ProcessMessageAsync(rawMessage);
                    _logger.LogDebug("Respuesta serie: {resp}", response);
                    TryWriteResponse(response);
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Error procesando mensaje del canal serie.");
                }
            }
        }

        // ── Lógica de negocio ─────────────────────────────────────────
        private async Task<string> ProcessMessageAsync(string rawData) {
            string msgId = "unknown";
            try {
                var result = _parser.Parse(rawData);

                if (result == null) {
                    _logger.LogWarning("Formato inválido en trama serie: {data}", rawData);
                    return GregalProtocolFactory.CreateError(ErrorCode.FORMAT_INVALID, msgId);
                }

                msgId = result.MsgId;

                if (result.IsPing)
                    return GregalProtocolFactory.CreatePong(msgId);

                if (result.Data != null) {
                    bool ok = await _repo.SaveHistoricoAsync(result.Data);
                    return ok
                        ? GregalProtocolFactory.CreateSuccess(msgId)
                        : GregalProtocolFactory.CreateError(ErrorCode.DATABASE_REJECTED, msgId);
                }

                return GregalProtocolFactory.CreateError(ErrorCode.UNKNOWN_OPERATION, msgId);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error procesando trama serie.");
                return GregalProtocolFactory.CreateError(ErrorCode.INTERNAL_SERVER_ERROR, msgId);
            }
        }

        // ── Escritura de respuesta ────────────────────────────────────
        private void TryWriteResponse(string response) {
            if (_port == null || !_port.IsOpen) return;
            try {
                _port.Write(response);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error escribiendo respuesta al puerto serie.");
            }
        }
    }
}