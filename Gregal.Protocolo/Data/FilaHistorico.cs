using System.Globalization;
using System.Text.RegularExpressions;
using Gregal.Models;

namespace Gregal.Data;

// Cómo se escribe un HistoricoDTO en una fila de HISTORICO (o de una tabla con
// la misma estructura): qué columnas, con qué valor por defecto si el dato no
// llega, y el UPDATE OR INSERT. Sin Firebird ni logging, para que lo usen igual
// HistoricoRepository (Gregal) y BingoHistoricoEditor al importar un log.
public static class FilaHistorico {
    public const int DecimalesPorDefecto = 2;

    private const string NotaBolasRecortadas = "BOLAS RECORTADAS";
    private const int MaxReclamacion = 64; // RECLAMACION es CHAR(64)

    // Columnas que se escriben, en orden. LETRA1_VIR/LETRA2_VIR (texto) no
    // están: el protocolo no las trae y quedan NULL. Cualquier columna que se
    // añada aquí tiene que existir en el HISTORICO de todas las salas.
    public static readonly IReadOnlyList<string> Columnas = new[] {
        "COD_SALA", "COD_CONC", "FECHA_SESION", "PARTIDA_DIA", "FECHA", "PARTIDA_ANUAL", "NUM_DECIMALES", "TIPO",
        "HORA_CIERRE", "HORA_VENTA", "HORA_SORTEO", "HORA_FIN", "HORA_CIERRE_H", "PRECIO", "VENTA", "CAJA", "TASA",
        "BENEFICIO", "CAJA_I", "CAJA_A_I", "ACUMULADOS", "LETRA1", "SERIE_1", "DEL_1", "AL_1", "LETRA2", "SERIE_2",
        "DEL_2", "AL_2", "VENDIDOS", "LARGO_SERIE", "EXTRACCIONES", "GUIABOLAS",
        "SERIE_1_VIR", "DEL_1_VIR", "AL_1_VIR", "SERIE_2_VIR", "DEL_2_VIR", "AL_2_VIR", "VENDIDOS_VIR",
        "RECLAMACION", "BOLAS", "BOLA_LINEA", "NUM_LINEAS", "HORA_LINEA", "CART_LINEA", "BOLA_BINGO",
        "NUM_BINGOS", "HORA_BINGO", "CART_BINGO", "FECHA_SESION_MES",
    }.Concat(Enumerable.Range(1, 99).Select(i => $"PREMIO{i}")).ToArray();

    // UPDATE OR INSERT con un parámetro @COLUMNA por cada una de Columnas.
    public static string SqlUpdateOrInsert(string tabla) {
        string nombre = ValidarNombreTabla(tabla);
        return $@"UPDATE OR INSERT INTO ""{nombre}"" ({string.Join(", ", Columnas)})
                  VALUES ({string.Join(", ", Columnas.Select(c => "@" + c))})
                  MATCHING (COD_SALA, COD_CONC, FECHA_SESION, PARTIDA_DIA)";
    }

    // Valor de cada columna (mismo orden que Columnas; DBNull para NULL).
    //
    // Las columnas numéricas que no llegan se guardan a 0 en vez de NULL
    // (decidido con el usuario 2026-10-30); las de texto, fecha y hora siguen
    // a NULL. "ahora": la hora a usar como HORA_FIN si el DTO no la trae.
    // Si la lista de bolas no cabe en maxLongitudBolas, se recorta (ver
    // AjustarBolasALaColumna) y se devuelve cuántas se han descartado; el DTO
    // no se toca, para que un guardado fallido conserve la lista completa.
    public static (IReadOnlyList<object> Valores, int BolasDescartadas) Valores(HistoricoDTO dto, DateTime ahora, int maxLongitudBolas) {
        var (bolas, reclamacion, descartadas) = AjustarBolasALaColumna(dto.Bolas, dto.Reclamacion, maxLongitudBolas);
        object N(object? v) => v ?? DBNull.Value;

        var valores = new List<object> {
            dto.CodSala, dto.CodConc, dto.FechaSesion, dto.PartidaDia,
            N(dto.Fecha), dto.PartidaAnual ?? 0,
            // Sin *K#, PartidaAccumulator ya ha dividido los importes entre
            // 100: guardar 0 diría que no llevan decimales.
            dto.NumDecimales ?? DecimalesPorDefecto,
            dto.Tipo ?? 0,
            N(dto.HoraCierre), N(dto.HoraVenta), N(dto.HoraSorteo), HoraFinAlGuardar(dto.HoraFin, ahora),
            EnteroOCero(dto.HoraCierreH),
            dto.Precio, dto.Venta, dto.Caja, dto.Tasa, dto.Beneficio,
            dto.CajaI ?? 0m, dto.CajaAI ?? 0m, dto.Acumulados ?? 0m,
            N(dto.Letra1), dto.Serie1 ?? 0, dto.Del1 ?? 0, dto.Al1 ?? 0,
            N(dto.Letra2), dto.Serie2 ?? 0, dto.Del2 ?? 0, dto.Al2 ?? 0,
            dto.Vendidos, dto.LargoSerie ?? 0, dto.Extracciones ?? 0, N(dto.GuiaBolas),
            // Columnas *_VIR: el protocolo no las trae, pero la tabla las tiene
            // (confirmado también en aamslog.FDB, 2026-10-30).
            dto.Serie1Vir ?? 0, dto.Del1Vir ?? 0, dto.Al1Vir ?? 0,
            dto.Serie2Vir ?? 0, dto.Del2Vir ?? 0, dto.Al2Vir ?? 0, dto.VendidosVir ?? 0,
            N(reclamacion), N(bolas),
            dto.BolaLinea ?? 0, dto.NumLineas ?? 0, N(dto.HoraLinea), N(dto.CartLinea),
            dto.BolaBingo ?? 0, dto.NumBingos ?? 0, N(dto.HoraBingo), N(dto.CartBingo),
            EnteroOCero(dto.FechaSesionMes),
        };
        for (int i = 0; i < 99; i++) {
            valores.Add(dto.Premios != null && dto.Premios.Length > i ? dto.Premios[i] ?? 0m : 0m);
        }
        return (valores, descartadas);
    }

    // HORA_FIN (TIME): la que traiga el DTO (PartidaAccumulator pone la del
    // cierre de la partida; el protocolo JSON/CSV puede traerla) o, si no,
    // la hora del guardado.
    public static string HoraFinAlGuardar(string? horaFinRecibida, DateTime ahora) =>
        string.IsNullOrWhiteSpace(horaFinRecibida)
            ? ahora.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
            : horaFinRecibida;

    // HORA_CIERRE_H y FECHA_SESION_MES son INTEGER en la tabla pero string en
    // el DTO (así los trae el protocolo JSON): vacío -> 0; si no, tal cual.
    public static object EnteroOCero(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? 0 : valor;

    // Si "bolas" (lista separada por comas) no cabe en maxLongitud, se queda
    // con las primeras bolas enteras que quepan y antepone NotaBolasRecortadas
    // a la reclamación (delante, para que no la corte el límite de 64).
    // Devuelve también cuántas bolas se han descartado (0 si cabía).
    public static (string? Bolas, string? Reclamacion, int Descartadas) AjustarBolasALaColumna(
            string? bolas, string? reclamacion, int maxLongitud) {
        if (bolas == null || maxLongitud <= 0 || bolas.Length <= maxLongitud) return (bolas, reclamacion, 0);

        int corte = bolas.LastIndexOf(',', maxLongitud);
        string recortadas = corte > 0 ? bolas[..corte] : "";
        int descartadas = bolas.Split(',').Length - (recortadas.Length == 0 ? 0 : recortadas.Split(',').Length);

        string nota = string.IsNullOrWhiteSpace(reclamacion) ? NotaBolasRecortadas : NotaBolasRecortadas + " | " + reclamacion.Trim();
        if (nota.Length > MaxReclamacion) nota = nota[..MaxReclamacion];

        return (recortadas.Length == 0 ? null : recortadas, nota, descartadas);
    }

    public static string ValidarNombreTabla(string? tableName) {
        if (string.IsNullOrWhiteSpace(tableName) ||
            tableName.Length > 63 ||
            !Regex.IsMatch(tableName, @"\A[A-Za-z][A-Za-z0-9_$]*\z", RegexOptions.CultureInvariant)) {
            throw new InvalidOperationException(
                "El nombre de la tabla debe ser un identificador Firebird de 1 a 63 caracteres, " +
                "empezar por una letra y contener solo letras, números, '_' o '$'.");
        }

        return tableName;
    }
}
