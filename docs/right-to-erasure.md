# Diritto alla Cancellazione (Right to Erasure) — Didasco LMS (Università Bocconi)

Versione: 1.0 — aggiornata al 2026-04-30  
Base normativa: GDPR art. 17 — Diritto alla cancellazione ("diritto all'oblio")

---

## Panoramica

Il presente documento descrive la procedura operativa per soddisfare una richiesta di cancellazione dei dati personali ai sensi dell'art. 17 GDPR.  
La procedura distingue tra:
- **Anonimizzazione**: i dati vengono sovrascritti con valori non identificabili; il record rimane per integrità referenziale.
- **Eliminazione completa**: il record viene fisicamente rimosso; gli CASCADE garantiscono la pulizia automatica delle tabelle collegate.

> **Avvertenza legale:** Prima di eseguire qualsiasi operazione, verificare con il DPO (`dpo@unibocconi.it`) se esistono obblighi di conservazione prevalenti (es. archivio accademico, contenziosi in corso, obblighi fiscali). In presenza di tali obblighi, sostituire l'eliminazione con la sola anonimizzazione.

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

-- Materiali di proprietà
SELECT id, title, status, created_at
FROM materials WHERE owner_id = <USER_ID>;

-- Versioni materiali caricate
SELECT mv.id, m.title AS material, mv.version_number, mv.file_name, mv.uploaded_at
FROM material_versions mv
JOIN materials m ON m.id = mv.material_id
WHERE mv.uploaded_by = <USER_ID>;
```

---

## Step 3 — Anonimizzare i dati personali dell'utente

Questa query sovrascrive i dati identificativi dell'utente con valori anonimi, mantenendo il record per integrità referenziale (corsi, quiz, iscrizioni rimangono intatti ma slegati dall'identità reale).

```sql
-- Avviare una transazione
START TRANSACTION;

-- 3a. Anonimizzare il profilo utente
UPDATE users
SET
    email         = CONCAT('deleted_', id, '@anonimizzato.invalid'),
    password_hash = '[CANCELLATO]',
    first_name    = '[Cancellato]',
    last_name     = '[Cancellato]',
    is_active     = 0
WHERE id = <USER_ID>;

-- 3b. Revocare le aree organizzative
DELETE FROM user_areas WHERE user_id = <USER_ID>;

-- 3c. Eliminare i token di reset password (dati sensibili)
DELETE FROM password_reset_tokens WHERE user_id = <USER_ID>;

-- 3d. (Opzionale) Anonimizzare il campo author_name nei materiali se corrisponde al nome
--     Solo se il nome è effettivamente il dato dell'interessato
UPDATE materials
SET author_name = '[Cancellato]'
WHERE owner_id = <USER_ID> AND author_name IS NOT NULL;

COMMIT;
```

> Dopo questo step l'account non è più utilizzabile (password invalida, account disattivato) e il nome/e-mail non è più identificabile.

---

## Step 4 — Eliminazione completa (se non ci sono obblighi di conservazione)

Se il DPO conferma che non esistono obblighi di conservazione, procedere con l'eliminazione fisica:

```sql
START TRANSACTION;

-- 4a. Le seguenti tabelle hanno FK con ON DELETE CASCADE da users:
--     enrollments, lesson_progress, quiz_attempts,
--     password_reset_tokens, user_areas
--     → vengono eliminate automaticamente.

-- 4b. Le seguenti tabelle hanno FK con ON DELETE SET NULL da users:
--     courses.teacher_id, materials.owner_id, material_versions.uploaded_by,
--     lesson_materials.added_by, courses.created_by, quizzes.created_by
--     → i riferimenti vengono impostati a NULL automaticamente.

-- Eliminare l'utente (CASCADE gestisce il resto)
DELETE FROM users WHERE id = <USER_ID>;

COMMIT;
```

> Verificare il risultato:
> ```sql
> SELECT COUNT(*) FROM users WHERE id = <USER_ID>;          -- deve essere 0
> SELECT COUNT(*) FROM enrollments WHERE user_id = <USER_ID>; -- deve essere 0
> SELECT COUNT(*) FROM quiz_attempts WHERE user_id = <USER_ID>; -- deve essere 0
> ```

---

## Step 5 — Gestione dei log

I log `[APP-AUDIT]` e `[HTTP-ACCESS]` possono contenere l'indirizzo e-mail e l'IP dell'utente. Questi log sono conservati su file di sistema (non nel database MySQL).

**Procedura per i log:**
1. Identificare i file di log che coprono il periodo di attività dell'utente.
2. Se tecnicamente possibile e non in conflitto con obblighi di sicurezza, applicare una pseudonimizzazione (sostituzione dell'e-mail con `[CANCELLATO_<USER_ID>]`) tramite `sed` o strumenti equivalenti.
3. Documentare l'operazione nel registro dei trattamenti.

> Nota: la modifica dei log di audit potrebbe compromettere l'integrità della catena probatoria. Consultare il DPO prima di modificare i file di log.

---

## Step 6 — Gestione dei file materiali

I file caricati nei materiali (`material_versions.file_path`) risiedono sul filesystem del server. Se l'utente è stato il solo uploader di un materiale che non ha rilevanza accademica autonoma:

```sql
-- Identificare i percorsi file delle versioni dell'utente
SELECT mv.file_path, mv.file_name, m.title
FROM material_versions mv
JOIN materials m ON m.id = mv.material_id
WHERE mv.uploaded_by = <USER_ID>;
```

Eliminare i file fisici identificati **solo dopo** aver valutato col DPO se i contenuti hanno rilevanza accademica indipendente dall'autore.

---

## Step 7 — Documentazione dell'operazione

Inserire nel registro dei trattamenti:

| Campo             | Valore da registrare                                              |
|-------------------|-------------------------------------------------------------------|
| Data richiesta    | Data in cui l'interessato ha inviato la richiesta                 |
| Data esecuzione   | Data in cui l'operazione è stata completata                       |
| Operatore         | Nome e ruolo di chi ha eseguito l'operazione                      |
| USER_ID anonimizzato | ID numerico dell'utente (non l'e-mail)                         |
| Tipo operazione   | Anonimizzazione / Eliminazione completa                           |
| Nota DPO          | Riferimento alla valutazione del DPO se presente                  |
| Esito             | Completato con successo / Parziale (motivo)                       |

---

## Riepilogo tabelle impattate

| Tabella                  | Azione            | Meccanismo                          |
|--------------------------|-------------------|-------------------------------------|
| `users`                  | UPDATE → anonimizza / DELETE | Manuale                  |
| `user_areas`             | DELETE            | CASCADE / Manuale                   |
| `password_reset_tokens`  | DELETE            | CASCADE / Manuale                   |
| `enrollments`            | DELETE            | CASCADE (con DELETE users)          |
| `lesson_progress`        | DELETE            | CASCADE (con DELETE users)          |
| `quiz_attempts`          | DELETE            | CASCADE (con DELETE users)          |
| `materials`              | UPDATE owner_id→NULL / UPDATE author_name | SET NULL / Manuale |
| `material_versions`      | UPDATE uploaded_by→NULL | SET NULL (con DELETE users) |
| `lesson_materials`       | UPDATE added_by→NULL | SET NULL (con DELETE users)      |
| `courses`                | UPDATE teacher_id→NULL / created_by→NULL | SET NULL           |
| `quizzes`                | UPDATE created_by→NULL | SET NULL (con DELETE users)      |
| Log di sistema           | Pseudonimizzazione manuale (file) | Nessun meccanismo automatico |

---

## Glossario

| Termine            | Significato                                                                    |
|--------------------|--------------------------------------------------------------------------------|
| Anonimizzazione    | Sostituzione di dati identificativi con valori non riconducibili all'interessato|
| Pseudonimizzazione | Sostituzione con un identificativo non direttamente riconducibile (es. ID hash) |
| CASCADE            | Eliminazione automatica dei record dipendenti al momento dell'eliminazione del padre |
| SET NULL           | Impostazione automatica a NULL dei riferimenti FK al momento dell'eliminazione del padre |
| DPO                | Data Protection Officer — figura obbligatoria ex art. 37 GDPR                  |
| Registro trattamenti| Documento obbligatorio ex art. 30 GDPR che traccia le operazioni di trattamento|
| Art. 17 GDPR       | Diritto dell'interessato a ottenere la cancellazione dei propri dati personali  |
