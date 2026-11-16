namespace Gregal.Utils;

/// <summary>
/// Un solo Gregal recibiendo partidas por PC. En las salas se arrancaba dos
/// veces (dos accesos directos en Inicio, 2026-11-10/11): el segundo no podía
/// abrir el puerto COM y se quedaba con su consola abierta sin recibir nada,
/// igual de "vivo" que el bueno a la vista. Ahora el segundo avisa y se cierra.
/// </summary>
public static class UnicaInstancia {
    // Global\: el mismo para todas las sesiones de Windows del PC (otro
    // usuario, o Gregal como servicio).
    public const string NombreMutex = @"Global\Gregal-Receptor";

    /// <summary>
    /// El mutex si este es el único Gregal (hay que mantenerlo vivo hasta
    /// salir), o null si ya hay otro en marcha. Windows lo libera solo al
    /// cerrarse el proceso, aunque sea por un corte de luz o matándolo.
    /// </summary>
    public static Mutex? Intentar(string nombre = NombreMutex) {
        try {
            var mutex = new Mutex(initiallyOwned: true, nombre, out bool creado);
            if (creado) return mutex;
            mutex.Dispose();
            return null;
        }
        catch (UnauthorizedAccessException) {
            // Existe, pero lo creó otro usuario (p.ej. el servicio): también
            // hay otro Gregal.
            return null;
        }
    }

    /// <summary>
    /// Mensaje para quien acaba de abrir Gregal a mano: la ventana se queda
    /// unos segundos para que se pueda leer, y se cierra sola.
    /// </summary>
    public static void AvisarEnConsola(int segundos = 20) {
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine(" GREGAL YA ESTÁ FUNCIONANDO en este PC, en otra ventana.");
        Console.WriteLine(" No hace falta abrirlo otra vez: esta ventana se cierra sola.");
        Console.WriteLine(" (Esa otra ventana es la que recibe las partidas: no la cierres.)");
        Console.WriteLine(new string('=', 78));
        if (Console.IsInputRedirected || !Environment.UserInteractive) return;
        var limite = DateTime.UtcNow.AddSeconds(segundos);
        while (DateTime.UtcNow < limite) {
            if (Console.KeyAvailable) return;
            Thread.Sleep(200);
        }
    }
}
