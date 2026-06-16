namespace Gregal.Networking {
    public interface IGregalReceiver {
        Task ListenAsync(CancellationToken ct);
        void Stop();
    }
}
