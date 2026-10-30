using FirebirdSql.Data.FirebirdClient;
using Gregal.Models;
using Gregal.Config;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Gregal.Data;

public class HistoricoRepository {
    private readonly IOptionsMonitor<DatabaseSettings> _dbSettings;
    private readonly ILogger<HistoricoRepository> _logger;
    /*
     Como en tu HistoricoRepository estás usando métodos asíncronos (async/await), no podemos usar un lock normal (ya que el lock de C# no se lleva bien con await). En su lugar, usaremos un SemaphoreSlim, que es el "hermano moderno" del Monitor diseñado específicamente para tareas asíncronas.
    */
    private static readonly SemaphoreSlim _dbSemaphore = new SemaphoreSlim(1, 1);

    private readonly TimeProvider _reloj;

    // reloj: solo para tests (hora fija); en producción, la hora local del PC.
    public HistoricoRepository(IOptionsMonitor<DatabaseSettings> dbSettings, ILogger<HistoricoRepository> logger, TimeProvider? reloj = null) {
        _dbSettings = dbSettings;
        _logger = logger;
        _reloj = reloj ?? TimeProvider.System;
    }

    // HORA_FIN (TIME): la hora del sistema al guardar la partida, salvo que ya
    // venga informada (el protocolo JSON/CSV puede traerla).
    public static string HoraFinAlGuardar(string? horaFinRecibida, DateTime ahora) =>
        string.IsNullOrWhiteSpace(horaFinRecibida)
            ? ahora.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
            : horaFinRecibida;

    public async Task<bool> SaveHistoricoAsync(HistoricoDTO dto) {
        // Log de nivel Debug: Solo lo verás si en el JSON pones "Default": "Debug"
        _logger.LogDebug("Intentando guardar Partida {partida} de la Sala {sala}...", dto.PartidaDia, dto.CodSala);

        var dbSettings = _dbSettings.CurrentValue;
        string tableName = ValidateTableName(dbSettings.TableName);

        // No se toca el DTO: si el guardado falla, al fichero de pendientes
        // debe ir la lista completa.
        var (bolas, reclamacion, descartadas) = AjustarBolasALaColumna(dto.Bolas, dto.Reclamacion, dbSettings.MaxLongitudBolas);
        if (descartadas > 0) {
            _logger.LogWarning("La lista de bolas de la Sala {sala}, Partida {partida} ocupa {longitud} caracteres y la columna BOLAS admite {max}: " +
                               "se guardan las primeras bolas y se descartan las {descartadas} últimas. Lista completa: {bolasCompletas}",
                dto.CodSala, dto.PartidaDia, dto.Bolas!.Length, dbSettings.MaxLongitudBolas, descartadas, dto.Bolas);
        }

        await _dbSemaphore.WaitAsync();
        try {
            using (FbConnection connection = new FbConnection(dbSettings.ConnectionString)) {
                try {
                    await connection.OpenAsync();
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "X Error al conectar con Firebird para guardar Sala {sala}, Partida {partida}.",
                        dto.CodSala, dto.PartidaDia);
                    return false;
                }

                using (FbTransaction trans = connection.BeginTransaction()) {
                    try {
                        // Construimos la parte de los premios dinámicamente para no escribir 99 líneas a mano
                        string columnasPremios = string.Join(", ", Enumerable.Range(1, 99).Select(i => $"PREMIO{i}"));
                        string parametrosPremios = string.Join(", ", Enumerable.Range(1, 99).Select(i => $"@PR{i}"));
                        string sql = $@"UPDATE OR INSERT INTO ""{tableName}"" (
                    COD_SALA, COD_CONC, FECHA_SESION, PARTIDA_DIA, FECHA, PARTIDA_ANUAL, NUM_DECIMALES, TIPO, 
                    HORA_CIERRE, HORA_VENTA, HORA_SORTEO, HORA_FIN, HORA_CIERRE_H, PRECIO, VENTA, CAJA, TASA, 
                    BENEFICIO, CAJA_I, CAJA_A_I, ACUMULADOS, LETRA1, SERIE_1, DEL_1, AL_1, LETRA2, SERIE_2, 
                    DEL_2, AL_2, VENDIDOS, LARGO_SERIE, EXTRACCIONES, GUIABOLAS,
                    SERIE_1_VIR, DEL_1_VIR, AL_1_VIR, SERIE_2_VIR, DEL_2_VIR, AL_2_VIR, VENDIDOS_VIR,
                    RECLAMACION, BOLAS, BOLA_LINEA, NUM_LINEAS, HORA_LINEA, CART_LINEA, BOLA_BINGO, 
                    NUM_BINGOS, HORA_BINGO, CART_BINGO, FECHA_SESION_MES,
                    {columnasPremios}
                ) VALUES (
                    @COD_SALA, @COD_CONC, @FECHA_SESION, @PARTIDA_DIA, @FECHA, @PARTIDA_ANUAL, @NUM_DECIMALES, @TIPO,
                    @HORA_CIERRE, @HORA_VENTA, @HORA_SORTEO, @HORA_FIN, @HORA_CIERRE_H, @PRECIO, @VENTA, @CAJA, @TASA,
                    @BENEFICIO, @CAJA_I, @CAJA_A_I, @ACUMULADOS, @LETRA1, @SERIE_1, @DEL_1, @AL_1, @LETRA2, @SERIE_2,
                    @DEL_2, @AL_2, @VENDIDOS, @LARGO_SERIE, @EXTRACCIONES, @GUIABOLAS,
                    @SERIE_1_VIR, @DEL_1_VIR, @AL_1_VIR, @SERIE_2_VIR, @DEL_2_VIR, @AL_2_VIR, @VENDIDOS_VIR,
                    @RECLAMACION, @BOLAS, @BOLA_LINEA, @NUM_LINEAS, @HORA_LINEA, @CART_LINEA, @BOLA_BINGO,
                    @NUM_BINGOS, @HORA_BINGO, @CART_BINGO, @FECHA_SESION_MES,
                    {parametrosPremios}
                )
                MATCHING (COD_SALA, COD_CONC, FECHA_SESION, PARTIDA_DIA)";

                        using (FbCommand cmd = new FbCommand(sql, connection, trans)) {
                            // Las columnas numéricas que no llegan se guardan a 0
                            // en vez de NULL (decidido con el usuario 2026-10-30);
                            // las de texto, fecha y hora siguen a NULL.

                            // --- Identificación ---
                            cmd.Parameters.Add("@COD_SALA", dto.CodSala);
                            cmd.Parameters.Add("@COD_CONC", dto.CodConc);
                            cmd.Parameters.Add("@FECHA_SESION", dto.FechaSesion);
                            cmd.Parameters.Add("@PARTIDA_DIA", dto.PartidaDia);

                            // --- Datos Generales y Económicos ---
                            cmd.Parameters.Add("@FECHA", (object?)dto.Fecha ?? DBNull.Value);
                            cmd.Parameters.Add("@PARTIDA_ANUAL", dto.PartidaAnual ?? 0);
                            // Sin *K#, PartidaAccumulator ya ha dividido los importes
                            // entre 100: guardar 0 diría que no llevan decimales.
                            cmd.Parameters.Add("@NUM_DECIMALES", dto.NumDecimales ?? DecimalesPorDefecto);
                            cmd.Parameters.Add("@TIPO", dto.Tipo ?? 0);
                            cmd.Parameters.Add("@HORA_CIERRE", (object?)dto.HoraCierre ?? DBNull.Value);
                            cmd.Parameters.Add("@HORA_VENTA", (object?)dto.HoraVenta ?? DBNull.Value);
                            cmd.Parameters.Add("@HORA_SORTEO", (object?)dto.HoraSorteo ?? DBNull.Value);
                            cmd.Parameters.Add("@HORA_FIN", HoraFinAlGuardar(dto.HoraFin, _reloj.GetLocalNow().DateTime));
                            cmd.Parameters.Add("@HORA_CIERRE_H", EnteroOCero(dto.HoraCierreH));
                            cmd.Parameters.Add("@PRECIO", dto.Precio);
                            cmd.Parameters.Add("@VENTA", dto.Venta);
                            cmd.Parameters.Add("@CAJA", dto.Caja);
                            cmd.Parameters.Add("@TASA", dto.Tasa);
                            cmd.Parameters.Add("@BENEFICIO", dto.Beneficio);
                            cmd.Parameters.Add("@CAJA_I", dto.CajaI ?? 0m);
                            cmd.Parameters.Add("@CAJA_A_I", dto.CajaAI ?? 0m);
                            cmd.Parameters.Add("@ACUMULADOS", dto.Acumulados ?? 0m);

                            // --- Series y Juego ---
                            cmd.Parameters.Add("@LETRA1", (object?)dto.Letra1 ?? DBNull.Value);
                            cmd.Parameters.Add("@SERIE_1", dto.Serie1 ?? 0);
                            cmd.Parameters.Add("@DEL_1", dto.Del1 ?? 0);
                            cmd.Parameters.Add("@AL_1", dto.Al1 ?? 0);
                            cmd.Parameters.Add("@LETRA2", (object?)dto.Letra2 ?? DBNull.Value);
                            cmd.Parameters.Add("@SERIE_2", dto.Serie2 ?? 0);
                            cmd.Parameters.Add("@DEL_2", dto.Del2 ?? 0);
                            cmd.Parameters.Add("@AL_2", dto.Al2 ?? 0);
                            cmd.Parameters.Add("@VENDIDOS", dto.Vendidos);
                            cmd.Parameters.Add("@LARGO_SERIE", dto.LargoSerie ?? 0);
                            cmd.Parameters.Add("@EXTRACCIONES", dto.Extracciones ?? 0);
                            cmd.Parameters.Add("@GUIABOLAS", (object?)dto.GuiaBolas ?? DBNull.Value);
                            // Columnas *_VIR: el protocolo no las trae, pero la tabla las
                            // tiene (confirmado también en aamslog.FDB, 2026-10-30).
                            // LETRA1_VIR/LETRA2_VIR son texto: no se incluyen y quedan NULL.
                            cmd.Parameters.Add("@SERIE_1_VIR", dto.Serie1Vir ?? 0);
                            cmd.Parameters.Add("@DEL_1_VIR", dto.Del1Vir ?? 0);
                            cmd.Parameters.Add("@AL_1_VIR", dto.Al1Vir ?? 0);
                            cmd.Parameters.Add("@SERIE_2_VIR", dto.Serie2Vir ?? 0);
                            cmd.Parameters.Add("@DEL_2_VIR", dto.Del2Vir ?? 0);
                            cmd.Parameters.Add("@AL_2_VIR", dto.Al2Vir ?? 0);
                            cmd.Parameters.Add("@VENDIDOS_VIR", dto.VendidosVir ?? 0);
                            cmd.Parameters.Add("@RECLAMACION", (object?)reclamacion ?? DBNull.Value);
                            cmd.Parameters.Add("@BOLAS", (object?)bolas ?? DBNull.Value);
                            cmd.Parameters.Add("@BOLA_LINEA", dto.BolaLinea ?? 0);
                            cmd.Parameters.Add("@NUM_LINEAS", dto.NumLineas ?? 0);
                            cmd.Parameters.Add("@HORA_LINEA", (object?)dto.HoraLinea ?? DBNull.Value);
                            cmd.Parameters.Add("@CART_LINEA", (object?)dto.CartLinea ?? DBNull.Value);                            
                            cmd.Parameters.Add("@BOLA_BINGO", dto.BolaBingo ?? 0);
                            cmd.Parameters.Add("@NUM_BINGOS", dto.NumBingos ?? 0);
                            cmd.Parameters.Add("@HORA_BINGO", (object?)dto.HoraBingo ?? DBNull.Value);
                            cmd.Parameters.Add("@CART_BINGO", (object?)dto.CartBingo ?? DBNull.Value);
                            cmd.Parameters.Add("@FECHA_SESION_MES", EnteroOCero(dto.FechaSesionMes));

                            // Bucle para los 99 premios
                            for (int i = 1; i <= 99; i++) {
                                decimal valor = 0m;
                                if (dto.Premios != null && dto.Premios.Length >= i) {
                                    valor = dto.Premios[i - 1] ?? 0m;
                                }
                                cmd.Parameters.Add($"@PR{i}", valor);
                            }

                            // --- Advertencia de Negocio: Validación de datos sospechosos ---
                            if (dto.Precio <= 0) {
                                _logger.LogWarning("! ADVERTENCIA: Partida recibida con PRECIO cero o negativo. Sala: {sala}", dto.CodSala);
                            }

                            if (dto.Premios == null || dto.Premios.All(p => p == 0)) {
                                _logger.LogWarning("! ADVERTENCIA: La partida no contiene importes en los PREMIOS. Sala: {sala}, Partida: {partida}",
                                    dto.CodSala, dto.PartidaDia);
                            }

                            // 1. Iniciamos el cronómetro justo antes de la ejecución
                            var watch = System.Diagnostics.Stopwatch.StartNew();

                            await cmd.ExecuteNonQueryAsync(); // La operación que queremos medir

                            // 2. Paramos el cronómetro
                            watch.Stop();

                            trans.Commit();

                            // 3. Logueamos el tiempo resultante
                            _logger.LogInformation("V PARTIDA GUARDADA: Sala {sala}, Partida {partida} en {ms}ms",
                                dto.CodSala, dto.PartidaDia, watch.ElapsedMilliseconds);

                            return true;
                        }
                    }
                    catch (Exception ex) {
                        trans.Rollback();
                        _logger.LogError(ex, "X Error al guardar Sala {sala}, Partida {partida}. Datos: {@dto}",
                            dto.CodSala, dto.PartidaDia, dto);
                        return false;
                    }
                }

            }
        }
        finally {
            // 2. IMPORTANTÍSIMO: Soltamos el turno para que el siguiente pueda entrar, 
            // pase lo que pase (éxito o error).
            _dbSemaphore.Release();
        }
    }

    private const int DecimalesPorDefecto = 2;

    // HORA_CIERRE_H y FECHA_SESION_MES son INTEGER en la tabla pero string en
    // el DTO (así los trae el protocolo JSON): vacío -> 0; si no, tal cual.
    public static object EnteroOCero(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? 0 : valor;

    private const string NotaBolasRecortadas = "BOLAS RECORTADAS";
    private const int MaxReclamacion = 64; // RECLAMACION es CHAR(64)

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

    private static string ValidateTableName(string? tableName) {
        if (string.IsNullOrWhiteSpace(tableName) ||
            tableName.Length > 63 ||
            !Regex.IsMatch(tableName, @"\A[A-Za-z][A-Za-z0-9_$]*\z", RegexOptions.CultureInvariant)) {
            throw new InvalidOperationException(
                "DatabaseSettings:TableName debe ser un identificador Firebird de 1 a 63 caracteres, " +
                "empezar por una letra y contener solo letras, números, '_' o '$'.");
        }

        return tableName;
    }
}