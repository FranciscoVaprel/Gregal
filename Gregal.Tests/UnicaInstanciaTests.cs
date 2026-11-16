using Gregal.Utils;

namespace Gregal.Tests;

public class UnicaInstanciaTests {
    [Fact]
    public void Intentar_ConOtroEnMarcha_DevuelveNullYAlCerrarseElPrimeroYaSePuede() {
        // Nombre propio del test: no choca con un Gregal real abierto en el PC.
        string nombre = @"Local\Gregal-Test-" + Guid.NewGuid().ToString("N");

        var primero = UnicaInstancia.Intentar(nombre);
        Assert.NotNull(primero);
        Assert.Null(UnicaInstancia.Intentar(nombre));

        primero!.Dispose();
        using var otraVez = UnicaInstancia.Intentar(nombre);
        Assert.NotNull(otraVez);
    }
}
