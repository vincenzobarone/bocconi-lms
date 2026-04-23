# Bocconi LMS

Piattaforma E-Learning dell'Università Bocconi — ASP.NET Core 9 MVC + MySQL.

## Documentazione

| Guida | Destinatari | Contenuto |
|---|---|---|
| [Guida Tecnica](docs/TECHNICAL.md) | Sviluppatori, sistemisti | Setup, architettura, deploy, estensioni, test |
| [Guida Utenti](docs/USER_GUIDE.md) | Studenti, Docenti, Admin | Come usare la piattaforma giorno per giorno |

## Stack tecnologico

- **Backend**: ASP.NET Core 9 MVC (Razor Pages)
- **Frontend**: Bootstrap 5, jQuery, DataTables
- **Database**: MySQL (connettore diretto: MySqlConnector, nessun ORM)
- **Autenticazione**: Cookie authentication ASP.NET Core + BCrypt password hashing

## Setup iniziale

### 1. Database MySQL

Eseguire lo script `schema.sql` sul server MySQL (Kamatera o locale):

```sql
mysql -u UTENTE -p < schema.sql
```

Prima del deploy, generare l'hash della password admin:

```csharp
BCrypt.Net.BCrypt.HashPassword("Admin@Bocconi2024")
```

E aggiornare il record nel DB.

### 2. Variabile d'ambiente

Impostare la connection string MySQL:

```
MYSQL_CONNECTION_STRING=Server=HOST;Port=3306;Database=bocconi_lms;User=UTENTE;Password=PASSWORD;
```

- **In Replit**: impostare come Secret `MYSQL_CONNECTION_STRING`
- **In Visual Studio**: Properties → launchSettings.json o User Secrets
- **In produzione (Kamatera/IIS)**: variabile d'ambiente di sistema o `appsettings.json`

### 3. Avvio in sviluppo

**Replit**: avviare il workflow `bocconi-lms`

**Visual Studio 2022**: aprire `BocconiLMS.csproj`, premere F5

**Dotnet CLI**:
```bash
cd artifacts/bocconi-lms
dotnet run
```

## Struttura progetto

```
BocconiLMS/
├── Controllers/         # Controller MVC
├── Data/                # Repository (query SQL dirette via MySqlConnector)
├── Models/              # Entità e ViewModel
├── Views/               # Razor Views per ogni area
├── wwwroot/
│   ├── css/site.css     # Stili personalizzati Bocconi
│   └── uploads/         # File caricati (non in versioning Git)
├── schema.sql           # Script SQL per creare il database
└── BocconiLMS.csproj    # Progetto apribile in VS2022
```

## Ruoli utente

| Ruolo | Permessi |
|-------|----------|
| **Admin** | Gestione utenti, tutti i corsi, statistiche |
| **Teacher** | Crea corsi, lezioni, documenti, quiz |
| **Student** | Visualizza corsi, si iscrive, risponde ai quiz |

## Funzionalità principali

- **Versioning documenti**: ogni upload crea una nuova versione; possibile visualizzare la cronologia e ripristinare versioni precedenti
- **Quiz interattivi**: con timer, navigazione tra domande, calcolo punteggio automatico
- **Tracking progressi**: le lezioni si segnano automaticamente come completate; progress bar per corso
- **Dashboard per ruolo**: studente, docente e amministratore hanno viste diverse

## Compatibilità Visual Studio 2022

Il progetto è un classico ASP.NET Core MVC, apribile nativamente in VS2022:

1. Clonare/copiare la cartella `artifacts/bocconi-lms/`
2. Aprire `BocconiLMS.csproj` in VS2022
3. Configurare la connection string in `appsettings.json` o User Secrets
4. F5 per avviare
