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

        // Stop() se puede invocar dos veces casi a la vez: una desde StopAsync
        // (al parar el host) y otra desde el finally de ListenAsync (en cuanto
        // el canal se completa). Sin este candado, ambas pueden intentar
        // cerrar/disponer el mismo SerialPort a la vez, lo que dispara un bug
        // conocido de System.IO.Ports (NullReferenceException dentro de
        // SerialStream.Dispose). _detenido además evita repetir el cierre.
        private readonly object _stopLock = new();
        private bool _detenido;

        // System.IO.Ports lanza DataReceived desde el pool de hilos: dos
        // llamadas pueden solaparse y, sin este candado, mezclar o perder bytes
        // de _accumulator.
        private readonly object _lecturaLock = new();

        // El token de StartAsync no se cancela al parar el host: este sí, en
        // Stop(), para cortar los reintentos de abrir el puerto.
        private readonly CancellationTokenSource _parada = new();
        private Task _escucha = Task.CompletedTask;
        private Task _persistencia = Task.CompletedTask;

        // Tras abrir el puerto (o limpiar el buffer por desbordamiento) lo
        // primero que llega puede ser la cola de un evento ya empezado. Esa
        // cola nunca empieza por '*' y TagEventParser.Trocear la devuelve como
        // basura, así que se descarta igual que cualquier otra; este flag solo
        // sirve para no registrarla como aviso, porque en ese momento es
        // esperable y no indica ruido en la línea.
        private bool _resincronizar = true;

        private bool _avisadoBytesControl;

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

            _port = CrearPuerto(_config);
            _port.DataReceived += OnDataReceived;
        }

        // El puerto tal como lo abre el receptor. También lo usa
        // "Gregal.exe --escuchar" (EscuchaPuerto), para probar el cable con
        // exactamente los mismos parámetros.
        public static SerialPort CrearPuerto(TagProtocolSettings config, string? puerto = null) =>
            new SerialPort(puerto ?? config.ComPort, config.BaudRate) {
                DataBits = config.SerialDataBits,
                Parity = config.SerialParity,
                StopBits = config.SerialStopBits,
                Handshake = config.SerialHandshake,
                RtsEnable = config.SerialRtsEnable,
                DtrEnable = config.SerialDtrEnable,
                ReadTimeout = config.SerialReadTimeoutMs,
                WriteTimeout = config.SerialWriteTimeoutMs,
                ReadBufferSize = config.SerialReadBufferSize,
                WriteBufferSize = config.SerialWriteBufferSize,
                // Latin1 (no ASCII/UTF-8, el default de SerialPort es ASCII):
                // mapea cada byte 0-255 a su mismo code point sin tocarlo. El
                // evento 'e' (EstadoPartidaDecoder) es binario con todos sus
                // bytes >= 0x80 a propósito; con ASCII se convertirían en '?'
                // y con UTF-8 podrían no formar secuencias válidas, perdiendo
                // los datos antes de que lleguen a nuestro parser.
                Encoding = System.Text.Encoding.Latin1
            };

        public Task StartAsync(CancellationToken cancellationToken) {
            _escucha = ListenAsync(_parada.Token);
            _persistencia = PersistLoopAsync();
            return Task.CompletedTask;
        }

        // Parada ordenada: se deja de leer el puerto, se procesan los eventos
        // que ya estaban en cola y se espera a que se guarden las partidas
        // terminadas (o vayan al fichero de pendientes). Antes se cerraban los
        // dos canales a la vez y una partida recién terminada se podía perder
        // sin dejar rastro. Si el host corta la espera, se avisa.
        public async Task StopAsync(CancellationToken cancellationToken) {
            Stop();
            try {
                await Task.WhenAll(_escucha, _persistencia).WaitAsync(cancellationToken);
                _logger.LogInformation("Receptor serie (protocolo tag) detenido de manera ordenada.");
            }
            catch (OperationCanceledException) {
                _logger.LogWarning("Receptor serie (protocolo tag) detenido sin terminar de guardar las partidas pendientes (el cierre no esperó más).");
            }
        }

        public async Task ListenAsync(CancellationToken ct) {
            try {
                await AbrirPuertoAsync(ct);
                _logger.LogInformation("Puerto serie (protocolo tag) abierto: {port} a {baud} baudios.", _config.ComPort, _config.BaudRate);

                await ProcessChannelAsync();
            }
            catch (OperationCanceledException) {
                _logger.LogInformation("Receptor serie (protocolo tag) detenido.");
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error al abrir el puerto serie (protocolo tag) {port}.", _config.ComPort);
            }
            finally {
                Stop();
                // Ya no llegarán más partidas: PersistLoopAsync termina en
                // cuanto guarde las que queden.
                _persistChannel.Writer.TryComplete();
            }
        }

        private static readonly TimeSpan EsperaReintentoPuerto = TimeSpan.FromSeconds(10);

        // Hasta la 1.0.19, si el puerto no se podía abrir Gregal anotaba el
        // error y se quedaba abierto sin recibir nada: una consola con pinta
        // de estar funcionando (salas, 2026-11-10/11, con Gregal arrancado dos
        // veces). Ahora sigue intentándolo: el puerto puede estar ocupado por
        // otro programa o no existir aún (adaptador USB que tarda en aparecer
        // tras dar la luz). Un nombre de puerto mal escrito (ArgumentException)
        // no se arregla reintentando: sale por el catch general.
        private async Task AbrirPuertoAsync(CancellationToken ct) {
            for (int intento = 1; ; intento++) {
                ct.ThrowIfCancellationRequested(); // Stop() ya ha cerrado el puerto
                try {
                    _port!.Open();
                    if (intento > 1)
                        _logger.LogInformation("El puerto {port} ya se ha podido abrir (intento {intento}).", _config.ComPort, intento);
                    return;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) {
                    string motivo = ex is UnauthorizedAccessException
                        ? "ocupado por otro programa"
                        : "no existe o no responde";
                    // Sin la traza: en la consola la lee gente no técnica, y el
                    // motivo ya va en el mensaje.
                    if (intento == 1)
                        _logger.LogError("NO SE RECIBEN PARTIDAS: no se puede abrir el puerto serie {port} ({motivo}: {detalle}). Se reintenta cada {segundos} s.",
                            _config.ComPort, motivo, ex.Message, (int)EsperaReintentoPuerto.TotalSeconds);
                    else if (intento % 30 == 0)
                        _logger.LogError("NO SE RECIBEN PARTIDAS: el puerto serie {port} sigue sin poder abrirse ({motivo}; {intento} intentos).",
                            _config.ComPort, motivo, intento);
                    await Task.Delay(EsperaReintentoPuerto, ct);
                }
            }
        }

        public void Stop() {
            lock (_stopLock) {
                if (_detenido) return;
                _detenido = true;

                // Solo el primer canal: ProcessChannelAsync acaba con lo que ya
                // tenía en cola y ListenAsync cierra el segundo al terminar.
                _messageChannel.Writer.TryComplete();
                _parada.Cancel();

                if (_port == null) return;

                try {
                    if (_port.IsOpen) {
                        _port.DataReceived -= OnDataReceived;
                        _port.Close();
                        _logger.LogInformation("Puerto serie (protocolo tag) cerrado.");
                    }
                    _port.Dispose();
                }
                catch (Exception ex) {
                    // Bug conocido de System.IO.Ports: cerrar/disponer un
                    // SerialPort puede lanzar una NullReferenceException
                    // interna (sobre todo con puertos virtuales tipo
                    // com0com). No es un fallo nuestro recuperable, así que
                    // solo se registra para no tumbar el apagado del host.
                    _logger.LogWarning(ex, "Error (no crítico) cerrando el puerto serie (protocolo tag).");
                }
                finally {
                    _port = null;
                }
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e) {
            lock (_lecturaLock) {
                var port = _port;
                if (port == null || !port.IsOpen) return;
                try {
                    string incoming = port.ReadExisting();
                    if (string.IsNullOrEmpty(incoming)) return;

                    _accumulator += incoming;

                    if (BufferExcedido()) return;

                    ExtractAndEnqueueMessages();
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Error leyendo del puerto serie (protocolo tag).");
                }
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

        private void ExtractAndEnqueueMessages() {
            while (_accumulator.Contains(Delimiter)) {
                int idx = _accumulator.IndexOf(Delimiter);
                string trozo = _accumulator[..idx];
                _accumulator = _accumulator[(idx + 1)..];

                var troceo = TagEventParser.Trocear(trozo);

                if (troceo.Basura != null) {
                    string visible = TagEventParser.Visible(troceo.Basura);
                    if (_resincronizar) {
                        _logger.LogDebug("Descartado el resto de un evento ya empezado al (re)sincronizar: '{basura}'", visible);
                    }
                    else if (TagEventParser.SoloControl(troceo.Basura)) {
                        // Llega dos veces por segundo en espera: se avisa una
                        // vez por arranque y el resto solo en Debug.
                        if (_avisadoBytesControl) {
                            _logger.LogDebug("Descartados bytes de control sin '*': '{basura}'", visible);
                        }
                        else {
                            _avisadoBytesControl = true;
                            _logger.LogInformation("Descartados bytes de control sin '*': '{basura}'. Si se repiten, ya no se anotan.", visible);
                        }
                    }
                    else {
                        _logger.LogWarning("Descartados datos sin '*' de inicio de evento: '{basura}' (posible byte perdido en la línea serie).", visible);
                    }
                }
                if (troceo.Eventos.Count > 1) {
                    _logger.LogWarning("Varios eventos sin '#' entre ellos en '{trozo}': se separan por '*' (posible byte perdido en la línea serie).", trozo);
                }
                if (troceo.Basura != null || troceo.Eventos.Count > 0) _resincronizar = false;

                foreach (string evento in troceo.Eventos) {
                    _messageChannel.Writer.TryWrite(evento);
                }
            }
        }

        // *f# y *~...# sin partida en marcha: un MCB los repite dos veces por
        // segundo mientras espera (unos 80 MB de log al día). Con una partida
        // abierta sí se anotan: un *f# ahí significa que se perdió su *F#.
        private bool EsEsperaEntrePartidas(TagEvent? evt) =>
            evt is { Code: 'f' or '~' } && !_acumulador.HayPartidaEnCurso;

        // Solo trocea/acumula eventos; nunca espera a Firebird (eso lo hace
        // PersistLoopAsync, en paralelo) para no frenar la lectura serie si
        // llegan varias partidas seguidas.
        // Sin token: termina cuando Stop() completa el canal, después de
        // procesar lo que ya estaba en cola.
        private async Task ProcessChannelAsync() {
            await foreach (string rawFragment in _messageChannel.Reader.ReadAllAsync()) {
                try {
                    var evt = TagEventParser.Parse(rawFragment);

                    if (_settings.CurrentValue.LogRawFrames && !EsEsperaEntrePartidas(evt)) {
                        // Transcripción fiel de lo que emite el hardware, útil en un
                        // despliegue nuevo para confirmar el protocolo real antes de
                        // fiarse del mapeo asumido. Desactivable (TagProtocolSettings.
                        // LogRawFrames) una vez confirmado, porque es una línea por evento.
                        _logger.LogInformation("Fragmento tag recibido: '{frag}'", rawFragment);
                    }

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
        private async Task PersistLoopAsync() {
            await foreach (HistoricoDTO dto in _persistChannel.Reader.ReadAllAsync()) {
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
                string fullPath = _settings.CurrentValue.FallbackFileFullPath;

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
