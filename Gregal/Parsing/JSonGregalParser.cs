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

    public Historico? Parse(string data) {
        if (string.IsNullOrWhiteSpace(data)) return null;

        // Este bloque solo se compilará y ejecutará si estás en modo Debug
#if DEBUG
        _logger.LogInformation("DEBUG - Trama recibida para parsear: {data}", data);
#endif

        try {
            var resultado = JsonSerializer.Deserialize<Historico>(data, _options);

#if DEBUG
            if (resultado != null) {
                _logger.LogInformation("DEBUG - Objeto deserializado: Sala {sala}, Venta {venta}",
                    resultado.COD_SALA, resultado.VENTA);
            }
#endif
            return resultado;
        }
        catch (JsonException ex) {
            _logger.LogError("Error al deserializar JSON: {msg}. Trama: {data}", ex.Message, data);
            return null;
        }
    }
}