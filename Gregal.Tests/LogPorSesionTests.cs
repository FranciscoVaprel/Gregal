using Gregal.Utils;
using Serilog.Events;
using Serilog.Parsing;

namespace Gregal.Tests;

public class LogPorSesionTests : IDisposable {
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "gregal-logsesion-" + Guid.NewGuid().ToString("N"));
    private const string Plantilla = "{Timestamp:yyyy-MM-dd HH:mm:ss} {Message:lj}{NewLine}";

    public void Dispose() {
        try { Directory.Delete(_carpeta, recursive: true); } catch (IOException) { }
    }

    private static LogEvent Evento(DateTime momento, string texto) =>
        new(new DateTimeOffset(momento), LogEventLevel.Information, null,
            new MessageTemplateParser().Parse(texto), Array.Empty<LogEventProperty>());

    private LogPorSesion Nuevo(DateTime? ahora = null) =>
        new(Path.Combine(_carpeta, "gregal.log"), new TimeSpan(7, 0, 0), Plantilla, ahora: ahora ?? new DateTime(2026, 11, 17, 12, 0, 0));

    [Fact]
    public void LaMadrugadaVaAlFicheroDeLaSesionAnterior() {
        using (var sink = Nuevo()) {
            sink.Emit(Evento(new DateTime(2026, 11, 17, 18, 0, 0), "partida 1"));
            sink.Emit(Evento(new DateTime(2026, 11, 18, 2, 30, 0), "partida 70"));
            sink.Emit(Evento(new DateTime(2026, 11, 18, 6, 59, 59), "cierre"));
            sink.Emit(Evento(new DateTime(2026, 11, 18, 7, 0, 0), "nueva sesion"));
        }

        string del17 = File.ReadAllText(Path.Combine(_carpeta, "gregal-sesion20261117.log"));
        string del18 = File.ReadAllText(Path.Combine(_carpeta, "gregal-sesion20261118.log"));
        Assert.Contains("partida 1", del17);
        Assert.Contains("partida 70", del17);
        Assert.Contains("cierre", del17);
        Assert.DoesNotContain("nueva sesion", del17);
        Assert.Contains("nueva sesion", del18);
        Assert.Equal(2, Directory.GetFiles(_carpeta).Length);
    }

    [Fact]
    public void UnEventoRezagadoNoReabreLaSesionAnterior() {
        using (var sink = Nuevo()) {
            sink.Emit(Evento(new DateTime(2026, 11, 18, 7, 0, 1), "nueva"));
            sink.Emit(Evento(new DateTime(2026, 11, 18, 6, 59, 59), "rezagado"));
        }
        Assert.Single(Directory.GetFiles(_carpeta));
        Assert.Contains("rezagado", File.ReadAllText(Path.Combine(_carpeta, "gregal-sesion20261118.log")));
    }

    [Fact]
    public void AlArrancarBorraLosLogsDeMasDeSesentaDias() {
        Directory.CreateDirectory(_carpeta);
        var ahora = new DateTime(2026, 11, 17, 12, 0, 0);
        string viejoDia = Path.Combine(_carpeta, "gregal20260901.log");
        string viejoSesion = Path.Combine(_carpeta, "gregal-sesion20260902.log");
        string reciente = Path.Combine(_carpeta, "gregal20261110.log");
        string otro = Path.Combine(_carpeta, "historico_pendientes_tag.jsonl");
        foreach (var f in new[] { viejoDia, viejoSesion, reciente, otro }) File.WriteAllText(f, "x");
        File.SetLastWriteTime(viejoDia, ahora.AddDays(-61));
        File.SetLastWriteTime(viejoSesion, ahora.AddDays(-61));
        File.SetLastWriteTime(otro, ahora.AddDays(-200));
        File.SetLastWriteTime(reciente, ahora.AddDays(-7));

        using (Nuevo(ahora)) { }

        Assert.False(File.Exists(viejoDia));
        Assert.False(File.Exists(viejoSesion));
        Assert.True(File.Exists(reciente));
        Assert.True(File.Exists(otro));
    }
}
