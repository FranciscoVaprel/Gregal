namespace Gregal.Config;

// Lo que PartidaAccumulator necesita saber de la sala para interpretar los
// eventos *CODIGO<valor>#. TagProtocolSettings (en Gregal) hereda de aquí y
// añade lo del puerto serie, así que appsettings.json no cambia; quien
// reconstruye partidas sin puerto serie (p. ej. desde un log, en
// BingoHistoricoEditor) usa esta clase directamente.
public class ReglasProtocoloTag {
    // Fabricante del hardware de la sala. Los dos conocidos usan protocolos
    // parecidos pero no idénticos: en MCB, A/M/p son ruido constante del
    // hardware y se ignoran aunque estén en PremioCodeMap (el fabricante
    // manda sobre el mapa para esos tres códigos); en Metronia son premios
    // reales (contrastado con tickets impresos por la máquina).
    public Fabricante Fabricante { get; set; } = Fabricante.Metronia;

    // Letra de código del protocolo -> índice 0-based en HistoricoDTO.Premios
    // (Premios[i] corresponde a la columna PREMIO{i+1}), confirmado por el
    // operador de la sala: PREMIO1=línea (L), PREMIO2=bingo (B), PREMIO5=línea
    // plus (p), PREMIO6=prima (A), PREMIO7=bola tope de prima (M). p/A/M solo
    // se aplican con Fabricante=Metronia (ver arriba). G, S y R no están aquí
    // porque se ignoran siempre, en cualquier fabricante (ver
    // PartidaAccumulator.Aplicar).
    public Dictionary<string, int> PremioCodeMap { get; set; } = new() {
        ["L"] = 0,
        ["B"] = 1,
        ["p"] = 4,
        ["A"] = 5,
        ["M"] = 6
    };

    // Código de longitud de serie (evento J) -> letra de edición del cartón
    // (HistoricoDTO.Letra1/Letra2). Confirmado contra tickets reales: J=3864
    // imprime "EB", J=3870 "EC". Relación arbitraria del fabricante, ajustable
    // aquí sin tocar código si aparece una edición nueva.
    public Dictionary<int, string> SerieLetraMap { get; set; } = new() {
        [3864] = "EB",
        [3870] = "EC"
    };

    // Placeholders: el protocolo no lleva CodSala/CodConc. Mientras no se
    // decida cómo obtenerlos (o, en un despliegue de una sola sala, se
    // configuren aquí los valores reales), quedan a 0 y HistoricoDtoValidator
    // rechazará el guardado de la partida (comportamiento intencional: mejor
    // no guardar que inventar una clave de upsert ambigua).
    public int DefaultCodSala { get; set; } = 0;
    public int DefaultCodConc { get; set; } = 0;
}

public enum Fabricante {
    Metronia,
    MCB
}
