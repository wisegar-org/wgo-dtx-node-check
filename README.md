# DtxNodeCheck

App .NET 10 in C# per controlli read-only sui nodi DTX Studio Clinic:
Core, Workstation e Client. La soluzione contiene una libreria condivisa e tre
app desktop Avalonia UI, una per nodo.

L'app sostituisce gli script PowerShell esistenti senza eseguire PowerShell,
comandi shell o chiamate di rete durante i controlli. I controlli sono locali e in sola lettura.
Le uniche scritture runtime avvengono quando vengono passati esplicitamente
`--report` o `--log`.

## Uso

```powershell
DtxNodeCheck.exe --config .\dtx-node-check.json --node workstation
DtxNodeCheck.exe --config .\dtx-node-check.json --node core --report .\DTX-Core-Report.txt
DtxNodeCheck.exe --config .\dtx-node-check.json --node client --report .\DTX-Client-Report.json --format json
DtxNodeCheck.exe --config .\dtx-node-check.json --node core --report .\DTX-Core-Report.html --format html --open-report
DtxNodeCheck.exe --config .\dtx-node-check.json --node core --log .\DtxNodeCheck-debug.log
DtxNodeCheck.exe --config .\dtx-node-check.json --node core --report .\DTX-Core-Report.txt --open-report
DtxNodeCheck.exe --inventory --report .\DTX-Inventory.json --format json --open-report
```

La CLI storica e' stata rifattorizzata in `DtxNodeCheck.Core`; le tre app
desktop usano la stessa logica condivisa e producono report HTML.

## Progetti

- `DtxNodeCheck.Core`: logica condivisa, configurazione, controlli, inventario e rendering report.
- `DtxNodeCheck.CoreApp`: app Avalonia per nodo Core.
- `DtxNodeCheck.WorkstationApp`: app Avalonia per nodo Workstation.
- `DtxNodeCheck.ClientApp`: app Avalonia per nodo Client.

Opzioni:

- `--inventory`: crea un inventario read-only del PC per preparare configurazioni.
- `--config <file>`: file JSON di configurazione, obbligatorio nei controlli nodo.
- `--node <core|workstation|client>`: nodo da controllare, obbligatorio nei controlli nodo.
- `--report <file>`: scrive il report nel percorso indicato, opzionale.
- `--log <file>`: scrive messaggi `INFO`, `DEBUG` ed eccezioni nel percorso indicato, opzionale.
- `--open-report`: apre il report alla fine dell'esecuzione, richiede `--report`.
- `--format <text|json|html>`: formato del report, default `text`.

Su Windows l'eseguibile richiede privilegi amministrativi tramite UAC. Se
l'app viene eseguita fuori Windows termina con un errore chiaro.

## Pacchetti Installabili Per Nodo

La cartella `artifacts/node-apps` puo' contenere tre pacchetti separati:

- `DtxNodeCheck-Core-installable.zip`
- `DtxNodeCheck-Workstation-installable.zip`
- `DtxNodeCheck-Client-installable.zip`

Ogni pacchetto contiene l'app Avalonia del nodo, il file di configurazione del
nodo e un installer `Install-DtxNodeCheck-<Nodo>.cmd`. L'installer:

- richiede privilegi amministrativi via UAC;
- copia l'app in `Program Files`;
- copia la configurazione del nodo accanto all'eseguibile;
- crea cartelle report/log in `ProgramData`;
- crea shortcut su Desktop e Start Menu;
- lo shortcut apre una finestra unica con log interattivo dei controlli;
- dalla finestra e' possibile eseguire test o inventario;
- al termine viene generato un report HTML e aperto automaticamente.

I report HTML vengono generati con:

```powershell
--format html --open-report
```

## Installer Windows

E' disponibile anche un packaging semplice con Inno Setup in
`packaging/inno`.

Da Windows, con Inno Setup installato:

```cmd
packaging\inno\Build-InnoInstallers.cmd
```

Lo script pubblica le tre app Avalonia in `Release` e genera:

```text
artifacts\inno\DtxNodeCheck-Core-Setup.exe
artifacts\inno\DtxNodeCheck-Workstation-Setup.exe
artifacts\inno\DtxNodeCheck-Client-Setup.exe
```

Gli installer installano in `Program Files`, richiedono privilegi admin,
creano cartelle report/log in `ProgramData`, shortcut Start Menu/Desktop e
uninstaller Windows standard.

## Controlli implementati

- Identita' locale della macchina e utente corrente.
- Presenza di directory e file configurati.
- Presenza di processi locali configurati.
- Stato di servizi Windows configurati tramite API Windows in sola lettura.
- Presenza di listener TCP locali configurati tramite API .NET locali.

Non modifica rete, DNS, firewall, servizi, registro, file hosts o
configurazioni di sistema. Non risolve hostname e non invia dati in rete.

## Inventario PC

La modalita' `--inventory` raccoglie dati locali read-only per aiutare a
preparare il file JSON di configurazione:

- macchina, utente, sistema operativo e architettura;
- dischi locali;
- processi raggruppati per nome;
- servizi Windows e stato;
- listener TCP locali;
- programmi installati letti dal registro in sola lettura;
- directory candidate DTX sotto Program Files e ProgramData.

Esempio consigliato:

```powershell
DtxNodeCheck.exe --inventory --report .\DTX-Inventory.json --format json --open-report
```

## Configurazione

Un unico file JSON contiene impostazioni comuni e impostazioni specifiche per
nodo. Le impostazioni `common` vengono unite a quelle del nodo selezionato.

Vedi [examples/dtx-node-check.example.json](examples/dtx-node-check.example.json).

Schema logico:

```json
{
  "environmentName": "DTX Node Check",
  "common": {
    "requiredDirectories": [],
    "requiredFiles": [],
    "requiredProcesses": [],
    "requiredServices": [],
    "requiredTcpListeners": []
  },
  "nodes": {
    "core": {},
    "workstation": {},
    "client": {}
  }
}
```

Ogni controllo supporta `required`. Se `true`, l'assenza produce `FAIL`; se
`false`, produce `WARNING`.

## Build

Build locale:

```bash
dotnet build
```

Pubblicazione manuale delle tre app Windows x64:

```bash
dotnet publish src/DtxNodeCheck.CoreApp/DtxNodeCheck.CoreApp.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
dotnet publish src/DtxNodeCheck.WorkstationApp/DtxNodeCheck.WorkstationApp.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
dotnet publish src/DtxNodeCheck.ClientApp/DtxNodeCheck.ClientApp.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Gli eseguibili vengono generati sotto:

```text
src/DtxNodeCheck.CoreApp/bin/Release/net10.0/win-x64/publish/
src/DtxNodeCheck.WorkstationApp/bin/Release/net10.0/win-x64/publish/
src/DtxNodeCheck.ClientApp/bin/Release/net10.0/win-x64/publish/
```

## Exit code

- `0`: controlli completati senza failure.
- `1`: controlli completati con almeno un failure.
- `2`: errore CLI, configurazione o I/O.
- `3`: sistema operativo non supportato.
