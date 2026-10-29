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

    public HistoricoRepository(IOptionsMonitor<DatabaseSettings> dbSettings, ILogger<HistoricoRepository> logger) {
        _dbSettings = dbSettings;
        _logger = logger;
    }

    public async Task<bool> SaveHistoricoAsync(HistoricoDTO dto) {
        // Log de nivel Debug: Solo lo verás si en el JSON pones "Default": "Debug"
        _logger.LogDebug("Intentando guardar Partida {partida} de la Sala {sala}...", dto.PartidaDia, dto.CodSala);

        var dbSettings = _dbSettings.CurrentValue;
        string tableName = ValidateTableName(dbSettings.TableName);

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
                    RECLAMACION, BOLAS, BOLA_LINEA, NUM_LINEAS, HORA_LINEA, CART_LINEA, BOLA_BINGO, 
                    NUM_BINGOS, HORA_BINGO, CART_BINGO, FECHA_SESION_MES,
                    {columnasPremios}
                ) VALUES (
                    @COD_SALA, @COD_CONC, @FECHA_SESION, @PARTIDA_DIA, @FECHA, @PARTIDA_ANUAL, @NUM_DECIMALES, @TIPO,
                    @HORA_CIERRE, @HORA_VENTA, @HORA_SORTEO, @HORA_FIN, @HORA_CIERRE_H, @PRECIO, @VENTA, @CAJA, @TASA,
                    @BENEFICIO, @CAJA_I, @CAJA_A_I, @ACUMULADOS, @LETRA1, @SERIE_1, @DEL_1, @AL_1, @LETRA2, @SERIE_2,
                    @DEL_2, @AL_2, @VENDIDOS, @LARGO_SERIE, @EXTRACCIONES, @GUIABOLAS,
                    @RECLAMACION, @BOLAS, @BOLA_LINEA, @NUM_LINEAS, @HORA_LINEA, @CART_LINEA, @BOLA_BINGO,
                    @NUM_BINGOS, @HORA_BINGO, @CART_BINGO, @FECHA_SESION_MES,
                    {parametrosPremios}
                )
                MATCHING (COD_SALA, COD_CONC, FECHA_SESION, PARTIDA_DIA)";

                        using (FbCommand cmd = new FbCommand(sql, connection, trans)) {
                            // --- Identificación ---
                            cmd.Parameters.Add("@COD_SALA", dto.CodSala);
                            cmd.Parameters.Add("@COD_CONC", dto.CodConc);
                            cmd.Parameters.Add("@FECHA_SESION", dto.FechaSesion);
                            cmd.Parameters.Add("@PARTIDA_DIA", dto.PartidaDia);

                            // --- Datos Generales y Económicos ---
                            cmd.Parameters.Add("@FECHA", (object?)dto.Fecha ?? DBNull.Value);
                            cmd.Parameters.Add("@PARTIDA_ANUAL", (object?)dto.PartidaAnual ?? DBNull.Value);
                            cmd.Parameters.Add("@NUM_DECIMALES", (object?)dto.NumDecimales ?? DBNull.Value);
                            cmd.Parameters.Add("@TIPO", (object?)dto.Tipo ?? DBNull.Value);
                            cmd.Parameters.Add("@HORA_CIERRE", (object?)dto.HoraCierre ?? DBNull.Value);
                            cmd.Parameters.Add("@HORA_VENTA", (object?)dto.HoraVenta ?? DBNull.Value);
                            cmd.Parameters.Add("@HORA_SORTEO", (object?)dto.HoraSorteo ?? DBNull.Value);
                            cmd.Parameters.Add("@HORA_FIN", (object?)dto.HoraFin ?? DBNull.Value);
                            cmd.Parameters.Add("@HORA_CIERRE_H", (object?)dto.HoraCierreH ?? DBNull.Value);
                            cmd.Parameters.Add("@PRECIO", dto.Precio);
                            cmd.Parameters.Add("@VENTA", dto.Venta);
                            cmd.Parameters.Add("@CAJA", dto.Caja);
                            cmd.Parameters.Add("@TASA", dto.Tasa);
                            cmd.Parameters.Add("@BENEFICIO", dto.Beneficio);
                            cmd.Parameters.Add("@CAJA_I", (object?)dto.CajaI ?? DBNull.Value);
                            cmd.Parameters.Add("@CAJA_A_I", (object?)dto.CajaAI ?? DBNull.Value);
                            cmd.Parameters.Add("@ACUMULADOS", (object?)dto.Acumulados ?? DBNull.Value);

                            // --- Series y Juego ---
                            cmd.Parameters.Add("@LETRA1", (object?)dto.Letra1 ?? DBNull.Value);
                            cmd.Parameters.Add("@SERIE_1", (object?)dto.Serie1 ?? DBNull.Value);
                            cmd.Parameters.Add("@DEL_1", (object?)dto.Del1 ?? DBNull.Value);
                            cmd.Parameters.Add("@AL_1", (object?)dto.Al1 ?? DBNull.Value);
                            cmd.Parameters.Add("@LETRA2", (object?)dto.Letra2 ?? DBNull.Value);
                            cmd.Parameters.Add("@SERIE_2", (object?)dto.Serie2 ?? DBNull.Value);
                            cmd.Parameters.Add("@DEL_2", (object?)dto.Del2 ?? DBNull.Value);
                            cmd.Parameters.Add("@AL_2", (object?)dto.Al2 ?? DBNull.Value);
                            cmd.Parameters.Add("@VENDIDOS", dto.Vendidos);
                            cmd.Parameters.Add("@LARGO_SERIE", (object?)dto.LargoSerie ?? DBNull.Value);
                            cmd.Parameters.Add("@EXTRACCIONES", (object?)dto.Extracciones ?? DBNull.Value);
                            cmd.Parameters.Add("@GUIABOLAS", (object?)dto.GuiaBolas ?? DBNull.Value);
                            cmd.Parameters.Add("@RECLAMACION", (object?)dto.Reclamacion ?? DBNull.Value);
                            cmd.Parameters.Add("@BOLAS", (object?)dto.Bolas ?? DBNull.Value);
                            cmd.Parameters.Add("@BOLA_LINEA", (object?)dto.BolaLinea ?? DBNull.Value);
                            cmd.Parameters.Add("@NUM_LINEAS", (object?)dto.NumLineas ?? DBNull.Value);
                            cmd.Parameters.Add("@HORA_LINEA", (object?)dto.HoraLinea ?? DBNull.Value);
                            cmd.Parameters.Add("@CART_LINEA", (object?)dto.CartLinea ?? DBNull.Value);                            
                            cmd.Parameters.Add("@BOLA_BINGO", (object?)dto.BolaBingo ?? DBNull.Value);
                            cmd.Parameters.Add("@NUM_BINGOS", (object?)dto.NumBingos ?? DBNull.Value);
                            cmd.Parameters.Add("@HORA_BINGO", (object?)dto.HoraBingo ?? DBNull.Value);
                            cmd.Parameters.Add("@CART_BINGO", (object?)dto.CartBingo ?? DBNull.Value);
                            cmd.Parameters.Add("@FECHA_SESION_MES", (object?)dto.FechaSesionMes ?? DBNull.Value);

                            // Bucle para los 99 premios
                            for (int i = 1; i <= 99; i++) {
                                object valor = DBNull.Value;
                                if (dto.Premios != null && dto.Premios.Length >= i) {
                                    valor = (object?)dto.Premios[i - 1] ?? DBNull.Value;
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