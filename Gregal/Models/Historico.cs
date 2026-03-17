using System;

namespace TuProyecto.Models {
    public class Historico {
        // --- CLAVE PRIMARIA COMPUESTA ---
        public int CodSala { get; set; }
        public int CodConc { get; set; }
        public DateTime FechaSesion { get; set; }
        public int PartidaDia { get; set; }

        // --- CAMPOS DE CONTROL Y TIEMPO ---
        public DateTime? Fecha { get; set; } // TIMESTAMP
        public int? PartidaAnual { get; set; }
        public int? NumDecimales { get; set; }
        public int? Tipo { get; set; }
        public TimeSpan? HoraCierre { get; set; }
        public TimeSpan? HoraVenta { get; set; }
        public TimeSpan? HoraSorteo { get; set; }
        public TimeSpan? HoraFin { get; set; }

        // --- DATOS ECONÓMICOS ---
        public decimal? Precio { get; set; }
        public decimal? Venta { get; set; }
        public decimal? Caja { get; set; }
        public decimal? Tasa { get; set; }
        public decimal? Beneficio { get; set; }
        public decimal? CajaI { get; set; }
        public decimal? CajaAI { get; set; }
        public decimal? Acumulados { get; set; }

        // --- SERIES Y CARTONES ---
        public string Letra1 { get; set; }
        public int? Serie1 { get; set; }
        public int? Del1 { get; set; }
        public int? Al1 { get; set; }
        public int? Vendidos { get; set; }

        // --- DATOS DE JUEGO ---
        public string GuiaBolas { get; set; }
        public string Reclamacion { get; set; }
        public string Bolas { get; set; }
        public int? BolaLinea { get; set; }
        public int? NumLineas { get; set; }
        public string CartLinea { get; set; }
        public int? BolaBingo { get; set; }
        public int? NumBingos { get; set; }
        public string CartBingo { get; set; }

        // --- PREMIOS (Manejo de array para los 99 campos) ---
        // En la base de datos son PREMIO1, PREMIO2... PREMIO99
        // Para no crear 99 propiedades, puedes usar un array o listado
        // aunque para mapeadores simples como Dapper, a veces es necesario declararlos:

        public decimal? Premio1 { get; set; }
        public decimal? Premio2 { get; set; }
        public decimal? Premio3 { get; set; }
        // ... (puedes seguir la serie según necesites)
        public decimal? Premio99 { get; set; }

        // --- CAMPOS ADICIONALES ---
        public int? HoraCierreH { get; set; }
        public int? FechaSesionMes { get; set; }
    }
}