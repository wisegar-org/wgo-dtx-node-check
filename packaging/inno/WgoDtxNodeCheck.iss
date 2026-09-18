#define AppName "WGO DTX Node Check"
#define AppVersion "1.0.0"
#define SourceDir "..\..\src\DtxNodeCheck.App\bin\Release\net10.0\win-x64\publish"

[Setup]
AppId={{780CD69D-9C44-47A0-9BE9-173968B21B79}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=WGO
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputDir=..\..\installers
OutputBaseFilename=WgoDtxNodeCheck-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\WgoDtxNodeCheck.exe
DisableProgramGroupPage=yes

[Languages]
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Dirs]
Name: "{commonappdata}\{#AppName}\Reports"
Name: "{commonappdata}\{#AppName}\Logs"

[Files]
Source: "{#SourceDir}\WgoDtxNodeCheck.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\dtx-node-check.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\WgoDtxNodeCheck.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\WgoDtxNodeCheck.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Run]
Filename: "{app}\WgoDtxNodeCheck.exe"; Description: "Avvia {#AppName}"; Flags: nowait postinstall skipifsilent
