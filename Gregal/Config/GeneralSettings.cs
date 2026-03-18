namespace Gregal.Config {
    public class GeneralSettings {
        public string LogPath { get; set; } = "logs/gregal.log";
        public bool EnableAutoRefresh { get; set; }
        public string CsvSeparator { get; set; } = ";";
    }

    
}