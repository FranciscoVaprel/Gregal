using System.Text.Json;
using System.Text.Json.Serialization;
using Gregal.Models;
using Microsoft.Extensions.Logging;

namespace Gregal.Parsing;

public class JsonGregalParser : IParser {
    private readonly ILogger<JsonGregalParser> _logger;

    private static readonly JsonSerializerOptions _options = new() {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true
    };

    public JsonGregalParser(ILogger<JsonGregalParser> logger) {
        _logger = logger;
    }

    public ParseResult? Parse(string data) {
        if (string.IsNullOrWhiteSpace(data)) return null;

        try {
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;

            var result = new ParseResult();

            // 1. Extraemos el msg_id de la raíz del JSON (generado por el Tester)
            if (root.TryGetProperty("msg_id", out var idProp)) {
                result.MsgId = idProp.GetString() ?? "unknown";
            }

            // 2. Intentamos extraer los datos del nodo "data" si existe
            if (root.TryGetProperty("data", out var dataElement)) {
                result.Data = JsonSerializer.Deserialize<HistoricoDTO>(dataElement.GetRawText(), _options);
            }
            else {
                // Si no hay nodo "data", intentamos parsear la raíz directamente
                result.Data = JsonSerializer.Deserialize<HistoricoDTO>(data, _options);
            }

            if (result.Data != null) {
                LogSuccess(result.Data);
            }

            return result;
        }
        catch (JsonException ex) {
            _logger.LogError("Error al deserializar JSON: {msg}. Trama: {data}", ex.Message, data);
            return null;
        }
    }

    private void LogSuccess(HistoricoDTO? dto) {
#if DEBUG
        if (dto != null) {
            _logger.LogInformation("DEBUG - JSON mapeado: Sala {sala}, Partida {partida}", dto.CodSala, dto.PartidaDia);
        }
#endif
    }
}