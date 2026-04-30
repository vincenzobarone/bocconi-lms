# Dipendenze — Didasco LMS (Università Bocconi)

Versione: 1.0 — aggiornata al 2026-04-30

---

## Dipendenze NuGet (back-end .NET 9)

| Pacchetto            | Versione    | Licenza SPDX         | Scopo                                                              |
|----------------------|-------------|----------------------|--------------------------------------------------------------------|
| `BCrypt.Net-Next`    | 4.0.3       | MIT                  | Hashing sicuro delle password (algoritmo BCrypt, work factor 11)  |
| `ClosedXML`          | 0.104.2     | MIT                  | Generazione di file Excel (.xlsx) per export materiali             |
| `MailKit`            | 4.16.0      | MIT                  | Invio e-mail transazionali via SMTP (registrazione, reset pwd…)    |
| `MySqlConnector`     | 2.3.7       | MIT                  | Driver ADO.NET asincrono per MySQL — nessun ORM                    |
| `QuestPDF`           | 2024.12.5   | QuestPDF Community\* | Generazione di PDF (export materiali, report)                      |

\* **QuestPDF Community License**: gratuita per progetti non commerciali o con fatturato annuo < 1 M USD. Verificare la conformità con Università Bocconi prima del deploy in produzione.

---

## Framework e runtime (inclusi nel progetto)

| Componente                                  | Versione  | Licenza SPDX | Scopo                                           |
|---------------------------------------------|-----------|--------------|-------------------------------------------------|
| .NET / ASP.NET Core                         | 9.0       | MIT          | Runtime, MVC, Identity, middleware pipeline     |
| ASP.NET Core Identity (`Microsoft.AspNetCore.Identity`) | 9.0 | MIT | Gestione autenticazione e sessioni utente  |
| `Microsoft.Extensions.HealthChecks`         | 9.0       | MIT          | Endpoint `/health` con check database           |
| `Microsoft.Extensions.Logging`              | 9.0       | MIT          | Sistema di logging strutturato (ILogger)        |
| `Microsoft.Extensions.Caching.Memory`       | 9.0       | MIT          | Cache in-memory per traduzioni e feature flags  |

---

## Dipendenze front-end (CDN / bundled)

| Libreria       | Versione | Licenza SPDX | Scopo                                                        |
|----------------|----------|--------------|--------------------------------------------------------------|
| Bootstrap      | 5.x      | MIT          | Layout responsive, componenti UI (navbar, card, modal, …)   |
| jQuery         | 3.x      | MIT          | Manipolazione DOM, AJAX, gestione form                       |
| Bootstrap Icons| 1.x      | MIT          | Set di icone SVG usate nell'interfaccia                      |

Le librerie front-end sono caricate da CDN. In ambienti air-gapped è necessario ospitarle localmente in `wwwroot/lib/`.

---

## Strumenti di sviluppo e testing

| Strumento / Pacchetto                        | Versione  | Licenza SPDX | Scopo                                                  |
|----------------------------------------------|-----------|--------------|--------------------------------------------------------|
| `Microsoft.AspNetCore.Mvc.Testing`           | 9.0       | MIT          | WebApplicationFactory per test d'integrazione HTTP     |
| `xUnit`                                      | 2.x       | Apache-2.0   | Framework di test unitari e d'integrazione             |
| `xUnit.runner.visualstudio`                  | 2.x       | Apache-2.0   | Runner xUnit per `dotnet test`                         |
| `MySqlConnector` (anche in test)             | 2.3.7     | MIT          | Connessione al DB di test nell'helper `DbTestHelper`   |

---

## Note di conformità licenze

1. Tutte le dipendenze principali sono rilasciate sotto licenza **MIT**, compatibile con uso accademico e commerciale senza restrizioni di distribuzione.
2. **QuestPDF Community License** richiede verifica: contattare il fornitore o valutare l'upgrade alla licenza Professional se il progetto è classificato come commerciale.
3. Le librerie front-end (Bootstrap, jQuery) usate via CDN non richiedono attribuzione esplicita nell'UI ma devono essere incluse in ogni SBOM (Software Bill of Materials) consegnato a Bocconi ICT.

---

## SBOM (Software Bill of Materials) — riepilogo

| # | Pacchetto              | Versione    | Licenza SPDX         | Categoria     |
|---|------------------------|-------------|----------------------|---------------|
| 1 | BCrypt.Net-Next        | 4.0.3       | MIT                  | Back-end NuGet|
| 2 | ClosedXML              | 0.104.2     | MIT                  | Back-end NuGet|
| 3 | MailKit                | 4.16.0      | MIT                  | Back-end NuGet|
| 4 | MySqlConnector         | 2.3.7       | MIT                  | Back-end NuGet|
| 5 | QuestPDF               | 2024.12.5   | QuestPDF Community   | Back-end NuGet|
| 6 | .NET 9 / ASP.NET Core  | 9.0         | MIT                  | Runtime       |
| 7 | Bootstrap              | 5.x         | MIT                  | Front-end     |
| 8 | jQuery                 | 3.x         | MIT                  | Front-end     |
| 9 | Bootstrap Icons        | 1.x         | MIT                  | Front-end     |
|10 | xUnit                  | 2.x         | Apache-2.0           | Test          |
|11 | Microsoft.AspNetCore.Mvc.Testing | 9.0 | MIT             | Test          |
