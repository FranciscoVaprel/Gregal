using Gregal.Config;
using Gregal.Data;
using Gregal.Models;
using Gregal.Parsing;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;

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
                _logger.LogInformation("Cliente conectado: {ip}", remoteIp);

                using var stream = client.GetStream();
                using var reader = new StreamReader(stream);

                // Bucle de persistencia para mantener la conexión abierta
                while (client.Connected && !ct.IsCancellationRequested) {
                    string? rawData = await reader.ReadLineAsync(ct);
                    if (rawData == null) break;

                    if (!string.IsNullOrWhiteSpace(rawData)) {
                        // Obtenemos el resultado del procesamiento
                        string response = await ProcessMessageAsync(rawData, remoteIp);

                        // Enviamos la respuesta de vuelta al emisor (Hercules)
                        byte[] responseData = System.Text.Encoding.UTF8.GetBytes(response + "\r\n");
                        await stream.WriteAsync(responseData, 0, responseData.Length, ct);
                    }
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error fatal en la sesión con el cliente {ip}", remoteIp);
            }
            finally {
                client.Close();
                _logger.LogInformation("Conexión con {ip} finalizada.", remoteIp);
            }
        }

        // 2. Ajustamos ProcessMessageAsync para que retorne el estado
        private async Task<string> ProcessMessageAsync(string rawData, string remoteIp) {
            try {
                var historico = _parser.ProcessRawData(rawData);

                if (historico == null) return GregalProtocol.ErrorParsing;

                return ExecuteDatabaseInsert(historico);
            }
            catch (Exception) {
                return GregalProtocol.ErrorInternal;
            }
        }

        // 3. Ajustamos ExecuteDatabaseInsert para detectar errores de BD
        private string ExecuteDatabaseInsert(Historico historico) {
            try {
                _repo.Insertar(historico);
                _logger.LogInformation("Registro insertado. Sala: {sala}", historico.COD_SALA);
                return GregalProtocol.ErrorDatabase;
            }
            catch (Exception ex) {
                _logger.LogError("Error de BD: {msg}", ex.Message);
                return GregalProtocol.ErrorDatabase;
            }
        }

        private bool IsIpAuthorized(string ip) {
            var allowedIps = _network.CurrentValue.AllowedIps;
            return allowedIps == null || allowedIps.Count == 0 || allowedIps.Contains(ip);
        }

        public void Stop() => _server?.Stop();
    }
}