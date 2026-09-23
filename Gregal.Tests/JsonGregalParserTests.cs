using Gregal.Parsing;
using Microsoft.Extensions.Logging.Abstractions;

public class JsonGregalParserTests {
    private static JsonGregalParser CreateParser() => new(NullLogger<JsonGregalParser>.Instance);

    [Fact]
    public void Parse_ConEnvoltorioMsgIdYData_ExtraeMsgIdYData() {
        var parser = CreateParser();
        string json = """
        {
            "msg_id": "TX-1",
            "operation": "upsert",
            "data": {
                "codSala": 1,
                "codConc": 1,
                "fechaSesion": "2026-06-09",
                "partidaDia": 12,
                "venta": 500.00
            }
        }
        """;

        var result = parser.Parse(json);

        Assert.NotNull(result);
        Assert.Equal("TX-1", result!.MsgId);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data!.CodSala);
        Assert.Equal(12, result.Data.PartidaDia);
        Assert.Null(result.ValidationError);
    }

    [Fact]
    public void Parse_SinNodoData_ParseaLaRaizDirectamente() {
        var parser = CreateParser();
        string json = """
        {
            "codSala": 5,
            "codConc": 2,
            "fechaSesion": "2026-06-09",
            "partidaDia": 3
        }
        """;

        var result = parser.Parse(json);

        Assert.NotNull(result);
        Assert.NotNull(result!.Data);
        Assert.Equal(5, result.Data!.CodSala);
    }

    [Fact]
    public void Parse_ConJsonMalformado_DevuelveNull() {
        var parser = CreateParser();

        var result = parser.Parse("{ esto no es json valido");

        Assert.Null(result);
    }

    [Fact]
    public void Parse_ConCodSalaAusente_DejaDataANullYRellenaValidationError() {
        var parser = CreateParser();
        string json = """
        {
            "msg_id": "TX-2",
            "data": {
                "codConc": 1,
                "fechaSesion": "2026-06-09",
                "partidaDia": 12
            }
        }
        """;

        var result = parser.Parse(json);

        Assert.NotNull(result);
        Assert.Equal("TX-2", result!.MsgId); // el msg_id se conserva aunque la validación falle
        Assert.Null(result.Data);
        Assert.NotNull(result.ValidationError);
        Assert.Contains("codSala", result.ValidationError);
    }

    [Fact]
    public void Parse_ConFechaSesionConFormatoInvalido_DejaDataANull() {
        var parser = CreateParser();
        string json = """
        {
            "msg_id": "TX-3",
            "data": {
                "codSala": 1,
                "codConc": 1,
                "fechaSesion": "09/06/2026",
                "partidaDia": 12
            }
        }
        """;

        var result = parser.Parse(json);

        Assert.NotNull(result);
        Assert.Null(result!.Data);
        Assert.NotNull(result.ValidationError);
        Assert.Contains("fechaSesion", result.ValidationError);
    }
}
