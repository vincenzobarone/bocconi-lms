# Diagramma delle Classi — Didasco LMS (Università Bocconi)

Versione: 1.0 — aggiornata al 2026-04-30

---

## Panoramica dell'architettura

L'applicazione segue il pattern **MVC (Model-View-Controller)** di ASP.NET Core 9.  
Nessun ORM: l'accesso ai dati avviene tramite classi **Repository** con query SQL raw via `MySqlConnector`.

```
┌─────────────────────────────────────────────────────────────────┐
│                        PRESENTATION LAYER                       │
│  Controllers (MVC)  ←→  Razor Views (.cshtml)  ←→  ViewModels  │
└────────────────────────────┬────────────────────────────────────┘
                             │ iniettano
┌────────────────────────────▼────────────────────────────────────┐
│                        SERVICE LAYER                            │
│  TranslationService · EmailService · AuditLogger               │
│  AppVersionService  · FeatureFlagService                       │
└────────────────────────────┬────────────────────────────────────┘
                             │ usano
┌────────────────────────────▼────────────────────────────────────┐
│                       DATA ACCESS LAYER                         │
│  *Repository (raw SQL via MySqlConnector)  ·  DbHelper         │
│  CustomUserStore  ·  CustomRoleStore                           │
└────────────────────────────┬────────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────────┐
│                           DATABASE                              │
│                      MySQL (utf8mb4)                            │
└─────────────────────────────────────────────────────────────────┘
```

---

## Controllers

### `AccountController`
- **Azioni:** `Login (GET/POST)`, `Logout (POST)`, `AccessDenied`, `ForgotPassword (GET/POST)`, `ResetPassword (GET/POST)`
- **Dipendenze:** `SignInManager<ApplicationUser>`, `IAuditLogger`, `EmailService`
- **Autenticazione:** Login via ASP.NET Identity + cookie; password hashing BCrypt

### `AdminController`
- **Azioni:** `Dashboard`, `Users`, `CreateUser (GET/POST)`, `EditUser (GET/POST)`, `DeleteUser`, `Roles`, `CreateRole (GET/POST)`, `EditRole (GET/POST)`, `DeleteRole`, `Dictionary`, `CreateArea`, `DeleteArea`, `CreateDocumentType`, `DeleteDocumentType`, `Settings`, `Translations`, `EditTranslation`, `Platforms`, `CreatePlatform`, `DeletePlatform`, `Migrations`
- **Dipendenze:** `UserRepository`, `RolePermissionRepository`, `AreaRepository`, `DocumentTypeRepository`, `TranslationRepository`, `SettingsRepository`, `PlatformRepository`, `MigrationRunner`, `IAuditLogger`
- **Accesso:** Solo ruolo `Admin`

### `CourseController`
- **Azioni:** `Index`, `Details/{id}`, `Create (GET/POST)`, `Edit/{id} (GET/POST)`, `Delete (POST)`, `Enroll/{id} (POST)`, `Unenroll/{id} (POST)`, `Publish/{id} (POST)`, `Unpublish/{id} (POST)`
- **Dipendenze:** `CourseRepository`, `EnrollmentRepository`, `LessonRepository`, `IAuditLogger`
- **Accesso:** `[Authorize]`; creazione/modifica richiede `courses.teach`

### `LessonController`
- **Azioni:** `Details/{id}`, `Create (GET/POST)`, `Edit/{id} (GET/POST)`, `Delete (POST)`, `MarkComplete/{id} (POST)`
- **Dipendenze:** `LessonRepository`, `CourseRepository`, `EnrollmentRepository`, `ProgressRepository`, `MaterialRepository`, `IAuditLogger`
- **Accesso:** `[Authorize]`; creazione/modifica richiede `courses.teach`

### `QuizController`
- **Azioni:** `Take/{id}`, `Submit/{id} (POST)`, `Result/{attemptId}`, `History`
- **Dipendenze:** `QuizRepository`, `EnrollmentRepository`, `IAuditLogger`
- **Accesso:** `[Authorize]`

### `MaterialsController`
- **Azioni:** `Index`, `Details/{id}`, `Create (GET/POST)`, `Edit/{id} (GET/POST)`, `Delete/{id} (POST)`, `UploadVersion (POST)`, `Download/{id}`, `ExportExcel`, `ExportPdf`
- **Dipendenze:** `MaterialRepository`, `DocumentTypeRepository`, `AreaRepository`, `PlatformRepository`, `MaterialRepository`, `IAuditLogger`
- **Accesso:** lettura = `[Authorize]`; creazione/modifica = `materials.*`

### `HomeController`
- **Azioni:** `Index`, `Dashboard`, `Error`, `ChangeLanguage`
- **Dipendenze:** `CourseRepository`, `EnrollmentRepository`, `TranslationService`

### `LanguageController`
- **Azioni:** `Set (POST)` — imposta la lingua nella sessione
- **Dipendenze:** `ISession`

### `StudentController`
- **Azioni:** `MyCourses`, `Progress`
- **Dipendenze:** `EnrollmentRepository`, `ProgressRepository`

---

## Models (Dominio)

```
ApplicationUser          : IdentityUser<int>
  + Id : int
  + Email : string
  + FirstName : string
  + LastName : string
  + Role : string
  + IsActive : bool
  + CreatedAt : DateTime

ApplicationRole          : IdentityRole<int>
  + Id : int
  + Name : string
  + NormalizedName : string
  + CreatedAt : DateTime

Course
  + Id, Title, Description, Category : string
  + TeacherId : int
  + StartDate, EndDate : DateTime?
  + IsPublished : bool

Lesson
  + Id, CourseId, Title, Content : string
  + SortOrder : int
  + IsPublished : bool

Quiz
  + Id, LessonId, Title, Description : string
  + TimeLimitMinutes, PassingScore : int

QuizQuestion
  + Id, QuizId, QuestionText : string
  + SortOrder : int

QuizOption
  + Id, QuestionId, OptionText : string
  + IsCorrect : bool

Enrollment
  + Id, UserId, CourseId : int
  + EnrolledAt : DateTime

Material
  + Id, Title, AuthorName, Language, Status : string
  + OwnerId, DocumentTypeId, FolderId, AreaId, PlatformId : int?
  + IsPublished, IsPublishable : bool
  + ProtocolNumber, PageCount : int?

Area
  + Id, Name : string
  + SortOrder : int

QuizAttempt
  + Id, QuizId, UserId, Score : int
  + TotalQuestions, CorrectAnswers : int
  + Passed : bool
  + AttemptedAt : DateTime
```

---

## ViewModels (`Models/ViewModels.cs`)

| ViewModel                     | Scopo                                               |
|-------------------------------|-----------------------------------------------------|
| `LoginViewModel`              | Form di login (Email, Password, RememberMe)         |
| `ForgotPasswordViewModel`     | Form recupero password                              |
| `ResetPasswordViewModel`      | Form reset password con token                       |
| `CourseCreateViewModel`       | Creazione/modifica corso                            |
| `CourseDetailsViewModel`      | Vista dettaglio corso con iscrizione                |
| `LessonCreateViewModel`       | Creazione/modifica lezione                          |
| `QuizCreateViewModel`         | Creazione quiz con domande/opzioni                  |
| `QuizTakeViewModel`           | Presentazione quiz allo studente                    |
| `QuizResultViewModel`         | Risultato tentativo quiz                            |
| `MaterialCreateViewModel`     | Creazione/modifica materiale                        |
| `MaterialIndexViewModel`      | Elenco materiali con filtri                         |
| `AdminUserListViewModel`      | Elenco utenti per pannello Admin                    |
| `CreateUserViewModel`         | Form creazione utente da Admin                      |
| `RoleEditViewModel`           | Modifica ruolo con permessi                         |
| `DashboardViewModel`          | Dati aggregati per la dashboard                     |

---

## Data Access Layer

### `DbHelper`
- Wrapper singleton su `MySqlConnectionStringBuilder`
- Metodi: `GetConnection()`, `GetConnectionWithUserVariables()`
- `GetConnectionWithUserVariables()` imposta `@lms_user` per tracciamento SQL

### Repository Pattern

Ogni repository riceve `DbHelper` via DI e apre connessioni on-demand.

| Repository                  | Tabelle principali               | Note                                         |
|-----------------------------|----------------------------------|----------------------------------------------|
| `UserRepository`            | `users`                          | CRUD utenti, cambio password, cambio ruolo   |
| `CourseRepository`          | `courses`                        | CRUD corsi, pubblicazione                    |
| `LessonRepository`          | `lessons`                        | CRUD lezioni, ordinamento                    |
| `QuizRepository`            | `quizzes`, `quiz_questions`, `quiz_options`, `quiz_attempts` | Gestione quiz completo |
| `EnrollmentRepository`      | `enrollments`                    | Iscrizione/disiscrizione                     |
| `ProgressRepository`        | `lesson_progress`                | Completamento lezioni                        |
| `MaterialRepository`        | `materials`, `material_versions`, `lesson_materials` | Libreria documenti      |
| `DocumentTypeRepository`    | `document_types`                 |                                              |
| `AreaRepository`            | `areas`, `user_areas`            |                                              |
| `PlatformRepository`        | `platforms`                      |                                              |
| `RolePermissionRepository`  | `role_permissions`               | Permessi funzionali per ruolo                |
| `TranslationRepository`     | `translations`                   | Stringhe UI multilingua                      |
| `SettingsRepository`        | (tabella `app_settings`)         | Feature flags e configurazioni runtime       |

### `CustomUserStore`
- Implementa `IUserStore<ApplicationUser>`, `IUserPasswordStore`, `IUserRoleStore`
- Usa `UserRepository` e `RolePermissionRepository` per verificare `courses.teach`/`courses.attend`

### `CustomRoleStore`
- Implementa `IRoleStore<ApplicationRole>`
- Gestisce `roles` + `role_permissions`

### `MigrationRunner`
- Esegue al boot le migrazioni SQL ordinate in `Migrations/`
- Tiene traccia delle migrazioni già applicate su tabella `schema_migrations`
- Usa `GetConnectionWithUserVariables()` per l'esecuzione
- Fail-fast: qualsiasi errore interrompe il boot dell'applicazione

---

## Services

### `IAuditLogger` / `AuditLogger`
- Singleton
- `Log(action, target, outcome)` — livello standard (skippato se Level=minimal)
- `LogMinimal(action, target, outcome)` — sempre scritto (autenticazione)
- Legge `AuditLog:Enabled` e `AuditLog:Level` da `appsettings.json`

### `TranslationService`
- Scoped — carica le traduzioni dal DB e le mette in cache (`IMemoryCache`)
- `T(key, fallback)` — restituisce la traduzione per la lingua corrente (in sessione)

### `EmailService`
- Scoped — invia e-mail via SMTP usando MailKit
- Configurazione letta da `appsettings.json` sezione `Smtp`

### `AppVersionService`
- Singleton — legge la versione dall'assembly e la espone a tutte le view

### `FeatureFlagService`
- Scoped — legge i feature flag da `SettingsRepository` con cache

### `LessonReminderHostedService`
- `IHostedService` — invia e-mail di promemoria alle scadenze dei corsi

---

## Middleware

### `HttpAccessLogMiddleware`
- Registrato dopo `UseAuthentication`, prima di `UseAuthorization`
- Logga ogni richiesta HTTP con tag `[HTTP-ACCESS]`
- Skip automatico per `/health` e `/favicon.ico`

---

## Glossario

| Termine              | Significato                                                                  |
|----------------------|------------------------------------------------------------------------------|
| MVC                  | Model-View-Controller — pattern architetturale di ASP.NET Core               |
| Repository           | Classe che incapsula l'accesso ai dati per una o più tabelle correlate       |
| ViewModel            | Oggetto trasferito dal Controller alla View; non corrisponde 1:1 al dominio  |
| Singleton            | Istanza unica per tutta la durata dell'applicazione                          |
| Scoped               | Istanza unica per ogni richiesta HTTP                                        |
| DI                   | Dependency Injection — meccanismo ASP.NET Core per fornire dipendenze        |
| IHostedService       | Servizio di background eseguito dal runtime ASP.NET Core                     |
| IdentityUser         | Classe base ASP.NET Identity per gli utenti autenticati                      |
| CustomUserStore      | Implementazione personalizzata di Identity Store che usa MySQL direttamente  |
| Feature Flag         | Valore runtime che abilita/disabilita funzionalità senza rideploy            |
| `courses.teach`      | Permesso che abilita la creazione e gestione di corsi                        |
| `courses.attend`     | Permesso che abilita l'iscrizione ai corsi                                   |
