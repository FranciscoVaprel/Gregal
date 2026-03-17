using Gregal;
using Gregal.Config;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// 1. Cargamos la configuración
builder.Services.Configure<GregalSettings>(
    builder.Configuration.GetSection("GregalSettings"));

// 2. Extraemos la ruta del log del JSON para configurar Serilog
var settings = builder.Configuration.GetSection("GregalSettings").Get<GregalSettings>() ?? new();

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(settings.LogPath, rollingInterval: RollingInterval.Day)
    .CreateLogger();

// 3. REGISTRO CORRECTO PARA HostApplicationBuilder
builder.Logging.ClearProviders();
builder.Services.AddSerilog(); 

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();