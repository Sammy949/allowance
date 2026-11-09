; Allowance installer (Inno Setup 6). Per-user: no admin prompt, installs to %LOCALAPPDATA%\Programs\Allowance.
; Build: ISCC.exe /DAppVersion=1.0.0 /DSourceDir=<publish folder> installer\Allowance.iss

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish\Allowance"
#endif

[Setup]
; Never change AppId: Windows uses it to recognise upgrades and the uninstall entry.
AppId={{B7E4D1A6-3C52-4F08-9A7D-61C2E0F4A915}
AppName=Allowance
AppVersion={#AppVersion}
AppVerName=Allowance {#AppVersion}
AppPublisher=Allowance contributors
AppPublisherURL=https://github.com/Sammy949/allowance
AppSupportURL=https://github.com/Sammy949/allowance/issues
AppUpdatesURL=https://github.com/Sammy949/allowance/releases
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\Allowance
DefaultGroupName=Allowance
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
WizardStyle=modern
SetupIconFile=..\src\Allowance\Assets\Allowance.ico
UninstallDisplayIcon={app}\Allowance.exe
UninstallDisplayName=Allowance
OutputDir=out
OutputBaseFilename=Allowance-Setup
Compression=lzma2/max
SolidCompression=yes
; Allowance is closed by PrepareToInstall below; this keeps the Restart Manager from asking about it too.
CloseApplications=no

[Tasks]
Name: startup; Description: "Start Allowance when I sign in"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Allowance"; Filename: "{app}\Allowance.exe"

[Registry]
; The same value the app's own "Start with Windows" menu item reads and writes.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Allowance"; ValueData: """{app}\Allowance.exe"""; Tasks: startup

[Run]
Filename: "{app}\Allowance.exe"; Description: "Open Allowance"; Flags: nowait postinstall skipifsilent

[Code]
procedure CloseAllowance();
var
  Code: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM Allowance.exe', '', SW_HIDE, ewWaitUntilTerminated, Code);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  CloseAllowance();
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Value: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    CloseAllowance();
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Allowance', Value) and
       (Pos(Lowercase(ExpandConstant('{app}')), Lowercase(Value)) > 0) then
      RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Allowance');
  end;
end;
