using Gregal.Models;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Gregal.Parsing;

public class CsvGregalParser : IParser {
    private readonly ILogger<CsvGregalParser> _logger;

    public CsvGregalParser(ILogger<CsvGregalParser> logger) {
        _logger = logger;
    }

    public ParseResult? Parse(string data) {
        if (string.IsNullOrWhiteSpace(data)) return null;
        
        // Ignorar cabeceras si el tester las envía por error
        if (data.StartsWith("msg_id", StringComparison.OrdinalIgnoreCase) ||
            data.StartsWith("codSala", StringComparison.OrdinalIgnoreCase)) return null;

        var cols = data.Trim().Split(';');
        if (cols.Length < 5) return null;

        try {
            var result = new ParseResult {
                MsgId = cols[0], // Extraemos el ID que viene del txtMsgId.Text del Tester
                Data = new HistoricoDTO {
                    CodSala = int.Parse(cols[1]),
                    CodConc = int.Parse(cols[2]),
                    FechaSesion = cols[3],
                    PartidaDia = int.Parse(cols[4]),
                    Precio = GetDecimal(cols, 14), // Índices alineados con CsvTramaTransmision
                    Venta = GetDecimal(cols, 15),
                    Caja = GetDecimal(cols, 16),
                    Bolas = cols.Length > 44 ? cols[44] : null
                }
            };
            return result;
        }
        catch (Exception ex) {
            _logger.LogWarning("X ERROR PARSE CSV: {msg}", ex.Message);
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