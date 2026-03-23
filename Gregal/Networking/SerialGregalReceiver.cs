using System.IO.Ports; // Recuerda instalar el paquete NuGet System.IO.Ports

namespace Gregal.Networking {
    public class SerialGregalReceiver : IGregalReceiver {
        private SerialPort? _serialPort;
        private readonly ILogger<SerialGregalReceiver> _logger;
        // ... otros campos como el parser o la configuración

        public SerialGregalReceiver(ILogger<SerialGregalReceiver> logger) {
            _logger = logger;
        }

        public async Task ListenAsync(CancellationToken ct) {
            // 1. Aquí abrirías el puerto (COM1, 9600, etc.)
            // 2. Te suscribes al evento de recepción de datos
            // 3. Mantienes la tarea viva mientras no se cancele
            await Task.Delay(Timeout.Infinite, ct);
        }

        public void Stop() {
            if (_serialPort != null && _serialPort.IsOpen) {
                _serialPort.Close();
            }
        }
    }
}