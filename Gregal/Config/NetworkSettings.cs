namespace Gregal.Config {
    public class NetworkSettings {
        public int TcpPort { get; set; }
        public string TcpAddress { get; set; } = "0.0.0.0";
        public List<string> AllowedIps { get; set; } = new();
    }
}