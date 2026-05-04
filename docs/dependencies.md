# Dipendenze — Didasco LMS (Università Bocconi)

Versione: 1.3 — aggiornata al 2026-05-02  
Versioni pinate alle rispettive dichiarazioni in `BocconiLMS.csproj`, `BocconiLMS.Tests/BocconiLMS.Tests.csproj` e `Views/Shared/_Layout.cshtml`.

---

## Dipendenze NuGet — applicazione principale (`BocconiLMS.csproj`)

| Pacchetto            | Versione    | Licenza SPDX         | Scopo                                                              |
|----------------------|-------------|----------------------|--------------------------------------------------------------------|
| `BCrypt.Net-Next`    | 4.0.3       | MIT                  | Hashing sicuro delle password (algoritmo BCrypt, work factor 11)  |
| `ClosedXML`          | 0.104.2     | MIT                  | Generazione di file Excel (.xlsx) per export materiali             |
| `MailKit`            | 4.16.0      | MIT                  | Invio e-mail transazionali via SMTP (registrazione, reset pwd…)    |
| `MySqlConnector`     | 2.3.7       | MIT                  | Driver ADO.NET asincrono per MySQL — nessun ORM                    |
| `QuestPDF`           | 2025.1.0    | QuestPDF Community\* | Generazione di PDF (export materiali, report)                      |

\* **QuestPDF Community License**: gratuita per progetti non commerciali o con fatturato annuo < 1 M USD. Verificare la conformità con Università Bocconi prima del deploy in produzione.

---

## Framework e runtime (inclusi nel SDK .NET 9)

| Componente                                         | Versione  | Licenza SPDX | Scopo                                           |
|----------------------------------------------------|-----------|--------------|-------------------------------------------------|
| .NET / ASP.NET Core MVC                            | 9.0       | MIT          | Runtime, MVC, routing, middleware pipeline      |
| ASP.NET Core Identity                              | 9.0       | MIT          | Gestione autenticazione e sessioni utente       |
| `Microsoft.Extensions.HealthChecks`               | 9.0       | MIT          | Endpoint `/health` con check database           |
| `Microsoft.Extensions.Logging`                    | 9.0       | MIT          | Sistema di logging strutturato (ILogger)        |
| `Microsoft.Extensions.Caching.Memory`             | 9.0       | MIT          | Cache in-memory per traduzioni e feature flags  |

---

## Dipendenze front-end (CDN — versioni pinate da `_Layout.cshtml`)

| Libreria                          | Versione | Licenza SPDX | CDN                    | Scopo                                                   |
|-----------------------------------|----------|--------------|------------------------|---------------------------------------------------------|
| Bootstrap                         | 5.3.3    | MIT          | cdn.jsdelivr.net       | Layout responsive, componenti UI (navbar, modal, ecc.)  |
| Bootstrap Icons                   | 1.11.3   | MIT          | cdn.jsdelivr.net       | Set di icone SVG usate nell'interfaccia                 |
| jQuery                            | 3.7.1    | MIT          | code.jquery.com        | Manipolazione DOM, AJAX, gestione form                  |
| DataTables                        | 2.0.3    | MIT          | cdn.datatables.net     | Tabelle interattive con paginazione e ricerca           |
| DataTables Bootstrap5 integration | 2.0.3    | MIT          | cdn.datatables.net     | Stile Bootstrap 5 per DataTables                        |
| jquery-validation                 | 1.21.0   | MIT          | cdn.jsdelivr.net       | Validazione lato client dei form                        |
| jquery-validation-unobtrusive     | 4.0.0    | MIT          | cdn.jsdelivr.net       | Integrazione ASP.NET Core con jquery-validation         |

> Le librerie front-end sono caricate da CDN. In ambienti air-gapped è necessario ospitarle localmente in `wwwroot/lib/`.

---

## Dipendenze NuGet — progetto test (`BocconiLMS.Tests/BocconiLMS.Tests.csproj`)

| Pacchetto                          | Versione  | Licenza SPDX  | Scopo                                                  |
|------------------------------------|-----------|---------------|--------------------------------------------------------|
| `Microsoft.NET.Test.Sdk`           | 17.12.0   | MIT           | SDK test runner per `dotnet test`                      |
| `Microsoft.AspNetCore.Mvc.Testing` | 9.0.5     | MIT           | WebApplicationFactory per test d'integrazione HTTP     |
| `xunit`                            | 2.9.2     | Apache-2.0    | Framework di test unitari e d'integrazione             |
| `xunit.runner.visualstudio`        | 2.8.2     | Apache-2.0    | Runner xUnit per Visual Studio e `dotnet test`         |
| `coverlet.collector`               | 6.0.2     | MIT           | Raccolta code coverage durante `dotnet test`           |
| `MySqlConnector`                   | 2.3.7     | MIT           | Connessione al DB di test nell'helper `DbTestHelper`   |
| `BCrypt.Net-Next`                  | 4.0.3     | MIT           | Hash password negli helper di test                     |

---

## Note di conformità licenze

1. Tutte le dipendenze principali sono rilasciate sotto licenza **MIT** o **Apache-2.0**, compatibili con uso accademico e commerciale senza restrizioni di distribuzione.
2. **QuestPDF Community License** richiede verifica: contattare il fornitore o valutare l'upgrade alla licenza Professional se il progetto è classificato come commerciale da Bocconi.
3. Le librerie front-end usate via CDN non richiedono attribuzione esplicita nell'UI ma devono essere incluse in ogni SBOM consegnato a Bocconi ICT.

---

## SBOM (Software Bill of Materials) — riepilogo completo

| # | Pacchetto                          | Versione    | Licenza SPDX         | Categoria       |
|---|------------------------------------|-------------|----------------------|-----------------|
| 1 | BCrypt.Net-Next                    | 4.0.3       | MIT                  | NuGet app       |
| 2 | ClosedXML                          | 0.104.2     | MIT                  | NuGet app       |
| 3 | MailKit                            | 4.16.0      | MIT                  | NuGet app       |
| 4 | MySqlConnector                     | 2.3.7       | MIT                  | NuGet app       |
| 5 | QuestPDF                           | 2025.1.0    | QuestPDF Community   | NuGet app       |
| 6 | .NET 9 / ASP.NET Core              | 9.0         | MIT                  | Runtime         |
| 7 | Bootstrap                          | 5.3.3       | MIT                  | Front-end CDN   |
| 8 | Bootstrap Icons                    | 1.11.3      | MIT                  | Front-end CDN   |
| 9 | jQuery                             | 3.7.1       | MIT                  | Front-end CDN   |
|10 | DataTables                         | 2.0.3       | MIT                  | Front-end CDN   |
|11 | DataTables Bootstrap5 integration  | 2.0.3       | MIT                  | Front-end CDN   |
|12 | jquery-validation                  | 1.21.0      | MIT                  | Front-end CDN   |
|13 | jquery-validation-unobtrusive      | 4.0.0       | MIT                  | Front-end CDN   |
|14 | Microsoft.NET.Test.Sdk             | 17.12.0     | MIT                  | NuGet test      |
|15 | Microsoft.AspNetCore.Mvc.Testing   | 9.0.5       | MIT                  | NuGet test      |
|16 | xunit                              | 2.9.2       | Apache-2.0           | NuGet test      |
|17 | xunit.runner.visualstudio          | 2.8.2       | Apache-2.0           | NuGet test      |
|18 | coverlet.collector                 | 6.0.2       | MIT                  | NuGet test      |

---

## Validazione documento

| Campo         | Valore                              |
|---------------|-------------------------------------|
| Data          | 2026-04-30                          |
| Approvatore   | _Da compilare — DPO / ICT Bocconi_  |
| Revisione     | Da compilare dopo revisione legale  |
| Versione doc. | Vedere intestazione                 |
