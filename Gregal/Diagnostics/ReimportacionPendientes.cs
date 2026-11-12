using System.Globalization;
using System.Text.Json;
using Gregal.Data;
using Gregal.Models;

namespace Gregal.Diagnostics;

// "Gregal.exe --reimportar-pendientes": vuelve a guardar en Firebird las
// partidas que TagSerialGregalReceiver dejó en el fichero de pendientes
// (TagProtocolSettings.FallbackFilePath, una partida en JSON por línea) porque
// la base de datos falló en su momento. Las que se guardan desaparecen del
// fichero; las que vuelven a fallar (y las líneas que no se entienden) se
// quedan en él para otro intento. Código de salida 0 si no queda ninguna
// pendiente, 2 si queda alguna, 1 si no se pudo ni empezar.
public static class ReimportacionPendientes {
    public sealed record Resultado(int Guardadas, int Fallidas, int Ilegibles, IReadOnlyList<string> LineasPendientes);

    public static async Task<int> EjecutarAsync(HistoricoRepository repo, string rutaFichero) {
        if (!File.Exists(rutaFichero)) {
            Console.WriteLine($"No hay partidas pendientes (no existe {rutaFichero}).");
            return 0;
        }

        // Se aparta el fichero antes de leerlo: si Gregal está en marcha y
        // deja una partida nueva mientras tanto, la escribe en un fichero
        // nuevo, y las que queden pendientes se le añaden al final. Así no se
        // pierde ninguna por pisar el fichero al reescribirlo.
        string enCurso = rutaFichero + ".reimportando";
        if (File.Exists(enCurso)) {
            Console.WriteLine($"ERROR: queda {enCurso} de una reimportación anterior que no terminó. " +
                              "Añade su contenido al final del fichero de pendientes y bórralo antes de repetir.");
            return 1;
        }
        try {
            File.Move(rutaFichero, enCurso);
        } catch (Exception ex) {
            Console.WriteLine($"ERROR: no se puede apartar {rutaFichero}: {ex.Message}");
            return 1;
        }

        Resultado resultado;
        try {
            string[] lineas = await File.ReadAllLinesAsync(enCurso);
            Console.WriteLine($"Partidas pendientes en {rutaFichero}: {lineas.Count(l => !string.IsNullOrWhiteSpace(l))}");
            resultado = await ProcesarAsync(lineas, repo.SaveHistoricoAsync, Console.WriteLine);
        } catch (Exception ex) {
            Console.WriteLine($"ERROR: {ex.Message}");
            File.Move(enCurso, rutaFichero, overwrite: false);
            return 1;
        }

        if (resultado.LineasPendientes.Count > 0) {
            await File.AppendAllLinesAsync(rutaFichero, resultado.LineasPendientes);
        }
        File.Delete(enCurso);

        Console.WriteLine();
        Console.WriteLine($"Guardadas en Firebird: {resultado.Guardadas}.");
        if (resultado.Fallidas > 0) Console.WriteLine($"Siguen pendientes (Firebird las ha rechazado otra vez): {resultado.Fallidas}.");
        if (resultado.Ilegibles > 0) Console.WriteLine($"Líneas que no se entienden (se dejan en el fichero): {resultado.Ilegibles}.");
        if (resultado.LineasPendientes.Count == 0) {
            Console.WriteLine("No queda ninguna partida pendiente.");
            return 0;
        }
        Console.WriteLine($"Quedan {resultado.LineasPendientes.Count} en {rutaFichero}. Mira el log de Gregal para ver el motivo.");
        return 2;
    }

    // La parte sin ficheros ni Firebird: una línea por partida, guardar() dice
    // si se pudo. Devuelve qué líneas tienen que seguir en el fichero.
    public static async Task<Resultado> ProcesarAsync(IEnumerable<string> lineas, Func<HistoricoDTO, Task<bool>> guardar, Action<string> informar) {
        int guardadas = 0, fallidas = 0, ilegibles = 0;
        var pendientes = new List<string>();

        foreach (string linea in lineas) {
            if (string.IsNullOrWhiteSpace(linea)) continue;

            HistoricoDTO? dto;
            try {
                dto = JsonSerializer.Deserialize<HistoricoDTO>(linea);
            } catch (JsonException) {
                dto = null;
            }
            if (dto == null) {
                ilegibles++;
                pendientes.Add(linea);
                informar($"  ? Línea que no es una partida: {Recortar(linea)}");
                continue;
            }

            string descripcion = Describir(dto);
            bool ok;
            try {
                ok = await guardar(dto);
            } catch (Exception ex) {
                informar($"  X {descripcion}: {ex.Message}");
                ok = false;
            }

            if (ok) {
                guardadas++;
                informar($"  V {descripcion}: guardada.");
            } else {
                fallidas++;
                pendientes.Add(linea);
                informar($"  X {descripcion}: sigue pendiente.");
            }
        }

        return new Resultado(guardadas, fallidas, ilegibles, pendientes);
    }

    private static string Describir(HistoricoDTO dto) {
        string fecha = DateTime.TryParse(dto.FechaSesion, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f)
            ? f.ToString("dd/MM/yyyy")
            : dto.FechaSesion;
        return $"Sala {dto.CodSala}/{dto.CodConc}, partida {dto.PartidaDia} del {fecha}";
    }

    private static string Recortar(string linea) => linea.Length <= 60 ? linea : linea[..60] + "…";
}
