# Inno Setup Packaging

Questa cartella contiene l'installer Windows unico di WGO DTX Inspector.
L'unico script installer e' `Wisegar.DTXInspector.Setup.iss`.

Il setup evita l'errore 740 all'avvio finale: `runascurrentuser` usa
il contesto amministrativo del setup, richiesto dal manifest dell'app.
Mantenere questo flag finche l'app richiede `requireAdministrator`.
Riferimento: [Inno Setup, sezione Run](https://jrsoftware.org/ishelp/topic_runsection.htm).

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
installers\Wisegar.DTXInspector.Setup-0.0.2.exe
```

## Comportamento Installer

- Installa in `Program Files`.
- Richiede privilegi amministrativi.
- Crea cartelle `Reports` e `Logs` sotto `ProgramData`.
- Crea shortcut nel menu Start.
- Offre opzionalmente shortcut desktop.
- Registra l'uninstaller standard di Windows.
- Pubblica solo `Wisegar.DTXInspector.App`, self-contained e single-file per Windows x64.
- Include `appsettings.json` accanto a `WgoDtxInspector.exe`.
- In aggiornamento rinomina il precedente `dtx-node-check.json` se `appsettings.json`
  non esiste ancora, conservando le personalizzazioni. Un `appsettings.json` esistente
  non viene sovrascritto. Per aggiornamenti portabili rinominare il JSON manualmente.
- Conserva il JSON esistente durante aggiornamento e disinstallazione.
- Mantiene separate le installazioni legacy: trasferire manualmente eventuali
  controlli personalizzati nel JSON unico prima di disinstallare le vecchie app.

