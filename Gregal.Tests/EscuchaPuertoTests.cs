using Gregal.Diagnostics;

public class EscuchaPuertoTests {
    [Fact]
    public void Interpretar_SinArgumentos_PuertoConfiguradoYSinLimite() {
        var (puerto, segundos) = EscuchaPuerto.Interpretar(new[] { "--escuchar" }, "COM1");

        Assert.Equal("COM1", puerto);
        Assert.Null(segundos);
    }

    [Fact]
    public void Legible_EscapaControlYBytesAltos() {
        Assert.Equal(@"*f#\x01*~\x85ab#\x7F", EscuchaPuerto.Legible("*f#\u0001*~\u0085ab#\u007F"));
    }

    [Theory]
    [InlineData(new[] { "--escuchar", "COM3" }, "COM3", null)]
    [InlineData(new[] { "--escuchar", "com3", "10" }, "COM3", 10)]
    [InlineData(new[] { "--escuchar", "10", "COM3" }, "COM3", 10)]
    [InlineData(new[] { "--escuchar", "30" }, "COM1", 30)]
    [InlineData(new[] { "--escuchar", "0" }, "COM1", null)]
    [InlineData(new[] { "--escuchar", "99999" }, "COM1", 3600)]
    [InlineData(new[] { "--escuchar", "COM3", "--TagProtocolSettings:BaudRate=19200" }, "COM3", null)]
    public void Interpretar_PuertoYSegundosEnCualquierOrden(string[] args, string puerto, int? segundos) {
        var r = EscuchaPuerto.Interpretar(args, "COM1");

        Assert.Equal(puerto, r.Puerto);
        Assert.Equal(segundos, r.Segundos);
    }
}
