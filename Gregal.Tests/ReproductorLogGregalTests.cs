using Gregal.Config;
using Gregal.Data;
using Gregal.Models;
using Gregal.Parsing;

// Reconstrucción de partidas desde un log de Gregal (lo usa
// BingoHistoricoEditor para importar un log). El log de ejemplo sigue el
// formato real (outputTemplate de Program.cs) y una partida MCB de Blanes
// (captura del 2026-10-29, partida 1), recortada.
public class ReproductorLogGregalTests {
    private static readonly ReglasProtocoloTag ReglasMcb = new() {
        Fabricante = Fabricante.MCB, DefaultCodSala = 90, DefaultCodConc = 1,
    };

    private static string L(string hora, string evento) =>
        $"2026-10-29 {hora} [INF] Fragmento tag recibido: '{evento}'";

    private static readonly string[] LogDeEjemplo = {
        "2026-10-29 16:59:58.100 [INF] Gregal 1.0.2+3b9e31b arrancando (consola).",
        "2026-10-29 16:59:58.700 [INF] Puerto serie (protocolo tag) abierto: COM7 a 9600 baudios.",
        // Arranca a mitad de partida: una bola y su F, sin T.
        L("17:00:01.000", "*E5"),
        L("17:00:02.000", "*F"),
        L("17:00:03.000", "*f"),
        // Partida 7 completa (la T da número 6 a la anterior).
        L("17:13:30.523", "*T7"),
        L("17:13:30.531", "*M200"),
        L("17:13:30.556", "*J386400001&BIPLUSE1.255"),
        L("17:13:30.560", "*L228"),
        L("17:13:30.573", "*B3000"),
        L("17:13:30.581", "*R412320"),
        L("17:13:30.614", "*z292044"),
        L("17:13:30.623", "*y0"),
        L("17:13:30.631", "*P3369"),
        L("17:13:30.634", "*U3387"),
        L("17:13:30.639", "*C200"),
        L("17:13:30.648", "*V19"),
        L("17:13:30.668", "*eÀÀ"),
        L("17:13:46.992", "*E26"),
        L("17:13:47.000", "*A000"),
        L("17:13:49.578", "*E48"),
        L("17:13:50.000", "*E101"),
        L("17:13:52.146", "*E4"),
        L("17:14:58.909", "*Y"),
        L("17:15:05.248", "*Y3383"),
        L("17:15:05.559", "*H"),
        L("17:15:06.983", "*E49"),
        L("17:17:28.726", "*Y"),
        L("17:17:40.000", "*Y3371"),
        L("17:17:40.845", "*F"),
        "2026-10-29 17:17:42.322 [INF] V PARTIDA GUARDADA: Sala 90, Partida 7 en 199ms",
        L("17:17:41.223", "*f"),
        L("17:17:41.349", "*F"),   // MCB manda un segundo *F#: se ignora
        // Partida 8 cortada por un reinicio de Gregal: se pierde.
        L("17:19:43.888", "*T8"),
        L("17:19:56.517", "*E74"),
        "2026-10-29 17:20:00.000 [INF] Gregal 1.0.2+3b9e31b arrancando (consola).",
        // Partida 9 sin terminar al final del log.
        L("17:25:49.494", "*T9"),
        L("17:26:05.541", "*E33"),
    };

    private static ResultadoReproduccion Reproducir(params string[] lineas) =>
        ReproductorLogGregal.Reproducir(new StringReader(string.Join("\r\n", lineas)), ReglasMcb);

    [Fact]
    public void Reproducir_ConLogDeEjemplo_DevuelveLaPartidaSinTYLaCompleta() {
        var r = Reproducir(LogDeEjemplo);

        Assert.Equal(new[] { 6, 7 }, r.Partidas.Select(p => p.Partida.PartidaDia));
        Assert.Equal(2, r.Arranques);
        Assert.Equal(new DateTime(2026, 10, 29, 17, 0, 1), r.PrimerEvento);
        Assert.Equal(new DateTime(2026, 10, 29, 17, 26, 5, 541), r.UltimoEvento);
    }

    [Fact]
    public void Reproducir_PartidaCompleta_TieneLosDatosYLasHorasDelLog() {
        var p = Reproducir(LogDeEjemplo).Partidas.Single(x => x.Partida.PartidaDia == 7).Partida;

        Assert.Equal(90, p.CodSala);
        Assert.Equal(1, p.CodConc);
        Assert.Equal("2026-10-29", p.FechaSesion);
        Assert.Equal(new DateTime(2026, 10, 29, 17, 13, 30, 523), p.Fecha);
        Assert.Equal("17:13:30", p.HoraVenta);
        Assert.Equal("17:17:40", p.HoraFin);
        Assert.Equal(2.00m, p.Precio);
        Assert.Equal(19, p.Vendidos);
        Assert.Equal(38.00m, p.Venta);
        Assert.Equal((3369, 3387), (p.Del1, p.Al1));
        Assert.Equal(292044, p.Serie1);
        Assert.Null(p.Serie2);
        Assert.Equal("EB", p.Letra1);
        Assert.Equal(2.28m, p.Premios[0]);
        Assert.Equal(30.00m, p.Premios[1]);
        Assert.Null(p.Premios[6]);          // M200 es ruido en MCB
        Assert.Equal(2, p.Premios[16]);     // bola plus tras 2 bolas
        Assert.Equal("26,48,4,49", p.Bolas);
        Assert.Equal(("3383", "17:15:05"), (p.CartLinea, p.HoraLinea));
        Assert.Equal(3, p.BolaLinea);
        Assert.Equal(("3371", "17:17:40"), (p.CartBingo, p.HoraBingo));
        Assert.Null(p.Reclamacion);
    }

    [Fact]
    public void Reproducir_PartidaSinT_SeNumeraConLaTSiguienteYGuardaSuHoraDeCierre() {
        var r = Reproducir(LogDeEjemplo);
        var sinT = r.Partidas.Single(x => x.Partida.PartidaDia == 6);

        Assert.Equal("5", sinT.Partida.Bolas);
        Assert.Equal("17:00:02", sinT.Partida.HoraFin);
        Assert.StartsWith("PARTIDA A MEDIAS", sinT.Partida.Reclamacion);
        Assert.Equal(new DateTime(2026, 10, 29, 17, 13, 30, 523), sinT.Hora);   // la de la T que le dio número
    }

    [Fact]
    public void Reproducir_AvisaDeLaPartidaCortadaPorUnReinicioYDeLaQueQuedaAbierta() {
        var avisos = Reproducir(LogDeEjemplo).Avisos.Select(a => a.Texto).ToList();

        Assert.Contains(avisos, a => a.StartsWith("Gregal se reinició con una partida a medias"));
        Assert.Contains(avisos, a => a.StartsWith("El log termina con una partida a medias"));
        Assert.Contains(avisos, a => a.StartsWith("Partida sin evento T"));
    }

    [Fact]
    public void Reproducir_SinSalaConfigurada_DescartaLasPartidasComoGregal() {
        var r = ReproductorLogGregal.Reproducir(new StringReader(string.Join("\n", LogDeEjemplo)),
            new ReglasProtocoloTag { Fabricante = Fabricante.MCB });

        Assert.Empty(r.Partidas);
        Assert.Contains(r.Avisos, a => a.Texto.Contains("codSala"));
    }

    [Fact]
    public void Reproducir_LogSinFragmentos_NoDevuelveNada() {
        var r = Reproducir(
            "2026-10-29 12:35:06.111 [INF] Gregal 1.0.2 arrancando (consola).",
            "2026-10-29 12:35:14.505 [INF] Application is shutting down...");

        Assert.Empty(r.Partidas);
        Assert.Equal(0, r.Eventos);
        Assert.Null(r.PrimerEvento);
    }

    [Fact]
    public void FilaHistorico_TieneUnValorPorColumnaYLosPorDefectoDeGregal() {
        var dto = new HistoricoDTO { CodSala = 90, CodConc = 1, FechaSesion = "2026-10-29", PartidaDia = 7 };
        dto.Premios[16] = 28;

        var (valores, descartadas) = FilaHistorico.Valores(dto, new DateTime(2026, 10, 29, 21, 3, 7), 255);
        object V(string columna) => valores[FilaHistorico.Columnas.ToList().IndexOf(columna)];

        Assert.Equal(150, FilaHistorico.Columnas.Count);
        Assert.Equal(FilaHistorico.Columnas.Count, valores.Count);
        Assert.Equal(0, descartadas);
        Assert.Equal(FilaHistorico.DecimalesPorDefecto, V("NUM_DECIMALES"));
        Assert.Equal("21:03:07", V("HORA_FIN"));
        Assert.Equal(DBNull.Value, V("BOLAS"));
        Assert.Equal(0, V("SERIE_2"));
        Assert.Equal(0m, V("PREMIO1"));
        Assert.Equal(28m, V("PREMIO17"));
    }

    [Fact]
    public void FilaHistorico_SqlUpdateOrInsert_RechazaNombresDeTablaRaros() {
        Assert.Contains("\"HISTORICO_LOG\"", FilaHistorico.SqlUpdateOrInsert("HISTORICO_LOG"));
        Assert.Throws<InvalidOperationException>(() => FilaHistorico.SqlUpdateOrInsert("HISTORICO; DROP TABLE X"));
    }
}
