using Gregal.Parsing;

public class EstadoPartidaDecoderTests {
    // Construye un payload válido (sin "última bola") con las bolas indicadas
    // marcadas como salidas, replicando el empaquetado de bits y el checksum
    // que espera EstadoPartidaDecoder (ver CadenaEstado.cs: bytes 1-12 = 7
    // bits cada uno -> bolas 1-84; byte 13 bits 0-5 -> bolas 85-90, bit 6 =
    // jugando a línea; bytes 14-15 = checksum en dos mitades de 7 bits).
    private static string ConstruirPayloadSinUltimaBola(IEnumerable<int> bolas, bool jugandoLinea) {
        var datos = new byte[14]; // índices 0..13
        datos[0] = 0x8D;

        foreach (int numero in bolas) {
            int idx = numero - 1;
            int byteIndex = (idx / 7) + 1;
            int bit = idx % 7;
            datos[byteIndex] |= (byte)(1 << bit);
        }

        if (jugandoLinea) datos[13] |= 0x40;

        for (int i = 1; i <= 13; i++) datos[i] |= 0x80;

        int checksum = 0;
        for (int i = 1; i <= 13; i++) checksum += datos[i] & 0x7F;

        byte checksumBajo = (byte)((checksum & 0xFF) | 0x80);
        byte checksumAlto = (byte)(((checksum >> 7) & 0xFF) | 0x80);

        var payload = new char[16];
        for (int i = 0; i < 14; i++) payload[i] = (char)datos[i];
        payload[14] = (char)checksumBajo;
        payload[15] = (char)checksumAlto;

        return new string(payload);
    }

    [Fact]
    public void Decode_ConPayloadValido_DevuelveLasBolasMarcadas() {
        string payload = ConstruirPayloadSinUltimaBola(new[] { 1, 7, 45, 90 }, jugandoLinea: true);

        var resultado = EstadoPartidaDecoder.Decode(payload);

        Assert.NotNull(resultado);
        Assert.True(resultado!.Value.BolasExtraidas[0]);  // bola 1
        Assert.True(resultado.Value.BolasExtraidas[6]);   // bola 7
        Assert.True(resultado.Value.BolasExtraidas[44]);  // bola 45
        Assert.True(resultado.Value.BolasExtraidas[89]);  // bola 90
        Assert.False(resultado.Value.BolasExtraidas[1]);  // bola 2, no marcada
        Assert.True(resultado.Value.JugandoLinea);
    }

    [Fact]
    public void Decode_ConBitDeFaseABingo_DevuelveJugandoLineaFalse() {
        string payload = ConstruirPayloadSinUltimaBola(new[] { 10 }, jugandoLinea: false);

        var resultado = EstadoPartidaDecoder.Decode(payload);

        Assert.NotNull(resultado);
        Assert.False(resultado!.Value.JugandoLinea);
    }

    [Fact]
    public void Decode_ConChecksumCorrompido_DevuelveNull() {
        string payload = ConstruirPayloadSinUltimaBola(new[] { 1, 2, 3 }, jugandoLinea: true);
        // Corrompemos un byte de datos sin recalcular el checksum.
        char[] chars = payload.ToCharArray();
        chars[5] = (char)((byte)chars[5] ^ 0x01);
        string corrupto = new string(chars);

        Assert.Null(EstadoPartidaDecoder.Decode(corrupto));
    }

    [Fact]
    public void Decode_ConMarcadorDesconocido_DevuelveNull() {
        string payload = ConstruirPayloadSinUltimaBola(new[] { 1 }, jugandoLinea: true);
        char[] chars = payload.ToCharArray();
        chars[0] = (char)0x90; // ni 0x8D, 0x8E ni 0x8F
        string invalido = new string(chars);

        Assert.Null(EstadoPartidaDecoder.Decode(invalido));
    }

    private static string DesdeHex(string hex) =>
        new string(hex.Split(' ').Select(h => (char)Convert.ToByte(h, 16)).ToArray());

    // Cadena real de la captura "7 partidas Metronia" (partida 1, justo tras
    // la bola 52, la 18ª), con marcador 0x8F.
    public const string CadenaRealMetronia = "8F 80 80 80 89 CB 80 B0 94 C4 92 80 84 8B 81 FE 81";
    public static readonly int[] BolasCadenaRealMetronia =
        { 22, 25, 29, 30, 32, 35, 47, 48, 52, 54, 59, 63, 65, 68, 80, 85, 86, 88 };

    [Fact]
    public void Decode_ConCadenaRealDeMetronia_DevuelveLasBolasSalidas() {
        var resultado = EstadoPartidaDecoder.Decode(DesdeHex(CadenaRealMetronia));

        Assert.NotNull(resultado);
        var bolas = Enumerable.Range(1, 90).Where(n => resultado!.Value.BolasExtraidas[n - 1]);
        Assert.Equal(BolasCadenaRealMetronia, bolas);
        Assert.Null(resultado!.Value.UltimaBola); // en Metronia el byte 14 no es la última bola
    }

    [Fact]
    public void Decode_ConCadenaRealVaciaDeMcb_EsValidaYSinBolas() {
        // Se repite al principio de cada partida en la captura de MCB.
        var resultado = EstadoPartidaDecoder.Decode(DesdeHex("8E 80 80 80 80 80 80 80 80 80 80 80 80 C0 80 C0 80"));

        Assert.NotNull(resultado);
        Assert.DoesNotContain(true, resultado!.Value.BolasExtraidas);
    }

    [Fact]
    public void Decode_ConPayloadDemasiadoCorto_DevuelveNull() {
        Assert.Null(EstadoPartidaDecoder.Decode(((char)0x8D).ToString()));
    }
}
