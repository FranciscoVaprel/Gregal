namespace Gregal.Config {
    public class GeneralSettings {
        // Relativo a la carpeta logs junto a Gregal.exe (Program.cs ya añade
        // "logs"): de aquí salen el prefijo y la extensión de los ficheros.
        public string LogPath { get; set; } = "gregal.log";
    }
}
