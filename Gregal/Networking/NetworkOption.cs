public class NetworkOptions {
    public int Port { get; set; }
    public List<string> AllowedIps { get; set; } = new();
}