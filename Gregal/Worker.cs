using Gregal.Networking;

namespace Gregal {
    public class Worker : BackgroundService {
        private readonly ILogger<Worker> _logger;
        private readonly IGregalReceiver _receiver; // Interfaz común para TCP o RS232

        public Worker(ILogger<Worker> logger, IGregalReceiver receiver) {
            _logger = logger;
            _receiver = receiver;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            _logger.LogInformation("Iniciando el receptor configurado...");
            // Llama al método de la interfaz sin importarle si es TCP o Serial
            await _receiver.ListenAsync(stoppingToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken) {
            _receiver.Stop();
            await base.StopAsync(cancellationToken);
        }
    }
}