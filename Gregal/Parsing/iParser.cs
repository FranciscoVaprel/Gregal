using Gregal.Models;

namespace Gregal.Parsing {
    public interface IParser {
        Historico Parse(string data);
    }
}