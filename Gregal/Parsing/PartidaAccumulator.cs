using System.Globalization;
using System.Text;
using Gregal.Config;
using Gregal.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gregal.Parsing;

// Acumula el estado de UNA partida a medida que llegan eventos del protocolo
// *CODIGO<valor># (ver TagEventParser), y devuelve el HistoricoDTO completo
// al recibir el evento F (fin de partida), reiniciando después su estado para
// la siguiente. Instancia con estado: una por conexión/puerto serie, nunca
// compartida entre partidas distintas en paralelo.
//
// El mapeo código -> campo de HistoricoDTO es una inferencia a partir de las
// constantes de Comunicaciones.cs (software de pantallas PidFullHD), no una
// especificación confirmada: se deja centralizado en Aplicar() para que sea
// fácil de corregir cuando se tenga más información real.
public class PartidaAccumulator {
    private readonly ILogger<PartidaAccumulator> _logger;
    private readonly IOptionsMonitor<TagProtocolSettings> _settings;

    private HistoricoDTO _dto = new();
    private readonly StringBuilder _bolas = new();
    private int _extracciones;

    // Qué números de bola (1-90) ya sabemos que han salido, vengan de un *E#
    // en tiempo real o de un evento 'e' (estado de partida). Hace falta aparte
    // de _bolas/_extracciones para poder fusionar un snapshot 'e' sin duplicar
    // ni perder el orden real ya conocido.
    private readonly bool[] _bolasConocidas = new bool[90];

    // Se pone a true si alguna bola se incorporó vía un evento 'e' en vez de
    // por su *E# real — es decir, si el receptor se enganchó a mitad de
    // partida. Se dejará constancia de esto en HistoricoDTO.Reclamacion.
    private bool _recuperadaParcialmente;

    // Último canto de línea/bingo "tentativo": D/H lo fijan de inmediato, pero
    // si el siguiente evento es u/h para el mismo cartón, se revierte (canto
    // incorrecto, la partida sigue).
    private int? _lineaTentativa;
    private int? _bingoTentativo;

    // Si el protocolo no emite el código T, no hay forma de saber el número
    // de partida del día; se numera de forma correlativa dentro de esta
    // ejecución (se reinicia si el servicio se reinicia) para no bloquear el
    // guardado solo por este campo. No se reinicia en Reset(): debe avanzar
    // de una partida a la siguiente.
    private int _siguientePartidaDia = 1;

    public PartidaAccumulator(ILogger<PartidaAccumulator> logger, IOptionsMonitor<TagProtocolSettings> settings) {
        _logger = logger;
        _settings = settings;
    }

    public HistoricoDTO? Feed(TagEvent evt) {
        if (evt.Code == 'F') {
            var completada = Finalizar();
            Reset();
            return completada;
        }

        Aplicar(evt);
        return null;
    }

    private void Aplicar(TagEvent evt) {
        switch (evt.Code) {
            case 'C':
                _dto.Precio = ParseDecimal(evt.Value);
                break;
            case 'Q':
                _dto.Caja = ParseDecimal(evt.Value);
                break;
            case 'V':
                _dto.Vendidos = ParseInt(evt.Value);
                break;
            case 'T':
                _dto.PartidaDia = ParseInt(evt.Value);
                break;
            case 'E': {
                int bola = ParseInt(evt.Value);
                _extracciones++;
                if (_bolas.Length > 0) _bolas.Append(',');
                _bolas.Append(evt.Value);
                if (bola is >= 1 and <= 90) _bolasConocidas[bola - 1] = true;
                _dto.Extracciones = _extracciones;
                _dto.Bolas = _bolas.ToString();
                break;
            }
            case 'e':
                AplicarEstadoPartida(evt.Value);
                break;
            case 'D':
                AplicarCanto(evt.Value, ref _lineaTentativa, esLinea: true);
                break;
            case 'u':
                RevertirCantoIncorrecto(evt.Value, ref _lineaTentativa, esLinea: true);
                break;
            case 'H':
                AplicarCanto(evt.Value, ref _bingoTentativo, esLinea: false);
                break;
            case 'h':
                RevertirCantoIncorrecto(evt.Value, ref _bingoTentativo, esLinea: false);
                break;
            default:
                AplicarSiEsPremioMapeado(evt);
                break;
        }
    }

    private void AplicarCanto(string valor, ref int? tentativo, bool esLinea) {
        int carton = ParseInt(valor);
        if (carton <= 0) return;

        if (esLinea) {
            _dto.CartLinea = carton.ToString(CultureInfo.InvariantCulture);
            _dto.BolaLinea = _extracciones;
            _dto.NumLineas = (_dto.NumLineas ?? 0) + 1;
        }
        else {
            _dto.CartBingo = carton.ToString(CultureInfo.InvariantCulture);
            _dto.BolaBingo = _extracciones;
            _dto.NumBingos = (_dto.NumBingos ?? 0) + 1;
        }
        tentativo = carton;
    }

    private void RevertirCantoIncorrecto(string valor, ref int? tentativo, bool esLinea) {
        int carton = ParseInt(valor);
        if (tentativo == null || tentativo.Value != carton) return;

        if (esLinea) {
            _dto.CartLinea = null;
            _dto.BolaLinea = null;
            _dto.NumLineas = Math.Max(0, (_dto.NumLineas ?? 1) - 1);
        }
        else {
            _dto.CartBingo = null;
            _dto.BolaBingo = null;
            _dto.NumBingos = Math.Max(0, (_dto.NumBingos ?? 1) - 1);
        }
        tentativo = null;
    }

    // Evento 'e': snapshot binario del estado actual (ver EstadoPartidaDecoder)
    // — pensado para que un receptor que se conecta a mitad de partida sepa
    // qué bolas han salido ya. Solo incorpora las que NO conociéramos todavía
    // (por su *E# real), para no tocar el orden real ya registrado; las
    // nuevas se añaden en orden numérico ascendente, que es lo único que este
    // snapshot puede dar (no dice en qué orden salieron).
    private void AplicarEstadoPartida(string payload) {
        var resultado = EstadoPartidaDecoder.Decode(payload);
        if (resultado == null) {
            _logger.LogWarning("Evento de estado de partida ('e') con checksum inválido o formato irreconocible; se ignora.");
            return;
        }

        bool huboBolasNuevas = false;
        for (int numero = 1; numero <= 90; numero++) {
            if (!resultado.Value.BolasExtraidas[numero - 1]) continue;
            if (_bolasConocidas[numero - 1]) continue; // ya la teníamos por su *E# real

            _bolasConocidas[numero - 1] = true;
            _extracciones++;
            if (_bolas.Length > 0) _bolas.Append(',');
            _bolas.Append(numero.ToString(CultureInfo.InvariantCulture));
            huboBolasNuevas = true;
        }

        if (huboBolasNuevas) {
            _dto.Extracciones = _extracciones;
            _dto.Bolas = _bolas.ToString();
            _recuperadaParcialmente = true;
            _logger.LogInformation("Partida (protocolo tag) enganchada a mitad vía evento 'e': se recuperaron bolas anteriores a la conexión (orden aproximado).");
        }
    }

    private void AplicarSiEsPremioMapeado(TagEvent evt) {
        var map = _settings.CurrentValue.PremioCodeMap;
        string code = evt.Code.ToString();

        if (map.TryGetValue(code, out int indice) && indice >= 0 && indice < _dto.Premios.Length) {
            _dto.Premios[indice] = ParseDecimal(evt.Value);
        }
        else {
            // Nivel Information a propósito: en un despliegue nuevo interesa
            // ver en el log, sin tocar configuración, qué códigos emite el
            // hardware real que no encajan con el mapeo asumido.
            _logger.LogInformation("Código de evento (protocolo tag) no mapeado: {code} (valor '{value}')", evt.Code, evt.Value);
        }
    }

    private HistoricoDTO Finalizar() {
        var settings = _settings.CurrentValue;
        _dto.CodSala = settings.DefaultCodSala;
        _dto.CodConc = settings.DefaultCodConc;

        // FechaSesion: el protocolo no la lleva; se asume la fecha de hoy en
        // el momento de cerrar la partida.
        if (string.IsNullOrWhiteSpace(_dto.FechaSesion)) {
            _dto.FechaSesion = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        // PartidaDia: si no llegó por el código T, se usa el correlativo interno.
        if (_dto.PartidaDia <= 0) {
            _dto.PartidaDia = _siguientePartidaDia;
        }
        _siguientePartidaDia++;

        if (_recuperadaParcialmente) {
            // RECLAMACION es VARCHAR(64) en Firebird (HISTORICO) — un texto más
            // largo provoca "string right truncation" y pierde la partida entera
            // (cae al fichero de fallback). El detalle completo queda en el log
            // ("enganchada a mitad vía evento 'e'"), aquí solo cabe una marca corta.
            const string nota = "PARTIDA RECUPERADA A MEDIAS (orden de bolas aproximado)";
            const int maxReclamacion = 64;
            string combinado = string.IsNullOrWhiteSpace(_dto.Reclamacion) ? nota : _dto.Reclamacion + " | " + nota;
            _dto.Reclamacion = combinado.Length > maxReclamacion ? combinado[..maxReclamacion] : combinado;
        }

        return _dto;
    }

    private void Reset() {
        _dto = new HistoricoDTO();
        _bolas.Clear();
        _extracciones = 0;
        _lineaTentativa = null;
        _bingoTentativo = null;
        Array.Clear(_bolasConocidas);
        _recuperadaParcialmente = false;
    }

    private static decimal ParseDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result) ? result : 0m;

    private static int ParseInt(string value) =>
        int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out int result) ? result : 0;
}
