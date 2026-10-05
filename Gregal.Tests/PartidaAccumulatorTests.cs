using Gregal.Config;
using Gregal.Models;
using Gregal.Parsing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public class PartidaAccumulatorTests {
    // IOptionsMonitor<T> no tiene una factoría de pruebas en el proyecto;
    // este doble mínimo basta porque PartidaAccumulator solo lee CurrentValue.
    private sealed class FakeMonitor<T> : IOptionsMonitor<T> {
        public FakeMonitor(T value) => CurrentValue = value;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private static PartidaAccumulator CreateAccumulator(TagProtocolSettings? settings = null) {
        var monitor = new FakeMonitor<TagProtocolSettings>(settings ?? new TagProtocolSettings());
        return new PartidaAccumulator(NullLogger<PartidaAccumulator>.Instance, monitor);
    }

    private static TagEvent EventoDe(string rawFragmentConHash) {
        var evt = TagEventParser.Parse(rawFragmentConHash.TrimEnd('#'));
        Assert.NotNull(evt);
        return evt!.Value;
    }

    // Ejemplo real de partida completa aportado para diseñar este parser:
    // precio/caja/venta, decenas de bolas, una línea y un bingo que resultan
    // incorrectos, y fin de partida.
    private static readonly string[] EjemploPartida = {
        "*B769#", "*L588#", "*P3167#", "*R9165#", "*C307#", "*V110#", "*J3888#", "*D#",
        "*E19#","*E27#","*E29#","*E40#","*E50#","*E64#","*E73#","*E48#","*E23#","*E76#",
        "*E31#","*E52#","*E65#","*E68#","*E70#","*E6#","*E46#","*E58#","*E37#","*E42#",
        "*E23#","*E44#","*E77#","*E75#","*E9#","*E60#","*E75#","*E45#","*E65#","*E28#",
        "*E21#","*E46#","*E13#","*E17#","*E83#","*E51#","*E60#","*E12#","*E72#","*E41#",
        "*E44#","*E89#","*E28#","*E4#","*E63#",
        "*D717#","*u717#","*Y#","*H#",
        "*E21#","*E3#","*E29#","*E85#","*E33#","*E33#","*E36#","*E88#","*E39#","*E53#",
        "*E65#","*E39#","*E44#","*E32#","*E80#","*E78#","*E28#","*E72#","*E85#","*E55#",
        "*E75#","*E39#","*E48#","*E74#","*E19#","*E36#","*E34#","*E75#","*E86#","*E88#",
        "*E24#","*E16#","*E74#","*E12#","*E78#","*E57#","*E47#","*E49#","*E32#","*E85#",
        "*E75#","*E47#","*E64#","*E54#",
        "*H1400#","*h1400#","*Y#","*F#"
    };

    private static HistoricoDTO FeedEjemplo(PartidaAccumulator acumulador) {
        HistoricoDTO? resultado = null;
        foreach (string frame in EjemploPartida) {
            var dto = acumulador.Feed(EventoDe(frame));
            if (dto != null) resultado = dto;
        }
        Assert.NotNull(resultado);
        return resultado!;
    }

    [Fact]
    public void Feed_MientrasNoLlegaElEventoF_DevuelveNull() {
        var acumulador = CreateAccumulator();

        var resultado = acumulador.Feed(EventoDe("*C307#"));

        Assert.Null(resultado);
    }

    [Fact]
    public void Feed_AlTerminarPartida_MapeaPrecioCajaYVendidos() {
        var dto = FeedEjemplo(CreateAccumulator());

        Assert.Equal(307m, dto.Precio);
        Assert.Equal(110, dto.Vendidos);
    }

    [Fact]
    public void Feed_AlTerminarPartida_AcumulaTodasLasBolasEnOrden() {
        var dto = FeedEjemplo(CreateAccumulator());

        int bolasEsperadas = EjemploPartida.Count(f => f.StartsWith("*E"));

        Assert.Equal(bolasEsperadas, dto.Extracciones);
        Assert.NotNull(dto.Bolas);
        Assert.Equal(bolasEsperadas, dto.Bolas!.Split(',').Length);
        Assert.StartsWith("19,27,29", dto.Bolas);
        Assert.EndsWith(",47,64,54", dto.Bolas);
    }

    [Fact]
    public void Feed_ConLineaYBingoCantadosIncorrectamente_NoDejaCartonGanadorFijado() {
        var dto = FeedEjemplo(CreateAccumulator());

        Assert.Null(dto.CartLinea);
        Assert.Equal(0, dto.NumLineas ?? 0);
        Assert.Null(dto.CartBingo);
        Assert.Equal(0, dto.NumBingos ?? 0);
    }

    [Fact]
    public void Feed_ConCodigoDePremioMapeado_AsignaAlIndiceConfigurado() {
        var settings = new TagProtocolSettings();

        var dto = FeedEjemplo(CreateAccumulator(settings));

        Assert.Equal(588m, dto.Premios[settings.PremioCodeMap["L"]]);
        Assert.Equal(769m, dto.Premios[settings.PremioCodeMap["B"]]);
        Assert.Equal(9165m, dto.Premios[settings.PremioCodeMap["R"]]);
    }

    [Fact]
    public void Feed_ConCantoCorrectoNoDesmentido_DejaElCartonGanadorFijado() {
        var acumulador = CreateAccumulator();

        acumulador.Feed(EventoDe("*E10#"));
        acumulador.Feed(EventoDe("*D555#"));
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.NotNull(dto);
        Assert.Equal("555", dto!.CartLinea);
        Assert.Equal(1, dto.NumLineas);
        Assert.Equal(1, dto.BolaLinea);
    }

    [Fact]
    public void Feed_TrasFinalizarUnaPartida_ReinicializaElEstadoParaLaSiguiente() {
        var acumulador = CreateAccumulator();
        FeedEjemplo(acumulador);

        var resultado = acumulador.Feed(EventoDe("*C500#"));

        Assert.Null(resultado); // la nueva partida empieza limpia, aún acumulando
    }

    [Fact]
    public void Feed_SinCodigoT_AsumeFechaSesionDeHoyYPartidaDiaCorrelativo() {
        var dto = FeedEjemplo(CreateAccumulator());

        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), dto.FechaSesion);
        Assert.Equal(1, dto.PartidaDia);
    }

    [Fact]
    public void Feed_ConCodigoT_UsaElValorRecibidoEnVezDelCorrelativo() {
        var acumulador = CreateAccumulator();

        acumulador.Feed(EventoDe("*T42#"));
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.NotNull(dto);
        Assert.Equal(42, dto!.PartidaDia);
    }

    [Fact]
    public void Feed_EnPartidasSucesivasSinCodigoT_IncrementaElCorrelativo() {
        var acumulador = CreateAccumulator();

        var primera = acumulador.Feed(EventoDe("*F#"));
        var segunda = acumulador.Feed(EventoDe("*F#"));

        Assert.Equal(1, primera!.PartidaDia);
        Assert.Equal(2, segunda!.PartidaDia);
    }

    // Construye el payload binario del evento 'e' (sin "última bola") con las
    // bolas indicadas marcadas como salidas, igual que EstadoPartidaDecoderTests.
    private static string ConstruirPayloadEstadoPartida(IEnumerable<int> bolas) {
        var datos = new byte[14];
        datos[0] = 0x8D;
        foreach (int numero in bolas) {
            int idx = numero - 1;
            datos[(idx / 7) + 1] |= (byte)(1 << (idx % 7));
        }
        for (int i = 1; i <= 13; i++) datos[i] |= 0x80;

        int checksum = 0;
        for (int i = 1; i <= 13; i++) checksum += datos[i] & 0x7F;

        var payload = new char[16];
        for (int i = 0; i < 14; i++) payload[i] = (char)datos[i];
        payload[14] = (char)((checksum & 0xFF) | 0x80);
        payload[15] = (char)(((checksum >> 7) & 0xFF) | 0x80);

        return new string(payload);
    }

    [Fact]
    public void Feed_ConEventoEstadoPartida_IncorporaSoloLasBolasQueNoConociamos() {
        var acumulador = CreateAccumulator();

        // Nos conectamos a mitad de partida: ya vimos los *E# reales de 5 y 10...
        acumulador.Feed(EventoDe("*E5#"));
        acumulador.Feed(EventoDe("*E10#"));

        // ...llega un 'e' que dice que también han salido 5, 10 (ya las sabíamos), 20 y 30 (nuevas).
        acumulador.Feed(new TagEvent('e', ConstruirPayloadEstadoPartida(new[] { 5, 10, 20, 30 })));

        // La partida continúa con normalidad tras la reconciliación.
        acumulador.Feed(EventoDe("*E40#"));
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.NotNull(dto);
        Assert.Equal("5,10,20,30,40", dto!.Bolas);
        Assert.Equal(5, dto.Extracciones);
        Assert.NotNull(dto.Reclamacion);
        Assert.Contains("RECUPERADA A MEDIAS", dto.Reclamacion);
    }

    [Fact]
    public void Feed_ConEventoEstadoPartidaQueNoAportaNada_NoMarcaLaPartidaComoRecuperada() {
        var acumulador = CreateAccumulator();

        acumulador.Feed(EventoDe("*E5#"));
        // El 'e' solo confirma lo que ya sabíamos: no debería marcar nada.
        acumulador.Feed(new TagEvent('e', ConstruirPayloadEstadoPartida(new[] { 5 })));
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.NotNull(dto);
        Assert.Equal("5", dto!.Bolas);
        Assert.Null(dto.Reclamacion);
    }

    [Fact]
    public void Feed_ConEventoEstadoPartidaCorrupto_SeIgnoraSinRomperLaPartida() {
        var acumulador = CreateAccumulator();
        string payload = ConstruirPayloadEstadoPartida(new[] { 5 });
        string corrupto = payload[..5] + (char)0x00 + payload[6..]; // rompe el checksum

        acumulador.Feed(EventoDe("*E1#"));
        acumulador.Feed(new TagEvent('e', corrupto));
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.NotNull(dto);
        Assert.Equal("1", dto!.Bolas); // el evento 'e' corrupto no afectó a nada
        Assert.Null(dto.Reclamacion);
    }
}
