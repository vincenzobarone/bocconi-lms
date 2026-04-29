# Bocconi LMS — User Guide

**Piattaforma:** Università Bocconi — E-Learning Management System  
**Stack:** ASP.NET Core 9 MVC · Razor · Bootstrap 5 · jQuery · MySqlConnector · MySQL  
**Ruoli disponibili:** Admin, Teacher, Student

---

## Indice

1. [Accesso e autenticazione](#1-accesso-e-autenticazione)
2. [Ruoli e permessi](#2-ruoli-e-permessi)
3. [Gestione utenti (Admin)](#3-gestione-utenti-admin)
4. [Gestione corsi](#4-gestione-corsi)
5. [Lezioni](#5-lezioni)
6. [Documenti e video](#6-documenti-e-video)
7. [Quiz](#7-quiz)
8. [Dashboard studente](#8-dashboard-studente)
9. [Dashboard docente](#9-dashboard-docente)
10. [Impostazioni email (Admin)](#10-impostazioni-email-admin)
11. [Traduzioni e lingue (Admin)](#11-traduzioni-e-lingue-admin)
12. [Account e profilo](#12-account-e-profilo)
13. [Struttura tecnica e note per sviluppatori](#13-struttura-tecnica-e-note-per-sviluppatori)

---

## 1. Accesso e autenticazione

### Login
- URL: `/Account/Login`
- Credenziali: email istituzionale + password
- **Admin predefinito:** `admin@bocconi.it` / `Admin@Bocconi2024`

### Password dimenticata
1. Nella pagina di login clicca **"Password dimenticata?"**
2. Inserisci l'email istituzionale
3. Se l'indirizzo è registrato, viene inviato un link di ripristino via email (valido per un tempo limitato)
4. Clicca il link nell'email e imposta la nuova password (minimo 8 caratteri)

### Cambio password
- Dal menu utente in alto a destra → **"Cambia password"**
- Richiede la password attuale + la nuova password (min 8 caratteri, con conferma)

### Sicurezza sessione
- I cookie di sessione scadono alla chiusura del browser
- Gli utenti disattivati non possono effettuare il login (anche se la sessione era già aperta, vengono disconnessi al prossimo controllo)

---

## 2. Ruoli e permessi

| Funzione | Admin | Teacher | Student |
|---|:---:|:---:|:---:|
| Gestire utenti | ✅ | — | — |
| Creare/modificare qualsiasi corso | ✅ | — | — |
| Assegnare docenti ai corsi | ✅ | — | — |
| Creare/modificare i propri corsi | ✅ | ✅ | — |
| Aggiungere lezioni | ✅ | ✅ | — |
| Caricare documenti/video | ✅ | ✅ | — |
| Creare quiz | ✅ | ✅ | — |
| Iscriversi ai corsi | — | — | ✅ |
| Studiare lezioni e sostenere quiz | — | — | ✅ |
| Gestire traduzioni | ✅ | — | — |
| Configurare email SMTP | ✅ | — | — |
| Selezionare lingue attive | ✅ | — | — |

**Regole sui ruoli:**
- Il ruolo **Admin** non può essere assegnato tramite l'interfaccia (è unico e non modificabile)
- Un **Teacher** non può cambiare ruolo se ha corsi attivi
- Uno **Student** non può cambiare ruolo se è iscritto a dei corsi
- Alla creazione di un nuovo utente, il ruolo Admin non è disponibile nel menu a tendina

### Ruoli personalizzati

Oltre ai tre ruoli di sistema (Admin, Teacher, Student) è possibile creare **ruoli personalizzati** con permessi granulari.

Percorso: **Admin → Utenti → tab "Ruoli"**

**Creazione ruolo:**
- Pulsante **"+ Create role"** — apre una pagina dedicata con form completo
- Campi: nome del ruolo (obbligatorio, max 50 caratteri, solo lettere/numeri/underscore/spazi)
- Il nome **Admin** è riservato e non può essere usato
- **Permessi configurabili per ogni ruolo:**
  - *Corsi* (se il modulo Corsi è abilitato): accesso al catalogo, iscrizione, gestione corsi docente, ecc.
  - *Accesso Menu — Materiali*: abilita l'accesso alla sezione Materiali con controllo granulare delle operazioni consentite (crea, modifica, approva) e del flag "Consenti modifica stato" (bypassa il blocco automatico bozza/in revisione)
  - *Accesso Menu — Utenti*: visibilità del pannello Utenti
  - *Accesso Menu — Dictionary*: visibilità del pannello Traduzioni

**Modifica ruolo:**
- Pulsante ✏ sulla riga del ruolo — stessa pagina con nome e permessi precompilati

**Eliminazione ruolo:**
- Pulsante 🗑 — non è possibile eliminare un ruolo con utenti assegnati

---

## 3. Gestione utenti (Admin)

Percorso: **Admin → Utenti**

### Lista utenti
La tabella mostra tutti gli utenti con: nome, email, ruolo, stato (Attivo/Inattivo), data di registrazione.

### Azioni disponibili per utente

| Icona | Azione | Descrizione |
|---|---|---|
| 👁 (occhio) | Vedi corsi | Per i **docenti**: lista dei corsi tenuti. Per gli **studenti**: corsi iscritti con barra di progresso (lezioni completate / totale). |
| ✏ (matita) | Modifica | Cambia nome, email, ruolo e stato account. Il cambio ruolo è bloccato se il docente ha corsi attivi o lo studente è iscritto a corsi. |
| 🔄 (freccia) | Attiva/Disattiva | Cambia lo stato dell'account. Gli utenti inattivi non possono accedere alla piattaforma. |
| 🗑 (cestino) | Elimina | Eliminazione permanente dell'utente e di tutti i dati correlati: tentativi quiz, progressi nelle lezioni, iscrizioni. **Azione irreversibile.** |

### Creazione utente
- Pulsante **"Nuovo utente"** in alto a destra
- Campi: nome, cognome, email, ruolo (Teacher o Student), password temporanea, stato iniziale
- Il campo Admin non è disponibile per sicurezza

### Blocco eliminazione docente
Se un docente ha corsi attivi, il sistema blocca l'eliminazione e mostra un messaggio con il numero di corsi. Il docente va prima riassegnato o i corsi vanno eliminati.

---

## 4. Gestione corsi

### Creazione corso (Teacher / Admin)

Percorso: **Dashboard → Crea Corso**

Campi del form:
- **Titolo** — obbligatorio
- **Descrizione** — testo libero
- **Categoria** — lista a tendina: Economia, Finanza, Informatica, Lingue, Diritto, Management, Marketing, Statistica, Altro
- **Data inizio / Data fine** — opzionali
- **Pubblica subito** — se spuntato, il corso è visibile agli studenti; altrimenti rimane in stato Bozza
- **Docente** _(solo Admin)_ — menu a tendina con tutti i docenti attivi; consente di assegnare o riassegnare il corso a qualsiasi docente

### Modifica corso
- Dal dettaglio corso → pulsante **"Modifica"**
- L'Admin può riassegnare il corso a un docente diverso senza perdere dati

### Stato corso
- **Bozza** — visibile solo al docente/admin; non appare nel catalogo studenti
- **Pubblicato** — visibile a tutti gli studenti nel catalogo

### Eliminazione corso
L'eliminazione è **a cascata**: vengono eliminati definitivamente lezioni, quiz, documenti (inclusi i file fisici su disco), iscrizioni e progressi degli studenti. Un avviso mostra il numero di documenti che verranno eliminati prima della conferma.

### Catalogo corsi (Student)
- Percorso: menu **Corsi**
- Mostra tutti i corsi pubblicati con titolo, categoria, docente, numero di lezioni e iscritti
- Campo di ricerca in tempo reale per filtrare per titolo

### Iscrizione (Student)
- Dal dettaglio corso → pulsante **"Iscriviti"**
- Possibile disiscriversi dal pulsante **"Annulla iscrizione"** nella stessa pagina
- Per visualizzare il dettaglio senza iscriversi occorre comunque essere autenticati

---

## 5. Lezioni

### Aggiunta lezione (Teacher / Admin)
- Dal dettaglio corso → pulsante **"Aggiungi lezione"**
- Campi: Titolo, Ordine (numero intero per ordinare le lezioni), Contenuto (testo libero, può contenere HTML), Pubblica (visibile agli studenti)

### Ordine lezioni
Le lezioni vengono visualizzate nell'ordine numerico del campo **Ordine**. È possibile riordinare modificando questo valore.

### Completamento lezione (Student)
- Aprire la pagina della lezione conta come "completata"
- Il progresso viene registrato e mostrato nella dashboard e nella barra di avanzamento del corso

---

## 6. Documenti e video

### Caricamento (Teacher / Admin)
- Dal dettaglio lezione → pulsante **"Carica documento"**
- Formati supportati:
  - **Documenti:** PDF, Word (.doc/.docx), PowerPoint (.ppt/.pptx), Excel (.xls/.xlsx), TXT — max **50 MB**
  - **Video:** MP4, WebM, MOV, AVI, MKV — max **500 MB**

### Versioning
- Ogni nuovo caricamento sullo stesso documento crea una **nuova versione**
- Le versioni precedenti sono conservate sul disco e accessibili dal pulsante **"Versioni"**
- Dal pannello versioni è possibile **ripristinare** una versione precedente (diventa la versione attiva)
- Eliminando il documento si eliminano **tutti i file di tutte le versioni** dal disco

### Visualizzazione (Student)
- **Documenti:** pulsante di download
- **Video:** player HTML5 integrato nella pagina, senza necessità di download

### Icone nella lista
- 📄 File generico (documenti)
- ▶ Video (file video con player inline)

---

## 7. Quiz

### Creazione quiz (Teacher / Admin)
- Dal dettaglio lezione → pulsante **"Crea quiz"**
- Campi impostazioni:
  - **Titolo**
  - **Descrizione** (opzionale)
  - **Limite di tempo** (minuti; 0 = nessun limite)
  - **Punteggio minimo** (%) per il superamento

### Aggiunta domande
- Pulsante **"Aggiungi domanda"** per aggiungere domande a scelta multipla
- Ogni domanda ha:
  - Testo della domanda
  - 2 o più opzioni di risposta
  - Un cerchio radio per contrassegnare la risposta corretta

### Eliminazione quiz
Eliminando un quiz vengono eliminati definitivamente **tutti i tentativi degli studenti** per quel quiz.

### Svolgimento quiz (Student)
1. Dal dettaglio lezione → pulsante **"Avvia"** accanto al quiz
2. Il countdown inizia immediatamente se è impostato un limite di tempo
3. Selezionare una risposta per ogni domanda
4. Pulsante **"Consegna"** (con conferma) per inviare le risposte
5. Al termine viene mostrato il punteggio e se il quiz è stato superato

### Cronologia tentativi (Student)
- Pulsante **"Cronologia"** nella pagina del quiz
- Mostra tutti i tentativi precedenti con data, punteggio e risultato (Superato / Non superato)
- È possibile ripetere il quiz cliccando **"Riprova"**

---

## 8. Dashboard studente

Percorso: **Dashboard** (per utenti Student)

Mostra (se i rispettivi moduli sono abilitati):
- **Statistiche corsi:** numero di corsi iscritti e lezioni completate
- **Statistiche materiali:** totale materiali disponibili e aggiunti negli ultimi 30 giorni

Per navigare tra le sezioni usare la barra di navigazione in alto.

---

## 9. Dashboard docente

Percorso: **Dashboard** (per utenti Teacher)

Mostra (se i rispettivi moduli sono abilitati):
- **Statistiche corsi:** numero di corsi creati e studenti iscritti
- **Statistiche materiali:** totale materiali e aggiunti negli ultimi 30 giorni

Per navigare tra le sezioni usare la barra di navigazione in alto.

---

## 10. Impostazioni email (Admin)

Percorso: **Admin → Email Settings**

### Configurazione SMTP
Campi:
- **Host SMTP** (es. `smtp.gmail.com`)
- **Porta** (tipicamente 465 per SSL, 587 per STARTTLS)
- **Usa SSL** — attiva SSL su porta 465; disattivare per usare STARTTLS su porta 587
- **Nome utente** — account email per l'autenticazione SMTP
- **Password** — lasciare vuoto per mantenere la password già salvata
- **Email mittente** — indirizzo che appare nel campo "Da:" delle email
- **Nome mittente** — nome visualizzato nel campo "Da:"
- **Abilita invio email** — se disattivato, le email vengono solo registrate nel log ma non inviate (utile in sviluppo/test)

### Notifiche corsi

Sezione **"Courses Notifications"** nella stessa pagina di Email Settings.

Attiva/disattiva selettivamente le email automatiche legate ai corsi:

| Impostazione | Destinatario | Evento |
|---|---|---|
| Student on enroll | Studente | Conferma iscrizione a un corso |
| Student on quiz completed | Studente | Ricezione risultato dopo il completamento di un quiz |
| Teacher on quiz completed | Docente | Notifica quando uno studente completa un quiz nel proprio corso |
| Teacher on student enrolled | Docente | Notifica quando uno studente si iscrive al proprio corso |

Le notifiche vengono inviate **solo se** l'invio email è abilitato nelle impostazioni SMTP e la singola opzione è spuntata.

### Test email
- Sezione separata nella stessa pagina
- Inserire un indirizzo destinatario e cliccare **"Invia email di test"**
- Vengono usate le impostazioni attualmente salvate nel DB

### Note tecniche
Le impostazioni salvate nel DB sovrascrivono quelle in `appsettings.json`. Il servizio di reminder lezioni (`LessonReminderHostedService`) usa queste impostazioni per inviare notifiche automatiche agli studenti.

---

## 11. Traduzioni e lingue (Admin)

Percorso: **Admin → Translations**

### Lingue supportate
La piattaforma supporta 4 lingue:
- 🇬🇧 **English** — lingua base, sempre attiva e non disabilitabile
- 🇮🇹 **Italiano**
- 🇪🇸 **Español**
- 🇩🇪 **Deutsch**

### Selezione lingue attive
- Pannello **"Active Languages"** in cima alla pagina
- Ogni lingua mostra un badge giallo con il numero di traduzioni mancanti
- Spuntare/togliere le lingue desiderate e cliccare **"Salva impostazioni lingua"**
- L'impostazione viene salvata nel DB (tabella `app_settings`, chiave `Languages:Enabled`)
- Effetti immediati:
  - Il **selettore lingua** nella navbar mostra solo le lingue abilitate; scompare se è attiva solo l'inglese
  - Le **colonne** nella tabella traduzioni mostrano solo le lingue abilitate
  - Se un utente ha un cookie per una lingua poi disabilitata, viene automaticamente riportato all'inglese

### Tabella traduzioni
Colonne:
- **Chiave** — identificatore univoco del testo (es. `nav.courses`, `quiz.submit`)
- **English** — valore base sempre presente
- Colonne per le lingue abilitate (IT, ES, DE) — mostra il valore o il badge "Mancante" in giallo se non ancora tradotto
- **Creata il** — data di prima creazione della chiave nel DB (formato dd/MM/yyyy)
- Azioni: ✏ modifica, 🗑 elimina

### Modifica traduzione
- Pulsante matita → form con i campi per ogni lingua abilitata
- Il campo EN è in sola lettura (è la base)
- Salvare aggiorna solo le lingue modificate

### Filtro chiavi mancanti
La barra informativa mostra il numero totale di voci mancanti nelle lingue attive. Se > 0, appare il pulsante **"Mostra solo mancanti"** (icona imbuto): filtra la tabella mostrando solo le righe con almeno una traduzione assente. Cliccarlo di nuovo rimuove il filtro. Le traduzioni mancanti vanno inserite manualmente tramite il pulsante ✏ Modifica.

### Aggiunta automatica chiavi
Quando il codice utilizza `T["nuova.chiave", "Default EN"]` e la chiave non esiste nel DB, viene creata automaticamente con il valore di default inglese al primo caricamento della pagina. Le traduzioni per le altre lingue rimangono "Mancante" finché non vengono aggiunte manualmente tramite il pulsante ✏ Modifica.

### Eliminazione chiave
Il pulsante 🗑 elimina la chiave e **tutte** le sue traduzioni in tutte le lingue. Azione irreversibile.

---

## 12. Account e profilo

Percorso: Menu utente in alto a destra → **"Profilo"** o **"Cambia password"**

### Modifica profilo
- Campi modificabili: Nome, Cognome, Email
- L'email deve essere unica sulla piattaforma

### Cambio password
- Richiede la password attuale per sicurezza
- Nuova password: minimo 8 caratteri, con campo di conferma

### Selettore lingua
- Visibile nella navbar in alto a destra (es. 🇬🇧 EN ▼)
- Mostra solo le lingue abilitate dall'Admin
- Non visibile se è abilitata solo l'inglese
- La preferenza viene salvata in un cookie di sessione

---

## 13. Struttura tecnica e note per sviluppatori

### Stack
- **Framework:** ASP.NET Core 9 MVC con Razor Pages
- **Frontend:** Bootstrap 5, jQuery, Bootstrap Icons, DataTables (per le griglie admin)
- **DB:** MySQL (Kamatera), accesso via `MySqlConnector` (NO Entity Framework)
- **IDE target:** Visual Studio 2026

### Struttura progetto
```
artifacts/bocconi-lms/
├── Controllers/
│   ├── AdminController.cs      # Gestione utenti, traduzioni, email, lingue
│   ├── CourseController.cs     # CRUD corsi, lezioni, studenti
│   ├── DocumentController.cs  # Upload, versioning, eliminazione documenti
│   ├── QuizController.cs      # CRUD quiz, svolgimento, cronologia
│   └── AccountController.cs   # Login, logout, profilo, reset password
├── Data/
│   ├── DbHelper.cs            # Factory connessioni MySQL
│   ├── TranslationRepository.cs
│   ├── SettingsRepository.cs  # app_settings (email, lingue abilitate)
│   ├── UserRepository.cs
│   ├── CourseRepository.cs
│   └── ...
├── Services/
│   ├── TranslationService.cs  # Cache 10 min, lingua corrente, EnabledLanguages
│   ├── EmailService.cs        # Invio SMTP
│   └── LessonReminderHostedService.cs
├── Views/
│   ├── Admin/
│   │   ├── Users.cshtml
│   │   ├── Translations.cshtml
│   │   └── EmailSettings.cshtml
│   ├── Course/
│   ├── Lesson/
│   ├── Quiz/
│   └── Shared/_Layout.cshtml
├── Program.cs                  # DI, Kestrel (500 MB upload), startup migrations
└── schema.sql                  # DDL puro (niente seed — traduzioni gestite via Admin UI)
```

### Sistema di traduzione
- `T["chiave", "Valore EN di default"]` — sintassi in tutte le view Razor
- `TranslationService` è iniettato via DI; la cache viene invalidata automaticamente quando si salvano le traduzioni dall'Admin
- Le chiavi vengono create automaticamente nel DB al primo utilizzo
- Le lingue abilitate sono cachate per 10 minuti (invalidate on save)

### Migrazioni DB
Le migrazioni incrementali vengono applicate all'avvio in `Program.cs` dentro blocchi `try/catch`. Schema completo in `schema.sql` (da applicare manualmente su un DB nuovo).

### Upload file
- Limite Kestrel: 500 MB (`MaxRequestBodySize`)
- `FormOptions.MultipartBodyLengthLimit`: 500 MB
- I file vengono salvati in `wwwroot/uploads/` con nome `{guid}_{filename}`
- Eliminando un documento o ripristinando una versione, i file fisici delle versioni non più attive vengono mantenuti su disco; eliminando il documento tutti i file vengono rimossi

### Modal di conferma globale
Pattern usato in tutto il progetto: `data-confirm="messaggio"` su qualsiasi elemento cliccabile. L'handler in `_Layout.cshtml` intercetta il click, mostra il modal Bootstrap e procede solo se confermato.

### Autenticazione
- Cookie-based con `AddCookie` in `Program.cs`
- Claims: `ClaimTypes.NameIdentifier` (userId), `ClaimTypes.Name` (email), `ClaimTypes.Role`, `"FullName"`
- Middleware `[Authorize]` e `[Authorize(Roles = "Admin")]` sui controller

### GitHub
- Repository: `vincenzobarone/bocconi-lms` (branch `main` ← `bocconi-lms-export`)
- Push: `bash push-lms-to-github.sh`
- Pull in Visual Studio: `git pull origin main`
