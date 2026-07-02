using System.Text.Json;
using Gregal.Models; // ◄── Ya encuentra tu espacio de nombres real

public class JsonParserTests {
    private static readonly JsonSerializerOptions _options = new() {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    [Fact]
    public void Parser_DeberiaMapearJsonAHistoricoDTOCorrectamente() {
        // 1. Arrange: Simulamos el JSON real que llega al servicio con tus campos
        string jsonTrama = """
        {
            "codSala": 999,
            "codConc": 1,
            "fechaSesion": "2026-06-29",
            "partidaDia": 12,
            "precio": 2.00,
            "venta": 150.50,
            "caja": 75.00
        }
        """;
        
        // 2. Act: Deserializamos usando tu clase real
        var resultado = JsonSerializer.Deserialize<HistoricoDTO>(jsonTrama, _options);

        // 3. Assert: Validamos que los datos se han parseado correctamente
        Assert.NotNull(resultado);
        Assert.Equal(999, resultado.CodSala);
        Assert.Equal(1, resultado.CodConc);
        Assert.Equal("2026-06-29", resultado.FechaSesion);
        Assert.Equal(12, resultado.PartidaDia);
        Assert.Equal(2.00m, resultado.Precio);
        Assert.Equal(150.50m, resultado.Venta);
        Assert.Equal(75.00m, resultado.Caja);
    }
}