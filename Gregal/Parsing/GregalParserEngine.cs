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

            var result = new ParseResult();

            // 1. Detección de PING (Común para ambos)
            if (cleanData.ToLower().Contains("\"operation\":\"ping\"") || cleanData.ToLower() == "ping") {
                result.IsPing = true;
                // Si es JSON, intentamos sacar el MsgId real
                return result;
            }

            // 2. ¿Es un JSON?
            if (cleanData.StartsWith("{")) {
                result.Data = _jsonParser.Parse(cleanData);
                // Aquí podrías extraer el MsgId del JSON si fuera necesario
                return result;
            }

            // 3. ¿Es un CSV?
            if (!string.IsNullOrEmpty(cleanData)) {
                result.Data = _csvParser.Parse(cleanData); //
                return result;
            }

            return null;
        }
    }
}