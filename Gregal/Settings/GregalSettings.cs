namespace Gregal.Config {
    public class GregalSettings {
        // Configuración de Base de Datos
        public string ConnectionString { get; set; } = string.Empty;

        // Configuración de Red
        public int TcpPort { get; set; } = 5000;
        public string TcpAddress { get; set; } = "127.0.0.1";

        // Configuración de Negocio
        public string LogPath { get; set; } = "logs/gregal.log";
        public bool EnableAutoRefresh { get; set; } = true;

        // Método para verificar que todo es correcto
        public bool IsValid() {
            return !string.IsNullOrEmpty(ConnectionString) && TcpPort > 0;
        }
    }
}