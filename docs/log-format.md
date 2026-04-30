# Formato Log — Didasco LMS (Università Bocconi)

Versione: 1.0 — aggiornata al 2026-04-30  
Riferimento: `Services/AuditLogger.cs`, `Middleware/HttpAccessLogMiddleware.cs`

---

## Panoramica

L'applicazione produce tre categorie di log, tutte convogliate nel sistema di logging standard di ASP.NET Core (`Microsoft.Extensions.Logging`). Il backend di output predefinito è lo **stdout** (console), compatibile con `systemd`, Docker e qualsiasi aggregatore log (Elastic, Loki, Azure Monitor, ecc.).

| Categoria        | Tag             | Classe sorgente                | Livello ASP.NET | Configurazione             |
|------------------|-----------------|-------------------------------|-----------------|----------------------------|
| Audit applicativo| `[APP-AUDIT]`   | `AuditLogger`                 | Information     | `AuditLog:Enabled/Level`   |
| Accesso HTTP     | `[HTTP-ACCESS]` | `HttpAccessLogMiddleware`     | Information     | sempre attivo              |
| Health Check     | `[$HEALTH-CHECK]`| `Program.cs` (MapHealthChecks)| Information     | sempre attivo              |

---

## 1. Audit Log Applicativo — `[APP-AUDIT]`

### Formato riga

```
[APP-AUDIT] <ISO8601-UTC> | user=<email|anonymous> | ip=<IPv4/IPv6|-> | action=<action> [| target=<target>] | outcome=<outcome>
```

### Campi

| Campo       | Tipo        | Esempio                          | Descrizione                                                   |
|-------------|-------------|----------------------------------|---------------------------------------------------------------|
| Timestamp   | ISO8601 UTC | `2026-04-30T14:22:01.123+00:00`  | Data/ora UTC precisa all'millisecondo                         |
| `user`      | string      | `mario.rossi@bocconi.it`         | E-mail dell'utente autenticato; `anonymous` se non autenticato|
| `ip`        | string      | `192.168.1.42` / `-`             | IP del client; `-` se non disponibile                         |
| `action`    | string      | `course.create`                  | Identificatore dell'azione (vedi tabella sotto)               |
| `target`    | string      | `courseId=12`                    | Risorsa coinvolta — omesso se non applicabile                 |
| `outcome`   | string      | `success` / `failure`            | Esito dell'operazione                                         |

### Azioni catalogate

Le azioni di seguito rispecchiano esattamente le stringhe prodotte dal codice sorgente (`AccountController`, `AdminController`, `CourseController`, `LessonController`, `QuizController`, `MaterialsController`).

| Azione                    | Livello  | Controller          | Descrizione                                    |
|---------------------------|----------|---------------------|------------------------------------------------|
| `auth.login`              | minimal  | AccountController   | Accesso riuscito                               |
| `auth.login` (failure)    | minimal  | AccountController   | Tentativo di accesso fallito (outcome=failure) |
| `auth.logout`             | minimal  | AccountController   | Logout eseguito                                |
| `auth.password_change`    | minimal  | AccountController   | Cambio password completato (reset)             |
| `auth.password_reset`     | minimal  | AccountController   | Reset password completato via token            |
| `user.create`             | standard | AdminController     | Nuovo utente creato da Admin                   |
| `user.edit`               | standard | AdminController     | Dati utente modificati                         |
| `user.delete`             | standard | AdminController     | Utente eliminato                               |
| `user.role_change`        | standard | AdminController     | Ruolo utente modificato                        |
| `user.activate`           | standard | AdminController     | Account utente riattivato                      |
| `user.deactivate`         | standard | AdminController     | Account utente disattivato                     |
| `settings.email_saved`    | standard | AdminController     | Configurazione SMTP salvata                    |
| `role.create`             | standard | AdminController     | Nuovo ruolo creato                             |
| `role.edit`               | standard | AdminController     | Ruolo modificato                               |
| `role.delete`             | standard | AdminController     | Ruolo eliminato                                |
| `course.create`           | standard | CourseController    | Nuovo corso creato                             |
| `course.edit`             | standard | CourseController    | Corso modificato                               |
| `course.delete`           | standard | CourseController    | Corso eliminato                                |
| `course.publish`          | standard | CourseController    | Corso pubblicato                               |
| `course.unpublish`        | standard | CourseController    | Pubblicazione corso revocata                   |
| `course.enroll`           | standard | CourseController    | Studente iscritto a un corso                   |
| `course.unenroll`         | standard | CourseController    | Studente disiscritto                           |
| `lesson.create`           | standard | LessonController    | Nuova lezione creata                           |
| `lesson.edit`             | standard | LessonController    | Lezione modificata                             |
| `lesson.delete`           | standard | LessonController    | Lezione eliminata                              |
| `quiz.create`             | standard | QuizController      | Nuovo quiz creato                              |
| `quiz.delete`             | standard | QuizController      | Quiz eliminato                                 |
| `quiz.submit`             | standard | QuizController      | Tentativo quiz inviato                         |
| `material.create`         | standard | MaterialsController | Nuovo materiale caricato                       |
| `material.edit`           | standard | MaterialsController | Materiale modificato                           |
| `material.delete`         | standard | MaterialsController | Materiale eliminato                            |

### Livelli di log audit

Configurazione in `appsettings.json`:
```json
"AuditLog": {
  "Enabled": true,
  "Level": "standard"
}
```

| `Level`    | Comportamento                                                       |
|------------|---------------------------------------------------------------------|
| `minimal`  | Solo eventi `LogMinimal()`: autenticazione (login, logout, reset)   |
| `standard` | Tutti gli eventi: autenticazione + operazioni CRUD (default)        |
| `verbose`  | Riservato a future estensioni; attualmente equivale a `standard`    |

Se `Enabled = false` nessun evento viene registrato (compreso il livello minimal).

### Esempi

```
[APP-AUDIT] 2026-04-30T14:22:01.123+00:00 | user=m.rossi@bocconi.it | ip=10.0.0.5 | action=auth.login | outcome=success
[APP-AUDIT] 2026-04-30T14:22:45.988+00:00 | user=m.rossi@bocconi.it | ip=10.0.0.5 | action=course.create | target=course#42 "Economia Aziendale" | outcome=success
[APP-AUDIT] 2026-04-30T14:23:00.001+00:00 | user=anonymous          | ip=10.0.0.5 | action=auth.login | outcome=failure
[APP-AUDIT] 2026-04-30T14:30:00.000+00:00 | user=admin@bocconi.it   | ip=10.0.0.5 | action=user.create | target=user#55 "nuovo@bocconi.it" role=Student | outcome=success
```

---

## 2. Log Accesso HTTP — `[HTTP-ACCESS]`

### Formato riga

```
[HTTP-ACCESS] <METHOD> <PATH> <STATUS> | user=<email|anonymous> | ip=<IP|-> | duration_ms=<ms>
```

### Campi

| Campo         | Tipo    | Esempio              | Descrizione                                        |
|---------------|---------|----------------------|----------------------------------------------------|
| `METHOD`      | string  | `GET`, `POST`        | Metodo HTTP                                        |
| `PATH`        | string  | `/Course/Details/12` | Path della richiesta (senza query string)          |
| `STATUS`      | int     | `200`, `302`, `403`  | Codice HTTP della risposta                         |
| `user`        | string  | `prof@bocconi.it`    | E-mail utente autenticato o `anonymous`            |
| `ip`          | string  | `10.0.0.5`           | IP del client                                      |
| `duration_ms` | int     | `42`                 | Tempo di elaborazione in millisecondi              |

### Path esclusi

I seguenti path **non** vengono registrati:
- `/health`
- `/favicon.ico`

### Esempi

```
[HTTP-ACCESS] GET  /Home/Dashboard 200 | user=m.rossi@bocconi.it | ip=10.0.0.5 | duration_ms=18
[HTTP-ACCESS] POST /Account/Login  302 | user=anonymous          | ip=10.0.0.5 | duration_ms=245
[HTTP-ACCESS] GET  /Admin/Users    403 | user=studente@bocconi.it| ip=10.0.0.5 | duration_ms=3
```

---

## 3. Health Check — `[$HEALTH-CHECK]`

### Formato riga (log)

```
[$HEALTH-CHECK] status=<healthy|degraded|unhealthy> duration_ms=<ms>
```

### Risposta JSON (endpoint `/health`)

```json
{
  "status": "healthy",
  "timestamp": "2026-04-30T14:00:00.000+00:00",
  "duration_ms": 12,
  "checks": [
    {
      "name": "database",
      "status": "healthy",
      "description": "MySQL connection OK",
      "duration_ms": 10,
      "error": null
    }
  ]
}
```

### Esempi log

```
[$HEALTH-CHECK] registered path=/health
[$HEALTH-CHECK] status=healthy duration_ms=12
[$HEALTH-CHECK] status=unhealthy duration_ms=5001
```

---

## Conservazione e rotazione

| Tipo log        | Conservazione consigliata | Note                                            |
|-----------------|---------------------------|-------------------------------------------------|
| `[APP-AUDIT]`   | 12 mesi                   | Obbligatorio per tracciamento accessi a dati PD |
| `[HTTP-ACCESS]` | 3 mesi                    | Utile per diagnostica e analisi traffico        |
| `[$HEALTH-CHECK]`| 1 mese                   | Monitoraggio disponibilità servizio             |

La rotazione è delegata all'infrastruttura di hosting (es. `logrotate`, Elastic Index Lifecycle Management, Azure Log Analytics retention policy).

---

## Glossario

| Termine     | Significato                                                                           |
|-------------|---------------------------------------------------------------------------------------|
| ISO8601 UTC | Formato datetime standard internazionale con offset `+00:00` (es. `2026-04-30T14:00:00.000+00:00`) |
| `anonymous` | Utente non autenticato al momento dell'azione                                         |
| `minimal`   | Livello audit che include solo eventi di autenticazione                               |
| `standard`  | Livello audit che include tutti gli eventi CRUD oltre a quelli di autenticazione      |
| `duration_ms`| Tempo di elaborazione della richiesta in millisecondi                                |
| `outcome`   | Esito dell'azione: `success` = completata correttamente, `failure` = errore/blocco   |

---

## Validazione documento

| Campo         | Valore                              |
|---------------|-------------------------------------|
| Data          | 2026-04-30                          |
| Approvatore   | _Da compilare — DPO / ICT Bocconi_  |
| Revisione     | Da compilare dopo revisione legale  |
| Versione doc. | Vedere intestazione                 |
