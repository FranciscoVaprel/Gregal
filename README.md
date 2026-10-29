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
- [Runtime de .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0)
  (basta el *Runtime*, no hace falta el SDK).
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

El `.exe` no está firmado: Windows puede mostrar el aviso de SmartScreen
(«Más información» → «Ejecutar de todas formas») y algún antivirus lo marca
como virus por error. En ese caso, usa los zip.

**Instalación con el zip completo:** descomprímelo en la carpeta definitiva y,
en PowerShell como administrador, ejecuta `.\Instalar-Gregal.ps1`. Pregunta el
puerto COM, la sala, la concesión, el fabricante y la base de datos, escribe
`appsettings.json` y registra el servicio. Si ya tienes un `appsettings.json`
de esa sala, cópialo encima en lugar de ejecutar el script.

**Actualización a mano:** para Gregal, copia encima el contenido del zip de
actualización y vuelve a arrancarlo. `Actualizar-Gregal.ps1` (incluido en los
zip) hace lo mismo con copia de seguridad, ejecutándolo desde la versión nueva
descomprimida en otra carpeta.

### Servicio de Windows o consola

Como servicio (`GregalReceiverService`, lo crea el instalador) arranca solo al
encender el PC aunque nadie inicie sesión, y Windows lo puede reiniciar si se
cae:

```powershell
sc.exe create GregalReceiverService binPath= "C:\Gregal\Gregal.exe" start= auto DisplayName= "Gregal - Receptor de Bingo"
sc.exe failure GregalReceiverService reset= 86400 actions= restart/60000/restart/60000/restart/60000
Start-Service GregalReceiverService
Get-Service GregalReceiverService
```

También puede ejecutarse en una consola (`Gregal.exe`), que muestra al
arrancar un resumen del PC, los puertos COM detectados y la configuración. En
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

La cadena de conexión incluye la contraseña de Firebird en texto plano.

## Protocolo de eventos

Cada evento llega como `*<código><valor>#`, por ejemplo `*E19#` (ha salido la
bola 19). Una partida son cientos de eventos; Gregal los acumula en memoria y
guarda la partida al recibir `*F#`. Nunca responde por el puerto.

| Código | Significado |
|---|---|
| `T` | Número de partida del día. |
| `C` / `V` / `P` / `U` | Precio del cartón (céntimos), cartones vendidos, primer y último cartón. |
| `X` / `J` | Serie (o dos series empalmadas) y código de letra de la serie. |
| `K` | Decimales de los importes (normalmente 2). |
| `L` / `B` | Premio de línea y de bingo (céntimos). `p` / `A` / `M` en Metronia: línea plus, prima y bola tope de prima. |
| `E<bola>` / `Z<bola>` | Bola extraída / bola anulada. |
| `D<tipo><cartón>` / `u` | Canto de línea / canto de línea erróneo. |
| `H<cartón>` / `h` | Canto de bingo / canto de bingo erróneo. `H` sin valor marca el paso a la fase de bingo. |
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
  1-90 se ignora; un evento `e` con checksum incorrecto se descarta.

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
├── Deploy/           Instalar-Gregal.ps1 y Actualizar-Gregal.ps1
└── Docs/             Especificaciones, capturas reales y migraciones SQL
Gregal.Tests/         Tests (xUnit)
Instalador/           Instalador Inno Setup (Gregal.iss) y script para generarlo
```
