using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gregal.Protocol {
    public static class GregalProtocolFactory {
        // Configuración para que el JSON sea limpio (no envíe nulos si no es necesario)
        private static readonly JsonSerializerOptions _options = new() {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase // Opcional, según prefieras
        };

        // --- Creadores de Mensajes ---

        public static string CreatePong(string msgId) {
            return Serialize(new GregalResponse { Status = "PONG", MsgId = msgId });
        }

        public static string CreateSuccess(string msgId) {
            return Serialize(new GregalResponse { Status = "SUCCESS", MsgId = msgId });
        }

        public static string CreateError(string detail, string? msgId = null) {
            return Serialize(new GregalResponse {
                Status = "ERROR",
                Detail = detail,
                MsgId = msgId
            });
        }

        // --- Utilidades ---

        private static string Serialize(GregalResponse response) {
            // Añadimos el delimitador \0 al final para que el Cliente sepa que el mensaje terminó
            return JsonSerializer.Serialize(response, _options) + "\0";
        }

        /// <summary>
        /// Intenta deserializar una cadena JSON a un objeto de petición.
        /// </summary>
        public static GregalRequest? ParseRequest(string json) {
            try {
                return JsonSerializer.Deserialize<GregalRequest>(json, _options);
            }
            catch {
                return null;
            }
        }
    }
}