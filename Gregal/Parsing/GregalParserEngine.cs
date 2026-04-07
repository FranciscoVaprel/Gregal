using Gregal.Models;
using Microsoft.Extensions.Logging;

namespace Gregal.Parsing {
    public class GregalParserEngine {
        private readonly IParser _jsonParser;
        private readonly IParser _csvParser;

        public GregalParserEngine(JsonGregalParser jsonParser, CsvGregalParser csvParser) {
            _jsonParser = jsonParser;
            _csvParser = csvParser;
        }

        // CAMBIO: Renombramos 'ProcessRawData' a 'Parse' para cumplir con la interfaz
        public ParseResult? Parse(string rawData) {
            if (string.IsNullOrWhiteSpace(rawData)) return null;
            string cleanData = rawData.Trim();

            // 1. Detección de PING
            if (cleanData.ToLower().Contains("\"operation\":\"ping\"") || cleanData.ToLower() == "ping") {
                return new ParseResult { IsPing = true, MsgId = "ping" };
            }

            // 2. ¿Es un JSON?
            if (cleanData.StartsWith("{")) {
                return _jsonParser.Parse(cleanData); // El JsonParser ahora devuelve el Result completo
            }

            // 3. ¿Es un CSV? (Cualquier otra cosa que no sea JSON y tenga contenido)
            return _csvParser.Parse(cleanData); // El CsvParser ahora devuelve el Result completo
        }
    }
}