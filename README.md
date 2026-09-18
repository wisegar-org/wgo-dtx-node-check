# WGO DTX Node Check

App .NET 10 in C# per controlli read-only sui nodi DTX Studio Clinic:
Core, Workstation e Client. La soluzione contiene una libreria condivisa, una
app desktop Avalonia UI unica e tre app desktop legacy, una per nodo.

L'app sostituisce gli script PowerShell esistenti senza eseguire PowerShell,
comandi shell o chiamate di rete durante i controlli. I controlli sono locali e in sola lettura.
Report e log vengono scritti quando si avviano test o inventario dalla finestra.
Il solo avvio dell'app non crea report o log.

## Uso

```powershell
.\WgoDtxNodeCheck.exe
```

La CLI storica e' stata rifattorizzata in `DtxNodeCheck.Core`; le app desktop
usano la stessa logica condivisa e producono report HTML. L'app unica
`WGO DTX Node Check` prova a inferire il nodo all'avvio usando solo segnali
locali read-only. Se il nodo non viene inferito con certezza, l'utente sceglie
Core, Workstation o Client dalla finestra.
Nella finestra desktop e' presente una sezione `Documentazione` con link
ufficiali apribili manualmente; i controlli non accedono alla rete in automatico.
La configurazione del nodo puo' essere aperta dalla finestra, modificata con
l'editor associato ai file JSON e ricaricata senza riavviare l'app. Ogni
esecuzione dei test rilegge comunque il file di configurazione da disco.
La finestra include anche un `About` con dati app, nodo, versione, ambiente e
percorsi usati.

## Progetti

- `DtxNodeCheck.Core`: logica condivisa, configurazione, controlli, inventario e rendering report.
- `DtxNodeCheck.App`: app Avalonia unica `WGO DTX Node Check` con inferenza nodo.
- `DtxNodeCheck.CoreApp`: app Avalonia legacy per nodo Core.
- `DtxNodeCheck.WorkstationApp`: app Avalonia legacy per nodo Workstation.
- `DtxNodeCheck.ClientApp`: app Avalonia legacy per nodo Client.

Aprire l'app, verificare o selezionare il nodo, quindi premere `Esegui test`.
`Inventario PC` e' disponibile anche senza selezionare un nodo. Entrambe le
azioni generano un report HTML e lo aprono automaticamente. La configurazione
si puo' aprire prima della selezione; `Ricarica config` ripete il rilevamento
se il nodo non e' ancora selezionato, altrimenti ricarica il piano del nodo.
La selezione manuale resta valida durante la sessione.

Su Windows l'eseguibile richiede privilegi amministrativi tramite UAC.
Su macOS l'interfaccia e' disponibile, ma test e inventario sono disabilitati:
i controlli DTX richiedono Windows. Le opzioni della CLI storica non sono
gestite dall'eseguibile desktop.

## Pacchetti Legacy Per Nodo

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
- dalla finestra e' possibile aprire e ricaricare il file JSON di configurazione;
- dalla finestra e' possibile consultare i dati applicativi tramite `About`;
- dalla finestra e' possibile aprire fonti ufficiali di supporto e documentazione;
- al termine viene generato un report HTML e aperto automaticamente.

## Fonti Ufficiali

Le app desktop espongono link manuali verso:

- DTX Studio Support: <https://www.dtxstudio.com/en-us/support>
- DTX Studio Go: <https://www.dtxstudio.com/en-us/dtx-studio-go>
- Help e IFU: <https://helpfiles.dtxstudio.com/>
- Installazione e aggiornamenti: <https://helpfiles.dtxstudio.com/Help/50784413-8047-4699-82f7-d1e9a868909e/4.1/EN/Installation_and_updates.htm>

Questi link vengono aperti solo su richiesta dell'utente.

I report HTML vengono generati dai pulsanti `Esegui test` e `Inventario PC`.

## Installer Windows

E' disponibile anche un packaging semplice con Inno Setup in
`packaging/inno`.

Da Windows, con Inno Setup installato:

```cmd
packaging\inno\Build-InnoInstallers.cmd
```

Lo script pubblica l'app unica in `Release`, self-contained e single-file, e genera:

```text
artifacts\inno\WgoDtxNodeCheck-Setup.exe
```

L'installer installa in `Program Files\WGO DTX Node Check`, richiede privilegi
admin e crea cartelle report/log in `ProgramData\WGO DTX Node Check`, shortcut
Start Menu/Desktop e uninstaller Windows standard. Il JSON e' esterno, accanto
all'eseguibile: un aggiornamento lo conserva, cosi' come la disinstallazione.
Le precedenti installazioni per nodo restano separate; le loro configurazioni
personalizzate vanno riportate manualmente nelle sezioni del JSON unificato.
Gli script `.iss` legacy restano disponibili per compatibilita'.

## Controlli implementati

- Identita' locale della macchina e utente corrente.
- Presenza di directory e file configurati.
- Presenza di processi locali configurati.
- Stato di servizi Windows configurati tramite API Windows in sola lettura.
- Presenza di listener TCP locali configurati tramite API .NET locali.

Non modifica rete, DNS, firewall, servizi, registro, file hosts o
configurazioni di sistema. Non risolve hostname e non invia dati in rete.

## Inventario PC

Il pulsante `Inventario PC` raccoglie dati locali read-only per aiutare a
preparare il file JSON di configurazione:

- macchina, utente, sistema operativo e architettura;
- dischi locali;
- processi raggruppati per nome;
- servizi Windows e stato;
- listener TCP locali;
- programmi installati letti dal registro in sola lettura;
- directory candidate DTX sotto Program Files e ProgramData.

Il report viene salvato in `ProgramData\WGO DTX Node Check\Reports\DTX-Inventory.html`.

## Configurazione

Un unico file JSON contiene impostazioni comuni e impostazioni specifiche per
nodo. L'app unica usa `dtx-node-check.json`; le impostazioni `common` vengono
unite a quelle del nodo selezionato o inferito.

Vedi [examples/dtx-node-check.example.json](examples/dtx-node-check.example.json).
La configurazione distribuita e' [configs/dtx-node-check.json](configs/dtx-node-check.json).
E' un punto di partenza: aggiungere controlli specifici dell'installazione.
Workstation e Client hanno inizialmente segnali simili, quindi il rilevamento
puo' richiedere la selezione manuale. L'app seleziona automaticamente un nodo
solo con punteggio almeno 3 e distacco almeno 2 dal secondo candidato.

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

Pubblicazione manuale dell'app unica Windows x64:

```bash
dotnet publish src/DtxNodeCheck.App/DtxNodeCheck.App.csproj -c Release -r win-x64
```

Pubblicazione macOS ARM64 (interfaccia, controlli disponibili solo su Windows):

```bash
dotnet publish src/DtxNodeCheck.App/DtxNodeCheck.App.csproj -c Release -r osx-arm64
```

Verifica delle transizioni UI, senza avviare controlli o aprire report
(richiede un ambiente desktop):

```bash
dotnet run --project tests/DtxNodeCheck.Desktop.Smoke
```

Pubblicazione manuale delle app legacy per nodo Windows x64:

```bash
dotnet publish src/DtxNodeCheck.CoreApp/DtxNodeCheck.CoreApp.csproj -c Release -r win-x64
dotnet publish src/DtxNodeCheck.WorkstationApp/DtxNodeCheck.WorkstationApp.csproj -c Release -r win-x64
dotnet publish src/DtxNodeCheck.ClientApp/DtxNodeCheck.ClientApp.csproj -c Release -r win-x64
```

Quando viene indicato un Runtime Identifier, tutti i progetti desktop pubblicano
sempre output self-contained e single-file. Un publish desktop senza Runtime
Identifier viene bloccato.

Gli eseguibili vengono generati sotto:

```text
src/DtxNodeCheck.App/bin/Release/net10.0/win-x64/publish/
```

## Exit code

- `0`: controlli completati senza failure.
- `1`: controlli completati con almeno un failure.
- `2`: errore CLI, configurazione o I/O.
- `3`: sistema operativo non supportato.
