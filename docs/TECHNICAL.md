# Guida Tecnica — Bocconi LMS

> **Destinatari:** Sviluppatori, sistemisti, team IT di Bocconi.  
> Questa guida copre installazione, architettura, deploy e manutenzione dell'applicazione.

---

## Indice

1. [Prerequisiti](#1-prerequisiti)
2. [Setup locale in Visual Studio 2022](#2-setup-locale-in-visual-studio-2022)
3. [Setup locale da riga di comando](#3-setup-locale-da-riga-di-comando)
4. [Struttura del progetto](#4-struttura-del-progetto)
5. [Architettura dell'applicazione](#5-architettura-dellapplicazione)
6. [Database: schema e migrazioni](#6-database-schema-e-migrazioni)
7. [Sistema multilingua](#7-sistema-multilingua)
8. [Configurazione email SMTP](#8-configurazione-email-smtp)
9. [Gestione utenti admin](#9-gestione-utenti-admin)
10. [Test automatici](#10-test-automatici)
11. [Deploy in produzione](#11-deploy-in-produzione)
12. [Sicurezza](#12-sicurezza)
13. [Estendere il sistema](#13-estendere-il-sistema)
14. [Note e decisioni tecniche](#14-note-e-decisioni-tecniche)

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

## 2. Setup locale in Visual Studio 2022

### 2.1 Ottenere il codice sorgente

**Opzione A — Clone dell'intero monorepo:**
```bash
git clone https://github.com/vincenzobarone/bocconi-lms.git
cd bocconi-lms
```
In VS2022: `File → Open → Project/Solution` → selezionare `artifacts/bocconi-lms/BocconiLMS.csproj`

**Opzione B — Sparse checkout (solo la cartella LMS):**
```bash
git clone --no-checkout https://github.com/vincenzobarone/bocconi-lms.git
cd bocconi-lms
git sparse-checkout init --cone
git sparse-checkout set artifacts/bocconi-lms
git checkout main
```
Poi aprire `artifacts/bocconi-lms/BocconiLMS.csproj` in VS2022.

### 2.2 Preparare il database

Eseguire lo script SQL sul server MySQL locale o remoto.  
**Importante:** se l'utente MySQL non ha permessi `CREATE DATABASE`, rimuovere le prime righe `CREATE DATABASE` e `USE` dallo script.

```bash
mysql -h HOST -P 3306 -u UTENTE -p NOME_DATABASE < artifacts/bocconi-lms/schema.sql
```

Lo script è idempotente: usa `CREATE TABLE IF NOT EXISTS` e `INSERT IGNORE`, quindi è sicuro rieseguirlo.

### 2.3 Configurare la connection string

**Metodo consigliato — User Secrets (non finisce in Git):**

In VS2022, tasto destro sul progetto → `Manage User Secrets`, poi incollare:
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

Premere `F5` in VS2022 oppure da CLI:
```bash
cd artifacts/bocconi-lms
dotnet run
```
L'applicazione si avvia su `http://localhost:5000` (porta configurabile tramite `launchSettings.json` o variabile `PORT`).

### 2.5 Primo accesso

URL: `http://localhost:5000/Account/Login`  
Credenziali di seed: `admin@bocconi.it` / `Admin@Bocconi2024`

> **Cambiare subito la password** prima di qualsiasi uso non di sviluppo.

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
│   ├── AccountController.cs       # Login, logout
│   ├── AdminController.cs         # Gestione utenti, email, traduzioni
│   ├── CourseController.cs        # Corsi (CRUD, iscrizioni)
│   ├── DocumentController.cs      # Upload, download, versioning
│   ├── HomeController.cs          # Home, dashboard (dispatch per ruolo)
│   ├── LanguageController.cs      # Cambio lingua (imposta cookie lang)
│   ├── LessonController.cs        # Lezioni (CRUD, completamento)
│   ├── QuizController.cs          # Quiz (CRUD, sessione, submit)
│   └── StudentController.cs       # Dashboard studente
│
├── Data/
│   ├── DbHelper.cs                # Factory per MySqlConnection
│   ├── ApplicationUser.cs         # Entità utente + ruolo per Identity
│   ├── CustomUserStore.cs         # IUserStore<ApplicationUser> — no EF, raw SQL
│   ├── CustomRoleStore.cs         # IRoleStore<ApplicationRole> — no EF, raw SQL
│   ├── BcryptPasswordHasher.cs    # IPasswordHasher con BCrypt (work factor 11)
│   ├── CourseRepository.cs
│   ├── LessonRepository.cs
│   ├── DocumentRepository.cs      # Include gestione versioni file
│   ├── QuizRepository.cs          # Domande, opzioni, tentativi, punteggio
│   ├── EnrollmentRepository.cs
│   ├── ProgressRepository.cs      # Tracking completamento lezioni
│   ├── UserRepository.cs          # Statistiche, elenco utenti admin
│   ├── SettingsRepository.cs      # Impostazioni chiave/valore (es. SMTP)
│   └── TranslationRepository.cs   # CRUD traduzioni multilingua
│
├── Models/
│   ├── Course.cs, Lesson.cs, ...  # Entità di dominio
│   └── ViewModels.cs              # ViewModel per le form
│
├── Services/
│   ├── EmailService.cs            # Invio email via SMTP (MailKit)
│   ├── TranslationService.cs      # Servizio traduzioni con cache
│   └── LessonReminderHostedService.cs  # Background service per reminder
│
├── Views/
│   ├── Shared/_Layout.cshtml      # Layout principale con navbar e language switcher
│   ├── _ViewImports.cshtml        # @inject TranslationService T (globale)
│   ├── Account/                   # Login, access denied
│   ├── Admin/                     # Dashboard admin, utenti, email, traduzioni
│   ├── Course/                    # Lista, dettaglio, crea, modifica, studenti
│   ├── Lesson/                    # Dettaglio, crea, modifica
│   ├── Quiz/                      # Take, risultato, storico, crea
│   ├── Student/                   # Dashboard studente
│   └── Document/                  # (gestito da modal inline nelle lezioni)
│
├── wwwroot/
│   ├── css/site.css               # Stili Bocconi (variabili colore, navbar)
│   └── uploads/                   # File caricati dagli utenti (escluso da Git)
│
├── BocconiLMS.Tests/              # Test di integrazione xUnit
│   ├── LoginFlowTests.cs
│   ├── QuizFlowTests.cs
│   └── DocumentVersioningTests.cs
│
├── schema.sql                     # DDL completo + seed iniziale
├── appsettings.json               # Configurazione base (SMTP placeholder)
├── Program.cs                     # Entry point, DI registration
└── BocconiLMS.csproj              # File progetto VS2022 (.NET 9)
```

---

## 5. Architettura dell'applicazione

### Pattern MVC + Repository

L'applicazione segue il pattern MVC standard di ASP.NET Core:

- **Controller** — riceve le richieste HTTP, chiama i repository, passa dati alle View
- **Repository** — classe dedicata per ogni aggregato (Course, Lesson, ecc.); contiene le query SQL grezze tramite `MySqlConnector`
- **View** — Razor `.cshtml`; utilizza ViewModel fortemente tipizzati

Ogni repository riceve `DbHelper` tramite dependency injection e apre/chiude le connessioni in modo esplicito con `using`:

```csharp
public class CourseRepository
{
    private readonly DbHelper _db;
    public CourseRepository(DbHelper db) => _db = db;

    public async Task<List<Course>> GetAllAsync()
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
| `courses` | Corsi con categoria, date, is_published |
| `lessons` | Lezioni di un corso (sort_order, is_published) |
| `documents` | Documenti allegati a una lezione |
| `document_versions` | Versioni storiche di ogni documento |
| `enrollments` | Iscrizioni studente-corso |
| `lesson_progress` | Completamento lezione per studente |
| `quizzes` | Quiz collegati a una lezione |
| `quiz_questions` | Domande con testo e punteggio |
| `quiz_options` | Opzioni di risposta (is_correct) |
| `quiz_attempts` | Tentativi quiz con punteggio e timestamp |
| `settings` | Tabella chiave/valore per configurazioni runtime (SMTP) |
| `translations` | Traduzioni UI per 4 lingue (en/it/es/de) |

### Eseguire una migrazione manuale

Non esiste un sistema di migrazione automatico (nessun EF). Per aggiungere colonne o tabelle:

1. Modificare `schema.sql` aggiungendo la DDL con `IF NOT EXISTS` o `MODIFY COLUMN`
2. Eseguire solo la parte nuova sul DB di produzione:
   ```bash
   mysql -h HOST -u UTENTE -p DB_NAME -e "ALTER TABLE ..."
   ```

### Nota sul campo `username`

Il campo `username` nella tabella `users` è `NOT NULL UNIQUE` ma in pratica contiene sempre lo stesso valore dell'email. Il `CustomUserStore` lo popola con `user.UserName ?? user.Email`. In una futura refactoring si potrebbe:
- Rimuovere la colonna `username`
- Modificare la query INSERT in `CustomUserStore.CreateAsync`
- Modificare le SELECT per usare solo `email` come identificatore

---

## 7. Sistema multilingua

### Panoramica

Le traduzioni sono salvate nella tabella `translations` (language_code, label_key, label_value). Il servizio `TranslationService` legge dalla cache in memoria (TTL 30 minuti) e rileva la lingua corrente dal cookie `lang`.

**Lingue supportate:** `en` (inglese, base) · `it` (italiano) · `es` (spagnolo) · `de` (tedesco)

### Come aggiungere una nuova chiave di traduzione

**Via admin UI (scelta consigliata):**
1. Accedere come admin → *Admin → Traduzioni*
2. Clic su **Aggiungi chiave**
3. Inserire la chiave (es. `course.subtitle`) e il valore inglese
4. Clic su **Aggiungi**, poi su **Modifica** per aggiungere IT/ES/DE
5. La cache viene invalidata automaticamente al salvataggio

**Via SQL diretto:**
```sql
INSERT IGNORE INTO translations (language_code, label_key, label_value) VALUES
  ('en', 'course.subtitle', 'Course subtitle'),
  ('it', 'course.subtitle', 'Sottotitolo del corso'),
  ('es', 'course.subtitle', 'Subtítulo del curso'),
  ('de', 'course.subtitle', 'Kurs-Untertitel');
```

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

### Cambio lingua (utente)

Il navbar mostra un dropdown con le bandiere. Cliccando una lingua, si effettua un `POST /Language/Set?lang=it` che imposta il cookie `lang` per 1 anno.

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

## 9. Gestione utenti admin

L'interfaccia di gestione utenti è accessibile solo agli utenti con ruolo **Admin** all'URL `/Admin/Users`.

### Percorso UI

```
Admin Dashboard (/Admin)
  └── Gestione Utenti (/Admin/Users)
        ├── Crea utente (/Admin/CreateUser)
        └── Modifica utente (/Admin/EditUser/{id})
```

### Creare un nuovo utente

`GET/POST /Admin/CreateUser`

Viene compilato un `RegisterViewModel` con: `FirstName`, `LastName`, `Email`, `Password`, `Role`.  
La password viene hashata tramite BCrypt al momento della creazione. Il `CustomUserStore` crea il record in `users` e poi `UserManager.AddToRoleAsync` inserisce la riga in `user_roles`.

```csharp
var appUser = new ApplicationUser { UserName = model.Email, Email = model.Email, ... };
await _userManager.CreateAsync(appUser, model.Password);
await _userManager.AddToRoleAsync(appUser, role);
```

### Modificare un utente

`GET/POST /Admin/EditUser/{id}`

Permette di aggiornare: nome, cognome, email, ruolo, stato attivo/inattivo.  
Il cambio di ruolo aggiorna la tabella `user_roles` rimuovendo prima i ruoli esistenti e aggiungendo quello nuovo.

### Attivare / disattivare un utente

`POST /Admin/ToggleUser/{id}`

Inverte il flag `is_active` nella tabella `users`. Un utente disattivato non riesce ad autenticarsi perché il `CustomUserStore` restituisce `null` dalla `FindByEmailAsync` (la query filtra `is_active = 1`).

### Operazioni solo da DB

Le seguenti operazioni **non hanno UI** e richiedono accesso diretto al database:
- Eliminazione fisica di un utente (preserva storico quiz/progressi)
- Reset password forzato (aggiornare `password_hash` con un hash BCrypt generato a mano)
- Cambio email duplicata

---

## 10. Test automatici

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

## 11. Deploy in produzione

### 11.1 Prerequisiti sul server

- **.NET 9 Runtime** (o Hosting Bundle per IIS): [download](https://dotnet.microsoft.com/download/dotnet/9.0)
- **MySQL 8** raggiungibile dalla macchina (IP whitelistato se necessario)
- Schema già applicato al DB di produzione (`schema.sql`)

### 11.2 Pubblicare da Visual Studio 2022

1. Tasto destro sul progetto → **Publish**
2. Scegliere il profilo:
   - **Folder** → copia manuale via FTP/SCP
   - **IIS** → deploy diretto (richiede Web Deploy installato)
   - **Azure App Service** → deploy diretto su Azure

Da CLI (equivalente):
```bash
dotnet publish -c Release -o ./publish
```

### 11.3 Deploy su IIS (Windows Server)

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

### 11.4 Deploy su Linux con systemd

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

### 11.5 Deploy su Azure App Service

1. In VS2022: tasto destro → Publish → Azure App Service
2. Creare o selezionare un App Service (almeno B1, .NET 9)
3. In *Configurazione → Impostazioni applicazione* aggiungere:
   - `MYSQL_CONNECTION_STRING` = connection string di produzione
4. La cartella `wwwroot/uploads/` su Azure non è persistente: considerare **Azure Blob Storage** per i file caricati

---

## 12. Sicurezza

### Cambio password admin post-deploy

Accedere come admin, poi (in attesa di una pagina di cambio password):
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

I file vengono salvati in `wwwroot/uploads/` con nomi UUID per evitare conflitti e directory traversal. Il download avviene tramite controller (`DocumentController`) che verifica l'autenticazione dell'utente.

---

## 13. Estendere il sistema

### Aggiungere un nuovo repository

1. Creare `Data/NuovoRepository.cs` sul modello degli esistenti
2. Registrarlo in `Program.cs`:
   ```csharp
   builder.Services.AddScoped<NuovoRepository>();
   ```
3. Iniettarlo nel controller tramite costruttore

### Aggiungere una nuova vista con traduzioni

1. Creare la Razor view
2. Aggiungere le chiavi nella tabella `translations` (via admin UI o SQL)
3. Usare `@T["chiave"]` nella view (il servizio è già iniettato da `_ViewImports.cshtml`)

### Aggiungere un nuovo ruolo

1. Inserire il ruolo nel DB:
   ```sql
   INSERT IGNORE INTO roles (name, normalized_name) VALUES ('Tutor', 'TUTOR');
   ```
2. Aggiungere il ruolo all'enum (se si usa nella logica) o gestirlo come stringa
3. Aggiungere `[Authorize(Roles = "Tutor")]` sui controller/action pertinenti

---

## 14. Note e decisioni tecniche

| Decisione | Motivazione |
|---|---|
| Nessun ORM (raw `MySqlConnector`) | Requisito esplicito del progetto; massima trasparenza SQL |
| ASP.NET Core Identity con store custom | Riuso dell'infrastruttura Identity (cookie, claim, roles) senza EF |
| BCrypt work factor 11 | Buon compromesso sicurezza/performance; aggiornabile modificando `BcryptPasswordHasher` |
| Cache traduzioni in memoria (IMemoryCache) | Evita una query DB a ogni richiesta; invalidata su ogni modifica admin |
| Sessione per quiz in esecuzione | Stato temporaneo non persistente; l'attempt viene salvato solo al submit |
| `username` ridondante (sempre = email) | Lasciato per compatibilità con Identity standard; refactorizable in futuro |
| Lingue da cookie (non da DB utente) | Semplicità; estendibile salvando la preferenza sul profilo utente |
