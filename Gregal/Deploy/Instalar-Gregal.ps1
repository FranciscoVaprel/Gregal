#Requires -RunAsAdministrator
<#
    Instalador de Gregal (Receptor de Bingo) para una sala nueva.

    Uso: este script debe estar en la MISMA carpeta que Gregal.exe (la carpeta
    publicada con `dotnet publish`). Ejecutarlo con "Ejecutar con PowerShell"
    como Administrador, o desde una consola de PowerShell elevada:

        .\Instalar-Gregal.ps1

    Requisitos en el PC de destino:
      - Windows 10/11 (x64). .NET 10 no puede instalarse en Windows 7/8/8.1.
      - .NET 10 Runtime (win-x64) instalado.
      - Firebird Server corriendo en local (puerto 3050 por defecto).
#>

$ErrorActionPreference = "Stop"
$InstallDir = $PSScriptRoot
$AppSettingsPath = Join-Path $InstallDir "appsettings.json"
$ExePath = Join-Path $InstallDir "Gregal.exe"

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Warn($msg) { Write-Host "! $msg" -ForegroundColor Yellow }
function Write-Ok($msg) { Write-Host "OK $msg" -ForegroundColor Green }

function Read-WithDefault($prompt, $default) {
    $value = Read-Host "$prompt [$default]"
    if ([string]::IsNullOrWhiteSpace($value)) { return $default }
    return $value
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

# Igual que Set-JsonScalarInSection pero para un valor que es un objeto JSON
# en varias líneas (p.ej. "PremioCodeMap": { ... }), que el patrón de
# Set-JsonScalar no captura entero (se para en el primer salto de línea).
function Set-JsonObjectInSection([string]$Content, [string]$Section, [string]$Key, [string]$RawObjectJson) {
    $rango = Get-JsonSectionRange $Content $Section
    $dentro = $Content.Substring($rango.Inicio, $rango.Fin - $rango.Inicio + 1)

    $inicioClave = [System.Text.RegularExpressions.Regex]::Match($dentro, "(?m)^(\s*""$Key""\s*:\s*)\{")
    if (-not $inicioClave.Success) { throw "No se encontró la clave de objeto '$Key' dentro de la sección '$Section'." }

    $valorInicio = $inicioClave.Index + $inicioClave.Length - 1
    $profundidad = 0
    $valorFin = -1
    for ($i = $valorInicio; $i -lt $dentro.Length; $i++) {
        if ($dentro[$i] -eq '{') { $profundidad++ }
        elseif ($dentro[$i] -eq '}') {
            $profundidad--
            if ($profundidad -eq 0) { $valorFin = $i; break }
        }
    }
    if ($valorFin -lt 0) { throw "No se encontró el cierre del objeto '$Key'." }

    $nuevoDentro = $dentro.Substring(0, $valorInicio) + $RawObjectJson + $dentro.Substring($valorFin + 1)
    $antes = $Content.Substring(0, $rango.Inicio)
    $despues = $Content.Substring($rango.Fin + 1)
    return $antes + $nuevoDentro + $despues
}

# ── Comprobaciones previas ──────────────────────────────────────────────
Write-Step "Comprobando requisitos"

if (-not (Test-Path $ExePath)) {
    Write-Warn "No se encuentra Gregal.exe en '$InstallDir'."
    Write-Warn "Este script debe ejecutarse desde la carpeta publicada (junto a Gregal.exe)."
    exit 1
}

$dotnetOk = $false
try {
    $runtimes = & dotnet --list-runtimes 2>$null
    if ($runtimes -match "Microsoft\.NETCore\.App 10\.") { $dotnetOk = $true }
}
catch { }

if ($dotnetOk) {
    Write-Ok ".NET 10 Runtime encontrado."
}
else {
    Write-Warn ".NET 10 Runtime (win-x64) no se detecta en este PC."
    Write-Warn "Descárgalo de https://dotnet.microsoft.com/download/dotnet/10.0 antes de continuar (elige 'Runtime', no hace falta el SDK)."
    $continuar = Read-Host "¿Continuar de todos modos con la instalación? (s/N)"
    if ($continuar -notmatch "^[sS]") { exit 1 }
}

# ── Protocolo a usar en esta sala ────────────────────────────────────────
Write-Step "Protocolo de esta sala"
Write-Host "  1) Protocolo de eventos 'tag' por RS232 (*CODIGO<valor>#) - RECOMENDADO, es el que se ha probado"
Write-Host "  2) Protocolo JSON/CSV antiguo, por TCP y/o RS232"
Write-Host "  3) Los dos a la vez (puertos COM distintos)"
$opcionProtocolo = Read-WithDefault "Elige una opción" "1"

$usarTag = $opcionProtocolo -in @("1", "3")
$usarJsonCsv = $opcionProtocolo -in @("2", "3")

if ($usarTag) {
    Write-Step "Configuración del protocolo de eventos (tag)"
    $TagComPort = Read-WithDefault "Puerto COM donde está conectado el hardware de la sala" "COM9"
    Write-Host "CodSala/CodConc identifican esta sala en Firebird (columnas COD_SALA/COD_CONC de HISTORICO)."
    Write-Host "Tienen que ser valores reales y distintos de 0, o Gregal rechazará guardar las partidas."
    $CodSala = Read-WithDefault "Código de sala (CodSala)" "1"
    $CodConc = Read-WithDefault "Código de concesión (CodConc)" "1"

    # Hay (al menos) dos fabricantes de hardware con protocolos parecidos
    # pero no idénticos (confirmado 2026-10-14): en Metronia los códigos A/M/p
    # llevan importes reales (línea plus/prima/bola tope de prima); en MCB son
    # ruido constante del hardware y hay que ignorarlos, o Gregal guardaría
    # basura en esos premios. Ver CLAUDE.md para el detalle.
    Write-Host ""
    Write-Host "Esta sala puede tener distinto hardware: en los códigos A, M y p (línea plus/"
    Write-Host "prima/bola tope de prima) cambia el comportamiento según el fabricante:"
    Write-Host "  1) Metronia - A/M/p llevan importes reales (RECOMENDADO si no lo sabes seguro)"
    Write-Host "  2) MCB      - A/M/p son ruido del hardware, Gregal los ignora"
    $opcionFabricante = Read-WithDefault "Fabricante del hardware de esta sala" "1"
    $UsarPremiosDeMetronia = $opcionFabricante -ne "2"
}

if ($usarJsonCsv) {
    Write-Step "Configuración del protocolo JSON/CSV (red y puerto serie)"
    $ServiceName = Read-WithDefault "Nombre del servicio de Windows" "GregalReceiverService"
    $TcpPort = Read-WithDefault "Puerto TCP" "8080"
    $TcpAddress = Read-WithDefault "Dirección de escucha TCP (0.0.0.0 = todas las interfaces)" "0.0.0.0"
    $AllowedIpsRaw = Read-WithDefault "IPs autorizadas por TCP, separadas por coma (vacío = sin restricción)" "127.0.0.1"
    $NetComPort = Read-WithDefault "Puerto COM de este protocolo (distinto del de arriba si usas los dos)" "COM8"
    $Handshake = Read-WithDefault "Control de flujo serie (None / RequestToSend)" "None"
}
if (-not $ServiceName) { $ServiceName = Read-WithDefault "Nombre del servicio de Windows" "GregalReceiverService" }

Write-Step "Base de datos Firebird"
$DefaultDbPath = Join-Path $InstallDir "Data\GREGAL.FDB"
$DbPath = Read-WithDefault "Ruta completa del fichero .FDB de esta sala" $DefaultDbPath

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

    $premioCodeMapJson = if ($UsarPremiosDeMetronia) {
        '{ "L": 0, "B": 1, "p": 4, "A": 5, "M": 6 }'
    } else {
        '{ "L": 0, "B": 1 }'
    }
    $content = Set-JsonObjectInSection $content "TagProtocolSettings" "PremioCodeMap" $premioCodeMapJson
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

# Sustituye solo el segmento Database=...; dentro de la cadena de conexión de Firebird,
# manteniendo usuario/contraseña/servidor/puerto tal como estén.
$dbPathEscaped = $DbPath.Replace('\', '\\')
$content = [System.Text.RegularExpressions.Regex]::Replace(
    $content, "Database=[^;""]*;", { param($m) "Database=$dbPathEscaped;" })

[System.IO.File]::WriteAllText($AppSettingsPath, $content, (New-Object System.Text.UTF8Encoding($false)))
Write-Ok "appsettings.json actualizado."

# ── Registrar como servicio de Windows ──────────────────────────────────
Write-Step "Registrando el servicio de Windows '$ServiceName'"

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Warn "Ya existe un servicio '$ServiceName'. Se detiene y se vuelve a crear con la ruta actual."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    & sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 1
}

& sc.exe create $ServiceName binPath= "`"$ExePath`"" start= auto DisplayName= "Gregal - Receptor de Bingo" | Out-Null
Write-Ok "Servicio '$ServiceName' creado."

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

Write-Step "Instalación terminada"
if ($usarTag) {
    Write-Host "Revisa el log anterior: debería decir 'Puerto serie (protocolo tag) abierto: $TagComPort'."
    if ($UsarPremiosDeMetronia) {
        Write-Host "Premios configurados para hardware Metronia (A/M/p con importes reales)."
    }
    else {
        Write-Host "Premios configurados para hardware MCB (A/M/p ignorados)."
    }
}
if ($usarJsonCsv) {
    Write-Host "Revisa el log anterior: debería decir 'Servidor TCP iniciado' y, si usas su puerto serie, 'Puerto serie abierto'."
}
Write-Host "Si un puerto serie falla, comprueba que el ComPort configurado es el correcto en este PC y que ningún otro programa lo tiene abierto."
