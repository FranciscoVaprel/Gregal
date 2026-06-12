# Especificación Técnica: Protocolo de Comunicación "Gregal"

## 1. Introducción
El proyecto **Gregal** es un servicio encargado de la recepción, procesamiento y almacenamiento de datos históricos de partidas. Esta documentación define el
contrato de comunicación entre el sistema emisor (cliente) y el servicio Gregal (servidor).

---

## 2. Configuración de Red
El servicio opera como un servidor de sockets TCP/IP.

* **Protocolo:** TCP
* **Codificación:** UTF-8
* **Puerto predeterminado:** *[Por definir, ej. 5000]*
* **Modo de flujo:** Stream continuo orientado a mensajes.

---

## 3. Formato de Trama (Contrato)
Para garantizar la compatibilidad y flexibilidad, se ha seleccionado el formato **JSON**.

### 3.1. Delimitador de Mensajes
Dado que TCP es un flujo de bytes, el emisor **debe** finalizar cada objeto JSON con el carácter de salto de línea **`\n`** (LF - Line Feed, ASCII 10). 
> **Nota:** El servidor utiliza este carácter para segmentar los mensajes en caso de recibir múltiples tramas en el mismo paquete de red.

### 3.2. Estructura de Datos
El cuerpo del mensaje es un objeto JSON plano cuyas claves deben coincidir con los campos de la tabla `HISTORICO`.

#### Campos de Identificación (Obligatorios)
| Campo | Tipo | Descripción |
| :--- | :--- | :--- |
| `CodSala` | Integer | Identificador único de la sala. |
| `CodConc` | Integer | Código de la concesión. |
| `FechaSesion` | String (ISO) | Fecha de la sesión en formato `YYYY-MM-DD`. |
| `PartidaDia` | Integer | Número correlativo de partida en el día. |

#### Campos de Negocio (Muestra de campos principales)
| Campo | Tipo | Formato / Observaciones |
| :--- | :--- | :--- |
| `Fecha` | String | Timestamp: `YYYY-MM-DD HH:mm:ss`. |
| `Venta` | Decimal | Valor numérico (ej: `150.50`). |
| `HoraCierre` | String | Formato `HH:mm:ss`. |
| `Bolas` | String | Lista de valores (ej: `"1,45,23..."`). |

#### Bloque de Premios
Se admiten claves individuales desde `Premio1` hasta `Premio99`. Solo es necesario enviar aquellos que contengan valores distintos de cero.

---

## 4. Ejemplo de Trama Válida

```json
{
  "CodSala": 1,
  "CodConc": 10,
  "FechaSesion": "2026-03-17",
  "PartidaDia": 5,
  "Fecha": "2026-03-17 12:30:00",
  "Venta": 500.00,
  "Caja": 450.00,
  "HoraSorteo": "12:35:00",
  "Bolas": "1,45,23,12,67,8,9,10",
  "Premio1": 150.25,
  "Premio2": 300.00
}\n

5. Reglas de Validación y Errores

    Formatos Decimales: Utilizar el punto . como separador decimal. No incluir separadores de miles.

    Campos Ausentes: Si un campo no tiene valor, puede omitirse o enviarse como null.

    Case Sensitivity: El servidor procesa las claves ignorando mayúsculas/minúsculas, pero se recomienda el uso de PascalCase para mantener la consistencia con el modelo C#.

    Triggers: El sistema destino (Firebird) disparará automáticamente eventos de replicación y refresco tras la inserción exitosa.