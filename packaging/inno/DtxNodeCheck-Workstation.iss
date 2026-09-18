#define AppName "DTX Node Check Workstation"
#define AppVersion "1.0.0"
#define AppPublisher "DtxNodeCheck Contributors"
#define SourceDir "..\..\src\DtxNodeCheck.WorkstationApp\bin\Release\net10.0\win-x64\publish"
#define OutputDir "..\..\artifacts\inno"

[Setup]
AppId={{14857D23-F158-4B29-B545-08DE490802F1}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputDir={#OutputDir}
OutputBaseFilename=DtxNodeCheck-Workstation-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\DtxNodeCheck.exe
DisableProgramGroupPage=yes

[Languages]
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Dirs]
Name: "{commonappdata}\{#AppName}\Reports"
Name: "{commonappdata}\{#AppName}\Logs"

[Files]
Source: "{#SourceDir}\DtxNodeCheck.Workstation.exe"; DestDir: "{app}"; DestName: "DtxNodeCheck.exe"; Flags: ignoreversion
Source: "{#SourceDir}\dtx-node-check.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\DtxNodeCheck.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\DtxNodeCheck.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Run]
Filename: "{app}\DtxNodeCheck.exe"; Description: "Avvia {#AppName}"; Flags: nowait postinstall skipifsilent
