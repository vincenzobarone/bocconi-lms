# Guida Tecnica — Bocconi LMS

> **Destinatari:** Sviluppatori, sistemisti, team IT di Bocconi.  
> Questa guida copre installazione, architettura, deploy e manutenzione dell'applicazione.

---

## Indice

1. [Prerequisiti](#1-prerequisiti)
2. [Setup locale in Visual Studio 2026](#2-setup-locale-in-visual-studio-2026)
3. [Setup locale da riga di comando](#3-setup-locale-da-riga-di-comando)
4. [Struttura del progetto](#4-struttura-del-progetto)
5. [Architettura dell'applicazione](#5-architettura-dellapplicazione)
6. [Database: schema](#6-database-schema)
7. [Sistema multilingua](#7-sistema-multilingua)
8. [Configurazione email SMTP](#8-configurazione-email-smtp)
9. [Gestione utenti e permessi](#9-gestione-utenti-e-permessi)
10. [Libreria materiali](#10-libreria-materiali)
11. [Test automatici](#11-test-automatici)
12. [Deploy in produzione](#12-deploy-in-produzione)
13. [Sicurezza](#13-sicurezza)
14. [SSO Shibboleth / SAML 2.0](#14-sso-shibboleth--saml-20)

---

## 1. Prerequisiti

| Componente | Versione minima | Note |
|---|---|---|
| .NET SDK | 10.0 (LTS) | [Download](https://dotnet.microsoft.com/download/dotnet/10.0) |
| MySQL | 8.0+ | Locale o remoto (testato su Kamatera) |
| Visual Studio | 2022 (17.x) | Workload "ASP.NET and web development" |
| Git | qualsiasi | Per il clone del repository |

> **Nota:** Il progetto **non usa Entity Framework**. Tutte le query sono SQL diretto via `MySqlConnector`. Non installare EF Core.

---

## 2. Setup locale in Visual Studio 2026

> **Flusso consigliato:** si lavora il codice su Replit (ambiente cloud), che salva automaticamente i commit su GitHub ad ogni task. Su Visual Studio 2026 basta fare **Git → Pull** per ricevere le modifiche aggiornate — tutto tramite GUI, senza usare la riga di comando.

### 2.1 Ottenere il codice sorgente

**Prima volta — Clone da VS2026 (GUI):**

1. `Git → Clone Repository...`
2. Incollare l'URL: `https://github.com/vincenzobarone/bocconi-lms.git`
3. Scegliere la cartella locale e cliccare **Clone**
4. Una volta clonato: `File → Open → Project/Solution` → selezionare `artifacts/bocconi-lms/BocconiLMS.csproj`

**Aggiornamenti successivi — Pull da VS2026 (GUI):**

1. Aprire il pannello **Git Changes** (`Visualizza → Git Changes`)
2. Cliccare la freccia **Pull** (⬇) per scaricare le ultime modifiche da Replit

Non è necessaria alcuna riga di comando: VS2026 gestisce tutto Git in modo nativo.

---

**Alternativa CLI — Clone dell'intero monorepo:**
```bash
git clone https://github.com/vincenzobarone/bocconi-lms.git
cd bocconi-lms
```

**Alternativa CLI — Sparse checkout (solo la cartella LMS):**
```bash
git clone --no-checkout https://github.com/vincenzobarone/bocconi-lms.git
cd bocconi-lms
git sparse-checkout init --cone
git sparse-checkout set artifacts/bocconi-lms
git checkout main
```
Poi aprire `artifacts/bocconi-lms/BocconiLMS.csproj` in VS2026.

### 2.2 Preparare il database

Eseguire lo script SQL sul server MySQL locale o remoto.  
**Importante:** se l'utente MySQL non ha permessi `CREATE DATABASE`, rimuovere le prime righe `CREATE DATABASE` e `USE` dallo script.

```bash
mysql -h HOST -P 3306 -u UTENTE -p NOME_DATABASE < artifacts/bocconi-lms/schema.sql
```

Lo script è idempotente: usa `CREATE TABLE IF NOT EXISTS` e `INSERT IGNORE`, quindi è sicuro rieseguirlo.

### 2.3 Configurare la connection string

**Metodo consigliato — User Secrets (non finisce in Git):**

In VS2026, tasto destro sul progetto → `Manage User Secrets`, poi incollare:
```json
{
  "ConnectionStrings": {
    "MySQL": "Server=HOST;Port=3306;Database=NOME_DB;User=UTENTE;Password=PASSWORD;"
  }
}
```

**Alternativa — variabile d'ambiente di sistema:**
```
MYSQL_CONNECTION_STRING=Server=HOST;Port=3306;Database=NOME_DB;User=UTENTE;Password=PASSWORD;
```

**Priorità di risoluzione** (in `Program.cs`):
1. Variabile d'ambiente `MYSQL_CONNECTION_STRING`
2. `ConnectionStrings:MySQL` in `appsettings.json` / User Secrets
3. Fallback hardcoded a `localhost` (solo sviluppo)

### 2.4 Avviare l'applicazione

Premere `F5` in VS2026 oppure da CLI:
```bash
cd artifacts/bocconi-lms
dotnet run
```
L'applicazione si avvia su `http://localhost:5000` (porta configurabile tramite `launchSettings.json` o variabile `PORT`).

### 2.5 Primo accesso

URL: `http://localhost:5000/Account/Login`  
Credenziali di seed: `admin@bocconi.it` / `Admin@Bocconi2024`

> **Cambiare subito la password** tramite il menu utente → *Cambia Password* prima di qualsiasi uso non di sviluppo.

---

## 3. Setup locale da riga di comando

```bash
# 1. Clona e posizionati
git clone https://github.com/vincenzobarone/bocconi-lms.git
cd bocconi-lms/artifacts/bocconi-lms

# 2. Esegui lo schema
mysql -u root -p mydb < schema.sql

# 3. Imposta la connection string
export MYSQL_CONNECTION_STRING="Server=localhost;Port=3306;Database=mydb;User=root;Password=;"

# 4. Avvia
dotnet run
```

---

## 4. Struttura del progetto

```
artifacts/bocconi-lms/
├── Controllers/
│   ├── AccountController.cs       # Login, logout, cambio password
│   ├── AdminController.cs         # Utenti, ruoli, email, Dictionary (traduzioni + tipi doc)
│   ├── CourseController.cs        # Corsi (CRUD, iscrizioni, feature flag)
│   ├── HomeController.cs          # Home, dashboard (dispatch per ruolo)
│   ├── LanguageController.cs      # Cambio lingua (imposta cookie lang)
│   ├── LessonController.cs        # Lezioni (CRUD, completamento)
│   ├── MaterialsController.cs     # Libreria materiali (CRUD, versioni, download, ZIP bulk)
│   ├── QuizController.cs          # Quiz (CRUD, sessione, submit)
│   └── StudentController.cs       # Dashboard studente
│
├── Data/
│   ├── DbHelper.cs                # Factory per MySqlConnection + GetLastInsertIdAsync
│   ├── ApplicationUser.cs         # Entità utente + ruolo per Identity
│   ├── CustomUserStore.cs         # IUserStore<ApplicationUser> — no EF, raw SQL
│   ├── CustomRoleStore.cs         # IRoleStore<ApplicationRole> — no EF, raw SQL
│   ├── BcryptPasswordHasher.cs    # IPasswordHasher con BCrypt (work factor 11)
│   ├── AreaRepository.cs          # Aree tematiche (collegate ai materiali)
│   ├── CourseRepository.cs
│   ├── DocumentTypeRepository.cs  # Tipi documento (gestiti in Dictionary)
│   ├── EnrollmentRepository.cs
│   ├── LessonRepository.cs
│   ├── MaterialRepository.cs      # Materiali + versioni file + bulk ops
│   ├── ProgressRepository.cs      # Tracking completamento lezioni
│   ├── QuizRepository.cs          # Domande, opzioni, tentativi, punteggio
│   ├── RolePermissionRepository.cs # Permessi granulari per ruolo (menu.users, ecc.)
│   ├── SettingsRepository.cs      # Impostazioni chiave/valore (SMTP, lingue abilitate)
│   ├── TranslationRepository.cs   # CRUD traduzioni multilingua
│   └── UserRepository.cs          # Statistiche, elenco utenti admin
│
├── Models/
│   ├── Material.cs, MaterialVersion.cs  # Entità libreria materiali
│   ├── Course.cs, Lesson.cs, ...        # Entità di dominio
│   └── ViewModels.cs                    # ViewModel per le form
│
├── Services/
│   ├── EmailService.cs                  # Invio email via SMTP (MailKit)
│   ├── FeatureFlagService.cs            # Feature flag (modulo Corsi on/off)
│   ├── TranslationService.cs            # Servizio traduzioni con cache IMemoryCache
│   └── LessonReminderHostedService.cs   # Background service per reminder lezioni
│
├── Views/
│   ├── Shared/_Layout.cshtml      # Layout principale con navbar e language switcher
│   ├── _ViewImports.cshtml        # @inject TranslationService T (globale)
│   ├── Account/                   # Login, cambio password, access denied
│   ├── Admin/                     # Dashboard, utenti, email, Dictionary, ruoli
│   │   └── Dictionary.cshtml      # Tabs: Traduzioni + Tipi Documento
│   ├── Course/                    # Lista, dettaglio, crea, modifica, studenti
│   ├── Lesson/                    # Dettaglio, crea, modifica
│   ├── Materials/                 # Libreria (index con multi-select, dettaglio versioni)
│   ├── Quiz/                      # Take, risultato, storico, crea
│   └── Student/                   # Dashboard studente
│
├── wwwroot/
│   ├── css/site.css               # Stili Bocconi (variabili colore, navbar)
│   └── uploads/                   # File caricati dagli utenti (escluso da Git)
│       └── mat_{id}/              # Cartella per ogni materiale
│
├── BocconiLMS.Tests/              # Test di integrazione xUnit
│   ├── Fixtures/LmsWebFactory.cs
│   ├── Helpers/{CsrfHelper,DbTestHelper}.cs
│   ├── LoginFlowTests.cs
│   ├── QuizFlowTests.cs
│   ├── CourseFlowTests.cs
│   ├── MaterialFlowTests.cs
│   ├── AdminCrudTests.cs
│   └── RoleCrudTests.cs
│
├── docs/
│   └── TECHNICAL.md               # Questa guida
├── schema.sql                     # DDL completo + seed iniziale
├── appsettings.json               # Configurazione base (SMTP placeholder)
├── Program.cs                     # Entry point, DI, configurazione startup
└── BocconiLMS.csproj              # File progetto VS2026 (.NET 10 LTS)
```

---

## 5. Architettura dell'applicazione

### Pattern MVC + Repository

L'applicazione segue il pattern MVC standard di ASP.NET Core:

- **Controller** — riceve le richieste HTTP, chiama i repository, passa dati alle View
- **Repository** — classe dedicata per ogni aggregato; contiene le query SQL grezze tramite `MySqlConnector`
- **View** — Razor `.cshtml`; utilizza ViewModel fortemente tipizzati

Ogni repository riceve `DbHelper` tramite dependency injection e apre/chiude le connessioni in modo esplicito con `using`:

```csharp
public class MaterialRepository
{
    private readonly DbHelper _db;
    public MaterialRepository(DbHelper db) => _db = db;

    public async Task<List<Material>> GetAllAsync(...)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand("SELECT ...", conn);
        // ...
    }
}
```

### Autenticazione (ASP.NET Core Identity — senza EF)

Il sistema usa `ASP.NET Core Identity` con store custom che leggono/scrivono su MySQL senza ORM:

- `CustomUserStore` — implementa `IUserStore<ApplicationUser>` + `IUserPasswordStore` + `IUserEmailStore` + `IUserRoleStore`
- `CustomRoleStore` — implementa `IRoleStore<ApplicationRole>`
- `BcryptPasswordHasher` — sostituisce il hasher standard con BCrypt (work factor 11)
- Cookie di sessione: durata 8 ore con sliding expiration

### Feature flag

`FeatureFlagService` legge il flag `Features:CoursesModule` dalla tabella `app_settings`. Quando disabilitato, il modulo Corsi è nascosto dalla navbar e studenti/teacher vengono indirizzati direttamente alla Libreria Materiali.

### Permessi menu per ruolo

La tabella `role_permissions` associa permessi granulari ai ruoli. I permessi di tipo menu (`menu.users`, `menu.translations`) controllano la visibilità delle voci di navigazione per ruoli non-Admin. `RolePermissionRepository.HasMenuPermissionAsync` verifica il permesso effettuando un JOIN con la tabella `roles` per normalized_name.

### Sessione

Usata solo per il quiz in esecuzione (risposte parziali salvate in `HttpContext.Session`). La sessione scade insieme al cookie di autenticazione (8 ore).

### Background service

`LessonReminderHostedService` gira come `IHostedService` e invia reminder email agli studenti prima delle lezioni pianificate.

---

## 6. Database: schema

### Tabelle principali

| Tabella | Descrizione |
|---|---|
| `users` | Utenti (email, password_hash BCrypt, ruolo, is_active) |
| `roles` | Ruoli (Student, Teacher, Admin) |
| `user_roles` | Associazione utente-ruolo (N:N) |
| `role_permissions` | Permessi granulari per ruolo (menu.users, menu.translations, ecc.) |
| `areas` | Aree tematiche associabili ai materiali |
| `document_types` | Tipologie documento (gestite in Dictionary → tab Tipi Documento) |
| `materials` | Materiali della libreria (titolo, lingua, tipo, stato, area, ecc.) |
| `material_versions` | Versioni file di ogni materiale (is_active, file_path, notes) |
| `lesson_materials` | Collegamento N:N tra lezioni e materiali |
| `courses` | Corsi con categoria, date, is_published |
| `lessons` | Lezioni di un corso (sort_order, is_published) |
| `enrollments` | Iscrizioni studente-corso |
| `lesson_progress` | Completamento lezione per studente |
| `quizzes` | Quiz collegati a una lezione |
| `quiz_questions` | Domande con testo e punteggio |
| `quiz_options` | Opzioni di risposta (is_correct) |
| `quiz_attempts` | Tentativi quiz con punteggio e timestamp |
| `settings` | Chiave/valore runtime (SMTP, lingue abilitate, feature flag) |
| `translations` | Traduzioni UI per 4 lingue (en/it/es/de) |
| `system_logs` | Log applicativi e accessi HTTP (canale DB, alimentato da `SystemLogRepository` in fire-and-forget; consultabile via Admin → Log di Sistema) |

### Modifiche allo schema

Lo schema non usa un migration runner automatico. Eventuali modifiche strutturali vanno applicate manualmente via MySQL client:

```bash
mysql -h HOST -u UTENTE -p DB_NAME < script.sql
```

Per generare uno script SQL completo dello schema attuale (DDL + dati di seed) usare la pagina **Admin → Database → Genera script di produzione**.

### Nota sul campo `username`

Il campo `username` nella tabella `users` è `NOT NULL UNIQUE` ma contiene sempre lo stesso valore dell'email. Il `CustomUserStore` lo popola con `user.UserName ?? user.Email`.

---

## 7. Sistema multilingua

### Panoramica

Le traduzioni sono salvate nella tabella `translations` (language_code, label_key, label_value). Il servizio `TranslationService` legge dalla cache in memoria (TTL 30 minuti) e rileva la lingua corrente dal cookie `lang`.

**Lingue supportate:** `en` (inglese, base) · `it` (italiano) · `es` (spagnolo) · `de` (tedesco)

Le lingue attive (oltre all'inglese, sempre obbligatorio) si configurano da **Admin → Dictionary → tab Traduzioni → Impostazioni Lingue**.

### Come aggiungere una nuova chiave di traduzione

Usare la sintassi `T["nuova.chiave", "Default EN"]` nella view Razor. Al primo caricamento della pagina la chiave viene inserita automaticamente nel DB con il valore inglese. Le traduzioni IT/ES/DE rimangono "Mancante" finché non vengono compilate dall'admin via ✏ **Modifica** nella tabella traduzioni (Admin → Dictionary).

### Come usare le chiavi nelle Razor View

Il servizio è iniettato globalmente in `_ViewImports.cshtml`:
```csharp
@inject TranslationService T
```

Nelle view si usa con:
```html
@T["chiave"]                              <!-- usa il fallback EN se manca -->
@T.T("chiave", "testo di default")        <!-- fallback esplicito -->
```

### Pagina Dictionary

La voce **Dictionary** nella navbar (visibile ad Admin e ai ruoli con permesso `menu.translations`) apre una pagina a due tab:

- **Traduzioni** — impostazioni lingue attive, tabella DataTable con tutte le chiavi, filtro chiavi mancanti, modifica/elimina singola chiave
- **Tipi Documento** — lista dei tipi documento con contatore materiali, form per creazione, eliminazione (bloccata se ci sono materiali agganciati)

### Cambio lingua (utente)

La navbar mostra un dropdown con le bandiere. Cliccando una lingua, si effettua un `POST /Language/Set?lang=it` che imposta il cookie `lang` per 1 anno.

---

## 8. Configurazione email SMTP

L'applicazione usa MailKit per l'invio email. Le impostazioni SMTP si configurano **runtime** senza riavviare il server, tramite la pagina *Admin → Impostazioni Email*.

I valori vengono salvati nella tabella `settings` con prefisso `Smtp:` e sovrascrivono quelli di `appsettings.json`.

**Campi configurabili:**
- Host SMTP, Porta, Username, Password
- Email e nome mittente
- Abilita/disabilita SSL
- Bottone "Invia email di test"

**Ordine di priorità** (in `EmailService.GetEffectiveSettingsAsync()`):
1. Valori nel DB (`settings` table)
2. Valori in `appsettings.json` sezione `Smtp`

---

## 9. Gestione utenti e permessi

### Interfaccia utenti

La gestione utenti è accessibile agli **Admin** sempre, e agli altri ruoli se hanno il permesso `menu.users` assegnato. URL: `/Admin/Users`.

### Percorso UI

```
Admin Dashboard (/Admin)
  └── Utenti (/Admin/Users)
        ├── Crea utente (/Admin/CreateUser)
        └── Modifica utente (/Admin/EditUser/{id})
              └── Corsi assegnati (/Admin/UserCourses/{id})
```

### Creare un nuovo utente

`GET/POST /Admin/CreateUser`

Viene compilato un `RegisterViewModel` con: `FirstName`, `LastName`, `Email`, `Password`, `Role`.  
La password viene hashata tramite BCrypt al momento della creazione. Il `CustomUserStore` crea il record in `users` e poi `UserManager.AddToRoleAsync` inserisce la riga in `user_roles`.

### Modificare un utente

`GET/POST /Admin/EditUser/{id}`

Permette di aggiornare: nome, cognome, email, ruolo, aree assegnate, stato attivo/inattivo.  
Il cambio di ruolo aggiorna la tabella `user_roles` rimuovendo prima i ruoli esistenti e aggiungendo quello nuovo.

### Attivare / disattivare un utente

`POST /Admin/ToggleUser/{id}`

Inverte il flag `is_active` nella tabella `users`. Un utente disattivato non riesce ad autenticarsi perché il `CustomUserStore` filtra `is_active = 1` nella `FindByEmailAsync`.

### Permessi per ruolo

Ogni ruolo può avere permessi granulari configurati dalla pagina **Admin → Utenti → tab Ruoli → Modifica Ruolo**:

| Permesso | Effetto |
|---|---|
| `menu.users` | Mostra la voce "Utenti" nella navbar per il ruolo |
| `menu.translations` | Mostra la voce "Dictionary" nella navbar per il ruolo |
| Permessi `corso.*` | Limitano le operazioni sui corsi al ruolo |

La verifica avviene in `AdminController.CanAccessMenuAsync()` → `RolePermissionRepository.HasMenuPermissionAsync()`.

### Cambio password

Tutti gli utenti autenticati possono cambiare la propria password da **Menu utente → Cambia Password** (`/Account/ChangePassword`). Non è richiesto l'intervento dell'admin.

---

## 10. Libreria materiali

La Libreria Materiali è il modulo principale dell'applicazione. Accessibile da tutti gli utenti autenticati; le operazioni di scrittura sono riservate a Teacher e Admin.

### Funzionalità principali

- **CRUD materiali** — titolo, lingua, tipo documento, stato (draft/under_review/verified), area tematica, data catalogazione, numero protocollo
- **Versionamento file** — ogni materiale può avere più versioni; una sola è "attiva". L'upload di una nuova versione non sovrascrive le precedenti
- **Ripristino versione** — una versione non attiva può essere riportata ad attiva (Teacher/Admin)
- **Eliminazione versione** — una versione può essere eliminata singolarmente; se era attiva, la versione precedente viene promossa automaticamente. Non è possibile eliminare l'unica versione di un materiale
- **Anteprima inline** — PDF, immagini e video si aprono in un modal senza uscire dalla pagina
- **Download singolo** — scarica la versione attiva di un materiale
- **Download multiplo (ZIP)** — selezionare più materiali con le checkbox e scaricare un archivio ZIP con tutte le versioni attive. La selezione persiste attraverso le pagine del DataTable

### Struttura file su disco

```
wwwroot/uploads/mat_{materialId}/
    v1_nomefile.pdf
    v2_nomefile.pdf
    ...
```

### Controller: `MaterialsController`

| Action | Metodo | Accesso | Descrizione |
|---|---|---|---|
| `Index` | GET | Tutti | Lista con filtri e DataTable |
| `Details` | GET | Tutti | Dettaglio + lista versioni |
| `Create` | GET/POST | Teacher, Admin | Crea nuovo materiale |
| `Edit` | GET/POST | Teacher, Admin | Modifica metadati |
| `Delete` | POST | Teacher, Admin | Elimina materiale e tutti i file |
| `UploadVersion` | POST | Teacher, Admin | Carica nuova versione |
| `DeleteVersion` | POST | Teacher, Admin | Elimina versione singola |
| `Restore` | POST | Teacher, Admin | Ripristina versione precedente come attiva |
| `Download` | GET | Tutti | Download versione attiva |
| `Preview` | GET | Tutti | Anteprima inline (PDF/immagini/video) |
| `BulkDownload` | POST | Tutti | Download ZIP di più materiali |

---

## 11. Test automatici

Il progetto di test `BocconiLMS.Tests` usa **xUnit** + `Microsoft.AspNetCore.Mvc.Testing` per test di integrazione end-to-end in-process.

> **Attenzione:** I test usano il database MySQL reale configurato in `MYSQL_CONNECTION_STRING`. Ogni test crea dati unici (suffisso UUID) e li rimuove al termine.

### Eseguire i test

```bash
cd artifacts/bocconi-lms/BocconiLMS.Tests
dotnet test
```

Con output verboso:
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Test disponibili

| File | Cosa testa |
|---|---|
| `LoginFlowTests.cs` | Login/logout per tutti i 3 ruoli, credenziali errate, redirect |
| `QuizFlowTests.cs` | Esecuzione quiz, submit risposte corrette/errate, storico, accesso non autorizzato |
| `CourseFlowTests.cs` | Index corsi, creazione corso (instructor con `can_teach`), divieto per attendee senza permessi, dettaglio corso, iscrizione, accesso lezione per iscritti |
| `MaterialFlowTests.cs` | Index materiali, creazione materiale (admin/instructor), divieto per attendee senza `can_teach`, redirect a login per anonimi, visualizzazione dettaglio materiale pubblicato |
| `AdminCrudTests.cs` | Lista utenti (Admin OK, non-Admin vietato), creazione utente, creazione/eliminazione area, gestione anagrafiche dal pannello admin |
| `RoleCrudTests.cs` | Creazione ruolo (con/senza flag `can_teach`), tab Ruoli nella pagina utenti, modifica nome ruolo, eliminazione ruolo se non assegnato |

### Struttura helper

- `Fixtures/LmsWebFactory.cs` — `WebApplicationFactory<Program>` che avvia l'app in-process
- `Helpers/DbTestHelper.cs` — crea/rimuove dati di test (utenti, corsi, lezioni, quiz, documenti)
- `Helpers/CsrfHelper.cs` — estrae il token CSRF dalle form HTML per i POST autenticati

---

## 12. Deploy in produzione

### 12.1 Prerequisiti sul server

- **.NET 10 Runtime** (o Hosting Bundle per IIS): [download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **MySQL 8** raggiungibile dalla macchina (IP whitelistato se necessario)
- Schema già applicato al DB di produzione (`schema.sql`)

### 12.2 Pubblicare da Visual Studio 2026

1. Tasto destro sul progetto → **Publish**
2. Scegliere il profilo:
   - **Folder** → copia manuale via FTP/SCP
   - **IIS** → deploy diretto (richiede Web Deploy installato)
   - **Azure App Service** → deploy diretto su Azure

Da CLI (equivalente):
```bash
dotnet publish -c Release -o ./publish
```

### 12.3 Deploy su IIS (Windows Server)

1. Installare **.NET 10 Hosting Bundle** sul server
2. Creare un nuovo sito in IIS Manager che punta alla cartella `publish/`
3. Impostare l'application pool su **"No Managed Code"** (ASP.NET Core è self-hosted)
4. Configurare la connection string MySQL — **scegliere una delle due modalità qui sotto, mai entrambe**
5. Assicurarsi che l'utente del pool IIS abbia accesso in scrittura a `wwwroot/uploads/`

#### Modalità A — Connection string nel `web.config` (segreto per-sito)

Più semplice da impostare; il segreto resta nel `web.config` del sito (file ACL ristretta a Administrators + identità del pool).

```xml
<aspNetCore processPath="dotnet" arguments=".\BocconiLMS.dll"
            stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout"
            hostingModel="inprocess">
    <environmentVariables>
        <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        <environmentVariable name="MYSQL_CONNECTION_STRING"
                             value="Server=HOST;Port=3306;Database=BocconiEdu;User=UTENTE;Password=PASSWORD;SslMode=Required;" />
    </environmentVariables>
</aspNetCore>
```

⚠️ Con questa modalità **`web.config` contiene un segreto**: non committarlo nel repo. Vedi sezione *12.3.1* qui sotto per le strategie git.

#### Modalità B — Connection string come variabile d'ambiente di sistema (consigliata)

Il `web.config` resta privo di segreti e versionabile. La connection string vive a livello macchina, condivisibile tra più siti, e sopravvive ai redeploy / `git pull`.

Impostazione una tantum sul server (PowerShell come Administrator):

```powershell
[System.Environment]::SetEnvironmentVariable(
  'MYSQL_CONNECTION_STRING',
  'Server=HOST;Port=3306;Database=BocconiEdu;User=UTENTE;Password=PASSWORD;SslMode=Required;',
  'Machine'
)
```

Poi **`iisreset`** (la prima volta serve il restart completo del servizio W3SVC perché IIS legga le env var di sistema; ai redeploy successivi basta il restart del solo App Pool).

Verifica che IIS la veda:
```powershell
[System.Environment]::GetEnvironmentVariable('MYSQL_CONNECTION_STRING', 'Machine')
```

Il `web.config` corrispondente, **senza segreti**, è:

```xml
<aspNetCore processPath="dotnet" arguments=".\BocconiLMS.dll"
            stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout"
            hostingModel="inprocess">
    <environmentVariables>
        <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
    </environmentVariables>
</aspNetCore>
```

#### 12.3.1 Gestione del `web.config` con git pull

Se fai deploy con `git pull` sul server:

- **Modalità A** (segreto nel file) → tre opzioni per evitare che il pull lo sovrascriva:
  1. `git update-index --skip-worktree web.config` (congela la copia locale, soluzione rapida)
  2. Aggiungere `web.config` al `.gitignore` e versionare invece `web.config.template` con placeholder
  3. Rimuovere dal tracking: `git rm --cached web.config && git commit ...`
- **Modalità B** (env var) → nessun problema: il `web.config` non contiene segreti, può stare tranquillamente nel repo e venire sovrascritto a ogni `git pull`.

#### 12.3.2 Debug startup IIS

Se l'app non parte, attiva temporaneamente i log di stdout nel `web.config`:

```xml
<aspNetCore ... stdoutLogEnabled="true" stdoutLogFile=".\logs\stdout" ... />
```

Crea la cartella `logs\` accanto a `BocconiLMS.dll` con permessi di scrittura per `IIS AppPool\<NomeAppPool>`. Riavvia il sito, riproduci l'errore e leggi `logs\stdout_*.log`. **Rimetti `false` quando finito**, altrimenti il file cresce indefinitamente.

### 12.4 Deploy su Linux con systemd

```bash
# Copia i file pubblicati sul server
scp -r ./publish/ user@server:/var/www/bocconi-lms/

# Crea il servizio systemd
sudo nano /etc/systemd/system/bocconi-lms.service
```

```ini
[Unit]
Description=Bocconi LMS ASP.NET Core
After=network.target

[Service]
WorkingDirectory=/var/www/bocconi-lms
ExecStart=/usr/bin/dotnet /var/www/bocconi-lms/BocconiLMS.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=bocconi-lms
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5000
Environment=MYSQL_CONNECTION_STRING=Server=HOST;Port=3306;Database=BocconiEdu;User=UTENTE;Password=PASSWORD;

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable bocconi-lms
sudo systemctl start bocconi-lms
sudo systemctl status bocconi-lms
```

Configurare **nginx** come reverse proxy davanti alla porta 5000.

### 12.5 Deploy su Azure App Service

1. In VS2026: tasto destro → Publish → Azure App Service
2. Creare o selezionare un App Service (almeno B1, .NET 10)
3. In *Configurazione → Impostazioni applicazione* aggiungere:
   - `MYSQL_CONNECTION_STRING` = connection string di produzione
4. La cartella `wwwroot/uploads/` su Azure non è persistente: considerare **Azure Blob Storage** per i file caricati dagli utenti

---

## 12.bis Logging e audit

L'applicazione adotta una strategia **dual-write** (vedi documento dedicato `docs/log-strategy.md`):

1. **Canale primario — stdout** (sempre attivo, conforme al capitolato Bocconi)
   - Tag `[APP-AUDIT]` e `[HTTP-ACCESS]` per machine-parsing
   - Catturato da Docker / systemd / Azure Monitor in produzione
2. **Canale secondario — tabella DB `system_logs`** (default attivo, disattivabile)
   - Fire-and-forget, non blocca mai la request pipeline
   - Consultabile da **Admin → Log di Sistema** (card sulla Dashboard admin)
   - Purge manuale 30 / 90 giorni o tutto

**Configurazione** in `appsettings.json`:
```json
"AuditLog": {
  "Enabled": true,
  "Level": "standard",
  "WriteToDatabase": true
}
```
Override via env var: `AuditLog__WriteToDatabase=false` (consigliato in produzione quando esiste un aggregatore log esterno).

Per il formato esatto dei tag, dei campi e l'elenco completo delle azioni, vedi [`docs/log-format.md`](log-format.md).

---

## 13. Sicurezza

### Cambio password

Tutti gli utenti autenticati possono cambiare la propria password da **Menu utente → Cambia Password**. La vecchia password viene verificata prima di accettare quella nuova.

Per un reset forzato da DB (es. account bloccato):
1. Generare un nuovo hash BCrypt in C#:
   ```csharp
   BCrypt.Net.BCrypt.HashPassword("NuovaPassword!", 11)
   ```
2. Aggiornare direttamente il DB:
   ```sql
   UPDATE users SET password_hash='$2a$11$...' WHERE email='admin@bocconi.it';
   ```

### HTTPS

In produzione, **sempre** usare HTTPS. Configurare il certificato SSL:
- **IIS**: certificate binding nel sito
- **Azure**: certificato gestito automaticamente
- **Linux/nginx**: Let's Encrypt con certbot

In `Program.cs` è già presente `app.UseHsts()` per ambienti non-Development.

### Cookie di autenticazione

Configurazione attuale (in `Program.cs`):
- Durata: 8 ore con sliding expiration
- HttpOnly: sì (non accessibile da JavaScript)
- Impostare `options.Cookie.Secure = true` in produzione se si usa HTTPS

### File caricati

I file vengono salvati in `wwwroot/uploads/mat_{id}/` con nomi prefissati dalla versione (`v1_`, `v2_`, ecc.) per evitare conflitti. Il download e l'anteprima avvengono tramite `MaterialsController` che verifica l'autenticazione dell'utente. Non è possibile accedere ai file tramite URL diretto senza essere autenticati.

---

## 14. SSO Shibboleth / SAML 2.0

### 14.1 Panoramica

Il LMS supporta il Single Sign-On via SAML 2.0 integrato direttamente nell'app come **Service Provider (SP)** tramite la libreria `Sustainsys.Saml2.AspNetCore2`. **Non è richiesto installare il daemon nativo Shibboleth SP** (`shibd`) — la libreria .NET è funzionalmente equivalente e si configura interamente tramite variabili d'ambiente.

L'Identity Provider (IdP) di riferimento è quello di Bocconi:

| Ruolo | URL |
|---|---|
| **Entity ID dell'IdP** (usato nelle asserzioni SAML) | `https://idp.unibocconi-prod.it/idp/shibboleth` |
| **Metadata XML dell'IdP** (da cui il LMS scarica la config) | `https://idp.unibocconi.it/metadata/get-config.php?what=UNIBOCCONI-ADFS` |
| **Metadata XML del LMS (SP)** | `https://<hostname>/Saml2/metadata` |

> Il metadata del LMS all'URL sopra è l'equivalente del `/Shibboleth.sso/Metadata` usato dagli altri applicativi Bocconi che usano il daemon nativo. Va fornito al team IT di Bocconi per completare la fase di setup.

---

### 14.2 Attributi SAML ricevuti dall'IdP

Il LMS usa i seguenti attributi dall'asserzione SAML (mappati dall'`attribute-map.xml` di Bocconi):

| Attributo | OID | Uso nel LMS |
|---|---|---|
| `mail` | `urn:oid:0.9.2342.19200300.100.1.3` | Email utente — chiave di lookup nell'anagrafica |
| `eduPersonPrincipalName` (`eppn`) | `urn:oid:1.3.6.1.4.1.5923.1.1.1.6` | Identificativo stabile Shibboleth — salvato in `users.shibboleth_id` |

L'utente deve già esistere nel database del LMS con la stessa email. Il login SSO **non crea automaticamente nuovi utenti**: associa l'`eppn` all'account esistente al primo accesso, e lo verifica ai successivi.

---

### 14.3 Certificato di firma SP

Il LMS firma le richieste SAML con un certificato self-signed RSA 2048 (valido 10 anni, CN=`didasco.unibocconi.it`).

**Strategia di caricamento (in ordine di priorità):**

1. **Segreto `SAML_SP_CERT_PFX`** (produzione) — bundle PKCS#12 codificato Base64 contenente cert + chiave privata
2. **Nessun segreto** (sviluppo / Replit) — il LMS genera automaticamente un certificato RSA-2048 self-signed ad ogni avvio

In sviluppo non serve configurare nulla. In produzione usare il flusso PKCS#12 descritto sotto.

**Generare il bundle PKCS#12 per la produzione:**

```bash
# 1. Genera cert e chiave PEM
openssl req -x509 -newkey rsa:2048 -keyout sp-key.pem -out sp-cert.pem -days 3650 -nodes \
  -subj "/CN=didasco.unibocconi.it/O=Universita Bocconi/C=IT"

# 2. Esporta come PKCS#12 (senza password)
openssl pkcs12 -export -out sp-bundle.pfx -inkey sp-key.pem -in sp-cert.pem -passout pass:

# 3. Converti in Base64 (una riga) → incolla nel segreto SAML_SP_CERT_PFX
cat sp-bundle.pfx | base64 -w 0
```

Dopo il rinnovo, fornire il nuovo metadata XML al team IT di Bocconi (l'URL non cambia, ma il certificato incorporato nel metadata sarà aggiornato).

> **Nota tecnica:** il formato PEM-da-base64 è stato abbandonato perché `X509Certificate2.CreateFromPem` in .NET è stretto sui caratteri invisibili introdotti dalle UI dei secret manager. Il PKCS#12 è binario e non ha questo problema.

---

### 14.4 Variabili d'ambiente

| Variabile | Ambiente | Valore |
|---|---|---|
| `SAML_IDP_METADATA_URL` | produzione | `https://idp.unibocconi.it/metadata/get-config.php?what=UNIBOCCONI-ADFS` |
| `SAML_IDP_ENTITY_ID` | produzione | `https://idp.unibocconi-prod.it/idp/shibboleth` |
| `SAML_SP_ENTITY_ID` | produzione | `https://<hostname-definitivo>` |
| `SAML_SP_BASE_URL` | produzione | `https://<hostname-definitivo>` |
| `SAML_SP_CERT_PFX` | produzione | Bundle PKCS#12 (cert + chiave) in Base64 — se assente il LMS genera un cert temporaneo |

> In sviluppo, se `SAML_IDP_METADATA_URL` non è impostata, il LMS usa `samltest.id` come IdP di test. In ambienti non-Development, puntare a `samltest.id` causa un errore di avvio intenzionale (fail-fast guard in `Program.cs`).

---

### 14.5 Procedura di setup con il team IT di Bocconi

1. **Deploy del LMS** sull'hostname definitivo con tutte le variabili d'ambiente di produzione impostate
2. **Girare al team IT** l'URL metadata SP: `https://<hostname>/Saml2/metadata`
3. **Il team IT** registra il nuovo SP nel loro IdP usando il metadata scaricato da quell'URL
4. **Verifica**: tentare un login SSO da `https://<hostname>/Account/SsoLogin` — il flusso redirige all'IdP Bocconi, autentica l'utente e ritorna al LMS con la sessione attiva

---

### 14.6 Endpoint SSO nel LMS

| URL | Descrizione |
|---|---|
| `/Account/SsoLogin` | Avvia il flusso SSO (redirect all'IdP) |
| `/Account/SsoCallback` | Riceve l'asserzione SAML dall'IdP (ACS) |
| `/Saml2/metadata` | Metadata XML del SP (da fornire al team IT) |
| `/auth/saml-metadata` | Alias di `/Saml2/metadata` |
