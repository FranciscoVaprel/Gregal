; Instalador/actualizador de Gregal con Inno Setup 6.
;
; No se compila a mano: usa Crear-Instalador.ps1 (en esta misma carpeta), que
; publica la app, saca la plantilla GREGAL_EMPTY.FDB del commit actual y pasa
; aquí AppVersion, PublishDir y TemplateFdb.
;
; Un mismo .exe instala o actualiza:
;   - Sala nueva (no existe el servicio): copia los ficheros y lanza
;     Instalar-Gregal.ps1 para configurar la sala (puerto COM, sala, fabricante,
;     base de datos) y registrar el servicio.
;   - Sala con Gregal (existe el servicio, instalado con este .exe o con los
;     scripts): detiene el servicio, sustituye el programa SIN tocar
;     appsettings.json ni la base de datos, y lo vuelve a arrancar. La
;     configuración de la versión nueva queda como appsettings.json.nuevo.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef PublishDir
  #error Falta /DPublishDir: compila con Crear-Instalador.ps1
#endif
#ifndef TemplateFdb
  #error Falta /DTemplateFdb: compila con Crear-Instalador.ps1
#endif

#ifndef ServiceName
  #define ServiceName "GregalReceiverService"
#endif
#define PowerShell "{sys}\WindowsPowerShell\v1.0\powershell.exe"

[Setup]
; AppId identifica la instalación entre versiones: no cambiarlo nunca.
AppId={{6F1C2B7E-3A4D-4E8B-9C21-5D7F0A9B3E42}
AppName=Gregal
AppVersion={#AppVersion}
AppVerName=Gregal {#AppVersion}
AppPublisher=Vaprel
UninstallDisplayName=Gregal - Receptor de Bingo
DefaultDirName={code:DirectorioPorDefecto}
DisableDirPage=auto
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputBaseFilename=Gregal-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; El servicio lo para PrepareToInstall; no hace falta el Restart Manager.
CloseApplications=no
SetupLogging=yes

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "appsettings.json,appsettings.Development.json"; Flags: ignoreversion recursesubdirs createallsubdirs
; La configuración y la base de datos de la sala no se sobrescriben nunca ni
; se borran al desinstalar.
Source: "{#PublishDir}\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#PublishDir}\appsettings.json"; DestDir: "{app}"; DestName: "appsettings.json.nuevo"; Flags: ignoreversion
Source: "{#TemplateFdb}"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall

[Run]
; Sala nueva: configuración y registro del servicio (Instalar-Gregal.ps1 ya
; arranca el servicio y enseña el log).
Filename: "{#PowerShell}"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""& '{app}\Instalar-Gregal.ps1'; Read-Host 'Pulsa Intro para cerrar esta ventana'"""; StatusMsg: "Configurando la sala..."; Check: not EsActualizacion; Flags: waituntilterminated
; Actualización: se vuelve a arrancar el servicio con la versión nueva.
Filename: "{sys}\sc.exe"; Parameters: "start {#ServiceName}"; StatusMsg: "Arrancando el servicio..."; Check: EsActualizacion; Flags: runhidden waituntilterminated
Filename: "{#PowerShell}"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""& '{app}\Instalar-Gregal.ps1'; Read-Host 'Pulsa Intro para cerrar esta ventana'"""; Description: "Volver a configurar la sala (puerto COM, sala, fabricante, base de datos)"; Check: EsActualizacion; Flags: postinstall unchecked waituntilterminated

[UninstallRun]
Filename: "{#PowerShell}"; Parameters: "-NoProfile -Command ""Stop-Service {#ServiceName} -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 2; sc.exe delete {#ServiceName}; Get-NetFirewallRule -DisplayName 'Gregal TCP *' -ErrorAction SilentlyContinue | Remove-NetFirewallRule"""; RunOnceId: "QuitarServicio"; Flags: runhidden waituntilterminated

[Code]
var
  ServicioExistia: Boolean;

// Carpeta del Gregal.exe registrado como servicio (vacía si no hay servicio).
function DirectorioDelServicio: String;
var
  ImagePath: String;
begin
  Result := '';
  if RegQueryStringValue(HKLM, 'SYSTEM\CurrentControlSet\Services\{#ServiceName}', 'ImagePath', ImagePath) then
  begin
    StringChangeEx(ImagePath, '"', '', True);
    Result := ExtractFileDir(Trim(ImagePath));
  end;
end;

// Si la sala ya tiene Gregal (instalado con este .exe o con los scripts), se
// actualiza en su sitio. Inno usa además la carpeta de la instalación previa
// (UsePreviousAppDir) cuando la hizo él.
function DirectorioPorDefecto(Param: String): String;
begin
  Result := DirectorioDelServicio;
  if Result = '' then
    Result := ExpandConstant('{sd}\Gregal');
end;

function EsActualizacion: Boolean;
begin
  Result := ServicioExistia;
end;

function DotNet10Instalado: Boolean;
var
  FindRec: TFindRec;
begin
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.NETCore.App\10.*'), FindRec);
  if Result then
    FindClose(FindRec);
end;

function InitializeSetup: Boolean;
var
  Codigo: Integer;
begin
  Result := True;
  ServicioExistia := RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\{#ServiceName}');

  if not DotNet10Instalado then
  begin
    if MsgBox('No se detecta el runtime de .NET 10 (x64) en este PC, y Gregal lo necesita para funcionar.' + #13#10#13#10 +
              '¿Abrir la página de descarga y cancelar la instalación? (elige "No" para instalar de todos modos)',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/10.0', '', '', SW_SHOWNORMAL, ewNoWait, Codigo);
      Result := False;
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Codigo: Integer;
begin
  Result := '';
  if ServicioExistia then
  begin
    Exec(ExpandConstant('{#PowerShell}'),
      '-NoProfile -Command "Stop-Service {#ServiceName} -Force -ErrorAction SilentlyContinue; ' +
      '(Get-Service {#ServiceName}).WaitForStatus(''Stopped'', ''00:00:30'')"',
      '', SW_HIDE, ewWaitUntilTerminated, Codigo);
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpReady) and ServicioExistia then
    Wizardform.ReadyMemo.Lines.Add(#13#10 + 'Gregal ya está instalado: se actualizará el programa conservando la configuración de la sala (appsettings.json) y su base de datos.');
end;
