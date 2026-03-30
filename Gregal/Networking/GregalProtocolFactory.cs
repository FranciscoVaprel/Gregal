using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gregal.Protocol {
    public static class GregalProtocolFactory {
        private static readonly JsonSerializerOptions _options = new() {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // Crear un PONG
        public static string CreatePong(string msgId)
            => Serialize(new GregalResponse { Status = "PONG", MsgId = msgId });

        // Crear un SUCCESS
        public static string CreateSuccess(string msgId)
            => Serialize(new GregalResponse { Status = "SUCCESS", MsgId = msgId });

        // Crear un ERROR (como el del Buffer que pediste)
        public static string CreateError(string detail, string? msgId = null)
            => Serialize(new GregalResponse { Status = "ERROR", Detail = detail, MsgId = msgId });

        private static string Serialize<T>(T obj) => JsonSerializer.Serialize(obj, _options);
    }
}