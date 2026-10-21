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

    public readonly record struct Troceo(string? Basura, IReadOnlyList<string> Eventos);

    // Separa un trozo del stream ya cortado por '#' en eventos, usando también
    // '*' como inicio de evento: si la línea pierde un '#' (p.ej. llega
    // "*H4000*F"), el '*F' se recupera en vez de quedarse dentro del valor del
    // evento anterior y dejar la partida sin cerrar. Es seguro porque '*' no
    // aparece nunca dentro de un evento: ni en los valores de texto vistos en
    // capturas reales, ni en la cadena de estado 'e', cuyos bytes llevan todos
    // el bit 0x80. Lo que haya antes del primer '*' no puede ser un evento
    // completo (es la cola de uno ya empezado, o ruido) y se devuelve como
    // Basura para que quien llame lo descarte.
    public static Troceo Trocear(string fragmento) {
        int primera = fragmento.IndexOf('*');
        if (primera < 0) return new Troceo(NullSiVacio(fragmento), Array.Empty<string>());

        var eventos = new List<string>();
        foreach (string trozo in fragmento[(primera + 1)..].Split('*')) {
            string evento = trozo.Trim(RellenoAsciiTolerado);
            if (evento.Length > 0) eventos.Add("*" + evento);
        }
        return new Troceo(NullSiVacio(fragmento[..primera]), eventos);
    }

    private static string? NullSiVacio(string texto) {
        string limpio = texto.Trim(RellenoAsciiTolerado);
        return limpio.Length == 0 ? null : limpio;
    }

    public static TagEvent? Parse(string rawFragment) {
        if (string.IsNullOrEmpty(rawFragment)) return null;

        string fragment = rawFragment.TrimStart('*').Trim(RellenoAsciiTolerado);
        if (fragment.Length == 0) return null;

        char code = fragment[0];
        string value = fragment.Length > 1 ? fragment[1..] : string.Empty;
        return new TagEvent(code, value);
    }
}
