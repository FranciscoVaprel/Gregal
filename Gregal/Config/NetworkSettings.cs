namespace Gregal.Config;

public class NetworkSettings {
    public string Mode { get; set; } = "TCP";
    public int TcpPort { get; set; }
    public string TcpAddress { get; set; } = "127.0.0.1";
    public List<string> AllowedIps { get; set; } = new();


    public int TcpKeepAliveTime { get; set; } = 60; 
    public int TcpKeepAliveInterval { get; set; } = 10;

    public string ComPort { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    
    public int TcpReadTimeoutMinutes { get; set; } = 5;
    public int TcpMaxBufferChars { get; set; } = 102400;

}