# Informativa Privacy (Registro Trattamenti) — Didasco LMS (Università Bocconi)

Versione: 1.0 — aggiornata al 2026-04-30  
Base normativa: Regolamento UE 2016/679 (GDPR) — art. 30 (Registro dei trattamenti)

---

## Titolare del trattamento

**Università Commerciale Luigi Bocconi**  
Via Sarfatti 25, 20136 Milano, Italy  
Posta: privacy@unibocconi.it  
DPO: dpo@unibocconi.it

---

## Responsabile del trattamento (sistema)

Didasco LMS — piattaforma e-learning di Ateneo  
Ambiente di esecuzione: infrastruttura ICT Bocconi (on-premise o cloud privato Ateneo)

---

## Categorie di dati trattati

### Categoria 1 — Dati identificativi utente

| Dato              | Tabella/Colonna               | Base giuridica GDPR | Finalità                                               |
|-------------------|-------------------------------|---------------------|--------------------------------------------------------|
| Indirizzo e-mail  | `users.email`                 | Art. 6(1)(b) — contratto | Accesso al sistema, identificazione univoca, notifiche |
| Nome              | `users.first_name`            | Art. 6(1)(b) — contratto | Personalizzazione UI, elenco utenti                    |
| Cognome           | `users.last_name`             | Art. 6(1)(b) — contratto | Personalizzazione UI, elenco utenti                    |
| Hash password     | `users.password_hash`         | Art. 6(1)(b) — contratto | Autenticazione (BCrypt, password in chiaro non salvata)|
| Ruolo             | `users.role`                  | Art. 6(1)(b) — contratto | Controllo accessi e autorizzazioni                     |
| Stato account     | `users.is_active`             | Art. 6(1)(f) — interesse legittimo | Gestione account attivi/disattivati         |
| Data creazione    | `users.created_at`            | Art. 6(1)(f) — interesse legittimo | Audit trail creazione account               |
| Creato da         | `users.created_by`            | Art. 6(1)(f) — interesse legittimo | Tracciabilità amministrativa                |

---

### Categoria 2 — Dati di apprendimento e progressione

| Dato                        | Tabella/Colonna                | Base giuridica GDPR         | Finalità                                          |
|-----------------------------|--------------------------------|-----------------------------|---------------------------------------------------|
| Iscrizioni a corsi          | `enrollments`                  | Art. 6(1)(b) — contratto    | Erogazione servizio didattico                     |
| Completamento lezioni       | `lesson_progress`              | Art. 6(1)(b) — contratto    | Calcolo progressione studente                     |
| Tentativi quiz              | `quiz_attempts`                | Art. 6(1)(b) — contratto    | Valutazione apprendimento                         |
| Punteggi quiz               | `quiz_attempts.score`          | Art. 6(1)(b) — contratto    | Certificazione superamento quiz                   |

---

### Categoria 3 — Materiali e autoria

| Dato             | Tabella/Colonna              | Base giuridica GDPR          | Finalità                                     |
|------------------|------------------------------|------------------------------|----------------------------------------------|
| Proprietario materiale | `materials.owner_id`   | Art. 6(1)(b) — contratto     | Attribuzione autoria e controllo accesso     |
| Autore esterno   | `materials.author_name`      | Art. 6(1)(f) — interesse legittimo | Catalogazione bibliografica             |
| Uploader versione| `material_versions.uploaded_by` | Art. 6(1)(b) — contratto  | Tracciabilità versioni documenti             |

---

### Categoria 4 — Dati di sicurezza e accesso

| Dato                     | Tabella/Colonna                     | Base giuridica GDPR          | Finalità                                     |
|--------------------------|-------------------------------------|------------------------------|----------------------------------------------|
| Token reset password     | `password_reset_tokens.token`       | Art. 6(1)(b) — contratto     | Recupero sicuro dell'account                 |
| Scadenza token           | `password_reset_tokens.expires_at`  | Art. 6(1)(b) — contratto     | Invalidazione automatica token               |
| Log audit `[APP-AUDIT]`  | File di log (stdout/sistema)        | Art. 6(1)(f) — interesse legittimo | Sicurezza, responsabilità, audit trail  |
| Log HTTP `[HTTP-ACCESS]` | File di log (stdout/sistema)        | Art. 6(1)(f) — interesse legittimo | Diagnostica, sicurezza perimetrale      |
| Indirizzo IP (log)       | Log audit + HTTP access             | Art. 6(1)(f) — interesse legittimo | Rilevamento accessi anomali             |

---

### Categoria 5 — Dati organizzativi

| Dato             | Tabella/Colonna    | Base giuridica GDPR          | Finalità                                |
|------------------|--------------------|------------------------------|-----------------------------------------|
| Assegnazione area| `user_areas`       | Art. 6(1)(b) — contratto     | Organizzazione utenti per dipartimento  |
| Corso di docente | `courses.teacher_id`| Art. 6(1)(b) — contratto    | Attribuzione responsabilità didattica   |

---

## Categorie speciali di dati (art. 9 GDPR)

L'applicazione **non tratta** categorie speciali di dati personali (dati sulla salute, origine etnica, opinioni politiche, ecc.).

---

## Comunicazione a terzi

I dati personali non vengono comunicati a terze parti. Le uniche eccezioni:
- **Servizio SMTP** (es. Bocconi Exchange): l'indirizzo e-mail viene trasmesso al mail server per l'invio di notifiche. Il mail server è gestito dall'ICT di Ateneo.
- **Obblighi di legge**: comunicazione ad autorità competenti su richiesta motivata.

---

## Trasferimento fuori UE

Nessun dato personale viene trasferito a paesi extra-UE. Tutti i sistemi operano su infrastruttura Bocconi ubicata nell'Unione Europea.

---

## Tempi di conservazione

| Categoria dato                    | Periodo di conservazione    | Motivazione                                        |
|-----------------------------------|-----------------------------|----------------------------------------------------|
| Dati account utente               | Durata del rapporto + 5 anni| Obblighi contrattuali, legali, archivio accademico |
| Dati di progressione/quiz         | Durata corso + 3 anni       | Rendicontazione accademica                         |
| Token reset password              | Scadenza token (es. 24h)    | Eliminati automaticamente dopo l'uso               |
| Log `[APP-AUDIT]`                 | 12 mesi                     | Sicurezza e audit                                  |
| Log `[HTTP-ACCESS]`               | 3 mesi                      | Diagnostica                                        |
| Log `[$HEALTH-CHECK]`             | 1 mese                      | Monitoraggio                                       |

---

## Misure di sicurezza tecniche e organizzative

| Misura                          | Dettaglio                                                               |
|---------------------------------|-------------------------------------------------------------------------|
| Cifratura password              | BCrypt con work factor 11 — la password in chiaro non è mai persistita  |
| Cookie di sessione              | HttpOnly, Secure (HTTPS), SameSite=Lax                                  |
| CSRF protection                 | Token anti-forgery su tutti i form POST                                  |
| Controllo accessi               | RBAC basato su permessi granulari per ruolo                              |
| Audit logging                   | Tutte le operazioni CRUD e autenticazione registrate                    |
| Migrazione DB fail-fast         | Ogni migrazione errata interrompe l'avvio (no stato inconsistente)      |
| Accesso admin segregato         | Pannello Admin accessibile solo al ruolo `Admin`                        |
| HTTPS enforced                  | HSTS abilitato in produzione (non sviluppo)                             |

---

## Diritti degli interessati

Ai sensi degli art. 15–22 GDPR, ogni utente ha diritto a:

| Diritto                  | Come esercitarlo in Didasco LMS                                         |
|--------------------------|-------------------------------------------------------------------------|
| Accesso (art. 15)        | Richiesta all'amministratore di sistema o all'indirizzo privacy@unibocconi.it |
| Rettifica (art. 16)      | L'admin può modificare i dati utente dal pannello `/Admin/EditUser`     |
| Cancellazione (art. 17)  | Procedura documentata in `right-to-erasure.md`                          |
| Limitazione (art. 18)    | Disattivazione account (`users.is_active = 0`) mantenendo i dati        |
| Portabilità (art. 20)    | Export manuale tramite query SQL documentata in `right-to-erasure.md`   |
| Opposizione (art. 21)    | Contattare il DPO: dpo@unibocconi.it                                    |

---

## Glossario

| Termine              | Significato                                                              |
|----------------------|--------------------------------------------------------------------------|
| Titolare             | Soggetto che determina le finalità e i mezzi del trattamento             |
| Responsabile         | Soggetto che tratta i dati per conto del titolare                        |
| Base giuridica       | Fondamento legale che legittima il trattamento ex art. 6 GDPR            |
| Art. 6(1)(b)         | Trattamento necessario per l'esecuzione di un contratto                  |
| Art. 6(1)(f)         | Trattamento necessario per legittimo interesse del titolare              |
| DPO                  | Data Protection Officer — responsabile della protezione dei dati         |
| RBAC                 | Role-Based Access Control — controllo accessi basato sul ruolo           |
| HSTS                 | HTTP Strict Transport Security — impone HTTPS per il dominio             |

---

## Validazione documento

| Campo         | Valore                              |
|---------------|-------------------------------------|
| Data          | 2026-04-30                          |
| Approvatore   | _Da compilare — DPO / ICT Bocconi_  |
| Revisione     | Da compilare dopo revisione legale  |
| Versione doc. | Vedere intestazione                 |
