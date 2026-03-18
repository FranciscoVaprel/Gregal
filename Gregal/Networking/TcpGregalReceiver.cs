using Gregal.Config;
using Gregal.Data;

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
                    _ = HandleIncomingConnection(client, ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) {
                    _logger.LogError(ex, "Error en receptor TCP");
                }
            }
        }

        private async Task HandleIncomingConnection(TcpClient client, CancellationToken ct) {
            try {
                var remoteIp = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();
                if (!IsIpAuthorized(remoteIp)) {
                    client.Close();
                    return;
                }

                // Aquí va tu lógica de lectura (StreamReader, etc.)
                // y llamada al _parser y _repo
            }
            finally {
                client.Close();
            }
        }

        private bool IsIpAuthorized(string ip) {
            var allowedIps = _network.CurrentValue.AllowedIps;
            return allowedIps == null || allowedIps.Count == 0 || allowedIps.Contains(ip);
        }

        public void Stop() => _server?.Stop();
    }
}