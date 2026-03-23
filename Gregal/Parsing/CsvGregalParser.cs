using Gregal.Models;
using System;
using System.Globalization;

namespace Gregal.Parsing {
    // Es CRITICO añadir ': IParser' para que GregalParserEngine la reconozca
    public class CsvGregalParser : IParser {
        public Historico Parse(string data) {
            var cols = data.Trim().Split(';');

            if (cols.Length < 4) throw new Exception("Trama CSV incompleta");

            var h = new Historico {
                COD_SALA = int.Parse(cols[0]),
                COD_CONC = int.Parse(cols[1]),
                FECHA_SESION = DateTime.ParseExact(cols[2], "yyyy-MM-dd", CultureInfo.InvariantCulture),
                PARTIDA_DIA = int.Parse(cols[3]),
                VENTA = GetDecimal(cols, 4),
                CAJA = GetDecimal(cols, 5),
                BOLAS = cols.Length > 6 ? cols[6] : null
            };

            for (int i = 1; i <= 99; i++) {
                int colIndex = 6 + i;
                if (cols.Length > colIndex) {
                    decimal valorPremio = GetDecimal(cols, colIndex);
                    var prop = typeof(Historico).GetProperty($"PREMIO{i}");
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