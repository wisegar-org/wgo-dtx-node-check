#define AppName "WGO DTX Inspector"
#define AppVersion "0.0.6"
#define SourceDir "..\..\src\Wisegar.DTXInspector.App\bin\Release\net10.0\win-x64\publish"

[Setup]
AppId={{780CD69D-9C44-47A0-9BE9-173968B21B79}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Wisegar
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputDir=..\..\installers
OutputBaseFilename=Wisegar.DTXInspector.Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\WgoDtxInspector.exe
DisableProgramGroupPage=yes

[Languages]
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Dirs]
Name: "{commonappdata}\{#AppName}\Reports"
Name: "{commonappdata}\{#AppName}\Logs"

[Files]
Source: "{#SourceDir}\WgoDtxInspector.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\WgoDtxInspector.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\WgoDtxInspector.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[InstallDelete]
Type: files; Name: "{app}\WgoDtxNodeCheck.exe"
Type: files; Name: "{autoprograms}\WGO DTX Node Check.lnk"
Type: files; Name: "{autodesktop}\WGO DTX Node Check.lnk"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Run]
; The app requires elevation; default postinstall credentials cause error 740.
Filename: "{app}\WgoDtxInspector.exe"; Description: "Avvia {#AppName}"; Flags: nowait postinstall skipifsilent runascurrentuser

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  OldConfig, NewConfig: String;
begin
  Result := '';
  OldConfig := ExpandConstant('{app}\dtx-node-check.json');
  NewConfig := ExpandConstant('{app}\appsettings.json');
  if FileExists(OldConfig) and not FileExists(NewConfig) then
  begin
    if not RenameFile(OldConfig, NewConfig) then
      Result := 'Impossibile rinominare dtx-node-check.json in appsettings.json. Verificare i permessi e riprovare.';
  end;
end;
