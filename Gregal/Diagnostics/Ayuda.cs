using System.Reflection;

namespace Gregal.Diagnostics;

// "Gregal.exe -h | --help | /?" y "Gregal.exe --version": se miran antes que
// nada (ni configuración ni log) y salen enseguida.
public static class Ayuda {
    private static readonly string[] OpcionesAyuda = { "-h", "--help", "/?", "-?", "--ayuda" };

    public static bool PideAyuda(string[] args) =>
        args.Any(a => OpcionesAyuda.Contains(a, StringComparer.OrdinalIgnoreCase));

    public static bool PideVersion(string[] args) =>
        args.Any(a => a.Equals("--version", StringComparison.OrdinalIgnoreCase));

    // Versión completa, con el commit: "1.0.19+91f28a8...".
    public static string Version =>
        typeof(Ayuda).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";

    public static string Texto => $"""
        Gregal {Version.Split('+')[0]} - Receptor de partidas de bingo

          Gregal.exe                          Arranca Gregal (recibe y guarda partidas).
          Gregal.exe --comprobar-bd           Comprueba la conexión con Firebird y la tabla.
          Gregal.exe --escuchar [COMx] [seg]  Enseña lo que llega por el puerto, sin guardar
                                              (Probar-COM.cmd). Esc para terminar.
          Gregal.exe --reimportar-pendientes  Guarda en Firebird las partidas que quedaron en
                                              logs\historico_pendientes_tag.jsonl
                                              (Reimportar-Pendientes.cmd).
          Gregal.exe --version                Muestra la versión.
          Gregal.exe -h | --help | /?         Muestra esta ayuda.

          Cualquier valor de appsettings.json se cambia solo para esa ejecución con
          --Seccion:Clave=valor, por ejemplo --TagProtocolSettings:ComPort=COM3

          Se ejecuta desde la carpeta de Gregal: lee appsettings.json del directorio actual.
        """;
}
