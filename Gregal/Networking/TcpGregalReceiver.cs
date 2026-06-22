using Gregal.Config;
using Gregal.Data;
using Gregal.Parsing;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using static Gregal.Protocol.GregalProtocol;

namespace Gregal.Networking {
    public class TcpGregalReceiver : IHostedService {
        private readonly ILogger<TcpGregalReceiver> _logger;
        private readonly GregalParserEngine _parser;
        private readonly IOptionsMonitor<NetworkSettings> _network;        
        private readonly HistoricoRepository _repo;
        private TcpListener? _server;
        
        private static readonly JsonSerializerOptions _options = new() {
            PropertyNameCaseInsensitive = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="network"></param>
        /// <param name="parser"></param>
        /// <param name="repo"></param>
        public TcpGregalReceiver(
            ILogger<TcpGregalReceiver> logger,
            IOptionsMonitor<NetworkSettings> network,
            GregalParserEngine parser,
            HistoricoRepository repo) {
            _logger = logger;
            _network = network;
            _parser = parser;
            _repo = repo;
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


        /// <summary>        
        ///    _server.Start(): El sistema operativo reserva el puerto(ej. 5000). A partir de aquí, el servidor es visible en la red.

        ///    await _server.AcceptTcpClientAsync(ct): El código se detiene aquí.No consume CPU, simplemente espera. Cuando un cliente se conecta, se "despierta" y nos entrega un objeto client.
        ///    SetSocketOption (KeepAlive): Activa los pulsos de mantenimiento.
        ///    TcpKeepAliveTime: Define cuánto tiempo de silencio debe pasar antes de enviar el primer pulso (ej. 30 segundos).
        /// TcpKeepAliveInterval: Si el primer pulso no tiene respuesta, define cada cuánto tiempo reintentar antes de dar la conexión por muerta.
        /// _ = HandleIncomingConnection(...): Esto es fundamental.El símbolo _ = (discard)significa: "Inicia la gestión de este cliente en un hilo aparte y vuelve inmediatamente al while para aceptar al siguiente". Sin esto, tu servidor solo podría atender a una persona a la vez.
        /// </summary> 

        public async Task ListenAsync(CancellationToken ct) {
            var settings = _network.CurrentValue;
            var port = settings.TcpPort;
            var address = IPAddress.Parse(settings.TcpAddress);

            _server = new TcpListener(address, port);
            _server.Start();

            _logger.LogInformation("Servidor TCP iniciado en {address}:{port}", address, port);

            while (!ct.IsCancellationRequested) {
                try {
                    var client = await _server.AcceptTcpClientAsync(ct);

                    // Aplicamos Keep-Alive usando los valores de la configuración[cite: 2]
                    client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, settings.TcpKeepAliveTime);
                    client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, settings.TcpKeepAliveInterval);

                    _ = HandleIncomingConnection(client, ct);
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Error en receptor TCP");
                }
            }
        }

        private async Task HandleIncomingConnection(TcpClient client, CancellationToken ct) {
            string remoteIp = "Desconocida";
            try {
                remoteIp = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();

                // REINTEGRACIÓN DE SEGURIDAD: Validamos la IP antes de hacer nada
                if (!IsIpAuthorized(remoteIp)) {
                    _logger.LogWarning("Intento de conexión bloqueado: {ip} no está autorizada.", remoteIp);
                    return; // Cerramos la conexión inmediatamente
                }

                _logger.LogInformation("Cliente autorizado y conectado: {ip}", remoteIp);
                await ReadStreamUntilClosed(client, remoteIp, ct);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error crítico en sesión con {ip}", remoteIp);
            }
            finally {
                client.Close();
                _logger.LogInformation("Conexión con {ip} cerrada.", remoteIp);
            }
        }
         
        private async Task ReadStreamUntilClosed(TcpClient client, string remoteIp, CancellationToken ct) {
            using var stream = client.GetStream();

            // LEEMOS DE CONFIGURACIÓN
            var settings = _network.CurrentValue;
            stream.ReadTimeout = (int)TimeSpan.FromMinutes(settings.TcpReadTimeoutMinutes).TotalMilliseconds;

            byte[] buffer = new byte[4096];
            string accumulator = string.Empty;

            try {
                _logger.LogDebug("Sesión iniciada con {ip}. Timeout de lectura: {min} min.", remoteIp, settings.TcpReadTimeoutMinutes);

                while (client.Connected && !ct.IsCancellationRequested) {
                    // ReadAsync utilizará el CancellationToken para salir si el servidor se apaga
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);

                    if (bytesRead == 0) {
                        _logger.LogInformation("El cliente {ip} ha finalizado la conexión de forma ordenada.", remoteIp);
                        break;
                    }

                    accumulator += System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    
                    if (accumulator.Length > settings.TcpMaxBufferChars) {
                        _logger.LogWarning("X ADVERTENCIA: Trama de red excedida. Tamaño: {len} bytes. IP: {ip} (Límite: {max})",
                            accumulator.Length, remoteIp, settings.TcpMaxBufferChars);

                        // 1. Intentamos avisar al cliente antes de limpiar
                        try {
                            // Intentamos extraer un msg_id si es posible, si no, enviamos uno genérico
                            string errorResponse = GregalProtocolFactory.CreateError(ErrorCode.BUFFER_EXCEEDED, "unknown", "El mensaje excede el tamaño máximo");
                            byte[] errorData = System.Text.Encoding.UTF8.GetBytes(errorResponse);
                            await stream.WriteAsync(errorData, 0, errorData.Length, ct);
                        }
                        catch { /* Si el socket ya falló, no importa */ }

                        // 2. Limpiamos y seguimos
                        accumulator = string.Empty;
                        continue;
                    }
                    
                    accumulator = await ExtractAndProcessMessages(accumulator, stream, remoteIp, ct);
                }
            }
            catch (OperationCanceledException) {
                _logger.LogInformation("Operación cancelada (Apagado del servidor) para {ip}.", remoteIp);
            }
            catch (IOException ex) when (ex.InnerException is SocketException sx &&
                                       (sx.SocketErrorCode == SocketError.TimedOut || sx.SocketErrorCode == SocketError.ConnectionReset)) {
                _logger.LogWarning("Conexión con {ip} cerrada por INACTIVIDAD ({min} min sin datos/ping) o fallo de red.", remoteIp, settings.TcpReadTimeoutMinutes);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error inesperado con {ip}", remoteIp);
            }
            finally {
                client.Close();
                _logger.LogInformation("Recursos liberados para la sesión de {ip}.", remoteIp);
            }
        }

        private async Task<string> ExtractAndProcessMessages(string accumulator, NetworkStream stream, string remoteIp, CancellationToken ct) {
            // Buscamos el delimitador \0 definido en el protocolo
            while (accumulator.Contains('\0')) {
                int index = accumulator.IndexOf('\0');
                string rawMessage = accumulator.Substring(0, index).Trim();
                accumulator = accumulator.Substring(index + 1);

                if (!string.IsNullOrWhiteSpace(rawMessage)) {
                    // Procesamos el mensaje y obtenemos la respuesta JSON ya formateada
                    string responseJson = await ProcessMessageAsync(rawMessage, remoteIp);
                    _logger.LogDebug("ENVIANDO AL CLIENTE ({ip}): {data}", remoteIp, responseJson);
                    // Enviamos la respuesta al cliente
                    byte[] responseBytes = System.Text.Encoding.UTF8.GetBytes(responseJson);
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length, ct);
                }
            }
            return accumulator;
        }

        private async Task<string> ProcessMessageAsync(string rawData, string remoteIp) {
            string msgId = "unknown";

            try {
                // 'result' es de tipo ParseResult, no HistoricoDTO
                var result = _parser.Parse(rawData);

                if (result == null) {
                    _logger.LogWarning("X FORMATO INVALIDO desde {ip}", remoteIp);
                    return GregalProtocolFactory.CreateError(ErrorCode.FORMAT_INVALID, msgId);
                }

                msgId = result.MsgId;

                if (result.IsPing) return GregalProtocolFactory.CreatePong(msgId);

                if (result.Data != null) {
                    // Pasamos el DTO al repositorio como siempre
                    bool ok = await _repo.SaveHistoricoAsync(result.Data);

                    return ok
                        ? GregalProtocolFactory.CreateSuccess(msgId)
                        : GregalProtocolFactory.CreateError(ErrorCode.DATABASE_REJECTED, msgId);
                }

                return GregalProtocolFactory.CreateError(ErrorCode.UNKNOWN_OPERATION, msgId);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "X ERROR con {ip}", remoteIp);
                return GregalProtocolFactory.CreateError(ErrorCode.INTERNAL_SERVER_ERROR, msgId);
            }
        }                

        private bool IsIpAuthorized(string ip) {
            var allowedIps = _network.CurrentValue.AllowedIps;
            return allowedIps == null || allowedIps.Count == 0 || allowedIps.Contains(ip);
        }
        
        public void Stop() => _server?.Stop();
    }
}