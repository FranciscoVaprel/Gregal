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
            byte[] buffer = new byte[4096];
            string accumulator = string.Empty;

            while (client.Connected && !ct.IsCancellationRequested) {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                if (bytesRead == 0) break;

                accumulator += System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);

                if (accumulator.Length > 102400) {
                    _logger.LogWarning("Buffer excedido para {ip}. Limpiando.", remoteIp);
                    accumulator = string.Empty;
                    continue;
                }

                accumulator = await ExtractAndProcessMessages(accumulator, stream, remoteIp);
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
                    _logger.LogDebug("Keep-alive recibido de {ip}", remoteIp);

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