using Gregal.Config;
using Gregal.Data;
using Gregal.Parsing;
using Microsoft.Extensions.Options;
using System.IO.Ports;
using System.Text;
using System.Threading.Channels;
using static Gregal.Protocol.GregalProtocol;

namespace Gregal.Networking {
    /// <summary>
    /// Receptor RS232. Es un IHostedService independiente, igual que TcpGregalReceiver,
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
    public class SerialGregalReceiver : IHostedService {
        private readonly ILogger<SerialGregalReceiver> _logger;
        private readonly IOptionsMonitor<NetworkSettings> _network;
        private readonly GregalParserEngine _parser;
        private readonly HistoricoRepository _repo;
        private readonly NetworkSettings networkSettings;

        private SerialPort _port;
        private string _accumulator = string.Empty;

        // Stop() se puede invocar dos veces casi a la vez: una desde StopAsync
        // (al parar el host) y otra desde el finally de ListenAsync (en cuanto
        // el canal se completa). Sin este candado, ambas pueden intentar
        // cerrar/disponer el mismo SerialPort a la vez, lo que dispara un bug
        // conocido de System.IO.Ports (NullReferenceException dentro de
        // SerialStream.Dispose). _detenido además evita repetir el cierre.
        private readonly object _stopLock = new();
        private bool _detenido;

        /* Canal SPSC: OnDataReceived escribe, ListenAsync lee
        Un Channel<T> actúa como una tubería de alta velocidad altamente optimizada para pasar datos entre hilos de forma asíncrona.
        Cómo funciona en tu código: El evento de hardware OnDataReceived corre en un hilo del pool de Windows; extrae la
        trama, hace el corte con el delimitador y la "empuja" al canal con un método no bloqueante (TryWrite). En el otro extremo,
        ProcessChannelAsync espera de forma puramente asíncrona (ReadAllAsync) a que caigan mensajes para procesarlos secuencialmente
        e insertarlos en Firebird sin bloquear nada.
        */
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
                DataBits = networkSettings.SerialDataBits,
                Parity = networkSettings.SerialParity,
                StopBits = networkSettings.SerialStopBits,
                Encoding = Encoding.GetEncoding(networkSettings.SerialEncoding),
                Handshake = networkSettings.SerialHandshake,
                RtsEnable = networkSettings.SerialRtsEnable,
                DtrEnable = networkSettings.SerialDtrEnable,
                ReadTimeout = networkSettings.SerialReadTimeoutMs,
                WriteTimeout = networkSettings.SerialWriteTimeoutMs,
                ReadBufferSize = networkSettings.SerialReadBufferSize,
                WriteBufferSize = networkSettings.SerialWriteBufferSize
            };

            _port.DataReceived += OnDataReceived;
        }

        // Mapeamos el ciclo de arranque de .NET a tu método ListenAsync
        public Task StartAsync(CancellationToken cancellationToken) {
            // Ejecutamos ListenAsync en segundo plano para no bloquear el hilo de arranque principal
            _ = ListenAsync(cancellationToken);
            return Task.CompletedTask;
        }

        // Mapeamos el ciclo de parada de .NET a tu método Stop
        public Task StopAsync(CancellationToken cancellationToken) {
            Stop();
            _logger.LogInformation("Servidor TCP detenido de manera ordenada.");
            return Task.CompletedTask;
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
            lock (_stopLock) {
                if (_detenido) return;
                _detenido = true;

                _messageChannel.Writer.TryComplete();

                if (_port == null) return;

                try {
                    if (_port.IsOpen) {
                        _port.DataReceived -= OnDataReceived;
                        _port.Close();
                        _logger.LogInformation("Puerto serie cerrado.");
                    }
                    _port.Dispose();
                }
                catch (Exception ex) {
                    // Bug conocido de System.IO.Ports: cerrar/disponer un
                    // SerialPort puede lanzar una NullReferenceException
                    // interna (sobre todo con puertos virtuales tipo
                    // com0com). No es un fallo nuestro recuperable, así que
                    // solo se registra para no tumbar el apagado del host.
                    _logger.LogWarning(ex, "Error (no crítico) cerrando el puerto serie.");
                }
                finally {
                    _port = null;
                }
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e) {
            if (_port == null || !_port.IsOpen) return;
            try {
                string incoming = _port.ReadExisting();
                if (string.IsNullOrEmpty(incoming)) return;

                _logger.LogDebug("Bytes recibidos serie ({len} chars).", incoming.Length);
                _accumulator += incoming;

                if (BufferExcedido()) return;

                ExtractAndEnqueueMessages();
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error leyendo del puerto serie.");
            }
        }

        private bool BufferExcedido() {
            if (_accumulator.Length <= _network.CurrentValue.TcpMaxBufferChars) return false;

            _logger.LogWarning("Buffer serie excedido ({len} chars). Limpiando.", _accumulator.Length);
            TryWriteResponse(GregalProtocolFactory.CreateError(
                ErrorCode.BUFFER_EXCEEDED, "unknown", "Buffer excedido"));
            _accumulator = string.Empty;
            return true;
        }

        private void ExtractAndEnqueueMessages() {
            while (_accumulator.Contains(Delimiter)) {
                int idx = _accumulator.IndexOf(Delimiter);
                string rawMessage = _accumulator[..idx].Trim();
                _accumulator = _accumulator[(idx + 1)..];

                if (string.IsNullOrWhiteSpace(rawMessage)) continue;

                _logger.LogDebug("Trama completa extraída: {msg}", rawMessage);
                _messageChannel.Writer.TryWrite(rawMessage);
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

                if (result.ValidationError != null) {
                    return GregalProtocolFactory.CreateError(ErrorCode.FORMAT_INVALID, msgId, result.ValidationError);
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