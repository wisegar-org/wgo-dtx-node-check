# Inno Setup Packaging

Questa cartella contiene l'installer Windows unico di WGO DTX Node Check.
Il file principale e' `WgoDtxNodeCheck.iss`; gli altri `.iss` sono legacy.

## Requisiti

- Windows
- .NET 10 SDK
- Inno Setup 7 o 6

Installazione Inno Setup:

```powershell
winget install --id JRSoftware.InnoSetup.7 -e
```

## Build

Da Windows:

```cmd
packaging\inno\Build-InnoInstallers.cmd
```

Output:

```text
installers\WgoDtxNodeCheck-Setup.exe
```

## Comportamento Installer

- Installa in `Program Files`.
- Richiede privilegi amministrativi.
- Crea cartelle `Reports` e `Logs` sotto `ProgramData`.
- Crea shortcut nel menu Start.
- Offre opzionalmente shortcut desktop.
- Registra l'uninstaller standard di Windows.
- Pubblica solo `DtxNodeCheck.App`, self-contained e single-file per Windows x64.
- Include `dtx-node-check.json` accanto a `WgoDtxNodeCheck.exe`.
- Conserva il JSON esistente durante aggiornamento e disinstallazione.
- Mantiene separate le installazioni legacy: trasferire manualmente eventuali
  controlli personalizzati nel JSON unico prima di disinstallare le vecchie app.

