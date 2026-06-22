namespace Gregal.Protocol {
    public static class GregalProtocol {
        public enum MessageStatus {
            SUCCESS,
            ERROR,
            PONG
        }

        public enum ErrorCode {
            FORMAT_INVALID,
            DATABASE_REJECTED,
            INTERNAL_SERVER_ERROR,
            BUFFER_EXCEEDED,
            UNKNOWN_OPERATION
        }
    }
}