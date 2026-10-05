using System.IO.Ports;
using System.Text.Json;
using System.Threading.Channels;
using Gregal.Config;
using Gregal.Data;
using Gregal.Models;
using Gregal.Parsing;
using Microsoft.Extensions.Options;

namespace Gregal.Networking {
    /// <summary>
    /// Receptor RS232 para el protocolo de eventos "tag" (*CODIGO<valor>#),
    /// el mismo que usa el software de pantallas PidFullHD. A diferencia de
    /// TcpGregalReceiver/SerialGregalReceiver (un mensaje = una respuesta
    /// inmediata), aquí una partida completa son decenas de eventos que se
    /// van acumulando en PartidaAccumulator; solo al ver el evento F se
    /// intenta guardar en Firebird. El protocolo original es UDP sin ACK, así
    /// que este receptor nunca escribe nada de vuelta al puerto serie.
    /// Puerto serie propio (TagProtocolSettings.ComPort), distinto del que
    /// usa SerialGregalReceiver para JSON/CSV.
    ///
    /// Pipeline en dos etapas desacopladas por sendos Channel:
    ///   1) OnDataReceived (hilo del puerto) -> _messageChannel: solo trocea
    ///      bytes en fragmentos "*CODIGO&lt;valor&gt;", nunca bloquea.
    ///   2) ProcessChannelAsync: decodifica cada fragmento y alimenta
    ///      PartidaAccumulator; cuando una partida se completa, la deja en
    ///      _persistChannel en vez de escribir en Firebird directamente.
    ///   3) PersistLoopAsync: único consumidor que llama a
    ///      HistoricoRepository.SaveHistoricoAsync. Así, si llegan varias
    ///      partidas seguidas más rápido de lo que Firebird puede absorber,
    ///      se acumulan en memoria en _persistChannel sin frenar nunca la
    ///      lectura/acumulación del puerto serie.
    /// </summary>
    public class TagSerialGregalReceiver : IHostedService {
        private readonly ILogger<TagSerialGregalReceiver> _logger;
        private readonly IOptionsMonitor<TagProtocolSettings> _settings;
        private readonly PartidaAccumulator _acumulador;
        private readonly HistoricoRepository _repo;
        private readonly TagProtocolSettings _config;

        private SerialPort? _port;
        private string _accumulator = string.Empty;

        // El primer fragmento tras abrir el puerto (o tras limpiar el buffer
        // por desbordamiento) puede ser la cola de un evento ya empezado
        // antes de que Gregal se pusiera a escuchar (p.ej. arrancar a mitad
        // de partida) — se descarta sin interpretarlo, para no tratar un
        // valor truncado como si fuera un código válido.
        private bool _resincronizar = true;

        private readonly Channel<string> _messageChannel =
            Channel.CreateUnbounded<string>(new UnboundedChannelOptions {
                SingleReader = true,
                SingleWriter = false
            });

        private readonly Channel<HistoricoDTO> _persistChannel =
            Channel.CreateUnbounded<HistoricoDTO>(new UnboundedChannelOptions {
                SingleReader = true,
                SingleWriter = true
            });

        private char Delimiter => _settings.CurrentValue.FrameDelimiter;

        public TagSerialGregalReceiver(
            ILogger<TagSerialGregalReceiver> logger,
            IOptionsMonitor<TagProtocolSettings> settings,
            PartidaAccumulator acumulador,
            HistoricoRepository repo) {
            _logger = logger;
            _settings = settings;
            _acumulador = acumulador;
            _repo = repo;

            _config = _settings.CurrentValue;

            _port = new SerialPort(_config.ComPort, _config.BaudRate) {
                DataBits = _config.SerialDataBits,
                Parity = _config.SerialParity,
                StopBits = _config.SerialStopBits,
                Handshake = _config.SerialHandshake,
                RtsEnable = _config.SerialRtsEnable,
                DtrEnable = _config.SerialDtrEnable,
                ReadTimeout = _config.SerialReadTimeoutMs,
                WriteTimeout = _config.SerialWriteTimeoutMs,
                ReadBufferSize = _config.SerialReadBufferSize,
                WriteBufferSize = _config.SerialWriteBufferSize,
                // Latin1 (no ASCII/UTF-8, el default de SerialPort es ASCII):
                // mapea cada byte 0-255 a su mismo code point sin tocarlo. El
                // evento 'e' (EstadoPartidaDecoder) es binario con todos sus
                // bytes >= 0x80 a propósito; con ASCII se convertirían en '?'
                // y con UTF-8 podrían no formar secuencias válidas, perdiendo
                // los datos antes de que lleguen a nuestro parser.
                Encoding = System.Text.Encoding.Latin1
            };

            _port.DataReceived += OnDataReceived;
        }

        public Task StartAsync(CancellationToken cancellationToken) {
            _ = ListenAsync(cancellationToken);
            _ = PersistLoopAsync(cancellationToken);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) {
            Stop();
            _logger.LogInformation("Receptor serie (protocolo tag) detenido de manera ordenada.");
            return Task.CompletedTask;
        }

        public async Task ListenAsync(CancellationToken ct) {
            try {
                _port!.Open();
                _logger.LogInformation("Puerto serie (protocolo tag) abierto: {port} a {baud} baudios.", _config.ComPort, _config.BaudRate);

                await ProcessChannelAsync(ct);
            }
            catch (OperationCanceledException) {
                _logger.LogInformation("Receptor serie (protocolo tag) detenido.");
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error al abrir el puerto serie (protocolo tag) {port}.", _config.ComPort);
            }
            finally {
                Stop();
            }
        }

        public void Stop() {
            _messageChannel.Writer.TryComplete();
            _persistChannel.Writer.TryComplete();

            if (_port != null && _port.IsOpen) {
                _port.DataReceived -= OnDataReceived;
                _port.Close();
                _logger.LogInformation("Puerto serie (protocolo tag) cerrado.");
            }
            _port?.Dispose();
            _port = null;
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e) {
            if (_port == null || !_port.IsOpen) return;
            try {
                string incoming = _port.ReadExisting();
                if (string.IsNullOrEmpty(incoming)) return;

                _accumulator += incoming;

                if (BufferExcedido()) return;

                ExtractAndEnqueueMessages();
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error leyendo del puerto serie (protocolo tag).");
            }
        }

        private bool BufferExcedido() {
            if (_accumulator.Length <= _settings.CurrentValue.MaxBufferChars) return false;

            // Sin ACK en este protocolo (ver clase): no se notifica el descarte
            // al emisor, solo se registra y se limpia el acumulador.
            _logger.LogWarning("Buffer serie (protocolo tag) excedido ({len} chars). Limpiando.", _accumulator.Length);
            _accumulator = string.Empty;
            // Tras limpiar, el próximo fragmento puede volver a ser una cola
            // truncada: hay que resincronizar igual que al arrancar.
            _resincronizar = true;
            return true;
        }

        // Solo ASCII de relleno (espacio/tab/CR/LF), nunca el Trim() genérico:
        // este también recorta U+0085/U+00A0, que son bytes 0x85/0xA0
        // legítimos dentro del payload binario del evento 'e' — recortarlos
        // desalinearía el mensaje y rompería su checksum.
        private static readonly char[] RellenoAsciiTolerado = { ' ', '\t', '\r', '\n' };

        private void ExtractAndEnqueueMessages() {
            while (_accumulator.Contains(Delimiter)) {
                int idx = _accumulator.IndexOf(Delimiter);
                string rawFragment = _accumulator[..idx].Trim(RellenoAsciiTolerado);
                _accumulator = _accumulator[(idx + 1)..];

                if (_resincronizar) {
                    _resincronizar = false;
                    _logger.LogDebug("Descartado el primer fragmento tras (re)sincronizar: '{frag}' (posible resto de un evento ya empezado).", rawFragment);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(rawFragment)) continue;

                _messageChannel.Writer.TryWrite(rawFragment);
            }
        }

        // Solo trocea/acumula eventos; nunca espera a Firebird (eso lo hace
        // PersistLoopAsync, en paralelo) para no frenar la lectura serie si
        // llegan varias partidas seguidas.
        private async Task ProcessChannelAsync(CancellationToken ct) {
            await foreach (string rawFragment in _messageChannel.Reader.ReadAllAsync(ct)) {
                try {
                    if (_settings.CurrentValue.LogRawFrames) {
                        // Transcripción fiel de lo que emite el hardware, útil en un
                        // despliegue nuevo para confirmar el protocolo real antes de
                        // fiarse del mapeo asumido. Desactivable (TagProtocolSettings.
                        // LogRawFrames) una vez confirmado, porque es una línea por evento.
                        _logger.LogInformation("Fragmento tag recibido: '{frag}'", rawFragment);
                    }

                    var evt = TagEventParser.Parse(rawFragment);
                    if (evt == null) continue;

                    var dto = _acumulador.Feed(evt.Value);
                    if (dto == null) continue;

                    string? validationError = HistoricoDtoValidator.ValidateRequiredFields(dto);
                    if (validationError != null) {
                        _logger.LogWarning("Partida (protocolo tag) descartada tras el evento F: {error}", validationError);
                        continue;
                    }

                    _persistChannel.Writer.TryWrite(dto);
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Error procesando evento del canal serie (protocolo tag).");
                }
            }
        }

        // Único consumidor de partidas completas. Si llegan más rápido de lo
        // que Firebird puede absorber, se acumulan en _persistChannel (en
        // memoria) sin bloquear ProcessChannelAsync. Si el guardado falla, la
        // partida se anexa a TagProtocolSettings.FallbackFilePath en vez de
        // perderse.
        private async Task PersistLoopAsync(CancellationToken ct) {
            await foreach (HistoricoDTO dto in _persistChannel.Reader.ReadAllAsync(ct)) {
                try {
                    bool ok = await _repo.SaveHistoricoAsync(dto);
                    if (!ok) {
                        await GuardarEnFallbackAsync(dto, "SaveHistoricoAsync devolvió false");
                    }
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Error guardando en Firebird una partida del protocolo tag.");
                    await GuardarEnFallbackAsync(dto, ex.Message);
                }
            }
        }

        private async Task GuardarEnFallbackAsync(HistoricoDTO dto, string motivo) {
            try {
                string path = _settings.CurrentValue.FallbackFilePath;
                string fullPath = Path.IsPathRooted(path)
                    ? path
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);

                string? folder = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) {
                    Directory.CreateDirectory(folder);
                }

                string linea = JsonSerializer.Serialize(dto) + Environment.NewLine;
                await File.AppendAllTextAsync(fullPath, linea);

                _logger.LogWarning(
                    "Partida (protocolo tag) no guardada en Firebird ({motivo}); anexada a {path} para recuperación manual.",
                    motivo, fullPath);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "No se pudo ni guardar en Firebird ni anexar al fallback la partida del protocolo tag; se pierde.");
            }
        }
    }
}
