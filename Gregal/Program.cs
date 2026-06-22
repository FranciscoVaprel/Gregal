using Gregal;
using Gregal.Config;
using Gregal.Data;
using Gregal.Networking;
using Gregal.Parsing;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
//sc.exe create "GregalReceiverService" binPath= "C:\Deploy\Gregal\Gregal.exe" start= auto displayname= "Gregal - Receptor de Bingo"
// 🚀 Indicar a .NET que gestione el ciclo de vida como Servicio de Windows
builder.Services.AddWindowsService(options => {
    options.ServiceName = "GregalReceiverService";
});

// 1. Mapeo de las tres secciones de configuración
builder.Services.Configure<GeneralSettings>(builder.Configuration.GetSection("GeneralSettings"));
builder.Services.Configure<NetworkSettings>(builder.Configuration.GetSection("NetworkSettings"));
builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection("DatabaseSettings"));

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

// ====================================================================
// 🚀 ¡NUEVO! ARRANQUE CONCURRENTE HÍBRIDO (TCP Y RS232 INDISTINTAMENTE)
// ====================================================================
// Eliminamos el antiguo condicional 'Worker' y los 'if/else' del modo.
// Registramos de forma nativa ambos receptores para que escuchen en paralelo.
builder.Services.AddHostedService<TcpGregalReceiver>();
builder.Services.AddHostedService<SerialGregalReceiver>();
// ====================================================================

var host = builder.Build();
host.Run();