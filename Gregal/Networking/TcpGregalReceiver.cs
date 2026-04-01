using Gregal.Config;
using Gregal.Data;
using Gregal.Models;
using Gregal.Parsing;
using Gregal.Protocol;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Timers;
using static Gregal.Protocol.GregalProtocol;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Gregal.Networking {
    public class TcpGregalReceiver : IGregalReceiver {
        private readonly ILogger<TcpGregalReceiver> _logger;
        private readonly IOptionsMonitor<NetworkSettings> _network;
        private readonly IParser _parser;
        private readonly HistoricoRepository _repo;
        private TcpListener? _server;
        
        private static readonly JsonSerializerOptions _options = new() {
            PropertyNameCaseInsensitive = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };

        public TcpGregalReceiver(
            ILogger<TcpGregalReceiver> logger,
            IOptionsMonitor<NetworkSettings> network,
            GregalParserEngine parser,
            HistoricoRepository repo) {
            _logger = logger;
            _network = network;
            _parser = parser;
            _repo = repo;
        }
        
        /// <summary>        
        ///    _server.Start(): El sistema operativo reserva el puerto(ej. 5000). A partir de aquí, el servidor es visible en la red.

        ///    await _server.AcceptTcpClientAsync(ct): El código se detiene aquí.No consume CPU, simplemente espera. Cuando un cliente se conecta, se "despierta" y nos entrega un objeto client.


        ///    SetSocketOption (KeepAlive): Activa los pulsos de mantenimiento.

        ///    TcpKeepAliveTime: Define cuánto tiempo de silencio debe pasar antes de enviar el primer pulso (ej. 30 segundos).

        /// TcpKeepAliveInterval: Si el primer pulso no tiene respuesta, define cada cuánto tiempo reintentar antes de dar la conexión por muerta.

        /// _ = HandleIncomingConnection(...): Esto es fundamental.El símbolo _ = (discard)significa: "Inicia la gestión de este cliente en un hilo aparte y vuelve inmediatamente al while para aceptar al siguiente". Sin esto, tu servidor solo podría atender a una persona a la vez.
        /// </summary> 
        public async Task ListenAsync(CancellationToken ct) {
            var settings = _network.CurrentValue;
            var port = settings.TcpPort;
            var address = IPAddress.Parse(settings.TcpAddress);

            _server = new TcpListener(address, port);
            _server.Start();

            _logger.LogInformation("Servidor TCP iniciado en {address}:{port}", address, port);

            while (!ct.IsCancellationRequested) {
                try {
                    var client = await _server.AcceptTcpClientAsync(ct);

                    // Aplicamos Keep-Alive usando los valores de la configuración[cite: 2]
                    client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, settings.TcpKeepAliveTime);
                    client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, settings.TcpKeepAliveInterval);

                    _ = HandleIncomingConnection(client, ct);
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Error en receptor TCP");
                }
            }
        }

        private async Task HandleIncomingConnection(TcpClient client, CancellationToken ct) {
            string remoteIp = "Desconocida";
            try {
                remoteIp = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();

                // REINTEGRACIÓN DE SEGURIDAD: Validamos la IP antes de hacer nada
                if (!IsIpAuthorized(remoteIp)) {
                    _logger.LogWarning("Intento de conexión bloqueado: {ip} no está autorizada.", remoteIp);
                    return; // Cerramos la conexión inmediatamente
                }

                _logger.LogInformation("Cliente autorizado y conectado: {ip}", remoteIp);
                await ReadStreamUntilClosed(client, remoteIp, ct);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error crítico en sesión con {ip}", remoteIp);
            }
            finally {
                client.Close();
                _logger.LogInformation("Conexión con {ip} cerrada.", remoteIp);
            }
        }

        private async Task ReadStreamUntilClosed(TcpClient client, string remoteIp, CancellationToken ct) {
            using var stream = client.GetStream();

            // LEEMOS DE CONFIGURACIÓN
            var settings = _network.CurrentValue;
            stream.ReadTimeout = (int)TimeSpan.FromMinutes(settings.TcpReadTimeoutMinutes).TotalMilliseconds;

            byte[] buffer = new byte[4096];
            string accumulator = string.Empty;

            try {
                _logger.LogDebug("Sesión iniciada con {ip}. Timeout de lectura: {min} min.", remoteIp, settings.TcpReadTimeoutMinutes);

                while (client.Connected && !ct.IsCancellationRequested) {
                    // ReadAsync utilizará el CancellationToken para salir si el servidor se apaga
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);

                    if (bytesRead == 0) {
                        _logger.LogInformation("El cliente {ip} ha finalizado la conexión de forma ordenada.", remoteIp);
                        break;
                    }

                    accumulator += System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    
                    if (accumulator.Length > settings.TcpMaxBufferChars) {
                        _logger.LogWarning("Inundación detectada en {ip}. Max: {max}. Actual: {len}",
                            remoteIp, settings.TcpMaxBufferChars, accumulator.Length);

                        // 1. Intentamos avisar al cliente antes de limpiar
                        try {
                            // Intentamos extraer un msg_id si es posible, si no, enviamos uno genérico
                            string errorResponse = GregalProtocolFactory.CreateError(ErrorCode.BUFFER_EXCEEDED, "unknown", "El mensaje excede el tamaño máximo");
                            byte[] errorData = System.Text.Encoding.UTF8.GetBytes(errorResponse);
                            await stream.WriteAsync(errorData, 0, errorData.Length, ct);
                        }
                        catch { /* Si el socket ya falló, no importa */ }

                        // 2. Limpiamos y seguimos
                        accumulator = string.Empty;
                        continue;
                    }
                    
                    accumulator = await ExtractAndProcessMessages(accumulator, stream, remoteIp, ct);
                }
            }
            catch (OperationCanceledException) {
                _logger.LogInformation("Operación cancelada (Apagado del servidor) para {ip}.", remoteIp);
            }
            catch (IOException ex) when (ex.InnerException is SocketException sx &&
                                       (sx.SocketErrorCode == SocketError.TimedOut || sx.SocketErrorCode == SocketError.ConnectionReset)) {
                _logger.LogWarning("Conexión con {ip} cerrada por INACTIVIDAD ({min} min sin datos/ping) o fallo de red.", remoteIp, settings.TcpReadTimeoutMinutes);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error inesperado con {ip}", remoteIp);
            }
            finally {
                client.Close();
                _logger.LogInformation("Recursos liberados para la sesión de {ip}.", remoteIp);
            }
        }

        private async Task<string> ExtractAndProcessMessages(string accumulator, NetworkStream stream, string remoteIp, CancellationToken ct) {
            // Buscamos el delimitador \0 definido en el protocolo
            while (accumulator.Contains('\0')) {
                int index = accumulator.IndexOf('\0');
                string rawMessage = accumulator.Substring(0, index).Trim();
                accumulator = accumulator.Substring(index + 1);

                if (!string.IsNullOrWhiteSpace(rawMessage)) {
                    // Procesamos el mensaje y obtenemos la respuesta JSON ya formateada
                    string responseJson = await ProcessMessageAsync(rawMessage, remoteIp);
#if DEBUG
                    _logger.LogDebug("ENVIANDO AL CLIENTE ({ip}): {data}", remoteIp, responseJson);
#endif
                    // Enviamos la respuesta al cliente
                    byte[] responseBytes = System.Text.Encoding.UTF8.GetBytes(responseJson);
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length, ct);
                }
            }
            return accumulator;
        }

        private async Task<string> ProcessMessageAsync(string rawData, string remoteIp) {
            // Definimos un msgId por defecto por si la deserialización falla
            string msgId = "unknown";

            try {
                // 1. Deserialización única
                var request = JsonSerializer.Deserialize<GregalRequest>(rawData, _options);

                // ERROR CORREGIDO: Pasamos Enum y msgId por defecto
                if (request == null)
                    return GregalProtocolFactory.CreateError(ErrorCode.FORMAT_INVALID, msgId);

                msgId = request.MsgId; // Ahora ya tenemos el ID real del cliente

                // 2. Lógica de PING
                if (request.Operation?.ToLower() == "ping") {
                    return GregalProtocolFactory.CreatePong(msgId);
                }

                // 3. Lógica de UPSERT
                if (request.Operation?.ToLower() == "upsert") {
                    // ERROR CORREGIDO: Pasamos Enum y msgId
                    if (request.Data == null)
                        return GregalProtocolFactory.CreateError(ErrorCode.FORMAT_INVALID, msgId, "Nodo 'data' ausente o vacío");

                    bool ok = await _repo.SaveHistoricoAsync(request.Data);

                    // ERROR CORREGIDO: Pasamos Enum y msgId
                    return ok
                        ? GregalProtocolFactory.CreateSuccess(msgId)
                        : GregalProtocolFactory.CreateError(ErrorCode.DATABASE_REJECTED, msgId);
                }

                // ERROR CORREGIDO: Pasamos Enum y msgId
                return GregalProtocolFactory.CreateError(ErrorCode.UNKNOWN_OPERATION, msgId);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error con {ip}", remoteIp);
                // ERROR CORREGIDO: Pasamos Enum y msgId
                return GregalProtocolFactory.CreateError(ErrorCode.INTERNAL_SERVER_ERROR, msgId, ex.Message);
            }
        }

        //private async Task<string> ProcessMessageAsync(string rawData, string remoteIp) {
        //    // 1. Primero, usamos JsonDocument para leer la "envoltura" sin romper nada
        //    // Esto nos permite sacar el msg_id y la operación sea como sea el JSON
        //    using var doc = JsonDocument.Parse(rawData);
        //    var root = doc.RootElement;

        //    // Extraemos el msg_id (si no existe, ponemos uno genérico)
        //    string msgId = root.TryGetProperty("msg_id", out var idProp)
        //        ? idProp.GetString() ?? "unknown"
        //        : "unknown";

        //    // Extraemos la operación
        //    string operation = root.TryGetProperty("operation", out var opProp)
        //        ? opProp.GetString()?.ToLower() ?? ""
        //        : "";

        //    try {
        //        // 2. TRATAMIENTO DEL PING (Respuesta inmediata)
        //        if (operation == "ping") {
        //            _logger.LogInformation("PING recibido de {ip}. Respondiendo PONG.", remoteIp);
        //            return GregalProtocolFactory.CreatePong(msgId);
        //        }

        //        // 3. TRATAMIENTO DEL UPSERT (Aquí entra tu DTO y tu Parser)
        //        if (operation == "upsert") {
        //            // Llamamos a TU motor (Aquí saltará tu punto de ruptura)
        //            // Le pasamos el rawData completo porque el Engine ya sabe buscar el nodo "data"
        //            var dto = _parser.Parse(rawData);

        //            if (dto == null) {
        //                _logger.LogWarning("Operación UPSERT fallida: No se pudo parsear el contenido de 'data' para {ip}", remoteIp);
        //                return GregalProtocolFactory.CreateError("INVALID_DATA_FORMAT", msgId);
        //            }

        //            // 4. GUARDADO EN BASE DE DATOS
        //            bool guardadoOk = await _repo.SaveHistoricoAsync(dto);

        //            if (guardadoOk) {
        //                _logger.LogInformation("ÉXITO: Partida {p} de Sala {s} guardada/actualizada (IP: {ip})",
        //                    dto.PartidaDia, dto.CodSala, remoteIp);
        //                return GregalProtocolFactory.CreateSuccess(msgId);
        //            }
        //            else {
        //                _logger.LogError("ERROR BBDD: El repositorio rechazó el guardado para {ip}", remoteIp);
        //                return GregalProtocolFactory.CreateError("DATABASE_INSERT_FAILED", msgId);
        //            }
        //        }

        //        // 5. OPERACIÓN DESCONOCIDA
        //        _logger.LogWarning("Operación no soportada '{op}' desde {ip}", operation, remoteIp);
        //        return GregalProtocolFactory.CreateError("UNKNOWN_OPERATION", msgId);
        //    }
        //    catch (Exception ex) {
        //        _logger.LogError(ex, "Error crítico procesando mensaje de {ip}", remoteIp);
        //        return GregalProtocolFactory.CreateError("INTERNAL_SERVER_ERROR", msgId);
        //    }
        //}

        private bool IsIpAuthorized(string ip) {
            var allowedIps = _network.CurrentValue.AllowedIps;
            return allowedIps == null || allowedIps.Count == 0 || allowedIps.Contains(ip);
        }

        public void Stop() => _server?.Stop();
    }
}