using Gregal.Config;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Gregal.Utils;

/// <summary>
/// Sink de Serilog que escribe un fichero por sesión de bingo en vez de uno por
/// día de calendario: <c>gregal-sesion20261117.log</c> tiene todo lo de la
/// sesión del 17, de las 07:00 del 17 a las 06:59 del 18 (la misma regla que
/// FECHA_SESION, <see cref="SesionBingo.FechaDe"/>). Así la sesión que pasa de
/// medianoche no queda partida en dos logs.
///
/// Por dentro, un sink de fichero normal por sesión (tope de tamaño incluido:
/// pasado el tope sigue en <c>_001</c>, <c>_002</c>…), que se cambia por el de
/// la sesión siguiente cuando llega el primer evento a partir de la hora de
/// cambio. Los logs de más de <c>diasConservados</c> días se borran al arrancar
/// (los PC de las salas se apagan cada noche, así que eso es a diario).
/// </summary>
public sealed class LogPorSesion : ILogEventSink, IDisposable {
    private readonly string _carpeta;
    private readonly string _prefijo;
    private readonly string _extension;
    private readonly string _plantilla;
    private readonly TimeSpan _horaCambio;
    private readonly long _tope;
    private readonly object _lock = new();
    private DateTime? _sesionActual;
    private Logger? _fichero;
    private bool _cerrado;

    /// <param name="rutaBase">Ruta "plantilla", p. ej. <c>C:\Gregal\logs\gregal.log</c>: de ella salen la carpeta, el prefijo y la extensión.</param>
    public LogPorSesion(string rutaBase, TimeSpan horaCambio, string plantilla,
                        long topeBytes = 50 * 1024 * 1024, int diasConservados = 60,
                        DateTime? ahora = null) {
        string completa = Path.GetFullPath(rutaBase);
        _carpeta = Path.GetDirectoryName(completa) ?? ".";
        _prefijo = Path.GetFileNameWithoutExtension(completa);
        _extension = Path.GetExtension(completa) is { Length: > 0 } ext ? ext : ".log";
        _plantilla = plantilla;
        _horaCambio = horaCambio;
        _tope = topeBytes;
        Directory.CreateDirectory(_carpeta);
        BorrarAntiguos(diasConservados, ahora ?? DateTime.Now);
    }

    /// <summary>Fichero de una sesión (sin el sufijo _001… de los que pasan del tope).</summary>
    public string RutaDe(DateTime sesion) =>
        Path.Combine(_carpeta, $"{_prefijo}-sesion{sesion:yyyyMMdd}{_extension}");

    public void Emit(LogEvent logEvent) {
        DateTime sesion = SesionBingo.FechaDe(logEvent.Timestamp.LocalDateTime, _horaCambio);
        lock (_lock) {
            if (_cerrado) return;
            // Solo hacia delante: un evento rezagado de justo antes de la hora de
            // cambio no reabre el fichero de la sesión anterior.
            if (_fichero == null || sesion > _sesionActual) {
                _fichero?.Dispose();
                _fichero = new LoggerConfiguration()
                    .MinimumLevel.Verbose() // el nivel ya lo filtra el logger principal
                    .WriteTo.File(RutaDe(sesion),
                        fileSizeLimitBytes: _tope,
                        rollOnFileSizeLimit: true,
                        retainedFileCountLimit: null, // lo hace BorrarAntiguos
                        outputTemplate: _plantilla)
                    .CreateLogger();
                _sesionActual = sesion;
            }
            _fichero.Write(logEvent);
        }
    }

    /// <summary>
    /// Borra los logs de Gregal (<c>prefijo*.log</c>: los de sesión y los
    /// antiguos por día de calendario) que no se han tocado en
    /// <paramref name="dias"/> días. Un fichero que no se puede borrar se deja.
    /// </summary>
    private void BorrarAntiguos(int dias, DateTime ahora) {
        if (dias <= 0) return;
        DateTime limite = ahora.AddDays(-dias);
        try {
            foreach (var f in Directory.EnumerateFiles(_carpeta, _prefijo + "*" + _extension)) {
                try {
                    if (File.GetLastWriteTime(f) < limite) File.Delete(f);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Dispose() {
        lock (_lock) {
            _cerrado = true;
            _fichero?.Dispose();
            _fichero = null;
        }
    }
}
