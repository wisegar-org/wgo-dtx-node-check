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
All'avvio nessun nodo e' selezionato: scegliere esplicitamente dal selettore tra
`DTX Core`, `Workstation`, `Client`, `Scan PC` e `Scan DTX`.
Ogni nodo richiede IP statico e IPv6 disabilitato sulla scheda DTX:
DHCP o indirizzi IPv6 presenti producono FAIL anche sui client.
La toolbar mostra le azioni del contesto selezionato: `Configura nodo`, `Esegui test`,
`Avvia scan`, `Avvia scan PC` e `Apri report`. `Configura nodo` è l'unico accesso
alla configurazione nella barra. `Aiuto > Strumenti avanzati` raccoglie apertura
del JSON, ricarica e impostazioni da inventario; `Aiuto` contiene anche documentazione e About.
Il pulsante Aiuto usa l'icona `?`, con tooltip e nome accessibile; si raggiunge
con Tab e si attiva con Invio o Spazio. Su finestre strette la
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

Aprire l'app, selezionare il nodo, quindi premere `Esegui test`.
`Scan PC` ha un contesto autonomo senza ruolo DTX. Entrambe le
azioni generano un report HTML senza aprirlo automaticamente: usare `Apri report`.
La configurazione
si puo' aprire prima della selezione; `Ricarica piano` aggiorna il contesto DTX scelto.
La selezione manuale resta valida durante la sessione.

La UI usa un tema Fluent chiaro senza ombre: accento rosso `#DA291C`, pulsanti
secondari con icone, superfici bianche e indicatori di esito con colore e testo.
Menu e selettori condividono gli stati di selezione, hover e disabilitazione.
La finestra si apre a 960 × 700 ed è ridimensionabile fino al minimo 800 × 620.
L'icona rappresenta una W di Wisegar come tracciato sanitario dentro un monitor.
Il selettore
del nodo resta obbligatorio; i comandi non disponibili rimangono disabilitati.

Ogni contesto conserva risultati, ricerca, filtro per esito/categoria, attività e ultimo
report. Si può cambiare selezione durante una prova, ma parte una sola operazione alla
volta. I risultati si aggiornano al completamento delle fasi reali; non ci sono
ritardi simulati. `Interrompi` produce dati parziali con segnalazioni esplicite.
I risultati sono mostrati direttamente, senza schede interne. La diagnostica
rimane nel log su disco, apribile da `Aiuto > Apri log completo del contesto selezionato`.

`Configura nodo` apre cinque passi: identità/scheda, Core/DNS, utente/cartelle,
servizi/comunicazioni e riepilogo. Le modifiche restano in bozza fino alla conferma.
Il salvataggio valida il JSON, conserva gli altri nodi e le proprietà sconosciute,
crea un backup e si blocca se il file è cambiato sul disco. `Prova collegamenti`
usa la bozza senza salvarla; `Salva ed esegui test` avvia la checklist completa.
Guida operativa: [selettore e configurazione guidata](docs/desktop-workflow.md).

### Scan DTX

Scegliere `Scan DTX` dal selettore, scegliere esplicitamente il ruolo
Core, Workstation o Client e premere `Avvia scan`. Risultati e report
dello scan sono separati per ruolo e dai test ordinari. Il report HTML
mostra servizi (nome interno, nome visualizzato, stato e PID), processi candidati,
listener e connessioni TCP IPv4/IPv6 con processo proprietario e indirizzi remoti,
schede locali con GUID, IP, DHCP, IPv6 e server DNS. La scheda configurata per DTX
e' distinta dalle altre schede di contesto.

La correlazione usa nomi DTX, processi/servizi configurati e PID. Le porte note
occupate da processi estranei sono solo candidati da verificare. I processi
condivisi non consentono di attribuire una porta a un singolo servizio. Campioni
non atomici e processi terminati possono limitare la correlazione; nessuna porta
viene interpretata come prova del protocollo. Il tool Inspector e' escluso dalla
ricerca euristica DTX. UDP non e' incluso e non viene eseguita alcuna scansione.

Lo scan esegue anche la checklist obbligatoria e le prove DNS/TCP sui target
configurati, con timeout. Target mancanti e letture incomplete restano segnalati.
Il report `Inspection-DTX-<ruolo>-<PC>-<data>-<id>.html` e' separato per esecuzione;
le impostazioni e la rete del PC non vengono modificate.

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
installers\Wisegar.DTXInspector.Setup-0.0.22.exe
```

L'installer installa in `Program Files\Wisegar\WGO DTX Inspector`, richiede privilegi
admin e crea cartelle report/log in `ProgramData\Wisegar\WGO DTX Inspector`, shortcut
Start Menu/Desktop e uninstaller Windows standard. Il JSON e' esterno, accanto
all'eseguibile: un aggiornamento lo conserva, cosi' come la disinstallazione.
Le precedenti installazioni per nodo restano separate; le loro configurazioni
personalizzate vanno riportate manualmente nelle sezioni del JSON unificato.
Il repository contiene un solo progetto desktop e un solo script `.iss`.
Il setup mantiene l'identificativo della precedente app: durante un aggiornamento
propone la nuova cartella sotto `Wisegar` anche negli aggiornamenti e copia il JSON
personalizzato dalla precedente installazione se la destinazione non ne contiene già uno.
La copia originale e gli altri file della vecchia cartella non vengono eliminati.
I nuovi report/log vanno in `ProgramData\Wisegar\WGO DTX Inspector`; quelli precedenti
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

Il pulsante `Avvia scan PC`, nel contesto omonimo, raccoglie dati locali read-only per aiutare a
preparare il file JSON di configurazione:

- macchina, utente, sistema operativo e architettura;
- dischi locali;
- processi raggruppati per nome;
- servizi Windows e stato;
- listener TCP locali;
- programmi installati letti dal registro in sola lettura;
- directory candidate DTX sotto Program Files e ProgramData.

Il report viene salvato in `ProgramData\Wisegar\WGO DTX Inspector\Reports\DTX-Inventory.html`.

## Configurazione

- [Guida alle impostazioni della workstation](docs/workstation-settings.md):
  significato dei campi, dove reperirli, esempi e limiti dei controlli attuali.
- [Piano della configurazione guidata dalla UI](docs/guided-configuration-plan.md):
  progetto di riferimento per passi, validazione e salvataggio protetto.
- [Piano tab per nodo e log leggibili](docs/tabs-and-readable-results-plan.md):
  progetto di riferimento per Core, Workstation, Client e Scan PC.

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
