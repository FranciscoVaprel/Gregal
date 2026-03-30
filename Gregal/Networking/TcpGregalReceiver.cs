using Gregal.Config;
using Gregal.Data;
using Gregal.Models;
using Gregal.Parsing;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Gregal.Networking {
    public class TcpGregalReceiver : IGregalReceiver {
        private readonly ILogger<TcpGregalReceiver> _logger;
        private readonly IOptionsMonitor<NetworkSettings> _network;
        private readonly GregalParserEngine _parser;
        private readonly HistoricoRepository _repo;
        private TcpListener? _server;

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

        public async Task ListenAsync(CancellationToken ct) {
            var settings = _network.CurrentValue; // Acceso a la config[cite: 2]
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
                        _logger.LogWarning("Inundación detectada en {ip}. Max: {max}. Actual: {len}",
                            remoteIp, settings.TcpMaxBufferChars, accumulator.Length);

                        // 1. Intentamos avisar al cliente antes de limpiar
                        try {
                            // Intentamos extraer un msg_id si es posible, si no, enviamos uno genérico
                            string errorResponse = $"{{\"status\":\"{GregalProtocol.StatusError}\",\"detail\":\"{GregalProtocol.DetailBufferExceeded}\"}}\0";
                            byte[] errorData = System.Text.Encoding.UTF8.GetBytes(errorResponse);
                            await stream.WriteAsync(errorData, 0, errorData.Length, ct);
                        }
                        catch { /* Si el socket ya falló, no importa */ }

                        // 2. Limpiamos y seguimos
                        accumulator = string.Empty;
                        continue;
                    }

                    accumulator = await ExtractAndProcessMessages(accumulator, stream, remoteIp);
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

        private async Task<string> ExtractAndProcessMessages(string accumulator, NetworkStream stream, string remoteIp) {
            while (accumulator.Contains(GregalProtocol.MessageDelimiter)) {
                int index = accumulator.IndexOf(GregalProtocol.MessageDelimiter);
                string fullMessage = accumulator.Substring(0, index).Trim();
                accumulator = accumulator.Substring(index + 1);

                if (!string.IsNullOrWhiteSpace(fullMessage)) {
                    string response = await ProcessMessageAsync(fullMessage, remoteIp);
                    byte[] responseBytes = GregalProtocol.ToBytes(response);
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                }
            }
            return accumulator;
        }

        private async Task<string> ProcessMessageAsync(string rawData, string remoteIp) {
            string? currentMsgId = null;
            try {
                using var doc = JsonDocument.Parse(rawData);
                var root = doc.RootElement;

                if (root.TryGetProperty("msg_id", out var idProp)) {
                    currentMsgId = idProp.GetString();
                }

                // LÓGICA DE KEEPALIVE (PING/PONG)
                if (root.TryGetProperty("operation", out var opProp) &&
                    opProp.GetString()?.Equals("ping", StringComparison.OrdinalIgnoreCase) == true) {
                    _logger.LogInformation("PING recibido de {ip}", remoteIp);

                    // Usamos la constante oficial del protocolo
                    return GregalProtocol.CreateResponse(GregalProtocol.StatusPong, currentMsgId);
                }

                // LÓGICA DE NEGOCIO
                var historico = _parser.ProcessRawData(rawData);
                if (historico == null) {
                    return GregalProtocol.CreateResponse(GregalProtocol.StatusError, currentMsgId, GregalProtocol.ErrParsing);
                }

                _repo.Guardar(historico);
                return GregalProtocol.CreateResponse(GregalProtocol.StatusOk, currentMsgId);
            }
            catch (Exception ex) {
                _logger.LogError("Error en {ip}: {msg}", remoteIp, ex.Message);
                return GregalProtocol.CreateResponse(GregalProtocol.StatusError, currentMsgId, GregalProtocol.ErrDatabase);
            }
        }

        private bool IsIpAuthorized(string ip) {
            var allowedIps = _network.CurrentValue.AllowedIps;
            return allowedIps == null || allowedIps.Count == 0 || allowedIps.Contains(ip);
        }

        public void Stop() => _server?.Stop();
    }
}