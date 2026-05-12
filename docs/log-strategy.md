# Strategia di Logging — Didasco

**Versione:** 1.0
**Data:** 2026-05-02
**Riferimento normativo:** Capitolato Bocconi SDA — sezione "Audit Log"

## 1. Requisito Bocconi

Il capitolato Bocconi prescrive testualmente:

> *"I log DEVONO essere inviati a std-out e non salvati su file."*
>
> *"Ciascun log, in base alla propria natura (ad es. log di accesso o log applicativi),
> DEVE avere un tag iniziale con la seguente sintassi: [$TIPO-LOG]."*
>
> *"La configurazione DOVREBBE permettere di impostare dinamicamente il livello di
> dettaglio del tracciato."*

Modello di riferimento Bocconi: container Docker → stdout → infrastruttura di
log aggregation (Azure Monitor / Log Analytics).

---

## 2. Strategia adottata: dual-write configurabile

L'app implementa **due canali di scrittura simultanei**:

### Canale primario — stdout
- **Sempre attivo**, non disattivabile (è il canale "ufficiale" del capitolato).
- Tag conformi alla sintassi richiesta:
  - `[HTTP-ACCESS]` — middleware accessi HTTP (`HttpAccessLogMiddleware`)
  - `[APP-AUDIT]` — eventi audit applicativi (`AuditLogger`)
- Formato: timestamp ISO8601, utente, IP, azione, esito, durata.
- Livello configurabile via `appsettings.json` → `Logging:LogLevel:Default`
  (ridiveribile dinamicamente senza ricompilazione).

### Canale secondario — database (`system_logs`)
- **Default attivo**, disattivabile via configurazione.
- Tabella MySQL dedicata, scritta in **fire-and-forget** (non blocca mai la request).
- Pensato per ambienti **senza infrastruttura di log aggregation centralizzata**.
- Consultabile via interfaccia Admin → "Log di Sistema" (paginata, filtri per tipo/utente/esito/data, purge).

---

## 3. Configurazione

In `appsettings.json` (o tramite variabile d'ambiente `AuditLog__WriteToDatabase`):

```json
"AuditLog": {
  "Enabled": true,            // master switch (true = tutti i log attivi)
  "Level": "standard",        // minimal | standard | verbose
  "WriteToDatabase": true     // ← scrittura su system_logs
}
```

| Scenario | `WriteToDatabase` | Conseguenze |
|---|---|---|
| **Sviluppo / Stage** | `true` (default) | log su stdout **+** consultabili via Admin UI |
| **Produzione Bocconi su Azure** | `false` | solo stdout → Azure Monitor; nessun overhead DB |
| **Audit interno temporaneo** | `true` | si attiva per finestra limitata, poi si disattiva |

### Override via env var (formato Microsoft.Extensions.Configuration):
```bash
export AuditLog__WriteToDatabase=false
```

---

## 4. Perché dual-write?

| Vincolo | Risposta |
|---|---|
| Capitolato richiede stdout | ✅ sempre rispettato (canale primario non disattivabile) |
| In sviluppo non c'è log aggregation centralizzata | ✅ il canale DB fornisce un viewer in-app |
| Su Azure il DB diventa ridondante | ✅ disattivabile con un flag, zero codice da toccare |
| Performance | ✅ scrittura DB fire-and-forget, non blocca le richieste |
| Costo storage | ✅ purge manuale da Admin UI (30/90 giorni o tutto) |

In altri termini: lo standard Bocconi è sempre rispettato, e abbiamo un **piano B
operativo** per le situazioni in cui Azure Monitor non è disponibile.

---

## 5. Considerazioni operative

### 5.1 In sviluppo (senza log aggregation centralizzata)
- Entrambi i canali attivi.
- I log su stdout sono visibili nella console dell'applicazione.
- I log su DB sono visibili in **Admin → Log di Sistema**.

### 5.2 In container Docker (futuro Bocconi)
- I log stdout vengono catturati da Docker → forwarded a Azure Monitor.
- Si **disattiva** `WriteToDatabase` per evitare storage ridondante.
- Il link "Log di Sistema" mostrerà un avviso di funzionalità disabilitata.

### 5.3 Retention
- **stdout**: gestita dall'infrastruttura (Azure Monitor: tipicamente 30 giorni
  configurabili a 90/365 con costo aggiuntivo).
- **DB**: gestita manualmente via Admin UI (purge 30/90 giorni o tutto).

### 5.4 Riservatezza
- Nessun log contiene password, token, o dati personali sensibili (verificato).
- IP e email sono presenti per ricostruire "chi ha operato" come da capitolato.
- In caso di richiesta GDPR (diritto all'oblio), la procedura di anonimizzazione
  utente deve includere anche `system_logs.user_email`.

---

## 6. Mapping requisito ↔ implementazione

| Requisito Bocconi | Implementazione |
|---|---|
| Log su stdout | `ILogger` + `Console` provider (default ASP.NET) |
| Tag `[$TIPO-LOG]` | Costanti `Tag` in `HttpAccessLogMiddleware` e `AuditLogger` |
| Data/ora ISO8601 | `DateTimeOffset.UtcNow.ToString("O")` in `AuditLogger.Write` |
| Utente | Estratto da `ClaimTypes.Email` o `User.Identity.Name` |
| IP | `HttpContext.Connection.RemoteIpAddress` |
| Descrizione operazione | Parametro `action` di `AuditLogger.Log()` |
| Esito | Parametro `outcome` (default `success`) |
| Livello dinamico | `appsettings.json` → `AuditLog:Level` (`minimal`/`standard`/`verbose`) |
| **Non salvati su file** | ✅ Nessun file logger configurato; solo stdout (+ DB opzionale) |

---

## 7. Riepilogo

L'app è **conforme al capitolato Bocconi** (stdout + tag + livello dinamico) e in
parallelo offre un **fallback operativo** (DB + viewer admin) per gli ambienti
in cui non è disponibile l'infrastruttura di log aggregation. Il fallback è
completamente disattivabile con un singolo flag, senza modifiche al codice.
