using Gregal;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.Configure<GregalSettings>(builder.Configuration.GetSection("GregalSettings"));

var host = builder.Build();
host.Run();
