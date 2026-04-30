# Cookie — Didasco LMS (Università Bocconi)

Versione: 1.1 — aggiornata al 2026-04-30  
Riferimento: `Program.cs` (ConfigureApplicationCookie, AddSession), ASP.NET Core Identity

---

## Riepilogo cookie prodotti dall'applicazione

| Nome cookie                        | Partito      | Categoria    | Durata             | HttpOnly | Secure | SameSite | Scopo                                                         |
|------------------------------------|--------------|--------------|-------------------|----------|--------|----------|---------------------------------------------------------------|
| `.AspNetCore.Identity.Application` | First-party  | Essenziale   | 8 ore (sliding)   | Sì       | Sì\*  | Lax      | Sessione di autenticazione utente (ASP.NET Identity)          |
| `.AspNetCore.Session`              | First-party  | Essenziale   | 8 ore (idle)      | Sì       | Sì\*  | Lax      | Sessione applicativa (dati temporanei di richiesta)           |
| `.AspNetCore.Antiforgery.*`        | First-party  | Essenziale   | Sessione browser  | Sì       | Sì\*  | Strict   | Token CSRF anti-forgery per protezione form POST              |
| `lang`                             | First-party  | Essenziale   | 1 anno            | No       | Sì\*  | Lax      | Lingua dell'interfaccia utente selezionata (`LanguageController`) |

\* Il flag `Secure` è imposto automaticamente da ASP.NET Core quando l'applicazione è servita via HTTPS (produzione). In sviluppo su HTTP non viene impostato.

> **Classificazione partito:** tutti i cookie sono **first-party** — impostati direttamente dal server `didasco.bocconi.it` (o dominio equivalente). L'applicazione non include script di terze parti che impostano cookie (nessun tracker, nessun CDN con cookie). Le librerie front-end (Bootstrap, jQuery, DataTables) vengono caricate da CDN ma **non impostano cookie**.

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
| Partito          | First-party                                |
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
**Finalità:** Mantiene la sessione server-side per dati temporanei dell'applicazione. La preferenza di lingua è gestita dal cookie `lang` (vedi sezione 4); la sessione è usata per altri dati temporanei di richiesta.

**Configurazione (Program.cs):**
```csharp
options.IdleTimeout = TimeSpan.FromHours(8);
options.Cookie.HttpOnly = true;
options.Cookie.IsEssential = true;
```

| Attributo        | Valore                                                       |
|------------------|--------------------------------------------------------------|
| Nome             | `.AspNetCore.Session`                                        |
| Partito          | First-party                                                  |
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
| Partito          | First-party                                                              |
| Durata           | Sessione browser (eliminato alla chiusura del browser)                   |
| HttpOnly         | Sì                                                                       |
| Secure           | Sì (HTTPS) / No (HTTP dev)                                               |
| SameSite         | Strict                                                                   |
| Contenuto        | Token crittografato; non contiene dati utente                            |
| Dati personali   | No                                                                       |

---

### 4. `lang` — Preferenza lingua interfaccia

**Categoria:** Strettamente necessario / Essenziale  
**Finalità:** Memorizza la lingua dell'interfaccia selezionata dall'utente tramite il selettore di lingua (`LanguageController.Set`). Valori possibili: `it`, `en`, `es`, `de`.

**Codice sorgente (`LanguageController.cs`):**
```csharp
Response.Cookies.Append("lang", lang, new CookieOptions
{
    Expires = DateTimeOffset.UtcNow.AddYears(1),
    HttpOnly = false,
    IsEssential = true,
    SameSite = SameSiteMode.Lax
});
```

| Attributo        | Valore                                                       |
|------------------|--------------------------------------------------------------|
| Nome             | `lang`                                                       |
| Partito          | First-party                                                  |
| Durata           | 1 anno dalla data di impostazione                            |
| HttpOnly         | **No** — accessibile da JavaScript (necessario per eventuale lettura lato client) |
| Secure           | Sì (HTTPS) / No (HTTP dev)                                   |
| SameSite         | Lax                                                          |
| IsEssential      | Sì — non soggetto a consenso cookie                          |
| Contenuto        | Stringa lingua: `it`, `en`, `es`, `de`                       |
| Dati personali   | No — preferenza di presentazione, non identificativa         |

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

---

## Validazione documento

| Campo         | Valore                              |
|---------------|-------------------------------------|
| Data          | 2026-04-30                          |
| Approvatore   | _Da compilare — DPO / ICT Bocconi_  |
| Revisione     | Da compilare dopo revisione legale  |
| Versione doc. | Vedere intestazione                 |
