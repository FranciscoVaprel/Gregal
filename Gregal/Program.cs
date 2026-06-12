using Gregal;
using Gregal.Config;
using Gregal.Data;
using Gregal.Networking;
using Gregal.Parsing;
using Serilog;

//Una vez compiles el proyecto en modo Release, podrás instalar el .exe resultante en la terminal de administración de Windows con un único comando:
//sc.exe create GregalReceiver binPath= "C:\RutaDeTuPublicacion\Gregal.exe" start= auto
// Lee appsettings.json, variables de entorno, etc.
var builder = Host.CreateApplicationBuilder(args);

// 1. Mapeo de las tres secciones de configuración
builder.Services.Configure<GeneralSettings>(builder.Configuration.GetSection("GeneralSettings"));
builder.Services.Configure<NetworkSettings>(builder.Configuration.GetSection("NetworkSettings"));
builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection("DatabaseSettings"));

// 2. Extracción manual para configurar el Logger al inicio
var generalSettings = builder.Configuration.GetSection("GeneralSettings").Get<GeneralSettings>()
                      ?? new GeneralSettings();

// 3. Configuración de Serilog vinculada al appsettings.json
// --- CÓDIGO MODIFICADO EN PROGRAM.CS ---

// 1. Mapeo de las tres secciones de configuración (se queda igual)
builder.Services.Configure<GeneralSettings>(builder.Configuration.GetSection("GeneralSettings"));
builder.Services.Configure<NetworkSettings>(builder.Configuration.GetSection("NetworkSettings"));
builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection("DatabaseSettings"));

// ====================================================================
// ¡NUEVO! CALCULAR RUTA ABSOLUTA ABAJO DEL .EXE
// ====================================================================
// AppDomain.CurrentDomain.BaseDirectory nos da la ruta exacta de la carpeta del .exe
string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;

// Combinamos para crear la ruta completa hacia: carpetaDelExe/logs/gregal.log
string finalLogPath = Path.Combine(exeDirectory, "logs", generalSettings.LogPath);

// Aseguramos de manera proactiva que la carpeta /logs exista en el disco duro
string? logsFolder = Path.GetDirectoryName(finalLogPath);
if (!string.IsNullOrEmpty(logsFolder) && !Directory.Exists(logsFolder)) {
    Directory.CreateDirectory(logsFolder);
}
// ====================================================================

// 3. Configuración de Serilog vinculada al appsettings.json
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration) // Lee "Logging:LogLevel"
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: finalLogPath, // <-- Usamos nuestra nueva ruta absoluta calculada
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

// 4. Registro de servicios (El resto del archivo se queda exactamente igual...)
builder.Logging.ClearProviders();
builder.Services.AddSerilog();

// 4. Registro de servicios (Sin usar builder.Host)
builder.Logging.ClearProviders();
builder.Services.AddSerilog(); // Esto sustituye a UseSerilog() en este tipo de Builder

builder.Services.AddSingleton<JsonGregalParser>();
builder.Services.AddSingleton<CsvGregalParser>();
builder.Services.AddSingleton<GregalParserEngine>();
builder.Services.AddSingleton<HistoricoRepository>();
builder.Services.AddHostedService<Worker>();

var mode = builder.Configuration["NetworkSettings:Mode"];

if (mode == "TCP")
    builder.Services.AddSingleton<IGregalReceiver, TcpGregalReceiver>();
else if (mode == "RS232")
    builder.Services.AddSingleton<IGregalReceiver, SerialGregalReceiver>();
else
    throw new InvalidOperationException(
        $"Modo de red '{mode}' no reconocido. Valores válidos: TCP, RS232.");


var host = builder.Build();
host.Run();