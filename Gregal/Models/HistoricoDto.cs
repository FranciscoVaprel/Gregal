using System;

namespace Gregal.Models {
    public class HistoricoDTO {
        // Identificación
        public int CodSala { get; set; }
        public int CodConc { get; set; }
        public string FechaSesion { get; set; } = string.Empty;
        public int PartidaDia { get; set; }
        public DateTime? Fecha { get; set; }
        public int? PartidaAnual { get; set; }
        public int? NumDecimales { get; set; }
        public int? Tipo { get; set; }

        // Tiempos (Cambiados a string para evitar errores de conversión con TimeSpan)
        public string? HoraCierre { get; set; }
        public string? HoraVenta { get; set; }
        public string? HoraSorteo { get; set; }
        public string? HoraFin { get; set; }
        public string? HoraCierreH { get; set; }
        public string? FechaSesionMes { get; set; }

        // Datos Económicos
        public decimal Precio { get; set; }
        public decimal Venta { get; set; }
        public decimal Caja { get; set; }
        public decimal Tasa { get; set; }
        public decimal Beneficio { get; set; }
        public decimal? CajaI { get; set; }
        public decimal? CajaAI { get; set; }
        public decimal? Acumulados { get; set; }

        // Series y Cartones
        public string? Letra1 { get; set; }
        public int? Serie1 { get; set; }
        public int? Del1 { get; set; }
        public int? Al1 { get; set; }
        public string? Letra2 { get; set; }
        public int? Serie2 { get; set; }
        public int? Del2 { get; set; }
        public int? Al2 { get; set; }
        public int Vendidos { get; set; }
        public int? LargoSerie { get; set; }

        public string? Letra1Vir { get; set; }
        public int? Serie1Vir { get; set; }
        public int? Del1Vir { get; set; }
        public int? Al1Vir { get; set; }
        public string? Letra2Vir { get; set; }
        public int? Serie2Vir { get; set; }
        public int? Del2Vir { get; set; }
        public int? Al2Vir { get; set; }
        public int? VendidosVir { get; set; }

        // Datos del Juego
        public int? Extracciones { get; set; }
        public string? GuiaBolas { get; set; }
        public string? Reclamacion { get; set; }
        public string? Bolas { get; set; }
        public int? BolaLinea { get; set; }
        public int? NumLineas { get; set; }
        public string? HoraLinea { get; set; }
        public string? CartLinea { get; set; } // Antes era int?
        public string? CartBingo { get; set; } // Antes era int?
        public int? BolaBingo { get; set; }
        public int? NumBingos { get; set; }
        public string? HoraBingo { get; set; }        

        // Premios
        public decimal?[] Premios { get; set; } = new decimal?[99];
    }
}