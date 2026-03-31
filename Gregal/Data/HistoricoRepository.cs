using FirebirdSql.Data.FirebirdClient;
using Gregal.Models;
using Gregal.Config;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Gregal.Data;

public class HistoricoRepository {
    private readonly IOptionsMonitor<DatabaseSettings> _dbSettings;
    private readonly ILogger<HistoricoRepository> _logger;

    public HistoricoRepository(IOptionsMonitor<DatabaseSettings> dbSettings, ILogger<HistoricoRepository> logger) {
        _dbSettings = dbSettings;
        _logger = logger;
    }

    /// <summary>
    /// NOTA: Evitar el uso de este método en nuevos desarrollos. 
    /// Bloquea el hilo actual. Usar SaveHistoricoAsync en su lugar.
    /// </summary>
    //[Obsolete("Usar SaveHistoricoAsync para evitar deadlocks.")]
    //public void Guardar(Historico h) {
    //    var dto = MapEntityToDto(h);
    //    SaveHistoricoAsync(dto).GetAwaiter().GetResult();
    //}

    // TODO: Mover a una clase HistoricoMapper en la próxima refactorización
    //private HistoricoDTO MapEntityToDto(Historico h) {
    //    var dto = new HistoricoDTO {
    //        CodSala = h.COD_SALA,
    //        CodConc = h.COD_CONC,
    //        FechaSesion = h.FECHA_SESION.ToString("yyyy-MM-dd"),
    //        PartidaDia = h.PARTIDA_DIA,
    //        Fecha = h.FECHA,
    //        PartidaAnual = h.PARTIDA_ANUAL,
    //        NumDecimales = h.NUM_DECIMALES,
    //        Tipo = h.TIPO,
    //        HoraCierre = h.HORA_CIERRE?.ToString(@"hh\:mm\:ss"),
    //        HoraVenta = h.HORA_VENTA?.ToString(@"hh\:mm\:ss"),
    //        HoraSorteo = h.HORA_SORTEO?.ToString(@"hh\:mm\:ss"),
    //        HoraFin = h.HORA_FIN?.ToString(@"hh\:mm\:ss"),
    //        HoraCierreH = h.HORA_CIERRE_H?.ToString(),
    //        FechaSesionMes = h.FECHA_SESION_MES?.ToString(),
    //        Precio = h.PRECIO ?? 0,
    //        Venta = h.VENTA ?? 0,
    //        Caja = h.CAJA ?? 0,
    //        Tasa = h.TASA ?? 0,
    //        Beneficio = h.BENEFICIO ?? 0,
    //        Letra1 = h.LETRA1,
    //        Serie1 = h.SERIE_1,
    //        Del1 = h.DEL_1,
    //        Al1 = h.AL_1,
    //        Letra2 = h.LETRA2,
    //        Serie2 = h.SERIE_2,
    //        Del2 = h.DEL_2,
    //        Al2 = h.AL_2,
    //        Vendidos = h.VENDIDOS ?? 0,
    //        LargoSerie = h.LARGO_SERIE,
    //        Bolas = h.BOLAS,
    //        HoraLinea = h.HORA_LINEA?.ToString(@"hh\:mm\:ss"),
    //        CartLinea = h.CART_LINEA?.ToString(),
    //        HoraBingo = h.HORA_BINGO?.ToString(@"hh\:mm\:ss"),
    //        CartBingo = h.CART_BINGO?.ToString(),
    //        Extracciones = h.EXTRACCIONES,
    //        BolaLinea = h.BOLA_LINEA,
    //        NumLineas = h.NUM_LINEAS,
    //        BolaBingo = h.BOLA_BINGO,
    //        NumBingos = h.NUM_BINGOS
    //    };

    //    for (int i = 1; i <= 99; i++) {
    //        var prop = typeof(Historico).GetProperty($"PREMIO{i}");
    //        dto.Premios[i - 1] = (decimal?)prop?.GetValue(h);
    //    }
    //    return dto;
    //}

    public async Task<bool> SaveHistoricoAsync(HistoricoDTO dto) {
        using (FbConnection connection = new FbConnection(_dbSettings.CurrentValue.ConnectionString)) {
            await connection.OpenAsync();

            // SQL REFRACTORIZADO CON NOMBRES DESCRIPTIVOS
            string sql = @"UPDATE OR INSERT INTO HISTORICO (
                COD_SALA, COD_CONC, FECHA_SESION, PARTIDA_DIA, FECHA, PARTIDA_ANUAL, NUM_DECIMALES, TIPO, 
                HORA_CIERRE, HORA_VENTA, HORA_SORTEO, HORA_FIN, HORA_CIERRE_H, PRECIO, VENTA, CAJA, TASA, 
                BENEFICIO, CAJA_I, CAJA_A_I, ACUMULADOS, LETRA1, SERIE_1, DEL_1, AL_1, LETRA2, SERIE_2, 
                DEL_2, AL_2, VENDIDOS, LARGO_SERIE, EXTRACCIONES, GUIABOLAS, 
                RECLAMACION, BOLAS, BOLA_LINEA, NUM_LINEAS, HORA_LINEA, CART_LINEA, BOLA_BINGO, 
                NUM_BINGOS, HORA_BINGO, CART_BINGO, FECHA_SESION_MES,
                PREMIO1, PREMIO2, PREMIO3, PREMIO4, PREMIO5, PREMIO6, PREMIO7, PREMIO8, PREMIO9, PREMIO10
                -- ... (continuar con los premios según sea necesario)
            ) VALUES (
                @COD_SALA, @COD_CONC, @FECHA_SESION, @PARTIDA_DIA, @FECHA, @PARTIDA_ANUAL, @NUM_DECIMALES, @TIPO,
                @HORA_CIERRE, @HORA_VENTA, @HORA_SORTEO, @HORA_FIN, @HORA_CIERRE_H, @PRECIO, @VENTA, @CAJA, @TASA,
                @BENEFICIO, @CAJA_I, @CAJA_A_I, @ACUMULADOS, @LETRA1, @SERIE_1, @DEL_1, @AL_1, @LETRA2, @SERIE_2,
                @DEL_2, @AL_2, @VENDIDOS, @LARGO_SERIE, @EXTRACCIONES, @GUIABOLAS,
                @RECLAMACION, @BOLAS, @BOLA_LINEA, @NUM_LINEAS, @HORA_LINEA, @CART_LINEA, @BOLA_BINGO,
                @NUM_Bingos, @HORA_BINGO, @CART_BINGO, @FECHA_SESION_MES,
                @PR1, @PR2, @PR3, @PR4, @PR5, @PR6, @PR7, @PR8, @PR9, @PR10
                -- ...
            )
            MATCHING (COD_SALA, COD_CONC, FECHA_SESION, PARTIDA_DIA)";

            try {
                using (FbCommand cmd = new FbCommand(sql, connection)) {
                    // Parámetros con nombres claros
                    cmd.Parameters.Add("@COD_SALA", dto.CodSala);
                    cmd.Parameters.Add("@COD_CONC", dto.CodConc);
                    cmd.Parameters.Add("@FECHA_SESION", dto.FechaSesion);
                    cmd.Parameters.Add("@PARTIDA_DIA", dto.PartidaDia);
                    cmd.Parameters.Add("@FECHA", (object)dto.Fecha ?? DBNull.Value);
                    cmd.Parameters.Add("@PARTIDA_ANUAL", (object)dto.PartidaAnual ?? DBNull.Value);
                    cmd.Parameters.Add("@NUM_DECIMALES", (object)dto.NumDecimales ?? DBNull.Value);
                    cmd.Parameters.Add("@TIPO", (object)dto.Tipo ?? DBNull.Value);
                    cmd.Parameters.Add("@HORA_CIERRE", (object)dto.HoraCierre ?? DBNull.Value);
                    cmd.Parameters.Add("@HORA_VENTA", (object)dto.HoraVenta ?? DBNull.Value);
                    cmd.Parameters.Add("@HORA_SORTEO", (object)dto.HoraSorteo ?? DBNull.Value);
                    cmd.Parameters.Add("@HORA_FIN", (object)dto.HoraFin ?? DBNull.Value);
                    cmd.Parameters.Add("@HORA_CIERRE_H", (object)dto.HoraCierreH ?? DBNull.Value);
                    cmd.Parameters.Add("@PRECIO", (object)dto.Precio ?? DBNull.Value);
                    cmd.Parameters.Add("@VENTA", (object)dto.Venta ?? DBNull.Value);
                    cmd.Parameters.Add("@CAJA", (object)dto.Caja ?? DBNull.Value);
                    cmd.Parameters.Add("@TASA", (object)dto.Tasa ?? DBNull.Value);
                    cmd.Parameters.Add("@BENEFICIO", (object)dto.Beneficio ?? DBNull.Value);
                    cmd.Parameters.Add("@CAJA_I", (object)dto.CajaI ?? DBNull.Value);
                    cmd.Parameters.Add("@CAJA_A_I", (object)dto.CajaAI ?? DBNull.Value);
                    cmd.Parameters.Add("@ACUMULADOS", (object)dto.Acumulados ?? DBNull.Value);
                    cmd.Parameters.Add("@LETRA1", (object)dto.Letra1 ?? DBNull.Value);
                    cmd.Parameters.Add("@SERIE_1", (object)dto.Serie1 ?? DBNull.Value);
                    cmd.Parameters.Add("@DEL_1", (object)dto.Del1 ?? DBNull.Value);
                    cmd.Parameters.Add("@AL_1", (object)dto.Al1 ?? DBNull.Value);
                    cmd.Parameters.Add("@LETRA2", (object)dto.Letra2 ?? DBNull.Value);
                    cmd.Parameters.Add("@SERIE_2", (object)dto.Serie2 ?? DBNull.Value);
                    cmd.Parameters.Add("@DEL_2", (object)dto.Del2 ?? DBNull.Value);
                    cmd.Parameters.Add("@AL_2", (object)dto.Al2 ?? DBNull.Value);
                    cmd.Parameters.Add("@VENDIDOS", (object)dto.Vendidos ?? DBNull.Value);
                    cmd.Parameters.Add("@LARGO_SERIE", (object)dto.LargoSerie ?? DBNull.Value);
                    cmd.Parameters.Add("@EXTRACCIONES", (object)dto.Extracciones ?? DBNull.Value);
                    cmd.Parameters.Add("@GUIABOLAS", (object)dto.GuiaBolas ?? DBNull.Value);
                    cmd.Parameters.Add("@RECLAMACION", (object)dto.Reclamacion ?? DBNull.Value);
                    cmd.Parameters.Add("@BOLAS", (object)dto.Bolas ?? DBNull.Value);
                    cmd.Parameters.Add("@BOLA_LINEA", (object)dto.BolaLinea ?? DBNull.Value);
                    cmd.Parameters.Add("@NUM_LINEAS", (object)dto.NumLineas ?? DBNull.Value);
                    cmd.Parameters.Add("@HORA_LINEA", (object)dto.HoraLinea ?? DBNull.Value);
                    cmd.Parameters.Add("@CART_LINEA", (object)dto.CartLinea ?? DBNull.Value);
                    cmd.Parameters.Add("@BOLA_BINGO", (object)dto.BolaBingo ?? DBNull.Value);
                    cmd.Parameters.Add("@NUM_Bingos", (object)dto.NumBingos ?? DBNull.Value);
                    cmd.Parameters.Add("@HORA_BINGO", (object)dto.HoraBingo ?? DBNull.Value);
                    cmd.Parameters.Add("@CART_BINGO", (object)dto.CartBingo ?? DBNull.Value);
                    cmd.Parameters.Add("@FECHA_SESION_MES", (object)dto.FechaSesionMes ?? DBNull.Value);

                    for (int i = 1; i <= 99; i++) {
                        object valor = DBNull.Value;
                        if (dto.Premios != null && dto.Premios.Length >= i) {
                            valor = (object)dto.Premios[i - 1] ?? DBNull.Value;
                        }
                        cmd.Parameters.Add($"@PR{i}", valor);
                    }

                    await cmd.ExecuteNonQueryAsync();
                    return true;
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error al guardar HistoricoDTO en Sala {sala}, Partida {partida}", dto.CodSala, dto.PartidaDia);
                return false;
            }
        }
    }
}