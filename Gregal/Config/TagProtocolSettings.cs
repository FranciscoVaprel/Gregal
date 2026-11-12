using System.IO.Ports;

namespace Gregal.Config;

// Configuración del protocolo de eventos "tag" (*CODIGO<valor>#), el mismo
// que usa el software de pantallas PidFullHD. Llega por un puerto serie
// dedicado, distinto del que usa NetworkSettings para JSON/CSV. Las reglas
// para interpretar los eventos (Fabricante, PremioCodeMap, SerieLetraMap,
// DefaultCodSala/DefaultCodConc) están en ReglasProtocoloTag, en la librería
// Gregal.Protocolo, para que BingoHistoricoEditor use las mismas al importar
// un log; en appsettings.json siguen en esta misma sección.
public class TagProtocolSettings : ReglasProtocoloTag {
    // Activa/desactiva TagSerialGregalReceiver como IHostedService, desde
    // appsettings.json sin tocar Program.cs. A true por defecto: hoy es el
    // receptor principal (ver NetworkSettings.Enabled para el otro protocolo).
    public bool Enabled { get; set; } = true;

    private string _comPort = "COM1";
    public string ComPort { get => _comPort; set => _comPort = PuertoSerie.Normalizar(value); }
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

    // FallbackFilePath como ruta absoluta (la relativa cuenta desde la carpeta
    // de Gregal.exe, no desde el directorio actual).
    public string FallbackFileFullPath => Path.GetFullPath(Path.IsPathRooted(FallbackFilePath)
        ? FallbackFilePath
        : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FallbackFilePath));

    // Registra en el log (nivel Information) cada fragmento *CODIGO<valor>#
    // tal cual llega, antes de interpretarlo. Útil al desplegar por primera
    // vez en una sala para confirmar qué emite realmente el hardware; se
    // puede poner a false una vez confirmado el protocolo (una línea por
    // evento, puede ser mucho volumen).
    public bool LogRawFrames { get; set; } = true;
}
