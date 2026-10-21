using Gregal.Config;
using Gregal.Models;
using Gregal.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public class PartidaAccumulatorTests {
    private sealed class RecordingLogger<T> : ILogger<T> {
        public List<string> Warnings { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) {
            if (logLevel == LogLevel.Warning) Warnings.Add(formatter(state, exception));
        }
    }

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

    // Ejemplo de partida completa aportado para diseñar este parser, antes de
    // tener datos reales: precio/caja/venta, decenas de bolas, una línea y un
    // bingo que resultan incorrectos, y fin de partida. Incluye "*K2#" para
    // que las comprobaciones de céntimos (confirmadas más tarde con tickets
    // reales, ver Feed_ConPartidaRealCompleta...) interpreten C/L/B/R igual
    // que lo haría el hardware real.
    private static readonly string[] EjemploPartida = {
        "*K2#", "*B769#", "*L588#", "*P3167#", "*R9165#", "*C307#", "*V110#", "*J3888#", "*D#",
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
    public void Feed_ConValorDeBingoNoNumerico_AvisaYNoGuardaCartonParcial() {
        var logger = new RecordingLogger<PartidaAccumulator>();
        var monitor = new FakeMonitor<TagProtocolSettings>(new TagProtocolSettings());
        var acumulador = new PartidaAccumulator(logger, monitor);
        var troceo = TagEventParser.Trocear("*H4000sdajfhg*F");
        HistoricoDTO? dto = null;

        foreach (string fragmento in troceo.Eventos) {
            var evt = TagEventParser.Parse(fragmento);
            Assert.NotNull(evt);
            dto = acumulador.Feed(evt!.Value) ?? dto;
        }

        Assert.NotNull(dto);
        Assert.Null(dto!.CartBingo);
        Assert.Contains(logger.Warnings, warning =>
            warning.Contains("evento tag H", StringComparison.Ordinal) &&
            warning.Contains("4000sdajfhg", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("*E4e1#")]   // notación científica: antes se leía como bola 40
    [InlineData("*E4,5#")]   // separador de miles
    [InlineData("*E(45)#")]  // negativo contable
    [InlineData("*E 45#")]   // espacio dentro del valor
    public void Feed_ConBolaConFormatoNoNumerico_LaDescartaYAvisa(string trama) {
        var logger = new RecordingLogger<PartidaAccumulator>();
        var acumulador = new PartidaAccumulator(logger, new FakeMonitor<TagProtocolSettings>(new TagProtocolSettings()));

        acumulador.Feed(EventoDe("*E10#"));
        acumulador.Feed(EventoDe(trama));
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.Equal("10", dto!.Bolas);
        Assert.Single(logger.Warnings);
    }

    [Theory]
    [InlineData("*D123fsdjkfh34#", "123fsdjkfh34")]
    [InlineData("*Dx123#", "x123")] // el dígito de tipo también tiene que ser un dígito
    public void Feed_ConLineaConBasura_AvisaConElValorCompleto(string trama, string valorEnElAviso) {
        var logger = new RecordingLogger<PartidaAccumulator>();
        var acumulador = new PartidaAccumulator(logger, new FakeMonitor<TagProtocolSettings>(new TagProtocolSettings()));

        acumulador.Feed(EventoDe(trama));
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.Null(dto!.CartLinea);
        Assert.Contains(logger.Warnings, w => w.Contains($"'{valorEnElAviso}'", StringComparison.Ordinal));
    }

    [Fact]
    public void Feed_ConDSinValor_NoAvisa() {
        // "*D#" llega en cada partida MCB como marcador, no es basura.
        var logger = new RecordingLogger<PartidaAccumulator>();
        var acumulador = new PartidaAccumulator(logger, new FakeMonitor<TagProtocolSettings>(new TagProtocolSettings()));

        acumulador.Feed(EventoDe("*D#"));
        acumulador.Feed(EventoDe("*F#"));

        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public void Feed_AlTerminarPartida_MapeaPrecioCajaYVendidos() {
        var dto = FeedEjemplo(CreateAccumulator());

        Assert.Equal(3.07m, dto.Precio); // *C307# son céntimos (K2) -> 3,07€
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

        // Céntimos (K2) -> euros, igual que Precio. R ya no se mapea (el
        // operador de la sala confirmó "R ignorar" el 2026-10-14, tercera
        // ronda) aunque EjemploPartida todavía incluye "*R9165#" de cuando
        // se diseñó este test sin datos reales.
        Assert.Equal(5.88m, dto.Premios[settings.PremioCodeMap["L"]]);
        Assert.Equal(7.69m, dto.Premios[settings.PremioCodeMap["B"]]);
    }

    [Fact]
    public void Feed_ConCantoCorrectoNoDesmentido_DejaElCartonGanadorFijado() {
        var acumulador = CreateAccumulator();

        acumulador.Feed(EventoDe("*E10#"));
        // El primer dígito es el tipo de línea (fila del cartón), no forma
        // parte del número de cartón — confirmado con tickets reales
        // (*D12363# de una partida real == cartón 2363 en el ticket impreso).
        acumulador.Feed(EventoDe("*D1555#"));
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.NotNull(dto);
        Assert.Equal("555", dto!.CartLinea);
        Assert.Equal(1, dto.NumLineas);
        Assert.Equal(1, dto.BolaLinea);
    }

    [Fact]
    public void Feed_ConDosBingosEnLaMismaPartida_GuardaAmbosCartones() {
        // Confirmado con un ticket real (partida 6 de "7 partidas Metronia"):
        // puede haber más de un cartón de bingo válido en la misma partida.
        var acumulador = CreateAccumulator();

        acumulador.Feed(EventoDe("*E10#"));
        acumulador.Feed(EventoDe("*H2891#"));
        acumulador.Feed(EventoDe("*H3468#"));
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.NotNull(dto);
        Assert.Equal("2891,3468", dto!.CartBingo);
        Assert.Equal(2, dto.NumBingos);
    }

    [Fact]
    public void Feed_ConCamposConfirmadosPorTicketsReales_LosMapeaCorrectamente() {
        // Reproduce la cabecera real de la partida 1 de "7 partidas Metronia"
        // (sin empalme de serie), cruzada contra el ticket impreso por la
        // máquina para esa misma partida.
        var acumulador = CreateAccumulator();

        acumulador.Feed(EventoDe("*K2#"));
        acumulador.Feed(EventoDe("*P2051#"));
        acumulador.Feed(EventoDe("*U2800#"));
        acumulador.Feed(EventoDe("*X00179689#"));
        acumulador.Feed(EventoDe("*J3864#"));
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.NotNull(dto);
        Assert.Equal(2, dto!.NumDecimales);
        Assert.Equal(2051, dto.Del1);
        Assert.Equal(2800, dto.Al1);
        Assert.Equal(179689, dto.Serie1);
        Assert.Null(dto.Serie2);
        Assert.Equal("EB", dto.Letra1);
        Assert.Null(dto.Letra2); // sin empalme, no se duplica la letra
    }

    [Fact]
    public void Feed_ConEmpalmeDeSerie_AsignaLaMismaLetraALasDosSeries() {
        // Confirmado con el ticket de la partida 4 ("7 partidas Metronia"):
        // dos series del mismo tipo de edición ("EB-00179689"/"EB-00179690").
        var acumulador = CreateAccumulator();

        acumulador.Feed(EventoDe("*X00179689,00179690#"));
        acumulador.Feed(EventoDe("*J3864#"));
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.NotNull(dto);
        Assert.Equal(179689, dto!.Serie1);
        Assert.Equal(179690, dto.Serie2);
        Assert.Equal("EB", dto.Letra1);
        Assert.Equal("EB", dto.Letra2);
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

    // Secuencia real completa de la "Partida 1" de la captura "7 partidas
    // Metronia.txt" (2026-10-14), sin los eventos 'e' (snapshot binario — ver
    // nota en PartidaAccumulator.AplicarEstadoPartida sobre por qué no se
    // pueden reproducir fielmente desde esa captura de texto) ni los '*f#'
    // de relleno tras el *F# final. Los valores esperados salen del ticket
    // que la propia máquina imprimió para esta partida.
    private static readonly string[] Partida1Real = {
        "*K2#", "*T1#", "*L9000#", "*B75000#", "*C200#", "*P2051#", "*U2800#", "*V750#",
        "*X00179689#", "*J3864#", "*p500#", "*A15000#", "*M55#",
        "*E68#", "*º0#", "*E85#", "*º0#", "*E30#", "*º0#", "*E29#", "*º0#", "*E63#", "*º0#",
        "*E65#", "*º0#", "*E48#", "*º0#", "*E59#", "*º0#", "*E22#", "*º0#", "*E88#", "*º0#",
        "*E25#", "*º0#", "*E32#", "*º0#", "*E80#", "*º0#", "*E86#", "*º0#", "*E35#", "*º0#",
        "*E54#", "*º0#", "*E47#", "*º0#", "*E52#", "*º0#",
        "*Y#", "*D12363#", "*H#",
        "*E6#", "*º0#", "*E26#", "*º0#", "*E27#", "*º0#", "*E12#", "*º0#", "*E20#", "*º0#",
        "*E74#", "*º0#", "*E75#", "*º0#", "*E10#", "*º0#", "*E70#", "*º0#", "*E56#", "*º0#",
        "*E58#", "*º0#", "*E41#", "*º0#", "*E82#", "*º0#", "*E46#", "*º0#", "*E77#", "*º0#",
        "*E87#", "*º0#", "*E43#", "*º0#", "*E60#", "*º0#", "*E18#", "*º0#", "*E64#", "*º0#",
        "*E45#", "*º0#", "*E4#", "*º0#", "*E3#", "*º0#", "*E37#", "*º0#", "*E16#", "*º0#",
        "*E81#", "*º0#", "*E24#", "*º0#", "*E36#", "*º0#", "*E21#", "*º0#", "*E78#", "*º0#",
        "*E33#", "*º0#", "*E57#", "*º0#", "*E72#", "*º0#", "*E34#", "*º0#", "*E23#", "*º0#",
        "*E42#", "*º0#", "*E15#", "*º0#", "*E71#", "*º0#", "*E66#", "*º0#", "*E7#", "*º0#",
        "*E2#", "*º0#",
        "*Y#", "*H2264#", "*H#", "*F#"
    };

    [Fact]
    public void Feed_ConPartidaRealCompleta_CoincideConElTicketImpresoPorLaMaquina() {
        var acumulador = CreateAccumulator();

        HistoricoDTO? dto = null;
        foreach (string frame in Partida1Real) {
            var resultado = acumulador.Feed(EventoDe(frame));
            if (resultado != null) dto = resultado;
        }

        Assert.NotNull(dto);
        Assert.Equal(1, dto!.PartidaDia);
        Assert.Equal(2, dto.NumDecimales);
        Assert.Equal(2m, dto.Precio); // *C200# = 200 céntimos
        Assert.Equal(750, dto.Vendidos);
        Assert.Equal(2051, dto.Del1);
        Assert.Equal(2800, dto.Al1);
        Assert.Equal(179689, dto.Serie1);
        Assert.Equal("EB", dto.Letra1);

        var settings = new TagProtocolSettings();
        Assert.Equal(90m, dto.Premios[settings.PremioCodeMap["L"]]); // *L9000# = 9000 céntimos
        Assert.Equal(750m, dto.Premios[settings.PremioCodeMap["B"]]);
        Assert.Equal(150m, dto.Premios[settings.PremioCodeMap["A"]]);
        Assert.Equal(5m, dto.Premios[settings.PremioCodeMap["p"]]);

        Assert.Equal(59, dto.Extracciones);
        Assert.Equal(
            "68,85,30,29,63,65,48,59,22,88,25,32,80,86,35,54,47,52," +
            "6,26,27,12,20,74,75,10,70,56,58,41,82,46,77,87,43,60,18,64,45,4,3,37,16,81,24,36,21,78,33,57,72,34,23,42,15,71,66,7,2",
            dto.Bolas);

        Assert.Equal("2363", dto.CartLinea); // *D12363# -> tipo "1" + cartón "2363" (ticket: "2363-S")
        Assert.Equal(18, dto.BolaLinea);
        Assert.Equal(1, dto.NumLineas);

        Assert.Equal("2264", dto.CartBingo); // *H2264# (ticket: "2264-B")
        Assert.Equal(59, dto.BolaBingo);
        Assert.Equal(1, dto.NumBingos);

        Assert.Null(dto.Reclamacion); // sin reconexión a mitad de partida
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

    [Fact]
    public void Feed_ConEventoM_GuardaLaBolaTopeDePrimaComoPremio7SinDividirPorCentimos() {
        // Confirmado por el operador de la sala (2026-10-14): M es la "bola
        // tope de prima" y se guarda como PREMIO7 (Premios[6]) — a diferencia
        // del resto de premios de PremioCodeMap, es un número de bola, no un
        // importe en céntimos, así que no debe dividirse al finalizar.
        var acumulador = CreateAccumulator();
        var settings = new TagProtocolSettings();

        acumulador.Feed(EventoDe("*K2#"));
        acumulador.Feed(EventoDe("*M55#"));
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.NotNull(dto);
        Assert.Equal(55m, dto!.Premios[settings.PremioCodeMap["M"]]);
    }

    [Fact]
    public void Feed_ConEventoZ_BorraUnaBolaYaRegistrada() {
        // Confirmado por el operador de la sala: el borrado de una bola es el
        // código 'Z' (la documentación original decía 'X' por error de
        // transcripción). Sin tráfico real que lo confirme, se prueba aquí
        // con el comportamiento más simple: quita la bola de la lista como si
        // nunca hubiera llegado.
        var acumulador = CreateAccumulator();

        acumulador.Feed(EventoDe("*E5#"));
        acumulador.Feed(EventoDe("*E10#"));
        acumulador.Feed(EventoDe("*E15#"));
        acumulador.Feed(EventoDe("*Z10#")); // bola 10 mal leída, se borra
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.NotNull(dto);
        Assert.Equal("5,15", dto!.Bolas);
        Assert.Equal(2, dto.Extracciones);
    }

    [Fact]
    public void Feed_ConEventoZDeUnaBolaNoRegistrada_NoHaceNada() {
        var acumulador = CreateAccumulator();

        acumulador.Feed(EventoDe("*E5#"));
        acumulador.Feed(EventoDe("*Z77#")); // nunca llegó la bola 77
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.NotNull(dto);
        Assert.Equal("5", dto!.Bolas);
        Assert.Equal(1, dto.Extracciones);
    }

    // Variante de protocolo confirmada por el operador de la sala (2026-10-14,
    // tercera ronda): un mismo código *Y<cartón># para línea y bingo, sin
    // dígito de tipo, distinguido por si ya llegó el *H# que marca la fase
    // de bingo. Convive con *D#/*H# (confirmado antes con tickets reales).

    [Fact]
    public void Feed_ConEventoYAntesDeLaFaseDeBingo_SeInterpretaComoLinea() {
        var acumulador = CreateAccumulator();

        acumulador.Feed(EventoDe("*E10#"));
        acumulador.Feed(EventoDe("*Y2363#")); // todavía no ha llegado *H#
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.NotNull(dto);
        Assert.Equal("2363", dto!.CartLinea);
        Assert.Equal(1, dto.NumLineas);
        Assert.Null(dto.CartBingo);
    }

    [Fact]
    public void Feed_ConEventoYDespuesDeLaFaseDeBingo_SeInterpretaComoBingo() {
        var acumulador = CreateAccumulator();

        acumulador.Feed(EventoDe("*E10#"));
        acumulador.Feed(EventoDe("*Y2363#")); // línea
        acumulador.Feed(EventoDe("*H#"));     // entra en fase de bingo
        acumulador.Feed(EventoDe("*E20#"));
        acumulador.Feed(EventoDe("*Y2264#")); // ahora es bingo
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.NotNull(dto);
        Assert.Equal("2363", dto!.CartLinea);
        Assert.Equal("2264", dto.CartBingo);
        Assert.Equal(1, dto.NumBingos);
    }

    [Fact]
    public void Feed_ConEventoYSinValor_NoTieneEfectoEnElDto() {
        var acumulador = CreateAccumulator();

        acumulador.Feed(EventoDe("*E10#"));
        acumulador.Feed(EventoDe("*Y#")); // "entrando en comprobación"
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.NotNull(dto);
        Assert.Null(dto!.CartLinea);
        Assert.Null(dto.CartBingo);
    }

    [Fact]
    public void Feed_ConJConBasuraTrasLosCuatroDigitos_IgnoraElResto() {
        // Confirmado por el operador: J puede llevar basura detrás de los 4
        // dígitos de longitud de serie, p.ej. "386400001&BIPLUSE1.255".
        var acumulador = CreateAccumulator();

        acumulador.Feed(EventoDe("*X179689#"));
        acumulador.Feed(EventoDe("*J386400001&BIPLUSE1.255#"));
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.NotNull(dto);
        Assert.Equal("EB", dto!.Letra1);
    }

    [Fact]
    public void Feed_ConBolaFueraDeRango_SeIgnoraComoRuido() {
        // Confirmado con una captura real del fabricante MCB: "*E101#"
        // aparece como ruido del hardware (la bola 101 no existe).
        var acumulador = CreateAccumulator();

        acumulador.Feed(EventoDe("*E45#"));
        acumulador.Feed(EventoDe("*E101#"));
        acumulador.Feed(EventoDe("*E0#"));
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.NotNull(dto);
        Assert.Equal("45", dto!.Bolas);
        Assert.Equal(1, dto.Extracciones);
    }

    // Confirmado (2026-10-14, cuarta ronda): hay dos fabricantes distintos
    // (Metronia y MCB) con protocolos parecidos. En MCB, A/M/p no llevan un
    // valor fiable (aparecen como ruido constante); en Metronia sí (ver
    // Feed_ConPartidaRealCompleta...). Se elige con TagProtocolSettings.Fabricante.
    [Fact]
    public void Feed_ConFabricanteMcb_IgnoraAMyPAunqueEstenEnElMapa() {
        var settings = new TagProtocolSettings { Fabricante = Fabricante.MCB }; // mapa por defecto, con p/A/M
        var acumulador = CreateAccumulator(settings);

        acumulador.Feed(EventoDe("*L2400#"));
        acumulador.Feed(EventoDe("*A15000#"));
        acumulador.Feed(EventoDe("*M55#"));
        acumulador.Feed(EventoDe("*p38000#"));
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.NotNull(dto);
        Assert.Equal(24m, dto!.Premios[settings.PremioCodeMap["L"]]);
        Assert.Null(dto.Premios[settings.PremioCodeMap["A"]]);
        Assert.Null(dto.Premios[settings.PremioCodeMap["M"]]);
        Assert.Null(dto.Premios[settings.PremioCodeMap["p"]]);
    }

    [Fact]
    public void Feed_ConAoMNoConfiguradosEnEstaSala_SeIgnoranEnSilencio() {
        var settingsEstiloMcb = new TagProtocolSettings {
            PremioCodeMap = new Dictionary<string, int> { ["L"] = 0, ["B"] = 1 }
        };
        var acumulador = CreateAccumulator(settingsEstiloMcb);

        acumulador.Feed(EventoDe("*L2400#"));
        acumulador.Feed(EventoDe("*A000#"));
        acumulador.Feed(EventoDe("*M0#"));
        acumulador.Feed(EventoDe("*p38000#"));
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.NotNull(dto);
        Assert.Equal(24m, dto!.Premios[0]); // L sí está configurado en esta sala
        Assert.Equal(1, dto.Premios.Count(p => p.HasValue)); // A/M/p no escriben nada
    }
}
