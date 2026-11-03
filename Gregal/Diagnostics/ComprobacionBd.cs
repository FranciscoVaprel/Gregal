using FirebirdSql.Data.FirebirdClient;
using Gregal.Config;
using Gregal.Data;

namespace Gregal.Diagnostics;

// "Gregal.exe --comprobar-bd": con la configuración de appsettings.json y el
// mismo conector que el servicio, comprueba que Firebird responde, que existe
// la tabla destino y que tiene todas las columnas que Gregal escribe
// (FilaHistorico.Columnas). Lo usa Instalar-Gregal.ps1 al configurar una sala,
// para no descubrir el fallo con la primera partida. Código de salida 0 si
// todo está bien.
internal static class ComprobacionBd {
    public static int Ejecutar(IConfiguration configuration) {
        var settings = configuration.GetSection("DatabaseSettings").Get<DatabaseSettings>() ?? new DatabaseSettings();
        try {
            // Solo servidor, puerto y fichero: la cadena lleva la contraseña.
            var cadena = new FbConnectionStringBuilder(settings.ConnectionString);
            Console.WriteLine($"Base de datos: {cadena.Database} (servidor {cadena.DataSource}, puerto {cadena.Port})");

            string tabla = FilaHistorico.ValidarNombreTabla(settings.TableName);
            using var conn = new FbConnection(settings.ConnectionString);
            conn.Open();

            var existentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = new FbCommand(
                       "SELECT TRIM(RDB$FIELD_NAME) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME = @tabla", conn)) {
                cmd.Parameters.AddWithValue("@tabla", tabla);
                using var rd = cmd.ExecuteReader();
                while (rd.Read()) existentes.Add(rd.GetString(0));
            }

            if (existentes.Count == 0) {
                Console.WriteLine($"ERROR: conecta con Firebird {conn.ServerVersion}, pero la base de datos no tiene la tabla {tabla}.");
                return 2;
            }
            var faltan = FilaHistorico.Columnas.Where(c => !existentes.Contains(c)).ToList();
            if (faltan.Count > 0) {
                Console.WriteLine($"ERROR: a la tabla {tabla} le faltan columnas que Gregal escribe: {string.Join(", ", faltan)}.");
                return 3;
            }
            Console.WriteLine($"OK: conecta con Firebird {conn.ServerVersion} y la tabla {tabla} tiene todas las columnas.");
            return 0;
        } catch (Exception ex) {
            Console.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }
}
