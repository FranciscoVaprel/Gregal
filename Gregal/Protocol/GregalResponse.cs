using System.Text.Json.Serialization;
using static Gregal.Protocol.GregalProtocol;

public class GregalResponse {
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))] // Para que en el JSON salga "Success" y no "0"
    public MessageStatus Status { get; set; }

    [JsonPropertyName("msg_id")]
    public string? MsgId { get; set; }

    [JsonPropertyName("error_code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ErrorCode? Error { get; set; }

    [JsonPropertyName("detail")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; set; }

    public string Timestamp { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}