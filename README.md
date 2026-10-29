# Gregal

## Configuración de la base de datos

En `appsettings.json`, `DatabaseSettings:ConnectionString` indica el archivo
`.FDB` y `DatabaseSettings:TableName` la tabla destino (por defecto,
`HISTORICO`). Para desarrollo, `appsettings.Development.json` puede definir
ambos valores de forma independiente.

La tabla configurada debe existir y tener las mismas columnas, tipos y clave de
emparejamiento que `HISTORICO`, ya que Gregal guarda el histórico con un
`UPDATE OR INSERT` usando `COD_SALA`, `COD_CONC`, `FECHA_SESION` y
`PARTIDA_DIA`.

## Cómo se guarda en la base de datos

Todo el acceso a Firebird está en `Gregal/Data/HistoricoRepository.cs`
(`SaveHistoricoAsync`).

**Una transacción por partida, no por evento.** Con el protocolo de eventos
(`*CODIGO<valor>#`), Gregal va acumulando la partida en memoria
(`PartidaAccumulator`) sin tocar la base de datos. Solo al recibir el `*F#`
de fin de partida monta la fila completa y la pasa a una cola de guardado,
que atiende un hilo aparte: la lectura del puerto serie nunca espera a
Firebird.

Para cada partida:

1. **Un guardado cada vez.** Un semáforo global serializa las escrituras,
   aunque lleguen varias partidas seguidas o haya varios receptores activos.
2. **Conexión.** Se abre con `DatabaseSettings:ConnectionString`. El driver
   de Firebird reutiliza conexiones (*pooling*, activo por defecto).
3. **Transacción** con una única sentencia
   `UPDATE OR INSERT INTO HISTORICO ... MATCHING (COD_SALA, COD_CONC, FECHA_SESION, PARTIDA_DIA)`:
   si ya existe una fila con esa clave se actualiza; si no, se inserta. Volver
   a enviar la misma partida sobrescribe su fila, no la duplica.
4. **Commit.** En el log: `V PARTIDA GUARDADA: Sala 90, Partida 10 en 27ms`.
5. **Si algo falla** (no conecta, error de SQL...): *rollback*, el error queda
   en el log y la partida se añade en JSON a
   `logs/historico_pendientes_tag.jsonl` para no perderla. No hay reintento
   automático: esas partidas se recuperan a mano.

Cada transacción dura unos milisegundos y solo hay una por partida, así que
Gregal puede compartir la base de datos con el software de la sala sin
bloquearlo.

**Lista de bolas demasiado larga.** `BOLAS` guarda las bolas en orden de
salida separadas por comas; 90 bolas ocupan 260 caracteres. En las bases de
datos de sala la columna suele ser `VARCHAR(255)`, y una partida de unas 86
bolas o más no cabría (Firebird rechazaría la fila entera). En ese caso se
guardan las primeras bolas enteras que caben en
`DatabaseSettings:MaxLongitudBolas` (255 por defecto), se antepone
`BOLAS RECORTADAS` a `RECLAMACION` y la lista completa queda en el log. Si la
columna se amplió con `Gregal/Docs/Migraciones/2026-10-08_ampliar_bolas.sql`,
pon `MaxLongitudBolas` a 300. En las partidas reales capturadas salieron entre
47 y 72 bolas (210 caracteres como mucho).

Gregal/
├── Data/                 # Acceso a Base de Datos (Firebird)
├── Models/               # Entidades (la clase Historico que creamos)
├── Networking/           # Lógica del servidor TCP (Sockets)
├── Parsing/              # Lógica para convertir texto a objetos
├── Services/             # El Worker Service principal
└── Utils/                # Formateadores, logs o constantes

Detalles de Namespaces y Responsabilidades
1. Gregal.Models

Aquí colocas la clase Historico.cs. Es el "corazón" que todos los demás componentes conocen.

    Namespace: Gregal.Models

2. Gregal.Data

Aquí creas una clase llamada HistoricoRepository.cs. Esta clase contendrá el FbConnection y el método Insertar(Historico registro).

    Namespace: Gregal.Data

    Objetivo: Aislar todo el SQL y las cadenas de conexión de Firebird.

3. Gregal.Networking

Aquí creas una clase llamada TcpServer.cs. Se encarga de abrir el puerto y recibir los bytes.

    Namespace: Gregal.Networking

    Objetivo: Manejar la conexión de red sin preocuparse de qué dicen los datos o dónde se guardan.

4. Gregal.Parsing

Crea una clase llamada GregalParser.cs. Esta clase recibe el string o byte[] que llega por TCP y devuelve un objeto Historico relleno.

    Namespace: Gregal.Parsing

    Objetivo: Centralizar las reglas de negocio (ej. cómo separar los campos, formatos de fecha, etc.).

5. Gregal.Services

Aquí estará el archivo Worker.cs que genera Visual Studio por defecto.

    Namespace: Gregal (o Gregal.Services)

    Objetivo: Es el "director de orquesta". Llama al TcpServer para obtener datos, al Parser para procesarlos y al Repository para guardarlos.