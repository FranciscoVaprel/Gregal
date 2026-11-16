using Gregal.Data;
using Gregal.Models;

namespace Gregal.Tests;

public class FilaHistoricoTests {
    private static object ValorDe(HistoricoDTO dto, string columna) {
        var (valores, _) = FilaHistorico.Valores(dto, new DateTime(2026, 11, 16, 12, 0, 0), 255);
        return valores[FilaHistorico.Columnas.ToList().IndexOf(columna)];
    }

    [Theory]
    [InlineData("2026-01-31", 1)]   // sesión del 31/01, aunque se juegue de madrugada del 01/02
    [InlineData("2026-11-14", 11)]
    [InlineData("2026-12-31", 12)]
    public void FechaSesionMes_SinValor_EsElMesDeLaSesion(string fechaSesion, int mes) {
        var dto = new HistoricoDTO { CodSala = 90, CodConc = 999, FechaSesion = fechaSesion, PartidaDia = 1 };
        Assert.Equal(mes, ValorDe(dto, "FECHA_SESION_MES"));
    }

    [Fact]
    public void FechaSesionMes_ConValorDelProtocolo_SeRespeta() {
        var dto = new HistoricoDTO { CodSala = 90, CodConc = 999, FechaSesion = "2026-11-14", PartidaDia = 1, FechaSesionMes = "7" };
        Assert.Equal("7", ValorDe(dto, "FECHA_SESION_MES"));
    }

    [Fact]
    public void FechaSesionMes_SinFechaLegible_EsCero() {
        var dto = new HistoricoDTO { CodSala = 90, CodConc = 999, FechaSesion = "", PartidaDia = 1 };
        Assert.Equal(0, ValorDe(dto, "FECHA_SESION_MES"));
    }
}
