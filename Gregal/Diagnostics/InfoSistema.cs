using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Gregal.Diagnostics;

// Datos del PC donde se ejecuta Gregal, para el banner de arranque: permite
// ver de un vistazo si la máquina tiene lo necesario (puertos COM, recursos
// básicos) antes de desplegar en una sala.
internal static class InfoSistema {
    [SupportedOSPlatform("windows")]
    public static string DescribirRam() {
        var estado = new MemoriaEstado { dwLength = (uint)Marshal.SizeOf<MemoriaEstado>() };
        if (!GlobalMemoryStatusEx(ref estado)) return "desconocida";
        double totalGb = estado.ullTotalPhys / 1024.0 / 1024.0 / 1024.0;
        double libreGb = estado.ullAvailPhys / 1024.0 / 1024.0 / 1024.0;
        return $"{totalGb:0.#} GB total, {libreGb:0.#} GB libre";
    }

    public static string DescribirDiscoLibre(string ruta) {
        try {
            var unidad = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(ruta)) ?? ruta);
            double libreGb = unidad.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
            return $"{libreGb:0.#} GB en {unidad.Name}";
        } catch {
            return "desconocido";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoriaEstado {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoriaEstado lpBuffer);
}
