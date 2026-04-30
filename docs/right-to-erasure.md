# Diritto alla Cancellazione (Right to Erasure) — Didasco LMS (Università Bocconi)

Versione: 1.1 — aggiornata al 2026-04-30  
Base normativa: GDPR art. 17 — Diritto alla cancellazione ("diritto all'oblio")

---

## Panoramica

Il presente documento descrive la procedura operativa per soddisfare una richiesta di cancellazione dei dati personali ai sensi dell'art. 17 GDPR.  
La procedura distingue tra:
- **Anonimizzazione**: i dati identificativi vengono sovrascritti con valori non riconducibili all'interessato; il record rimane per integrità referenziale.
- **Eliminazione completa**: il record viene fisicamente rimosso; le FK CASCADE ne propagano l'effetto automaticamente.

> **Avvertenza legale:** Prima di eseguire qualsiasi operazione, verificare con il DPO (`dpo@unibocconi.it`) se esistono obblighi di conservazione prevalenti (es. archivio accademico, contenziosi in corso, obblighi fiscali). In presenza di tali obblighi, sostituire l'eliminazione con la sola anonimizzazione.

---

## Comportamento effettivo dei vincoli di chiave esterna

È fondamentale conoscere il comportamento reale del database prima di procedere. La tabella seguente riassume ogni FK che coinvolge la tabella `users`:

| FK (tabella.colonna)                     | Tipo FK       | Comportamento a DELETE users             |
|------------------------------------------|---------------|------------------------------------------|
| `courses.teacher_id → users`             | CASCADE       | **Elimina automaticamente tutti i corsi** del docente (e a cascata lezioni, quiz, iscrizioni, ecc.) |
| `courses.created_by → users`             | SET NULL      | Il campo viene impostato a NULL          |
| `material_versions.uploaded_by → users`  | RESTRICT (default MySQL) | **Blocca l'eliminazione** — impossibile eliminare l'utente se ha versioni caricate |
| `materials.owner_id → users`             | SET NULL      | Il campo viene impostato a NULL          |
| `lesson_materials.added_by → users`      | SET NULL      | Il campo viene impostato a NULL          |
| `quizzes.created_by → users`             | SET NULL      | Il campo viene impostato a NULL          |
| `enrollments.user_id → users`            | CASCADE       | Iscrizioni eliminate automaticamente     |
| `lesson_progress.user_id → users`        | CASCADE       | Progressi eliminati automaticamente      |
| `quiz_attempts.user_id → users`          | CASCADE       | Tentativi quiz eliminati automaticamente |
| `password_reset_tokens.user_id → users`  | CASCADE       | Token eliminati automaticamente          |
| `user_areas.user_id → users`             | CASCADE       | Associazioni area eliminate automaticamente |

> **Attenzione:** La FK `material_versions.uploaded_by` ha comportamento **RESTRICT** (default MySQL implicito, nessun `ON DELETE` dichiarato). Prima di eliminare l'utente dal DB occorre obbligatoriamente gestire questa dipendenza (vedi Step 3b).

---

## Verifica dei presupposti

Prima di procedere, verificare che:

1. La richiesta provenga dall'interessato o da un suo delegato verificato.
2. Non esistano obblighi di conservazione prevalenti (contratto, legge, archivio accademico).
3. L'utente non sia l'unico Admin del sistema (in tal caso nominare un sostituto prima di procedere).
4. L'operazione sia documentata nel registro dei trattamenti (data, operatore, riferimento richiesta).

---

## Step 1 — Identificare l'utente

```sql
-- Ricerca per e-mail (case-insensitive)
SELECT id, email, first_name, last_name, role, is_active, created_at
FROM users
WHERE email = 'utente@esempio.it';
```

Annotare il valore di `id` (es. `42`) — sarà `<USER_ID>` nei passaggi successivi.

---

## Step 2 — Esportare i dati dell'utente (portabilità, art. 20 GDPR)

Prima di cancellare, esportare i dati per consegnarli all'interessato se richiesto:

```sql
-- Dati anagrafici
SELECT id, email, first_name, last_name, role, is_active, created_at
FROM users WHERE id = <USER_ID>;

-- Iscrizioni a corsi
SELECT e.id, c.title AS course, e.enrolled_at
FROM enrollments e
JOIN courses c ON c.id = e.course_id
WHERE e.user_id = <USER_ID>;

-- Progressione lezioni
SELECT lp.id, l.title AS lesson, lp.completed_at
FROM lesson_progress lp
JOIN lessons l ON l.id = lp.lesson_id
WHERE lp.user_id = <USER_ID>;

-- Tentativi quiz
SELECT qa.id, q.title AS quiz, qa.score, qa.passed, qa.attempted_at
FROM quiz_attempts qa
JOIN quizzes q ON q.id = qa.quiz_id
WHERE qa.user_id = <USER_ID>;

-- Corsi insegnati (ATTENZIONE: eliminare l'utente cancella anche questi corsi se CASCADE)
SELECT id, title, is_published, created_at
FROM courses WHERE teacher_id = <USER_ID>;

-- Materiali di proprietà
SELECT id, title, status, created_at
FROM materials WHERE owner_id = <USER_ID>;

-- Versioni materiali caricate (FK RESTRICT: blocca eliminazione utente)
SELECT mv.id, m.title AS material, mv.version_number, mv.file_name, mv.uploaded_at
FROM material_versions mv
JOIN materials m ON m.id = mv.material_id
WHERE mv.uploaded_by = <USER_ID>;
```

---

## Step 3 — Gestire le dipendenze bloccanti (pre-requisiti per eliminazione/anonimizzazione)

### 3a. Corsi del docente

L'utente insegna corsi (`courses.teacher_id`): se si esegue `DELETE FROM users WHERE id = <USER_ID>`, MySQL eseguirà automaticamente `DELETE FROM courses WHERE teacher_id = <USER_ID>` per CASCADE, eliminando anche lezioni, quiz, iscrizioni e progressi collegati.

**Verificare con il DPO** se i corsi hanno rilevanza accademica e devono essere conservati. In caso affermativo:
```sql
-- Riassegnare i corsi a un altro docente prima di procedere
UPDATE courses SET teacher_id = <NUOVO_DOCENTE_ID> WHERE teacher_id = <USER_ID>;
```

### 3b. Versioni materiali caricate (RESTRICT — blocca eliminazione)

La FK `material_versions.uploaded_by` ha comportamento **RESTRICT**: non è possibile eliminare l'utente finché esistono versioni caricate da lui.

**Opzione A — Anonimizzare il riferimento (se non possibile eliminare le versioni):**

Non è possibile impostare `uploaded_by = NULL` perché la colonna è `NOT NULL`. Occorre riassegnare a un utente "anonimo" o "sistema":

```sql
-- Verificare se esiste un utente di sistema (es. id=1)
SELECT id, email FROM users WHERE email = 'sistema@bocconi.it' LIMIT 1;

-- Riassegnare le versioni all'utente di sistema
UPDATE material_versions
SET uploaded_by = <SISTEMA_USER_ID>
WHERE uploaded_by = <USER_ID>;
```

**Opzione B — Eliminare le versioni (solo se i file non hanno rilevanza accademica):**

```sql
-- Identificare prima i file da eliminare fisicamente
SELECT file_path, file_name FROM material_versions WHERE uploaded_by = <USER_ID>;

-- Eliminare le versioni dal DB
DELETE FROM material_versions WHERE uploaded_by = <USER_ID>;
-- Nota: eliminare i file fisici dal filesystem dopo aver confermato l'operazione DB
```

---

## Step 4a — Anonimizzazione (se obblighi di conservazione prevalenti)

Questa query sovrascrive i dati identificativi dell'utente con valori anonimi, mantenendo il record per integrità referenziale. Applicare dopo aver completato lo Step 3.

```sql
START TRANSACTION;

-- 4a.1 Anonimizzare il profilo utente
UPDATE users
SET
    email         = CONCAT('deleted_', id, '@anonimizzato.invalid'),
    password_hash = '[CANCELLATO]',
    first_name    = '[Cancellato]',
    last_name     = '[Cancellato]',
    is_active     = 0
WHERE id = <USER_ID>;

-- 4a.2 Revocare le aree organizzative
DELETE FROM user_areas WHERE user_id = <USER_ID>;

-- 4a.3 Eliminare i token di reset password (dati sensibili)
DELETE FROM password_reset_tokens WHERE user_id = <USER_ID>;

-- 4a.4 (Opzionale) Anonimizzare author_name nei materiali se corrisponde al nome dell'interessato
UPDATE materials
SET author_name = '[Cancellato]'
WHERE owner_id = <USER_ID> AND author_name IS NOT NULL;

COMMIT;
```

> Dopo questo step l'account non è più utilizzabile (password invalida, disattivato) e nome/e-mail non sono più identificabili.

---

## Step 4b — Eliminazione completa (se nessun obbligo di conservazione)

Applicare **dopo** aver completato lo Step 3 (riassegnazione corsi e gestione `material_versions`).

```sql
START TRANSACTION;

-- Le FK con CASCADE eliminano automaticamente:
--   enrollments, lesson_progress, quiz_attempts, password_reset_tokens, user_areas
-- Le FK con SET NULL impostano a NULL:
--   courses.created_by, materials.owner_id, quizzes.created_by,
--   lesson_materials.added_by

DELETE FROM users WHERE id = <USER_ID>;

COMMIT;
```

### Verifica post-eliminazione

```sql
SELECT COUNT(*) FROM users WHERE id = <USER_ID>;              -- deve essere 0
SELECT COUNT(*) FROM enrollments WHERE user_id = <USER_ID>;   -- deve essere 0
SELECT COUNT(*) FROM quiz_attempts WHERE user_id = <USER_ID>; -- deve essere 0
SELECT COUNT(*) FROM material_versions WHERE uploaded_by = <USER_ID>; -- deve essere 0
```

---

## Step 5 — Gestione dei log

I log `[APP-AUDIT]` e `[HTTP-ACCESS]` possono contenere l'indirizzo e-mail e l'IP dell'utente. Questi log sono conservati su file di sistema (non nel database MySQL).

**Procedura per i log:**
1. Identificare i file di log che coprono il periodo di attività dell'utente.
2. Se tecnicamente possibile e non in conflitto con obblighi di sicurezza, applicare una pseudonimizzazione (sostituzione dell'e-mail con `[CANCELLATO_<USER_ID>]`) tramite `sed` o strumenti equivalenti.
3. Documentare l'operazione nel registro dei trattamenti.

> Nota: la modifica dei log di audit potrebbe compromettere l'integrità della catena probatoria. Consultare il DPO prima di modificare i file di log.

---

## Step 6 — Gestione dei file materiali sul filesystem

I file caricati nei materiali (`material_versions.file_path`) risiedono sul filesystem del server. Se nell'Step 3b si è scelto di eliminare le versioni, eliminare anche i file fisici:

```sql
-- Recuperare i percorsi file prima dell'eliminazione (eseguire nello Step 3b)
SELECT file_path, file_name FROM material_versions WHERE uploaded_by = <USER_ID>;
```

Eliminare i file fisici solo dopo aver verificato col DPO se i contenuti hanno rilevanza accademica indipendente dall'autore.

---

## Step 7 — Documentazione dell'operazione

Inserire nel registro dei trattamenti:

| Campo                     | Valore da registrare                                              |
|---------------------------|-------------------------------------------------------------------|
| Data richiesta            | Data in cui l'interessato ha inviato la richiesta                 |
| Data esecuzione           | Data in cui l'operazione è stata completata                       |
| Operatore                 | Nome e ruolo di chi ha eseguito l'operazione                      |
| USER_ID (numerico)        | ID numerico dell'utente (non l'e-mail)                            |
| Tipo operazione           | Anonimizzazione / Eliminazione completa                           |
| Gestione corsi docente    | Riassegnati a docente X / Eliminati per CASCADE                   |
| Gestione material_versions| Riassegnate a utente sistema / Eliminate                          |
| Nota DPO                  | Riferimento alla valutazione del DPO se presente                  |
| Esito                     | Completato con successo / Parziale (motivo)                       |

---

## Riepilogo tabelle impattate e meccanismo

| Tabella                  | Azione (anonimizzazione)   | Azione (eliminazione completa) | Meccanismo          |
|--------------------------|----------------------------|-------------------------------|---------------------|
| `users`                  | UPDATE (sovrascrittura)     | DELETE                        | Manuale             |
| `user_areas`             | DELETE                     | DELETE                        | Manuale / CASCADE   |
| `password_reset_tokens`  | DELETE                     | DELETE                        | Manuale / CASCADE   |
| `enrollments`            | Rimangono (dati anonimi)   | DELETE                        | CASCADE             |
| `lesson_progress`        | Rimangono (dati anonimi)   | DELETE                        | CASCADE             |
| `quiz_attempts`          | Rimangono (dati anonimi)   | DELETE                        | CASCADE             |
| `courses` (teacher)      | teacher_id rimane (verif.) | **DELETE per CASCADE** — **valutare prima** | CASCADE |
| `materials.owner_id`     | NULL automatico            | NULL automatico               | SET NULL            |
| `material_versions`      | Riassegnare (RESTRICT)     | Eliminare prima (RESTRICT)    | **Manuale obbligatorio** |
| `lesson_materials.added_by` | NULL automatico          | NULL automatico               | SET NULL            |
| `quizzes.created_by`     | NULL automatico            | NULL automatico               | SET NULL            |
| Log di sistema           | Pseudonimizzazione manuale | Pseudonimizzazione manuale    | Nessun meccanismo automatico |

---

## Glossario

| Termine            | Significato                                                                    |
|--------------------|--------------------------------------------------------------------------------|
| Anonimizzazione    | Sostituzione di dati identificativi con valori non riconducibili all'interessato|
| Pseudonimizzazione | Sostituzione con un identificativo non direttamente riconducibile (es. ID hash) |
| CASCADE            | Eliminazione automatica dei record dipendenti al momento dell'eliminazione del padre |
| SET NULL           | Impostazione automatica a NULL dei riferimenti FK al momento dell'eliminazione del padre |
| RESTRICT           | Comportamento default MySQL: impedisce l'eliminazione del padre se esistono figli con FK NOT NULL |
| DPO                | Data Protection Officer — figura obbligatoria ex art. 37 GDPR                  |
| Registro trattamenti| Documento obbligatorio ex art. 30 GDPR che traccia le operazioni di trattamento|
| Art. 17 GDPR       | Diritto dell'interessato a ottenere la cancellazione dei propri dati personali  |

---

## Validazione documento

| Campo         | Valore                              |
|---------------|-------------------------------------|
| Data          | 2026-04-30                          |
| Approvatore   | _Da compilare — DPO / ICT Bocconi_  |
| Revisione     | Da compilare dopo revisione legale  |
| Versione doc. | Vedere intestazione                 |
