
using System;
using System.Collections.Generic;
using System.Text;

namespace Gregal.Networking {
    public interface IGregalReceiver {
        Task ListenAsync(CancellationToken ct);
        void Stop();
    }
}
