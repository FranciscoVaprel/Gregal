1. Inserción o Actualización de Partida (upsert)

Entrada (Lo que tú envías): (Recuerda añadir al final tu delimitador físico según configuración (\n o \0).)
{"msg_id":"TX-20260616-01","operation":"upsert","data":{"codSala":1,"codConc":1,"fechaSesion":"2026-06-16","partidaDia":42,"precio":2.00,"venta":640.00,"caja":320.00,"PREMIO1":12.50,"PREMIO2":25.00,"PREMIO3":150.00}}

Respuesta de Gregal (Éxito):
{"msg_id":"TX-20260616-01","status":"OK","message":"Historico guardado correctamente"}


2. Trama de Verificación de Conexión (ping)
Útil para que el hardware de la sala compruebe si el receptor de C# sigue encendido y escuchando de fondo.

Entrada (Lo que tú envías):
{"msg_id":"PING-REQ-05","operation":"ping"}

Respuesta de Gregal:
{"msg_id":"PING-REQ-05","status":"PONG","message":"Service operational"}

🔸 Modo 2: Tramas compactas en CSV (Líneas de Texto Plano)
Si las salas de bingo usan hardware antiguo que no procesa JSON, envían los campos separados por el carácter delimitador (normalmente ;).
Tu CsvGregalParser se encarga de trocearlo.

El orden posicional estándar que espera tu lógica de base de datos es:
ID_MENSAJE ; OPERACION ; CODSALA ; CODCONC ; FECHA ; PARTIDADIA ; PRECIO ; VENTA ; CAJA

1. Operación de Guardar Histórico

Entrada (Lo que tú envías):
TX-CSV-88;upsert;1;1;2026-06-16;42;2.00;640.00;320.00

Respuesta de Gregal (Éxito):
{"msg_id":"TX-CSV-88","status":"OK","message":"Historico guardado correctamente"}

❌ Tramas de Error del Protocolo

Si envías algo que rompe las reglas que programamos en el acumulador de bytes o en las transacciones de Firebird, el servicio atrapará el fallo y escupirá una respuesta controlada de error para no colgarse:
1. Error de Formato Desconocido (No es JSON ni CSV válido)
Si mandas una cadena corrupta como HOLA MUNDO BINGO \n.

Respuesta de Gregal:
{"msg_id":"unknown","status":"ERROR","error_code":"PARSE_ERROR","message":"Formato de trama invalido o desconocido"}

2. Error de Desbordamiento de Memoria (BUFFER_EXCEEDED)
Ocurre si bombardeas el puerto serie tan rápido que el acumulador supera el tamaño configurado en el JSON antes de poder extraer los delimitadores (lo que nos pasó en el test de estrés con PowerShell).

Respuesta de Gregal:
{"msg_id":"unknown","status":"ERROR","error_code":"BUFFER_EXCEEDED","message":"Buffer excedido"}

3. Error Fatal de Base de Datos
Si pones un campo mal o Firebird rechaza la consulta por una colisión en los tipos de datos o un bloqueo de tabla.

Respuesta de Gregal:
{"msg_id":"TX-20260616-01","status":"ERROR","error_code":"DATABASE_ERROR","message":"Error al guardar en Firebird SQL"}

Pruebas en Hercules Setup:
{"msg_id":"HERCULES-TEST-JSON","operation":"upsert","data":{"codSala":1,"codConc":1,"fechaSesion":"2026-06-09","partidaDia":12,"precio":2.00,"venta":500.00,"caja":250.00}}$00

Creacion como servicio de Windows:
sc.exe create "GregalReceiverService" binPath= "C:\Deploy\Gregal\Gregal.exe" start= auto displayname= "Gregal - Receptor de Bingo"
