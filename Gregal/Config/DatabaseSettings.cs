using System;
using System.Collections.Generic;
using System.Text;

namespace Gregal.Config {
    public class DatabaseSettings {
        public string ConnectionString { get; set; } = string.Empty;
        public string TableName { get; set; } = "HISTORICO";
        // Longitud de la columna HISTORICO.BOLAS en la base de datos de la
        // sala. Una lista más larga (hacia 86 bolas o más) haría que Firebird
        // rechazara la partida entera, así que se recorta a las primeras bolas
        // que quepan y se anota en RECLAMACION. 255 es el tamaño original;
        // con la migración 2026-10-08_ampliar_bolas.sql aplicada, ponlo a 300.
        public int MaxLongitudBolas { get; set; } = 255;
    }
}
