using System;

namespace Gregal.Models {
    public class Historico {
        // --- CLAVE PRIMARIA COMPUESTA ---
        public int COD_SALA { get; set; }
        public int COD_CONC { get; set; }
        public DateTime FECHA_SESION { get; set; }
        public int PARTIDA_DIA { get; set; }

        // --- CAMPOS DE CONTROL Y TIEMPO ---
        public DateTime? FECHA { get; set; }
        public int? PARTIDA_ANUAL { get; set; }
        public int? NUM_DECIMALES { get; set; }
        public int? TIPO { get; set; }
        public TimeSpan? HORA_CIERRE { get; set; }
        public TimeSpan? HORA_VENTA { get; set; }
        public TimeSpan? HORA_SORTEO { get; set; }
        public TimeSpan? HORA_FIN { get; set; }
        public int? HORA_CIERRE_H { get; set; }

        // --- DATOS ECONÓMICOS ---
        public decimal? PRECIO { get; set; }
        public decimal? VENTA { get; set; }
        public decimal? CAJA { get; set; }
        public decimal? TASA { get; set; }
        public decimal? BENEFICIO { get; set; }
        public decimal? CAJA_I { get; set; }
        public decimal? CAJA_A_I { get; set; }
        public decimal? ACUMULADOS { get; set; }

        // --- SERIES Y CARTONES (Físicos) ---
        public string LETRA1 { get; set; }
        public int? SERIE_1 { get; set; }
        public int? DEL_1 { get; set; }
        public int? AL_1 { get; set; }
        public string LETRA2 { get; set; }
        public int? SERIE_2 { get; set; }
        public int? DEL_2 { get; set; }
        public int? AL_2 { get; set; }
        public int? VENDIDOS { get; set; }
        public int? LARGO_SERIE { get; set; }

        // --- SERIES Y CARTONES (Virtuales) ---
        public string LETRA1_VIR { get; set; }
        public int? SERIE_1_VIR { get; set; }
        public int? DEL_1_VIR { get; set; }
        public int? AL_1_VIR { get; set; }
        public string LETRA2_VIR { get; set; }
        public int? SERIE_2_VIR { get; set; }
        public int? DEL_2_VIR { get; set; }
        public int? AL_2_VIR { get; set; }
        public int? VENDIDOS_VIR { get; set; }

        // --- DATOS DE JUEGO ---
        public int? EXTRACCIONES { get; set; }
        public string GUIABOLAS { get; set; }
        public string RECLAMACION { get; set; }
        public string BOLAS { get; set; }
        public int? BOLA_LINEA { get; set; }
        public int? NUM_LINEAS { get; set; }
        public TimeSpan? HORA_LINEA { get; set; }
        public string CART_LINEA { get; set; }
        public int? BOLA_BINGO { get; set; }
        public int? NUM_BINGOS { get; set; }
        public TimeSpan? HORA_BINGO { get; set; }
        public string CART_BINGO { get; set; }
        public int? FECHA_SESION_MES { get; set; }

        // --- PREMIOS (Del 1 al 99) ---
        public decimal? PREMIO1 { get; set; }
        public decimal? PREMIO2 { get; set; }
        public decimal? PREMIO3 { get; set; }
        public decimal? PREMIO4 { get; set; }
        public decimal? PREMIO5 { get; set; }
        public decimal? PREMIO6 { get; set; }
        public decimal? PREMIO7 { get; set; }
        public decimal? PREMIO8 { get; set; }
        public decimal? PREMIO9 { get; set; }
        public decimal? PREMIO10 { get; set; }
        public decimal? PREMIO11 { get; set; }
        public decimal? PREMIO12 { get; set; }
        public decimal? PREMIO13 { get; set; }
        public decimal? PREMIO14 { get; set; }
        public decimal? PREMIO15 { get; set; }
        public decimal? PREMIO16 { get; set; }
        public decimal? PREMIO17 { get; set; }
        public decimal? PREMIO18 { get; set; }
        public decimal? PREMIO19 { get; set; }
        public decimal? PREMIO20 { get; set; }
        public decimal? PREMIO21 { get; set; }
        public decimal? PREMIO22 { get; set; }
        public decimal? PREMIO23 { get; set; }
        public decimal? PREMIO24 { get; set; }
        public decimal? PREMIO25 { get; set; }
        public decimal? PREMIO26 { get; set; }
        public decimal? PREMIO27 { get; set; }
        public decimal? PREMIO28 { get; set; }
        public decimal? PREMIO29 { get; set; }
        public decimal? PREMIO30 { get; set; }
        public decimal? PREMIO31 { get; set; }
        public decimal? PREMIO32 { get; set; }
        public decimal? PREMIO33 { get; set; }
        public decimal? PREMIO34 { get; set; }
        public decimal? PREMIO35 { get; set; }
        public decimal? PREMIO36 { get; set; }
        public decimal? PREMIO37 { get; set; }
        public decimal? PREMIO38 { get; set; }
        public decimal? PREMIO39 { get; set; }
        public decimal? PREMIO40 { get; set; }
        public decimal? PREMIO41 { get; set; }
        public decimal? PREMIO42 { get; set; }
        public decimal? PREMIO43 { get; set; }
        public decimal? PREMIO44 { get; set; }
        public decimal? PREMIO45 { get; set; }
        public decimal? PREMIO46 { get; set; }
        public decimal? PREMIO47 { get; set; }
        public decimal? PREMIO48 { get; set; }
        public decimal? PREMIO49 { get; set; }
        public decimal? PREMIO50 { get; set; }
        public decimal? PREMIO51 { get; set; }
        public decimal? PREMIO52 { get; set; }
        public decimal? PREMIO53 { get; set; }
        public decimal? PREMIO54 { get; set; }
        public decimal? PREMIO55 { get; set; }
        public decimal? PREMIO56 { get; set; }
        public decimal? PREMIO57 { get; set; }
        public decimal? PREMIO58 { get; set; }
        public decimal? PREMIO59 { get; set; }
        public decimal? PREMIO60 { get; set; }
        public decimal? PREMIO61 { get; set; }
        public decimal? PREMIO62 { get; set; }
        public decimal? PREMIO63 { get; set; }
        public decimal? PREMIO64 { get; set; }
        public decimal? PREMIO65 { get; set; }
        public decimal? PREMIO66 { get; set; }
        public decimal? PREMIO67 { get; set; }
        public decimal? PREMIO68 { get; set; }
        public decimal? PREMIO69 { get; set; }
        public decimal? PREMIO70 { get; set; }
        public decimal? PREMIO71 { get; set; }
        public decimal? PREMIO72 { get; set; }
        public decimal? PREMIO73 { get; set; }
        public decimal? PREMIO74 { get; set; }
        public decimal? PREMIO75 { get; set; }
        public decimal? PREMIO76 { get; set; }
        public decimal? PREMIO77 { get; set; }
        public decimal? PREMIO78 { get; set; }
        public decimal? PREMIO79 { get; set; }
        public decimal? PREMIO80 { get; set; }
        public decimal? PREMIO81 { get; set; }
        public decimal? PREMIO82 { get; set; }
        public decimal? PREMIO83 { get; set; }
        public decimal? PREMIO84 { get; set; }
        public decimal? PREMIO85 { get; set; }
        public decimal? PREMIO86 { get; set; }
        public decimal? PREMIO87 { get; set; }
        public decimal? PREMIO88 { get; set; }
        public decimal? PREMIO89 { get; set; }
        public decimal? PREMIO90 { get; set; }
        public decimal? PREMIO91 { get; set; }
        public decimal? PREMIO92 { get; set; }
        public decimal? PREMIO93 { get; set; }
        public decimal? PREMIO94 { get; set; }
        public decimal? PREMIO95 { get; set; }
        public decimal? PREMIO96 { get; set; }
        public decimal? PREMIO97 { get; set; }
        public decimal? PREMIO98 { get; set; }
        public decimal? PREMIO99 { get; set; }
    }
}