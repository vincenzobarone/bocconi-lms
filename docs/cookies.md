# Cookie — Didasco LMS (Università Bocconi)

Versione: 1.0 — aggiornata al 2026-04-30  
Riferimento: `Program.cs` (ConfigureApplicationCookie, AddSession), ASP.NET Core Identity

---

## Riepilogo cookie prodotti dall'applicazione

| Nome cookie                   | Tipo         | Durata             | HttpOnly | Secure | SameSite | Scopo                                                         |
|-------------------------------|--------------|-------------------|----------|--------|----------|---------------------------------------------------------------|
| `.AspNetCore.Identity.Application` | Essenziale | 8 ore (sliding) | Sì | Sì\* | Lax | Sessione di autenticazione utente (ASP.NET Identity)          |
| `.AspNetCore.Session`          | Essenziale   | 8 ore (idle)       | Sì       | Sì\*  | Lax      | Sessione applicativa (lingua UI, dati temporanei)             |
| `.AspNetCore.Antiforgery.*`    | Essenziale   | Sessione browser   | Sì       | Sì\*  | Strict   | Token CSRF anti-forgery per protezione form POST              |

\* Il flag `Secure` è imposto automaticamente da ASP.NET Core quando l'applicazione è servita via HTTPS (produzione). In sviluppo su HTTP non viene impostato.

---

## Dettaglio cookie

### 1. `.AspNetCore.Identity.Application` — Cookie di autenticazione

**Categoria:** Strettamente necessario / Essenziale  
**Finalità:** Mantiene la sessione utente autenticata tra le richieste HTTP. Senza questo cookie l'utente deve effettuare l'accesso a ogni pagina.

**Configurazione (Program.cs):**
```csharp
options.ExpireTimeSpan = TimeSpan.FromHours(8);
options.SlidingExpiration = true;
```

| Attributo        | Valore                                     |
|------------------|--------------------------------------------|
| Nome             | `.AspNetCore.Identity.Application`         |
| Durata           | 8 ore dalla creazione; si rinnova a ogni richiesta (sliding) |
| Scade a logout   | Sì — eliminato da `Account/Logout`         |
| HttpOnly         | Sì — non accessibile da JavaScript         |
| Secure           | Sì (HTTPS) / No (HTTP dev)                 |
| SameSite         | Lax                                        |
| Contenuto        | Token cifrato (ASP.NET Core Data Protection) — non leggibile in chiaro |
| Dati personali   | No — il token è opaco; nessun dato PD è leggibile senza la chiave del server |

---

### 2. `.AspNetCore.Session` — Cookie di sessione applicativa

**Categoria:** Strettamente necessario / Essenziale  
**Finalità:** Mantiene la sessione server-side per dati temporanei dell'applicazione, in particolare la **lingua selezionata dall'utente** nell'interfaccia (`LanguageController`).

**Configurazione (Program.cs):**
```csharp
options.IdleTimeout = TimeSpan.FromHours(8);
options.Cookie.HttpOnly = true;
options.Cookie.IsEssential = true;
```

| Attributo        | Valore                                                       |
|------------------|--------------------------------------------------------------|
| Nome             | `.AspNetCore.Session`                                        |
| Durata           | 8 ore di inattività (idle timeout); si rinnova a ogni richiesta |
| HttpOnly         | Sì                                                           |
| Secure           | Sì (HTTPS) / No (HTTP dev)                                   |
| SameSite         | Lax (default ASP.NET Core)                                   |
| Contenuto        | ID sessione — i dati effettivi risiedono in memoria server   |
| Dati personali   | No — il cookie è solo un identificatore opaco                |

---

### 3. `.AspNetCore.Antiforgery.*` — Token CSRF

**Categoria:** Strettamente necessario / Essenziale  
**Finalità:** Protezione contro attacchi Cross-Site Request Forgery (CSRF). Ogni form POST include un token abbinato a questo cookie. Senza corrispondenza la richiesta viene rifiutata (HTTP 400).

| Attributo        | Valore                                                                   |
|------------------|--------------------------------------------------------------------------|
| Nome             | `.AspNetCore.Antiforgery.<hash>` (hash generato all'avvio)               |
| Durata           | Sessione browser (eliminato alla chiusura del browser)                   |
| HttpOnly         | Sì                                                                       |
| Secure           | Sì (HTTPS) / No (HTTP dev)                                               |
| SameSite         | Strict                                                                   |
| Contenuto        | Token crittografato; non contiene dati utente                            |
| Dati personali   | No                                                                       |

---

## Cookie di terze parti

L'applicazione **non installa cookie di terze parti** (analytics, advertising, tracking).  
Le librerie front-end Bootstrap e jQuery sono caricate da CDN ma non impostano cookie propri.

---

## Consenso cookie

Tutti i cookie prodotti da Didasco LMS rientrano nella categoria **strettamente necessari** (art. 5(3) Direttiva ePrivacy / Recital 25 GDPR): non richiedono consenso esplicito dell'utente in quanto essenziali al funzionamento del servizio richiesto.

Non è necessario implementare un banner di consenso cookie per questi cookie.  
Se in futuro verranno introdotti cookie analitici o di marketing, dovrà essere implementato un meccanismo di consenso conforme al GDPR e al Provvedimento Garante Privacy.

---

## Procedura di eliminazione cookie (utente)

Un utente può eliminare i cookie:
1. Utilizzando la funzione "Cancella dati di navigazione" del browser.
2. Eseguendo il **Logout** dall'applicazione — questa azione elimina il cookie di autenticazione e invalida la sessione lato server.

---

## Glossario

| Termine       | Significato                                                                               |
|---------------|-------------------------------------------------------------------------------------------|
| HttpOnly      | Il cookie non è accessibile tramite JavaScript (protegge da XSS)                         |
| Secure        | Il cookie viene trasmesso solo su connessioni HTTPS                                       |
| SameSite      | Politica di invio cookie in richieste cross-site (Lax = sicuro, Strict = molto restrittivo)|
| Sliding exp.  | La scadenza si rinnova automaticamente a ogni richiesta attiva                            |
| Idle timeout  | La sessione scade se non si effettuano richieste per il periodo indicato                  |
| CSRF          | Cross-Site Request Forgery — attacco che sfrutta la sessione autenticata di un utente     |
| Data Protection| Sistema ASP.NET Core per cifrare e firmare i cookie (chiavi ruotate automaticamente)    |
| Essenziale    | Cookie necessario per l'erogazione del servizio; esente da consenso ex ePrivacy Directive |
