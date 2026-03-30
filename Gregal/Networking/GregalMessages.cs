using System.Text.Json.Serialization;
using Gregal.Models;

namespace Gregal.Protocol {
    // --- BASE DE COMUNICACIÓN ---
    public class GregalMessage {
        [JsonPropertyName("msg_id")]
        public string MsgId { get; set; } = string.Empty;

        [JsonPropertyName("operation")]
        public string? Operation { get; set; }
    }

    // --- MENSAJES DE ENTRADA (Lo que recibe el Servidor) ---
    public class UpsertRequest : GregalMessage {
        [JsonPropertyName("data")]
        public HistoricoDTO? Data { get; set; }
    }
    

    // --- MENSAJES DE SALIDA (Lo que responde el Servidor) ---
    public class GregalResponse {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("msg_id")]
        public string? MsgId { get; set; }

        [JsonPropertyName("detail")]
        public string? Detail { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}