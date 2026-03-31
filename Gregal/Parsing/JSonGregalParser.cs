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

    public HistoricoDTO? Parse(string data) {
        if (string.IsNullOrWhiteSpace(data)) return null;

#if DEBUG
        _logger.LogInformation("DEBUG - Trama JSON recibida: {data}", data);
#endif

        try {
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;

            // Intentamos extraer del nodo "data" si existe (común en el Tester)
            if (root.TryGetProperty("data", out var dataElement)) {
                var res = JsonSerializer.Deserialize<HistoricoDTO>(dataElement.GetRawText(), _options);
                LogSuccess(res);
                return res;
            }

            // Si no, parseamos la raíz
            var directo = JsonSerializer.Deserialize<HistoricoDTO>(data, _options);
            LogSuccess(directo);
            return directo;
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