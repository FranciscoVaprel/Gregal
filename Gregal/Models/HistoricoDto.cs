using System;
using System.Text.Json.Serialization;

namespace Gregal.Models {
    /// <summary>
    /// Objeto de Transferencia de Datos (DTO) para la tabla HISTORICO.
    /// Representa exactamente la estructura JSON que el cliente envía.
    /// </summary>
    public class HistoricoDTO {
        // --- Identificadores Clave ---
        [JsonPropertyName("codSala")]
        public int CodSala { get; set; }

        [JsonPropertyName("codConc")]
        public int CodConc { get; set; }

        [JsonPropertyName("fechaSesion")]
        public string FechaSesion { get; set; } = string.Empty; // Formato "yyyy-MM-dd"

        [JsonPropertyName("partidaDia")]
        public int PartidaDia { get; set; }

        // --- Información de Tiempos y Tipos ---
        [JsonPropertyName("fecha")]
        public DateTime? Fecha { get; set; } // Fecha y hora real del evento

        [JsonPropertyName("partidaAnual")]
        public int? PartidaAnual { get; set; }

        [JsonPropertyName("numDecimales")]
        public int NumDecimales { get; set; } = 2;

        [JsonPropertyName("tipo")]
        public int Tipo { get; set; } // Tipo de partida (Normal, Especial, etc.)

        [JsonPropertyName("horaVenta")]
        public string? HoraVenta { get; set; } // Formato "HH:mm:ss"

        [JsonPropertyName("horaCierre")]
        public string? HoraCierre { get; set; }

        // --- Datos Económicos ---
        [JsonPropertyName("precio")]
        public decimal Precio { get; set; }

        [JsonPropertyName("vendidos")]
        public int Vendidos { get; set; }

        [JsonPropertyName("venta")]
        public decimal Venta { get; set; } // Importe total de cartones vendidos

        [JsonPropertyName("caja")]
        public decimal Caja { get; set; } // Lo que queda para la sala

        [JsonPropertyName("tasa")]
        public decimal Tasa { get; set; } // Porcentaje o importe de tasa fiscal

        [JsonPropertyName("beneficio")]
        public decimal Beneficio { get; set; }

        // --- Detalles de Serie ---
        [JsonPropertyName("letra1")]
        public string? Letra1 { get; set; }

        [JsonPropertyName("serie1")]
        public int? Serie1 { get; set; }

        [JsonPropertyName("del1")]
        public int? Del1 { get; set; }

        [JsonPropertyName("al1")]
        public int? Al1 { get; set; }

        // --- Premios (Los 99 premios del protocolo) ---
        /// <summary>
        /// Array de premios. El índice suele corresponder al tipo de premio.
        /// Usamos decimal? para permitir valores nulos si un premio no se ha dado.
        /// </summary>
        [JsonPropertyName("premios")]
        public decimal?[]? Premios { get; set; }
    }
}