namespace Gregal.Models {
    public class ParseResult {
        public HistoricoDTO? Data { get; set; }
        public string MsgId { get; set; } = "unknown";
        public bool IsPing { get; set; }

        // Motivo por el que Data se ha puesto a null pese a haberse podido deserializar el JSON/CSV
        // (p.ej. faltan campos de identificación obligatorios). Null si no hubo error de validación.
        public string? ValidationError { get; set; }
    }
}