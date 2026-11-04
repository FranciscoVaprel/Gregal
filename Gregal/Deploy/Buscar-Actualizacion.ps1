<#
    Busca en GitHub una versión de Gregal más nueva que la instalada y, si la
    hay, la instala con Gregal-Setup en modo silencioso. El instalador
    conserva appsettings.json y la base de datos de la sala, para el servicio
    (o cierra el Gregal de consola) y lo vuelve a arrancar.

    Lo lanza el acceso directo "Actualizar Gregal" del escritorio que crea
    Gregal-Setup. Pide permisos de administrador solo si hay algo que
    instalar.

        .\Buscar-Actualizacion.ps1                 # comprueba y pregunta
        .\Buscar-Actualizacion.ps1 -SoloComprobar  # solo dice si hay versión nueva
#>
param(
    [switch]$SoloComprobar,
    # Uso interno: la copia elevada ya no vuelve a preguntar.
    [switch]$Confirmado
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"   # en PowerShell 5.1 la barra de progreso hace lentísima la descarga
$Repositorio = "FranciscoVaprel/Gregal"
$InstallDir = $PSScriptRoot
$ExePath = Join-Path $InstallDir "Gregal.exe"

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Warn($msg) { Write-Host "! $msg" -ForegroundColor Yellow }
function Write-Ok($msg) { Write-Host "OK $msg" -ForegroundColor Green }
function Salir([int]$Codigo) {
    if (-not $SoloComprobar) { Read-Host "`nPulsa Intro para cerrar esta ventana" | Out-Null }
    exit $Codigo
}

function Get-VersionInstalada {
    $dll = Join-Path $InstallDir "Gregal.dll"
    if (-not (Test-Path $dll)) { return $null }
    # ProductVersion = "1.0.9+<commit>"
    return [version]((Get-Item $dll).VersionInfo.ProductVersion.Split('+')[0])
}

Write-Step "Buscando actualizaciones de Gregal"
$instalada = Get-VersionInstalada
if (-not $instalada) {
    Write-Warn "No se encuentra Gregal.dll en '$InstallDir': este script tiene que estar en la carpeta de Gregal."
    Salir 1
}
Write-Host "Versión instalada: $instalada"

try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $release = Invoke-RestMethod "https://api.github.com/repos/$Repositorio/releases/latest" -UseBasicParsing -TimeoutSec 30
}
catch {
    Write-Warn "No se pudo consultar GitHub (¿sin conexión a internet?): $($_.Exception.Message)"
    Salir 1
}
$ultima = [version]($release.tag_name.TrimStart('v'))
Write-Host "Última versión publicada: $ultima"

if ($ultima -le $instalada) {
    Write-Ok "Gregal está al día."
    Salir 0
}

$setup = $release.assets | Where-Object { $_.name -like "Gregal-Setup-*.exe" } | Select-Object -First 1
if (-not $setup) {
    Write-Warn "La versión $ultima no tiene instalador (Gregal-Setup-*.exe) en GitHub."
    Salir 1
}
if ($SoloComprobar) {
    Write-Host "Hay una versión nueva: $ultima."
    exit 0
}

if (-not $Confirmado) {
    Write-Step "Novedades de la versión $ultima"
    # Solo los cambios: las notas acaban con secciones para quien instala a
    # mano (descargas, instalación, requisitos). Como mucho 25 líneas: el
    # detalle está en GitHub.
    $notas = foreach ($linea in ($release.body -split "`r?`n")) { if ($linea -match '^##\s*(Descargas|Instalaci|Requisitos)') { break }; $linea }
    $notas | Select-Object -First 25 | ForEach-Object { Write-Host "  $_" }
    Write-Host "  ($($release.html_url))"
    Write-Host ""
    Write-Warn "Gregal se para unos segundos mientras se actualiza: si hay una partida en marcha, se pierde."
    Write-Host "La configuración de la sala (appsettings.json) y la base de datos no se tocan."
    $respuesta = Read-Host "¿Actualizar Gregal a la versión $ultima ahora? (S/n)"
    if ($respuesta -match "^[nN]") { Salir 0 }
}

# El instalador necesita permisos de administrador: se vuelve a lanzar este
# script elevado (pide confirmación de Windows) y esta ventana se cierra.
$esAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $esAdmin) {
    Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Confirmado"
    exit 0
}

Write-Step "Descargando $($setup.name)"
$destino = Join-Path $env:TEMP $setup.name
try {
    Invoke-WebRequest $setup.browser_download_url -OutFile $destino -UseBasicParsing -TimeoutSec 600
}
catch {
    Write-Warn "No se pudo descargar: $($_.Exception.Message)"
    Salir 1
}
if ((Get-Item $destino).Length -ne $setup.size) {
    Write-Warn "La descarga está incompleta ($((Get-Item $destino).Length) de $($setup.size) bytes). Vuelve a intentarlo."
    Remove-Item $destino -ErrorAction SilentlyContinue
    Salir 1
}
Write-Ok "Descargado."

# Gregal en consola (sin servicio): se cierra aquí, y el instalador lo
# vuelve a abrir al terminar. Con servicio, lo para y arranca el instalador.
$enConsola = @(Get-Process Gregal -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $ExePath })
if ($enConsola.Count -gt 0) {
    Write-Step "Cerrando Gregal"
    $enConsola | Stop-Process -Force
    Start-Sleep -Seconds 2
}

Write-Step "Instalando la versión $ultima"
$log = Join-Path $env:TEMP "Gregal-Setup-$ultima.log"
$proceso = Start-Process $destino -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /LOG=`"$log`"" -Wait -PassThru
Remove-Item $destino -ErrorAction SilentlyContinue
if ($proceso.ExitCode -ne 0) {
    Write-Warn "El instalador ha terminado con error ($($proceso.ExitCode)). Detalle en $log"
    Salir 1
}

$instalada = Get-VersionInstalada
if ($instalada -eq $ultima) { Write-Ok "Gregal actualizado a la versión $instalada." }
else { Write-Warn "El instalador terminó, pero la versión instalada es $instalada. Detalle en $log" }

$servicio = Get-Service GregalReceiverService -ErrorAction SilentlyContinue
if ($servicio) { Write-Host "Estado del servicio: $($servicio.Status)" }
elseif (Get-Process Gregal -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $ExePath }) { Write-Ok "Gregal está abierto de nuevo." }
else { Write-Warn "Gregal no está en marcha: arráncalo con el acceso directo 'Gregal' del escritorio." }
Salir 0
