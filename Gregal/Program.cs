using Gregal.Config;
using Gregal.Data;
using Gregal.Diagnostics;
using Gregal.Networking;
using Gregal.Parsing;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;
using System.IO.Ports;

// Gregal solo se despliega en Windows (servicio de Windows, hardware RS232
// local): marca el ensamblado entero para no avisar (CA1416) al usar APIs
// de Windows como InfoSistema.DescribirRam().
[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]

var builder = Host.CreateApplicationBuilder(args);
// sc.exe create "GregalReceiverService" binPath= "C:\Deploy\Gregal\Gregal.exe"
// start= auto displayname= "Gregal - Receptor de Bingo"
// 🚀 Indicar a .NET que gestione el ciclo de vida como Servicio de Windows
builder.Services.AddWindowsService(options => {
    options.ServiceName = "GregalReceiverService";
});

// 1. Mapeo de las secciones de configuración
builder.Services.Configure<GeneralSettings>(builder.Configuration.GetSection("GeneralSettings"));
builder.Services.Configure<NetworkSettings>(builder.Configuration.GetSection("NetworkSettings"));
builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection("DatabaseSettings"));
builder.Services.Configure<TagProtocolSettings>(builder.Configuration.GetSection("TagProtocolSettings"));

var generalSettings = builder.Configuration.GetSection("GeneralSettings").Get<GeneralSettings>()
                      ?? new GeneralSettings();

// 2. Configuración de Ruta de Logs Absoluta
string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
string finalLogPath = Path.Combine(exeDirectory, "logs", generalSettings.LogPath);
string? logsFolder = Path.GetDirectoryName(finalLogPath);
if (!string.IsNullOrEmpty(logsFolder) && !Directory.Exists(logsFolder)) {
    Directory.CreateDirectory(logsFolder);
}

// 3. Inicialización de Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: finalLogPath,
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Services.AddSerilog();

// 4. Registro de Infraestructura de Datos y Motores de Parseo
builder.Services.AddSingleton<JsonGregalParser>();
builder.Services.AddSingleton<CsvGregalParser>();
builder.Services.AddSingleton<GregalParserEngine>();
builder.Services.AddSingleton<HistoricoRepository>();
builder.Services.AddSingleton<PartidaAccumulator>();

// ====================================================================
// Qué receptores arrancan se decide en appsettings.json, no aquí:
// NetworkSettings.Enabled (protocolo JSON/CSV, TCP+RS232 juntos) y
// TagProtocolSettings.Enabled (protocolo de eventos por RS232). Por defecto
// solo el segundo está activo — es el receptor principal ahora mismo.
var networkSettings = builder.Configuration.GetSection("NetworkSettings").Get<NetworkSettings>() ?? new NetworkSettings();
var tagSettings = builder.Configuration.GetSection("TagProtocolSettings").Get<TagProtocolSettings>() ?? new TagProtocolSettings();

if (networkSettings.Enabled) {
    builder.Services.AddHostedService<TcpGregalReceiver>();
    builder.Services.AddHostedService<SerialGregalReceiver>();
}
if (tagSettings.Enabled) {
    builder.Services.AddHostedService<TagSerialGregalReceiver>();
}
// ====================================================================

var host = builder.Build();

MostrarBannerDeArranque(builder.Configuration);

host.Run();

// Solo tiene sentido si alguien está mirando una consola real; como servicio
// de Windows (sc.exe/services.msc) no hay consola que lo muestre, y
// Console.WriteLine ahí no sirve de nada.
static void MostrarBannerDeArranque(IConfiguration configuration) {
    if (WindowsServiceHelpers.IsWindowsService()) return;

    var network = configuration.GetSection("NetworkSettings").Get<NetworkSettings>() ?? new NetworkSettings();
    var tag = configuration.GetSection("TagProtocolSettings").Get<TagProtocolSettings>() ?? new TagProtocolSettings();
    string logsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
    string[] puertosDisponibles = SerialPort.GetPortNames();

    Console.WriteLine();
    Console.WriteLine(new string('=', 78));
    Console.WriteLine(" Gregal - Receptor de histórico de partidas de Bingo");
    Console.WriteLine(new string('-', 78));

    // Características del PC, para ver de un vistazo si esta máquina sirve
    // para ejecutar Gregal (puertos COM disponibles, recursos básicos).
    Console.WriteLine($" PC: {Environment.MachineName} | {Environment.OSVersion.VersionString} | " +
                       $"{(Environment.Is64BitOperatingSystem ? "64" : "32")} bits");
    Console.WriteLine($" CPU: {Environment.ProcessorCount} núcleos | RAM: {InfoSistema.DescribirRam()} | " +
                       $"Disco libre: {InfoSistema.DescribirDiscoLibre(AppDomain.CurrentDomain.BaseDirectory)}");
    Console.WriteLine($" Puertos COM detectados: {(puertosDisponibles.Length > 0 ? string.Join(", ", puertosDisponibles) : "ninguno")}");
    Console.WriteLine(new string('=', 78));

    if (!network.Enabled && !tag.Enabled) {
        Console.WriteLine(" ¡ATENCIÓN! Ningún receptor activo (NetworkSettings.Enabled y");
        Console.WriteLine(" TagProtocolSettings.Enabled están ambos a false en appsettings.json).");
        Console.WriteLine(new string('=', 78));
        Console.WriteLine();
        return;
    }

    if (tag.Enabled) {
        bool salaConfigurada = tag.DefaultCodSala > 0 && tag.DefaultCodConc > 0;
        bool puertoDetectado = puertosDisponibles.Contains(tag.ComPort, StringComparer.OrdinalIgnoreCase);
        Console.WriteLine($" [Tag] ComPort={tag.ComPort}{(puertoDetectado ? "" : " <- NO detectado en este PC")} | " +
                           $"CodSala/CodConc={tag.DefaultCodSala}/{tag.DefaultCodConc}" +
                           (salaConfigurada ? "" : " <- sin configurar, se rechazarán partidas"));
    }
    if (network.Enabled) {
        bool puertoDetectado = puertosDisponibles.Contains(network.ComPort, StringComparer.OrdinalIgnoreCase);
        Console.WriteLine($" [JSON/CSV] TCP={network.TcpAddress}:{network.TcpPort} | " +
                           $"Serie={network.ComPort}{(puertoDetectado ? "" : " <- NO detectado en este PC")}");
    }

    Console.WriteLine($" Logs: {logsFolder}");
    Console.WriteLine(new string('=', 78));
    Console.WriteLine();
}