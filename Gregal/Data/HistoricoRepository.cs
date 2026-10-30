using FirebirdSql.Data.FirebirdClient;
using Gregal.Models;
using Gregal.Config;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

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

    // Qué columnas se escriben y con qué valores (0 o NULL si el dato no
    // llega, bolas recortadas, HORA_FIN...) está en FilaHistorico, en la
    // librería Gregal.Protocolo: BingoHistoricoEditor escribe igual las
    // partidas que importa de un log.
    public async Task<bool> SaveHistoricoAsync(HistoricoDTO dto) {
        // Log de nivel Debug: Solo lo verás si en el JSON pones "Default": "Debug"
        _logger.LogDebug("Intentando guardar Partida {partida} de la Sala {sala}...", dto.PartidaDia, dto.CodSala);

        var dbSettings = _dbSettings.CurrentValue;
        string sql = FilaHistorico.SqlUpdateOrInsert(dbSettings.TableName);

        // No se toca el DTO: si el guardado falla, al fichero de pendientes
        // debe ir la lista completa.
        var (valores, descartadas) = FilaHistorico.Valores(dto, _reloj.GetLocalNow().DateTime, dbSettings.MaxLongitudBolas);
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
                        using (FbCommand cmd = new FbCommand(sql, connection, trans)) {
                            for (int i = 0; i < FilaHistorico.Columnas.Count; i++) {
                                cmd.Parameters.Add("@" + FilaHistorico.Columnas[i], valores[i]);
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

}