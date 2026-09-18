# Piano: configurazione guidata del nodo

Stato: **percorso guidato disponibile nella 0.0.2**; vedere la
[guida operativa](desktop-workflow.md) per il comportamento consegnato.
Questo documento conserva il progetto di riferimento.
Prima consegna focalizzata sulla workstation, con componenti riutilizzabili per
Core e Client. Riferimento dei campi: [guida workstation](workstation-settings.md).

Integrazione aggiornata: il [piano tab e risultati leggibili](tabs-and-readable-results-plan.md)
definisce il contenitore della UI. Configura nodo usa il ruolo del tab DTX corrente;
dalla pagina iniziale o Ispezione PC si richiede una scelta esplicita di un tab DTX.
I passi del wizard e le regole di salvataggio qui descritte restano validi.

## Obiettivo e accesso

Aggiungere **Configurazione > Configura nodo...**. Senza ruolo selezionato,
chiedere Core / Workstation / Client senza proporne uno automaticamente. Mostrare
sempre ruolo, PC e file destinazione. Il percorso guidato modifica solo i settings
dell'Inspector: non promettere di configurare Windows o DTX Studio Clinic.

Usare una finestra con passi, Avanti/Indietro e riepilogo finale. Campi essenziali
visibili; timeout, note e liste tecniche espandibili. Per ciascun campo: etichetta
comprensibile, aiuto breve, valore e origine (salvato / rilevato / inserito).
Non mostrare JSON come interfaccia principale; mantenere Apri config per uso avanzato.

## Percorso proposto

| Passo | Cosa vede l'utente | Comportamento |
| --- | --- | --- |
| 1. Workstation e rete | Nome attuale; nome approvato; schede con nome, stato, IPv4 e DNS | Pulsante **Rileva dati locali** in sola lettura. Scegliere la scheda senza digitare GUID. Nessuna selezione automatica, anche con una sola scheda. Nome approvato confermato dall'utente, non ricavato silenziosamente dal nome attuale. IP statico e IPv6 disabilitato mostrati come requisiti fissi. |
| 2. Server Core e DNS | Nome del Core, IP attesi, DNS interni, altri nodi | Accettare nome o URL copiato da Clinic, ma estrarre nome/porta in una proposta da confermare. Rifiutare credenziali nell'URL, non memorizzarle. Nessuna IP discovery o scansione. IP e DNS rilevati sono osservazioni, non baseline approvate. |
| 3. Utente e dati | Account operativo; elenco cartelle con **Sfoglia** e **Aggiungi** | Proporre cartelle DTX rilevate, da selezionare. Chiedere conferma dell'account: quello amministrativo dell'Inspector puo' non essere quello operativo. Nessuna password. Solo percorsi locali assoluti, niente UNC o unita' di rete. |
| 4. Servizi e comunicazioni | Liste leggibili di servizi, processi e porte; DICOM Si / No / Da confermare | Scegliere solo componenti pertinenti. Per ogni endpoint: descrizione, destinazione, porta e tipo. Separare **porte locali** da **collegamenti ad altri nodi**. PID osservati non vengono salvati come requisiti permanenti. Nessuna attribuzione automatica di REST/gRPC da numero di porta. |
| 5. Riepilogo e salvataggio | Valori modificati, dati mancanti, requisiti ancora manuali, percorso backup | **Salva** oppure **Salva ed esegui test**. Conferma concreta delle sostituzioni. **Annulla** non scrive nulla. Se un dato manca, consentire **Salva incompleto** con elenco esplicito delle verifiche non eseguibili. |

Le porte suggerite dal catalogo devono riportare versione e condizione d'uso;
se non confermate restano suggerimenti non salvati. Un servizio non presente puo'
essere aggiunto manualmente come atteso: non limitare la configurazione a cio' che
oggi risulta installato. Lasciare facoltativi i componenti opzionali.

## Prove durante la compilazione

- L'apertura della finestra e **Rileva dati locali** non fanno richieste di rete.
  Non riusare direttamente l'azione Ispeziona DTX, che avvia anche le prove di rete.
- Un pulsante **Prova collegamenti** usa la bozza corrente con i timeout previsti.
  Mostrare quali nomi/endpoint saranno verificati; non salvare implicitamente.
  Le prove complete con **Esegui test** restano sempre attive sui target configurati,
  senza introdurre un'opzione generale per disabilitarle.
- Separare «dato rilevato», «configurazione completa» e «prova riuscita». Una risposta
  DNS non approva automaticamente l'IP atteso. Il successo TCP non certifica DTX.
- Esecuzione asincrona, avanzamento e annullamento; annullare interrompe le nuove
  prove e quelle pendenti tramite token. Modifica/salvataggio bloccati durante la prova.
  Nessun risultato di questa anteprima vale come certificazione infrastrutturale.
- Non generare report riepilogativi del nodo incompleti dalla sola anteprima.
  I report finali di Esegui test/Ispeziona DTX mantengono tutta la checklist e la
  separazione per ruolo, macchina e singola esecuzione.

## Validazione

Distinguere **errore di formato** (non salvabile) da **dato non ancora disponibile**
(bozza salvabile, ma copertura incompleta). Riutilizzare il loader come validazione
finale; aggiungere messaggi accanto ai campi nella UI.

- Scheda scelta per GUID esistente; se scollegata o non piu' presente mostrare
  l'anomalia, senza sostituirla con la prima scheda disponibile.
- Nome workstation confrontabile con il nome breve locale; richiedere conferma
  della baseline. Core/peer preferibilmente DNS, niente URL nei campi JSON hostname.
- Liste IP valide e senza duplicati; IPv6 eventualmente osservato evidenziato come
  violazione della policy, non trasformato in valore suggerito da approvare.
- Destinazione endpoint valida, porta 1..65535, protocolli supportati; massimo
  16 endpoint e 16 peer. DICOM attivo richiede endpoint TCP 104 per copertura completa;
  disattivarlo con un endpoint DICOM esistente richiede rimuovere/correggere la voce
  nel riepilogo, non cancellarla silenziosamente.
- Timeout 100..10000 ms; soglia DNS 1..10000 ms. Avviso se la soglia e' superiore
  al timeout. Default rispettivamente 2000 e 250.
- Cartelle assolute locali, account non vuoto per copertura ACL; account non
  verificabile o cartella assente sono segnalazioni, non ragioni per inventare valori.
- Mostrare i controlli ereditati da `common` come tali; non duplicarli nelle liste
  del nodo o suggerire che eliminarli dalla bozza locale li disabiliti.
- Note manuali selezionabili dagli ID della checklist e annotate come dichiarazioni;
  non convertire WARNING in PASS in base a una casella di conferma.

## Salvataggio e protezione delle personalizzazioni

1. All'apertura leggere il JSON e conservare una copia in memoria con hash del file.
   Lavorare su una bozza separata dal modello in uso. Con file invalido mostrare
   l'errore e offrire Apri config; non rigenerarlo sovrascrivendo le personalizzazioni.
2. Applicare al JSON originale solo i campi gestiti del ruolo scelto. Conservare
   `common`, gli altri nodi e le proprieta sconosciute anche dentro il nodo,
   `infrastructure` e gli elementi esistenti delle liste. Segnalare che l'eventuale
   riserializzazione normalizza formattazione/commenti; il backup conserva l'originale.
3. Mostrare modifiche e rimozioni prima di salvare. Se il file e' cambiato sul disco
   rispetto all'hash iniziale, fermare la scrittura e proporre di ricaricare; nessun
   merge silenzioso. Controllare ancora nella fase di scrittura per limitare le race.
4. Scrivere file temporaneo nella stessa cartella, validare, sostituire atomicamente
   con backup univoco. Con errore di accesso/spazio/validazione lasciare l'originale
   integro e la bozza disponibile. Non auto-ripristinare backup su file modificati.
5. Ricaricare il piano solo dopo salvataggio riuscito. L'opzione Salva ed esegui test
   avvia il percorso standard, con tutti i controlli e report separato.

Non riusare `InventoryConfiguration.Create` per salvare il wizard: sostituisce
le liste del ruolo ed e' pensato per l'importazione da inventario. Riutilizzare o
estrarre la logica di scrittura atomica/backup di `InventoryConfiguration.Save`,
aggiungendo gestione dei conflitti. Non introdurre un secondo file di configurazione.

## Lavori previsti, in ordine

1. **Modello bozza e servizio di salvataggio**: lettura selettiva, provenienza valori,
   modifica conservativa del JSON, validazione, confronto modifiche, conflitti e backup.
2. **Finestra guidata**: comando di menu, passi 1-5, suggerimenti locali su richiesta,
   form per endpoint e componenti. Mantenere scelta manuale del ruolo e guardie busy.
3. **Anteprima di rete**: esporre un servizio per le prove sulla bozza, riusando
   NetworkChecks; aggiungere cancellazione esterna senza perdere timeout/concorrenza.
4. **Integrazione e consegna**: Salva/Salva ed esegui test, documentazione, smoke test,
   build Windows x64 self-contained e installer. La numerazione resta nello schema
   0.0.x e viene aggiornata alla consegna effettiva, non per questo piano.

## Criteri di accettazione

- Una workstation si configura senza scrivere JSON, con aiuti e indicazioni sui
  dati mancanti; nessun nome, IP, DNS o ruolo viene approvato automaticamente.
- Aprire/chiudere/annullare il wizard non modifica file o sistema e non genera rete.
- Salva incompleto mantiene visibili i limiti; JSON formalmente errato non viene scritto.
- Test su preservazione di altri nodi/common/proprieta sconosciute, conflitti,
  permessi negati, backup e file inizialmente invalido.
- Test sui limiti peer/endpoint/timeout, DICOM, scheda rimossa e differenza fra
  utente operativo e amministratore; test con provider DNS/TCP simulati e timeout.
- UI utilizzabile alla dimensione minima, navigabile da tastiera, con errori
  associati ai campi. Nessuna perdita di bozza passando avanti/indietro.
- Ogni esecuzione completa conserva IP statico, IPv6 disabilitato, identita, DNS,
  VPN/hosts, permessi, privilegi installazione, traffico/protocolli, TLS/DPI e SMB
  nella checklist; report separati e note manuali senza falsi PASS.
