using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Gregal.Protocol {
    /// <summary>
    /// Estructura de respuesta que el Servidor envía al Cliente.
    /// </summary>
    public class GregalResponse {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("msg_id")]
        public string? MsgId { get; set; }

        [JsonPropertyName("detail")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Detail { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
