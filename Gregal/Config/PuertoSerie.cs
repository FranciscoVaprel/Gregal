namespace Gregal.Config;

// Nombre de puerto serie tal como lo escribe alguien en appsettings.json o al
// instalar: "1", "com1" o " COM1 " valen como "COM1". Visto en una sala con
// ComPort "1": Windows no lo reconoce y el receptor no arrancaba.
public static class PuertoSerie {
    public static string Normalizar(string? puerto) {
        string p = (puerto ?? "").Trim();
        if (p.Length > 0 && p.All(char.IsDigit)) return "COM" + p;
        if (p.StartsWith("COM", StringComparison.OrdinalIgnoreCase)) return "COM" + p[3..].Trim();
        return p;   // otros nombres válidos, p. ej. los virtuales de com0com (CNCA0)
    }
}
