Documento de Integración para Terceros

Versión: 1.0 | Referencia: Gregal-Net-2026
1. REQUISITOS DE CONEXIÓN
El sistema de intercambio de datos se basa en un socket TCP persistente.
    Transporte: TCP/IP Estándar.
    Codificación de caracteres: UTF-8.
    Delimitador de trama: Carácter Nulo (\0, 0x00, ASCII 0).
    Puerto por defecto: 5000 (Configurable).
    IMPORTANTE: El servidor utiliza el carácter \0 para determinar el fin de un mensaje.
                Todas las peticiones del cliente deben finalizar con este carácter o el servidor ignorará la trama.

2. FORMATO DE LOS MENSAJES (JSON)
    2.1 Petición del Cliente (Request)

    El cliente debe enviar un objeto JSON con la siguiente estructura obligatoria:
    Campo	Tipo	Requerido	Descripción
    msg_id	String	Sí	Identificador único de transacción para trazabilidad.
    operation	String	Sí	Valores válidos: "ping", "upsert".
    data	Object	Solo Upsert	Contenedor de datos del histórico de partida.
    Ejemplo de Trama Upsert:
    JSON

    {
      "msg_id": "REQ-1001",
      "operation": "upsert",
      "data": {
        "CodSala": 12,
        "PartidaDia": 58,
        "Fecha": "2026-03-31",
        "Hora": "14:20:00",
        "ImportePremios": 1250.00
      }
    }\0

2.2 Respuesta del Servidor (Response)
El servidor responderá siempre con un JSON que confirma el estado del procesamiento:
    Campo	    Tipo	    Descripción
    status	    String	    Success, Error o Pong.
    msg_id	    String	    Retorna el mismo ID enviado por el cliente.
    error_code	String	    Código técnico del error (solo si status es Error).
    detail	    String	    Descripción humana o técnica adicional.
    timestamp	String	    Estampa de tiempo del servidor (YYYY-MM-DD HH:mm:ss).

3. DICCIONARIO DE ERRORES (error_code)
    Código	            Descripción
    FormatInvalid	    JSON mal formado o nodo "data" mal estructurado.
    UnknownOperation	La operación enviada no existe en el servidor.
    DatabaseRejected	La base de datos Firebird rechazó el registro.
    InternalServerError	Error crítico inesperado en el servidor.
    BufferExceeded	    El mensaje excede el tamaño máximo de seguridad.

4. POLÍTICAS DE RED Y SEGURIDAD
    Lista Blanca (AllowList): El servidor solo acepta conexiones de IPs previamente autorizadas.
    Keep-Alive: Se recomienda que el cliente mantenga la conexión abierta. El servidor enviará paquetes de mantenimiento cada 30 segundos.
    Timeout de Inactividad: Si no se detecta tráfico tras 5 minutos, el servidor cerrará el socket de forma automática.
    Confirmación: Una partida no se considera procesada hasta que el servidor devuelva status: "Success".

© 2026 Proyecto Gregal - Departamento de Sistemas

