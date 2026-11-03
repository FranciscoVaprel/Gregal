using Gregal.Config;
using Microsoft.Extensions.Configuration;

public class PuertoSerieTests {
    [Theory]
    [InlineData("1", "COM1")]
    [InlineData(" 12 ", "COM12")]
    [InlineData("com3", "COM3")]
    [InlineData("COM 4", "COM4")]
    [InlineData("COM9", "COM9")]
    [InlineData("CNCA0", "CNCA0")]
    [InlineData("", "")]
    public void Normalizar_DejaElNombreQueEntiendeWindows(string escrito, string esperado) {
        Assert.Equal(esperado, PuertoSerie.Normalizar(escrito));
    }

    [Fact]
    public void ComPort_LeidoDeLaConfiguracion_SeNormaliza() {
        // Caso real de una sala: ComPort "1" en appsettings.json.
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["TagProtocolSettings:ComPort"] = "1",
            ["NetworkSettings:ComPort"] = "com2",
        }).Build();

        Assert.Equal("COM1", config.GetSection("TagProtocolSettings").Get<TagProtocolSettings>()!.ComPort);
        Assert.Equal("COM2", config.GetSection("NetworkSettings").Get<NetworkSettings>()!.ComPort);
    }
}
