# Piano UI: tab per nodo e risultati leggibili

Stato: **implementazione consegnata nella 0.0.2**. La [guida operativa](desktop-workflow.md)
descrive il comportamento disponibile. Questo documento conserva il progetto della UI
con il [percorso di configurazione guidata](guided-configuration-plan.md).
La [guida workstation](workstation-settings.md) resta il riferimento per i campi.

## Obiettivo

Sostituire il selettore del ruolo e il riquadro testuale unico con quattro tab:
**DTX Core**, **Workstation**, **Client**, **Ispezione PC**. Ogni tab conserva
configurazione visualizzata, ultima esecuzione, risultati, filtri e report propri.
I tab rappresentano contesti sul **PC corrente**, non PC remoti scoperti in rete.

All'apertura mostrare un pannello neutro: "Scegli cosa vuoi verificare".
Nessun ruolo DTX preselezionato o inferito. La scelta esplicita di un tab attiva
il contesto ma non avvia inventario, prove di rete o scritture di file.
Non introdurre un quinto ruolo nel JSON per simulare la pagina iniziale.

## Aspetto e contenuti dei tab

Usare un'icona e un accento grafico per distinguere i tab, mantenendo testi ed esiti
uniformi. I colori decorativi dei tab non indicano conformita. FAIL/WARNING/PASS
hanno sempre gli stessi simboli, etichette e colori in tutte le viste.

| Tab | Identita visiva | Schede in evidenza | Azioni |
| --- | --- | --- | --- |
| DTX Core | Server, accento blu | Servizi DTX; rete e hostname; DNS; comunicazioni con gli altri nodi | Configura nodo, Esegui test, Ispeziona DTX, Apri report |
| Workstation | Postazione/acquisizione, accento viola | Collegamento al Core; rete locale; directory e utente operativo; componenti di acquisizione/ricostruzione | Stesse azioni, configurazione guidata orientata alla workstation |
| Client | Monitor, accento turchese | Collegamento al Core; rete e DNS; identita; componenti di visualizzazione | Stesse azioni, campi pertinenti al client |
| Ispezione PC | Computer/lente, accento grigio | Sistema e dischi; software; servizi/processi; schede IP/DNS; porte TCP con PID | Avvia inventario, Aggiorna inventario, Apri report, Copia/Esporta |

I tre tab DTX mantengono tutti i requisiti della checklist, anche quelli non presenti
nelle schede in evidenza. In particolare IP statico e IPv6 disabilitato valgono
anche per Client. Non rimuovere controlli per rendere la vista piu' semplice.
Il tab PC non certifica un ruolo DTX e non visualizza un giudizio di conformita DTX.

Il primo inventario PC includera' le informazioni gia raccolte e IP/DNS/porte con
PID riusando i lettori locali. CPU, RAM, GPU e requisiti hardware delle diverse
versioni DTX non sono oggi coperti dall'inventario completo: rinviare la loro
eventuale introduzione a un'estensione esplicita, senza presentarli come disponibili.

## Struttura della finestra

1. Intestazione compatta con nome prodotto, PC corrente e versione.
2. Tab principali, navigabili da tastiera, con contatore di segnalazioni dell'ultima
   esecuzione e indicatore discreto se un lavoro e' in corso.
3. Toolbar contestuale con le azioni del tab. Configurazione/Aiuto mantengono i menu;
   la configurazione avanzata JSON resta disponibile.
4. Riepilogo con ruolo, data ultima esecuzione, stato configurazione e conteggi
   risultati. Mostrare "Mai eseguito" prima del primo run, non un PASS vuoto.
5. Area principale con **Risultati** e **Attivita** come viste distinte; nella
   pagina PC, tabelle per categoria di inventario.
6. Barra di stato con fase corrente, avanzamento reale e azione Annulla quando
   il percorso di cancellazione sara' supportato end-to-end.

Alla larghezza minima le schede riepilogative vanno su piu' righe; i comandi meno
frequenti restano nei menu. Messaggi a capo e dettagli in pannello espandibile,
evitando di obbligare allo scorrimento orizzontale per capire un risultato.

## Rendere leggibili i log mostrati a video

Oggi `_log` e' una TextBox: riceve piano, messaggi RUNNING e risultati finali,
ricostruendo il testo a ogni aggiunta e spostando il cursore alla fine. Il nuovo
modello separa i risultati dei controlli dalla cronologia delle operazioni.

### Vista Risultati

Una riga per controllo, aggiornata da In attesa a In corso e poi all'esito, senza
stampare tre copie della stessa voce. Colonne principali:

| Colonna | Esempio di contenuto |
| --- | --- |
| Esito | Fallito (FAIL), Da verificare (WARNING), Superato (PASS), Non applicabile |
| Categoria | Rete locale, DNS, Servizi, Comunicazioni, Permessi |
| Controllo | IP statico sulla scheda DTX |
| Sintesi | DHCP attivo su Ethernet |
| Dettagli | Pulsante/espansione con valore atteso, rilevato, ambito, evidenze e prossimo passo |

Le descrizioni amichevoli affiancano gli ID tecnici, disponibili nei dettagli e
nelle esportazioni. Per l'ispezione distinguere **Osservato** da **Conforme**:
una riga che attesta un PID o un indirizzo non e' prova del buon funzionamento DTX.
Non modificare il significato di CheckStatus per rendere la UI piu' rassicurante.

Esempio di dettaglio, **puramente illustrativo**:

```text
FALLITO  Rete locale > IP statico
Rilevato: DHCP attivo sulla scheda Ethernet
Atteso:   IPv4 statico sulla scheda DTX selezionata
Ambito:   workstation WS-DTX-01, osservazione locale
Azione:   verificare l'assegnazione statica con il responsabile rete
Dettagli: GUID scheda, IP osservati, data e ID esecuzione
```

Filtri: Tutti / Falliti / Da verificare / Superati / Non applicabili, categoria
e ricerca testuale. Gli stati In attesa/In corso devono essere visibili durante
l'esecuzione. Tutti e' la vista iniziale; eventuali filtri attivi sono espliciti.
Il riepilogo riporta sempre i totali dell'intera esecuzione, oltre al numero di
righe visibili. Filtrare non altera il report o l'esito complessivo.

I dettagli devono includere realmente `CheckResult.Details`, oggi non trasferiti
al DTO GUI `NodeCheckItem`: indirizzi DNS, latenze, PID, nomi servizi, percorsi e
ambito non devono andare persi nel passaggio dal motore alla UI.

### Vista Attivita

- Una cronologia per esecuzione: ora, livello, fase e messaggio breve.
- Distinguere eventi informativi da errori di esecuzione e dai FAIL dei controlli.
  Una connessione rifiutata puo' essere un test fallito senza che l'app sia guasta.
- Stack trace e dati diagnostici sotto **Dettagli tecnici**, non nel messaggio principale.
- Raggruppare eventi ripetitivi senza eliminare evidenze; niente righe decorative
  con sequenze di `====` e niente ripetizione del piano a ogni cambio tab.
- **Segui nuovi eventi** attivo inizialmente; sospenderlo quando l'utente scorre
  indietro. Pulsante "Torna all'ultimo evento" senza spostamenti forzati del cursore.
- **Copia selezione**, **Copia riepilogo**, **Esporta attivita** con contesto PC,
  ruolo/tipo operazione, ora e ID esecuzione. Le esportazioni avvengono su richiesta.
- Lista virtualizzata e aggiunte in batch sul thread UI. Imporre un limite documentato
  agli eventi conservati in memoria, con indicazione delle righe precedenti non
  visualizzate e accesso al file completo; non perdere risultati della checklist.

### Log su file e report

Mantenere distinti il log diagnostico e il report leggibile. Il log tecnico mantiene
timestamp ISO, livello, proprieta e stack trace; aggiungere run ID, contesto e fase.
Eliminare il percorso globale mutabile di `DebugLog` a favore di un contesto per
esecuzione, per evitare di attribuire eventi al tab/file sbagliato.

Ogni report e log ha nome univoco per operazione, macchina, ruolo se presente,
timestamp e ID. Anche l'inventario PC deve smettere di sovrascrivere il report fisso
`DTX-Inventory.html`. "Apri report" apre l'ultimo report di quel tab, mai quello
dell'ultimo tab usato globalmente. Aprire report precedenti non cambia i risultati
dell'esecuzione corrente. Un filtro a video non elimina righe dal report completo.

L'HTML stampabile conserva riepilogo, raggruppamento per categoria, intestazioni
ripetibili e dettagli leggibili senza dipendere solo dai colori. Escapare i dati
locali/DNS e mantenere leggibili hostname, timestamp e ambito su ogni report.

## Stato dei tab, operazioni e cancellazione

- Stato indipendente per tab: bozza, validazione, snapshot della configurazione
  usata, risultati, attivita, filtri, selezione, posizione di scorrimento, ultimo report.
- Nuovo run separato dal precedente; mai appendere risultati di ruoli diversi.
  Se cambia la configurazione, etichettare l'ultimo run come precedente alle modifiche.
- Una sola operazione di raccolta/test alla volta nella prima versione, anche per
  Ispezione PC. Consentire di consultare altri tab durante il lavoro, ma disabilitare
  tutti i nuovi avvii, modifiche di configurazione e importazioni fino alla fine.
- Fissare ruolo, percorsi e snapshot alla partenza. Eventi e completamento tornano
  sempre al tab origine anche se l'utente sta guardando un altro tab. Non leggere
  il tab attivo da un callback per scegliere dove scrivere un risultato.
- Il cambio tab non avvia test, non salva bozze e non cancella dati. Alla chiusura
  con bozza modificata o lavoro attivo proporre conservazione/salvataggio/annullamento
  in modo esplicito; non chiudere silenziosamente perdendo una bozza.
- Cancellazione con token propagato a runner/probe. Le chiamate native sincrone
  gia iniziate possono terminare prima dello stop; la UI indica "Annullamento in corso".
  Esito "Annullato / parziale", nessun falso completamento o PASS complessivo.
  Eventuali report parziali mantengono la checklist con voci non eseguite, chiaramente
  distinte da NotApplicable. Nessuna scrittura tardiva dopo la chiusura del contesto.

## Avanzamento reale

Oggi la UI stampa RUNNING per il piano con ritardi artificiali prima di chiamare
il motore. Rimuovere il ciclo con `Task.Delay(60)`: non descrive l'esecuzione reale.

Il motore emette eventi strutturati tramite un canale di progresso: avvio/fine
fase, avvio/fine controllo, risultato e diagnostica. Ogni voce ha ID stabile nel
run, categoria, timestamp e correlazione con ruolo/macchina. Le liste common e
del ruolo possono contenere nomi duplicati: non usare il testo visibile come ID.
Finche il totale e' ignoto usare avanzamento indeterminato; percentuale solo per
un totale noto. I risultati devono essere identici con e senza osservatore UI.

## Integrazione della configurazione guidata

**Configura nodo** apre il percorso del tab DTX corrente, senza un secondo
selettore ruolo. Da pagina iniziale o Ispezione PC chiedere prima di scegliere
un tab DTX; Ispezione PC non ha un profilo DTX da modificare.

Riutilizzare i servizi di bozza, validazione, anteprima e salvataggio del
[piano dedicato](guided-configuration-plan.md), adattando le etichette per ruolo.
Core mette in evidenza servizi e host server; Workstation utente/cartelle e
acquisizione; Client connessione al Core e rete. Non aggiungere permessi workstation
o servizi Core come requisiti automatici degli altri ruoli.

Conservare un solo `appsettings.json`, i tre profili esistenti, common, campi
sconosciuti, conferma delle sostituzioni e backup. Le proposte rilevate restano
osservazioni da confermare, non baseline approvate automaticamente.

## Modifiche tecniche previste

| Area | Lavoro |
| --- | --- |
| MainWindow | Diventare contenitore di tab e comandi comuni; togliere stato unico `_nodeRole`, `_paths`, `_lastReportPath`, `_log` come fonte globale dei risultati. |
| Stato UI | Modelli per tab e run, comandi con guardie, collezioni osservabili; contesto PC distinto da DtxNodeRole, senza estendere l'enum dei profili per comodita. |
| Viste | Componenti condivisi per riepilogo, risultato, dettaglio e attivita; sezioni mirate al ruolo e tabelle inventario PC. |
| NodeCheckService | Risultati strutturati con ID, dettagli e natura verifica/osservazione; progresso e token; API inventario ricca, non solo la voce generica "Inventario completato". |
| CheckRunner / NetworkChecks / DtxInspection / InventoryRunner | Eventi reali, cancellazione cooperativa, raccolta locale PC senza filtro DTX; preservare timeout, limite concorrenza e nessuna rete nell'inventario. |
| DebugLog / report | Contesto per esecuzione, file univoci, errori di lettura espliciti anche nell'inventario; esportazione e stampa leggibile. |
| Smoke test | Testare comportamento e isolamento dei tab; ridurre dipendenza dagli indici dei figli visuali e dai vecchi nomi di campi privati. |

## Sequenza di consegna

1. **Contratti e stato**: definire contesto/run ID, risultati con dettagli, eventi
   di progresso e coordinatore di esecuzione. Test di isolamento prima del layout.
2. **Tab e viste mirate**: pagina iniziale neutra, quattro tab, toolbar contestuale,
   riepiloghi e ultimo report indipendenti; integrazione coi motori esistenti.
3. **Risultati e log leggibili**: righe espandibili, filtri, ricerca, cronologia,
   avanzamento reale, virtualizzazione, copia/esportazione e report PC univoci.
4. **Configurazione guidata**: integrare il piano dedicato nel tab workstation,
   quindi estenderlo agli altri due ruoli con sezioni pertinenti e salvataggio sicuro.
5. **Verifica e distribuzione**: cancellazione completa, accessibilita, prove
   d'integrazione, documentazione e installer Windows x64 self-contained.

Passi 1-3 costituiscono una prima consegna utile anche prima del wizard.
Durante la transizione restano Apri config e l'importazione con avviso; nessun
comando non implementato viene presentato come funzionante.
Versione nello schema 0.0.x, da incrementare alla consegna effettiva. Nessuna nuova
app, nessun setup per ruolo, nessuna migrazione automatica di configurazioni OS.

## Verifiche di accettazione

- Avvio senza ruolo selezionato; cambiare tab non attiva inventario, rete o scritture.
- Un run Workstation continua a ricevere risultati nel proprio tab mentre si guarda
  Core; avvii simultanei impediti; nessuna contaminazione di report o log.
- Ispezione PC funziona senza config DTX valida e senza usare Client come ruolo
  implicito. Schede e porte sono contesto locale, non conformita DTX.
- Ogni ruolo conserva la checklist completa e i suoi requisiti specifici, con
  report per nodo: DHCP/IPv6, hostname, DNS, VPN/hosts, servizi Core, ACL/privilegi
  workstation, traffico REST/gRPC/servizi/DICOM, TLS/DPI e dipendenza SMB.
- Filtri e ricerca non cambiano conteggi globali, esiti, evidenze o report completi.
  Espansione mostra tutti i Details; dati mancanti e manuali non diventano PASS.
- Nessun RUNNING fittizio prima della partenza del controllo; timeout DNS/TCP e
  annullamento verificati con provider simulati e risultati parziali riconoscibili.
- Bozze preservate navigando; salvataggio isolato per ruolo con controllo conflitti
  e backup; modifica della config rende riconoscibili i risultati precedenti.
- Liste con migliaia di processi/eventi restano reattive; lo scorrimento manuale non
  viene interrotto; eventuale limite eventi e' visibile, report e log completi disponibili.
- Verifica visuale a 760x560 e 980x720, ridimensionamento e scaling Windows 125/150%,
  tastiera e nomi accessibili; icone/esiti comprensibili anche senza colore.
- Test di timestamp/ID, nomi univoci, escaping HTML, copia, export e stampa;
  build/test Windows x64 e prova installer con appsettings esistente preservato.
