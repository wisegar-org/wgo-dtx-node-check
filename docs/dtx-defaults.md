# Valori DTX predefiniti

Verifica delle fonti ufficiali: 18 settembre 2026. Il JSON unico contiene una
base documentata, non una dichiarazione di compatibilita universale con tutte le
versioni DTX. Le etichette dei controlli riportano versione e condizioni e vengono
incluse nei report di ciascun nodo. Tutti questi controlli hanno `required: false`:
un percorso personalizzato, un componente opzionale o Clinic chiuso producono
WARNING, non un errore obbligatorio. Confermare i valori dell'impianto prima di
renderli obbligatori. La checklist infrastrutturale rimane sempre attiva.

## Core

| Impostazione | Valore distribuito | Fonte / ambito |
| --- | --- | --- |
| Servizio Windows | Nome visualizzato `DTX Studio Core`, stato `Running` | Quick Guide Core 3.9, p. 20 |
| Directory dati | `C:\DTX Studio\core\data` | Silent Installation Core 4.0, p. 7 |
| API HTTPS | TCP 26850 | Core 4.0 |
| Scan Center HTTPS | TCP 26860 | Core 4.0 |
| MCC HTTPS | TCP 26852 | Core 4.0 |
| Core Manager HTTPS | TCP 26862 | Core 4.0 |
| PostgreSQL | TCP 35432 | Core 4.0, solo se usa PostgreSQL |

`matchDisplayName: true` cerca una corrispondenza esatta, senza distinzione tra
maiuscole e minuscole, fra i nomi visualizzati dei servizi Windows. Il nome interno
effettivo viene poi usato per interrogare lo stato e registrato nel report.
Assenza, ambiguita o enumerazione incompleta restano non verificate. Con il valore
predefinito `false`, `name` continua a indicare il nome interno, come nei settings
generati dall'inventario. Non vengono inventati nomi di servizi PostgreSQL o Driver.

Le porte HTTP Core 4.0 (26851, 26861, 26853, 26863) non sono controlli attivi:
HTTP e disabilitato per default nella guida. PostgreSQL puo essere sostituito da
SQL Server. DICOM 104 resta condizionato a `dicomRequired` e a un endpoint esplicito.

## Workstation e client Clinic

| Impostazione | Valore distribuito | Fonte / ambito |
| --- | --- | --- |
| Directory installazione | `C:\Program Files\DTX Studio` | Installation Guide Clinic 4.1, p. 6 |
| Directory dati | `C:\ProgramData\DTX Studio\Clinic` | Silent Installation Clinic 4.3, p. 7 |
| IPC principale | 127.0.0.1 TCP 4417, 4418 | Installation Guide Clinic 4.1, p. 13 |
| IPC acquisizione / dispositivi | 127.0.0.1 TCP 4422 / 4430 | Stessa guida |
| IPC terze parti | 127.0.0.1 TCP 4436, 4437 | Stessa guida |
| IPC PMS | 127.0.0.1 TCP 4429, 4431, 4435 | Stessa guida |
| IPC TWAIN | 127.0.0.1 TCP 4438 | Stessa guida |

Gli IPC sono listener locali opzionali, non porte da aprire tra nodi. Il controllo
processo `DTXStudioClinic` gia presente resta un suggerimento da confermare con
l'inventario. I percorsi sono i default letterali documentati su C:; correggerli
per installazioni su altri dischi. `localDtxDirectories` resta da compilare con i
percorsi effettivi per non trasformare un percorso suggerito in un requisito ACL.

## Collegamenti tra nodi

Non vengono assegnati hostname, IP, DNS, adattatori, utente operativo o endpoint
remoti fittizi. Impostare `infrastructure.coreHostname` e gli endpoint effettivi:
le prove DNS/TCP partiranno a ogni test con timeout (default 2000 ms).
La guida Clinic 4.1 riporta Core HTTPS 33001 / HTTP 33000, personalizzabili:
sono riferimenti storici, non alternative da sondare automaticamente alle porte
Core 4.0. Confermare versione e configurazione prima di compilare gli endpoint
REST, gRPC e servizi dinamici. Un listener locale non prova la raggiungibilita
remota, il protocollo applicativo o l'assenza di ispezione TLS.

L'installer preserva un JSON gia esistente: questi default si applicano alle nuove
installazioni e alla configurazione del repository. Per aggiornare una configurazione
personalizzata, integrare solo le voci pertinenti. L'importazione da inventario
sostituisce i controlli del ruolo selezionato dopo conferma e backup, conservando
le impostazioni infrastrutturali.

## Fonti ufficiali

- [Core 3.9 Quick Guide](https://ifuwebsite.blob.core.windows.net/datastorage/dtx-studio-core/instructions-for-use/QG_DTX_Studio_Core_3.9_EN.pdf)
- [Core 4.0 Silent Installation, GMT 89213](https://ifu.dtxstudio.com/wp-content/uploads/2024/09/GMT89213_DTX_Studio_Core_4.0_Silent_Installation_en.pdf)
- [Clinic 4.1 Installation Guide, GMT 85886](https://ifuwebsite.blob.core.windows.net/datastorage/dtx-studio-clinic/installation-guides/IG_DTX_Studio_Clinic_4.1_en.pdf)
- [Clinic 4.3 Silent Installation, GMT 88163](https://ifu.dtxstudio.com/wp-content/uploads/2024/06/GMT88163_DTX_Studio_Clinic_4.3_Silent_Installation.pdf)
