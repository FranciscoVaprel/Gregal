using Gregal.Networking;

public class Worker : BackgroundService {
    private readonly IGregalReceiver _receiver;
    private readonly ILogger<Worker> _logger;

    public Worker(IGregalReceiver receiver, ILogger<Worker> logger) {
        _receiver = receiver;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        // No importa si es TCP o RS232, el worker solo dice "escucha"
        await _receiver.ListenAsync(stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken) {
        _receiver.Stop();
        return base.StopAsync(cancellationToken);
    }
}