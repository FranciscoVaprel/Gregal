using System.Globalization;
using Gregal.Config;

namespace Gregal.Tests;

public class SesionBingoTests {
    [Theory]
    [InlineData("2026-11-15 00:00:00", "2026-11-14")]
    [InlineData("2026-11-15 06:59:59", "2026-11-14")]
    [InlineData("2026-11-15 07:00:00", "2026-11-15")]
    [InlineData("2026-11-15 23:59:59", "2026-11-15")]
    [InlineData("2026-01-01 03:00:00", "2025-12-31")]   // cambio de año
    [InlineData("2026-03-01 03:00:00", "2026-02-28")]   // cambio de mes
    public void FechaDe_PorDefecto_CambiaALas7(string momento, string esperada) {
        var fecha = SesionBingo.FechaDe(DateTime.Parse(momento, CultureInfo.InvariantCulture));
        Assert.Equal(esperada, fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void FechaDe_ConHoraFueraDeRango_UsaLaDePorDefecto() {
        var momento = new DateTime(2026, 11, 15, 3, 0, 0);
        Assert.Equal(new DateTime(2026, 11, 14), SesionBingo.FechaDe(momento, TimeSpan.FromHours(25)));
        Assert.Equal(new DateTime(2026, 11, 14), SesionBingo.FechaDe(momento, TimeSpan.FromHours(-1)));
    }

    [Fact]
    public void FechaDe_ConCambioAMedianoche_EsElDiaDeCalendario() {
        Assert.Equal(new DateTime(2026, 11, 15), SesionBingo.FechaDe(new DateTime(2026, 11, 15, 0, 30, 0), TimeSpan.Zero));
    }
}
