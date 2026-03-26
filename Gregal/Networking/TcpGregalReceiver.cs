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
            var port = _network.CurrentValue.TcpPort;
            var address = IPAddress.Parse(_network.CurrentValue.TcpAddress);
            _server = new TcpListener(address, port);
            _server.Start();

            _logger.LogInformation("Servidor TCP iniciado en {address}:{port}", address, port);

            while (!ct.IsCancellationRequested) {
                try {
                    var client = await _server.AcceptTcpClientAsync(ct);
                    /* _ = "discard" ¿Qué hace exactamente?
                   ¿Qué hace exactamente?
                    Le indica al compilador: "Ejecuta esta tarea en segundo plano, pero no te quedes esperando a que termine".
                    Puntos clave para tu comentario:

                    Fuego y Olvido (Fire & Forget): Lanza el proceso de atención al cliente y libera inmediatamente el bucle principal (while) para que pueda aceptar al siguiente cliente sin demora.

                    Evita Advertencias: Sin el _ =, el compilador de C# te daría un aviso (Warning) diciendo que estás llamando a un método asíncrono sin usar await.

                    Independencia: Permite que cada cliente tenga su propio "hilo" de ejecución lógico. Si un cliente tarda mucho en procesar sus datos, no bloquea la entrada de nuevos usuarios.*/
                    _ = HandleIncomingConnection(client, ct);
                }
                catch (OperationCanceledException)
                { 
                    break;
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
                if (doc.RootElement.TryGetProperty("msg_id", out var idProp)) {
                    currentMsgId = idProp.GetString();
                }

                var historico = _parser.ProcessRawData(rawData);
                if (historico == null) {
                    // Aquí se usa StatusError (parámetro status) y ErrParsing (parámetro detail)
                    return GregalProtocol.CreateResponse(GregalProtocol.StatusError, currentMsgId, GregalProtocol.ErrParsing);
                }

                _repo.Insertar(historico);

                // Respuesta de éxito: usa StatusOk
                return GregalProtocol.CreateResponse(GregalProtocol.StatusOk, currentMsgId);
            }
            catch (Exception ex) {
                _logger.LogError("Fallo en procesamiento/BD: {msg}", ex.Message);
                // Respuesta de error de BD: usa StatusError y ErrDatabase
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