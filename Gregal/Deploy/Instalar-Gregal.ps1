<#
    Configuración de Gregal (Receptor de Bingo): la primera vez en una sala
    nueva y siempre que haya que cambiar algo (puerto COM, sala, fabricante,
    base de datos, contraseña o cómo arranca Gregal).

    Lo normal es abrirlo con doble clic en "Configurar Gregal" (escritorio) o
    en Configurar.cmd (carpeta de Gregal), que se saltan el bloqueo de scripts
    de Windows. Si no tiene permisos de administrador, los pide él solo. Cada
    pregunta propone lo que ya hay en appsettings.json: Intro lo deja igual.
    Tiene que estar en la MISMA carpeta que Gregal.exe.

        .\Instalar-Gregal.ps1                 # pregunta cómo arrancar Gregal
        .\Instalar-Gregal.ps1 -ConServicio    # servicio de Windows
        .\Instalar-Gregal.ps1 -AlIniciarSesion  # consola, al iniciar sesión en Windows
        .\Instalar-Gregal.ps1 -SinServicio    # consola, se arranca a mano (acceso directo)

    Con servicio (lo normal en una sala), Gregal arranca solo al encender el
    PC, sin nadie conectado. -AlIniciarSesion abre la versión de consola cada
    vez que alguien inicia sesión en Windows (acceso directo en la carpeta
    Inicio de todos los usuarios): necesita que el PC inicie sesión, a mano o
    con inicio de sesión automático. Sin servicio, solo recibe partidas
    mientras alguien lo tenga abierto. Gregal-Setup pasa la opción elegida en
    el instalador.

    Requisitos en el PC de destino:
      - Windows 10/11 (x64). .NET 10 no puede instalarse en Windows 7/8/8.1.
      - .NET 10 Runtime (win-x64): ya viene incluido en la carpeta publicada
        (publicación autocontenida). Solo hace falta instalarlo aparte con
        una publicación antigua, que no lo llevaba.
      - Firebird Server 2.5 corriendo en local (puerto 3050 por defecto). Se
        comprueba al principio y, al final, con "Gregal.exe --comprobar-bd".
#>
param(
    [switch]$ConServicio,
    [switch]$SinServicio,
    [switch]$AlIniciarSesion,
    # Espera a Intro antes de cerrar la ventana (accesos directos y .cmd).
    [switch]$Pausar
)

$ErrorActionPreference = "Stop"
$InstallDir = $PSScriptRoot
$AppSettingsPath = Join-Path $InstallDir "appsettings.json"
$ExePath = Join-Path $InstallDir "Gregal.exe"

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Warn($msg) { Write-Host "! $msg" -ForegroundColor Yellow }
function Write-Ok($msg) { Write-Host "OK $msg" -ForegroundColor Green }
function Salir([int]$Codigo) {
    if ($Pausar) { Read-Host "`nPulsa Intro para cerrar esta ventana" | Out-Null }
    exit $Codigo
}

# Servicio, firewall y carpeta Inicio de todos los usuarios necesitan
# administrador: si no lo es, se vuelve a lanzar elevado (Windows pide
# confirmación) con los mismos parámetros y esta ventana se cierra.
$esAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $esAdmin) {
    $argumentos = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Pausar"
    foreach ($s in 'ConServicio', 'SinServicio', 'AlIniciarSesion') {
        if ((Get-Variable $s -ValueOnly).IsPresent) { $argumentos += " -$s" }
    }
    try { Start-Process powershell.exe -Verb RunAs -ArgumentList $argumentos }
    catch { Write-Warn "Hacen falta permisos de administrador para configurar Gregal."; Salir 1 }
    exit 0
}

function Read-WithDefault($prompt, $default) {
    $value = Read-Host "$prompt [$default]"
    if ([string]::IsNullOrWhiteSpace($value)) { return $default }
    return $value
}

# Puerto COM: enseña los de este PC con el nombre del dispositivo, acepta "1"
# como "COM1" (en una sala se respondió "1" y Gregal no abría el puerto) y,
# si no existe en el PC, avisa y vuelve a preguntar.
function Read-PuertoCom([string]$Prompt, [string]$Default) {
    $puertos = @([System.IO.Ports.SerialPort]::GetPortNames() | Sort-Object -Unique)
    if ($puertos.Count -gt 0) {
        $nombres = @{}
        Get-CimInstance Win32_PnPEntity -ErrorAction SilentlyContinue | ForEach-Object {
            if ($_.Name -match '\((COM\d+)\)') { $nombres[$Matches[1]] = $_.Name }
        }
        Write-Host "Puertos serie de este PC:"
        foreach ($p in $puertos) { Write-Host "  $p  $($nombres[$p])" }
        if ($puertos -notcontains $Default) { $Default = $puertos[0] }
    }
    else {
        Write-Warn "No se detecta ningún puerto serie en este PC (¿adaptador USB sin conectar o sin driver?)."
    }
    while ($true) {
        $puerto = (Read-WithDefault $Prompt $Default).Trim()
        if ($puerto -match '^\d+$') { $puerto = "COM$puerto" }
        elseif ($puerto -match '^com\s*(\d+)$') { $puerto = "COM$($Matches[1])" }
        if ($puertos -contains $puerto) { return $puerto }
        Write-Warn "$puerto no está en este PC."
        $usar = Read-Host "¿Usarlo de todos modos (por ejemplo, si el adaptador aún no está conectado)? (s/N)"
        if ($usar -match "^[sS]") { return $puerto }
    }
}

# ── Edición de appsettings.json con ámbito de sección ───────────────────
# No usamos ConvertFrom-Json/ConvertTo-Json porque eso no tolera los
# comentarios "//" del fichero y lo reescribiría entero perdiendo el formato.
#
# appsettings.json tiene DOS claves "ComPort" (una en NetworkSettings, otra en
# TagProtocolSettings: son puertos serie distintos a propósito, ver CLAUDE.md).
# Un reemplazo global por nombre de clave las tocaría las dos a la vez, así
# que toda edición va acotada a la sección correspondiente.
function Get-JsonSectionRange([string]$Content, [string]$Section) {
    $inicio = [System.Text.RegularExpressions.Regex]::Match($Content, "(?m)^\s*""$Section""\s*:\s*\{")
    if (-not $inicio.Success) { throw "No se encontró la sección '$Section' en appsettings.json." }

    $llaveApertura = $inicio.Index + $inicio.Length - 1
    $profundidad = 0
    for ($i = $llaveApertura; $i -lt $Content.Length; $i++) {
        if ($Content[$i] -eq '{') { $profundidad++ }
        elseif ($Content[$i] -eq '}') {
            $profundidad--
            if ($profundidad -eq 0) { return @{ Inicio = $llaveApertura; Fin = $i } }
        }
    }
    throw "No se encontró el cierre de la sección '$Section' en appsettings.json."
}

function Set-JsonScalar([string]$Content, [string]$Key, [string]$RawValue) {
    $pattern = "(?m)^(\s*""$Key""\s*:\s*)(?:""[^""]*""|\[[^\]]*\]|[^,\r\n]+)"
    $evaluator = { param($m) $m.Groups[1].Value + $RawValue }
    return [System.Text.RegularExpressions.Regex]::Replace($Content, $pattern, $evaluator)
}

function Set-JsonScalarInSection([string]$Content, [string]$Section, [string]$Key, [string]$RawValue) {
    $rango = Get-JsonSectionRange $Content $Section
    $antes = $Content.Substring(0, $rango.Inicio)
    $dentro = $Content.Substring($rango.Inicio, $rango.Fin - $rango.Inicio + 1)
    $despues = $Content.Substring($rango.Fin + 1)
    return $antes + (Set-JsonScalar $dentro $Key $RawValue) + $despues
}

function Set-JsonStringInSection([string]$Content, [string]$Section, [string]$Key, [string]$Value) {
    $escaped = $Value.Replace('\', '\\').Replace('"', '\"')
    return Set-JsonScalarInSection $Content $Section $Key "`"$escaped`""
}

# Valor actual de una clave escalar de una sección, sin comillas ($null si
# no está): se propone como respuesta por defecto en cada pregunta.
function Get-JsonValueInSection([string]$Content, [string]$Section, [string]$Key) {
    try { $rango = Get-JsonSectionRange $Content $Section } catch { return $null }
    $dentro = $Content.Substring($rango.Inicio, $rango.Fin - $rango.Inicio + 1)
    $m = [regex]::Match($dentro, "(?m)^\s*""$Key""\s*:\s*(?:""([^""]*)""|([^,\r\n/]+))")
    if (-not $m.Success) { return $null }
    if ($m.Groups[1].Success) { return $m.Groups[1].Value }
    return $m.Groups[2].Value.Trim()
}

# ── Comprobaciones previas ──────────────────────────────────────────────
Write-Step "Comprobando requisitos"

if (-not (Test-Path $ExePath)) {
    Write-Warn "No se encuentra Gregal.exe en '$InstallDir'."
    Write-Warn "Este script debe ejecutarse desde la carpeta publicada (junto a Gregal.exe)."
    Salir 1
}

# Configuración actual: cada pregunta la propone por defecto.
$actual = [System.IO.File]::ReadAllText($AppSettingsPath)
$ServicioActual = "GregalReceiverService"
$accesoInicio = Join-Path ([Environment]::GetFolderPath("CommonStartup")) "Gregal.lnk"
# El de la carpeta Inicio del usuario: el que se ponía a mano antes de que
# Gregal-Setup tuviera esta opción.
$accesoInicioUsuario = Join-Path ([Environment]::GetFolderPath("Startup")) "Gregal.lnk"
$accesoEscritorio = Join-Path ([Environment]::GetFolderPath("CommonDesktopDirectory")) "Gregal.lnk"

$dotnetOk = $false
$dotnetIncluido = Test-Path (Join-Path $InstallDir "coreclr.dll")   # publicación autocontenida
if (-not $dotnetIncluido) {
    try {
        $runtimes = & dotnet --list-runtimes 2>$null
        if ($runtimes -match "Microsoft\.NETCore\.App 10\.") { $dotnetOk = $true }
    }
    catch { }
}

if ($dotnetIncluido) {
    Write-Ok ".NET 10 Runtime incluido en la carpeta de Gregal."
}
elseif ($dotnetOk) {
    Write-Ok ".NET 10 Runtime encontrado."
}
else {
    Write-Warn ".NET 10 Runtime (win-x64) no se detecta en este PC."
    Write-Warn "Descárgalo de https://dotnet.microsoft.com/download/dotnet/10.0 antes de continuar (elige 'Runtime', no hace falta el SDK)."
    $continuar = Read-Host "¿Continuar de todos modos con la instalación? (s/N)"
    if ($continuar -notmatch "^[sS]") { Salir 1 }
}

# Firebird: servidor y puerto de la cadena de conexión de appsettings.json.
# Aquí solo se mira que responda; al final, con la configuración ya escrita,
# "Gregal.exe --comprobar-bd" comprueba la base de datos y la tabla.
function Test-Puerto([string]$Servidor, [int]$Puerto) {
    $cliente = New-Object System.Net.Sockets.TcpClient
    try { return $cliente.ConnectAsync($Servidor, $Puerto).Wait(3000) }
    catch { return $false }
    finally { $cliente.Dispose() }
}

$cadenaBd = [System.IO.File]::ReadAllText($AppSettingsPath)
$fbServidor = "localhost"; $fbPuerto = 3050
if ($cadenaBd -match 'DataSource=([^;"]+)') { $fbServidor = $Matches[1] }
if ($cadenaBd -match 'Port=(\d+)') { $fbPuerto = [int]$Matches[1] }

if (Test-Puerto $fbServidor $fbPuerto) {
    Write-Ok "Firebird responde en ${fbServidor}:$fbPuerto."
}
else {
    $fbServicios = @(Get-Service -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "*Firebird*" -or $_.DisplayName -like "*Firebird*" })
    $parados = @($fbServicios | Where-Object Status -ne "Running")
    if ($parados.Count -gt 0) {
        Write-Warn "Firebird está instalado pero parado: $(($parados | ForEach-Object Name) -join ', ')."
        $arrancar = Read-Host "¿Arrancarlo ahora? (S/n)"
        if ($arrancar -notmatch "^[nN]") {
            $parados | ForEach-Object { Start-Service $_.Name -ErrorAction SilentlyContinue }
            Start-Sleep -Seconds 2
        }
    }
    if (Test-Puerto $fbServidor $fbPuerto) {
        Write-Ok "Firebird responde en ${fbServidor}:$fbPuerto."
    }
    else {
        if ($fbServicios.Count -eq 0) {
            Write-Warn "No se encuentra ningún servidor Firebird en este PC."
            Write-Warn "Instala Firebird 2.5 de 64 bits (https://firebirdsql.org, Downloads > Firebird 2.5) como servicio."
            Write-Warn "Las bases de datos de Gregal son de Firebird 2.5: las versiones 3, 4 y 5 no las abren sin convertirlas."
        }
        else {
            Write-Warn "Firebird no responde en ${fbServidor}:$fbPuerto (servicios: $(($fbServicios | ForEach-Object { "$($_.Name)=$($_.Status)" }) -join ', '))."
        }
        Write-Warn "Sin Firebird, Gregal no podrá guardar las partidas."
        $continuar = Read-Host "¿Continuar de todos modos con la instalación? (s/N)"
        if ($continuar -notmatch "^[sS]") { Salir 1 }
    }
}

# ── Servicio de Windows o arranque a mano ───────────────────────────────
$arrancarAlIniciarSesion = $false
if ($ConServicio) { $instalarServicio = $true }
elseif ($AlIniciarSesion) { $instalarServicio = $false; $arrancarAlIniciarSesion = $true }
elseif ($SinServicio) { $instalarServicio = $false }
else {
    # Siempre se propone la 2, la ventana al iniciar Windows (lo normal en
    # nuestras salas: decidido con el usuario 2026-11-04, también en un PC
    # que esté como servicio). El modo actual solo se señala en la lista.
    $arranqueActual = $null
    if (Get-Service -Name $ServicioActual -ErrorAction SilentlyContinue) { $arranqueActual = "1" }
    elseif ((Test-Path $accesoInicio) -or (Test-Path $accesoInicioUsuario)) { $arranqueActual = "2" }
    elseif (Test-Path $accesoEscritorio) { $arranqueActual = "3" }
    $opciones = [ordered]@{
        "1" = "Servicio de Windows: arranca al encender el PC aunque nadie entre al escritorio, sin ventana"
        "2" = "Ventana de consola al iniciar Windows (el PC tiene que entrar solo al escritorio; lo normal en nuestras salas)"
        "3" = "Ventana de consola a mano, con el acceso directo del escritorio"
    }
    Write-Step "Cómo arranca Gregal"
    foreach ($n in $opciones.Keys) {
        $marca = ""
        if ($n -eq $arranqueActual) { $marca = "   <- así está ahora" }
        Write-Host "  $n) $($opciones[$n])$marca"
    }
    Write-Host "  Sin servicio, Gregal solo recibe partidas mientras su ventana esté abierta."
    do { $opcionArranque = (Read-WithDefault "Elige una opción" "2").Trim() } while ($opcionArranque -notin @("1", "2", "3"))
    $instalarServicio = $opcionArranque -eq "1"
    $arrancarAlIniciarSesion = $opcionArranque -eq "2"
}

# ── Protocolo a usar en esta sala ────────────────────────────────────────
Write-Step "Protocolo de esta sala"
Write-Host "  1) Protocolo de eventos 'tag' por RS232 (*CODIGO<valor>#) - RECOMENDADO, es el que se ha probado"
Write-Host "  2) Protocolo JSON/CSV antiguo, por TCP y/o RS232"
Write-Host "  3) Los dos a la vez (puertos COM distintos)"
$protocoloActual = "1"
if ((Get-JsonValueInSection $actual "NetworkSettings" "Enabled") -eq "true") {
    $protocoloActual = "2"
    if ((Get-JsonValueInSection $actual "TagProtocolSettings" "Enabled") -eq "true") { $protocoloActual = "3" }
}
$opcionProtocolo = Read-WithDefault "Elige una opción" $protocoloActual

$usarTag = $opcionProtocolo -in @("1", "3")
$usarJsonCsv = $opcionProtocolo -in @("2", "3")

if ($usarTag) {
    Write-Step "Configuración del protocolo de eventos (tag)"
    $comActual = Get-JsonValueInSection $actual "TagProtocolSettings" "ComPort"
    if (-not $comActual) { $comActual = "COM1" }
    elseif ($comActual -match '^\d+$') { $comActual = "COM$comActual" }
    $TagComPort = Read-PuertoCom "Puerto COM donde está conectado el hardware de la sala" $comActual

    # Hay (al menos) dos fabricantes de hardware con protocolos parecidos
    # pero no idénticos (confirmado 2026-10-14): en Metronia los códigos A/M/p
    # llevan importes reales (línea plus/prima/bola tope de prima); en MCB son
    # ruido constante del hardware y hay que ignorarlos, o Gregal guardaría
    # basura en esos premios. Ver CLAUDE.md para el detalle.
    Write-Host ""
    Write-Host "Esta sala puede tener distinto hardware: en los códigos A, M y p (línea plus/"
    Write-Host "prima/bola tope de prima) cambia el comportamiento según el fabricante:"
    Write-Host "  1) MCB      - A/M/p son ruido del hardware, Gregal los ignora (lo normal en nuestras salas)"
    Write-Host "  2) Metronia - A/M/p llevan importes reales"
    $fabricanteActual = "1"
    if ((Get-JsonValueInSection $actual "TagProtocolSettings" "Fabricante") -eq "Metronia") { $fabricanteActual = "2" }
    $opcionFabricante = Read-WithDefault "Fabricante del hardware de esta sala" $fabricanteActual
    $Fabricante = "MCB"
    if ($opcionFabricante -eq "2") { $Fabricante = "Metronia" }

    # Después del fabricante: en las salas MCB la concesión es 999. Lo que ya
    # haya configurado se respeta, salvo el 1 de la plantilla.
    $CodSalaActual = Get-JsonValueInSection $actual "TagProtocolSettings" "DefaultCodSala"
    if (-not $CodSalaActual) { $CodSalaActual = "1" }
    $CodConcPorDefecto = Get-JsonValueInSection $actual "TagProtocolSettings" "DefaultCodConc"
    if ($CodConcPorDefecto -in @($null, "", "0", "1")) {
        $CodConcPorDefecto = "1"
        if ($Fabricante -eq "MCB") { $CodConcPorDefecto = "999" }
    }
    Write-Host ""
    Write-Host "CodSala/CodConc identifican esta sala en Firebird (columnas COD_SALA/COD_CONC de HISTORICO)."
    Write-Host "Tienen que ser valores reales y distintos de 0, o Gregal rechazará guardar las partidas."
    $CodSala = Read-WithDefault "Código de sala (CodSala)" $CodSalaActual
    $CodConc = Read-WithDefault "Código de concesión (CodConc)" $CodConcPorDefecto
}

if ($usarJsonCsv) {
    Write-Step "Configuración del protocolo JSON/CSV (red y puerto serie)"
    $ServiceName = Read-WithDefault "Nombre del servicio de Windows" "GregalReceiverService"
    $TcpPort = Read-WithDefault "Puerto TCP" "8080"
    $TcpAddress = Read-WithDefault "Dirección de escucha TCP (0.0.0.0 = todas las interfaces)" "0.0.0.0"
    $AllowedIpsRaw = Read-WithDefault "IPs autorizadas por TCP, separadas por coma (vacío = sin restricción)" "127.0.0.1"
    $NetComPort = Read-PuertoCom "Puerto COM de este protocolo (distinto del de arriba si usas los dos)" "COM8"
    $Handshake = Read-WithDefault "Control de flujo serie (None / RequestToSend)" "None"
}
if (-not $ServiceName) {
    if ($instalarServicio) { $ServiceName = Read-WithDefault "Nombre del servicio de Windows" "GregalReceiverService" }
    else { $ServiceName = "GregalReceiverService" }
}

Write-Step "Base de datos Firebird"
# La ruta actual si ese fichero existe (p. ej. C:\DatosCV\aamslog.FDB); si
# no, la de la carpeta de Gregal, donde se copia la plantilla vacía.
$DefaultDbPath = Join-Path $InstallDir "Data\GREGAL.FDB"
$dbActual = [regex]::Match($actual, '(?<="ConnectionString"\s*:\s*"[^"]*)Database=([^;"]*)').Groups[1].Value.Replace('\\', '\')
if ($dbActual -and (Test-Path $dbActual)) { $DefaultDbPath = $dbActual }
$DbPath = Read-WithDefault "Ruta completa del fichero .FDB de esta sala" $DefaultDbPath

# Contraseña de SYSDBA: el appsettings.json publicado ya lleva la de las
# salas, así que lo normal es Intro (deja la que tenga el fichero). Se pide
# sin mostrarla. Solo dentro de "ConnectionString": los comentarios del
# fichero también pueden hablar de la contraseña.
$PatronPassword = '(?<="ConnectionString"\s*:\s*"[^"]*)Password=[^;"]*'
$passActual = [regex]::Match([System.IO.File]::ReadAllText($AppSettingsPath), $PatronPassword).Value -replace '^Password=', ''
$pistaPass = "Intro = dejar la actual"
while ($true) {
    $seguro = Read-Host "Contraseña de SYSDBA en el Firebird de este PC ($pistaPass)" -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($seguro)
    try { $DbPass = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
    if ([string]::IsNullOrEmpty($DbPass)) { $DbPass = $passActual; break }
    # Romperían la cadena de conexión o el JSON.
    if ($DbPass -notmatch '[;"]') { break }
    Write-Warn "La contraseña no puede llevar ';' ni comillas. Cámbiala en Firebird (gsec) o escríbela de nuevo."
}

$DbDir = Split-Path $DbPath -Parent
if (-not (Test-Path $DbDir)) { New-Item -ItemType Directory -Path $DbDir -Force | Out-Null }

if (-not (Test-Path $DbPath)) {
    $Template = Join-Path $InstallDir "GREGAL_EMPTY.FDB"
    if (Test-Path $Template) {
        Copy-Item $Template $DbPath
        Write-Ok "Copiada la base de datos vacía de plantilla a '$DbPath'."
    }
    else {
        Write-Warn "No existe '$DbPath' y no se encontró una plantilla GREGAL_EMPTY.FDB junto al instalador."
        Write-Warn "Copia manualmente el fichero .FDB de esta sala en esa ruta antes de arrancar el servicio."
    }
}

# ── Escribir appsettings.json ────────────────────────────────────────────
Write-Step "Actualizando appsettings.json"

$content = [System.IO.File]::ReadAllText($AppSettingsPath)

$tagEnabledValue = "false"
if ($usarTag) { $tagEnabledValue = "true" }
$content = Set-JsonScalarInSection $content "TagProtocolSettings" "Enabled" $tagEnabledValue
if ($usarTag) {
    $content = Set-JsonStringInSection $content "TagProtocolSettings" "ComPort" $TagComPort
    $content = Set-JsonScalarInSection $content "TagProtocolSettings" "DefaultCodSala" $CodSala
    $content = Set-JsonScalarInSection $content "TagProtocolSettings" "DefaultCodConc" $CodConc

    $content = Set-JsonStringInSection $content "TagProtocolSettings" "Fabricante" $Fabricante
}

$networkEnabledValue = "false"
if ($usarJsonCsv) { $networkEnabledValue = "true" }
$content = Set-JsonScalarInSection $content "NetworkSettings" "Enabled" $networkEnabledValue
if ($usarJsonCsv) {
    $allowedIpsArray = ($AllowedIpsRaw -split ",") | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" }
    $allowedIpsJson = "[ " + (($allowedIpsArray | ForEach-Object { "`"$_`"" }) -join ", ") + " ]"

    $content = Set-JsonScalarInSection $content "NetworkSettings" "TcpPort" $TcpPort
    $content = Set-JsonStringInSection $content "NetworkSettings" "TcpAddress" $TcpAddress
    $content = Set-JsonScalarInSection $content "NetworkSettings" "AllowedIps" $allowedIpsJson
    $content = Set-JsonStringInSection $content "NetworkSettings" "ComPort" $NetComPort
    $content = Set-JsonStringInSection $content "NetworkSettings" "SerialHandshake" $Handshake
}

# Sustituye solo los segmentos Database=...; y Password=... dentro de la
# cadena de conexión de Firebird, manteniendo usuario/servidor/puerto tal como estén.
$dbPathEscaped = $DbPath.Replace('\', '\\')
$content = [System.Text.RegularExpressions.Regex]::Replace(
    $content, "Database=[^;""]*;", { param($m) "Database=$dbPathEscaped;" })
$dbPassEscaped = $DbPass.Replace('\', '\\')
$content = [System.Text.RegularExpressions.Regex]::Replace(
    $content, $PatronPassword, { param($m) "Password=$dbPassEscaped" })

[System.IO.File]::WriteAllText($AppSettingsPath, $content, (New-Object System.Text.UTF8Encoding($false)))
Write-Ok "appsettings.json actualizado."

# ── Comprobar la base de datos con la configuración ya escrita ──────────
# Gregal.exe se conecta como lo hará al guardar una partida: servidor,
# fichero .FDB, usuario/contraseña, tabla y columnas.
Write-Step "Comprobando la base de datos"
# Con -Password, prueba esa contraseña sin tocar appsettings.json (variable
# de entorno, que Gregal lee por encima del fichero).
function Test-BaseDeDatos([string]$Password) {
    if ($Password) {
        $cadena = [regex]::Match($content, '"ConnectionString"\s*:\s*"([^"]*)"').Groups[1].Value.Replace('\\', '\')
        $env:DatabaseSettings__ConnectionString = [regex]::Replace($cadena, 'Password=[^;]*', { param($m) "Password=$Password" })
    }
    Push-Location $InstallDir
    try { $salida = & $ExePath --comprobar-bd; $codigo = $LASTEXITCODE }
    finally { Pop-Location; Remove-Item Env:DatabaseSettings__ConnectionString -ErrorAction SilentlyContinue }
    return @{ Salida = @($salida); Codigo = $codigo }
}

# gsec.exe de la carpeta bin del Firebird de este PC (la del servicio).
function Get-Gsec {
    $carpetas = @(Get-CimInstance Win32_Service -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "Firebird*" -and $_.PathName -match '(fbserver|fb_inet_server|firebird)\.exe' } |
        ForEach-Object { Split-Path ($_.PathName -replace '^"?([^"]+?\.exe)"?.*$', '$1') -Parent })
    $carpetas += "C:\Firebird\bin", "$env:ProgramFiles\Firebird\Firebird_2_5\bin", "${env:ProgramFiles(x86)}\Firebird\Firebird_2_5\bin"
    foreach ($c in $carpetas) { if ($c -and (Test-Path (Join-Path $c "gsec.exe"))) { return Join-Path $c "gsec.exe" } }
    return $null
}

$prueba = Test-BaseDeDatos
$prueba.Salida | ForEach-Object { Write-Host "  $_" }
$codigoBd = $prueba.Codigo

# Firebird recién instalado: sigue con masterkey y no con la contraseña de
# las salas. Si es así, se ofrece cambiarla en Firebird (gsec), para no
# tener que escribir comandos. Solo con el Firebird de este mismo PC.
$fbLocal = $fbServidor -in @("localhost", "127.0.0.1", $env:COMPUTERNAME)
if ($codigoBd -ne 0 -and $fbLocal -and $DbPass -ne "masterkey" -and
    ($prueba.Salida -join "`n") -match "user name and password are not defined") {
    $usuarioBd = [regex]::Match($content, '(?<="ConnectionString"\s*:\s*"[^"]*)User=([^;"]*)').Groups[1].Value
    if (-not $usuarioBd) { $usuarioBd = "SYSDBA" }
    if ((Test-BaseDeDatos "masterkey").Codigo -eq 0) {
        # Sin preguntar (decidido con el usuario 2026-11-04): se pone la de
        # las salas.
        Write-Host ""
        Write-Host "El Firebird de este PC tiene todavía la contraseña de fábrica (masterkey): se cambia por la de las salas."
        if ($true) {
            $gsec = Get-Gsec
            if (-not $gsec) {
                Write-Warn "No se encuentra gsec.exe en la carpeta bin de Firebird: cámbiala a mano."
            }
            else {
                $eap = $ErrorActionPreference; $ErrorActionPreference = "Continue"
                $salidaGsec = & $gsec -user $usuarioBd -password masterkey -modify $usuarioBd -pw $DbPass 2>&1
                $codigoGsec = $LASTEXITCODE
                $ErrorActionPreference = $eap
                if ($codigoGsec -eq 0) {
                    Write-Ok "Contraseña de $usuarioBd cambiada en Firebird."
                    if ($DbPass.Length -gt 8) { Write-Warn "Firebird 2.5 solo usa los 8 primeros caracteres de la contraseña." }
                    $prueba = Test-BaseDeDatos
                    $prueba.Salida | ForEach-Object { Write-Host "  $_" }
                    $codigoBd = $prueba.Codigo
                }
                else {
                    Write-Warn "gsec no pudo cambiarla: $($salidaGsec -join ' ')"
                }
            }
        }
    }
}

if ($codigoBd -eq 0) {
    Write-Ok "Base de datos lista."
}
else {
    Write-Warn "Gregal no podrá guardar partidas con esta configuración hasta que se corrija lo anterior"
    Write-Warn "(Firebird, ruta del .FDB, contraseña de SYSDBA o columnas de la tabla)."
    $continuar = Read-Host "¿Continuar de todos modos? (s/N)"
    if ($continuar -notmatch "^[sS]") {
        Write-Warn "Configuración detenida. appsettings.json ya está escrito: corrige lo anterior y vuelve a abrir 'Configurar Gregal'."
        Salir 1
    }
}

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

# Con la carpeta de Gregal como carpeta de trabajo: en consola, Gregal lee
# appsettings.json de la carpeta desde la que se arranca.
function New-AccesoDirecto([string]$Ruta) {
    $shell = New-Object -ComObject WScript.Shell
    $lnk = $shell.CreateShortcut($Ruta)
    $lnk.TargetPath = $ExePath
    $lnk.WorkingDirectory = $InstallDir
    $lnk.Description = "Gregal - Receptor de Bingo"
    $lnk.Save()
}

# Accesos directos de la carpeta Inicio: se quitan en cualquier otro modo
# para que no arranquen dos Gregal (servicio y consola) peleándose por el
# mismo puerto COM. El del usuario se quita siempre: con el modo 2 queda el
# de todos los usuarios.
foreach ($acceso in $accesoInicio, $accesoInicioUsuario) {
    if ((Test-Path $acceso) -and (-not $arrancarAlIniciarSesion -or $acceso -eq $accesoInicioUsuario)) {
        Remove-Item $acceso -Force
        Write-Ok "Quitado el arranque de Gregal al iniciar sesión ($acceso)."
    }
}

# Un Gregal de consola abierto tiene el puerto COM y la configuración vieja:
# se cierra (con servicio, para siempre; sin él, se ofrece abrirlo al final).
$consolaAbierta = @(Get-Process Gregal -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $ExePath })
if ($consolaAbierta.Count -gt 0) {
    Write-Warn "Gregal está abierto en una ventana: se cierra para aplicar la configuración nueva."
    $consolaAbierta | Stop-Process -Force
    Start-Sleep -Seconds 2
}

if ($instalarServicio) {
    # ── Registrar como servicio de Windows ──────────────────────────────
    Write-Step "Registrando el servicio de Windows '$ServiceName'"

    if ($existing) {
        Write-Warn "Ya existe un servicio '$ServiceName'. Se detiene y se vuelve a crear con la ruta actual."
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        & sc.exe delete $ServiceName | Out-Null
        Start-Sleep -Seconds 1
    }

    & sc.exe create $ServiceName binPath= "`"$ExePath`"" start= auto DisplayName= "Gregal - Receptor de Bingo" | Out-Null
    Write-Ok "Servicio '$ServiceName' creado."
}
else {
    # ── Sin servicio: acceso directo para arrancarlo a mano ─────────────
    Write-Step "Gregal sin servicio de Windows"
    if ($existing) {
        # Si se quedara, los dos Gregal se pelearían por el mismo puerto COM.
        Write-Warn "Había un servicio '$ServiceName': se detiene y se elimina."
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        & sc.exe delete $ServiceName | Out-Null
    }
    New-AccesoDirecto $accesoEscritorio
    Write-Ok "Acceso directo creado en el escritorio: $accesoEscritorio"
    if ($arrancarAlIniciarSesion) {
        New-AccesoDirecto $accesoInicio
        Write-Ok "Gregal arrancará al iniciar sesión en Windows: $accesoInicio"
    }
}

# ── Regla de firewall para el puerto TCP (solo si se usa el protocolo JSON/CSV) ──
if ($usarJsonCsv) {
    Write-Step "Configurando regla de firewall para el puerto TCP $TcpPort"

    $fwRuleName = "Gregal TCP $TcpPort"
    if (-not (Get-NetFirewallRule -DisplayName $fwRuleName -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -DisplayName $fwRuleName -Direction Inbound -Protocol TCP -LocalPort $TcpPort -Action Allow | Out-Null
        Write-Ok "Regla de firewall creada."
    }
    else {
        Write-Ok "La regla de firewall ya existía."
    }
}
else {
    Write-Ok "Solo protocolo tag por serie: no hace falta abrir ningún puerto en el firewall."
}

if (-not $instalarServicio) {
    Write-Step "Configuración terminada (sin servicio)"
    if ($arrancarAlIniciarSesion) {
        Write-Host "Gregal se abrirá cada vez que se inicie Windows. Si el PC se queda en la pantalla de inicio"
        Write-Host "de sesión, Gregal no se abre: configura el inicio de sesión automático o usa el servicio."
    }
    Write-Host "Gregal solo recibe partidas mientras está abierto: si se cierra, arráncalo con el acceso directo 'Gregal' del escritorio"
    Write-Host "y deja la ventana abierta (se cierra con Ctrl+C)."
    $arrancar = Read-Host "¿Arrancar Gregal ahora? (S/n)"
    if ($arrancar -notmatch "^[nN]") { Start-Process -FilePath $ExePath -WorkingDirectory $InstallDir }
    Salir 0
}

# ── Arrancar el servicio ──────────────────────────────────────────────────
Write-Step "Arrancando el servicio"
Start-Service -Name $ServiceName
Start-Sleep -Seconds 3
$status = (Get-Service -Name $ServiceName).Status
Write-Host "Estado del servicio: $status"

$logsDir = Join-Path $InstallDir "logs"
$lastLog = Get-ChildItem $logsDir -Filter "*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($lastLog) {
    Write-Step "Últimas líneas del log ($($lastLog.Name))"
    Get-Content $lastLog.FullName -Tail 15
}
else {
    Write-Warn "Todavía no hay ningún fichero de log en '$logsDir'."
}

Write-Step "Configuración terminada"
if ($usarTag) {
    Write-Host "Revisa el log anterior: debería decir 'Puerto serie (protocolo tag) abierto: $TagComPort'."
    Write-Host "Fabricante configurado: $Fabricante."
}
if ($usarJsonCsv) {
    Write-Host "Revisa el log anterior: debería decir 'Servidor TCP iniciado' y, si usas su puerto serie, 'Puerto serie abierto'."
}
Write-Host "Si un puerto serie falla, comprueba que el ComPort configurado es el correcto en este PC y que ningún otro programa lo tiene abierto."
Salir 0
