using System;
using System;
using System.Text.Json.Serialization;

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

        private HistoricoDTO MapEntityToDto(Historico h) {
            var dto = new HistoricoDTO {
                CodSala = h.COD_SALA,
                CodConc = h.COD_CONC,
                FechaSesion = h.FECHA_SESION.ToString("yyyy-MM-dd"),
                PartidaDia = h.PARTIDA_DIA,
                Fecha = h.FECHA,
                PartidaAnual = h.PARTIDA_ANUAL,
                NumDecimales = h.NUM_DECIMALES,
                Tipo = h.TIPO,
                // Conversión segura de TimeSpan a string
                HoraCierre = h.HORA_CIERRE?.ToString(@"hh\:mm\:ss"),
                HoraVenta = h.HORA_VENTA?.ToString(@"hh\:mm\:ss"),
                HoraSorteo = h.HORA_SORTEO?.ToString(@"hh\:mm\:ss"),
                HoraFin = h.HORA_FIN?.ToString(@"hh\:mm\:ss"),
                HoraCierreH = h.HORA_CIERRE_H?.ToString(),
                FechaSesionMes = h.FECHA_SESION_MES?.ToString(),

                // Manejo de nulos en decimales
                Precio = h.PRECIO ?? 0,
                Venta = h.VENTA ?? 0,
                Caja = h.CAJA ?? 0,
                Tasa = h.TASA ?? 0,
                Beneficio = h.BENEFICIO ?? 0,
                CajaI = h.CAJA_I,
                CajaAI = h.CAJA_A_I,
                Acumulados = h.ACUMULADOS,

                // Series y Cartones
                Letra1 = h.LETRA1,
                Serie1 = h.SERIE_1,
                Del1 = h.DEL_1,
                Al1 = h.AL_1,
                Letra2 = h.LETRA2,
                Serie2 = h.SERIE_2,
                Del2 = h.DEL_2,
                Al2 = h.AL_2,
                Vendidos = h.VENDIDOS ?? 0,
                LargoSerie = h.LARGO_SERIE,

                // Virtuales
                Letra1Vir = h.LETRA1_VIR,
                Serie1Vir = h.SERIE_1_VIR,
                Del1Vir = h.DEL_1_VIR,
                Al1Vir = h.AL_1_VIR,
                Letra2Vir = h.LETRA2_VIR,
                Serie2Vir = h.SERIE_2_VIR,
                Del2Vir = h.DEL_2_VIR,
                Al2Vir = h.AL_2_VIR,
                VendidosVir = h.VENDIDOS_VIR,

                // Datos de Juego
                Extracciones = h.EXTRACCIONES,
                GuiaBolas = h.GUIABOLAS,
                Reclamacion = h.RECLAMACION,
                Bolas = h.BOLAS,
                BolaLinea = h.BOLA_LINEA,
                NumLineas = h.NUM_LINEAS,
                HoraLinea = h.HORA_LINEA?.ToString(@"hh\:mm\:ss"),
                CartLinea = h.CART_LINEA?.ToString(),
                BolaBingo = h.BOLA_BINGO,
                NumBingos = h.NUM_BINGOS,
                HoraBingo = h.HORA_BINGO?.ToString(@"hh\:mm\:ss"),
                CartBingo = h.CART_BINGO?.ToString()
            };

            // Mapeo dinámico de los 99 premios de la entidad al array del DTO
            for (int i = 1; i <= 99; i++) {
                var prop = typeof(Historico).GetProperty($"PREMIO{i}");
                if (prop != null) {
                    dto.Premios[i - 1] = (decimal?)prop.GetValue(h);
                }
            }

            return dto;
        }
    }
}