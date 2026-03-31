using System.Text.Json;
using static Gregal.Protocol.GregalProtocol;

public static class GregalProtocolFactory {
    public static string CreateSuccess(string msgId) =>
        Serialize(new GregalResponse { Status = MessageStatus.Success, MsgId = msgId });

    public static string CreateError(ErrorCode code, string msgId, string? detail = null) =>
        Serialize(new GregalResponse {
            Status = MessageStatus.Error,
            MsgId = msgId,
            Error = code,
            Detail = detail
        });

    public static string CreatePong(string msgId) =>
        Serialize(new GregalResponse { Status = MessageStatus.Pong, MsgId = msgId });

    private static string Serialize(GregalResponse resp) =>
        JsonSerializer.Serialize(resp) + "\0";
}