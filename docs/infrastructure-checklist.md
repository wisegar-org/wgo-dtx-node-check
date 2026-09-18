# Checklist infrastrutturale DTX

La checklist richiesta per questa installazione e' obbligatoria nei controlli
e nei report. Non e' una certificazione dei requisiti universali del produttore.
Il tool verifica e segnala; non configura il sistema.

## Copertura per nodo

- Core: IP statico, IPv6 disabilitato sulla scheda DTX, hostname invariato dalla
  baseline, DNS interno, VPN/mesh/adapter/hosts, servizi attivi e salute DTX.
- Workstation: IP statico, IPv6, hostname, DNS interno del Core, VPN/mesh/adapter/hosts,
  lettura/scrittura dell'utente operativo sulle directory DTX (in particolare
  `C:\ProgramData\DTX Studio...`) e privilegi amministrativi per installazioni/aggiornamenti.
- Client: IP statico, IPv6 disabilitato sulla scheda DTX, DNS interno del Core;
  hostname invariato; VPN/mesh/adapter/hosts.
- Tutti: DNS bidirezionale, assenza di fallback DNS pubblici lenti, traffico
  consentito/non ispezionato, REST, gRPC, porte TCP dinamiche/servizio,
  DICOM TCP 104 se richiesto, assenza di ispezione SSL/TLS o DPI antivirus/EDR,
  assenza di dipendenza da SMB o unita' mappate nello scambio dati DTX.

## Configurazione per nodo

Compilare `nodes.<nodo>.infrastructure` nel JSON unico:

| Campo | Contenuto |
| --- | --- |
| `adapterId` | GUID della scheda usata da DTX; i GUID disponibili compaiono nel report |
| `expectedHostname` | Nome approvato all'installazione/associazione, da una baseline indipendente |
| `coreHostname`, `expectedCoreAddresses` | Nome DNS e IP attesi del Core |
| `internalDnsServers` | IP dei resolver interni approvati |
| `peerHostnames` | Nomi degli altri nodi per le prove DNS da questo PC |
| `operationalUser`, `localDtxDirectories` | Utente operativo e percorsi locali DTX per verifica ACL |
| `dtxServiceNames` | Nomi Windows reali dei servizi DTX |
| `dicomRequired` | `true`/`false` secondo installazione; `null` significa da confermare |
| `endpoints` | Elementi con `name`, `host`, `port`, `protocol` (`rest`, `grpc`, `dynamic-tcp`, `dicom`, `tcp`) |
| `networkTimeoutMs` | Timeout per tentativo, default 2000 ms, intervallo 100..10000 |
| `dnsMaxLatencyMs` | Soglia DNS, default 250 ms; tarare secondo la rete |
| `manualEvidence` | Note per ID checklist, dichiarazioni mai trasformate automaticamente in PASS |

Gli ID della checklist compaiono nei dettagli del report: `ip`, `ipv6`,
`hostname`, `core-dns`, `vpn`, `hosts`, `services`, `service-health`, `permissions`,
`installation-admin`, `bidirectional-dns`, `dns-fallback`, `traffic`, `rest`,
`grpc`, `dynamic-tcp`, `dicom`, `security-inspection`, `smb`.
Le voci specifiche compaiono solo per il ruolo pertinente.
IP statico e IPv6 disabilitato sono obbligatori per ogni ruolo. Il vecchio campo
`clientRequiresIpv6Disabled` e' ignorato anche nei JSON esistenti: non esclude piu'
il client dal controllo. Non vengono applicate modifiche alla rete del PC.

Massimo 16 peer e 16 endpoint, con quattro prove concorrenti. Ogni nome viene
risolto tre volte; il report mostra risposte e tempi. DICOM usa TCP 104 solo
se dichiarato richiesto e con endpoint configurato. Le porte REST/gRPC/dinamiche
vanno indicate secondo la versione e l'installazione: nessuna porta viene indovinata.
Le prove partono sempre quando si eseguono i test; non all'avvio o durante inventario.

## Interpretazione e limiti

- IP e hostname: evidenza della scheda selezionata e confronto con la baseline.
- DHCP: attivo sulla scheda DTX produce FAIL per tutti i ruoli, anche con DNS stabile.
- IPv6: indirizzi osservati, anche link-local, sono una violazione su ogni ruolo;
  assenza di indirizzi non dimostra che il binding sia disabilitato e resta WARNING.
- VPN/mesh/adapter: euristica sui nomi, non prova esaustiva di presenza/assenza
  o interferenza. Hosts: righe attive riportate, senza modificarle.
- Servizi: stato Windows distinto dalla salute applicativa.
- ACL: lette senza file di prova. Il token elevato del tool non certifica i
  diritti effettivi dell'utente operativo; verificarli nella sua sessione.
- Un PASS DNS/TCP dimostra solo quella prova puntuale: il resolver di sistema puo'
  usare cache/hosts e non identifica il server che ha risposto. Non prova DNS interno,
  stabilita' nel tempo, bidirezionalita', salute REST/gRPC/DICOM o assenza di ispezione.
- Policy EDR/firewall, inoltri DNS a monte, privilegi delle precedenti installazioni
  e dipendenza da SMB richiedono verifica operativa. Restano WARNING, con note esplicite.
- Dati mancanti o prove non eseguite non sono PASS. Condizioni dichiarate non
  applicabili sono `NotApplicable`, non successi. WARNING lascia la verifica incompleta.

Generare un report per ciascun nodo fisico e ruolo. Il nome include ruolo,
hostname, data UTC e ID univoco; nessuna esecuzione sovrascrive la precedente.
Le prove dal Core non certificano quelle dalla workstation o dal client.

Riferimenti API per interpretare le osservazioni: [stato dei servizi Windows](https://learn.microsoft.com/en-us/windows/win32/services/service-status-transitions)
e [supporto dei protocolli di un'interfaccia](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.networkinterfacecomponent?view=net-10.0).
