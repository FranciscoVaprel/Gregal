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

# Reemplaza el valor de una propiedad JSON escalar ("clave": valor) preservando
# el resto del fichero (incluidos los comentarios "//" que tiene appsettings.json).
# No usamos ConvertFrom-Json/ConvertTo-Json porque eso no tolera comentarios
# y reescribiría el fichero entero perdiendo el formato.
function Set-JsonScalar([string]$Content, [string]$Key, [string]$RawValue) {
    $pattern = "(?m)^(\s*""$Key""\s*:\s*)(?:""[^""]*""|\[[^\]]*\]|[^,\r\n]+)"
    $evaluator = { param($m) $m.Groups[1].Value + $RawValue }
    return [System.Text.RegularExpressions.Regex]::Replace($Content, $pattern, $evaluator)
}

function Set-JsonString([string]$Content, [string]$Key, [string]$Value) {
    $escaped = $Value.Replace('\', '\\').Replace('"', '\"')
    return Set-JsonScalar $Content $Key "`"$escaped`""
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

# ── Datos de configuración de esta sala ─────────────────────────────────
Write-Step "Configuración de red y puerto serie para esta sala"

$ServiceName = Read-WithDefault "Nombre del servicio de Windows" "GregalReceiverService"
$TcpPort = Read-WithDefault "Puerto TCP" "8080"
$TcpAddress = Read-WithDefault "Dirección de escucha TCP (0.0.0.0 = todas las interfaces)" "0.0.0.0"
$AllowedIpsRaw = Read-WithDefault "IPs autorizadas por TCP, separadas por coma (vacío = sin restricción)" "127.0.0.1"
$ComPort = Read-WithDefault "Puerto COM del hardware serie de esta sala" "COM8"
$Handshake = Read-WithDefault "Control de flujo serie (None / RequestToSend)" "None"

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

$allowedIpsArray = ($AllowedIpsRaw -split ",") | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" }
$allowedIpsJson = "[ " + (($allowedIpsArray | ForEach-Object { "`"$_`"" }) -join ", ") + " ]"

$content = Set-JsonScalar $content "TcpPort" $TcpPort
$content = Set-JsonString $content "TcpAddress" $TcpAddress
$content = Set-JsonScalar $content "AllowedIps" $allowedIpsJson
$content = Set-JsonString $content "ComPort" $ComPort
$content = Set-JsonString $content "SerialHandshake" $Handshake

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

# ── Regla de firewall para el puerto TCP ─────────────────────────────────
Write-Step "Configurando regla de firewall para el puerto TCP $TcpPort"

$fwRuleName = "Gregal TCP $TcpPort"
if (-not (Get-NetFirewallRule -DisplayName $fwRuleName -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $fwRuleName -Direction Inbound -Protocol TCP -LocalPort $TcpPort -Action Allow | Out-Null
    Write-Ok "Regla de firewall creada."
}
else {
    Write-Ok "La regla de firewall ya existía."
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
Write-Host "Revisa el log anterior: debería decir 'Servidor TCP iniciado' y 'Puerto serie abierto'."
Write-Host "Si el puerto serie falla, comprueba que ComPort ('$ComPort') es el correcto en este PC y que ningún otro programa lo tiene abierto."
