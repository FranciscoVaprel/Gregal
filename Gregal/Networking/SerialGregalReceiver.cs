namespace Gregal.Networking {
    public class SerialGregalReceiver : IGregalReceiver {
        public async Task ListenAsync(CancellationToken ct) {
            // Tu lógica de puerto serie aquí
            await Task.CompletedTask;
        }

        public void Stop() {
            // Tu lógica para cerrar el puerto aquí
        }
    }
}