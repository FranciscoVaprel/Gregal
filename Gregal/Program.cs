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

// Ayuda y versión: antes de leer la configuración, salen enseguida.
if (Ayuda.PideAyuda(args)) {
    Console.WriteLine(Ayuda.Texto);
    return 0;
}
if (Ayuda.PideVersion(args)) {
    Console.WriteLine($"Gregal {Ayuda.Version}");
    return 0;
}

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

// Órdenes de diagnóstico que no arrancan receptores ni escriben en el log:
//   --comprobar-bd   comprueba la base de datos y sale (lo usa Instalar-Gregal.ps1).
//   --escuchar       enseña lo que llega por el puerto COM, sin guardar nada (Probar-COM.cmd).
// Más abajo, ya con el log: --reimportar-pendientes.
if (args.Contains("--comprobar-bd", StringComparer.OrdinalIgnoreCase)) {
    Environment.Exit(ComprobacionBd.Ejecutar(builder.Configuration));
}
if (args.Contains("--escuchar", StringComparer.OrdinalIgnoreCase)) {
    Environment.Exit(EscuchaPuerto.Ejecutar(builder.Configuration, args));
}

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
        // Tope de disco: ficheros de 50 MB como mucho (si un día llega a más,
        // sigue en gregal<fecha>_001.log...) y solo los 60 últimos, unos 3 GB.
        fileSizeLimitBytes: 50 * 1024 * 1024,
        rollOnFileSizeLimit: true,
        retainedFileCountLimit: 60,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Services.AddSerilog();

// Red de seguridad: sin esto, una excepción que se escape de todos los
// try/catch mata el proceso sin dejar rastro en el log (solo en el Visor de
// eventos de Windows, que en una sala nadie mira). Se anota y se vacía el log
// antes de que el proceso termine.
AppDomain.CurrentDomain.UnhandledException += (_, e) => {
    Log.Fatal(e.ExceptionObject as Exception,
        "Excepción no controlada: Gregal se va a cerrar (IsTerminating={terminando}).", e.IsTerminating);
    Log.CloseAndFlush();
};
// Una Task que falla sin que nadie espere su resultado no cierra el proceso,
// pero el error se perdería sin más.
TaskScheduler.UnobservedTaskException += (_, e) => {
    Log.Error(e.Exception, "Excepción no observada en una tarea en segundo plano.");
    e.SetObserved();
};

string version = typeof(Program).Assembly
    .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
    .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
    .FirstOrDefault()?.InformationalVersion ?? "?";

// 4. Registro de Infraestructura de Datos y Motores de Parseo
builder.Services.AddSingleton<JsonGregalParser>();
builder.Services.AddSingleton<CsvGregalParser>();
builder.Services.AddSingleton<GregalParserEngine>();
builder.Services.AddSingleton<HistoricoRepository>();
// PartidaAccumulator (librería Gregal.Protocolo) solo conoce ReglasProtocoloTag,
// la parte de TagProtocolSettings que interpreta los eventos.
builder.Services.AddSingleton<Microsoft.Extensions.Options.IOptionsMonitor<ReglasProtocoloTag>>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<TagProtocolSettings>>());
builder.Services.AddSingleton<PartidaAccumulator>();

// ====================================================================
// Qué receptores arrancan se decide en appsettings.json, no aquí:
// NetworkSettings.Enabled (protocolo JSON/CSV, TCP+RS232 juntos) y
// TagProtocolSettings.Enabled (protocolo de eventos por RS232). Por defecto
// solo el segundo está activo — es el receptor principal ahora mismo.
var networkSettings = builder.Configuration.GetSection("NetworkSettings").Get<NetworkSettings>() ?? new NetworkSettings();
var tagSettings = builder.Configuration.GetSection("TagProtocolSettings").Get<TagProtocolSettings>() ?? new TagProtocolSettings();

// "Gregal.exe --reimportar-pendientes": vuelve a guardar en Firebird las
// partidas del fichero de pendientes, con el mismo HistoricoRepository (y el
// mismo log) que el servicio, y sale. Sin receptores: puede correr con Gregal
// en marcha (Reimportar-Pendientes.cmd).
if (args.Contains("--reimportar-pendientes", StringComparer.OrdinalIgnoreCase)) {
    Log.Information("Gregal {version}: reimportando las partidas pendientes de {fichero}.", version, tagSettings.FallbackFileFullPath);
    int codigo;
    try {
        using var hostReimportacion = builder.Build();
        codigo = await ReimportacionPendientes.EjecutarAsync(
            hostReimportacion.Services.GetRequiredService<HistoricoRepository>(), tagSettings.FallbackFileFullPath);
    }
    catch (Exception ex) {
        Log.Fatal(ex, "La reimportación de pendientes se ha detenido por un error.");
        codigo = 1;
    }
    Log.CloseAndFlush();
    return codigo;
}

Log.Information("Gregal {version} arrancando ({modo}).", version,
    WindowsServiceHelpers.IsWindowsService() ? "servicio de Windows" : "consola");

if (networkSettings.Enabled) {
    builder.Services.AddHostedService<TcpGregalReceiver>();
    builder.Services.AddHostedService<SerialGregalReceiver>();
}
if (tagSettings.Enabled) {
    builder.Services.AddHostedService<TagSerialGregalReceiver>();
}
// ====================================================================

try {
    var host = builder.Build();

    MostrarBannerDeArranque(builder.Configuration);

    host.Run();
}
catch (Exception ex) {
    Log.Fatal(ex, "Gregal se ha detenido por un error.");
    // Código distinto de 0 para que Windows lo trate como un fallo del
    // servicio y aplique sus acciones de recuperación (sc.exe failure).
    Environment.ExitCode = 1;
}
finally {
    Log.CloseAndFlush();
}
return Environment.ExitCode;

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
                           (salaConfigurada ? "" : " <- sin configurar, se rechazarán partidas") +
                           $" | Fabricante={tag.Fabricante}");
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