using System.IO.Ports;

namespace Gregal.Config;

public class NetworkSettings {
    // Activa/desactiva TcpGregalReceiver y SerialGregalReceiver (el protocolo
    // JSON/CSV original) juntos, como IHostedService, desde appsettings.json
    // sin tocar Program.cs. A false por defecto: hoy el receptor principal es
    // el del protocolo de eventos (ver TagProtocolSettings.Enabled).
    public bool Enabled { get; set; } = false;

    // Vestigial: no selecciona nada (los dos receptores de arriba van juntos,
    // activados por Enabled). Se mantiene por compatibilidad con config ya
    // desplegada; no lo uses para decidir nada nuevo.
    public string Mode { get; set; } = "TCP";
    public int TcpPort { get; set; }
    public string TcpAddress { get; set; } = "127.0.0.1";
    public List<string> AllowedIps { get; set; } = new();
    public int TcpKeepAliveTime { get; set; } = 60; 
    public int TcpKeepAliveInterval { get; set; } = 10;
    public string ComPort { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;    
    public int TcpReadTimeoutMinutes { get; set; } = 5;
    public int TcpMaxBufferChars { get; set; } = 102400;

    // Parámetros de Sincronización y Línea (Nuevos)
    public int SerialDataBits { get; set; } = 8;
    public Parity SerialParity { get; set; } = Parity.None;
    public StopBits SerialStopBits { get; set; } = StopBits.One;
    public string SerialEncoding { get; set; } = "UTF-8";

    // Delimitador para el protocolo serie.
    // '\0' = byte nulo (igual que TCP, recomendado si el otro extremo es software)
    // '\n' = salto de línea (más común con dispositivos hardware)
    public char SerialDelimiter { get; set; } = '\0';

    // Nuevos parámetros mapeados
    public Handshake SerialHandshake { get; set; } = Handshake.None;
    public bool SerialRtsEnable { get; set; } = false;
    public bool SerialDtrEnable { get; set; } = false;
    public int SerialReadTimeoutMs { get; set; } = -1; // -1 equivale a SerialPort.InfiniteTimeout
    public int SerialWriteTimeoutMs { get; set; } = 2000;
    public int SerialReadBufferSize { get; set; } = 4096;
    public int SerialWriteBufferSize { get; set; } = 4096;

}