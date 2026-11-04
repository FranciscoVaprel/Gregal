# Gregal

Servicio de Windows (.NET 10) que recibe los datos de las partidas de bingo
de una sala y los guarda en la tabla `HISTORICO` de una base de datos
Firebird.

Entiende dos protocolos, que se activan por separado en `appsettings.json`:

- **Protocolo de eventos por RS232** (`*CODIGO<valor>#`), el que usan las
  máquinas de bingo de **Metronia** y **MCB**. Es el receptor principal y el
  único activo por defecto.
- **Protocolo JSON/CSV antiguo**, por TCP y/o RS232: un mensaje por partida
  con respuesta. Se conserva para hardware que aún lo use (ver
  [`Gregal/readme.md`](Gregal/readme.md) y
  [`Gregal/Docs/Specs.md`](Gregal/Docs/Specs.md)).

## Requisitos

- Windows 10/11 de 64 bits.
- No hace falta instalar .NET: el instalador y los zip llevan el runtime de
  .NET 10 dentro de la carpeta del programa (publicación autocontenida).
- Servidor Firebird en marcha, con una base de datos que tenga la tabla
  `HISTORICO` (o una copia de la plantilla vacía `GREGAL_EMPTY.FDB`).
- La máquina de bingo conectada a un puerto serie del PC (9600 baudios, 8N1).

## Instalar o actualizar en una sala

Descarga la última versión desde
[Releases](https://github.com/FranciscoVaprel/Gregal/releases). Cada versión
trae tres ficheros:

| Fichero | Para qué |
|---|---|
| `Gregal-Setup-<versión>.exe` | Instalador/actualizador. En un PC sin Gregal instala en `C:\Gregal` y abre la configuración de la sala; en uno con Gregal lo actualiza **sin tocar `appsettings.json` ni la base de datos**. |
| `Gregal-<versión>-win-x64.zip` | Instalación completa sin instalador (por si el antivirus bloquea el `.exe`). |
| `Gregal-<versión>-actualizacion-sin-config.zip` | Actualización a mano: no lleva `appsettings.json` ni ninguna `.FDB`, así que se puede copiar entero encima de la instalación. |

**En la sala, todo con doble clic.** No hace falta escribir comandos: el
instalador deja en el escritorio **«Actualizar Gregal»** y **«Configurar
Gregal»** (y la carpeta de Gregal tiene `Actualizar.cmd`, `Configurar.cmd` y
`LEEME.txt`, la guía rápida de una página). Los `.ps1` sueltos no se pueden
ejecutar con `.\script.ps1` en un Windows normal (bloqueo de scripts): usa los
accesos directos o los `.cmd`, que lo saltan.

**Actualizar con un clic:** a partir de la 1.0.10, **«Actualizar Gregal»**
(`Buscar-Actualizacion.ps1`) mira en GitHub si hay una versión más nueva que
la instalada, enseña sus cambios y, si se acepta, descarga `Gregal-Setup` y lo
ejecuta en silencio: conserva `appsettings.json` y la base de datos, y vuelve a
arrancar el servicio (o Gregal en consola). Pide permisos de administrador solo
si hay algo que instalar. Gregal se para unos segundos: mejor no hacerlo con
una partida en marcha. Una sala con una versión anterior lo tendrá después de
actualizarla una vez a mano.

El `.exe` no está firmado: Windows puede mostrar el aviso de SmartScreen
(«Más información» → «Ejecutar de todas formas») y algún antivirus lo marca
como virus por error. En ese caso, usa los zip.

**Configurar Gregal** (a partir de la 1.0.11; `Instalar-Gregal.ps1`) sirve
para la primera configuración y para cualquier cambio posterior: cada pregunta
propone lo que ya hay en `appsettings.json` (Intro lo deja igual), y pide
permisos de administrador él solo.

**Instalación con el zip completo:** descomprímelo en la carpeta definitiva y
haz doble clic en `Configurar.cmd`. Comprueba que
Firebird responde, pregunta el puerto COM, el fabricante, la sala, la concesión
(propone `999` en las salas MCB), la base de datos y la contraseña de SYSDBA, escribe `appsettings.json`, comprueba que Gregal puede usar
esa base de datos y registra el servicio (o, con `-AlIniciarSesion` o
`-SinServicio`, deja Gregal en consola: ver más abajo). Si ya tienes un `appsettings.json` de esa sala,
cópialo encima en lugar de ejecutar el script.

**Comprobar la base de datos en cualquier momento:** desde la carpeta de
Gregal, `.\Gregal.exe --comprobar-bd`. Con la configuración de
`appsettings.json`, dice si conecta con Firebird y si la tabla tiene todas las
columnas que Gregal escribe.

**Actualización a mano:** para Gregal, copia encima el contenido del zip de
actualización y vuelve a arrancarlo. `Actualizar-Gregal.ps1` (incluido en los
zip, pero no en la carpeta instalada desde la 1.0.11) hace lo mismo con copia
de seguridad, ejecutándolo desde la versión nueva descomprimida en otra
carpeta.

### Servicio de Windows o consola

El instalador y «Configurar Gregal» preguntan cómo arranca Gregal (para
cambiarlo después, basta con volver a abrir «Configurar Gregal»):

- **Servicio de Windows**: arranca aunque nadie entre al escritorio, sin ventana.
- **Ventana de consola al iniciar Windows** (la opción por defecto, lo normal en
  nuestras salas, cuyos PCs entran solos al escritorio; `-AlIniciarSesion` en
  `Instalar-Gregal.ps1`): pone el acceso directo `Gregal.lnk` en la carpeta
  Inicio de todos los usuarios
  (`C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp`). Solo se abre
  cuando alguien inicia sesión: si el PC arranca y se queda en la pantalla de
  inicio de sesión, Gregal no recibe partidas (configura el inicio de sesión
  automático de Windows o usa el servicio).
- **Ventana de consola a mano** (`-SinServicio`): solo el acceso directo del
  escritorio.

Al cambiar de modo, «Configurar Gregal» quita el servicio o los accesos
directos de Inicio que sobren (también uno puesto a mano en la carpeta Inicio
del usuario) y cierra un Gregal de consola abierto, para que nunca haya dos
compitiendo por el puerto COM.

Como servicio (`GregalReceiverService`) arranca solo al encender el PC aunque
nadie inicie sesión, y Windows lo puede reiniciar si se cae:

```powershell
sc.exe create GregalReceiverService binPath= "C:\Gregal\Gregal.exe" start= auto DisplayName= "Gregal - Receptor de Bingo"
sc.exe failure GregalReceiverService reset= 86400 actions= restart/60000/restart/60000/restart/60000
Start-Service GregalReceiverService
Get-Service GregalReceiverService
```

Sin servicio, se arranca con el acceso directo «Gregal» del escritorio, o en
una consola (`Gregal.exe`, desde su carpeta), que muestra al arrancar un resumen del PC, los puertos COM detectados y la configuración. En
ese caso: no ejecutes a la vez el servicio (el puerto COM solo lo puede abrir
un programa), desactiva el «Modo de edición rápida» de la consola (un clic en
la ventana congela Gregal hasta pulsar Esc) y recuerda que se para al cerrar
la ventana o apagar el PC.

### Comprobar el puerto de la máquina

Con Gregal parado, este script escucha 3 segundos en cada puerto. La máquina
manda `*f#` cada segundo entre partidas, así que el puerto bueno es el que
recibe eso:

```powershell
foreach ($p in [System.IO.Ports.SerialPort]::GetPortNames()) {
    $sp = New-Object System.IO.Ports.SerialPort $p, 9600, 'None', 8, 'One'
    try { $sp.Open(); Start-Sleep 3; $d = $sp.ReadExisting(); $sp.Close(); "{0}: {1} bytes  {2}" -f $p, $d.Length, ($d -replace '[^\x20-\x7E]', '.') }
    catch { "{0}: no se puede abrir ({1})" -f $p, $_.Exception.Message }
}
```

## Configuración (`appsettings.json`)

Está junto a `Gregal.exe`. Los cambios se aplican al reiniciar Gregal.

| Opción | Qué es |
|---|---|
| `TagProtocolSettings:Enabled` | Activa el protocolo de eventos (por defecto `true`). |
| `TagProtocolSettings:ComPort` | Puerto serie de la máquina de bingo, p. ej. `COM1`. |
| `TagProtocolSettings:BaudRate` | Velocidad (9600). |
| `TagProtocolSettings:DefaultCodSala` / `DefaultCodConc` | Código de sala y de concesión. El protocolo no los lleva: tienen que ser los reales de la sala y distintos de 0, o las partidas se rechazan. |
| `TagProtocolSettings:Fabricante` | `Metronia` o `MCB`. En MCB los códigos `A`, `M` y `p` son ruido y se ignoran; en Metronia son premios. |
| `TagProtocolSettings:LogRawFrames` | Escribe en el log cada evento recibido. Útil al poner en marcha una sala; después, `false`. |
| `TagProtocolSettings:FallbackFilePath` | Fichero donde van las partidas que no se pudieron guardar en Firebird. |
| `DatabaseSettings:ConnectionString` | Cadena de conexión de Firebird; `Database=` es la ruta del `.FDB`. Lleva `Charset=ISO8859_1` para que los acentos se guarden bien. |
| `DatabaseSettings:TableName` | Tabla destino (`HISTORICO`). |
| `DatabaseSettings:MaxLongitudBolas` | Longitud de la columna `BOLAS` (255; 300 si se amplió). Ver más abajo. |
| `NetworkSettings:Enabled` | Activa el protocolo JSON/CSV antiguo (por defecto `false`). |

La cadena de conexión incluye la contraseña de Firebird en texto plano. El
`appsettings.json` del repositorio (que es público) lleva `masterkey`, la de
Firebird recién instalado: el instalador pregunta la de cada sala y la escribe
en el fichero de esa sala.

## Protocolo de eventos

Cada evento llega como `*<código><valor>#`, por ejemplo `*E19#` (ha salido la
bola 19). Una partida son cientos de eventos; Gregal los acumula en memoria y
guarda la partida al recibir `*F#`. Nunca responde por el puerto.

| Código | Significado |
|---|---|
| `T` | Número de partida del día. Marca el inicio de la partida: `FECHA` toma la fecha y hora de ese momento, y `HORA_VENTA` y `HORA_CIERRE` la hora. `HORA_FIN` es la hora del PC al guardar la partida. |
| `C` / `V` / `P` / `U` | Precio del cartón (céntimos), cartones vendidos, primer y último cartón. `VENTA` no llega: Gregal la calcula como vendidos × precio. |
| `X` / `J` | Serie (o dos series empalmadas) y código de letra de la serie. MCB no manda `X`: la serie llega en `z` (serie 1) e `y` (serie 2 si hay empalme; `0` si no). |
| `K` | Decimales de los importes (normalmente 2). |
| `L` / `B` | Premio de línea y de bingo (céntimos). `p` / `A` / `M` en Metronia: línea plus, prima y bola tope de prima. |
| `E<bola>` / `Z<bola>` | Bola extraída / bola anulada. `E101` es la bola plus: no cuenta como bola extraída; en `PREMIO17` se guarda cuántas bolas habían salido cuando llegó. |
| `D<tipo><cartón>` / `u` | Canto de línea / canto de línea erróneo. La hora del primer canto va a `HORA_LINEA`. |
| `H<cartón>` / `h` | Canto de bingo / canto de bingo erróneo. La hora del primer canto va a `HORA_BINGO`. `H` sin valor marca el paso a la fase de bingo. |
| `Y<cartón>` | Canto en el formato más reciente: línea antes del `H` sin valor, bingo después. |
| `e<datos binarios>` | Estado completo de la partida (bolas salidas), cada segundo durante la partida. |
| `f` | Latido entre partidas, cada segundo. |
| `F` | Fin de partida: se guarda. |

Comportamiento ante situaciones reales:

- **Empezar a escuchar a mitad de partida.** La partida no tendrá `T`: se
  guarda con el número anterior a la `T` de la siguiente partida, con las
  bolas perdidas recuperadas del evento `e`, y se marca en `RECLAMACION`
  (`PARTIDA A MEDIAS: Nº DEDUCIDO, SIN CABECERA`). Si la siguiente es la
  `T1`, se descarta con un aviso.
- **MCB manda dos `*F#`** al acabar cada partida: el segundo se ignora.
- **Se pierde un `*F#`:** el siguiente `*f#` cierra la partida.
- **Tramas dañadas:** un `#` perdido no mezcla dos eventos; los valores con
  basura no se interpretan (se avisa en el log); una bola repetida o fuera de
  1-90 (salvo la bola plus, `E101`) se ignora; un evento `e` con checksum incorrecto se descarta.

## Cómo se guarda en la base de datos

Todo el acceso a Firebird está en `Gregal/Data/HistoricoRepository.cs`
(`SaveHistoricoAsync`).

**Una transacción por partida, no por evento.** Gregal va acumulando la
partida en memoria (`PartidaAccumulator`) sin tocar la base de datos. Solo al
recibir el `*F#` monta la fila completa y la pasa a una cola de guardado, que
atiende un hilo aparte: la lectura del puerto serie nunca espera a Firebird.

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
bloquearlo. La tabla tiene que tener las mismas columnas y la misma clave que
`HISTORICO`.

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

## Logs y diagnóstico

Los logs están en la carpeta `logs` junto a `Gregal.exe`, un fichero por día
(`gregal<AAAAMMDD>.log`), y se conservan los últimos 31.

- La primera línea de cada arranque indica la versión y el modo:
  `Gregal 1.0.3+23cea73... arrancando (servicio de Windows)`.
- Si Gregal se cierra por un error inesperado, la causa queda como
  `[FTL] Excepción no controlada...`. Si el log se corta sin línea de cierre
  y sin `[FTL]`, el proceso terminó desde fuera (apagado o corte de corriente).
- Con `LogRawFrames` activo hay una línea por evento (unos 37 KB por partida).

```powershell
Get-Content (Get-ChildItem C:\Gregal\logs\*.log | Sort-Object LastWriteTime | Select-Object -Last 1) -Tail 20 -Wait
```

## Desarrollo

```powershell
dotnet build                                       # compilar
dotnet test                                        # tests (xUnit)
dotnet run --project Gregal                        # ejecutar en consola
.\Instalador\Crear-Instalador.ps1                  # publicar y generar el instalador (Inno Setup 6)
```

- **Bases de datos:** `Gregal/Data/GREGAL_EMPTY.FDB` es la plantilla vacía que
  se copia a cada sala nueva (versionada; nunca escribas datos en ella). Para
  desarrollo, copia la plantilla como `Gregal/Data/GREGAL_DEV.FDB` (ignorada
  por git); `dotnet run` la usa a través de `appsettings.Development.json`.
  La contraseña real va en *user secrets*, fuera del repositorio:
  `dotnet user-secrets set "DatabaseSettings:ConnectionString" "User=SYSDBA;Password=...;Database=...GREGAL_DEV.FDB;..."`
  desde `Gregal/`.
- **Pruebas sin máquina:** con [com0com](https://com0com.sourceforge.net/)
  se crea una pareja de puertos virtuales (p. ej. COM8 ↔ COM9). Gregal escucha
  en uno y [Hercules](https://www.hw-group.com/software/hercules-setup-utility)
  envía tramas por el otro (en Hercules, `##` envía un `#`):
  `*T901##*C200##*L1200##*E10##*E20##*D11234##*H##*E30##*H5678##*F##`.
- **Capturas reales** de una sala Metronia y otra MCB en `Gregal/Docs/`; los
  tests las reproducen enteras.
- **Nueva versión:** sube `<Version>` en `Gregal/Gregal.csproj`, ejecuta
  `Crear-Instalador.ps1` y publica el resultado de `Instalador/Output/` como
  Release.
- Las migraciones de `Gregal/Docs/Migraciones/` no se aplican solas: hay que
  ejecutarlas a mano en cada base de datos ya desplegada.

### Estructura

```
Gregal/
├── Program.cs        Arranque, logs, receptores activos y banner de consola
├── Config/           Opciones de appsettings.json
├── Networking/       Receptores: protocolo de eventos (RS232), TCP y serie JSON/CSV
├── Parsing/          Troceo y acumulación de eventos, cadena de estado, parsers JSON/CSV
├── Data/             Guardado en Firebird (HistoricoRepository)
├── Models/           HistoricoDTO y tipos auxiliares
├── Protocol/         Respuestas del protocolo JSON/CSV
├── Diagnostics/      Datos del PC para el banner
├── Deploy/           Scripts de la sala (Instalar-Gregal/Configurar, Buscar-Actualizacion/Actualizar, Actualizar-Gregal) y LEEME.txt
└── Docs/             Especificaciones, capturas reales y migraciones SQL
Gregal.Tests/         Tests (xUnit)
Instalador/           Instalador Inno Setup (Gregal.iss) y script para generarlo
```
