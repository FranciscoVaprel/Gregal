using FirebirdSql.Data.FirebirdClient;
using Gregal.Models;
using Gregal.Config;
using Microsoft.Extensions.Options;

namespace Gregal.Data;

public class HistoricoRepository {
    private readonly IOptionsMonitor<DatabaseSettings> _dbSettings;
    private readonly ILogger<HistoricoRepository> _logger;

    public HistoricoRepository(IOptionsMonitor<DatabaseSettings> dbSettings, ILogger<HistoricoRepository> logger) {
        _dbSettings = dbSettings;
        _logger = logger;
    }

    public void Insertar(Historico h) {
        using (FbConnection connection = new FbConnection(_dbSettings.CurrentValue.ConnectionString)) {
            connection.Open();

            string sql = @"INSERT INTO HISTORICO (
                COD_SALA, COD_CONC, FECHA_SESION, PARTIDA_DIA, FECHA, PARTIDA_ANUAL, NUM_DECIMALES, TIPO, 
                HORA_CIERRE, HORA_VENTA, PRECIO, LETRA1, SERIE_1, DEL_1, AL_1, LETRA2, SERIE_2, DEL_2, AL_2, 
                VENDIDOS, LETRA1_VIR, SERIE_1_VIR, DEL_1_VIR, AL_1_VIR, LETRA2_VIR, SERIE_2_VIR, DEL_2_VIR, 
                AL_2_VIR, VENDIDOS_VIR, LARGO_SERIE, VENTA, CAJA, TASA, BENEFICIO, CAJA_I, CAJA_A_I, 
                ACUMULADOS, EXTRACCIONES, GUIABOLAS, RECLAMACION, HORA_SORTEO, BOLAS, BOLA_LINEA, 
                NUM_LINEAS, HORA_LINEA, CART_LINEA, BOLA_BINGO, NUM_BINGOS, HORA_BINGO, CART_BINGO, 
                HORA_FIN, HORA_CIERRE_H, FECHA_SESION_MES,
                PREMIO1, PREMIO2, PREMIO3, PREMIO4, PREMIO5, PREMIO6, PREMIO7, PREMIO8, PREMIO9, PREMIO10,
                PREMIO11, PREMIO12, PREMIO13, PREMIO14, PREMIO15, PREMIO16, PREMIO17, PREMIO18, PREMIO19, PREMIO20,
                PREMIO21, PREMIO22, PREMIO23, PREMIO24, PREMIO25, PREMIO26, PREMIO27, PREMIO28, PREMIO29, PREMIO30,
                PREMIO31, PREMIO32, PREMIO33, PREMIO34, PREMIO35, PREMIO36, PREMIO37, PREMIO38, PREMIO39, PREMIO40,
                PREMIO41, PREMIO42, PREMIO43, PREMIO44, PREMIO45, PREMIO46, PREMIO47, PREMIO48, PREMIO49, PREMIO50,
                PREMIO51, PREMIO52, PREMIO53, PREMIO54, PREMIO55, PREMIO56, PREMIO57, PREMIO58, PREMIO59, PREMIO60,
                PREMIO61, PREMIO62, PREMIO63, PREMIO64, PREMIO65, PREMIO66, PREMIO67, PREMIO68, PREMIO69, PREMIO70,
                PREMIO71, PREMIO72, PREMIO73, PREMIO74, PREMIO75, PREMIO76, PREMIO77, PREMIO78, PREMIO79, PREMIO80,
                PREMIO81, PREMIO82, PREMIO83, PREMIO84, PREMIO85, PREMIO86, PREMIO87, PREMIO88, PREMIO89, PREMIO90,
                PREMIO91, PREMIO92, PREMIO93, PREMIO94, PREMIO95, PREMIO96, PREMIO97, PREMIO98, PREMIO99
            ) VALUES (
                @p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17, @p18, 
                @p19, @p20, @p21, @p22, @p23, @p24, @p25, @p26, @p27, @p28, @p29, @p30, @p31, @p32, @p33, @p34, @p35, 
                @p36, @p37, @p38, @p39, @p40, @p41, @p42, @p43, @p44, @p45, @p46, @p47, @p48, @p49, @p50, @p51, @p52,
                @PR1, @PR2, @PR3, @PR4, @PR5, @PR6, @PR7, @PR8, @PR9, @PR10, @PR11, @PR12, @PR13, @PR14, @PR15, @PR16, @PR17, @PR18, @PR19, @PR20,
                @PR21, @PR22, @PR23, @PR24, @PR25, @PR26, @PR27, @PR28, @PR29, @PR30, @PR31, @PR32, @PR33, @PR34, @PR35, @PR36, @PR37, @PR38, @PR39, @PR40,
                @PR41, @PR42, @PR43, @PR44, @PR45, @PR46, @PR47, @PR48, @PR49, @PR50, @PR51, @PR52, @PR53, @PR54, @PR55, @PR56, @PR57, @PR58, @PR59, @PR60,
                @PR61, @PR62, @PR63, @PR64, @PR65, @PR66, @PR67, @PR68, @PR69, @PR70, @PR71, @PR72, @PR73, @PR74, @PR75, @PR76, @PR77, @PR78, @PR79, @PR80,
                @PR81, @PR82, @PR83, @PR84, @PR85, @PR86, @PR87, @PR88, @PR89, @PR90, @PR91, @PR92, @PR93, @PR94, @PR95, @PR96, @PR97, @PR98, @PR99
            )";
            
            /*
             Esa línea es la encargada de pasar el dato desde tu código C# al motor de la base de datos Firebird de forma segura. Se puede desglosar en cuatro partes clave:
                1. cmd.Parameters.Add

                En lugar de escribir el valor directamente en la cadena de texto del SQL (lo cual es peligroso y lento), usamos parámetros. Esto evita ataques de Inyección SQL y permite que el motor de la base de datos reutilice el plan de ejecución.
                2. "@p51"

                Es la etiqueta o "sitio reservado" que pusimos en el comando INSERT. El motor busca @p51 en el SQL y lo sustituye por el valor que le estamos pasando aquí. En este caso, corresponde a la columna HORA_CIERRE_H.
                3. (object)h.HORA_CIERRE_H

                Aquí ocurren dos cosas:

                    Accedemos a la propiedad HORA_CIERRE_H del objeto Historico que quieres guardar.

                    Casteo a (object): Los parámetros de ADO.NET esperan un tipo de dato genérico (object). Como HORA_CIERRE_H es un entero opcional (int?), lo convertimos a objeto para que sea compatible con lo que espera el método Add.

                4. ?? DBNull.Value (El Operador Null-Coalescing)

                Esta es la parte más importante para evitar errores:

                    En C#, un valor vacío se representa como null.

                    En las bases de datos (SQL), un valor vacío se representa como DBNull.Value.

                    La lógica: Si h.HORA_CIERRE_H tiene un número, se usa ese número. Si es null, el operador ?? salta a la derecha y envía DBNull.Value. Sin esto, Firebird lanzaría una excepción porque no entiende el null de C# directamente en los parámetros.
             */

            using (FbCommand cmd = new FbCommand(sql, connection)) {
                // Campos base (0-52)
                cmd.Parameters.Add("@p0", h.COD_SALA);
                cmd.Parameters.Add("@p1", h.COD_CONC);
                cmd.Parameters.Add("@p2", h.FECHA_SESION);
                cmd.Parameters.Add("@p3", h.PARTIDA_DIA);
                cmd.Parameters.Add("@p4", (object)h.FECHA ?? DBNull.Value);
                cmd.Parameters.Add("@p5", (object)h.PARTIDA_ANUAL ?? DBNull.Value);
                cmd.Parameters.Add("@p6", (object)h.NUM_DECIMALES ?? DBNull.Value);
                cmd.Parameters.Add("@p7", (object)h.TIPO ?? DBNull.Value);
                cmd.Parameters.Add("@p8", (object)h.HORA_CIERRE ?? DBNull.Value);
                cmd.Parameters.Add("@p9", (object)h.HORA_VENTA ?? DBNull.Value);
                cmd.Parameters.Add("@p10", (object)h.PRECIO ?? DBNull.Value);
                cmd.Parameters.Add("@p11", (object)h.LETRA1 ?? DBNull.Value);
                cmd.Parameters.Add("@p12", (object)h.SERIE_1 ?? DBNull.Value);
                cmd.Parameters.Add("@p13", (object)h.DEL_1 ?? DBNull.Value);
                cmd.Parameters.Add("@p14", (object)h.AL_1 ?? DBNull.Value);
                cmd.Parameters.Add("@p15", (object)h.LETRA2 ?? DBNull.Value);
                cmd.Parameters.Add("@p16", (object)h.SERIE_2 ?? DBNull.Value);
                cmd.Parameters.Add("@p17", (object)h.DEL_2 ?? DBNull.Value);
                cmd.Parameters.Add("@p18", (object)h.AL_2 ?? DBNull.Value);
                cmd.Parameters.Add("@p19", (object)h.VENDIDOS ?? DBNull.Value);
                cmd.Parameters.Add("@p20", (object)h.LETRA1_VIR ?? DBNull.Value);
                cmd.Parameters.Add("@p21", (object)h.SERIE_1_VIR ?? DBNull.Value);
                cmd.Parameters.Add("@p22", (object)h.DEL_1_VIR ?? DBNull.Value);
                cmd.Parameters.Add("@p23", (object)h.AL_1_VIR ?? DBNull.Value);
                cmd.Parameters.Add("@p24", (object)h.LETRA2_VIR ?? DBNull.Value);
                cmd.Parameters.Add("@p25", (object)h.SERIE_2_VIR ?? DBNull.Value);
                cmd.Parameters.Add("@p26", (object)h.DEL_2_VIR ?? DBNull.Value);
                cmd.Parameters.Add("@p27", (object)h.AL_2_VIR ?? DBNull.Value);
                cmd.Parameters.Add("@p28", (object)h.VENDIDOS_VIR ?? DBNull.Value);
                cmd.Parameters.Add("@p29", (object)h.LARGO_SERIE ?? DBNull.Value);
                cmd.Parameters.Add("@p30", (object)h.VENTA ?? DBNull.Value);
                cmd.Parameters.Add("@p31", (object)h.CAJA ?? DBNull.Value);
                cmd.Parameters.Add("@p32", (object)h.TASA ?? DBNull.Value);
                cmd.Parameters.Add("@p33", (object)h.BENEFICIO ?? DBNull.Value);
                cmd.Parameters.Add("@p34", (object)h.CAJA_I ?? DBNull.Value);
                cmd.Parameters.Add("@p35", (object)h.CAJA_A_I ?? DBNull.Value);
                cmd.Parameters.Add("@p36", (object)h.ACUMULADOS ?? DBNull.Value);
                cmd.Parameters.Add("@p37", (object)h.EXTRACCIONES ?? DBNull.Value);
                cmd.Parameters.Add("@p38", (object)h.GUIABOLAS ?? DBNull.Value);
                cmd.Parameters.Add("@p39", (object)h.RECLAMACION ?? DBNull.Value);
                cmd.Parameters.Add("@p40", (object)h.HORA_SORTEO ?? DBNull.Value);
                cmd.Parameters.Add("@p41", (object)h.BOLAS ?? DBNull.Value);
                cmd.Parameters.Add("@p42", (object)h.BOLA_LINEA ?? DBNull.Value);
                cmd.Parameters.Add("@p43", (object)h.NUM_LINEAS ?? DBNull.Value);
                cmd.Parameters.Add("@p44", (object)h.HORA_LINEA ?? DBNull.Value);
                cmd.Parameters.Add("@p45", (object)h.CART_LINEA ?? DBNull.Value);
                cmd.Parameters.Add("@p46", (object)h.BOLA_BINGO ?? DBNull.Value);
                cmd.Parameters.Add("@p47", (object)h.NUM_BINGOS ?? DBNull.Value);
                cmd.Parameters.Add("@p48", (object)h.HORA_BINGO ?? DBNull.Value);
                cmd.Parameters.Add("@p49", (object)h.CART_BINGO ?? DBNull.Value);
                cmd.Parameters.Add("@p50", (object)h.HORA_FIN ?? DBNull.Value);
                cmd.Parameters.Add("@p51", (object)h.HORA_CIERRE_H ?? DBNull.Value);
                cmd.Parameters.Add("@p52", (object)h.FECHA_SESION_MES ?? DBNull.Value);

                // Bucle para los 99 premios (PR1 a PR99)
                for (int i = 1; i <= 99; i++) {
                    var valor = typeof(Historico).GetProperty($"PREMIO{i}")?.GetValue(h);
                    cmd.Parameters.Add($"@PR{i}", valor ?? DBNull.Value);
                }

                cmd.ExecuteNonQuery();
            }
        }
    }
}