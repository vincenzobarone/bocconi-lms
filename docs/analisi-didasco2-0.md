# Didasco LMS — Documento di Analisi
## Vantaggi della migrazione dalla piattaforma esistente e requisiti soddisfatti

**Documento:** Analisi tecnica e funzionale  
**Cliente:** Università Bocconi  
**Data:** Maggio 2026  
**Versione:** 1.1

---

## Indice

1. [Premessa](#1-premessa)
2. [Confronto tecnologico: .NET Standard vs .NET 10](#2-confronto-tecnologico-net-standard-vs-net-10)
3. [SQL Server + OpenText vs MySQL + gestione documentale integrata](#3-sql-server--opentext-vs-mysql--gestione-documentale-integrata)
4. [Vantaggi architetturali di Didasco](#4-vantaggi-architetturali-di-didasco)
5. [Requisiti del cliente soddisfatti](#5-requisiti-del-cliente-soddisfatti)
6. [Accessibilità e standard normativi](#6-accessibilità-e-standard-normativi)
7. [Conclusioni](#7-conclusioni)

---

## 1. Premessa

La piattaforma **Didasco** è il nuovo sistema LMS (Learning Management System) sviluppato per l'Università Bocconi in sostituzione della precedente soluzione basata su **ASP.NET con .NET Standard**, con **SQL Server** come database relazionale e **OpenText** come sistema di gestione documentale. Il presente documento illustra i vantaggi tecnici e funzionali del nuovo approccio, le motivazioni della scelta tecnologica e la corrispondenza tra i requisiti espressi dal cliente e le funzionalità implementate.

---

## 2. Confronto tecnologico: .NET Standard vs .NET 10

### 2.1 Ciclo di vita e supporto

| Aspetto | Vecchia piattaforma (.NET Standard) | Didasco (.NET 10 LTS) |
|---|---|---|
| **Stato** | Fine vita (deprecated dal 2021) | Attivo — supporto ufficiale Microsoft |
| **Aggiornamenti di sicurezza** | Non garantiti | Patch mensili garantite da Microsoft |
| **Supporto a lungo termine** | Nessuno | LTS attivo (.NET 10 LTS, supporto fino a novembre 2028) |
| **Compatibilità futura** | Bloccata | Aggiornamento annuale al costo di pochi giorni |

.NET Standard era una specifica di interfaccia, non un runtime autonomo. Le applicazioni costruite su di esso dipendevano da runtime ormai obsoleti (ASP.NET 4.x su Windows, Mono su Linux) privi di aggiornamenti di sicurezza. Microsoft ha ufficialmente dichiarato .NET Standard 2.x come il punto finale della linea: tutti gli investimenti futuri sono su .NET (Core) 5 e successivi.

### 2.2 Prestazioni

.NET 10 (LTS, supporto Microsoft fino a novembre 2028) introduce miglioramenti sostanziali rispetto alle versioni precedenti e, a maggior ragione, rispetto all'ecosistema .NET Standard:

- **Kestrel HTTP Server**: il server web integrato di ASP.NET Core gestisce fino a **10 volte più richieste al secondo** rispetto a IIS+ASP.NET 4.x nelle configurazioni equivalenti (benchmark ufficiali Microsoft, TechEmpower Framework Benchmarks 2024).
- **JIT e AOT compilation**: il compilatore Just-In-Time di .NET 10 produce codice nativo ottimizzato; le operazioni critiche (hashing password, parsing JSON, query SQL) sono sensibilmente più veloci.
- **Garbage Collector migliorato**: riduzione delle pause GC del 30-40% rispetto a .NET Standard, con minor impatto sulla latenza delle richieste HTTP concorrenti.
- **Async/await nativo**: ASP.NET Core è costruito interamente su I/O asincrono. Ogni operazione di database, lettura file e invio email in Didasco è non bloccante, permettendo al server di gestire molte richieste concorrenti con un singolo thread pool.

### 2.3 Portabilità e deploy

| Scenario | .NET Standard | .NET 10 |
|---|---|---|
| **Windows (IIS)** | ✅ | ✅ |
| **Windows (self-hosted)** | Limitato | ✅ |
| **Linux (systemd)** | Non supportato | ✅ |
| **Docker / container** | Non supportato | ✅ |
| **macOS (sviluppo)** | Non supportato | ✅ |
| **Cloud (Azure, AWS)** | Parziale | ✅ completo |

La vecchia piattaforma era vincolata a Windows Server con IIS. Didasco può essere distribuita su qualsiasi sistema operativo: Windows Server con IIS, Linux con Nginx/Apache, container Docker, o ambienti cloud come Azure App Service e AWS Elastic Beanstalk. Questo elimina la dipendenza da licenze Windows Server e riduce i costi di infrastruttura.

### 2.4 Sicurezza

- **BCrypt con work factor 11** per l'hashing delle password, in sostituzione del MD5/SHA1 spesso usato nelle applicazioni .NET Standard legacy.
- **ASP.NET Core Identity** con store custom: nessun framework ORM introduce vulnerabilità indirette; ogni query SQL è sotto il diretto controllo del team di sviluppo.
- **Anti-CSRF tokens** generati automaticamente su tutti i form POST.
- **Cookie sicuri**: `HttpOnly`, `SameSite=Lax`, scadenza a 8 ore con sliding expiration.
- **Password reset via token monouso** con scadenza temporale configurabile.
- **Audit log HTTP** su ogni richiesta (utente, IP, path, codice risposta, durata).
- **Nessun ORM**: tutte le query sono SQL diretto via `MySqlConnector`; non esiste la superficie d'attacco delle query generate automaticamente da Entity Framework o NHibernate.

### 2.5 Manutenibilità e sviluppo

- **Visual Studio 2022/2026**: supporto nativo senza estensioni aggiuntive. Il progetto si apre con F5 e parte immediatamente.
- **Hot reload**: le modifiche alle Razor View si riflettono senza riavviare l'applicazione durante lo sviluppo.
- **Test automatici**: il framework `Microsoft.AspNetCore.Mvc.Testing` permette test di integrazione end-to-end in-process, impossibili con l'architettura .NET Standard.
- **Dependency Injection nativa**: nessuna libreria IoC di terze parti (Autofac, Unity, Ninject); il container DI di ASP.NET Core è sufficiente e non introduce dipendenze esterne.
- **NuGet moderno**: tutte le dipendenze sono risolte con il nuovo formato SDK-style `.csproj`, senza `packages.config` o `web.config` complessi.

---

## 3. SQL Server + OpenText vs MySQL + gestione documentale integrata

### 3.1 Database: da SQL Server a MySQL

#### Costi di licenza

SQL Server è un prodotto commerciale Microsoft con un modello di licenza basato sul numero di core del server. Le edizioni Enterprise e Standard hanno costi che possono raggiungere **decine di migliaia di euro per server all'anno**. MySQL è open source, distribuito sotto licenza GPL: **gratuito per la maggior parte degli scenari di utilizzo**, incluso quello accademico non commerciale di Bocconi.

| Voce | SQL Server (Standard/Enterprise) | MySQL 8 |
|---|---|---|
| **Licenza server** | Da ~3.000 € a ~15.000 €+ per core/anno | Gratuito (GPL) |
| **Client Access License** | Richiesta per ogni utente/dispositivo | Non applicabile |
| **Supporto commerciale** | Incluso (Microsoft) o a pagamento | Opzionale (Oracle/community) |
| **Hosting cloud** | Azure SQL obbligatorio per costi ottimali | Qualsiasi provider (anche Kamatera) |
| **Vincolo di piattaforma** | Preferenzialmente Windows | Windows, Linux, macOS, container |

La migrazione a MySQL elimina completamente questa voce di costo ricorrente e svincola Bocconi dal vendor lock-in Microsoft sul database.

#### Funzionalità e compatibilità

MySQL 8.0, la versione utilizzata da Didasco, include tutte le funzionalità necessarie per un LMS:

- **Transazioni ACID** con InnoDB (stesso livello di garanzia di SQL Server).
- **Window functions, CTE (Common Table Expressions)**, JSON nativo: funzionalità avanzate già presenti da MySQL 8.0.
- **Full-text search** nativo per ricerche sui materiali.
- **Replica e alta disponibilità** con MySQL Group Replication o Galera Cluster, senza costi aggiuntivi di licenza.
- **Performance**: per i carichi tipici di un LMS accademico (centinaia di utenti concorrenti, tabelle da milioni di righe), MySQL 8 e SQL Server offrono prestazioni equivalenti.

#### Strumenti di amministrazione

- **MySQL Workbench** e **phpMyAdmin**: strumenti grafici gratuiti per la gestione del database.
- **Schema idempotente**: lo script `schema.sql` di Didasco usa `CREATE TABLE IF NOT EXISTS` e `INSERT IGNORE`, quindi è rieseguibile in sicurezza senza rischio di perdita dati.
- **Script di produzione**: la piattaforma include una funzione (Admin → Database) per generare automaticamente uno script SQL completo dello schema attuale, pronto per il backup o il deploy su un nuovo server.

---

### 3.2 Gestione documentale: da OpenText a sistema integrato

#### Il costo nascosto di OpenText

OpenText è un sistema ECM (Enterprise Content Management) enterprise di classe superiore. I suoi vantaggi — workflow documentali complessi, archiviazione legale, integrazione con SAP/Oracle — sono reali, ma comportano un costo strutturale significativo per un caso d'uso come un LMS accademico:

| Voce di costo | OpenText | Didasco (integrato) |
|---|---|---|
| **Licenza software** | Contratto enterprise annuale (tipicamente 5–6 cifre €) | Zero |
| **Infrastruttura dedicata** | Server/cluster separati per il documentale | Nessuna infrastruttura aggiuntiva |
| **Amministratori specializzati** | Necessari (certificazione OpenText) | Non necessari |
| **Integrazione con il LMS** | API custom da sviluppare e mantenere | Nativa — fa parte della stessa applicazione |
| **Aggiornamenti** | Ciclo separato, spesso incompatibile con l'LMS | Un unico deploy aggiorna tutto |
| **Dipendenza di rete** | Il LMS deve contattare OpenText per ogni documento | Nessuna chiamata di rete aggiuntiva |
| **Punto di guasto** | Doppio (LMS + OpenText) | Singolo (solo Didasco) |

#### Cosa fa il sistema documentale integrato di Didasco

Tutte le funzionalità necessarie per la gestione documentale di un LMS sono implementate direttamente in Didasco, senza sistemi esterni:

**Versionamento completo dei file**
- Ogni upload crea una nuova versione numerata (v1, v2, v3…) che non sovrascrive le precedenti.
- Storico delle versioni sempre consultabile dalla pagina di dettaglio del materiale.
- Ripristino di una versione precedente con un click.
- Eliminazione selettiva di singole versioni (con protezione: impossibile eliminare l'unica versione).

**Metadati strutturati e workflow**
- Ogni documento ha: titolo, autore, tipo, lingua, stato (`draft / under_review / verified`), area didattica, cartella, data, numero protocollo.
- Flusso di approvazione: `draft → under_review → verified`, con numero protocollo progressivo assegnato automaticamente ai documenti verificati.
- Il permesso `setstatus` è configurabile per ruolo: solo chi ha l'autorizzazione può cambiare lo stato.

**Archiviazione fisica**
- I file sono salvati sul filesystem del server in cartelle dedicate per materiale (`wwwroot/uploads/mat_{id}/`), con nome che include il numero di versione.
- Struttura leggibile e trasferibile: un backup con `tar` o `rsync` è sufficiente per preservare tutti i file.
- Nessun formato proprietario: i file vengono archiviati e restituiti esattamente come caricati.

**Ricerca e filtro**
- Filtri combinabili su titolo, lingua, tipo documento, anno catalogazione, anno ultima modifica, cartella.
- Ricerca full-text sul titolo tramite DataTable con paginazione e ordinamento.
- Rilevamento automatico di duplicati per titolo simile al momento del caricamento.
- Estrazione automatica di autore e numero di pagine dai metadati di `.docx`, `.pptx` e `.pdf`.

**Download e distribuzione**
- Download singolo della versione attiva.
- Download multiplo: selezionare N materiali dalla lista e ricevere un archivio ZIP con tutti i file attivi.
- Anteprima inline di PDF, immagini e video direttamente nel browser, senza scaricare il file.
- Collegamento materiali alle lezioni: i documenti della libreria vengono associati alle lezioni dei corsi senza duplicare i file fisici.

**Notifiche automatiche**
- Il sistema invia email automatiche ai ruoli configurati quando un materiale viene creato o modificato, eliminando la necessità di comunicazioni manuali tra il team documentale e gli utenti.

#### Quando OpenText rimane la scelta giusta

È doveroso precisare i casi in cui un sistema ECM enterprise come OpenText rimane preferibile:
- **Archiviazione legale obbligatoria** con firma digitale qualificata e marca temporale.
- **Integrazione con ERP** (SAP, Oracle) per workflow che attraversano più sistemi aziendali.
- **Volumi documentali molto elevati** (milioni di documenti con ricerca full-text su contenuto).

Per le esigenze di un LMS accademico — distribuzione controllata di materiali didattici con versionamento, metadati e workflow di approvazione — il sistema integrato di Didasco copre integralmente i requisiti senza le complessità e i costi di un ECM enterprise.

---

## 4. Vantaggi architetturali di Didasco

### 4.1 Pattern Repository senza ORM

La scelta di non utilizzare Entity Framework (EF Core) è deliberata e porta tre vantaggi concreti:

1. **Controllo totale delle query**: ogni SELECT, INSERT, UPDATE e DELETE è scritto esplicitamente. Non esistono query generate automaticamente che potrebbero essere inefficienti o includere colonne non necessarie (SELECT *).
2. **Performance prevedibile**: senza il livello di astrazione dell'ORM, le query sono deterministiche. Un DBA Bocconi può analizzare e ottimizzare ogni singola query nel log MySQL.
3. **Curva di apprendimento ridotta**: chiunque conosca SQL e C# può leggere e modificare i repository. Non è necessario conoscere la sintassi LINQ-to-EF o le convenzioni EF (migrations, navigation properties, lazy loading).

### 4.2 Sistema multilingua integrato

Le traduzioni dell'interfaccia sono gestite direttamente nel database MySQL (tabella `translations`) e non in file `.resx` statici. Questo permette:

- Aggiunta e modifica di traduzioni **senza ricompilare** l'applicazione.
- Cache in-memory con TTL di 30 minuti: nessun impatto sulle prestazioni.
- Interfaccia di amministrazione integrata (Admin → Dictionary) per gestire tutte le traduzioni senza accedere al codice.
- Supporto a **4 lingue**: inglese (base), italiano, spagnolo, tedesco; estendibile ad altre lingue senza modifiche al codice.

### 4.3 Feature flag e modularità

Il sistema di **feature flag** permette di abilitare o disabilitare interi moduli funzionali a runtime senza deploy. Questo è particolarmente utile in fase di rollout progressivo verso gli utenti finali.

### 4.4 Ruoli personalizzati con permessi granulari

L'unico ruolo di sistema fisso è **Admin**. Tutti gli altri ruoli sono completamente dinamici: Didasco introduce un sistema di **ruoli personalizzati** configurabili dall'interfaccia senza toccare il codice. Ogni ruolo può avere permessi specifici su:

- Accesso al modulo Corsi (docente o studente)
- Accesso al modulo Materiali (con controllo su creazione, modifica, cambio stato)
- Visibilità del pannello Utenti
- Visibilità del pannello Traduzioni

Questo elimina la necessità di intervento del team di sviluppo ogni volta che si vuole creare un nuovo profilo operativo.

---

## 5. Requisiti del cliente soddisfatti

Di seguito la mappatura tra i requisiti espressi dall'Università Bocconi e le funzionalità implementate in Didasco.

### 5.1 Gestione documentale con versionamento

**Requisito:** Gestione centralizzata dei materiali didattici con storico delle versioni e possibilità di ripristino.

**Implementazione:**
- Ogni documento caricato crea una **nuova versione** numerata (v1, v2, v3…) senza sovrascrivere le precedenti.
- La **versione attiva** è quella mostrata agli studenti; le precedenti rimangono accessibili.
- L'utente con i permessi può **ripristinare** una versione precedente come attiva con un click.
- L'**eliminazione di una versione** è bloccata se è l'unica rimasta; in tal caso occorre eliminare l'intero materiale.
- I file sono organizzati su disco in cartelle dedicate per materiale (`wwwroot/uploads/mat_{id}/`), con nome file che include il numero di versione.

### 5.2 Libreria materiali con metadati ricchi e ricerca avanzata

**Requisito:** Catalogo ricercabile dei materiali con classificazione per tipo, lingua, area didattica e stato.

**Implementazione:**
- Ogni materiale ha: titolo, autore (estratto automaticamente dai metadati del file), tipo documento, lingua, stato di workflow (`draft` / `under_review` / `verified`), area didattica, cartella logica, data catalogazione, numero protocollo automatico.
- **Filtri combinabili**: titolo, lingua, tipo documento, anno catalogazione, anno ultima modifica, cartella.
- **DataTable interattivo** con paginazione, ordinamento e ricerca inline.
- **Estrazione automatica metadati** da `.docx`, `.pptx` e `.pdf` (autore, numero pagine).
- **Rilevamento duplicati**: al caricamento, il sistema segnala se esiste un materiale con titolo simile.
- **Numero protocollo progressivo** assegnato automaticamente ai materiali in stato `verified`.

### 5.3 Workflow di revisione e stato dei materiali

**Requisito:** Processo strutturato di approvazione dei documenti prima della pubblicazione.

**Implementazione:**
- Flusso: `draft → under_review → verified`
- Lo stato `verified` richiede obbligatoriamente l'assegnazione a una cartella.
- Il permesso `setstatus` è configurabile per ruolo: chi non lo possiede vede il campo stato bloccato.
- Il cambio stato è tracciato nell'audit log.

### 5.4 Corsi strutturati con lezioni e quiz

**Requisito:** Piattaforma e-learning con percorsi didattici strutturati, contenuti multimediali e valutazione.

**Implementazione:**
- **Corsi** con titolo, descrizione, categoria, date, docente assegnato, stato (pubblicato/non pubblicato).
- **Lezioni** ordinate con contenuto HTML, collegabili a più materiali dalla libreria centralizzata.
- **Quiz** con: titolo, descrizione, limite di tempo (countdown real-time), punteggio minimo di superamento, domande a scelta multipla.
- **Cronologia tentativi**: lo studente può ripetere il quiz e visualizzare tutti i tentativi precedenti.
- **Tracking automatico** del completamento lezione al primo accesso.
- **Barra di avanzamento** per corso nella dashboard dello studente.

### 5.5 Gestione utenti e ruoli

**Requisito:** Controllo degli accessi differenziato per tipologia di utente con possibilità di creare profili personalizzati.

**Implementazione:**
- Un unico ruolo di sistema fisso: **Admin** (non eliminabile, non assegnabile dall'interfaccia).
- Tutti gli altri ruoli sono **completamente dinamici**: creati, modificati ed eliminati dall'Admin tramite interfaccia grafica, senza toccare il codice. Il database di partenza include ruoli di esempio (es. Teacher, Student), ma si tratta di ruoli ordinari — non di ruoli privilegiati o hardcoded.
- Ogni ruolo personalizzato ha permessi granulari configurabili:
  - Accesso al modulo Corsi (con sottoruolo docente o studente)
  - Accesso al modulo Materiali (con controllo su creazione, modifica, cambio stato)
  - Visibilità del pannello Utenti
  - Visibilità del pannello Traduzioni
- Creazione, modifica, attivazione/disattivazione e cancellazione degli utenti dall'Admin.
- **Blocco eliminazione ruolo** se esistono utenti assegnati: l'Admin deve prima riassegnarli.
- **Reset password via email** con token sicuro monouso e scadenza temporale.
- **Cambio password self-service** per tutti gli utenti autenticati.

### 5.6 Notifiche email configurabili

**Requisito:** Sistema di comunicazione automatica agli utenti per eventi rilevanti della piattaforma.

**Implementazione:**
- Configurazione SMTP **modificabile a runtime** senza riavviare l'applicazione (Admin → Email Settings).
- Notifiche configurabili singolarmente:
  - Utenti con permesso `can_attend`: conferma iscrizione al corso, risultato quiz.
  - Utenti con permesso `can_teach`: nuovo iscritto al corso, quiz completato.
  - Admin/altri ruoli: creazione o modifica di materiali.
- **Reminder automatici** per le lezioni pianificate, inviati da un background service.
- **Bottone "Invia email di test"** per verificare la configurazione SMTP prima dell'uso in produzione.

### 5.7 Interfaccia multilingua

**Requisito:** Piattaforma utilizzabile in più lingue per un'utenza internazionale.

**Implementazione:**
- Interfaccia disponibile in **inglese, italiano, spagnolo, tedesco**.
- Selettore lingua nella navbar con flag nazionali; visibile solo se sono abilitate più lingue.
- L'Admin abilita/disabilita le lingue dall'interfaccia (Admin → Dictionary → Impostazioni Lingue).
- Le traduzioni sono gestibili dall'interfaccia stessa senza accesso al codice.
- **Badge di completamento**: contatore delle chiavi non ancora tradotte per lingua.

### 5.8 Export dati in Excel e PDF

**Requisito:** Possibilità di esportare l'elenco dei materiali in formato Office per uso offline e reportistica.

**Implementazione:**
- **Export Excel (.xlsx)** della lista materiali con tutti i metadati (ClosedXML).
- **Export PDF** della lista materiali con layout tabellare formattato (QuestPDF).
- Entrambi gli export rispettano i filtri attivi nella lista al momento dell'esportazione.
- **Download multiplo ZIP**: selezione di più materiali dalla lista e download del pacchetto con un click.

### 5.9 Dashboard differenziate per ruolo

**Requisito:** Ogni tipologia di utente deve accedere immediatamente alle informazioni e funzioni rilevanti per il proprio ruolo.

**Implementazione:**
- **Dashboard Admin**: statistiche globali, accesso rapido a tutte le sezioni di gestione.
- **Dashboard docente** (ruolo con `can_teach`): corsi propri, iscritti, materiali caricati, attività ultimi 30 giorni.
- **Dashboard studente** (ruolo con `can_attend`): corsi iscritti con barra avanzamento, materiali disponibili, quiz in sospeso.
- Il routing post-login è automatico: ogni utente arriva alla dashboard appropriata in base ai permessi del proprio ruolo.

### 5.10 Anteprima documenti senza download

**Requisito:** Visualizzazione dei contenuti direttamente in piattaforma senza necessità di scaricare i file.

**Implementazione:**
- PDF e immagini si aprono in un **modal inline** direttamente nella pagina.
- Video riproducibili inline senza download.
- Il download è sempre disponibile come alternativa.

### 5.11 Audit e tracciabilità

**Requisito:** Tracciamento delle operazioni per conformità e sicurezza.

**Implementazione:**
- **Log HTTP strutturato** su ogni richiesta: utente autenticato, IP, metodo, path, codice risposta HTTP, durata in millisecondi.
- **Endpoint `/health`** per il monitoraggio dell'applicazione da sistemi esterni (load balancer, uptime monitor).
- Log applicativo tramite `ILogger` con livelli configurabili (Information, Warning, Error).

---

## 6. Accessibilità e standard normativi

In conformità con le linee guida **WCAG 2.1 livello AA** (recepite dalla Direttiva UE 2016/2102 sull'accessibilità dei siti web degli enti pubblici), Didasco implementa:

| Requisito WCAG | Implementazione |
|---|---|
| **Skip link** (2.4.1) | Link "Salta al contenuto principale" visibile via Tab, invisibile a video |
| **Intestazioni tabella** (1.3.1) | `scope="col"` su tutti gli `<th>` nelle 9 viste con tabelle dati |
| **Etichette controlli icona** (4.1.2) | `aria-label` su tutti i pulsanti e link con solo icona (matita, cestino, download) |
| **Attributi autocomplete** (1.3.5) | `autocomplete` corretto su tutti i campi form di autenticazione |
| **Focus visibile** | Stili Bootstrap 5 con `:focus-visible` nativi |
| **Contrasto cromatico** | Palette Bocconi (#002554 su bianco) con rapporto > 4.5:1 |

---

## 7. Conclusioni

La migrazione dalla piattaforma esistente a Didasco porta vantaggi concreti su quattro dimensioni:

**Tecnologica:** il runtime .NET 10 LTS garantisce prestazioni superiori, sicurezza aggiornata con patch mensili, deploy su qualsiasi infrastruttura (Windows, Linux, cloud) e un ciclo di vita con supporto attivo Microsoft fino a novembre 2028.

**Economica:** la sostituzione di SQL Server con MySQL elimina i costi di licenza database ricorrenti; l'eliminazione di OpenText rimuove un contratto enterprise significativo, la necessità di infrastruttura dedicata e le figure specializzate per la sua amministrazione. Il risparmio complessivo si stima in decine di migliaia di euro per anno, a parità di funzionalità operative.

**Funzionale:** tutti i requisiti espressi dall'Università Bocconi sono stati implementati e verificati — dalla gestione documentale con versionamento ai ruoli personalizzati, dal sistema multilingua alle notifiche email, dai quiz con timer all'export Excel/PDF. La gestione documentale integrata copre integralmente le esigenze di un LMS accademico senza le complessità di un ECM enterprise.

**Operativa:** la piattaforma è progettata per essere gestita autonomamente dal personale Bocconi senza dipendenza continua dal team di sviluppo: traduzioni, configurazione email, ruoli, feature flag e tipi documento sono tutti configurabili dall'interfaccia di amministrazione senza modificare il codice. Un unico sistema da mantenere, aggiornare e monitorare, al posto dei tre precedenti (applicativo + SQL Server + OpenText).

---

*Documento redatto dal team di sviluppo Didasco — Maggio 2026*  
*Per informazioni tecniche: riferirsi alla [Guida Tecnica](technical.md)*  
*Per l'utilizzo della piattaforma: riferirsi alla [Guida Utenti](user_guide.md)*
