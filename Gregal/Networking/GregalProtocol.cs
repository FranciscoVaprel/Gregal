namespace Gregal.Networking {
    public static class GregalProtocol {
        // Respuestas del Servidor
        public const string Success = "OK";
        public const string ErrorParsing = "ERR_PARSING";
        public const string ErrorDatabase = "ERR_DB";
        public const string ErrorInternal = "ERR_INTERNAL";

        // Formatos y Delimitadores
        public const string Terminator = "\r\n";

        // Método de ayuda para formatear la respuesta
        public static byte[] ToBytes(string message) {
            return System.Text.Encoding.UTF8.GetBytes(message + Terminator);
        }
    }
}