# Test Book — Didasco LMS (Università Bocconi)

Versione: 2.0 — aggiornata al 2026-04-30  
Framework: xUnit + `Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory)  
Sorgente: `BocconiLMS.Tests/` — ogni riga corrisponde a un metodo di test reale.

---

## Convenzioni

| Campo                  | Descrizione                                                                                           |
|------------------------|-------------------------------------------------------------------------------------------------------|
| **ID**                 | `<FILE>-<NNN>` (es. `AUTH-001`)                                                                       |
| **Tipo**               | `[Fact]` = singolo caso; `[Theory]` = eseguito per ogni `[InlineData]`                               |
| **Metodo di test**     | Nome esatto del metodo in C# — consente la verifica diretta nel test runner                          |
| **Varianti**           | Valori `[InlineData]` (solo per `[Theory]`); vuoto per `[Fact]`                                      |
| **Risultato atteso**   | Comportamento verificato dall'asserzione xUnit                                                       |
| **Risultato automatico** | Esito xUnit (`xUnit PASS` = il test supera `dotnet test` nel CI)                                 |

---

## AUTH — `LoginFlowTests.cs` (6 metodi · 12 esecuzioni)

| ID       | Tipo     | Metodo di test                                     | Varianti (InlineData)              | Risultato atteso                                         | Risultato automatico |
|----------|----------|----------------------------------------------------|------------------------------------|---------------------------------------------------------|----------------------|
| AUTH-001 | [Theory] | `Login_WithValidCredentials_RedirectsToDashboard`  | "attendee", "instructor", "admin"  | HTTP 302 → `/Home/Dashboard`                            | xUnit PASS           |
| AUTH-002 | [Fact]   | `Login_WithWrongPassword_ShowsError`               | —                                  | HTTP 200; risposta contiene "Credenziali non valide"    | xUnit PASS           |
| AUTH-003 | [Fact]   | `Login_WithNonExistentEmail_ShowsError`            | —                                  | HTTP 200; risposta contiene "Credenziali non valide"    | xUnit PASS           |
| AUTH-004 | [Theory] | `Logout_WhenLoggedIn_RedirectsToHome`              | "attendee", "instructor", "admin"  | HTTP 302 → `/`                                          | xUnit PASS           |
| AUTH-005 | [Theory] | `Login_AlreadyAuthenticated_RedirectsToDashboard`  | "attendee", "instructor", "admin"  | HTTP 302 (non mostra la pagina di login)                | xUnit PASS           |
| AUTH-006 | [Fact]   | `ProtectedPage_Unauthenticated_RedirectsToLogin`   | —                                  | HTTP 302 → `/Account/Login`                             | xUnit PASS           |

---

## COURSE — `CourseFlowTests.cs` (10 metodi · 12 esecuzioni)

| ID        | Tipo     | Metodo di test                                    | Varianti (InlineData)          | Risultato atteso                                                    | Risultato automatico |
|-----------|----------|---------------------------------------------------|--------------------------------|---------------------------------------------------------------------|----------------------|
| COURSE-001| [Theory] | `CourseIndex_AuthenticatedUser_Returns200`        | "instructor", "attendee"       | HTTP 200                                                            | xUnit PASS           |
| COURSE-002| [Fact]   | `CourseCreate_InstructorWithCanTeach_Returns200`  | —                              | HTTP 200                                                            | xUnit PASS           |
| COURSE-003| [Fact]   | `CourseCreate_AttendeeWithoutCanTeach_IsForbidden`| —                              | HTTP 302/403 (accesso negato)                                        | xUnit PASS           |
| COURSE-004| [Theory] | `CourseDetails_AuthenticatedUser_Returns200`      | "instructor", "attendee"       | HTTP 200                                                            | xUnit PASS           |
| COURSE-005| [Fact]   | `Enroll_AttendeeUser_SucceedsAndRedirects`        | —                              | HTTP 302; record `enrollments` presente nel DB                      | xUnit PASS           |
| COURSE-006| [Fact]   | `LessonDetails_EnrolledAttendee_Returns200`       | —                              | HTTP 200                                                            | xUnit PASS           |
| COURSE-007| [Fact]   | `LessonDetails_NotEnrolledAttendee_IsForbidden`   | —                              | HTTP 302/403 (accesso negato)                                        | xUnit PASS           |
| COURSE-008| [Fact]   | `LessonCreate_InstructorWithCanTeach_Returns200`  | —                              | HTTP 200                                                            | xUnit PASS           |
| COURSE-009| [Fact]   | `LessonCreate_AttendeeWithoutCanTeach_IsForbidden`| —                              | HTTP 302/403 (accesso negato)                                        | xUnit PASS           |
| COURSE-010| [Fact]   | `CourseIndex_Unauthenticated_RedirectsToLogin`    | —                              | HTTP 302 → `/Account/Login`                                         | xUnit PASS           |

---

## QUIZ — `QuizFlowTests.cs` (6 metodi · 6 esecuzioni)

| ID      | Tipo   | Metodo di test                                     | Varianti (InlineData) | Risultato atteso                                                          | Risultato automatico |
|---------|--------|----------------------------------------------------|-----------------------|---------------------------------------------------------------------------|----------------------|
| QUIZ-001| [Fact] | `TakeQuiz_AsEnrolledAttendee_ShowsQuizPage`        | —                     | HTTP 200; HTML contiene titolo quiz, testo domanda, contatore "1 /"       | xUnit PASS           |
| QUIZ-002| [Fact] | `SubmitQuiz_WithCorrectAnswer_ReturnsPassedResult` | —                     | HTTP 302 → `/Quiz/Result`; pagina risultato mostra punteggio 100          | xUnit PASS           |
| QUIZ-003| [Fact] | `SubmitQuiz_WithWrongAnswer_ReturnsFailedResult`   | —                     | HTTP 302 → `/Quiz/Result`; pagina risultato mostra punteggio 0            | xUnit PASS           |
| QUIZ-004| [Fact] | `TakeQuiz_Unauthenticated_RedirectsToLogin`        | —                     | HTTP 302 → `/Account/Login`                                               | xUnit PASS           |
| QUIZ-005| [Fact] | `QuizHistory_AfterAttempt_ShowsAttempt`            | —                     | HTTP 200; HTML contiene punteggio o titolo quiz                           | xUnit PASS           |
| QUIZ-006| [Fact] | `TakeQuiz_AttendeeNotEnrolled_RedirectsToAccessDenied` | —                 | HTTP 302 → `/Account/AccessDenied`                                        | xUnit PASS           |

---

## ADMIN — `AdminCrudTests.cs` (10 metodi · 10 esecuzioni)

| ID       | Tipo   | Metodo di test                                       | Varianti (InlineData) | Risultato atteso                                                        | Risultato automatico |
|----------|--------|------------------------------------------------------|-----------------------|-------------------------------------------------------------------------|----------------------|
| ADMIN-001| [Fact] | `UsersList_AsAdmin_Returns200`                       | —                     | HTTP 200                                                                | xUnit PASS           |
| ADMIN-002| [Fact] | `UsersList_AsNonAdmin_IsForbidden`                   | —                     | HTTP 302/403 (accesso negato)                                            | xUnit PASS           |
| ADMIN-003| [Fact] | `CreateUser_AsAdmin_PersistsUser`                    | —                     | HTTP 302; utente trovato nel DB; cleanup dopo il test                   | xUnit PASS           |
| ADMIN-004| [Fact] | `CreateArea_AsAdmin_PersistsArea`                    | —                     | HTTP 302; area trovata nel DB; cleanup dopo il test                     | xUnit PASS           |
| ADMIN-005| [Fact] | `DeleteArea_WithNoUsers_RemovesArea`                 | —                     | HTTP 302; area non trovata nel DB                                       | xUnit PASS           |
| ADMIN-006| [Fact] | `CreateDocumentType_AsAdmin_PersistsDocType`         | —                     | HTTP 302; tipo documento trovato nel DB; cleanup dopo il test           | xUnit PASS           |
| ADMIN-007| [Fact] | `DeleteDocumentType_WithNoMaterials_RemovesDocType`  | —                     | HTTP 302; tipo documento non trovato nel DB                             | xUnit PASS           |
| ADMIN-008| [Fact] | `AdminDashboard_AsAdmin_Returns200`                  | —                     | HTTP 200                                                                | xUnit PASS           |
| ADMIN-009| [Fact] | `AdminDashboard_AsNonAdmin_IsForbidden`              | —                     | HTTP 302/403 (accesso negato)                                            | xUnit PASS           |
| ADMIN-010| [Fact] | `AppSettings_AsAdmin_Returns200`                     | —                     | HTTP 200                                                                | xUnit PASS           |

---

## ROLES — `RoleCrudTests.cs` (7 metodi · 7 esecuzioni)

| ID      | Tipo   | Metodo di test                              | Varianti (InlineData) | Risultato atteso                                                                     | Risultato automatico |
|---------|--------|---------------------------------------------|-----------------------|--------------------------------------------------------------------------------------|----------------------|
| ROLE-001| [Fact] | `CreateRole_AsAdmin_PersistsInDatabase`     | —                     | HTTP 302; ruolo trovato nel DB; cleanup dopo il test                                 | xUnit PASS           |
| ROLE-002| [Fact] | `CreateRole_WithCanTeachFlag_StoredCorrectly` | —                   | HTTP 302; `canTeach=true`, `canAttend=false` verificati nel DB                       | xUnit PASS           |
| ROLE-003| [Fact] | `UsersPage_RolesTab_ReturnsOk_AsAdmin`      | —                     | HTTP 200                                                                             | xUnit PASS           |
| ROLE-004| [Fact] | `EditRole_AsAdmin_UpdatesName`              | —                     | HTTP 302; nuovo nome trovato nel DB; vecchio nome assente                            | xUnit PASS           |
| ROLE-005| [Fact] | `DeleteRole_WithNoUsers_RemovesRole`        | —                     | HTTP 302; ruolo non trovato nel DB                                                   | xUnit PASS           |
| ROLE-006| [Fact] | `DeleteRole_Admin_IsProtected`              | —                     | HTTP 302; ruolo `Admin` ancora presente nel DB (operazione bloccata dal controller)  | xUnit PASS           |
| ROLE-007| [Fact] | `RoleManagement_Unauthenticated_RedirectsToLogin` | —               | HTTP 302 → `/Account/Login`                                                          | xUnit PASS           |

---

## MATERIALS — `MaterialFlowTests.cs` (5 metodi · 8 esecuzioni)

| ID      | Tipo     | Metodo di test                                          | Varianti (InlineData)              | Risultato atteso                                                   | Risultato automatico |
|---------|----------|---------------------------------------------------------|------------------------------------|--------------------------------------------------------------------|----------------------|
| MAT-001 | [Theory] | `MaterialsIndex_AuthenticatedUser_Returns200`           | "instructor", "attendee", "admin"  | HTTP 200                                                           | xUnit PASS           |
| MAT-002 | [Theory] | `MaterialsCreate_CanTeachOrAdmin_Returns200`            | "instructor", "admin"              | HTTP 200                                                           | xUnit PASS           |
| MAT-003 | [Fact]   | `MaterialsCreate_CanAttendUser_IsForbidden`             | —                                  | HTTP 302/403 (accesso negato)                                       | xUnit PASS           |
| MAT-004 | [Fact]   | `MaterialsIndex_Unauthenticated_RedirectsToLogin`       | —                                  | HTTP 302 → `/Account/Login`                                        | xUnit PASS           |
| MAT-005 | [Fact]   | `MaterialDetails_CanAttendUser_CanViewPublishedMaterial`| —                                  | HTTP 200; HTML contiene titolo materiale                           | xUnit PASS           |

---

## Configurazione ambiente di test

```
Test DB    : MySQL separato — MYSQL_CONNECTION_STRING_TEST (override via appsettings.Test.json)
Factory    : LmsWebFactory : WebApplicationFactory<Program>
Helper     : DbTestHelper — crea/elimina fixture; cleanup in DisposeAsync()
Isolamento : Ogni test class usa suffix GUID per utenti/risorse → zero conflitti tra test
SMTP       : Disabilitato via EnsureSmtpDisabledAsync() prima di ogni suite
```

### Comando di esecuzione

```bash
cd artifacts/bocconi-lms
dotnet test BocconiLMS.Tests/BocconiLMS.Tests.csproj --logger "console;verbosity=normal"
```

---

## Riepilogo copertura

| File di test          | Metodi di test | [Fact] | [Theory] | Esecuzioni totali (InlineData espanse) |
|-----------------------|----------------|--------|----------|----------------------------------------|
| `LoginFlowTests.cs`   | 6              | 3      | 3        | 12                                     |
| `CourseFlowTests.cs`  | 10             | 8      | 2        | 12                                     |
| `QuizFlowTests.cs`    | 6              | 6      | 0        | 6                                      |
| `AdminCrudTests.cs`   | 10             | 10     | 0        | 10                                     |
| `RoleCrudTests.cs`    | 7              | 7      | 0        | 7                                      |
| `MaterialFlowTests.cs`| 5              | 3      | 2        | 8                                      |
| **Totale**            | **44**         | **37** | **7**    | **55**                                 |

> **Nota**: ogni `[Theory]` con N `[InlineData]` genera N esecuzioni separate nel test runner xUnit.  
> Il totale 55 esecuzioni corrisponde alla somma delle righe InlineData; i 44 metodi di test sono le unità codificate nel sorgente.
