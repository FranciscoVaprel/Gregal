using Gregal.Models;

namespace Gregal.Parsing;

// Decodifica UN fragmento ya separado por el delimitador '#' del protocolo
// *CODIGO<valor># en su código + valor. Sin estado y sin lógica de negocio,
// igual que CsvGregalParser pero para un único evento del stream.
public static class TagEventParser {
    // Caracteres ASCII de relleno que se pueden colar alrededor de una trama
    // de texto por ruido de línea serie. OJO: no usar el Trim() genérico de
    // .NET aquí — también recorta U+0085/U+00A0, que son valores de BYTE
    // legítimos (0x85/0xA0) dentro del payload binario del evento 'e'
    // (EstadoPartidaDecoder); recortarlos corrompería ese mensaje.
    private static readonly char[] RellenoAsciiTolerado = { ' ', '\t', '\r', '\n' };

    public static TagEvent? Parse(string rawFragment) {
        if (string.IsNullOrEmpty(rawFragment)) return null;

        string fragment = rawFragment.TrimStart('*').Trim(RellenoAsciiTolerado);
        if (fragment.Length == 0) return null;

        char code = fragment[0];
        string value = fragment.Length > 1 ? fragment[1..] : string.Empty;
        return new TagEvent(code, value);
    }
}
