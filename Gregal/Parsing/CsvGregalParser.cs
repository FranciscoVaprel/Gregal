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
#if DEBUG
        _logger.LogInformation("DEBUG - Trama CSV recibida: {data}", data);
#endif

        var cols = data.Trim().Split(';');
        if (cols.Length < 4) {
            _logger.LogWarning("CSV inválido o incompleto: {data}", data);
            return null;
        }

        try {
            var dto = new HistoricoDTO {
                CodSala = int.Parse(cols[0]),
                CodConc = int.Parse(cols[1]),
                FechaSesion = cols[2],
                PartidaDia = int.Parse(cols[3]),
                Venta = GetDecimal(cols, 4),
                Caja = GetDecimal(cols, 5),
                Bolas = cols.Length > 6 ? cols[6] : null
            };

            // Mapeo exhaustivo de premios (99 columnas)
            for (int i = 1; i <= 99; i++) {
                int colIndex = 6 + i;
                if (cols.Length > colIndex) {
                    dto.Premios[i - 1] = GetDecimal(cols, colIndex);
                }
            }

            return dto;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error crítico parseando CSV");
            return null;
        }
    }

    private decimal GetDecimal(string[] cols, int index) {
        if (index >= cols.Length || string.IsNullOrWhiteSpace(cols[index])) return 0m;
        decimal.TryParse(cols[index], NumberStyles.Any, CultureInfo.InvariantCulture, out decimal val);
        return val;
    }
}