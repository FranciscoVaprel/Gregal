using Gregal.Models;
using Gregal.Parsing;

public class HistoricoDtoValidatorTests {
    private static HistoricoDTO ValidDto() => new() {
        CodSala = 1,
        CodConc = 1,
        PartidaDia = 12,
        FechaSesion = "2026-06-09"
    };

    [Fact]
    public void ValidateRequiredFields_ConDatosValidos_DevuelveNull() {
        var dto = ValidDto();

        var error = HistoricoDtoValidator.ValidateRequiredFields(dto);

        Assert.Null(error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidateRequiredFields_ConCodSalaInvalido_DevuelveError(int codSala) {
        var dto = ValidDto();
        dto.CodSala = codSala;

        var error = HistoricoDtoValidator.ValidateRequiredFields(dto);

        Assert.NotNull(error);
        Assert.Contains("codSala", error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidateRequiredFields_ConCodConcInvalido_DevuelveError(int codConc) {
        var dto = ValidDto();
        dto.CodConc = codConc;

        var error = HistoricoDtoValidator.ValidateRequiredFields(dto);

        Assert.NotNull(error);
        Assert.Contains("codConc", error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidateRequiredFields_ConPartidaDiaInvalido_DevuelveError(int partidaDia) {
        var dto = ValidDto();
        dto.PartidaDia = partidaDia;

        var error = HistoricoDtoValidator.ValidateRequiredFields(dto);

        Assert.NotNull(error);
        Assert.Contains("partidaDia", error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("09/06/2026")]      // formato incorrecto (no ISO)
    [InlineData("2026-13-40")]      // fecha inexistente
    public void ValidateRequiredFields_ConFechaSesionInvalida_DevuelveError(string? fechaSesion) {
        var dto = ValidDto();
        dto.FechaSesion = fechaSesion!;

        var error = HistoricoDtoValidator.ValidateRequiredFields(dto);

        Assert.NotNull(error);
        Assert.Contains("fechaSesion", error);
    }
}
