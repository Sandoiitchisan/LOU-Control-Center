#ifndef AppVersion
  #define AppVersion "2.1.0"
#endif
[Setup]
AppId={{6AC3EEC2-94C8-41F2-92AD-B4C09B989CF0}
AppName=LOU Control Center
AppVersion={#AppVersion}
AppPublisher=LOU
DefaultDirName={autopf}\LOU Control Center Universal
DisableDirPage=yes
DefaultGroupName=LOU Control Center Universal
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\..\artifacts
OutputBaseFilename=LOU-Control-Center-Setup-{#AppVersion}-win-x64
SetupIconFile=..\..\assets\icons\lou.ico
UninstallDisplayIcon={app}\Lou.ControlCenter.exe
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
LicenseFile=..\..\LICENSE
InfoBeforeFile=INSTALLATION.txt

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "startup"; Description: "Lancer LOU automatiquement à la connexion Windows"; Flags: unchecked
Name: "desktopicon"; Description: "Créer un raccourci sur le Bureau"

[Files]
Source: "..\..\artifacts\portable\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "Data\*,Review\*,*.pdb"
Source: "Configure-Sensors.ps1"; DestDir: "{app}\setup"; Flags: ignoreversion
Source: "installed.mode"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\LOU Control Center Universal\LOU Control Center"; Filename: "{app}\Lou.ControlCenter.exe"
Name: "{autodesktop}\LOU Control Center Universal"; Filename: "{app}\Lou.ControlCenter.exe"; Tasks: desktopicon

[Registry]
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "LouControlCenterUniversal"; ValueData: """{app}\Lou.ControlCenter.exe"" --tray"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\Lou.ControlCenter.exe"; Description: "Ouvrir LOU Control Center"; Flags: postinstall nowait runasoriginaluser skipifsilent

[UninstallRun]
Filename: "{app}\Lou.ControlCenter.exe"; Parameters: "--shutdown"; Flags: runhidden waituntilterminated; RunOnceId: "CloseUniversalLou"
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\setup\Configure-Sensors.ps1"" -Remove"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveUniversalSensorTask"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var Code: Integer;
begin
  if CurStep = ssPostInstall then begin
    if not WizardIsTaskSelected('startup') then
      RegDeleteValue(HKLM, 'Software\Microsoft\Windows\CurrentVersion\Run', 'LouControlCenterUniversal');
    if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
      '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}\setup\Configure-Sensors.ps1') + '"',
      '', SW_HIDE, ewWaitUntilTerminated, Code) or (Code <> 0) then
      RaiseException('Les capteurs n''ont pas pu être configurés. Consultez %ProgramData%\LouControlCenterUniversal\Sensors\installation.log puis relancez cette installation.');
  end;
end;
function NeedRestart(): Boolean;
begin
  Result := FileExists(ExpandConstant('{commonappdata}\LouControlCenterUniversal\Sensors\restart-required.txt'));
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
var Code: Integer;
begin
  Result := '';
  if FileExists(ExpandConstant('{app}\Lou.ControlCenter.exe')) then begin
    Exec(ExpandConstant('{app}\Lou.ControlCenter.exe'), '--shutdown', '', SW_HIDE, ewWaitUntilTerminated, Code);
    Sleep(3000);
  end;
  if FileExists(ExpandConstant('{app}\setup\Configure-Sensors.ps1')) then
    if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
      '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}\setup\Configure-Sensors.ps1') + '" -Remove',
      '', SW_HIDE, ewWaitUntilTerminated, Code) or (Code <> 0) then
      Result := 'Impossible d''arrêter le collecteur de capteurs existant.';
end;
