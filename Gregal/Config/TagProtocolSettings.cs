using System.IO.Ports;

namespace Gregal.Config;

// Configuración del protocolo de eventos "tag" (*CODIGO<valor>#), el mismo
// que usa el software de pantallas PidFullHD. Llega por un puerto serie
// dedicado, distinto del que usa NetworkSettings para JSON/CSV.
public class TagProtocolSettings {
    // Activa/desactiva TagSerialGregalReceiver como IHostedService, desde
    // appsettings.json sin tocar Program.cs. A true por defecto: hoy es el
    // receptor principal (ver NetworkSettings.Enabled para el otro protocolo).
    public bool Enabled { get; set; } = true;

    public string ComPort { get; set; } = "COM9";
    public int BaudRate { get; set; } = 9600;
    public int SerialDataBits { get; set; } = 8;
    public Parity SerialParity { get; set; } = Parity.None;
    public StopBits SerialStopBits { get; set; } = StopBits.One;
    public Handshake SerialHandshake { get; set; } = Handshake.None;
    public bool SerialRtsEnable { get; set; } = false;
    public bool SerialDtrEnable { get; set; } = false;
    public int SerialReadTimeoutMs { get; set; } = -1;
    public int SerialWriteTimeoutMs { get; set; } = 2000;
    public int SerialReadBufferSize { get; set; } = 4096;
    public int SerialWriteBufferSize { get; set; } = 4096;
    public int MaxBufferChars { get; set; } = 102400;

    // Carácter de fin de evento (*CODIGO<valor>#). No confundir con
    // NetworkSettings.SerialDelimiter, que es '\0' para el protocolo JSON/CSV.
    public char FrameDelimiter { get; set; } = '#';

    // Si Firebird rechaza o no puede guardar una partida completa (p.ej. una
    // ráfaga de partidas más rápida de lo que la BBDD puede absorber), el DTO
    // se anexa aquí en vez de perderse, para poder reimportarlo a mano más
    // tarde. Ruta relativa al directorio del ejecutable.
    public string FallbackFilePath { get; set; } = "logs/historico_pendientes_tag.jsonl";

    // Letra de código del protocolo -> índice 0-based en HistoricoDTO.Premios
    // (Premios[i] corresponde a la columna PREMIO{i+1}). L, B, A y p están
    // confirmados contra tickets reales de sala (2026-10-14, "7 partidas
    // Metronia"): L=línea, B=bingo, A=prima, p=línea plus, todos en céntimos.
    // G, S y R quedan como estaban (sin confirmar, no aparecieron en esa
    // captura); se ajusta aquí sin tocar código si hiciera falta.
    public Dictionary<string, int> PremioCodeMap { get; set; } = new() {
        ["L"] = 0,
        ["B"] = 1,
        ["G"] = 2,
        ["A"] = 3,
        ["S"] = 4,
        ["R"] = 5,
        ["p"] = 6
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

    // Registra en el log (nivel Information) cada fragmento *CODIGO<valor>#
    // tal cual llega, antes de interpretarlo. Útil al desplegar por primera
    // vez en una sala para confirmar qué emite realmente el hardware; se
    // puede poner a false una vez confirmado el protocolo (una línea por
    // evento, puede ser mucho volumen).
    public bool LogRawFrames { get; set; } = true;
}
