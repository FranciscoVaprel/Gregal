using Gregal.Models;
using System;
using System.Globalization;

namespace Gregal.Parsing {
    public class CsvGregalParser : IParser {
        public Historico Parse(string data) {
            // Separamos por punto y coma
            var cols = data.Trim().Split(';');

            // Validación mínima de columnas (mínimo las PKs y algunos datos)
            if (cols.Length < 4) throw new Exception("Trama CSV incompleta");

            var h = new Historico {
                CodSala = int.Parse(cols[0]),
                CodConc = int.Parse(cols[1]),
                FechaSesion = DateTime.ParseExact(cols[2], "yyyy-MM-dd", CultureInfo.InvariantCulture),
                PartidaDia = int.Parse(cols[3]),
                Venta = GetDecimal(cols, 4),
                Caja = GetDecimal(cols, 5),
                Bolas = cols.Length > 6 ? cols[6] : null
            };

            // Mapeo automático de los 99 premios (si existen en la trama)
            for (int i = 1; i <= 99; i++) {
                int colIndex = 6 + i; // Los premios empiezan en la columna 7 (índice 7)
                if (cols.Length > colIndex) {
                    decimal valorPremio = GetDecimal(cols, colIndex);
                    var prop = typeof(Historico).GetProperty($"Premio{i}");
                    prop?.SetValue(h, valorPremio);
                }
            }

            return h;
        }

        private decimal GetDecimal(string[] cols, int index) {
            if (index >= cols.Length || string.IsNullOrWhiteSpace(cols[index])) return 0m;
            return decimal.Parse(cols[index], CultureInfo.InvariantCulture);
        }
    }
}