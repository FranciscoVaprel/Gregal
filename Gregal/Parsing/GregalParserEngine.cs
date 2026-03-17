using System;
using System.Reflection;

namespace Gregal.Parsing {
    public class GregalParserEngine {
        private readonly JsonGregalParser _jsonParser = new();
        private readonly CsvGregalParser _csvParser = new();

        public Models.Historico ProcessRawData(string rawData) {
            if (string.IsNullOrWhiteSpace(rawData)) return null;

            string cleanData = rawData.Trim();

            // DETECCIÓN DINÁMICA
            if (cleanData.StartsWith("{")) {
                return _jsonParser.Parse(cleanData);
            }
            else {
                // Si no empieza por llave, asumimos CSV
                return _csvParser.Parse(cleanData);
            }
        }
    }
}