using System.Text.Json;

namespace Gregal.Protocol {
    public static class GregalProtocol {
        public enum MessageStatus {
            Success,
            Error,
            Pong
        }

        public enum ErrorCode {
            FormatInvalid,
            DatabaseRejected,
            InternalServerError,
            BufferExceeded,
            UnknownOperation
        }
    }
}