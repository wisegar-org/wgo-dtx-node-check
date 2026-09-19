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
installers\Wisegar.DTXInspector.Setup-0.0.22.exe
```

## Comportamento Installer

### Icona condivisa

L'icona rappresenta Wisegar e il monitoraggio di software sanitario: una W come
tracciato dentro uno schermo, bianca su rosso `#DA291C`, senza ombre.
Il sorgente vettoriale è `src/Wisegar.DTXInspector.App/Assets/app-icon.svg`.
Dopo una modifica al disegno, rigenerare PNG e ICO prima del build:

```powershell
.\packaging\Build-AppIcon.ps1
```

Lo script usa GDI+ su Windows e genera le risoluzioni 16, 20, 24, 32, 48, 64,
128 e 256 pixel. SVG, PNG e ICO sono versionati. La stessa icona è incorporata
nell'eseguibile, nelle finestre Avalonia e nel setup; i collegamenti usano quella
dell'eseguibile. Installer e anteprime temporanee in `installers` non sono versionati.

### Installazione

- Installa in `C:\Program Files\Wisegar\WGO DTX Inspector`.
- Usa il nuovo percorso anche in aggiornamento; copia il JSON della precedente
  installazione se manca nella destinazione, senza eliminare quello originale.
- Richiede privilegi amministrativi.
- Crea `Reports` e `Logs` sotto `C:\ProgramData\Wisegar\WGO DTX Inspector`.
- Report e log precedenti rimangono nella vecchia cartella; i nuovi usano il percorso Wisegar.
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

