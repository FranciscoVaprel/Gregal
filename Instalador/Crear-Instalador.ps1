<#
    Genera el instalador de Gregal (Inno Setup) en Instalador\Output\.

        .\Instalador\Crear-Instalador.ps1

    Publica la app en Release, saca la plantilla vacía GREGAL_EMPTY.FDB del
    commit actual (nunca de la copia de trabajo, que SQL Manager puede haber
    tocado) y compila Gregal.iss con la versión del propio Gregal.dll
    (<Version> de Gregal.csproj).

    Requiere Inno Setup 6 (ISCC.exe); se busca en las rutas habituales o se
    indica con -Iscc.
#>
param([string]$Iscc)

$ErrorActionPreference = "Stop"
$Repo = Split-Path $PSScriptRoot -Parent
$Publish = Join-Path $Repo "Gregal\bin\Release\net10.0\publish\win-x64"
$Obj = Join-Path $PSScriptRoot "obj"
$Output = Join-Path $PSScriptRoot "Output"

if (-not $Iscc) {
    $Iscc = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Iscc -or -not (Test-Path $Iscc)) { throw "No se encuentra ISCC.exe de Inno Setup 6. Indícalo con -Iscc." }

Write-Host "==> Publicando Gregal (Release, win-x64)" -ForegroundColor Cyan
if (Test-Path $Publish) { Remove-Item $Publish -Recurse -Force }
& dotnet publish (Join-Path $Repo "Gregal") -c Release -p:PublishProfile=FolderProfile -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "dotnet publish ha fallado." }

Write-Host "==> Plantilla GREGAL_EMPTY.FDB del commit actual" -ForegroundColor Cyan
New-Item -ItemType Directory -Path $Obj -Force | Out-Null
$Plantilla = Join-Path $Obj "GREGAL_EMPTY.FDB"
# Por cmd: la redirección de PowerShell reescribiría el binario como texto.
& cmd.exe /c "git -C `"$Repo`" show HEAD:Gregal/Data/GREGAL_EMPTY.FDB > `"$Plantilla`""
if ($LASTEXITCODE -ne 0) { throw "No se pudo extraer GREGAL_EMPTY.FDB de git." }

$Version = (Get-Item (Join-Path $Publish "Gregal.dll")).VersionInfo.ProductVersion.Split('+')[0]

Write-Host "==> Compilando el instalador $Version" -ForegroundColor Cyan
& $Iscc /Q "/DAppVersion=$Version" "/DPublishDir=$Publish" "/DTemplateFdb=$Plantilla" "/O$Output" (Join-Path $PSScriptRoot "Gregal.iss")
if ($LASTEXITCODE -ne 0) { throw "ISCC ha fallado." }

$Setup = Join-Path $Output "Gregal-Setup-$Version.exe"
Write-Host "OK $Setup" -ForegroundColor Green
