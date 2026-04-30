# Test Book — Didasco LMS (Università Bocconi)

Versione: 1.0 — aggiornata al 2026-04-30  
Framework: xUnit + Microsoft.AspNetCore.Mvc.Testing (test d'integrazione HTTP)  
File sorgente: `BocconiLMS.Tests/`

---

## Convenzioni

- **ID test**: `<MODULO>-<NNN>` (es. `AUTH-001`)
- **Precondizioni**: stato del sistema prima dell'esecuzione
- **Risultato atteso**: comportamento verificato dall'asserzione xUnit
- **Tipo**: `Integrazione` = test HTTP end-to-end contro WebApplicationFactory; `Unitario` = logica isolata

---

## AUTH — Autenticazione e Sessione

| ID       | Scenario                                        | Precondizioni                                    | Passi                                                                                          | Risultato atteso                                                         | Tipo        |
|----------|-------------------------------------------------|--------------------------------------------------|-----------------------------------------------------------------------------------------------|--------------------------------------------------------------------------|-------------|
| AUTH-001 | Login valido — docente                          | Utente docente creato con ruolo `canTeach`        | 1. GET `/Account/Login` → estrai token CSRF<br>2. POST `/Account/Login` con credenziali valide | HTTP 302 redirect a `/Home/Dashboard`                                    | Integrazione|
| AUTH-002 | Login valido — studente                         | Utente studente creato con ruolo `canAttend`      | Come AUTH-001 con utente studente                                                             | HTTP 302 redirect a `/Home/Dashboard`                                    | Integrazione|
| AUTH-003 | Login valido — admin                            | Utente admin con ruolo `Admin`                   | Come AUTH-001 con utente admin                                                                | HTTP 302 redirect a `/Home/Dashboard`                                    | Integrazione|
| AUTH-004 | Login con password errata                       | Utente esistente nel DB                          | POST `/Account/Login` con password sbagliata                                                  | HTTP 200; risposta contiene "Credenziali non valide"                     | Integrazione|
| AUTH-005 | Login con e-mail inesistente                    | Nessun utente con quella e-mail                  | POST `/Account/Login` con e-mail sconosciuta                                                  | HTTP 200; risposta contiene "Credenziali non valide"                     | Integrazione|
| AUTH-006 | Logout — docente                                | Utente docente autenticato                       | 1. Login<br>2. GET `/Home/Dashboard` → token CSRF<br>3. POST `/Account/Logout`               | HTTP 302 redirect a `/`                                                  | Integrazione|
| AUTH-007 | Logout — studente                               | Utente studente autenticato                      | Come AUTH-006 con utente studente                                                             | HTTP 302 redirect a `/`                                                  | Integrazione|
| AUTH-008 | Logout — admin                                  | Utente admin autenticato                         | Come AUTH-006 con utente admin                                                                | HTTP 302 redirect a `/`                                                  | Integrazione|
| AUTH-009 | Login con sessione già attiva — docente         | Utente docente già autenticato                   | GET `/Account/Login` con cookie di sessione valido                                            | HTTP 302 (redirect, non mostra la pagina di login)                       | Integrazione|
| AUTH-010 | Login con sessione già attiva — studente        | Utente studente già autenticato                  | Come AUTH-009 con utente studente                                                             | HTTP 302                                                                 | Integrazione|
| AUTH-011 | Login con sessione già attiva — admin           | Utente admin già autenticato                     | Come AUTH-009 con utente admin                                                                | HTTP 302                                                                 | Integrazione|
| AUTH-012 | Pagina protetta senza autenticazione            | Nessun cookie di sessione                        | GET `/Home/Dashboard` senza cookie                                                            | HTTP 302 redirect a `/Account/Login`                                     | Integrazione|

---

## COURSE — Gestione Corsi

| ID        | Scenario                                               | Precondizioni                                            | Passi                                                                        | Risultato atteso                                                  | Tipo        |
|-----------|--------------------------------------------------------|----------------------------------------------------------|------------------------------------------------------------------------------|-------------------------------------------------------------------|-------------|
| COURSE-001| Lista corsi — docente autenticato                      | Utente docente autenticato                               | GET `/Course/Index`                                                          | HTTP 200                                                          | Integrazione|
| COURSE-002| Lista corsi — studente autenticato                     | Utente studente autenticato                              | GET `/Course/Index`                                                          | HTTP 200                                                          | Integrazione|
| COURSE-003| Pagina creazione corso — docente con `courses.teach`   | Utente con permesso `courses.teach`                      | GET `/Course/Create`                                                         | HTTP 200                                                          | Integrazione|
| COURSE-004| Pagina creazione corso — studente senza `courses.teach`| Utente senza permesso `courses.teach`                    | GET `/Course/Create`                                                         | HTTP 403 o HTTP 302 a pagina accesso negato                       | Integrazione|
| COURSE-005| Dettaglio corso — docente autenticato                  | Corso pubblicato nel DB                                  | GET `/Course/Details/{courseId}`                                             | HTTP 200                                                          | Integrazione|
| COURSE-006| Dettaglio corso — studente autenticato                 | Corso pubblicato nel DB                                  | GET `/Course/Details/{courseId}`                                             | HTTP 200                                                          | Integrazione|
| COURSE-007| Iscrizione a corso — studente                          | Studente non ancora iscritto al corso; corso pubblicato  | 1. GET `/Course/Details/{id}` → token CSRF<br>2. POST `/Course/Enroll/{id}` | HTTP 302; record in `enrollments` verificato via DB               | Integrazione|
| COURSE-008| Lista corsi senza autenticazione                       | Nessun cookie di sessione                                | GET `/Course/Index` senza cookie                                             | HTTP 302 redirect a `/Account/Login`                              | Integrazione|

---

## LESSON — Gestione Lezioni

| ID        | Scenario                                             | Precondizioni                                              | Passi                                             | Risultato atteso                                                  | Tipo        |
|-----------|------------------------------------------------------|------------------------------------------------------------|---------------------------------------------------|-------------------------------------------------------------------|-------------|
| LESSON-001| Dettaglio lezione — studente iscritto                | Studente iscritto al corso; lezione pubblicata             | GET `/Lesson/Details/{lessonId}`                  | HTTP 200                                                          | Integrazione|
| LESSON-002| Dettaglio lezione — studente NON iscritto            | Studente non iscritto al corso                             | GET `/Lesson/Details/{lessonId}`                  | HTTP 403 o HTTP 302 (accesso negato)                              | Integrazione|
| LESSON-003| Pagina creazione lezione — docente con `courses.teach`| Utente con `courses.teach`; corso esistente               | GET `/Lesson/Create?courseId={id}`                | HTTP 200                                                          | Integrazione|
| LESSON-004| Pagina creazione lezione — studente senza `courses.teach`| Utente senza `courses.teach`                           | GET `/Lesson/Create?courseId={id}`                | HTTP 403 o HTTP 302 (accesso negato)                              | Integrazione|

---

## QUIZ — Somministrazione Quiz

| ID      | Scenario                                             | Precondizioni                                                        | Passi                                                                          | Risultato atteso                                                    | Tipo        |
|---------|------------------------------------------------------|----------------------------------------------------------------------|--------------------------------------------------------------------------------|---------------------------------------------------------------------|-------------|
| QUIZ-001| Visualizzazione quiz — studente iscritto             | Studente iscritto al corso; quiz con 1 domanda e 2 opzioni           | GET `/Quiz/Take/{quizId}`                                                      | HTTP 200; HTML contiene titolo quiz, testo domanda, contatore `1 /` | Integrazione|
| QUIZ-002| Invio risposta corretta                              | Come QUIZ-001                                                        | 1. GET `/Quiz/Take/{id}` → token CSRF<br>2. POST `/Quiz/Submit/{id}` con opzione corretta | HTTP 302 a `/Quiz/Result`; pagina risultato mostra punteggio 100 | Integrazione|
| QUIZ-003| Invio risposta errata                                | Come QUIZ-001                                                        | POST `/Quiz/Submit/{id}` con opzione sbagliata                                 | HTTP 302 a `/Quiz/Result`; pagina risultato mostra punteggio 0      | Integrazione|
| QUIZ-004| Accesso quiz senza autenticazione                    | Nessun cookie di sessione                                            | GET `/Quiz/Take/{quizId}` senza cookie                                         | HTTP 302 a `/Account/Login`                                         | Integrazione|
| QUIZ-005| Storico tentativi dopo risposta                      | Studente ha completato un tentativo                                  | GET `/Quiz/History?quizId={id}`                                                | HTTP 200; HTML contiene punteggio o titolo quiz                     | Integrazione|
| QUIZ-006| Accesso quiz — studente non iscritto al corso        | Studente autenticato ma non iscritto                                 | GET `/Quiz/Take/{quizId}`                                                      | HTTP 302 a `/Account/AccessDenied`                                  | Integrazione|

---

## ADMIN — Pannello Amministratore

| ID       | Scenario                                          | Precondizioni                            | Passi                                                                                    | Risultato atteso                                                    | Tipo        |
|----------|---------------------------------------------------|------------------------------------------|------------------------------------------------------------------------------------------|---------------------------------------------------------------------|-------------|
| ADMIN-001| Lista utenti — admin                              | Utente admin autenticato                 | GET `/Admin/Users`                                                                       | HTTP 200                                                            | Integrazione|
| ADMIN-002| Lista utenti — non-admin                          | Utente docente autenticato               | GET `/Admin/Users`                                                                       | HTTP 403 o HTTP 302 (accesso negato)                                | Integrazione|
| ADMIN-003| Creazione utente — admin                          | Admin autenticato                        | 1. GET `/Admin/CreateUser` → token CSRF<br>2. POST `/Admin/CreateUser` con dati validi  | HTTP 302; utente trovato nel DB; cleanup dopo il test               | Integrazione|
| ADMIN-004| Dashboard admin — admin                           | Admin autenticato                        | GET `/Admin/Dashboard`                                                                   | HTTP 200                                                            | Integrazione|
| ADMIN-005| Dashboard admin — non-admin                       | Docente autenticato                      | GET `/Admin/Dashboard`                                                                   | HTTP 403 o HTTP 302 (accesso negato)                                | Integrazione|
| ADMIN-006| Impostazioni app — admin                          | Admin autenticato                        | GET `/Admin/Settings`                                                                    | HTTP 200                                                            | Integrazione|
| ADMIN-007| Creazione area — admin                            | Admin autenticato                        | 1. GET `/Admin/Dictionary?tab=aree` → token<br>2. POST `/Admin/CreateArea`              | HTTP 302; area trovata nel DB; cleanup dopo il test                 | Integrazione|
| ADMIN-008| Eliminazione area senza utenti — admin            | Area senza utenti associati              | POST `/Admin/DeleteArea/{areaId}`                                                        | HTTP 302; area non trovata nel DB                                   | Integrazione|
| ADMIN-009| Creazione tipo documento — admin                  | Admin autenticato                        | 1. GET `/Admin/Dictionary?tab=doctypes` → token<br>2. POST `/Admin/CreateDocumentType` | HTTP 302; tipo documento nel DB; cleanup dopo il test               | Integrazione|
| ADMIN-010| Eliminazione tipo documento senza materiali — admin| Tipo documento senza materiali associati | POST `/Admin/DeleteDocumentType/{id}`                                                   | HTTP 302; tipo documento non trovato nel DB                         | Integrazione|

---

## ROLES — Gestione Ruoli

| ID      | Scenario                                              | Precondizioni                                       | Passi                                                                               | Risultato atteso                                                        | Tipo        |
|---------|-------------------------------------------------------|-----------------------------------------------------|-------------------------------------------------------------------------------------|-------------------------------------------------------------------------|-------------|
| ROLE-001| Creazione ruolo — admin                               | Admin autenticato                                   | 1. GET `/Admin/CreateRole` → token<br>2. POST `/Admin/CreateRole` con nome          | HTTP 302; ruolo trovato nel DB; cleanup dopo il test                    | Integrazione|
| ROLE-002| Creazione ruolo con flag `courses.teach` — admin      | Admin autenticato; feature flag CoursesModule=true  | POST `/Admin/CreateRole` con `permissions=courses.teach`                            | HTTP 302; `canTeach=true`, `canAttend=false` verificati nel DB          | Integrazione|
| ROLE-003| Lista ruoli — admin                                   | Admin autenticato                                   | GET `/Admin/Users?tab=ruoli`                                                        | HTTP 200                                                                | Integrazione|
| ROLE-004| Modifica nome ruolo — admin                           | Ruolo esistente nel DB                              | 1. GET `/Admin/EditRole/{id}` → token<br>2. POST `/Admin/EditRole` con nuovo nome  | HTTP 302; nuovo nome trovato; vecchio nome non trovato nel DB           | Integrazione|
| ROLE-005| Eliminazione ruolo senza utenti — admin               | Ruolo senza utenti associati                        | POST `/Admin/DeleteRole/{id}`                                                       | HTTP 302; ruolo non trovato nel DB                                      | Integrazione|
| ROLE-006| Eliminazione ruolo Admin — bloccata                   | Admin autenticato; ruolo `Admin` nel DB             | POST `/Admin/DeleteRole/{adminRoleId}`                                              | HTTP 302; ruolo `Admin` ancora presente nel DB (operazione bloccata)    | Integrazione|
| ROLE-007| Gestione ruoli — non autenticato                      | Nessun cookie di sessione                           | GET `/Admin/CreateRole` senza cookie                                                | HTTP 302 a `/Account/Login`                                             | Integrazione|

---

## MATERIALS — Libreria Materiali

| ID      | Scenario                                              | Precondizioni                               | Passi                                                 | Risultato atteso                                                          | Tipo        |
|---------|-------------------------------------------------------|---------------------------------------------|-------------------------------------------------------|---------------------------------------------------------------------------|-------------|
| MAT-001 | Lista materiali — docente autenticato                 | Docente autenticato                         | GET `/Materials/Index`                                | HTTP 200                                                                  | Integrazione|
| MAT-002 | Lista materiali — studente autenticato                | Studente autenticato                        | GET `/Materials/Index`                                | HTTP 200                                                                  | Integrazione|
| MAT-003 | Lista materiali — admin autenticato                   | Admin autenticato                           | GET `/Materials/Index`                                | HTTP 200                                                                  | Integrazione|
| MAT-004 | Pagina creazione materiale — docente (`canTeach`)     | Docente con `courses.teach` autenticato     | GET `/Materials/Create`                               | HTTP 200                                                                  | Integrazione|
| MAT-005 | Pagina creazione materiale — admin                    | Admin autenticato                           | GET `/Materials/Create`                               | HTTP 200                                                                  | Integrazione|
| MAT-006 | Pagina creazione materiale — studente (`canAttend`)   | Studente senza `materials.*` autenticato    | GET `/Materials/Create`                               | HTTP 403 o HTTP 302 a `/Account/AccessDenied`                            | Integrazione|
| MAT-007 | Lista materiali — non autenticato                     | Nessun cookie di sessione                   | GET `/Materials/Index` senza cookie                   | HTTP 302 a `/Account/Login`                                               | Integrazione|
| MAT-008 | Dettaglio materiale pubblicato — studente             | Materiale con `status='pubblicato'`; studente autenticato | GET `/Materials/Details/{id}`          | HTTP 200; HTML contiene titolo materiale                                  | Integrazione|

---

## Configurazione dell'ambiente di test

```
Test DB    : Database MySQL separato (configurabile via MYSQL_CONNECTION_STRING_TEST)
Factory    : LmsWebFactory (estende WebApplicationFactory<Program>)
Helper DB  : DbTestHelper — crea/elimina dati di test; cleanup in DisposeAsync()
Isolamento : Ogni test class crea i propri utenti/corsi con suffix GUID univoco
SMTP       : Disabilitato via EnsureSmtpDisabledAsync() prima di ogni test
```

### Esecuzione

```bash
cd artifacts/bocconi-lms
dotnet test BocconiLMS.Tests/BocconiLMS.Tests.csproj --logger "console;verbosity=normal"
```

---

## Copertura funzionale riepilogativa

| Modulo            | Test totali | Funzionalità coperte                                                    |
|-------------------|-------------|-------------------------------------------------------------------------|
| Autenticazione    | 12          | Login, logout, sessione attiva, pagine protette                         |
| Corsi             | 8           | Lista, dettaglio, creazione (RBAC), iscrizione                          |
| Lezioni           | 4           | Dettaglio (iscrizione richiesta), creazione (RBAC)                      |
| Quiz              | 6           | Visualizzazione, invio (corretto/errato), storico, RBAC, iscrizione     |
| Pannello Admin    | 10          | CRUD utenti/aree/tipi documento, dashboard, accesso non-admin bloccato  |
| Gestione Ruoli    | 7           | CRUD ruoli, permessi `canTeach`, protezione ruolo Admin                 |
| Materiali         | 8           | Lista, creazione (RBAC), dettaglio pubblicato                           |
| **Totale**        | **55**      |                                                                         |
