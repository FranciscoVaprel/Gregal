using Gregal;
using Gregal.Config;
using Gregal.Data;
using Gregal.Networking;
using Gregal.Parsing;
using Serilog;

// Lee appsettings.json, variables de entorno, etc.
var builder = Host.CreateApplicationBuilder(args);

// 1. Mapeo de las tres secciones de configuración
builder.Services.Configure<GeneralSettings>(builder.Configuration.GetSection("GeneralSettings"));
builder.Services.Configure<NetworkSettings>(builder.Configuration.GetSection("NetworkSettings"));
builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection("DatabaseSettings"));

// 2. Extracción manual para configurar el Logger al inicio
var generalSettings = builder.Configuration.GetSection("GeneralSettings").Get<GeneralSettings>()
                      ?? new GeneralSettings();

// 3. Configuración de Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(generalSettings.LogPath,
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

// 4. Registro de servicios (Sin usar builder.Host)
builder.Logging.ClearProviders();
builder.Services.AddSerilog(); // Esto sustituye a UseSerilog() en este tipo de Builder

builder.Services.AddSingleton<JsonGregalParser>();
builder.Services.AddSingleton<CsvGregalParser>();
builder.Services.AddSingleton<GregalParserEngine>();
builder.Services.AddSingleton<HistoricoRepository>();
builder.Services.AddHostedService<Worker>();

var mode = builder.Configuration["NetworkSettings:Mode"];

if (mode == "TCP") {
    // Si la configuración dice TCP, el Worker recibirá la versión de red
    builder.Services.AddSingleton<IGregalReceiver, TcpGregalReceiver>();
}
else if (mode == "RS232") {
    // Si dice RS232, el Worker recibirá la versión de puerto serie
    builder.Services.AddSingleton<IGregalReceiver, SerialGregalReceiver>();
}


var host = builder.Build();
host.Run();