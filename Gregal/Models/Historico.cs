using System;
using System.Text.Json.Serialization;

namespace Gregal.Models {
    public class Historico {
        // --- CLAVE PRIMARIA COMPUESTA ---
        [JsonPropertyName("codSala")]
        public int COD_SALA { get; set; }

        [JsonPropertyName("codConc")]
        public int COD_CONC { get; set; }

        [JsonPropertyName("fechaSesion")]
        public DateTime FECHA_SESION { get; set; }

        [JsonPropertyName("partidaDia")]
        public int PARTIDA_DIA { get; set; }

        // --- CAMPOS DE CONTROL Y TIEMPO ---
        [JsonPropertyName("fecha")]
        public DateTime? FECHA { get; set; }

        [JsonPropertyName("partidaAnual")]
        public int? PARTIDA_ANUAL { get; set; }

        [JsonPropertyName("numDecimales")]
        public int? NUM_DECIMALES { get; set; }

        [JsonPropertyName("tipo")]
        public int? TIPO { get; set; }

        [JsonPropertyName("horaCierre")]
        public TimeSpan? HORA_CIERRE { get; set; }

        [JsonPropertyName("horaVenta")]
        public TimeSpan? HORA_VENTA { get; set; }

        [JsonPropertyName("horaSorteo")]
        public TimeSpan? HORA_SORTEO { get; set; }

        [JsonPropertyName("horaFin")]
        public TimeSpan? HORA_FIN { get; set; }

        [JsonPropertyName("horaCierreH")]
        public int? HORA_CIERRE_H { get; set; }

        // --- DATOS ECONÓMICOS ---
        [JsonPropertyName("precio")]
        public decimal? PRECIO { get; set; }

        [JsonPropertyName("venta")]
        public decimal? VENTA { get; set; }

        [JsonPropertyName("caja")]
        public decimal? CAJA { get; set; }

        [JsonPropertyName("tasa")]
        public decimal? TASA { get; set; }

        [JsonPropertyName("beneficio")]
        public decimal? BENEFICIO { get; set; }

        [JsonPropertyName("cajaI")]
        public decimal? CAJA_I { get; set; }

        [JsonPropertyName("cajaAI")]
        public decimal? CAJA_A_I { get; set; }

        [JsonPropertyName("acumulados")]
        public decimal? ACUMULADOS { get; set; }

        // --- SERIES Y CARTONES (Físicos) ---
        [JsonPropertyName("letra1")]
        public string? LETRA1 { get; set; }

        [JsonPropertyName("serie1")]
        public int? SERIE_1 { get; set; }

        [JsonPropertyName("del1")]
        public int? DEL_1 { get; set; }

        [JsonPropertyName("al1")]
        public int? AL_1 { get; set; }

        [JsonPropertyName("letra2")]
        public string? LETRA2 { get; set; }

        [JsonPropertyName("serie2")]
        public int? SERIE_2 { get; set; }

        [JsonPropertyName("del2")]
        public int? DEL_2 { get; set; }

        [JsonPropertyName("al2")]
        public int? AL_2 { get; set; }

        [JsonPropertyName("vendidos")]
        public int? VENDIDOS { get; set; }

        [JsonPropertyName("largoSerie")]
        public int? LARGO_SERIE { get; set; }

        // --- SERIES Y CARTONES (Virtuales) ---
        [JsonPropertyName("letra1Vir")]
        public string? LETRA1_VIR { get; set; }

        [JsonPropertyName("serie1Vir")]
        public int? SERIE_1_VIR { get; set; }

        [JsonPropertyName("del1Vir")]
        public int? DEL_1_VIR { get; set; }

        [JsonPropertyName("al1Vir")]
        public int? AL_1_VIR { get; set; }

        [JsonPropertyName("letra2Vir")]
        public string? LETRA2_VIR { get; set; }

        [JsonPropertyName("serie2Vir")]
        public int? SERIE_2_VIR { get; set; }

        [JsonPropertyName("del2Vir")]
        public int? DEL_2_VIR { get; set; }

        [JsonPropertyName("al2Vir")]
        public int? AL_2_VIR { get; set; }

        [JsonPropertyName("vendidosVir")]
        public int? VENDIDOS_VIR { get; set; }

        // --- DATOS DE JUEGO ---
        [JsonPropertyName("extracciones")]
        public int? EXTRACCIONES { get; set; }

        [JsonPropertyName("guiabolas")]
        public string? GUIABOLAS { get; set; }

        [JsonPropertyName("reclamacion")]
        public string? RECLAMACION { get; set; }

        [JsonPropertyName("bolas")]
        public string? BOLAS { get; set; }

        [JsonPropertyName("bolaLinea")]
        public int? BOLA_LINEA { get; set; }

        [JsonPropertyName("numLineas")]
        public int? NUM_LINEAS { get; set; }

        [JsonPropertyName("horaLinea")]
        public TimeSpan? HORA_LINEA { get; set; }

        [JsonPropertyName("cartLinea")]
        public string? CART_LINEA { get; set; }

        [JsonPropertyName("bolaBingo")]
        public int? BOLA_BINGO { get; set; }

        [JsonPropertyName("numBingos")]
        public int? NUM_BINGOS { get; set; }

        [JsonPropertyName("horaBingo")]
        public TimeSpan? HORA_BINGO { get; set; }

        [JsonPropertyName("cartBingo")]
        public string? CART_BINGO { get; set; }

        [JsonPropertyName("fechaSesionMes")]
        public int? FECHA_SESION_MES { get; set; }

        // --- PREMIOS (1-99) ---
        [JsonPropertyName("premio1")] public decimal? PREMIO1 { get; set; }
        [JsonPropertyName("premio2")] public decimal? PREMIO2 { get; set; }
        [JsonPropertyName("premio3")] public decimal? PREMIO3 { get; set; }
        [JsonPropertyName("premio4")] public decimal? PREMIO4 { get; set; }
        [JsonPropertyName("premio5")] public decimal? PREMIO5 { get; set; }
        [JsonPropertyName("premio6")] public decimal? PREMIO6 { get; set; }
        [JsonPropertyName("premio7")] public decimal? PREMIO7 { get; set; }
        [JsonPropertyName("premio8")] public decimal? PREMIO8 { get; set; }
        [JsonPropertyName("premio9")] public decimal? PREMIO9 { get; set; }
        [JsonPropertyName("premio10")] public decimal? PREMIO10 { get; set; }
        [JsonPropertyName("premio11")] public decimal? PREMIO11 { get; set; }
        [JsonPropertyName("premio12")] public decimal? PREMIO12 { get; set; }
        [JsonPropertyName("premio13")] public decimal? PREMIO13 { get; set; }
        [JsonPropertyName("premio14")] public decimal? PREMIO14 { get; set; }
        [JsonPropertyName("premio15")] public decimal? PREMIO15 { get; set; }
        [JsonPropertyName("premio16")] public decimal? PREMIO16 { get; set; }
        [JsonPropertyName("premio17")] public decimal? PREMIO17 { get; set; }
        [JsonPropertyName("premio18")] public decimal? PREMIO18 { get; set; }
        [JsonPropertyName("premio19")] public decimal? PREMIO19 { get; set; }
        [JsonPropertyName("premio20")] public decimal? PREMIO20 { get; set; }
        [JsonPropertyName("premio21")] public decimal? PREMIO21 { get; set; }
        [JsonPropertyName("premio22")] public decimal? PREMIO22 { get; set; }
        [JsonPropertyName("premio23")] public decimal? PREMIO23 { get; set; }
        [JsonPropertyName("premio24")] public decimal? PREMIO24 { get; set; }
        [JsonPropertyName("premio25")] public decimal? PREMIO25 { get; set; }
        [JsonPropertyName("premio26")] public decimal? PREMIO26 { get; set; }
        [JsonPropertyName("premio27")] public decimal? PREMIO27 { get; set; }
        [JsonPropertyName("premio28")] public decimal? PREMIO28 { get; set; }
        [JsonPropertyName("premio29")] public decimal? PREMIO29 { get; set; }
        [JsonPropertyName("premio30")] public decimal? PREMIO30 { get; set; }
        [JsonPropertyName("premio31")] public decimal? PREMIO31 { get; set; }
        [JsonPropertyName("premio32")] public decimal? PREMIO32 { get; set; }
        [JsonPropertyName("premio33")] public decimal? PREMIO33 { get; set; }
        [JsonPropertyName("premio34")] public decimal? PREMIO34 { get; set; }
        [JsonPropertyName("premio35")] public decimal? PREMIO35 { get; set; }
        [JsonPropertyName("premio36")] public decimal? PREMIO36 { get; set; }
        [JsonPropertyName("premio37")] public decimal? PREMIO37 { get; set; }
        [JsonPropertyName("premio38")] public decimal? PREMIO38 { get; set; }
        [JsonPropertyName("premio39")] public decimal? PREMIO39 { get; set; }
        [JsonPropertyName("premio40")] public decimal? PREMIO40 { get; set; }
        [JsonPropertyName("premio41")] public decimal? PREMIO41 { get; set; }
        [JsonPropertyName("premio42")] public decimal? PREMIO42 { get; set; }
        [JsonPropertyName("premio43")] public decimal? PREMIO43 { get; set; }
        [JsonPropertyName("premio44")] public decimal? PREMIO44 { get; set; }
        [JsonPropertyName("premio45")] public decimal? PREMIO45 { get; set; }
        [JsonPropertyName("premio46")] public decimal? PREMIO46 { get; set; }
        [JsonPropertyName("premio47")] public decimal? PREMIO47 { get; set; }
        [JsonPropertyName("premio48")] public decimal? PREMIO48 { get; set; }
        [JsonPropertyName("premio49")] public decimal? PREMIO49 { get; set; }
        [JsonPropertyName("premio50")] public decimal? PREMIO50 { get; set; }
        [JsonPropertyName("premio51")] public decimal? PREMIO51 { get; set; }
        [JsonPropertyName("premio52")] public decimal? PREMIO52 { get; set; }
        [JsonPropertyName("premio53")] public decimal? PREMIO53 { get; set; }
        [JsonPropertyName("premio54")] public decimal? PREMIO54 { get; set; }
        [JsonPropertyName("premio55")] public decimal? PREMIO55 { get; set; }
        [JsonPropertyName("premio56")] public decimal? PREMIO56 { get; set; }
        [JsonPropertyName("premio57")] public decimal? PREMIO57 { get; set; }
        [JsonPropertyName("premio58")] public decimal? PREMIO58 { get; set; }
        [JsonPropertyName("premio59")] public decimal? PREMIO59 { get; set; }
        [JsonPropertyName("premio60")] public decimal? PREMIO60 { get; set; }
        [JsonPropertyName("premio61")] public decimal? PREMIO61 { get; set; }
        [JsonPropertyName("premio62")] public decimal? PREMIO62 { get; set; }
        [JsonPropertyName("premio63")] public decimal? PREMIO63 { get; set; }
        [JsonPropertyName("premio64")] public decimal? PREMIO64 { get; set; }
        [JsonPropertyName("premio65")] public decimal? PREMIO65 { get; set; }
        [JsonPropertyName("premio66")] public decimal? PREMIO66 { get; set; }
        [JsonPropertyName("premio67")] public decimal? PREMIO67 { get; set; }
        [JsonPropertyName("premio68")] public decimal? PREMIO68 { get; set; }
        [JsonPropertyName("premio69")] public decimal? PREMIO69 { get; set; }
        [JsonPropertyName("premio70")] public decimal? PREMIO70 { get; set; }
        [JsonPropertyName("premio71")] public decimal? PREMIO71 { get; set; }
        [JsonPropertyName("premio72")] public decimal? PREMIO72 { get; set; }
        [JsonPropertyName("premio73")] public decimal? PREMIO73 { get; set; }
        [JsonPropertyName("premio74")] public decimal? PREMIO74 { get; set; }
        [JsonPropertyName("premio75")] public decimal? PREMIO75 { get; set; }
        [JsonPropertyName("premio76")] public decimal? PREMIO76 { get; set; }
        [JsonPropertyName("premio77")] public decimal? PREMIO77 { get; set; }
        [JsonPropertyName("premio78")] public decimal? PREMIO78 { get; set; }
        [JsonPropertyName("premio79")] public decimal? PREMIO79 { get; set; }
        [JsonPropertyName("premio80")] public decimal? PREMIO80 { get; set; }
        [JsonPropertyName("premio81")] public decimal? PREMIO81 { get; set; }
        [JsonPropertyName("premio82")] public decimal? PREMIO82 { get; set; }
        [JsonPropertyName("premio83")] public decimal? PREMIO83 { get; set; }
        [JsonPropertyName("premio84")] public decimal? PREMIO84 { get; set; }
        [JsonPropertyName("premio85")] public decimal? PREMIO85 { get; set; }
        [JsonPropertyName("premio86")] public decimal? PREMIO86 { get; set; }
        [JsonPropertyName("premio87")] public decimal? PREMIO87 { get; set; }
        [JsonPropertyName("premio88")] public decimal? PREMIO88 { get; set; }
        [JsonPropertyName("premio89")] public decimal? PREMIO89 { get; set; }
        [JsonPropertyName("premio90")] public decimal? PREMIO90 { get; set; }
        [JsonPropertyName("premio91")] public decimal? PREMIO91 { get; set; }
        [JsonPropertyName("premio92")] public decimal? PREMIO92 { get; set; }
        [JsonPropertyName("premio93")] public decimal? PREMIO93 { get; set; }
        [JsonPropertyName("premio94")] public decimal? PREMIO94 { get; set; }
        [JsonPropertyName("premio95")] public decimal? PREMIO95 { get; set; }
        [JsonPropertyName("premio96")] public decimal? PREMIO96 { get; set; }
        [JsonPropertyName("premio97")] public decimal? PREMIO97 { get; set; }
        [JsonPropertyName("premio98")] public decimal? PREMIO98 { get; set; }
        [JsonPropertyName("premio99")] public decimal? PREMIO99 { get; set; }
    }
}