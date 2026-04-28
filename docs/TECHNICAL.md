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
6. [Database: schema e migrazioni](#6-database-schema-e-migrazioni)
7. [Sistema multilingua](#7-sistema-multilingua)
8. [Configurazione email SMTP](#8-configurazione-email-smtp)
9. [Gestione utenti e permessi](#9-gestione-utenti-e-permessi)
10. [Libreria materiali](#10-libreria-materiali)
11. [Test automatici](#11-test-automatici)
12. [Deploy in produzione](#12-deploy-in-produzione)
13. [Sicurezza](#13-sicurezza)

---

## 1. Prerequisiti

| Componente | Versione minima | Note |
|---|---|---|
| .NET SDK | 9.0 | [Download](https://dotnet.microsoft.com/download/dotnet/9.0) |
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
│   ├── LoginFlowTests.cs
│   ├── QuizFlowTests.cs
│   └── DocumentVersioningTests.cs
│
├── docs/
│   └── TECHNICAL.md               # Questa guida
├── schema.sql                     # DDL completo + seed iniziale
├── appsettings.json               # Configurazione base (SMTP placeholder)
├── Program.cs                     # Entry point, DI, migrazioni runtime
└── BocconiLMS.csproj              # File progetto VS2026 (.NET 9)
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

## 6. Database: schema e migrazioni

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

### Migrazioni runtime in Program.cs

Il progetto non usa un migration runner formale. Le modifiche strutturali vengono applicate all'avvio tramite blocchi `try/catch` in `Program.cs`, usando il pattern:

```csharp
// Controlla se la colonna esiste prima di aggiungerla
var chk = new MySqlCommand(@"
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME   = 'materials'
      AND COLUMN_NAME  = 'area_id'", conn);
if (Convert.ToInt32(await chk.ExecuteScalarAsync()) == 0)
{
    var alter = new MySqlCommand(
        "ALTER TABLE materials ADD COLUMN area_id INT NULL ...", conn);
    await alter.ExecuteNonQueryAsync();
}
```

> **Nota:** Non usare `ADD COLUMN IF NOT EXISTS` (non supportato da MySQL 8). Usare sempre il controllo via `INFORMATION_SCHEMA`.

### Eseguire una modifica manuale sul DB di produzione

```bash
mysql -h HOST -u UTENTE -p DB_NAME -e "ALTER TABLE materials ADD COLUMN ..."
```

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

- **CRUD materiali** — titolo, lingua, tipo documento, stato (bozza/in revisione/verificato), area tematica, data catalogazione, numero protocollo
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
| `DocumentVersioningTests.cs` | Storico versioni, upload documento, caricamento nuova versione, ripristino, accesso studente negato |

### Struttura helper

- `Fixtures/LmsWebFactory.cs` — `WebApplicationFactory<Program>` che avvia l'app in-process
- `Helpers/DbTestHelper.cs` — crea/rimuove dati di test (utenti, corsi, lezioni, quiz, documenti)
- `Helpers/CsrfHelper.cs` — estrae il token CSRF dalle form HTML per i POST autenticati

---

## 12. Deploy in produzione

### 12.1 Prerequisiti sul server

- **.NET 9 Runtime** (o Hosting Bundle per IIS): [download](https://dotnet.microsoft.com/download/dotnet/9.0)
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

1. Installare **.NET 9 Hosting Bundle** sul server
2. Creare un nuovo sito in IIS Manager che punta alla cartella `publish/`
3. Impostare l'application pool su **"No Managed Code"** (ASP.NET Core è self-hosted)
4. Configurare le variabili d'ambiente nel sito IIS:
   - *Configuration Editor → system.webServer/aspNetCore → environmentVariables*
   - Aggiungere `MYSQL_CONNECTION_STRING` e `ASPNETCORE_ENVIRONMENT=Production`
5. Assicurarsi che l'utente del pool IIS abbia accesso in scrittura a `wwwroot/uploads/`

Esempio `web.config` (generato automaticamente da `dotnet publish`):
```xml
<aspNetCore processPath="dotnet" arguments=".\BocconiLMS.dll"
            stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout"
            hostingModel="inprocess">
    <environmentVariables>
        <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        <environmentVariable name="MYSQL_CONNECTION_STRING" value="Server=...;" />
    </environmentVariables>
</aspNetCore>
```

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
2. Creare o selezionare un App Service (almeno B1, .NET 9)
3. In *Configurazione → Impostazioni applicazione* aggiungere:
   - `MYSQL_CONNECTION_STRING` = connection string di produzione
4. La cartella `wwwroot/uploads/` su Azure non è persistente: considerare **Azure Blob Storage** per i file caricati dagli utenti

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
