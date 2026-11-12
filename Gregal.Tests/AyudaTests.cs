using Gregal.Diagnostics;

public class AyudaTests {
    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    [InlineData("/?")]
    [InlineData("-?")]
    [InlineData("--HELP")]
    public void PideAyuda_ReconoceLasFormasHabituales(string arg) {
        Assert.True(Ayuda.PideAyuda(new[] { arg }));
    }

    [Fact]
    public void PideAyuda_NoConLasOrdenesNormales() {
        Assert.False(Ayuda.PideAyuda(new[] { "--escuchar", "COM3" }));
        Assert.False(Ayuda.PideAyuda(Array.Empty<string>()));
    }

    [Fact]
    public void Texto_ListaTodasLasOrdenes() {
        foreach (string orden in new[] { "--comprobar-bd", "--escuchar", "--reimportar-pendientes", "--version" }) {
            Assert.Contains(orden, Ayuda.Texto);
        }
    }

    [Fact]
    public void PideVersion() {
        Assert.True(Ayuda.PideVersion(new[] { "--version" }));
        Assert.False(Ayuda.PideVersion(new[] { "-v" }));
    }
}
