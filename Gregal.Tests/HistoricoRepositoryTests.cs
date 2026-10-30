using Gregal.Data;

public class HistoricoRepositoryTests {
    private static readonly string NoventaBolas = string.Join(",", Enumerable.Range(1, 90)); // 260 caracteres

    [Fact]
    public void HoraFin_SiNoVieneInformada_UsaLaHoraDelGuardado() {
        Assert.Equal("21:03:07", FilaHistorico.HoraFinAlGuardar(null, new DateTime(2026, 10, 30, 21, 3, 7)));
    }

    [Fact]
    public void HoraFin_SiYaVieneInformada_LaRespeta() {
        Assert.Equal("20:59:00", FilaHistorico.HoraFinAlGuardar("20:59:00", new DateTime(2026, 10, 30, 21, 3, 7)));
    }

    [Fact]
    public void AjustarBolas_SiCabe_NoTocaNada() {
        var (bolas, reclamacion, descartadas) = FilaHistorico.AjustarBolasALaColumna("5,10,20", null, 255);

        Assert.Equal("5,10,20", bolas);
        Assert.Null(reclamacion);
        Assert.Equal(0, descartadas);
    }

    [Fact]
    public void AjustarBolas_SiNoCabe_GuardaLasPrimerasBolasEnterasYLoAnota() {
        var (bolas, reclamacion, descartadas) = FilaHistorico.AjustarBolasALaColumna(NoventaBolas, null, 255);

        Assert.True(bolas!.Length <= 255);
        Assert.StartsWith(bolas + ",", NoventaBolas + ","); // las primeras, sin partir ningún número
        Assert.Equal(2, descartadas);                      // "1,...,88" ocupa 254; sobran 89 y 90
        Assert.EndsWith(",88", bolas);
        Assert.Equal("BOLAS RECORTADAS", reclamacion);
    }

    [Fact]
    public void AjustarBolas_SiLaComaCaeJustoEnElLimite_AprovechaTodoElEspacio() {
        // "1,2,...,9" = 17 caracteres y lo siguiente es ",10": con límite 17 cabe justo.
        var (bolas, _, descartadas) = FilaHistorico.AjustarBolasALaColumna(NoventaBolas, null, 17);

        Assert.Equal("1,2,3,4,5,6,7,8,9", bolas);
        Assert.Equal(81, descartadas);
    }

    [Fact]
    public void AjustarBolas_ConReclamacionPrevia_AntePoneLaNotaSinPasarDe64() {
        const string previa = "PARTIDA A MEDIAS: Nº DEDUCIDO, SIN CABECERA, BOLAS APROX.";

        var (_, reclamacion, _) = FilaHistorico.AjustarBolasALaColumna(NoventaBolas, previa, 255);

        Assert.StartsWith("BOLAS RECORTADAS | PARTIDA A MEDIAS", reclamacion);
        Assert.Equal(64, reclamacion!.Length);
    }

    [Fact]
    public void AjustarBolas_ConLaColumnaAmpliadaA300_NoRecortaNiNoventaBolas() {
        var (bolas, reclamacion, descartadas) = FilaHistorico.AjustarBolasALaColumna(NoventaBolas, null, 300);

        Assert.Equal(NoventaBolas, bolas);
        Assert.Null(reclamacion);
        Assert.Equal(0, descartadas);
    }
}
