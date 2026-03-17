using Gregal.Models;
using System.Text.Json;
using TuProyecto.Models;

namespace Gregal.Parsing {
    public class JsonGregalParser : IParser {
        private readonly JsonSerializerOptions _options = new() {
            PropertyNameCaseInsensitive = true // Para que no importe si envían "codsala" o "CodSala"
        };

        public Historico Parse(string data) {
            return JsonSerializer.Deserialize<Historico>(data, _options);
        }
    }
}