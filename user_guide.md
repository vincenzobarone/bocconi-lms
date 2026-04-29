# Bocconi LMS — User Guide

**Piattaforma:** Università Bocconi — E-Learning Management System  
**Stack:** ASP.NET Core 9 MVC · Razor · Bootstrap 5 · jQuery · MySqlConnector · MySQL  
**Ruoli disponibili:** Admin, Teacher, Student

---

## Indice

1. [Accesso e autenticazione](#1-accesso-e-autenticazione)
2. [Ruoli e permessi](#2-ruoli-e-permessi)
3. [Gestione utenti (Admin)](#3-gestione-utenti-admin)
4. [Materiali didattici](#4-materiali-didattici)
5. [Gestione corsi](#5-gestione-corsi)
6. [Lezioni](#6-lezioni)
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
| Libreria Materiali — upload/modifica | ✅ | ✅ | — |
| Libreria Materiali — sfoglia/download | ✅ | ✅ | ✅ |
| Creare/modificare qualsiasi corso | ✅ | — | — |
| Assegnare docenti ai corsi | ✅ | — | — |
| Creare/modificare i propri corsi | ✅ | ✅ | — |
| Aggiungere lezioni | ✅ | ✅ | — |
| Collegare materiali alle lezioni | ✅ | ✅ | — |
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
La tabella mostra tutti gli utenti con: nome, email, ruolo, stato (Attivo/Inattivo), data di registrazione e nome del creatore.

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

## 4. Materiali didattici

Percorso: **Materiali** (voce navbar)

La **Libreria Materiali** è il repository centralizzato di tutti i documenti e i file multimediali della piattaforma. I materiali vengono prima caricati nella libreria, quindi collegati alle singole lezioni dei corsi. Il modulo deve essere abilitato dall'Admin tramite i Feature Flags.

### Chi può fare cosa

| Operazione | Admin | Teacher | Student |
|---|:---:|:---:|:---:|
| Sfogliare e scaricare | ✅ | ✅ | ✅ |
| Caricare nuovo materiale | ✅ | ✅ | — |
| Modificare materiale | ✅ | ✅ | — |
| Cambiare stato | ✅ | solo con permesso | — |
| Eliminare materiale | ✅ | ✅ | — |

Gli studenti vedono un avviso che ricorda loro che per caricamenti o modifiche devono contattare il docente.

### Campi del materiale

| Campo | Note |
|---|---|
| **Autore** | Obbligatorio. Se vuoto, viene estratto automaticamente dai metadati del file (.docx, .pptx, .pdf). |
| **Titolo** | Obbligatorio, univoco nell'intera libreria. |
| **Tipo documento** | Lista configurabile dall'Admin (Admin → Dictionary → tab "Tipi documento"). |
| **Lingua** | Italiano, English, Français, Español, Deutsch, Altro. |
| **Stato** | `bozza` / `in_revisione` / `verificato` — vedi workflow sotto. |
| **Cartella** | Organizzazione logica. Digitare un nome esistente (autocomplete) o uno nuovo (la cartella viene creata automaticamente). |
| **Area didattica** | L'Admin vede tutte le aree; il Teacher vede solo le aree assegnate al proprio account. |
| **Data catalogazione** | Data di riferimento del documento (opzionale). |
| **File** | Obbligatorio in creazione; opzionale in modifica (caricarne uno nuovo crea una nuova versione). |
| **Converti in PDF** | Opzionale: converte automaticamente .doc/.docx/.ppt/.pptx in PDF prima del salvataggio (richiede LibreOffice sul server). |
| **Note versione** | Testo libero per descrivere le modifiche della versione. |

### Workflow stato

```
bozza  →  in_revisione  →  verificato
```

- **bozza** — stato iniziale; il materiale non è ancora pronto per la distribuzione.
  - Se l'utente non possiede il permesso `setstatus`, il campo stato è bloccato su _bozza_ in creazione.
- **in_revisione** — materiale inviato alla revisione.
- **verificato** — materiale approvato e pronto all'uso.
  - Richiede obbligatoriamente l'assegnazione a una **cartella**.
  - Viene assegnato automaticamente un **numero protocollo** progressivo univoco.
  - Se l'utente non possiede il permesso `setstatus`, lo stato non può essere modificato in modifica.

Il permesso `setstatus` per creazione, modifica e approvazione si configura separatamente nei ruoli personalizzati.

### Versioning

Ogni materiale mantiene uno storico completo delle versioni del file.

- **Caricamento nuova versione:** in pagina Edit, caricare un nuovo file crea automaticamente una nuova versione (v2, v3…). La versione caricata diventa quella attiva.
- **Upload da Details:** nella pagina di dettaglio è presente un riquadro "Carica nuova versione" con campo note.
- **Ripristina versione:** pulsante ↩ accanto a ogni versione precedente — la versione scelta diventa la versione attiva.
- **Elimina versione:** pulsante 🗑 accanto a ogni versione — non è possibile eliminare l'unica versione rimasta (per farlo, eliminare il materiale).
- **Elimina materiale:** rimuove il record e tutti i file fisici di tutte le versioni dal disco.

### Download e anteprima

- **Download singolo:** pulsante nella lista o nella pagina di dettaglio per la versione attiva.
- **Download multiplo (ZIP):** nella lista, selezionare più materiali con le checkbox e cliccare **"Scarica selezione"** — viene generato un archivio ZIP con i file attivi.
- **Anteprima inline:** PDF e immagini si aprono direttamente nel browser senza scaricare.

### Filtri nella lista

| Filtro | Tipo |
|---|---|
| Titolo | Testo libero |
| Lingua | Lista a tendina |
| Tipo documento | Lista a tendina |
| Anno catalogazione | Anno (es. 2024) |
| Anno ultima modifica | Anno (es. 2024) |
| Nome cartella | Testo libero |

### Tipi documento (Admin)

Percorso: **Admin → Dictionary → tab "Tipi documento"**

Lista completamente configurabile: aggiungere, rinominare e riordinare i tipi (es. Dispensa, Slide, Articolo, Video, Esercizio).

### Notifiche email sui materiali

Percorso: **Admin → Email Settings → sezione "Material Notifications"**

Quando un materiale viene creato o modificato, il sistema può inviare una notifica automatica ai ruoli configurati (configurazione: abilita notifica + seleziona i ruoli destinatari).

---

## 5. Gestione corsi

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
L'eliminazione è **a cascata**: vengono eliminati definitivamente lezioni, quiz, collegamenti ai materiali (i materiali stessi rimangono nella libreria), iscrizioni e progressi degli studenti. Un avviso mostra il numero di elementi che verranno eliminati prima della conferma.

### Catalogo corsi (Student)
- Percorso: menu **Corsi**
- Mostra tutti i corsi pubblicati con titolo, categoria, docente, numero di lezioni e iscritti
- Campo di ricerca in tempo reale per filtrare per titolo

### Iscrizione (Student)
- Dal dettaglio corso → pulsante **"Iscriviti"**
- Possibile disiscriversi dal pulsante **"Annulla iscrizione"** nella stessa pagina
- Per visualizzare il dettaglio senza iscriversi occorre comunque essere autenticati

---

## 6. Lezioni

### Aggiunta lezione (Teacher / Admin)
- Dal dettaglio corso → pulsante **"Aggiungi lezione"**
- Campi: Titolo, Ordine (numero intero per ordinare le lezioni), Contenuto (testo libero, può contenere HTML), Pubblica (visibile agli studenti)

### Ordine lezioni
Le lezioni vengono visualizzate nell'ordine numerico del campo **Ordine**. È possibile riordinare modificando questo valore.

### Collegamento materiali alla lezione

I file e i documenti visibili agli studenti in una lezione provengono dalla **Libreria Materiali** (§4) — non si caricano direttamente nella lezione.

**Come collegare un materiale:**
1. Aprire il dettaglio della lezione
2. Sezione **"Materiali"** → pulsante **"Aggiungi dalla libreria"**
3. Nel modal di selezione cercare il materiale per titolo e cliccare **"Aggiungi"**
4. Il materiale compare nella sezione con pulsanti: download diretto, link alla scheda dettaglio, rimozione collegamento

**Come rimuovere il collegamento:**
- Pulsante 🗑 accanto al materiale nella sezione lezione — rimuove solo il collegamento; il materiale resta nella libreria

**Nota:** se la libreria è vuota o tutti i materiali sono già collegati a quella lezione, il modal mostra un link per creare un nuovo materiale.

**Cosa vede lo studente:**
- Nella pagina della lezione compare la sezione "Materiali" con tutti i documenti collegati
- Per ogni materiale: nome file, tipo, dimensione e pulsante di download
- I video sono riproducibili inline senza necessità di download

### Completamento lezione (Student)
- Aprire la pagina della lezione conta come "completata"
- Il progresso viene registrato e mostrato nella dashboard e nella barra di avanzamento del corso

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

### Notifiche materiali

Sezione **"Material Notifications"** nella stessa pagina di Email Settings.

- **Abilita notifica materiali** — attiva l'invio di email quando un materiale viene creato o modificato
- **Ruoli destinatari** — lista di ruoli che ricevono la notifica (configurati separatamente)

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
│   ├── AdminController.cs      # Gestione utenti, ruoli, aree, traduzioni, email, lingue
│   ├── MaterialsController.cs  # Libreria materiali: CRUD, versioning, download, link lezioni
│   ├── CourseController.cs     # CRUD corsi, lezioni, studenti
│   ├── LessonController.cs     # Dettaglio lezione, collegamento materiali
│   ├── QuizController.cs       # CRUD quiz, svolgimento, cronologia
│   └── AccountController.cs   # Login, logout, profilo, reset password
├── Data/
│   ├── DbHelper.cs            # Factory connessioni MySQL
│   ├── MaterialRepository.cs  # Query libreria materiali, versioni, cartelle, link lezioni
│   ├── AreaRepository.cs      # Aree didattiche
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
│   ├── Materials/
│   │   ├── Index.cshtml       # Lista con filtri e selezione multipla
│   │   ├── Create.cshtml
│   │   ├── Edit.cshtml
│   │   └── Details.cshtml     # Storico versioni, upload nuova versione
│   ├── Course/
│   ├── Lesson/                # Details.cshtml include sezione collegamento materiali
│   ├── Quiz/
│   └── Shared/_Layout.cshtml
├── Program.cs                  # DI, Kestrel (500 MB upload), startup seeding
└── schema.sql                  # DDL completo (applicare manualmente su DB nuovo)
```

### Sistema di traduzione
- `T["chiave", "Valore EN di default"]` — sintassi in tutte le view Razor
- `TranslationService` è iniettato via DI; la cache viene invalidata automaticamente quando si salvano le traduzioni dall'Admin
- Le chiavi vengono create automaticamente nel DB al primo utilizzo
- Le lingue abilitate sono cachate per 10 minuti (invalidate on save)

### Schema DB
Lo schema completo è in `schema.sql` — da applicare manualmente su un DB nuovo. Le modifiche incrementali allo schema si applicano aggiornando `schema.sql` ed eseguendo l'`ALTER TABLE` direttamente sul DB; non si usano migrazioni runtime.

### Relazione materiali ↔ lezioni
La tabella `lesson_materials` (o equivalente) collega N materiali a N lezioni in modalità N:N. Il materiale esiste indipendentemente dalla lezione; il collegamento può essere aggiunto e rimosso senza toccare il materiale stesso.

### Upload file
- Limite Kestrel: 500 MB (`MaxRequestBodySize`)
- `FormOptions.MultipartBodyLengthLimit`: 500 MB
- I file sono salvati in `wwwroot/uploads/mat_{id}/` con prefisso versione (`v1_nome`, `v2_nome`…)
- Eliminando un materiale si rimuovono tutti i file di tutte le versioni e la cartella `mat_{id}`

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
