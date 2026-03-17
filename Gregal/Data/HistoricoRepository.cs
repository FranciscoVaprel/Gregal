//using FirebirdSql.Data.FirebirdClient;
//using Gregal.Models;
//using Gregal.Config;
//using Microsoft.Extensions.Options;

//namespace Gregal.Data;

//public class HistoricoRepository {
//    private readonly IOptionsMonitor<GregalSettings> _settings;

//    // Recibimos la configuración para saber dónde está la BBDD
//    public HistoricoRepository(IOptionsMonitor<GregalSettings> settings) {
//        _settings = settings;
//    }

//    public void Insertar(Historico h) {
//        // Usamos la cadena de conexión de nuestro appsettings.json
//        using (FbConnection connection = new FbConnection(_settings.CurrentValue.ConnectionString)) {
//            connection.Open();
//            string sql = @"INSERT INTO HISTORICO (CODSALA, CODCONC, FECHASESION, PARTIDADIA, VENTA) 
//                           VALUES (@sala, @conc, @fecha, @partida, @venta)";

//            using (FbCommand cmd = new FbCommand(sql, connection)) {
//                cmd.Parameters.Add("@sala", h.CodSala);
//                cmd.Parameters.Add("@conc", h.CodConc);
//                cmd.Parameters.Add("@fecha", h.FechaSesion);
//                cmd.Parameters.Add("@partida", h.PartidaDia);
//                cmd.Parameters.Add("@venta", h.Venta);

//                cmd.ExecuteNonQuery();
//            }
//        }
//    }
//}