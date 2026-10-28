using System.Globalization;
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
// El mapeo código -> campo de HistoricoDTO se confirmó el 2026-10-14 contra
// una captura real de 7 partidas de la sala Metronia ("7 partidas
// Metronia.txt") y los tickets que la propia máquina imprime al final de cada
// partida (que incluyen los mismos valores ya calculados, en claro). Los
// campos K, T, L, B, C, P, U, V, X, J, p, A, M, D y H están confirmados así.
// Confirmado también por el operador de la sala (2026-10-14, segunda ronda):
// 'º' no corresponde a ningún campo de HISTORICO (se ignora a propósito, no
// por desconocimiento), y "las bolas se borran con *X#" de la primera
// explicación era un error de transcripción — el código real es 'Z', distinto
// de 'X' (que siempre es el número de serie, sin ambigüedad). 'Z' no apareció
// ni una vez en las 7 partidas capturadas (ninguna tuvo que corregir una
// bola), así que su implementación no se ha podido probar contra tráfico real.
//
// IMPORTANTE (2026-10-14, cuarta ronda): hay (al menos) DOS fabricantes de
// hardware distintos en las salas, con protocolos parecidos pero no
// idénticos — Metronia (el de arriba) y MCB ("VolcadoMCB.txt"). En MCB, A y
// M (y probablemente p) no llevan un valor fiable: aparecen constantemente
// como ruido ("*M0#*A0#*A000#...", varias veces entre cada bola) en vez de
// una vez por partida con un importe real. No es una contradicción entre las
// dos capturas, son máquinas distintas. Se decide por configuración con
// TagProtocolSettings.Fabricante (ver AplicarSiEsPremioMapeado): con MCB,
// A/M/p se ignoran en silencio aunque estén en PremioCodeMap.
public class PartidaAccumulator {
    private readonly ILogger<PartidaAccumulator> _logger;
    private readonly IOptionsMonitor<TagProtocolSettings> _settings;

    private HistoricoDTO _dto = new();

    // Bolas salidas, en el orden real de llegada (mezcla de *E# en tiempo
    // real y, si se reconecta a mitad de partida, las recuperadas vía el
    // snapshot 'e' — ver AplicarEstadoPartida). Lista en vez de
    // string/StringBuilder porque el evento Z (ver Aplicar) puede quitar una
    // bola ya registrada, no solo añadir.
    private readonly List<int> _bolasOrden = new();

    // Qué números de bola (1-90) ya sabemos que han salido, vengan de un *E#
    // en tiempo real o de un evento 'e' (estado de partida). Hace falta aparte
    // de _bolasOrden para poder fusionar un snapshot 'e' sin duplicar ni
    // perder el orden real ya conocido.
    private readonly bool[] _bolasConocidas = new bool[90];

    // Se pone a true si alguna bola se incorporó vía un evento 'e' en vez de
    // por su *E# real — es decir, si el receptor se enganchó a mitad de
    // partida. Se dejará constancia de esto en HistoricoDTO.Reclamacion.
    //
    // El evento 'e' se manda aprox. una vez por segundo DURANTE TODA la
    // partida (confirmado: "cada segundo se recibe el *f# ... o la cadena ...
    // *e......#"), no solo al reconectar — pero en la captura real siempre
    // llega justo despues de su *E# correspondiente, así que en una partida
    // normal (sin desconexión real) nunca aporta bolas "nuevas" y este flag
    // no se activa: comprobado con las 1.469 cadenas de la captura real de
    // Metronia, que coinciden todas con las bolas ya recibidas por *E#.
    private bool _recuperadaParcialmente;

    // Letra de edición del cartón (evento J, vía TagProtocolSettings.
    // SerieLetraMap), aplicada a Letra1 y, si hay empalme de serie (ver
    // AplicarSerie), también a Letra2 — confirmado con un ticket real
    // (partida 4: dos series "EB-00179689"/"EB-00179690", misma letra "EB"
    // para las dos). Se guarda aparte en vez de asignar directo a
    // HistoricoDTO porque J puede llegar antes o después de X en teoría
    // (en la captura real siempre llega después, pero no hay garantía) y así
    // no depende del orden de llegada.
    private string? _letraSerie;

    // Cartones que han cantado línea/bingo, en el orden en que llegó su *D#/
    // *H#. Puede haber más de uno en la misma partida (confirmado: partida 6
    // de la captura real tuvo dos bingos, "*H2891#" y "*H3468#", ambos
    // válidos) — HistoricoDTO.CartLinea/CartBingo los guarda como lista
    // separada por comas, igual que ya se hace con Bolas.
    private readonly List<int> _cartonesLinea = new();
    private readonly List<int> _cartonesBingo = new();

    // Último canto de línea/bingo "tentativo": D/H lo fijan de inmediato, pero
    // si el siguiente evento es u/h para el MISMO cartón, se revierte (canto
    // incorrecto, la partida sigue). Confirmado por el usuario: "u y h
    // indican que ha habido un error en la línea y en el bingo
    // respectivamente" — no apareció en la captura real porque esas 7
    // partidas no tuvieron ningún canto erróneo, pero el mecanismo es
    // correcto tal y como estaba diseñado.
    //
    // Limitación conocida y asumida: solo se seguimiento de UN canto
    // tentativo a la vez por tipo. Si llegaran dos cantos (p.ej. de línea)
    // sin confirmar/revertir antes de que llegue el siguiente, el revert solo
    // podría deshacer el más reciente. No hay evidencia real de que esto
    // pueda pasar (los cantos van de uno en uno en la captura disponible).
    private int? _lineaTentativa;
    private int? _bingoTentativo;

    // Se pone a true al recibir el primer *H# sin valor ("jugando a bingo",
    // confirmado en las dos descripciones del protocolo recibidas). Hace
    // falta para distinguir las dos formas del canto que ha descrito el
    // operador de la sala: *D<tipo><cartón># / *H<cartón># (formato
    // confirmado con tickets reales) frente a *Y<cartón># (formato más
    // reciente, un mismo código para línea y bingo): antes de este flag, un
    // *Y<cartón># es línea; después, es bingo.
    private bool _faseBingo;

    // Partida cerrada sin haber visto su *T# (la captura empezó a mitad de
    // partida: T llega solo al principio, en las dos capturas reales). No se
    // le inventa un número — el UPDATE OR INSERT machacaría una partida real
    // del día con ese número —, sino que espera a la T de la siguiente
    // partida y se guarda como la anterior (T-1). No se reinicia en Reset().
    private HistoricoDTO? _pendienteSinT;

    public PartidaAccumulator(ILogger<PartidaAccumulator> logger, IOptionsMonitor<TagProtocolSettings> settings) {
        _logger = logger;
        _settings = settings;
    }

    public HistoricoDTO? Feed(TagEvent evt) {
        switch (evt.Code) {
            case 'F':
                return CerrarPartida();

            // *f# solo llega entre partidas (comprobado en las 11 partidas de
            // la captura MCB: ninguno entre una T y su F). Si llega con una
            // partida abierta, su *F# se perdió por el camino.
            case 'f' when TieneContenido:
                _logger.LogWarning("Llega *f# (entre partidas) con una partida abierta: se perdió su *F#. Se cierra la partida.");
                return CerrarPartida();

            case 'T': {
                var pendiente = ResolverPendienteSinT(evt.Value);
                Aplicar(evt);
                return pendiente;
            }

            default:
                Aplicar(evt);
                return null;
        }
    }

    private bool TieneContenido =>
        _bolasOrden.Count > 0 || _cartonesLinea.Count > 0 || _cartonesBingo.Count > 0;

    private HistoricoDTO? CerrarPartida() {
        bool conT = _dto.PartidaDia > 0;
        bool vacia = !TieneContenido;
        var completada = Finalizar(sinT: !conT);
        Reset();

        if (conT) return completada;

        // MCB manda dos *F# al acabar cada partida (el segundo tras un *f#),
        // y un receptor que arranca entre partidas puede ver un *F# suelto.
        if (vacia) {
            _logger.LogDebug("*F# sin partida en curso (sin T ni bolas); se ignora.");
            return null;
        }

        if (_pendienteSinT != null) {
            _logger.LogWarning("Se descarta una partida sin T anterior (bolas {bolas}): llegó otra sin T antes de poder deducir su número.", _pendienteSinT.Bolas);
        }
        _pendienteSinT = completada;
        _logger.LogWarning("Partida sin evento T (la captura empezó a mitad de partida): se guardará con el número anterior a la siguiente T que llegue.");
        return null;
    }

    private HistoricoDTO? ResolverPendienteSinT(string valorT) {
        if (_pendienteSinT == null) return null;

        var pendiente = _pendienteSinT;
        _pendienteSinT = null;

        if (int.TryParse(valorT, SoloDigitos, CultureInfo.InvariantCulture, out int siguiente) && siguiente > 1) {
            pendiente.PartidaDia = siguiente - 1;
            _logger.LogInformation("Partida sin T guardada como partida {partida} (la siguiente llegó con T{siguiente}).", pendiente.PartidaDia, siguiente);
            return pendiente;
        }

        // T1 (o una T ilegible): cambio de día o reinicio de la numeración; no
        // hay forma de saber qué número tenía.
        _logger.LogWarning("Se descarta la partida sin T (bolas {bolas}): la siguiente T ('{t}') no permite deducir su número.", pendiente.Bolas, valorT);
        return null;
    }

    private void Aplicar(TagEvent evt) {
        switch (evt.Code) {
            case 'C':
                _dto.Precio = ParseDecimal(evt.Code, evt.Value);
                break;
            case 'Q':
                _dto.Caja = ParseDecimal(evt.Code, evt.Value);
                break;
            case 'V':
                _dto.Vendidos = ParseInt(evt.Code, evt.Value);
                break;
            case 'T':
                _dto.PartidaDia = ParseInt(evt.Code, evt.Value);
                break;

            // --- Campos confirmados el 2026-10-14 contra tickets reales ---
            case 'K':
                // Número de decimales (ticket no lo imprime tal cual, pero
                // encaja con el campo ya existente HistoricoDTO.NumDecimales,
                // hasta ahora sin usar).
                _dto.NumDecimales = ParseInt(evt.Code, evt.Value);
                break;
            case 'P':
                // Primer cartón vendido. Confirmado: *P2051# == ticket
                // "SERIE/S ... 2051" (partida 1).
                _dto.Del1 = ParseInt(evt.Code, evt.Value);
                break;
            case 'U':
                // Último cartón vendido. Confirmado: *U2800# == ticket
                // "SERIE/S ... 2800" (partida 1).
                _dto.Al1 = ParseInt(evt.Code, evt.Value);
                break;
            case 'X':
                // Confirmado por el operador de la sala: X es SIEMPRE el
                // número de serie, nunca "borrar una bola" (eso es Z, ver
                // más abajo) — ya no hay ambigüedad entre los dos casos.
                AplicarSerie(evt.Value);
                break;
            case 'J':
                // El operador de la sala confirmó (2026-10-14, tercera ronda)
                // que en el lanzamiento de partida J puede llevar basura
                // detrás de los 4 dígitos de longitud de serie (p.ej.
                // "386400001&BIPLUSE1.255"), a ignorar — ver AplicarLetraSerie.
                AplicarLetraSerie(evt.Value);
                break;
            case 'Z':
                // Borra una bola ya registrada (corrección de una lectura
                // errónea del hardware). Confirmado que existe y que el
                // código correcto es 'Z', no 'X'; no apareció en las 7
                // partidas capturadas así que esta rama no se ha probado
                // contra tráfico real.
                EliminarBola(evt.Value);
                break;

            case 'E': {
                // Confirmado con una captura real del fabricante MCB
                // ("VolcadoMCB.txt", 2026-10-14): valores fuera de 1-90
                // (p.ej. "*E101#") aparecen como ruido del hardware — la
                // propia foto 'e' que llega justo antes y después es idéntica,
                // confirmando que no es una bola real. Se descartan en vez de
                // dejar que corrompan Bolas con un número imposible.
                int bola = ParseInt(evt.Code, evt.Value);
                if (bola is < 1 or > 90) break;

                // Una bola no puede salir dos veces en la misma partida (salvo
                // que se haya borrado antes con Z, que la desmarca): una
                // repetición es ruido, p.ej. una retransmisión.
                if (_bolasConocidas[bola - 1]) {
                    _logger.LogWarning("Bola {bola} repetida en la misma partida (evento E); se ignora.", bola);
                    break;
                }

                _bolasOrden.Add(bola);
                _bolasConocidas[bola - 1] = true;
                ActualizarBolasDto();
                break;
            }
            case 'e':
                AplicarEstadoPartida(evt.Value);
                break;

            case 'D':
                AplicarCantoLinea(evt.Value);
                break;
            case 'u':
                RevertirCantoLinea(evt.Value);
                break;
            case 'H':
                AplicarEventoH(evt.Value);
                break;
            case 'h':
                RevertirCantoBingo(evt.Value);
                break;
            case 'Y':
                AplicarEventoY(evt.Value);
                break;

            // 'f' (heartbeat "entre partidas") y 'º' (uno por cada *E#) están
            // confirmados pero no tienen efecto en el DTO — el operador de la
            // sala confirmó que 'º' no corresponde a ningún campo de
            // HISTORICO, así que ignorarlo es la respuesta definitiva, no un
            // parche mientras se investiga. 'G', 'S', 'R' (premios sin
            // confirmar) y las versiones en minúscula 'z'/'y' se ignoran por
            // instrucción explícita del operador (2026-10-14, tercera ronda:
            // "morralla" que complica sin aportar nada útil). Tratarlos como
            // "no mapeado" inundaría el log sin motivo.
            case 'f':
            case 'º':
            case 'G':
            case 'S':
            case 'R':
            case 'z':
            case 'y':
                break;

            default:
                AplicarSiEsPremioMapeado(evt);
                break;
        }
    }

    // Evento X: número de serie de los cartones, con posible empalme (p.ej.
    // "00179689,00179690" si una serie no basta para los cartones vendidos).
    // Confirmado con un ticket real (partida 4, dos líneas de SERIE/S) y
    // confirmado por el operador de la sala que X es siempre este campo
    // (el borrado de bolas es un código distinto, 'Z' — ver EliminarBola).
    private void AplicarSerie(string valor) {
        string[] partes = valor.Split(',');
        _dto.Serie1 = ParseIntOrNull('X', partes[0]);
        if (partes.Length > 1) {
            _dto.Serie2 = ParseIntOrNull('X', partes[1]);
        }
    }

    // Evento Z: elimina una bola ya marcada como salida (corrección de una
    // lectura errónea del hardware). Sin tráfico real que lo confirme, se
    // interpreta de la forma más simple posible: quita esa bola de la lista
    // y de _bolasConocidas como si nunca hubiera llegado. Si la bola no
    // estaba registrada (número fuera de rango o que no habíamos visto), no
    // hace nada — más seguro que corromper el estado con un valor inesperado.
    private void EliminarBola(string valor) {
        int bola = ParseInt('Z', valor);
        if (bola is < 1 or > 90) return;
        if (!_bolasConocidas[bola - 1]) return;

        _bolasOrden.Remove(bola);
        _bolasConocidas[bola - 1] = false;
        ActualizarBolasDto();
    }

    private void ActualizarBolasDto() {
        _dto.Bolas = _bolasOrden.Count > 0 ? string.Join(",", _bolasOrden) : null;
        _dto.Extracciones = _bolasOrden.Count;
    }

    // Evento J: código de longitud de serie -> letra de edición (ver
    // TagProtocolSettings.SerieLetraMap). Se guarda en _letraSerie en vez de
    // asignar directo a HistoricoDTO.Letra1/Letra2 porque Serie2 (si existe)
    // puede no estar asignado todavía cuando llega J — en Finalizar() se
    // aplica a ambas letras según corresponda, sin depender del orden real
    // de llegada entre X y J (en la captura disponible X siempre llega antes
    // que J, pero nada en el protocolo lo garantiza).
    //
    // Solo los primeros 4 caracteres son el código de longitud; el resto es
    // basura a ignorar (confirmado por el operador de la sala, 2026-10-14,
    // tercera ronda: p.ej. "386400001&BIPLUSE1.255" -> "3864").
    private void AplicarLetraSerie(string valor) {
        string codigoTexto = valor.Length > 4 ? valor[..4] : valor;
        int codigo = ParseInt('J', codigoTexto);
        if (_settings.CurrentValue.SerieLetraMap.TryGetValue(codigo, out string? letra)) {
            _letraSerie = letra;
        }
        else {
            _logger.LogInformation("Código de longitud de serie (evento J) no mapeado: {codigo}", codigo);
        }
    }

    // Canto de línea: *D<dígito de tipo de línea><cartón>#. Confirmado con
    // tickets reales que el primer dígito NO forma parte del número de
    // cartón: *D12363# (partida 1) es tipo "1" + cartón "2363", y el ticket
    // imprime "2363-S"; *D22940# (partida 2) es tipo "2" + cartón "2940",
    // ticket "2940-C". El dígito de tipo parece corresponder a la fila del
    // cartón (S/C/I = Superior/Central/Inferior en el ticket) pero no hay
    // columna en HISTORICO para guardarlo y no es necesario para el resto de
    // la lógica, así que se descarta tras separarlo del número de cartón.
    private void AplicarCantoLinea(string valor) {
        int carton = QuitarDigitoDeTipo('D', valor);
        if (carton > 0) RegistrarCantoLinea(carton);
    }

    private void RevertirCantoLinea(string valor) {
        int carton = QuitarDigitoDeTipo('u', valor);
        if (_lineaTentativa == null || _lineaTentativa.Value != carton) return;

        _cartonesLinea.Remove(carton);
        _dto.CartLinea = _cartonesLinea.Count > 0 ? string.Join(",", _cartonesLinea) : null;
        _dto.NumLineas = _cartonesLinea.Count;
        // BolaLinea no se puede recalcular con precisión si queda algún otro
        // cartón válido (no guardamos en qué bola cantó cada uno por
        // separado); se deja como estaba salvo que ya no quede ninguno.
        if (_cartonesLinea.Count == 0) _dto.BolaLinea = null;
        _lineaTentativa = null;
    }

    private void RegistrarCantoLinea(int carton) {
        _cartonesLinea.Add(carton);
        _dto.CartLinea = string.Join(",", _cartonesLinea);
        _dto.BolaLinea = _bolasOrden.Count;
        _dto.NumLineas = _cartonesLinea.Count;
        _lineaTentativa = carton;
    }

    // Canto de bingo: *H<cartón>#, SIN dígito de tipo (a diferencia de D) —
    // confirmado: *H2264# (partida 1) == ticket "2264-B" directo, sin
    // transformar.
    private void AplicarCantoBingo(string valor) {
        int carton = ParseInt('H', valor);
        if (carton > 0) RegistrarCantoBingo(carton);
    }

    // *H# sin valor: marca la entrada en la fase de bingo (confirmado en las
    // dos descripciones del protocolo recibidas) — no toca el DTO, pero
    // activa _faseBingo para que un *Y<cartón># posterior se interprete como
    // bingo en vez de línea (ver AplicarEventoY).
    private void AplicarEventoH(string valor) {
        if (string.IsNullOrEmpty(valor)) {
            _faseBingo = true;
            return;
        }
        AplicarCantoBingo(valor);
    }

    // *Y<cartón>#: formato de protocolo más reciente (confirmado por el
    // operador de la sala, 2026-10-14, tercera ronda) donde un mismo código
    // sirve para línea Y bingo, sin dígito de tipo — se distingue por si ya
    // ha llegado el *H# que marca la fase de bingo. *Y# sin valor sigue
    // siendo "entrando en comprobación", sin efecto en el DTO. Convive con el
    // *D#/*H# ya confirmado con tickets reales por si la sala usa hardware
    // de ambas variantes del protocolo.
    private void AplicarEventoY(string valor) {
        if (string.IsNullOrEmpty(valor)) return;

        int carton = ParseInt('Y', valor);
        if (carton <= 0) return;

        if (_faseBingo) RegistrarCantoBingo(carton);
        else RegistrarCantoLinea(carton);
    }

    private void RevertirCantoBingo(string valor) {
        int carton = ParseInt('h', valor);
        if (_bingoTentativo == null || _bingoTentativo.Value != carton) return;

        _cartonesBingo.Remove(carton);
        _dto.CartBingo = _cartonesBingo.Count > 0 ? string.Join(",", _cartonesBingo) : null;
        _dto.NumBingos = _cartonesBingo.Count;
        if (_cartonesBingo.Count == 0) _dto.BolaBingo = null;
        _bingoTentativo = null;
    }

    private void RegistrarCantoBingo(int carton) {
        _cartonesBingo.Add(carton);
        _dto.CartBingo = string.Join(",", _cartonesBingo);
        _dto.BolaBingo = _bolasOrden.Count;
        _dto.NumBingos = _cartonesBingo.Count;
        _bingoTentativo = carton;
    }

    // "*D#" sin valor (marcador en MCB) o con solo el dígito de tipo no es un
    // canto: se ignora sin avisar. El aviso, si lo hay, muestra el valor
    // completo (con el dígito de tipo) para que coincida con lo recibido.
    private int QuitarDigitoDeTipo(char code, string valor) {
        if (valor.Length < 2) return 0;
        if (char.IsAsciiDigit(valor[0]) &&
            int.TryParse(valor[1..], SoloDigitos, CultureInfo.InvariantCulture, out int carton)) {
            return carton;
        }
        AvisarValorNoNumerico(code, valor);
        return 0;
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
            _bolasOrden.Add(numero);
            huboBolasNuevas = true;
        }

        if (huboBolasNuevas) {
            ActualizarBolasDto();
            _recuperadaParcialmente = true;
            _logger.LogInformation("Partida (protocolo tag) enganchada a mitad vía evento 'e': se recuperaron bolas anteriores a la conexión (orden aproximado).");
        }
    }

    // Códigos cuyo significado difiere confirmadamente según el fabricante
    // (ver la nota al principio del fichero). Con Fabricante=MCB se ignoran
    // aunque estén en PremioCodeMap; y en cualquier caso, si no están en el
    // mapa, no se registran como "no mapeado": en hardware MCB aparecen
    // decenas de veces por partida como puro ruido y llenarían el log.
    private static readonly HashSet<char> CodigosSegunFabricante = new() { 'A', 'M', 'p' };

    private void AplicarSiEsPremioMapeado(TagEvent evt) {
        var settings = _settings.CurrentValue;
        if (settings.Fabricante == Fabricante.MCB && CodigosSegunFabricante.Contains(evt.Code)) return;

        var map = settings.PremioCodeMap;
        string code = evt.Code.ToString();

        if (map.TryGetValue(code, out int indice) && indice >= 0 && indice < _dto.Premios.Length) {
            _dto.Premios[indice] = ParseDecimal(evt.Code, evt.Value);
        }
        else if (!CodigosSegunFabricante.Contains(evt.Code)) {
            // Nivel Information a propósito: en un despliegue nuevo interesa
            // ver en el log, sin tocar configuración, qué códigos emite el
            // hardware real que no encajan con el mapeo asumido.
            _logger.LogInformation("Código de evento (protocolo tag) no mapeado: {code} (valor '{value}')", evt.Code, evt.Value);
        }
    }

    private HistoricoDTO Finalizar(bool sinT) {
        var settings = _settings.CurrentValue;
        _dto.CodSala = settings.DefaultCodSala;
        _dto.CodConc = settings.DefaultCodConc;

        // FechaSesion: el protocolo no la lleva; se asume la fecha de hoy en
        // el momento de cerrar la partida.
        if (string.IsNullOrWhiteSpace(_dto.FechaSesion)) {
            _dto.FechaSesion = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        // Letra de edición (ver AplicarLetraSerie): se aplica aquí, ya con
        // Serie1/Serie2 definitivos, en vez de al llegar el evento J.
        if (_letraSerie != null) {
            _dto.Letra1 = _letraSerie;
            if (_dto.Serie2.HasValue) _dto.Letra2 = _letraSerie;
        }

        // Precio y los premios monetarios (L, B, p, A — los códigos de
        // PremioCodeMap) llegan en céntimos, no en euros: confirmado céntimo
        // a céntimo contra los tickets reales (*C200# == ticket
        // "PRECIO: 2,00", *L9000# == "LINEA: 90,00", etc. en las 7 partidas).
        // Se convierten aquí, al final, usando el número de decimales del
        // evento K (K2 en las 7 partidas reales; si no llegara K se asume 2,
        // lo habitual en euros) — así da igual en qué orden lleguen K y los
        // importes. Caja (evento Q) NO se toca: Q no aparece en ninguna de
        // las 7 partidas reales, así que no hay forma de confirmar si también
        // va en céntimos; se deja como estaba hasta tener evidencia real.
        //
        // M (bola tope de prima, PremioCodeMap["M"]) se queda en Premios pero
        // es un número de bola, no un importe — se excluye explícitamente de
        // la conversión para no dividirlo por error.
        int decimales = _dto.NumDecimales ?? 2;
        decimal divisor = (decimal)Math.Pow(10, decimales);
        _dto.Precio /= divisor;
        int? indiceBolaTopePrima = _settings.CurrentValue.PremioCodeMap.TryGetValue("M", out int indiceM) ? indiceM : null;
        for (int i = 0; i < _dto.Premios.Length; i++) {
            if (i == indiceBolaTopePrima) continue;
            if (_dto.Premios[i].HasValue) _dto.Premios[i] = _dto.Premios[i]!.Value / divisor;
        }

        // Sin T, la partida se guarda con el número deducido de la siguiente
        // (ver ResolverPendienteSinT) y sin la cabecera (precio, cartones,
        // serie, premios: todo llega justo tras la T), así que siempre se marca.
        string? nota = sinT
            ? (_recuperadaParcialmente
                ? "PARTIDA A MEDIAS: Nº DEDUCIDO, SIN CABECERA, BOLAS APROX."
                : "PARTIDA A MEDIAS: Nº DEDUCIDO, SIN CABECERA")
            : (_recuperadaParcialmente ? "PARTIDA RECUPERADA A MEDIAS (orden de bolas aproximado)" : null);

        if (nota != null) {
            // RECLAMACION es CHAR(64) en Firebird (HISTORICO, confirmado vía
            // RDB$FIELDS: tipo 14 = CHAR fijo, no VARCHAR) — un texto más
            // largo provoca "string right truncation" y pierde la partida entera
            // (cae al fichero de fallback). El detalle completo queda en el log
            // ("enganchada a mitad vía evento 'e'"), aquí solo cabe una marca corta.
            const int maxReclamacion = 64;
            string combinado = string.IsNullOrWhiteSpace(_dto.Reclamacion) ? nota : _dto.Reclamacion + " | " + nota;
            _dto.Reclamacion = combinado.Length > maxReclamacion ? combinado[..maxReclamacion] : combinado;
        }

        return _dto;
    }

    private void Reset() {
        _dto = new HistoricoDTO();
        _bolasOrden.Clear();
        _letraSerie = null;
        _cartonesLinea.Clear();
        _cartonesBingo.Clear();
        _lineaTentativa = null;
        _bingoTentativo = null;
        _faseBingo = false;
        Array.Clear(_bolasConocidas);
        _recuperadaParcialmente = false;
    }

    // El protocolo solo manda dígitos (los importes van en céntimos, sin punto
    // decimal). NumberStyles.Any aceptaba basura con pinta de número como
    // válida: "4e1" -> 40, "4,000" -> 4000, "(400)" -> -400.
    private const NumberStyles SoloDigitos = NumberStyles.None;

    private decimal ParseDecimal(char code, string value) {
        if (decimal.TryParse(value, SoloDigitos, CultureInfo.InvariantCulture, out decimal result)) return result;
        AvisarValorNoNumerico(code, value);
        return 0m;
    }

    private int ParseInt(char code, string value) {
        if (int.TryParse(value, SoloDigitos, CultureInfo.InvariantCulture, out int result)) return result;
        AvisarValorNoNumerico(code, value);
        return 0;
    }

    private int? ParseIntOrNull(char code, string value) {
        if (int.TryParse(value, SoloDigitos, CultureInfo.InvariantCulture, out int result)) return result;
        AvisarValorNoNumerico(code, value);
        return null;
    }

    private void AvisarValorNoNumerico(char code, string value) =>
        _logger.LogWarning("Valor numérico inválido en evento tag {code}: '{value}'. Se descarta.", code, value);
}
