# Diagramma E-R — Didasco LMS (Università Bocconi)

Versione: 1.0 — aggiornata al 2026-04-30  
Fonte: `Migrations/000_baseline_schema.sql` + migrazioni 001–021

---

## Schema relazionale (testo ASCII)

```
users ──< courses (teacher_id)
users ──< enrollments
users ──< lesson_progress
users ──< quiz_attempts
users ──< materials (owner_id)
users ──< material_versions (uploaded_by)
users ──< lesson_materials (added_by)
users ──< password_reset_tokens
users ──< user_areas
users ──< roles (created_by — logico, non FK)

roles ──< role_permissions

courses ──< lessons
lessons ──< quizzes
lessons ──< lesson_progress
lessons ──< lesson_materials
lessons ──< documents (legacy, rimossi dalla migrazione 008)

quizzes ──< quiz_questions
quiz_questions ──< quiz_options
quizzes ──< quiz_attempts

areas ──< user_areas
areas ──< materials (area_id)

material_folders ──< materials (folder_id)
document_types ──< materials (document_type_id)
platforms ──< materials (platform_id)
materials ──< material_versions
materials ──< lesson_materials

translations — entità standalone (nessuna FK)
```

---

## Dettaglio tabelle

### `users`
| Colonna        | Tipo             | Vincoli                  | Descrizione                         |
|----------------|------------------|--------------------------|-------------------------------------|
| id             | INT              | PK, AUTO_INCREMENT       | Identificativo univoco utente       |
| email          | VARCHAR(255)     | NOT NULL, UNIQUE         | Indirizzo e-mail (login)            |
| password_hash  | VARCHAR(255)     | NOT NULL                 | Hash BCrypt della password          |
| first_name     | VARCHAR(100)     | NOT NULL                 | Nome                                |
| last_name      | VARCHAR(100)     | NOT NULL                 | Cognome                             |
| role           | VARCHAR(50)      | NOT NULL DEFAULT ''      | Nome del ruolo (denormalizzato)     |
| is_active      | TINYINT(1)       | NOT NULL DEFAULT 1       | 1 = attivo, 0 = disattivato         |
| created_at     | DATETIME         | NOT NULL DEFAULT NOW()   | Data creazione record               |
| created_by     | INT NULL         | —                        | ID utente che ha creato il record   |

**Indici:** `idx_email(email)`, `idx_role(role)`

---

### `roles`
| Colonna          | Tipo         | Vincoli                  | Descrizione                         |
|------------------|--------------|--------------------------|-------------------------------------|
| id               | INT          | PK, AUTO_INCREMENT       | Identificativo ruolo                |
| name             | VARCHAR(256) | NOT NULL                 | Nome leggibile (es. "Teacher")      |
| normalized_name  | VARCHAR(256) | NOT NULL, UNIQUE         | Versione maiuscola per ricerca      |
| created_at       | DATETIME     | NOT NULL DEFAULT NOW()   | Data creazione                      |
| created_by       | INT NULL     | —                        | Utente creatore                     |

---

### `role_permissions`
| Colonna        | Tipo         | Vincoli                           | Descrizione                          |
|----------------|--------------|-----------------------------------|--------------------------------------|
| role_id        | INT          | PK (composita), FK → roles(id)    | Ruolo di riferimento                 |
| permission_key | VARCHAR(50)  | PK (composita)                    | Chiave permesso (es. `courses.teach`)|

**Permessi disponibili:** `courses.teach`, `courses.attend`, `menu.materials`, `materials.*`, `menu.users`, `menu.translations`

---

### `courses`
| Colonna      | Tipo         | Vincoli                         | Descrizione                         |
|--------------|--------------|---------------------------------|-------------------------------------|
| id           | INT          | PK, AUTO_INCREMENT              |                                     |
| title        | VARCHAR(255) | NOT NULL                        | Titolo del corso                    |
| description  | TEXT         | NOT NULL                        | Descrizione                         |
| category     | VARCHAR(100) | NOT NULL                        | Categoria/area tematica             |
| teacher_id   | INT          | NOT NULL, FK → users(id) CASCADE| Docente responsabile                |
| start_date   | DATE         | NULL                            | Data inizio                         |
| end_date     | DATE         | NULL                            | Data fine                           |
| is_published | TINYINT(1)   | NOT NULL DEFAULT 0              | 1 = pubblicato, 0 = bozza           |
| created_at   | DATETIME     | NOT NULL DEFAULT NOW()          |                                     |
| created_by   | INT NULL     | FK → users(id) SET NULL         |                                     |

---

### `lessons`
| Colonna      | Tipo         | Vincoli                            | Descrizione                       |
|--------------|--------------|------------------------------------|-----------------------------------|
| id           | INT          | PK, AUTO_INCREMENT                 |                                   |
| course_id    | INT          | NOT NULL, FK → courses(id) CASCADE |                                   |
| title        | VARCHAR(255) | NOT NULL                           |                                   |
| content      | TEXT         | NULL                               | Testo HTML della lezione          |
| sort_order   | INT          | NOT NULL DEFAULT 0                 | Ordine nella sequenza             |
| is_published | TINYINT(1)   | NOT NULL DEFAULT 0                 |                                   |
| created_at   | DATETIME     | NOT NULL DEFAULT NOW()             |                                   |

---

### `quizzes`
| Colonna             | Tipo         | Vincoli                             | Descrizione                         |
|---------------------|--------------|-------------------------------------|-------------------------------------|
| id                  | INT          | PK, AUTO_INCREMENT                  |                                     |
| lesson_id           | INT          | NOT NULL, FK → lessons(id) CASCADE  |                                     |
| title               | VARCHAR(255) | NOT NULL                            |                                     |
| description         | TEXT         | NULL                                |                                     |
| time_limit_minutes  | INT          | NOT NULL DEFAULT 30                 | Limite di tempo in minuti           |
| passing_score       | INT          | NOT NULL DEFAULT 60                 | Punteggio minimo (percentuale)      |
| created_at          | DATETIME     | NOT NULL DEFAULT NOW()              |                                     |
| created_by          | INT NULL     | FK → users(id) SET NULL             |                                     |

---

### `quiz_questions`
| Colonna       | Tipo | Vincoli                            | Descrizione              |
|---------------|------|------------------------------------|--------------------------|
| id            | INT  | PK, AUTO_INCREMENT                 |                          |
| quiz_id       | INT  | NOT NULL, FK → quizzes(id) CASCADE |                          |
| question_text | TEXT | NOT NULL                           | Testo della domanda      |
| sort_order    | INT  | NOT NULL DEFAULT 0                 | Ordine di presentazione  |

---

### `quiz_options`
| Colonna     | Tipo       | Vincoli                                  | Descrizione                       |
|-------------|------------|------------------------------------------|-----------------------------------|
| id          | INT        | PK, AUTO_INCREMENT                       |                                   |
| question_id | INT        | NOT NULL, FK → quiz_questions(id) CASCADE|                                   |
| option_text | TEXT       | NOT NULL                                 | Testo dell'opzione di risposta    |
| is_correct  | TINYINT(1) | NOT NULL DEFAULT 0                       | 1 = risposta corretta             |
| sort_order  | INT        | NOT NULL DEFAULT 0                       |                                   |

---

### `enrollments`
| Colonna     | Tipo     | Vincoli                                       | Descrizione                    |
|-------------|----------|-----------------------------------------------|--------------------------------|
| id          | INT      | PK, AUTO_INCREMENT                            |                                |
| user_id     | INT      | NOT NULL, FK → users(id) CASCADE              | Studente iscritto              |
| course_id   | INT      | NOT NULL, FK → courses(id) CASCADE            |                                |
| enrolled_at | DATETIME | NOT NULL DEFAULT NOW()                        | Timestamp iscrizione           |

**Vincolo unico:** `(user_id, course_id)`

---

### `lesson_progress`
| Colonna      | Tipo     | Vincoli                                    | Descrizione                  |
|--------------|----------|--------------------------------------------|------------------------------|
| id           | INT      | PK, AUTO_INCREMENT                         |                              |
| user_id      | INT      | NOT NULL, FK → users(id) CASCADE           |                              |
| lesson_id    | INT      | NOT NULL, FK → lessons(id) CASCADE         |                              |
| completed_at | DATETIME | NOT NULL DEFAULT NOW()                     | Timestamp completamento      |

**Vincolo unico:** `(user_id, lesson_id)`

---

### `quiz_attempts`
| Colonna         | Tipo       | Vincoli                              | Descrizione                         |
|-----------------|------------|--------------------------------------|-------------------------------------|
| id              | INT        | PK, AUTO_INCREMENT                   |                                     |
| quiz_id         | INT        | NOT NULL, FK → quizzes(id) CASCADE   |                                     |
| user_id         | INT        | NOT NULL, FK → users(id) CASCADE     |                                     |
| score           | INT        | NOT NULL                             | Punteggio percentuale (0–100)       |
| total_questions | INT        | NOT NULL                             |                                     |
| correct_answers | INT        | NOT NULL                             |                                     |
| passed          | TINYINT(1) | NOT NULL DEFAULT 0                   | 1 = superato                        |
| attempted_at    | DATETIME   | NOT NULL DEFAULT NOW()               |                                     |

---

### `password_reset_tokens`
| Colonna    | Tipo         | Vincoli                          | Descrizione                    |
|------------|--------------|----------------------------------|--------------------------------|
| id         | INT          | PK, AUTO_INCREMENT               |                                |
| user_id    | INT          | NOT NULL, FK → users(id) CASCADE |                                |
| token      | VARCHAR(64)  | NOT NULL, UNIQUE                 | Token crittograficamente sicuro|
| expires_at | DATETIME     | NOT NULL                         | Scadenza (es. +24h)            |
| used       | TINYINT(1)   | NOT NULL DEFAULT 0               | 1 = già utilizzato             |
| created_at | DATETIME     | NOT NULL DEFAULT NOW()           |                                |

---

### `areas`
| Colonna    | Tipo         | Vincoli                | Descrizione              |
|------------|--------------|------------------------|--------------------------|
| id         | INT          | PK, AUTO_INCREMENT     |                          |
| name       | VARCHAR(255) | NOT NULL               | Nome dell'area           |
| sort_order | INT          | NOT NULL DEFAULT 0     | Ordinamento              |
| created_at | DATETIME     | NOT NULL DEFAULT NOW() |                          |
| created_by | INT NULL     | —                      |                          |

---

### `user_areas`
| Colonna | Tipo | Vincoli                                          | Descrizione                |
|---------|------|--------------------------------------------------|----------------------------|
| user_id | INT  | PK (composita), FK → users(id) CASCADE           | Utente assegnato all'area  |
| area_id | INT  | PK (composita), FK → areas(id) CASCADE           | Area di appartenenza       |

---

### `translations`
| Colonna       | Tipo         | Vincoli                            | Descrizione                         |
|---------------|--------------|------------------------------------|-------------------------------------|
| id            | INT          | PK, AUTO_INCREMENT                 |                                     |
| language_code | VARCHAR(10)  | NOT NULL                           | Codice lingua (es. `it`, `en`)      |
| label_key     | VARCHAR(255) | NOT NULL                           | Chiave della stringa                |
| label_value   | TEXT         | NOT NULL                           | Testo tradotto                      |
| created_at    | DATETIME     | NOT NULL DEFAULT NOW()             |                                     |
| updated_at    | DATETIME     | NOT NULL, aggiornato automaticamente|                                    |

**Vincolo unico:** `(language_code, label_key)`

---

### `materials`
| Colonna                | Tipo         | Vincoli                                         | Descrizione                         |
|------------------------|--------------|-------------------------------------------------|-------------------------------------|
| id                     | INT          | PK, AUTO_INCREMENT                              |                                     |
| title                  | VARCHAR(255) | NOT NULL, UNIQUE                                |                                     |
| author_name            | VARCHAR(255) | NULL                                            | Autore esterno (testo libero)       |
| owner_id               | INT NULL     | FK → users(id) SET NULL                         | Utente proprietario                 |
| language               | VARCHAR(50)  | NOT NULL DEFAULT 'Italiano'                     |                                     |
| document_type_id       | INT NULL     | FK → document_types(id) SET NULL               |                                     |
| status                 | VARCHAR(50)  | NOT NULL DEFAULT 'bozza'                        | `bozza`, `pubblicato`, ecc.         |
| protocol_number        | INT NULL     |                                                  | Numero protocollo interno           |
| folder_id              | INT NULL     | FK → material_folders(id) SET NULL              |                                     |
| folder                 | VARCHAR(255) | NULL                                            | Percorso cartella (legacy)          |
| area_id                | INT NULL     | FK → areas(id) SET NULL                         |                                     |
| catalogation_date      | DATE NULL    |                                                  | Data catalogazione                  |
| page_count             | INT NULL     |                                                  | Numero di pagine                    |
| is_publishable         | TINYINT(1)   | NOT NULL DEFAULT 0                              |                                     |
| external_protocol_code | VARCHAR(100) | NULL                                            |                                     |
| platform_id            | INT NULL     | FK → platforms(id) SET NULL                     |                                     |
| is_published           | TINYINT(1)   | NOT NULL DEFAULT 0                              |                                     |
| external_link          | VARCHAR(500) | NULL                                            |                                     |
| created_at             | DATETIME     | NOT NULL DEFAULT NOW()                          |                                     |

---

### `material_versions`
| Colonna        | Tipo         | Vincoli                               | Descrizione               |
|----------------|--------------|---------------------------------------|---------------------------|
| id             | INT          | PK, AUTO_INCREMENT                    |                           |
| material_id    | INT          | NOT NULL, FK → materials(id) CASCADE  |                           |
| version_number | INT          | NOT NULL                              | Numero versione (1, 2...) |
| file_name      | VARCHAR(255) | NOT NULL                              |                           |
| file_path      | VARCHAR(500) | NOT NULL                              | Percorso file su disco    |
| file_type      | VARCHAR(20)  | NOT NULL                              | Estensione (pdf, docx…)   |
| file_size_bytes| BIGINT       | NOT NULL DEFAULT 0                    |                           |
| uploaded_by    | INT          | NOT NULL, FK → users(id)              | Utente che ha caricato    |
| notes          | TEXT         | NULL                                  |                           |
| is_active      | TINYINT(1)   | NOT NULL DEFAULT 1                    | 1 = versione corrente     |
| uploaded_at    | DATETIME     | NOT NULL DEFAULT NOW()                |                           |

**Vincolo unico:** `(material_id, version_number)`

---

### `lesson_materials`
| Colonna     | Tipo     | Vincoli                                      | Descrizione               |
|-------------|----------|----------------------------------------------|---------------------------|
| lesson_id   | INT      | PK (composita), FK → lessons(id) CASCADE     |                           |
| material_id | INT      | PK (composita), FK → materials(id) CASCADE   |                           |
| added_at    | DATETIME | NOT NULL DEFAULT NOW()                       |                           |
| added_by    | INT NULL | FK → users(id) SET NULL                      |                           |

---

### `document_types`
| Colonna    | Tipo         | Vincoli           | Descrizione          |
|------------|--------------|-------------------|----------------------|
| id         | INT          | PK, AUTO_INCREMENT|                      |
| name       | VARCHAR(255) | NOT NULL, UNIQUE  | Es. "PDF", "Video"   |
| sort_order | INT          | NOT NULL DEFAULT 0|                      |

---

### `material_folders`
| Colonna    | Tipo         | Vincoli           | Descrizione                    |
|------------|--------------|-------------------|--------------------------------|
| id         | INT          | PK, AUTO_INCREMENT|                                |
| name       | VARCHAR(255) | NOT NULL, UNIQUE  | Nome cartella                  |
| created_at | DATETIME     | NOT NULL DEFAULT NOW()|                           |

---

### `platforms`
| Colonna    | Tipo         | Vincoli                | Descrizione                     |
|------------|--------------|------------------------|---------------------------------|
| id         | INT          | PK, AUTO_INCREMENT     |                                 |
| name       | VARCHAR(255) | NOT NULL, UNIQUE       | Es. "YouTube", "Moodle"         |
| sort_order | INT          | NOT NULL DEFAULT 0     |                                 |
| created_at | DATETIME     | NOT NULL DEFAULT NOW() |                                 |

---

## Glossario

| Termine              | Significato                                                                 |
|----------------------|-----------------------------------------------------------------------------|
| PK                   | Primary Key — chiave primaria, identifica univocamente il record            |
| FK                   | Foreign Key — vincolo referenziale verso un'altra tabella                   |
| CASCADE              | All'eliminazione del padre, i figli vengono eliminati automaticamente       |
| SET NULL             | All'eliminazione del padre, la FK nel figlio viene posta a NULL             |
| TINYINT(1)           | Usato come booleano: 0 = false, 1 = true                                    |
| normalized_name      | Versione UPPERCASE usata per confronti case-insensitive nei ruoli           |
| permission_key       | Stringa puntata che identifica un permesso funzionale (es. `courses.teach`) |
| label_key            | Identificatore unico di una stringa dell'interfaccia utente multilingua     |
| password_hash        | Hash BCrypt (work factor 11) — la password in chiaro non viene mai salvata  |
| status (materials)   | Ciclo di vita: `bozza` → `pubblicato`                                       |
| is_active (mv)       | Solo una versione per materiale è attiva contemporaneamente                 |
