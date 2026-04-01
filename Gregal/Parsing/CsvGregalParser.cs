using Gregal.Models;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Gregal.Parsing;

public class CsvGregalParser : IParser {
    private readonly ILogger<CsvGregalParser> _logger;

    public CsvGregalParser(ILogger<CsvGregalParser> logger) {
        _logger = logger;
    }

    public HistoricoDTO? Parse(string data) {
        var cols = data.Trim().Split(';');

        // Validación mínima: Al menos Sala, Concesionario, Fecha y Partida
        if (cols.Length < 4) {
            _logger.LogWarning("X CSV incompleto: faltan columnas básicas. Datos: {data}", data);
            return null;
        }

        try {
            // Usamos una variable auxiliar para validar cada número crítico
            if (!int.TryParse(cols[0], out int codSala)) return LogFormatError("CodSala", cols[0]);
            if (!int.TryParse(cols[1], out int codConc)) return LogFormatError("CodConc", cols[1]);
            if (!int.TryParse(cols[3], out int partida)) return LogFormatError("PartidaDia", cols[3]);

            var dto = new HistoricoDTO {
                CodSala = codSala,
                CodConc = codConc,
                FechaSesion = cols[2], // La fecha se valida en el Repositorio/BBDD
                PartidaDia = partida,
                Venta = GetDecimal(cols, 4),
                Caja = GetDecimal(cols, 5),
                Bolas = cols.Length > 6 ? cols[6] : null
            };

            // Mapeo de premios con el ajuste de seguridad que comentamos
            for (int i = 1; i <= 99; i++) {
                int colIndex = 6 + i;
                dto.Premios[i - 1] = (cols.Length > colIndex) ? GetDecimal(cols, colIndex) : 0m;
            }

            return dto;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "X ERROR crítico inesperado parseando CSV");
            return null;
        }
    }

    // Método auxiliar para limpiar el código y loguear el error exacto
    private HistoricoDTO? LogFormatError(string campo, string valor) {
        _logger.LogWarning("X ERROR DE FORMATO: El campo {campo} debe ser numérico. Recibido: '{valor}'", campo, valor);
        return null;
    }

    private decimal GetDecimal(string[] cols, int index) {
        if (index >= cols.Length || string.IsNullOrWhiteSpace(cols[index])) return 0m;
        decimal.TryParse(cols[index], NumberStyles.Any, CultureInfo.InvariantCulture, out decimal val);
        return val;
    }
}