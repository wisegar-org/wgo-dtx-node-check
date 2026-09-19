# Configurare i test della workstation

Questa guida descrive le funzionalita **gia disponibili**. Il percorso guidato
proposto per la UI e' nel [piano di implementazione](guided-configuration-plan.md).

Le impostazioni dicono all'Inspector **che cosa verificare**. Non cambiano IP,
DNS, IPv6, servizi, firewall o configurazione di DTX Studio Clinic.
IP statico e IPv6 disabilitato sulla scheda DTX sono requisiti automatici di tutti
i ruoli: non occorre abilitarli nel JSON. Il tool non imposta un indirizzo fisso.

## Procedura attuale

1. Selezionare **Workstation** nella toolbar. Il ruolo non viene dedotto dal PC.
2. Avviare Clinic e i componenti necessari al flusso da provare. Usare
   **Scan DTX** per vedere GUID delle schede, IP/DNS, servizi, PID e porte.
   Questa azione esegue anche i controlli e le prove di rete gia configurate.
   Un primo report con dati mancanti puo' contenere WARNING: e' previsto.
3. Recuperare dal responsabile dell'impianto il nome approvato della workstation,
   il nome DNS/IP del Core, i DNS interni e gli endpoint realmente necessari.
   Un dato osservato oggi non e' automaticamente il valore corretto atteso.
4. Aprire **Configurazione > Apri config**. Modificare
   `nodes.workstation.infrastructure` in `appsettings.json`, accanto all'eseguibile.
   Non sostituire l'intero file con un frammento di questa guida.
5. Completare, se necessari, i controlli locali in `nodes.workstation.required*`.
   Salvare e usare **Configurazione > Ricarica config**, poi **Esegui test**.
6. Eseguire la prova applicativa con l'utente operativo e verificare dal Core anche
   la direzione opposta. Conservare report distinti per macchina, ruolo ed esecuzione.

`Impostazioni da inventario` propone una sostituzione dei controlli locali dopo
avviso e backup: **non compila questi campi infrastrutturali**. Li conserva se
esistono. Non usarla come conferma automatica dei requisiti dell'impianto.

## Come interpretare campi vuoti e valori attesi

Il JSON puo' essere valido anche se incompleto. `null` significa dato non indicato;
`[]` significa lista vuota. Le liste e `manualEvidence` non accettano `null`.
L'assenza di dati non certifica il requisito: il relativo controllo puo' restare
non verificato. Un errore di formato puo' invece impedire il caricamento del file.

Le liste `required*` di `common` vengono aggiunte a quelle della workstation.
`infrastructure` va compilata nel nodo: non viene ereditata da `common`.

## Scheda di rete e identita

| Campo | Cosa inserire e dove trovarlo | Effetto e limiti |
| --- | --- | --- |
| `adapterId` | GUID esatto della scheda DTX, riportato in **Scan DTX** come `adapterId`. Scegliere in base al collegamento effettivo al Core, non solo al nome Ethernet/Wi-Fi. | Seleziona la scheda su cui verificare IP, DHCP, IPv6 e DNS. Senza selezione, o con scheda inattiva, i controlli possono restare non verificati. |
| `expectedHostname` | Nome breve approvato della workstation, dal verbale di installazione/associazione. | Confrontato con il nome macchina locale senza distinguere maiuscole/minuscole. Non usare il nome del Core o un FQDN al posto del nome breve. Copiare il nome corrente senza baseline non dimostra che sia rimasto invariato. |

DHCP attivo produce FAIL. Non e' presente un campo per confrontare l'IP locale
con uno specifico IP atteso: il controllo osserva IPv4 e DHCP, ma non certifica
unicita' dell'indirizzo, correttezza di subnet o gateway.
IPv6 osservato, anche link-local, produce FAIL; nessun indirizzo IPv6 osservato
resta WARNING perche' non prova la disabilitazione del binding sulla scheda.

## Core, DNS e altri nodi

| Campo | Cosa inserire e dove trovarlo | Effetto e limiti |
| --- | --- | --- |
| `coreHostname` | Nome DNS interno del Core, preferibilmente completo, confermato dal responsabile rete o dalla connessione configurata in Clinic. Esempio fittizio: `core.dtx.example.test`. | Tre risoluzioni dal PC corrente. Inserire solo il nome, senza `https://`, percorso o porta. Un IP letterale e' accettato dal formato ma non verifica DNS e produce WARNING. |
| `expectedCoreAddresses` | Lista di IP approvati del Core, ad esempio `["192.0.2.10"]` (fittizio). | Se valorizzata, una risposta DNS con IP esterni alla lista produce FAIL. Non richiede che ogni IP della lista sia restituito. Se vuota manca il confronto con la destinazione attesa. Non ricavare la baseline solo dalla risposta DNS che si vuole verificare. |
| `internalDnsServers` | IP dei resolver interni autorizzati, confermati dall'amministratore della rete. Quelli osservati sono nel report delle schede. | Confronto con i DNS configurati sulla scheda DTX. Non forza le query verso questi server e non prova quale resolver abbia risposto o gli inoltri a monte. Non inserire l'IP del Core salvo che sia davvero anche un DNS. |
| `peerHostnames` | Nomi DNS degli altri nodi da risolvere dalla workstation, massimo 16. | Tre risoluzioni per nome. Non sono sottoreti da scandire. Non verifica automaticamente la direzione inversa: ripetere dal nodo remoto. |

Le risposte usano il resolver di sistema: cache e file hosts possono intervenire.
Il successo della singola prova DNS non chiude automaticamente i requisiti
«DNS interno», «assenza di fallback pubblico» e «bidirezionalita» della checklist.

## Utente, directory e servizi

| Campo | Cosa inserire e dove trovarlo | Effetto e limiti |
| --- | --- | --- |
| `operationalUser` | Account che usa Clinic, ad esempio `CLINICA\\operatore` o `WS01\\operatore` nel testo JSON. Confermare con chi usa il PC. | Identifica l'utente cui riferire la verifica permessi. Non cambia utente, non esegue impersonazione e non richiede password. L'Inspector elevato puo' essere eseguito con un altro account. |
| `localDtxDirectories` | Percorsi assoluti locali dei dati DTX effettivamente utilizzati. `C:\ProgramData\DTX Studio\Clinic` e' un default documentato, da confermare. | Verifica presenza/accessibilita e legge ACL quando possibile. Non scrive file di prova; i diritti effettivi restano da verificare con l'utente operativo. Percorso atteso assente/inaccessibile puo' produrre FAIL. Usare percorsi locali, non UNC o unita' mappate. Le variabili come `%ProgramData%` non vengono espanse. |
| `dtxServiceNames` | Nomi **interni** dei servizi riconosciuti come DTX, dal campo `serviceName` dello scan. | Aiuta la correlazione servizi/PID nell'ispezione. La voce infrastrutturale «servizi attivi» e' specifica del Core: sulla workstation usare `requiredServices` per verificare obbligatoriamente stato e presenza. |

I nomi dei servizi possono cambiare tra versioni/installazioni. Non copiare sulla
workstation i servizi del server Core se non sono realmente previsti su quel PC.

## Comunicazioni da verificare

`endpoints` contiene al massimo 16 destinazioni. Ogni elemento ha questi campi:

| Campo | Significato |
| --- | --- |
| `name` | Etichetta leggibile nel report, obbligatoria. |
| `host` | Nome DNS o IP della destinazione, senza schema, percorso o porta. Non e' ricavato automaticamente da `coreHostname`. |
| `port` | Porta TCP intera, da 1 a 65535, confermata per la versione e l'impianto. |
| `protocol` | `rest`, `grpc`, `dynamic-tcp`, `dicom` o `tcp`, in minuscolo. Etichetta il requisito a cui associare la prova; non seleziona un test applicativo del protocollo. |

Esempio **solo di sintassi**, con nome e porta fittizi da sostituire:

```json
{
  "name": "API Core - porta confermata dall'installatore",
  "host": "core.dtx.example.test",
  "port": 12345,
  "protocol": "rest"
}
```

La prova apre una connessione TCP con timeout, senza payload HTTP, TLS, gRPC o
DICOM. Una connessione riuscita non prova autenticazione, certificati, salute
applicativa o assenza di ispezione. Una porta dinamica deve essere confermata per
la sessione/servizio: non si effettuano scansioni o espansioni di intervalli.
I default documentati sono elencati in [Default DTX](dtx-defaults.md), con le
versioni a cui si riferiscono; non sono endpoint universali da abilitare tutti.

`dicomRequired` e' a tre stati: `true` se DICOM e' richiesto, `false` se non
applicabile, `null` se ancora da confermare. Nel caso `true`, aggiungere l'endpoint
appropriato con `protocol: "dicom"` e porta `104`. Il validatore attuale accetta
un endpoint DICOM solo con questa combinazione. Dichiarare `true` senza endpoint
non esegue da solo alcuna connessione. `false` produce NotApplicable per DICOM.

## Tempi e note

| Campo | Default / valori ammessi | Uso |
| --- | --- | --- |
| `networkTimeoutMs` | `2000`; intero 100..10000 | Limite per singola risoluzione DNS o tentativo TCP, non durata massima dell'intera esecuzione. |
| `dnsMaxLatencyMs` | `250`; intero 1..10000 | Soglia oltre cui una risoluzione riuscita e' segnalata come lenta. Risposte variabili tra i tre campioni producono comunque WARNING. |
| `manualEvidence` | `{}`; oggetto con ID checklist come chiavi e note testuali come valori | Registrare chi ha verificato cosa e quando. Esempio: `{"permissions":"2026-09-19, tecnico: prova da eseguire con account operativo"}`. Nessuna nota trasforma automaticamente WARNING in PASS. |

Gli ID ammessi dalla checklist sono descritti nella
[documentazione infrastrutturale](infrastructure-checklist.md). Il loader attuale
non rifiuta chiavi di note sconosciute, ma queste non vengono mostrate da un controllo
che non ha quell'ID: usare gli ID effettivi, non etichette inventate.

## Esempio di sezione infrastrutturale

Valori dimostrativi, **non pronti per il proprio impianto**. Sostituire GUID,
hostname, IP, utente e percorsi. `endpoints` resta volutamente vuoto finche le
porte non sono confermate; i relativi requisiti rimarranno non verificati.
Inserire questo oggetto soltanto in `nodes.workstation.infrastructure`:

```json
{
  "adapterId": "{11111111-2222-3333-4444-555555555555}",
  "expectedHostname": "WS-DTX-01",
  "coreHostname": "core.dtx.example.test",
  "expectedCoreAddresses": ["192.0.2.10"],
  "internalDnsServers": ["192.0.2.53"],
  "peerHostnames": ["ws02.dtx.example.test"],
  "operationalUser": "CLINICA\\operatore",
  "localDtxDirectories": ["C:\\ProgramData\\DTX Studio\\Clinic"],
  "dtxServiceNames": [],
  "dicomRequired": null,
  "endpoints": [],
  "networkTimeoutMs": 2000,
  "dnsMaxLatencyMs": 250,
  "manualEvidence": {}
}
```

## Controlli locali fuori da infrastructure

Queste liste sono sorelle di `infrastructure`, dentro `nodes.workstation`:

| Lista | Cosa verifica | Scelta per la workstation |
| --- | --- | --- |
| `requiredDirectories` / `requiredFiles` | Presenza del percorso | Inserire solo componenti confermati. Non sostituisce la verifica ACL delle directory dati. |
| `requiredProcesses` | Processo avviato, tramite nome senza percorso | Confermare il nome rilevato e avviare Clinic prima del test se il processo e' obbligatorio. |
| `requiredServices` | Servizio Windows presente e stato atteso | Usare `name` interno; `expectedStatuses: ["Running"]` per servizi che devono essere attivi. `displayName` e' un'etichetta; con `matchDisplayName: true` e' invece `name` a essere cercato fra i nomi visualizzati. |
| `requiredTcpListeners` | Listener sul PC locale | Non controlla una porta remota del Core. Le porte IPC Clinic su `127.0.0.1` restano locali. La presenza non prova il processo proprietario: usare Scan DTX per la correlazione. |

`required: true` rende l'assenza un FAIL; `false` la segnala come WARNING.
Errori di lettura o corrispondenze ambigue possono restare non verificati anche
per elementi obbligatori. I default distribuiti sono facoltativi finche non sono
confermati. I campi vuoti di queste liste non rimuovono la checklist obbligatoria.

## Quando la configurazione e' sufficiente

Per le prime prove: scheda DTX, nome workstation approvato, nome/IP del Core e
DNS interni. Per coprire i requisiti workstation: aggiungere utente e directory,
endpoint richiesti, decisione DICOM e controlli locali pertinenti. Indicare i peer
necessari per la direzione workstation verso altri nodi.

Una configurazione completa non garantisce un report tutto PASS. Restano da
verificare con evidenze operative i diritti dell'utente, i privilegi usati per
installazioni/aggiornamenti, VPN/mesh/adapter e hosts interferenti, DNS bidirezionale
senza fallback pubblici lenti, traffico consentito/non ispezionato, REST/gRPC/servizi
dinamici e DICOM se richiesto, assenza di ispezione TLS/DPI AV/EDR e assenza di
dipendenza da SMB/mapping. Eseguire infine un flusso DTX autorizzato di
acquisizione/importazione, ricostruzione, salvataggio e riapertura dal Core/altro nodo.
Mantenere separati esiti automatici e verifiche manuali nei report di ciascun nodo.
