using System.Text.Json;
using System.Text.Json.Serialization;
using Gregal.Models;
using Microsoft.Extensions.Logging;

namespace Gregal.Parsing;

public class JsonGregalParser : IParser {
    private readonly ILogger<JsonGregalParser> _logger;

    private static readonly JsonSerializerOptions _options = new() {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true // Esto permite leer 'codSala' como 'COD_SALA'
    };

    public JsonGregalParser(ILogger<JsonGregalParser> logger) {
        _logger = logger;
    }

    public Historico? Parse(string data) {
        if (string.IsNullOrWhiteSpace(data)) return null;

#if DEBUG
        _logger.LogInformation("DEBUG - Trama recibida: {data}", data);
#endif

        try {
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;

            // 1. Validar que existe el nodo "data" enviado por el Tester
            if (!root.TryGetProperty("data", out var dataElement)) {
                _logger.LogWarning("El JSON no contiene el nodo 'data'. Se aborta el parseo.");
                return null;
            }

            // 2. Deserializar solo el contenido de "data"
            var resultado = JsonSerializer.Deserialize<Historico>(dataElement.GetRawText(), _options);

#if DEBUG
            if (resultado != null) {
                _logger.LogInformation("DEBUG - Objeto mapeado con éxito: Sala {sala}", resultado.COD_SALA);
            }
#endif
            return resultado;
        }
        catch (JsonException ex) {
            _logger.LogError("Error al deserializar: {msg}", ex.Message);
            return null;
        }
    }
}