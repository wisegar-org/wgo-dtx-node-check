# Inno Setup Packaging

Questa cartella contiene un packaging Windows semplice basato su Inno Setup.

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
artifacts\inno\DtxNodeCheck-Core-Setup.exe
artifacts\inno\DtxNodeCheck-Workstation-Setup.exe
artifacts\inno\DtxNodeCheck-Client-Setup.exe
```

## Comportamento Installer

- Installa in `Program Files`.
- Richiede privilegi amministrativi.
- Crea cartelle `Reports` e `Logs` sotto `ProgramData`.
- Crea shortcut nel menu Start.
- Offre opzionalmente shortcut desktop.
- Registra l'uninstaller standard di Windows.

