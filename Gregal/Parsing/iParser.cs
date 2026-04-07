using Gregal.Models;

namespace Gregal.Parsing {
    public interface IParser {
        ParseResult? Parse(string data);
    }
}