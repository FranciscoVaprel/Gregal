using System.Text.Json;

namespace Gregal.Networking {
    public static class GregalProtocol {
        public const char MessageDelimiter = '\0';

        // Códigos de estado internos
        public const string StatusOk = "SUCCESS";
        public const string StatusError = "ERROR";
        public const string StatusPong = "PONG";

        // Tipos de error
        public const string ErrParsing = "FORMAT_INVALID";
        public const string ErrDatabase = "DATABASE_REJECTED";
        public const string ErrInternal = "INTERNAL_SERVER_ERROR";

        public static byte[] ToBytes(string json) =>
            System.Text.Encoding.UTF8.GetBytes(json + MessageDelimiter);

        public static string CreateResponse(string status, string? messageId, string? detail = null) {
            var responseObj = new {
                status = status,
                msg_id = messageId ?? "unknown",
                detail = detail,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            return JsonSerializer.Serialize(responseObj);
        }
    }
}