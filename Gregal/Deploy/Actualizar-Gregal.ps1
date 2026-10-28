#Requires -RunAsAdministrator
<#
    Actualizador de Gregal en un PC de sala que YA tiene Gregal instalado.

    Uso: descomprime el zip de la versión nueva en una carpeta CUALQUIERA que
    no sea la de la instalación (p.ej. C:\Temp\Gregal-nuevo) y ejecuta este
    script desde ahí, como Administrador:

        .\Actualizar-Gregal.ps1

    Qué hace:
      - Localiza la instalación a partir del servicio de Windows (o -InstallDir).
      - Detiene el servicio y guarda una copia de seguridad de la instalación
        en <instalación>\_backup\<fecha_hora>\.
      - Copia los ficheros de la versión nueva, SIN tocar:
          * appsettings.json (la configuración de la sala),
          * logs\ y _backup\,
          * ninguna base de datos .FDB que ya exista.
      - Deja la configuración de la versión nueva como appsettings.json.nuevo
        (solo de referencia: Gregal no la lee) y avisa si trae claves que la
        configuración de la sala no tiene (Gregal usará su valor por defecto).
      - Vuelve a arrancar el servicio y muestra el final del log.

    Parámetros:
      -InstallDir <ruta>           Carpeta de la instalación, si no se puede
                                   deducir del servicio.
      -ServiceName <nombre>        Nombre del servicio (GregalReceiverService).
      -SobrescribirConfiguracion   Sustituye también appsettings.json por el de
                                   la versión nueva (tras guardar copia). Habrá
                                   que volver a configurar la sala.

    Para una sala nueva, usa Instalar-Gregal.ps1.
#>
param(
    [string]$InstallDir,
    [string]$ServiceName = "GregalReceiverService",
    [switch]$SobrescribirConfiguracion
)

$ErrorActionPreference = "Stop"
$Origen = $PSScriptRoot

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Warn($msg) { Write-Host "! $msg" -ForegroundColor Yellow }
function Write-Ok($msg) { Write-Host "OK $msg" -ForegroundColor Green }

function Get-Version([string]$Exe) {
    if (-not (Test-Path $Exe)) { return "?" }
    $v = (Get-Item $Exe).VersionInfo.ProductVersion
    if ([string]::IsNullOrWhiteSpace($v)) { return "?" }
    return $v
}

# appsettings.json lleva comentarios "//", que ConvertFrom-Json no acepta. Se
# quitan respetando las cadenas (una "//" dentro de un valor no es comentario).
function Remove-JsonComments([string]$Text) {
    $sb = New-Object System.Text.StringBuilder
    $enCadena = $false
    $escape = $false
    for ($i = 0; $i -lt $Text.Length; $i++) {
        $c = $Text[$i]
        if ($enCadena) {
            [void]$sb.Append($c)
            if ($escape) { $escape = $false }
            elseif ($c -eq '\') { $escape = $true }
            elseif ($c -eq '"') { $enCadena = $false }
            continue
        }
        if ($c -eq '"') { $enCadena = $true; [void]$sb.Append($c); continue }
        if ($c -eq '/' -and $i + 1 -lt $Text.Length -and $Text[$i + 1] -eq '/') {
            while ($i -lt $Text.Length -and $Text[$i] -ne "`n") { $i++ }
            [void]$sb.Append("`n")
            continue
        }
        [void]$sb.Append($c)
    }
    return $sb.ToString()
}

# Rutas "Seccion:Clave" de todas las claves de configuración (como las ve .NET).
function Get-ConfigKeys([string]$Path) {
    $json = Remove-JsonComments ([System.IO.File]::ReadAllText($Path)) | ConvertFrom-Json
    $claves = New-Object System.Collections.Generic.List[string]
    function Recorrer($obj, [string]$prefijo) {
        foreach ($p in $obj.PSObject.Properties) {
            $ruta = if ($prefijo) { "${prefijo}:$($p.Name)" } else { $p.Name }
            if ($p.Value -is [System.Management.Automation.PSCustomObject]) { Recorrer $p.Value $ruta }
            else { $claves.Add($ruta) }
        }
    }
    Recorrer $json ""
    return $claves
}

# ── Localizar la instalación ─────────────────────────────────────────────
Write-Step "Localizando la instalación"

if (-not (Test-Path (Join-Path $Origen "Gregal.exe"))) {
    Write-Warn "No se encuentra Gregal.exe junto a este script ('$Origen')."
    Write-Warn "Ejecútalo desde la carpeta donde has descomprimido la versión nueva."
    exit 1
}

$servicio = Get-CimInstance Win32_Service -Filter "Name='$ServiceName'" -ErrorAction SilentlyContinue
if (-not $InstallDir) {
    if (-not $servicio) {
        Write-Warn "No existe el servicio '$ServiceName': no parece que Gregal esté instalado en este PC."
        Write-Warn "Para una sala nueva usa Instalar-Gregal.ps1, o indica la carpeta con -InstallDir."
        exit 1
    }
    $exeInstalado = $servicio.PathName.Trim().Trim('"')
    $InstallDir = Split-Path $exeInstalado -Parent
}

$InstallDir = (Resolve-Path $InstallDir).Path.TrimEnd('\')
$OrigenNorm = (Resolve-Path $Origen).Path.TrimEnd('\')
if ($InstallDir -ieq $OrigenNorm) {
    Write-Warn "Este script se está ejecutando desde la propia carpeta de la instalación ('$InstallDir')."
    Write-Warn "Descomprime la versión nueva en OTRA carpeta (p.ej. C:\Temp\Gregal-nuevo) y ejecútalo desde allí."
    exit 1
}

$ConfigSala = Join-Path $InstallDir "appsettings.json"
if (-not (Test-Path (Join-Path $InstallDir "Gregal.exe")) -or -not (Test-Path $ConfigSala)) {
    Write-Warn "'$InstallDir' no parece una instalación de Gregal (falta Gregal.exe o appsettings.json)."
    exit 1
}

Write-Host "Instalación:      $InstallDir"
Write-Host "Versión instalada: $(Get-Version (Join-Path $InstallDir 'Gregal.exe'))"
Write-Host "Versión nueva:     $(Get-Version (Join-Path $Origen 'Gregal.exe'))"
if ($SobrescribirConfiguracion) {
    Write-Warn "Se va a SUSTITUIR appsettings.json por el de la versión nueva (se guarda copia)."
}
else {
    Write-Host "La configuración de la sala (appsettings.json) se conserva tal cual."
}
$seguir = Read-Host "¿Continuar con la actualización? (S/n)"
if ($seguir -match "^[nN]") { exit 1 }

# ── Detener el servicio ──────────────────────────────────────────────────
Write-Step "Deteniendo el servicio '$ServiceName'"
if ($servicio) {
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    (Get-Service -Name $ServiceName).WaitForStatus("Stopped", [TimeSpan]::FromSeconds(30))
}
# Por si quedara un Gregal.exe de esa carpeta abierto en consola.
$exeDestino = Join-Path $InstallDir "Gregal.exe"
for ($i = 0; $i -lt 30; $i++) {
    $vivos = Get-Process -Name Gregal -ErrorAction SilentlyContinue | Where-Object { $_.Path -ieq $exeDestino }
    if (-not $vivos) { break }
    if ($i -eq 0) { Write-Warn "Gregal.exe sigue abierto desde '$InstallDir'; esperando a que se cierre..." }
    Start-Sleep -Seconds 1
}
if ($vivos) {
    Write-Warn "Gregal.exe sigue en marcha (PID $($vivos.Id -join ', ')). Ciérralo y vuelve a ejecutar el script."
    exit 1
}
if ($servicio) { Write-Ok "Servicio detenido." }
else { Write-Warn "No hay servicio '$ServiceName' registrado; se actualizan solo los ficheros." }

# ── Copia de seguridad ───────────────────────────────────────────────────
Write-Step "Copia de seguridad"
$Backup = Join-Path $InstallDir ("_backup\" + (Get-Date -Format "yyyyMMdd_HHmmss"))
New-Item -ItemType Directory -Path $Backup -Force | Out-Null
Get-ChildItem $InstallDir -Recurse -File | Where-Object {
    $rel = $_.FullName.Substring($InstallDir.Length + 1)
    -not ($rel -like "_backup\*" -or $rel -like "logs\*" -or $_.Extension -ieq ".fdb")
} | ForEach-Object {
    $rel = $_.FullName.Substring($InstallDir.Length + 1)
    $dest = Join-Path $Backup $rel
    New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force | Out-Null
    Copy-Item $_.FullName $dest
}
Write-Ok "Copia guardada en '$Backup' (sin logs ni bases de datos)."

# ── Copiar la versión nueva ──────────────────────────────────────────────
Write-Step "Copiando la versión nueva"
$copiados = 0
$conservados = New-Object System.Collections.Generic.List[string]
Get-ChildItem $OrigenNorm -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring($OrigenNorm.Length + 1)
    $dest = Join-Path $InstallDir $rel

    if ($rel -like "_backup\*" -or $rel -like "logs\*") { return }
    if ($rel -ieq "appsettings.json" -and -not $SobrescribirConfiguracion) { $conservados.Add($rel); return }
    if ($_.Extension -ieq ".fdb" -and (Test-Path $dest)) { $conservados.Add($rel); return }

    New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force | Out-Null
    Copy-Item $_.FullName $dest -Force
    $copiados++
}
Write-Ok "$copiados ficheros actualizados."
foreach ($c in $conservados) { Write-Host "   conservado: $c" }

# ── Configuración ────────────────────────────────────────────────────────
Write-Step "Configuración"
$ConfigNueva = Join-Path $OrigenNorm "appsettings.json"
if ($SobrescribirConfiguracion) {
    Write-Warn "appsettings.json sustituido por el de la versión nueva. La configuración anterior está en '$Backup\appsettings.json'."
    Write-Warn "Revisa puerto COM, CodSala/CodConc, Fabricante y ruta de la base de datos (o ejecuta Instalar-Gregal.ps1)."
}
else {
    Copy-Item $ConfigNueva (Join-Path $InstallDir "appsettings.json.nuevo") -Force
    try {
        $deSala = Get-ConfigKeys $ConfigSala
        $nuevas = Get-ConfigKeys $ConfigNueva | Where-Object { $deSala -notcontains $_ }
        if ($nuevas) {
            Write-Warn "La versión nueva trae opciones que la configuración de esta sala no tiene:"
            foreach ($n in $nuevas) { Write-Host "     $n" }
            Write-Warn "Gregal usará su valor por defecto. Si alguna hace falta, cópiala a mano desde appsettings.json.nuevo."
        }
        else {
            Write-Ok "La configuración de la sala ya tiene todas las opciones de la versión nueva."
        }
    }
    catch {
        Write-Warn "No se pudo comparar la configuración ($($_.Exception.Message)). Revisa a mano appsettings.json.nuevo."
    }
}

# ── Arrancar el servicio ─────────────────────────────────────────────────
Write-Step "Arrancando el servicio"
if ($servicio) {
    Start-Service -Name $ServiceName
    Start-Sleep -Seconds 3
    Write-Host "Estado del servicio: $((Get-Service -Name $ServiceName).Status)"
}
else {
    Write-Warn "No hay servicio '$ServiceName' registrado: arranca Gregal a mano o ejecuta Instalar-Gregal.ps1."
}

$lastLog = Get-ChildItem (Join-Path $InstallDir "logs") -Filter "*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($lastLog) {
    Write-Step "Últimas líneas del log ($($lastLog.Name))"
    Get-Content $lastLog.FullName -Tail 15
}

Write-Step "Actualización terminada"
Write-Host "Versión instalada ahora: $(Get-Version $exeDestino)"
Write-Host "Si algo va mal, detén el servicio y copia el contenido de '$Backup' sobre '$InstallDir'."
