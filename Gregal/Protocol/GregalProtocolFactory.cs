using System.Text.Json;
using static Gregal.Protocol.GregalProtocol;

public static class GregalProtocolFactory {
    public static string CreateSuccess(string msgId) =>
        Serialize(new GregalResponse { Status = MessageStatus.SUCCESS, MsgId = msgId });

    public static string CreateError(ErrorCode code, string msgId, string? detail = null) =>
        Serialize(new GregalResponse {
            Status = MessageStatus.ERROR,
            MsgId = msgId,
            Error = code,
            Detail = detail
        });

    public static string CreatePong(string msgId) =>
        Serialize(new GregalResponse { Status = MessageStatus.PONG, MsgId = msgId });

    private static string Serialize(GregalResponse resp) {
        string json = JsonSerializer.Serialize(resp) + "\0";

        // Aquí puedes poner un Console.WriteLine o un Debug.WriteLine
        System.Diagnostics.Debug.WriteLine($"[PROTOCOLO OUT]: {json}");
        return json;
    }
}