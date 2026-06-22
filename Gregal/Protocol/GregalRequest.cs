using Gregal.Models;
using System.Text.Json.Serialization;

namespace Gregal.Protocol {
    /// <summary>
    /// Estructura base para cualquier mensaje entrante del cliente.
    /// </summary>
    public class GregalRequest {
        [JsonPropertyName("msg_id")]
        public string MsgId { get; set; } = string.Empty;

        [JsonPropertyName("operation")]
        public string Operation { get; set; } = string.Empty;

        // Usamos el DTO que creamos antes
        [JsonPropertyName("data")]
        public HistoricoDTO? Data { get; set; }
    }

  
}