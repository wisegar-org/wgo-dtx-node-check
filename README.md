# WGO DTX Inspector

App .NET 10 in C# per controlli read-only sui nodi DTX Studio Clinic:
Core, Workstation e Client. La soluzione contiene una libreria condivisa, una
app desktop Avalonia UI unica per tutti e tre i ruoli.

L'app sostituisce gli script PowerShell esistenti senza eseguire PowerShell,
comandi shell. I controlli non modificano il sistema; ogni esecuzione dei test
include prove DNS/TCP sui target configurati, con timeout.
Report e log vengono scritti quando si avviano test o inventario dalla finestra.
`Impostazioni da inventario` puo' aggiornare il JSON dell'app e salvarne una
copia di sicurezza, solo dopo conferma esplicita.
Il solo avvio dell'app non crea report o log.

## Uso

```powershell
.\WgoDtxInspector.exe
```

`Wisegar.DTXInspector.Core` contiene controlli, inventario e report HTML. L'app unica
`WGO DTX Inspector` richiede la scelta manuale di Core, Workstation o Client.
All'avvio nessun nodo e' selezionato e il menu invita a scegliere.
La toolbar contiene il selettore nodo e le azioni `Esegui test`, `Inventario PC`
e `Apri report`, con icone e tooltip. Il menu `Configurazione` raccoglie apertura,
ricarica e impostazioni da inventario; `Aiuto` contiene documentazione e About.
I menu sono accessibili da tastiera con Alt+C / Alt+A. Su finestre strette la
toolbar dispone le azioni su piu' righe senza nasconderle.
Nel menu `Aiuto > Documentazione` sono presenti link
ufficiali apribili manualmente. Avvio e inventario non effettuano
prove di rete; le prove DNS/TCP partono con `Esegui test`.
Il JSON include [default DTX documentati per versione](docs/dtx-defaults.md): servizio
Core, directory dati/installazione, listener Core e IPC locali Clinic. Sono controlli
opzionali da confermare per l'impianto; i target di rete restano da configurare.

La configurazione del nodo puo' essere aperta dalla finestra, modificata con
l'editor associato ai file JSON e ricaricata senza riavviare l'app. Ogni
esecuzione dei test rilegge comunque il file di configurazione da disco.
La finestra include anche un `About` con dati app, nodo, versione, ambiente e
percorsi usati.

## Progetti

- `Wisegar.DTXInspector.Core`: logica condivisa, configurazione, controlli, inventario e rendering report.
- `Wisegar.DTXInspector.App`: app Avalonia unica `WGO DTX Inspector` con scelta manuale del nodo.
- `tests/Wisegar.DTXInspector.Desktop.Smoke`: verifiche automatiche, non un'app distribuita.

Aprire l'app, verificare o selezionare il nodo, quindi premere `Esegui test`.
`Inventario PC` e' disponibile anche senza selezionare un nodo. Entrambe le
azioni generano un report HTML e lo aprono automaticamente. La configurazione
si puo' aprire prima della selezione; `Ricarica config` chiede di scegliere
se il nodo non e' ancora selezionato, altrimenti ricarica il piano del nodo.
La selezione manuale resta valida durante la sessione.

Su Windows l'eseguibile richiede privilegi amministrativi tramite UAC.
L'app viene compilata e distribuita solo per Windows x64, con runtime incluso.
Le opzioni della CLI storica non sono
gestite dall'eseguibile desktop.

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
installers\Wisegar.DTXInspector.Setup-1.0.3.exe
```

L'installer installa in `Program Files\WGO DTX Inspector`, richiede privilegi
admin e crea cartelle report/log in `ProgramData\WGO DTX Inspector`, shortcut
Start Menu/Desktop e uninstaller Windows standard. Il JSON e' esterno, accanto
all'eseguibile: un aggiornamento lo conserva, cosi' come la disinstallazione.
Le precedenti installazioni per nodo restano separate; le loro configurazioni
personalizzate vanno riportate manualmente nelle sezioni del JSON unificato.
Il repository contiene un solo progetto desktop e un solo script `.iss`.
Il setup mantiene l'identificativo della precedente app: durante un aggiornamento
puo' riutilizzare la cartella gia' installata e conserva il JSON personalizzato.
I nuovi report/log vanno in `ProgramData\WGO DTX Inspector`; quelli precedenti
restano nella vecchia cartella dati.

## Controlli implementati

- Identita' locale della macchina e utente corrente.
- Presenza di directory e file configurati.
- Presenza di processi locali configurati.
- Stato di servizi Windows configurati tramite API Windows in sola lettura.
- Presenza di listener TCP locali configurati tramite API .NET locali.

Non modifica rete, DNS, firewall, servizi, registro, file hosts o
configurazioni di sistema. Le prove di rete risolvono i nomi configurati e aprono
connessioni TCP senza inviare payload applicativi.

Ogni test include la checklist infrastrutturale obbligatoria del nodo e delle
comunicazioni, anche con liste JSON vuote. Configurazione, copertura e limiti:
[Checklist infrastrutturale](docs/infrastructure-checklist.md).

I report dei test sono separati per ruolo, macchina ed esecuzione:
`DTX-<nodo>-<PC>-<data-UTC>-<id>.html`. Ogni report riguarda solo il PC su cui
e' stato eseguito. Se restano requisiti non verificati, il riepilogo indica
`VERIFICA INCOMPLETA`, anche in assenza di FAIL.

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

Il report viene salvato in `ProgramData\WGO DTX Inspector\Reports\DTX-Inventory.html`.

## Configurazione

Un unico file JSON contiene impostazioni comuni e impostazioni specifiche per
nodo. L'app unica usa `appsettings.json`; le impostazioni `common` vengono
unite a quelle del nodo selezionato manualmente.

L'unico file da mantenere e' [configs/appsettings.json](configs/appsettings.json),
copiato accanto all'eseguibile in build e pubblicazione.
`common` contiene i controlli condivisi; `nodes.core`, `nodes.workstation` e
`nodes.client` contengono quelli specifici. Cambiare nodo non cambia file.
E' un punto di partenza: aggiungere controlli specifici dell'installazione.
Il ruolo viene scelto dall'utente e non viene dedotto dai componenti installati.

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

### Impostazioni dall'inventario del PC

Selezionare il nodo e premere `Impostazioni da inventario`. La finestra avvisa
che le impostazioni correnti del nodo saranno sostituite e le personalizzazioni
potrebbero andare perse. `Annulla`, Esc o la chiusura della finestra lasciano
il file invariato; scegliere `Sostituisci impostazioni` per procedere.

L'app legge un nuovo inventario locale e importa directory, processi e servizi
con nome DTX, escludendo lo stesso Node Check. I controlli generati sono
facoltativi e vanno verificati: rappresentano cio' che e' presente sul PC,
non i requisiti ufficiali del prodotto. Le porte TCP non vengono importate
perche' l'inventario non identifica il processo proprietario.

Viene sostituita solo la sezione del nodo selezionato; `common` e gli altri
nodi e la sezione `infrastructure` vengono conservati. La checklist obbligatoria
non viene rimossa dall'importazione. Il JSON precedente viene salvato accanto all'originale
con suffisso `.bak` univoco. Il nuovo JSON viene validato prima della sostituzione
e il piano dei controlli viene ricaricato. Se non vengono rilevati componenti
DTX utili, la configurazione resta invariata. Non vengono modificati servizi,
registro o impostazioni di Windows.

## Build

Build locale:

```bash
dotnet build
```

Pubblicazione manuale dell'app unica Windows x64:

```bash
dotnet publish src/Wisegar.DTXInspector.App/Wisegar.DTXInspector.App.csproj -c Release -r win-x64
```

Verifica delle transizioni UI, senza avviare controlli o aprire report
(richiede un ambiente desktop):

```bash
dotnet run --project tests/Wisegar.DTXInspector.Desktop.Smoke
```

Il progetto desktop usa per default `win-x64`, architettura `x64` e
runtime incluso (self-contained), anche in build Debug. La pubblicazione e'
single-file. `-r win-x64` e' facoltativo; build e publish con un altro RID o
con `SelfContained=false` vengono bloccati. Le impostazioni comuni sono in
`Directory.Build.props`, i vincoli in `Directory.Build.targets`.

Gli eseguibili vengono generati sotto:

```text
src/Wisegar.DTXInspector.App/bin/Release/net10.0/win-x64/publish/
```
