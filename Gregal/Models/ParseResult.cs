namespace Gregal.Models {
    public class ParseResult {
        public HistoricoDTO? Data { get; set; }
        public string MsgId { get; set; } = "unknown";
        public bool IsPing { get; set; }
    }
}