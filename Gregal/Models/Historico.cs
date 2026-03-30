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

        // --- SERIES Y CARTONES ---
        [JsonPropertyName("letra1")] public string? LETRA1 { get; set; }
        [JsonPropertyName("serie1")] public int? SERIE_1 { get; set; }
        [JsonPropertyName("del1")] public int? DEL_1 { get; set; }
        [JsonPropertyName("al1")] public int? AL_1 { get; set; }
        [JsonPropertyName("letra2")] public string? LETRA2 { get; set; }
        [JsonPropertyName("serie2")] public int? SERIE_2 { get; set; }
        [JsonPropertyName("del2")] public int? DEL_2 { get; set; }
        [JsonPropertyName("al2")] public int? AL_2 { get; set; }
        [JsonPropertyName("vendidos")] public int? VENDIDOS { get; set; }
        [JsonPropertyName("largoSerie")] public int? LARGO_SERIE { get; set; }

        [JsonPropertyName("letra1Vir")] public string? LETRA1_VIR { get; set; }
        [JsonPropertyName("serie1Vir")] public int? SERIE_1_VIR { get; set; }
        [JsonPropertyName("del1Vir")] public int? DEL_1_VIR { get; set; }
        [JsonPropertyName("al1Vir")] public int? AL_1_VIR { get; set; }
        [JsonPropertyName("letra2Vir")] public string? LETRA2_VIR { get; set; }
        [JsonPropertyName("serie2Vir")] public int? SERIE_2_VIR { get; set; }
        [JsonPropertyName("del2Vir")] public int? DEL_2_VIR { get; set; }
        [JsonPropertyName("al2Vir")] public int? AL_2_VIR { get; set; }
        [JsonPropertyName("vendidosVir")] public int? VENDIDOS_VIR { get; set; }

        // --- DATOS DE JUEGO ---
        [JsonPropertyName("extracciones")] public int? EXTRACCIONES { get; set; }
        [JsonPropertyName("guiabolas")] public string? GUIABOLAS { get; set; }
        [JsonPropertyName("reclamacion")] public string? RECLAMACION { get; set; }
        [JsonPropertyName("bolas")] public string? BOLAS { get; set; }
        [JsonPropertyName("bolaLinea")] public int? BOLA_LINEA { get; set; }
        [JsonPropertyName("numLineas")] public int? NUM_LINEAS { get; set; }
        [JsonPropertyName("horaLinea")] public TimeSpan? HORA_LINEA { get; set; }
        [JsonPropertyName("cartLinea")] public string? CART_LINEA { get; set; }
        [JsonPropertyName("bolaBingo")] public int? BOLA_BINGO { get; set; }
        [JsonPropertyName("numBingos")] public int? NUM_BINGOS { get; set; }
        [JsonPropertyName("horaBingo")] public TimeSpan? HORA_BINGO { get; set; }
        [JsonPropertyName("cartBingo")] public string? CART_BINGO { get; set; }
        [JsonPropertyName("fechaSesionMes")] public int? FECHA_SESION_MES { get; set; }

        // --- LÓGICA DEL ARRAY DE PREMIOS ---
        [JsonPropertyName("premios")]
        public decimal?[]? PREMIOS_ARRAY
        {
            set
            {
                if (value == null) return;
                // Mapeo automático del array a las columnas individuales de la tabla
                if (value.Length >= 1) PREMIO1 = value[0];
                if (value.Length >= 2) PREMIO2 = value[1];
                if (value.Length >= 3) PREMIO3 = value[2];
                if (value.Length >= 4) PREMIO4 = value[3];
                if (value.Length >= 5) PREMIO5 = value[4];
                if (value.Length >= 6) PREMIO6 = value[5];
                if (value.Length >= 7) PREMIO7 = value[6];
                if (value.Length >= 8) PREMIO8 = value[7];
                if (value.Length >= 9) PREMIO9 = value[8];
                if (value.Length >= 10) PREMIO10 = value[9];
                // ... puedes añadir más si la lógica de negocio lo requiere
            }
        }

        // --- COLUMNAS INDIVIDUALES DE PREMIOS (Mapeadas a la DB) ---
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