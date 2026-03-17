using Gregal.Models;
using TuProyecto.Models;

namespace Gregal.Parsing {
    public interface IParser {
        Historico Parse(string data);
    }
}