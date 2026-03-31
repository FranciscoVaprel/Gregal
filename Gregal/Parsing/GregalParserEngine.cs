using Gregal.Models;
using Microsoft.Extensions.Logging;

namespace Gregal.Parsing {
    public class GregalParserEngine : IParser {
        private readonly IParser _jsonParser;
        private readonly IParser _csvParser;

        public GregalParserEngine(JsonGregalParser jsonParser, CsvGregalParser csvParser) {
            _jsonParser = jsonParser;
            _csvParser = csvParser;
        }

        // CAMBIO: Renombramos 'ProcessRawData' a 'Parse' para cumplir con la interfaz
        public HistoricoDTO? Parse(string rawData) {
            // 1. Limpieza inicial: Si no hay texto, no hay nada que hacer
            if (string.IsNullOrWhiteSpace(rawData)) {
                return null;
            }

            string cleanData = rawData.Trim();

            // 2. ¿Es un JSON? (Empieza por llave)
            if (cleanData.StartsWith("{")) {
                // El flujo se desvía al especialista en JSON
                return _jsonParser.Parse(cleanData);
            }

            // 3. ¿Es un CSV? (Si no es JSON y tiene comas o es texto plano)
            // Aquí podrías añadir más lógica si fuera necesario, 
            // pero por ahora el "else" es el CSV.
            if (!string.IsNullOrEmpty(cleanData)) {
                // El flujo se desvía al especialista en CSV
                return _csvParser.Parse(cleanData);
            }

            return null;
        }
    }
}