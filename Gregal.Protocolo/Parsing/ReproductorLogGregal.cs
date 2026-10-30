using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Gregal.Config;
using Gregal.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gregal.Parsing;

// Reconstruye las partidas de un log de Gregal (logs\gregal<AAAAMMDD>.log)
// volviendo a pasar por PartidaAccumulator, uno a uno, los eventos que Gregal
// dejó anotados como "Fragmento tag recibido: '<evento>'" (solo están si
// TagProtocolSettings.LogRawFrames estaba activo). Lo usa BingoHistoricoEditor
// para importar un log a una tabla aparte.
//
// Reproduce lo que hizo el servicio, no lo que "debería" haber pasado:
// - El reloj del acumulador es la hora de cada línea, así que FECHA, las horas
//   de venta, línea, bingo y fin, y la fecha de sesión salen las del log.
// - Cada "Gregal ... arrancando" empieza con un acumulador nuevo, como el
//   proceso nuevo: una partida que el reinicio dejó a medias se pierde (se
//   avisa) y una sin T pendiente también.
// - Las partidas que el acumulador entrega pasan por HistoricoDtoValidator,
//   igual que en el servicio; si no lo superan (p. ej. sala 0) se descartan.
public static class ReproductorLogGregal {
    // "2026-10-29 17:13:30.523 [INF] mensaje" (outputTemplate de Program.cs).
    private static readonly Regex Linea = new(
        @"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}) \[(\w{3})\] (.*)$", RegexOptions.Compiled);

    // Las comillas son del texto del mensaje; el evento va tal cual dentro
    // (Serilog con :lj no lo escapa), con su '*' inicial y sin el '#'.
    private const string PrefijoFragmento = "Fragmento tag recibido: '";

    public static ResultadoReproduccion Reproducir(string rutaLog, ReglasProtocoloTag reglas) {
        // Serilog escribe el log en UTF-8; el payload binario del evento 'e' se
        // leyó del puerto como Latin1, así que leyéndolo como UTF-8 vuelven los
        // mismos caracteres que vio el acumulador. FileShare.ReadWrite: Gregal
        // puede tener abierto el log del día.
        using var fs = new FileStream(rutaLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var lector = new StreamReader(fs, new UTF8Encoding(false));
        return Reproducir(lector, reglas);
    }

    public static ResultadoReproduccion Reproducir(TextReader log, ReglasProtocoloTag reglas) {
        var reloj = new RelojManual();
        var avisos = new List<AvisoReproduccion>();
        var partidas = new List<PartidaReproducida>();
        int numLinea = 0, eventos = 0, arranques = 0;
        DateTime? primera = null, ultima = null;

        var logger = new LoggerDeAvisos(avisos, () => numLinea, () => reloj.Ahora);
        var opciones = new OpcionesFijas(reglas);
        PartidaAccumulator? acumulador = null;

        void Avisar(string texto) => avisos.Add(new AvisoReproduccion(numLinea, reloj.Ahora, texto));

        string? texto;
        while ((texto = log.ReadLine()) != null) {
            numLinea++;
            var m = Linea.Match(texto);
            if (!m.Success) continue;   // continuación de una excepción, línea en blanco...

            DateTime hora = DateTime.ParseExact(m.Groups[1].Value, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            string mensaje = m.Groups[3].Value;

            if (mensaje.StartsWith("Gregal ", StringComparison.Ordinal) && mensaje.Contains(" arrancando", StringComparison.Ordinal)) {
                if (acumulador?.HayPartidaEnCurso == true) {
                    Avisar("Gregal se reinició con una partida a medias: esa partida se perdió (no hay *F# que la cierre).");
                }
                arranques++;
                acumulador = null;   // proceso nuevo: estado nuevo
                continue;
            }

            if (!mensaje.StartsWith(PrefijoFragmento, StringComparison.Ordinal) || !mensaje.EndsWith('\'')) continue;
            string fragmento = mensaje[PrefijoFragmento.Length..^1];

            reloj.Ahora = hora;
            primera ??= hora;
            ultima = hora;
            eventos++;
            acumulador ??= new PartidaAccumulator(logger, opciones, reloj);

            var evt = TagEventParser.Parse(fragmento);
            if (evt == null) continue;

            var dto = acumulador.Feed(evt.Value);
            if (dto == null) continue;

            string? error = HistoricoDtoValidator.ValidateRequiredFields(dto);
            if (error != null) {
                Avisar($"Partida descartada (como en Gregal): {error}");
                continue;
            }
            partidas.Add(new PartidaReproducida(dto, numLinea, hora));
        }

        if (acumulador?.HayPartidaEnCurso == true) {
            Avisar("El log termina con una partida a medias: no se importa (no tiene *F#).");
        }

        return new ResultadoReproduccion(partidas, avisos, numLinea, eventos, arranques, primera, ultima);
    }

    // TimeProvider cuya hora es la de la línea del log que se está procesando.
    // Zona UTC para que GetLocalNow() devuelva esa hora tal cual, sin desplazar.
    private sealed class RelojManual : TimeProvider {
        public DateTime Ahora { get; set; }
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(Ahora, DateTimeKind.Unspecified), TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class OpcionesFijas(ReglasProtocoloTag valor) : IOptionsMonitor<ReglasProtocoloTag> {
        public ReglasProtocoloTag CurrentValue => valor;
        public ReglasProtocoloTag Get(string? name) => valor;
        public IDisposable? OnChange(Action<ReglasProtocoloTag, string?> listener) => null;
    }

    // Recoge los avisos (Warning o más) del acumulador con la línea y hora del
    // log en que se produjeron.
    private sealed class LoggerDeAvisos(List<AvisoReproduccion> destino, Func<int> linea, Func<DateTime> hora)
            : ILogger<PartidaAccumulator> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                                Func<TState, Exception?, string> formatter) {
            if (!IsEnabled(logLevel)) return;
            destino.Add(new AvisoReproduccion(linea(), hora(), formatter(state, exception)));
        }
    }
}

// Una partida reconstruida: el DTO tal como lo habría guardado Gregal, la
// línea del log en la que se cerró (o, si era una partida sin T, la de la T
// siguiente que le dio número) y la hora de esa línea.
public sealed record PartidaReproducida(HistoricoDTO Partida, int Linea, DateTime Hora);

public sealed record AvisoReproduccion(int Linea, DateTime Hora, string Texto);

public sealed record ResultadoReproduccion(
    IReadOnlyList<PartidaReproducida> Partidas,
    IReadOnlyList<AvisoReproduccion> Avisos,
    int LineasLeidas,
    int Eventos,
    int Arranques,
    DateTime? PrimerEvento,
    DateTime? UltimoEvento);
