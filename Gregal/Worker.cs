using Microsoft.Extensions.Options;
using Gregal.Config;
using Gregal.Parsing;
using Gregal.Data;

namespace Gregal {

    public class Worker : BackgroundService {
        
        private readonly ILogger<Worker> _logger;
        private readonly GregalParserEngine _parser;
        private readonly HistoricoRepository _repo;
        private readonly IOptionsMonitor<GregalSettings> _settings;
        
        public Worker(
            ILogger<Worker> logger,
            GregalParserEngine parser,
            HistoricoRepository repo,
            IOptionsMonitor<GregalSettings> settings) {
            _logger = logger;
            _parser = parser;
            _repo = repo;
            _settings = settings;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            _logger.LogInformation(">>> Gregal ha arrancado correctamente.");

            while (!stoppingToken.IsCancellationRequested) {
                // Ejemplo de uso:
                var puerto = _settings.CurrentValue.TcpPort;
                _logger.LogInformation("Escuchando en el puerto: {p}", puerto);

                // Esperamos 5 segundos antes de repetir el bucle
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}