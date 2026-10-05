namespace Gregal.Parsing;

// Decodifica el mensaje binario "cadena de estado" (código 'e', ver
// CadenaEstado.cs del software de pantallas PidFullHD). A diferencia del
// resto de eventos (texto, *CODIGO<valor>#), este es un payload binario
// empaquetado a nivel de bit: todos sus bytes llevan el bit 0x80 forzado a 1
// a propósito, para no colisionar nunca con '*' (0x2A) ni '#' (0x23). Se usa
// para que un receptor que se conecta a mitad de partida pueda saber qué
// bolas han salido ya y si se está jugando a línea o a bingo, sin haber visto
// los *E# individuales anteriores.
//
// Requiere que el string de entrada preserve cada byte original 1:1 (puerto
// serie con Encoding.Latin1, no ASCII/UTF-8 — ver TagSerialGregalReceiver).
public static class EstadoPartidaDecoder {
    private const int MaxBolas = 90;
    private const byte MarcadorSinUltimaBola = 0x8D;
    private const byte MarcadorConUltimaBola = 0x8E;
    private const byte BitMarcador = 0x80;
    private const byte MascaraDatos = 0x7F;

    public readonly record struct Resultado(bool[] BolasExtraidas, bool JugandoLinea, int? UltimaBola);

    // payload = los caracteres tras el código 'e' (sin el propio 'e').
    // Devuelve null si el checksum no cuadra o el payload es demasiado corto
    // (mensaje corrupto o truncado: se ignora en vez de usar datos a medias).
    public static Resultado? Decode(string payload) {
        var b = new byte[payload.Length];
        for (int i = 0; i < payload.Length; i++) b[i] = (byte)payload[i];

        if (b.Length == 0) return null;

        bool conUltimaBola = b[0] == MarcadorConUltimaBola;
        bool sinUltimaBola = b[0] == MarcadorSinUltimaBola;
        if (!conUltimaBola && !sinUltimaBola) return null;

        // "Con última bola" trae un byte extra (el número de esa bola) antes
        // del checksum; por eso sus índices van uno más allá.
        int ultimoIndiceSumado = conUltimaBola ? 14 : 13;
        int indiceChecksumBajo = ultimoIndiceSumado + 1;
        int indiceChecksumAlto = ultimoIndiceSumado + 2;

        if (b.Length <= indiceChecksumAlto) return null;

        for (int i = 1; i <= indiceChecksumAlto; i++) {
            if ((b[i] & BitMarcador) == 0) return null;
        }

        int checksum = 0;
        for (int i = 1; i <= ultimoIndiceSumado; i++) checksum += b[i] & MascaraDatos;

        if (b[indiceChecksumBajo] != (byte)((checksum & 0xFF) | BitMarcador)) return null;
        if (b[indiceChecksumAlto] != (byte)(((checksum >> 7) & 0xFF) | BitMarcador)) return null;

        var bolas = new bool[MaxBolas];
        for (int i = 1; i <= 12; i++) { // bytes 1-12: 7 bits cada uno -> bolas 1-84
            for (int j = 0; j < 7; j++) {
                if ((b[i] & (1 << j)) == 0) continue;
                int numero = (i - 1) * 7 + j + 1;
                if (numero <= MaxBolas) bolas[numero - 1] = true;
            }
        }
        for (int j = 0; j < 6; j++) { // byte 13, bits 0-5: bolas 85-90
            if ((b[13] & (1 << j)) == 0) continue;
            int numero = 84 + j + 1;
            if (numero <= MaxBolas) bolas[numero - 1] = true;
        }

        // byte 13, bit 6: fase actual de la partida.
        bool jugandoLinea = (b[13] & 0x40) != 0;

        int? ultimaBola = null;
        if (conUltimaBola) {
            int valor = b[14] & MascaraDatos;
            if (valor is > 0 and <= MaxBolas) ultimaBola = valor;
        }

        return new Resultado(bolas, jugandoLinea, ultimaBola);
    }
}
