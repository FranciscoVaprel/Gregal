using System.IO.Ports;
using Gregal.Config;
using Gregal.Networking;
using Gregal.Parsing;

namespace Gregal.Diagnostics;

// "Gregal.exe --escuchar [COMx] [segundos]": abre el puerto de la máquina con
// los mismos parámetros que el receptor y enseña en pantalla lo que llega, tal
// cual, sin interpretarlo ni guardar nada. Para el primer día en una sala
// (¿está bien el cable? ¿es este el puerto?) sin tener que arrancar Gregal
// entero ni buscar en el log. Una máquina MCB manda *f# dos veces por segundo
// mientras espera, así que si el cable está bien se ve algo enseguida.
// Termina con Esc (o al cabo de los segundos indicados). Código de salida 0
// si llegó algo, 2 si no llegó nada, 1 si no se pudo abrir el puerto.
public static class EscuchaPuerto {
    public static int Ejecutar(IConfiguration configuration, string[] args) {
        var config = configuration.GetSection("TagProtocolSettings").Get<TagProtocolSettings>() ?? new TagProtocolSettings();

        var (puerto, segundos) = Interpretar(args, config.ComPort);

        string[] detectados = SerialPort.GetPortNames();
        Console.WriteLine($"Puertos COM de este PC: {(detectados.Length > 0 ? string.Join(", ", detectados) : "ninguno")}");
        if (!detectados.Contains(puerto, StringComparer.OrdinalIgnoreCase)) {
            Console.WriteLine($"AVISO: {puerto} no aparece entre los puertos del PC. ¿Está conectado el adaptador?");
        }

        using SerialPort port = TagSerialGregalReceiver.CrearPuerto(config, puerto);
        try {
            port.Open();
        } catch (UnauthorizedAccessException) {
            Console.WriteLine($"ERROR: {puerto} está ocupado por otro programa. Si Gregal está abierto (ventana o servicio), ciérralo antes de probar.");
            return 1;
        } catch (Exception ex) {
            Console.WriteLine($"ERROR: no se puede abrir {puerto}: {ex.Message}");
            return 1;
        }

        Console.WriteLine($"Escuchando en {puerto} a {port.BaudRate} baudios ({port.DataBits}{Letra(port.Parity)}{Bits(port.StopBits)}, control de flujo {port.Handshake}).");
        Console.WriteLine(segundos.HasValue
            ? $"Durante {segundos} segundos. Esc para terminar antes."
            : "Pulsa Esc para terminar.");
        Console.WriteLine("No se guarda nada: solo se muestra lo que llega.");
        Console.WriteLine(new string('-', 78));

        var fin = segundos.HasValue ? DateTime.Now.AddSeconds(segundos.Value) : DateTime.MaxValue;
        long bytes = 0;
        var eventos = new SortedDictionary<char, int>();
        string acumulado = "";
        var ultimoAviso = DateTime.Now;

        while (DateTime.Now < fin) {
            // Sin consola propia (entrada redirigida) no hay Esc: solo el límite de segundos.
            if (!Console.IsInputRedirected && Console.KeyAvailable && Console.ReadKey(intercept: true).Key == ConsoleKey.Escape) break;

            string llegado;
            try {
                llegado = port.ReadExisting();
            } catch (Exception ex) {
                Console.WriteLine($"ERROR leyendo del puerto: {ex.Message}");
                break;
            }

            if (llegado.Length == 0) {
                if (bytes == 0 && DateTime.Now - ultimoAviso >= TimeSpan.FromSeconds(5)) {
                    ultimoAviso = DateTime.Now;
                    Console.WriteLine("  (no llega nada todavía: comprueba el cable y que la máquina esté encendida)");
                }
                Thread.Sleep(100);
                continue;
            }

            bytes += llegado.Length;
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}  {Legible(llegado)}");

            // Cuenta de eventos *X...# por código, para el resumen.
            acumulado += llegado;
            while (acumulado.Contains(config.FrameDelimiter)) {
                int idx = acumulado.IndexOf(config.FrameDelimiter);
                foreach (string evento in TagEventParser.Trocear(acumulado[..idx]).Eventos) {
                    var evt = TagEventParser.Parse(evento);
                    if (evt != null) eventos[evt.Value.Code] = eventos.GetValueOrDefault(evt.Value.Code) + 1;
                }
                acumulado = acumulado[(idx + 1)..];
            }
            if (acumulado.Length > 100_000) acumulado = "";
        }

        try { port.Close(); } catch { /* bug conocido de System.IO.Ports al cerrar, ver TagSerialGregalReceiver.Stop */ }

        Console.WriteLine(new string('-', 78));
        if (bytes == 0) {
            Console.WriteLine($"No ha llegado nada por {puerto}.");
            Console.WriteLine("Comprueba el cable, que la máquina esté encendida y que el puerto sea el correcto" +
                              (detectados.Length > 1 ? $" (prueba los otros: {string.Join(", ", detectados.Where(p => !p.Equals(puerto, StringComparison.OrdinalIgnoreCase)))})." : "."));
            return 2;
        }
        Console.WriteLine($"Recibidos {bytes} bytes por {puerto}.");
        if (eventos.Count > 0) {
            Console.WriteLine("Eventos por código: " + string.Join(", ", eventos.Select(e => $"{e.Key} x{e.Value}")));
            Console.WriteLine(eventos.ContainsKey('f') || eventos.ContainsKey('~')
                ? "Se reciben los eventos de espera de la máquina: el cable y el puerto están bien."
                : "Llegan eventos del protocolo: el cable y el puerto están bien.");
        } else {
            Console.WriteLine("Llegan datos pero no tienen la forma *CODIGO<valor>#: puede que la velocidad (BaudRate) no sea la de la máquina, o que este no sea su puerto.");
        }
        return 0;
    }

    // Argumentos tras --escuchar, en cualquier orden: un puerto (COM3) y/o un
    // número de segundos (sin número, hasta pulsar Esc). Sin puerto, el de
    // appsettings.json. Un número solo siempre son segundos: el puerto se
    // escribe con su "COM" para que "3" no sea ambiguo.
    public static (string Puerto, int? Segundos) Interpretar(string[] args, string puertoConfigurado) {
        string? puerto = null;
        int? segundos = null;
        foreach (string arg in args.SkipWhile(a => !a.Equals("--escuchar", StringComparison.OrdinalIgnoreCase)).Skip(1)) {
            if (arg.StartsWith("--")) break;
            if (int.TryParse(arg, out int n)) {
                if (n > 0) segundos = Math.Min(n, 3600);
            } else {
                puerto = PuertoSerie.Normalizar(arg);
            }
        }
        return (puerto ?? puertoConfigurado, segundos);
    }

    // Como TagEventParser.Visible, pero también los bytes >= 0x80 como \xNN:
    // el evento 'e' es binario y en la ventana de cmd saldría como símbolos
    // sueltos que no dicen nada.
    public static string Legible(string texto) =>
        string.Concat(texto.Select(c => c < ' ' || c >= '\u007F' ? $"\\x{(int)c:X2}" : c.ToString()));

    private static string Letra(Parity p) => p switch {
        Parity.None => "N", Parity.Even => "E", Parity.Odd => "O", Parity.Mark => "M", Parity.Space => "S", _ => "?"
    };

    private static string Bits(StopBits s) => s switch {
        StopBits.One => "1", StopBits.Two => "2", StopBits.OnePointFive => "1.5", _ => "0"
    };
}
