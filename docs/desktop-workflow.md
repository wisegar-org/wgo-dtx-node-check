# Selettore e configurazione guidata — 0.0.19

I cinque contesti del selettore rappresentano ruoli o modalità di scan sul **PC corrente**, non computer remoti.
Nessun contesto è selezionato all'avvio. La scelta non avvia scansioni o test.

La finestra iniziale misura 960 × 700 ed è ridimensionabile. Il tema Fluent chiaro
usa rosso `#DA291C`, superfici bianche e nessuna ombra. Menu, selettori e pulsanti
usano una palette comune anche negli stati selezionato, hover e disabilitato.
L'azione principale è sempre il primo pulsante dopo il selettore. Il pulsante
`?` apre Aiuto; `Configura nodo` è l'unico accesso alla configurazione nella toolbar.
La riga duplicata ruolo/nome PC sotto i pulsanti è stata rimossa: stato, data e
conteggi sono nel banner. I report si aprono solo con `Apri report`.

| Selettore | Contesto |
| --- | --- |
| DTX Core | Servizi, identità, rete e comunicazioni del server |
| Workstation | Acquisizione/ricostruzione, utente operativo e directory DTX |
| Client | Visualizzazione, identità e comunicazioni con il Core |
| Scan PC | Inventario locale di software, servizi, processi, porte, IP e DNS |
| Scan DTX | Correlazione servizi/processi/porte DTX, IP e DNS, checklist del ruolo scelto |

Per **Scan DTX** scegliere anche il ruolo Core, Workstation o Client nel
selettore dedicato, poi premere **Avvia scan**, il pulsante principale.
Nessun ruolo è dedotto da installazioni o selezioni precedenti dei test ordinari.
Ogni ruolo conserva separatamente risultati e report dello scan.

## Configurare un nodo

1. Selezionare il nodo DTX e premere **Configura nodo**.
2. Inserire il nome approvato all'installazione. **Rileva dati locali** mostra le
   schede con indirizzi e DNS: selezionare quella DTX, senza approvare implicitamente
   i dati osservati. IPv4 statico e IPv6 disabilitato restano requisiti fissi.
3. Inserire nome Core, IP approvati, DNS interni e altri nodi (uno per riga).
   Un URL può proporre il nome Core previa conferma, ma non aggiunge porte o protocolli.
   Gli URL con credenziali vengono rifiutati.
4. Per la workstation indicare l'utente operativo e le cartelle locali DTX.
   **Sfoglia e aggiungi cartella** evita di digitare i percorsi. Non inserire password,
   percorsi UNC o unità di rete. L'account dell'Inspector può essere diverso.
5. Verificare i componenti salvati e modificare le liste espandibili. Le porte locali
   sono separate dagli endpoint remoti; per questi scegliere host, porta e tipo.
   Non dedurre REST/gRPC da un numero. DICOM richiede una scelta esplicita;
   un endpoint DICOM deve avere porta 104 e applicabilità confermata.
6. Facoltativamente usare **Prova collegamenti della bozza**: DNS/TCP con timeout,
   nessuna scrittura e nessun report di conformità. Si può interrompere la prova.
7. Leggere modifiche, rimozioni e dati mancanti nel riepilogo. Confermare, quindi
   scegliere **Salva**, **Salva incompleto** oppure **Salva ed esegui test**.

Il salvataggio riguarda solo `appsettings.json`: nessuna modifica a Windows o DTX.
La copia `.bak` conserva i byte del file precedente, inclusi commenti e formattazione.
Il nuovo JSON normalizza commenti/formattazione, preservando campi sconosciuti,
controlli comuni e altri nodi. Se il file cambia sul disco, il salvataggio si ferma:
chiudere e riaprire il wizard per ricaricarlo. La bozza resta disponibile dopo un errore.
Annullare o chiudere una bozza modificata richiede conferma dello scarto.

## Leggere le verifiche

Il banner raccoglie descrizione del nodo, stato/data dell'esecuzione, conteggi e
avanzamento. **Risultati** contiene filtri per esito/categoria, ricerca e dettagli della riga
selezionata. I conteggi riguardano tutti i risultati, anche con filtri attivi.
Le osservazioni locali sono **OSSERVATO**, separate da **SUPERATO**.
**DA VERIFICARE** include dati mancanti, limiti di lettura e requisiti manuali.
Le annotazioni manuali non trasformano una verifica in PASS.

I risultati sono mostrati direttamente, senza il tab Attività. La fase corrente
resta visibile nello stato dell'esecuzione. La diagnostica viene conservata su file:
usare **Aiuto > Apri log completo del contesto selezionato** per consultarla.

È possibile selezionare altri contesti durante una prova: risultati e report restano
nel contesto che l'ha avviata. Tutte le nuove operazioni e le modifiche alla config
sono bloccate fino alla fine. **Interrompi** cancella DNS/TCP pendenti e ferma le
successive fasi locali; una chiamata nativa già in corso termina prima dello stop.
Le sezioni non eseguite restano segnalate e la checklist obbligatoria non scompare.

Ogni esecuzione genera report e log univoci. I report DTX sono distinti per ruolo,
macchina, data e identificativo; quelli dello scan PC non attestano conformità.
I report HTML raggruppano i controlli per categoria e includono regole di stampa.

La verifica applicativa DTX, la direzione remota del DNS, l'assenza di ispezione
TLS/DPI, i privilegi usati nelle installazioni e l'indipendenza da SMB richiedono
ancora evidenze specifiche. Un collegamento TCP riuscito non risolve questi punti.
