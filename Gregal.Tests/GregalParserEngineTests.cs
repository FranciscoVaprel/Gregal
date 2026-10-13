using Gregal.Parsing;
using Microsoft.Extensions.Logging.Abstractions;

public class GregalParserEngineTests {
    private static GregalParserEngine CreateEngine() => new(
        new JsonGregalParser(NullLogger<JsonGregalParser>.Instance),
        new CsvGregalParser(NullLogger<CsvGregalParser>.Instance));

    [Theory]
    [InlineData("""{"operation":"ping"}""")]
    [InlineData("""{"operation": "ping"}""")]
    [InlineData("""{ "operation" : "ping" }""")]
    [InlineData("ping")]
    [InlineData("PING")]
    public void Parse_DetectaPingConCualquierFormatoDeEspacios(string rawData) {
        var result = CreateEngine().Parse(rawData);

        Assert.NotNull(result);
        Assert.True(result!.IsPing);
    }

    [Fact]
    public void Parse_JsonSinOperationPing_NoSeConfundeConPing() {
        string json = """
        {
            "codSala": 1,
            "codConc": 1,
            "fechaSesion": "2026-06-09",
            "partidaDia": 1
        }
        """;

        var result = CreateEngine().Parse(json);

        Assert.NotNull(result);
        Assert.False(result!.IsPing);
    }
}
