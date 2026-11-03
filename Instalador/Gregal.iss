; Instalador/actualizador de Gregal con Inno Setup 6.
;
; No se compila a mano: usa Crear-Instalador.ps1 (en esta misma carpeta), que
; publica la app (autocontenida: incluye el runtime de .NET 10), saca la plantilla GREGAL_EMPTY.FDB del commit actual y pasa
; aquí AppVersion, PublishDir y TemplateFdb.
;
; Un mismo .exe instala o actualiza:
;   - Sala nueva: copia los ficheros y lanza Instalar-Gregal.ps1 para
;     configurar la sala (puerto COM, sala, fabricante, base de datos, que
;     comprueba con "Gregal.exe --comprobar-bd"). Con la casilla "servicio"
;     (marcada por defecto) registra el servicio; sin ella deja un acceso
;     directo en el escritorio para arrancar Gregal a mano.
;   - Sala con Gregal (existe el servicio, o una instalación previa de este
;     .exe aunque sea sin servicio): para el servicio si lo hay, sustituye el
;     programa SIN tocar appsettings.json ni la base de datos, y vuelve a
;     arrancar el servicio. La configuración de la versión nueva queda como
;     appsettings.json.nuevo.

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
; El servicio lo para PrepareToInstall. El Restart Manager queda para un
; Gregal abierto a mano (instalación sin servicio): ofrece cerrarlo.
CloseApplications=yes
RestartApplications=no
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

[Tasks]
; Solo en una sala nueva: al actualizar se respeta lo que ya hay.
Name: "servicio"; Description: "Instalar como servicio de Windows: arranca solo al encender el PC, sin nadie conectado (lo normal en una sala). Sin marcar, Gregal se arranca a mano con un acceso directo en el escritorio y solo recibe partidas mientras está abierto."; Check: not EsActualizacion

[Run]
; Sala nueva: configuración (Instalar-Gregal.ps1 registra y arranca el
; servicio y enseña el log, o crea el acceso directo si va sin servicio).
Filename: "{#PowerShell}"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""& '{app}\Instalar-Gregal.ps1' -ConServicio; Read-Host 'Pulsa Intro para cerrar esta ventana'"""; StatusMsg: "Configurando la sala..."; Tasks: servicio; Check: not EsActualizacion; Flags: waituntilterminated
Filename: "{#PowerShell}"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""& '{app}\Instalar-Gregal.ps1' -SinServicio; Read-Host 'Pulsa Intro para cerrar esta ventana'"""; StatusMsg: "Configurando la sala..."; Tasks: not servicio; Check: not EsActualizacion; Flags: waituntilterminated
; Actualización: se vuelve a arrancar el servicio con la versión nueva, o se
; ofrece arrancar Gregal si va sin servicio.
Filename: "{sys}\sc.exe"; Parameters: "start {#ServiceName}"; StatusMsg: "Arrancando el servicio..."; Check: EsActualizacion and TieneServicio; Flags: runhidden waituntilterminated
Filename: "{app}\Gregal.exe"; WorkingDir: "{app}"; Description: "Arrancar Gregal"; Check: EsActualizacion and not TieneServicio; Flags: postinstall nowait runasoriginaluser
Filename: "{#PowerShell}"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""& '{app}\Instalar-Gregal.ps1'; Read-Host 'Pulsa Intro para cerrar esta ventana'"""; Description: "Volver a configurar la sala (puerto COM, sala, fabricante, base de datos, servicio)"; Check: EsActualizacion; Flags: postinstall unchecked waituntilterminated

[UninstallDelete]
; El acceso directo lo crea Instalar-Gregal.ps1 cuando va sin servicio.
Type: files; Name: "{commondesktop}\Gregal.lnk"

[UninstallRun]
Filename: "{#PowerShell}"; Parameters: "-NoProfile -Command ""Stop-Service {#ServiceName} -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 2; sc.exe delete {#ServiceName}; Get-NetFirewallRule -DisplayName 'Gregal TCP *' -ErrorAction SilentlyContinue | Remove-NetFirewallRule"""; RunOnceId: "QuitarServicio"; Flags: runhidden waituntilterminated

[Code]
var
  ServicioExistia: Boolean;
  // Instalación previa hecha con este .exe (con o sin servicio).
  InstalacionPrevia: Boolean;

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

// Sin servicio, una sala ya instalada con este .exe se reconoce por su
// entrada de desinstalación: no se vuelve a preguntar la configuración.
function EsActualizacion: Boolean;
begin
  Result := ServicioExistia or InstalacionPrevia;
end;

// Para los Check: de [Run], que solo admiten funciones.
function TieneServicio: Boolean;
begin
  Result := ServicioExistia;
end;

// No hace falta comprobar el runtime de .NET 10: la app se publica
// autocontenida y lo lleva en su propia carpeta.
function InitializeSetup: Boolean;
begin
  Result := True;
  ServicioExistia := RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\{#ServiceName}');
  InstalacionPrevia := RegKeyExists(HKLM,
    'Software\Microsoft\Windows\CurrentVersion\Uninstall\{6F1C2B7E-3A4D-4E8B-9C21-5D7F0A9B3E42}_is1');
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
  if (CurPageID = wpReady) and EsActualizacion then
  begin
    Wizardform.ReadyMemo.Lines.Add(#13#10 + 'Gregal ya está instalado: se actualizará el programa conservando la configuración de la sala (appsettings.json) y su base de datos.');
    if not ServicioExistia then
      Wizardform.ReadyMemo.Lines.Add('Está instalado sin servicio de Windows: si Gregal está abierto, se cerrará para actualizarlo.');
  end;
end;
