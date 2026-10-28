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

    // Con una partida ya empezada (*T1#): sin T, el acumulador no devuelve la
    // partida al llegar F (no sabe su número), y la mayoría de tests no van de eso.
    private static PartidaAccumulator CreateAccumulator(TagProtocolSettings? settings = null, ILogger<PartidaAccumulator>? logger = null) {
        var acumulador = CreateAccumulatorSinT(settings, logger);
        acumulador.Feed(new TagEvent('T', "1"));
        return acumulador;
    }

    private static PartidaAccumulator CreateAccumulatorSinT(TagProtocolSettings? settings = null, ILogger<PartidaAccumulator>? logger = null) {
        var monitor = new FakeMonitor<TagProtocolSettings>(settings ?? new TagProtocolSettings());
        return new PartidaAccumulator(logger ?? NullLogger<PartidaAccumulator>.Instance, monitor);
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
        "*T1#", "*K2#", "*B769#", "*L588#", "*P3167#", "*R9165#", "*C307#", "*V110#", "*J3888#", "*D#",
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
        var acumulador = CreateAccumulator(logger: logger);
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
        var acumulador = CreateAccumulator(logger: logger);

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
        var acumulador = CreateAccumulator(logger: logger);

        acumulador.Feed(EventoDe(trama));
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.Null(dto!.CartLinea);
        Assert.Contains(logger.Warnings, w => w.Contains($"'{valorEnElAviso}'", StringComparison.Ordinal));
    }

    [Fact]
    public void Feed_ConDSinValor_NoAvisa() {
        // "*D#" llega en cada partida MCB como marcador, no es basura.
        var logger = new RecordingLogger<PartidaAccumulator>();
        var acumulador = CreateAccumulator(logger: logger);

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

        // EjemploPartida (anterior a tener datos reales) repite algunas bolas;
        // las repeticiones se ignoran, así que se espera cada bola una vez, en
        // el orden de su primera aparición.
        var bolasEsperadas = EjemploPartida
            .Where(f => f.StartsWith("*E"))
            .Select(f => f[2..^1])
            .Distinct()
            .ToList();

        Assert.Equal(bolasEsperadas.Count, dto.Extracciones);
        Assert.Equal(string.Join(",", bolasEsperadas), dto.Bolas);
        Assert.StartsWith("19,27,29", dto.Bolas);
    }

    [Fact]
    public void Feed_ConCadenaDeEstadoRealDeMetronia_NoAvisaNiMarcaRecuperada() {
        // Las 18 bolas reales previas a esa cadena, en su orden de salida.
        var logger = new RecordingLogger<PartidaAccumulator>();
        var acumulador = CreateAccumulator(logger: logger);
        foreach (int bola in new[] { 68, 85, 30, 29, 63, 65, 48, 59, 22, 88, 25, 32, 80, 86, 35, 54, 47, 52 }) {
            acumulador.Feed(EventoDe($"*E{bola}#"));
        }
        string payload = new string(EstadoPartidaDecoderTests.CadenaRealMetronia.Split(' ')
            .Select(h => (char)Convert.ToByte(h, 16)).ToArray());

        acumulador.Feed(new TagEvent('e', payload));
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.Empty(logger.Warnings);
        Assert.Null(dto!.Reclamacion);
        Assert.Equal(18, dto.Extracciones);
    }

    [Fact]
    public void Feed_ConBolaRepetida_LaIgnoraYAvisa() {
        var logger = new RecordingLogger<PartidaAccumulator>();
        var acumulador = CreateAccumulator(logger: logger);

        acumulador.Feed(EventoDe("*E10#"));
        acumulador.Feed(EventoDe("*E20#"));
        acumulador.Feed(EventoDe("*E10#"));
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.Equal("10,20", dto!.Bolas);
        Assert.Equal(2, dto.Extracciones);
        Assert.Contains(logger.Warnings, w => w.Contains("Bola 10 repetida", StringComparison.Ordinal));
    }

    [Fact]
    public void Feed_ConBolaBorradaYVueltaARecibir_LaAceptaSinAviso() {
        var logger = new RecordingLogger<PartidaAccumulator>();
        var acumulador = CreateAccumulator(logger: logger);

        acumulador.Feed(EventoDe("*E10#"));
        acumulador.Feed(EventoDe("*Z10#"));
        acumulador.Feed(EventoDe("*E10#"));
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.Equal("10", dto!.Bolas);
        Assert.Empty(logger.Warnings);
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
    public void Feed_AsumeFechaSesionDeHoy() {
        var dto = FeedEjemplo(CreateAccumulator());

        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), dto.FechaSesion);
    }

    [Fact]
    public void Feed_ConCodigoT_UsaElValorRecibido() {
        var acumulador = CreateAccumulator();

        acumulador.Feed(EventoDe("*T42#"));
        var dto = acumulador.Feed(EventoDe("*F#"));

        Assert.NotNull(dto);
        Assert.Equal(42, dto!.PartidaDia);
    }

    [Fact]
    public void Feed_ConDobleFDeMcb_IgnoraElSegundoF() {
        var acumulador = CreateAccumulatorSinT();

        acumulador.Feed(EventoDe("*T3#"));
        acumulador.Feed(EventoDe("*E10#"));
        var partida = acumulador.Feed(EventoDe("*F#"));
        acumulador.Feed(EventoDe("*f#"));
        var segundoF = acumulador.Feed(EventoDe("*F#"));

        Assert.Equal(3, partida!.PartidaDia);
        Assert.Null(segundoF);
    }

    [Fact]
    public void Feed_ConCapturaRealDeMcb_DevuelveSoloLasOncePartidas() {
        var logger = new RecordingLogger<PartidaAccumulator>();
        var acumulador = CreateAccumulatorSinT(new TagProtocolSettings { Fabricante = Fabricante.MCB }, logger);

        var partidas = ReproducirCaptura(acumulador, LeerCaptura("Volcado MCB.txt"));

        Assert.Equal(Enumerable.Range(1, 11), partidas.Select(p => p.PartidaDia));
        Assert.DoesNotContain(logger.Warnings, w => w.Contains("estado de partida"));
        // La 11 se cierra sin ninguna bola (la captura acaba ahí), pero con su T.
        Assert.All(partidas.Take(10), p => Assert.NotNull(p.Bolas));
        Assert.Null(partidas[10].Bolas);
    }

    private static List<HistoricoDTO> ReproducirCaptura(PartidaAccumulator acumulador, string captura) {
        var partidas = new List<HistoricoDTO>();
        foreach (string trozo in captura.Split('#')) {
            foreach (string fragmento in TagEventParser.Trocear(trozo).Eventos) {
                var evt = TagEventParser.Parse(fragmento);
                if (evt == null) continue;
                var dto = acumulador.Feed(evt.Value);
                if (dto != null) partidas.Add(dto);
            }
        }
        return partidas;
    }

    // Las capturas son texto UTF-8 de los bytes leídos como Windows-1252: se
    // deshace eso para tener cada byte original como un char, igual que lo
    // entrega el puerto serie con Encoding.Latin1 (load-bearing para 'e').
    private static string LeerCaptura(string nombre) {
        string texto = File.ReadAllText(Path.Combine(DirectorioDeEsteFichero(), "..", "Gregal", "Docs", nombre));
        byte[] bytes = System.Text.CodePagesEncodingProvider.Instance.GetEncoding(1252)!.GetBytes(texto);
        return System.Text.Encoding.Latin1.GetString(bytes);
    }

    private static string LeerCapturaMetronia() => LeerCaptura("Volcado Metronia.txt");

    [Fact]
    public void Feed_ConCapturaRealDeMetronia_DevuelveLasSietePartidasSinRecuperar() {
        var logger = new RecordingLogger<PartidaAccumulator>();
        var partidas = ReproducirCaptura(CreateAccumulatorSinT(logger: logger), LeerCapturaMetronia());

        Assert.Equal(Enumerable.Range(1, 7), partidas.Select(p => p.PartidaDia));
        Assert.DoesNotContain(logger.Warnings, w => w.Contains("estado de partida"));
        Assert.All(partidas, p => Assert.Null(p.Reclamacion));
    }

    [Fact]
    public void Feed_ConCapturaDeMetroniaEmpezadaAMitadDePartida_RecuperaLaPartidaUnoAlLlegarT2() {
        string captura = LeerCapturaMetronia();
        var completa = ReproducirCaptura(CreateAccumulatorSinT(), captura);

        // Empezamos a escuchar justo en la 10ª bola de la partida 1: se pierden
        // T1, la cabecera (precio, cartones, premios) y las 9 primeras *E#.
        int corte = System.Text.RegularExpressions.Regex.Matches(captura, @"\*E\d+#")[9].Index;
        var partidas = ReproducirCaptura(CreateAccumulatorSinT(), captura[corte..]);

        Assert.Equal(Enumerable.Range(1, 7), partidas.Select(p => p.PartidaDia));
        var recuperada = partidas[0];
        // Las 9 bolas anteriores vuelven gracias a la cadena de estado 'e'
        // (en otro orden), y se marca en RECLAMACION.
        Assert.Equal(completa[0].Extracciones, recuperada.Extracciones);
        Assert.Equal(completa[0].Bolas!.Split(',').Order(), recuperada.Bolas!.Split(',').Order());
        Assert.Equal("PARTIDA A MEDIAS: Nº DEDUCIDO, SIN CABECERA, BOLAS APROX.", recuperada.Reclamacion);
        Assert.All(partidas.Skip(1), p => Assert.Null(p.Reclamacion));
        Assert.Equal(completa[0].CartBingo, recuperada.CartBingo);
        Assert.Equal(0m, recuperada.Precio); // la cabecera no se puede recuperar
        Assert.Equal(completa.Skip(1).Select(p => p.Bolas), partidas.Skip(1).Select(p => p.Bolas));
    }

    private static string DirectorioDeEsteFichero([System.Runtime.CompilerServices.CallerFilePath] string ruta = "") =>
        Path.GetDirectoryName(ruta)!;

    [Fact]
    public void Feed_SinCodigoT_EsperaALaSiguienteTYSeGuardaComoLaAnterior() {
        var acumulador = CreateAccumulatorSinT();

        // La captura empieza a mitad de partida: no llegó su *T#.
        acumulador.Feed(EventoDe("*E10#"));
        acumulador.Feed(EventoDe("*E20#"));
        Assert.Null(acumulador.Feed(EventoDe("*F#")));
        acumulador.Feed(EventoDe("*f#"));

        var recuperada = acumulador.Feed(EventoDe("*T7#"));

        Assert.NotNull(recuperada);
        Assert.Equal(6, recuperada!.PartidaDia);
        Assert.Equal("10,20", recuperada.Bolas);
        Assert.Equal("PARTIDA A MEDIAS: Nº DEDUCIDO, SIN CABECERA", recuperada.Reclamacion);

        // La partida 7 sigue su curso con normalidad.
        acumulador.Feed(EventoDe("*E5#"));
        var siguiente = acumulador.Feed(EventoDe("*F#"));
        Assert.Equal(7, siguiente!.PartidaDia);
        Assert.Equal("5", siguiente.Bolas);
        Assert.Null(siguiente.Reclamacion);
    }

    [Fact]
    public void Feed_SinCodigoTYSiguienteT1_DescartaLaPartidaConAviso() {
        var logger = new RecordingLogger<PartidaAccumulator>();
        var acumulador = CreateAccumulatorSinT(logger: logger);

        acumulador.Feed(EventoDe("*E10#"));
        acumulador.Feed(EventoDe("*F#"));
        var resultado = acumulador.Feed(EventoDe("*T1#"));

        Assert.Null(resultado);
        Assert.Contains(logger.Warnings, w => w.Contains("Se descarta la partida sin T") && w.Contains("10"));
    }

    [Fact]
    public void Feed_ConFPerdidoYLlegaF_CierraLaPartida() {
        var logger = new RecordingLogger<PartidaAccumulator>();
        var acumulador = CreateAccumulatorSinT(logger: logger);

        acumulador.Feed(EventoDe("*T4#"));
        acumulador.Feed(EventoDe("*E10#"));
        // Se pierde el *F#; lo siguiente es el *f# de entre partidas.
        var dto = acumulador.Feed(EventoDe("*f#"));

        Assert.Equal(4, dto!.PartidaDia);
        Assert.Equal("10", dto.Bolas);
        Assert.Contains(logger.Warnings, w => w.Contains("se perdió su *F#"));
        Assert.Null(acumulador.Feed(EventoDe("*f#"))); // los siguientes *f# no hacen nada
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
