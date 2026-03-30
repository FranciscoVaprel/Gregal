namespace Gregal.Config;

public class NetworkSettings {
    public string Mode { get; set; } = "TCP";
    public int TcpPort { get; set; }
    public string TcpAddress { get; set; } = "127.0.0.1";
    public List<string> AllowedIps { get; set; } = new();

    // Nuevos campos para Keep-Alive
    public int TcpKeepAliveTime { get; set; } = 60;     // Valor por defecto
    public int TcpKeepAliveInterval { get; set; } = 10; // Valor por defecto

    public string ComPort { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
}