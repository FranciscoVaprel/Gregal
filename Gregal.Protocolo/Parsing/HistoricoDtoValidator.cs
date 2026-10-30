using Gregal.Models;

namespace Gregal.Parsing;

// Compartido entre JsonGregalParser y CsvGregalParser: valida los campos de identificación
// obligatorios definidos en Specs.md (sección 3.2): CodSala, CodConc, FechaSesion y PartidaDia.
// Sin ellos, la clave de upsert de Firebird (COD_SALA, COD_CONC, FECHA_SESION, PARTIDA_DIA)
// sería inválida o ambigua (p.ej. 0/"").
public static class HistoricoDtoValidator {
    public static string? ValidateRequiredFields(HistoricoDTO dto) {
        if (dto.CodSala <= 0) return "El campo 'codSala' es obligatorio y debe ser mayor que 0";
        if (dto.CodConc <= 0) return "El campo 'codConc' es obligatorio y debe ser mayor que 0";
        if (dto.PartidaDia <= 0) return "El campo 'partidaDia' es obligatorio y debe ser mayor que 0";
        if (string.IsNullOrWhiteSpace(dto.FechaSesion) ||
            !DateTime.TryParseExact(dto.FechaSesion, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out _)) {
            return "El campo 'fechaSesion' es obligatorio y debe tener formato YYYY-MM-DD";
        }
        return null;
    }
}
