namespace Gregal.Networking {
    public interface IGregalReceiver {
        internal Task ListenAsync(CancellationToken ct);
        internal void Stop();
    }
}
