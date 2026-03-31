using Gregal.Models;

namespace Gregal.Parsing;

public interface IParser {
    // Debe devolver HistoricoDTO para que el Engine y el Repo no fallen
    HistoricoDTO? Parse(string data);
}