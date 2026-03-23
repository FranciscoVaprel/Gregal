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

        public Historico? ProcessRawData(string rawData) {
            if (string.IsNullOrWhiteSpace(rawData)) return null;

            string cleanData = rawData.Trim();

            // DETECCIÓN DINÁMICA
            if (cleanData.StartsWith("{")) {
                return _jsonParser.Parse(cleanData);
            }
            else {
                return _csvParser.Parse(cleanData);
            }
        }
    }
}