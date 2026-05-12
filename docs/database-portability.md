# Database Portability — MySQL → SQL Server

**Versione:** 1.0
**Data:** 2026-05-02
**Stato:** Documento di riferimento per eventuale migrazione futura

## 1. Contesto

Didasco è attualmente sviluppato e ospitato su **MySQL 8.x** (DB remoto Kamatera `BocconiEdu`).
Bocconi potrebbe in futuro richiedere il deploy su **SQL Server** (on-prem o Azure SQL),
in coerenza con un'eventuale infrastruttura Microsoft preesistente.

Questo documento mappa **tutti i punti del codice MySQL-specific** e indica le azioni
necessarie per una eventuale migrazione, in modo da poter stimare costi e rischi senza
sorprese.

---

## 2. Sintesi esecutiva

| Area | Effort stimato | Rischio |
|---|---|---|
| Driver swap (MySqlConnector → Microsoft.Data.SqlClient) | ~4h | Basso (cambio meccanico) |
| Refactoring `DbHelper` per `IDbConnection` | ~2h | Basso |
| Riscrittura `schema.sql` in T-SQL | ~4h | Basso (DDL noto) |
| Conversione query MySQL-specific (UPSERT, INTERVAL, ecc.) | ~6h | **Medio** (test regressivi su tutti i flussi) |
| Riscrittura `ProductionScriptGenerator` | ~4h | Medio (genera DDL) |
| Adeguamento test suite | ~3h | Basso |
| Test di regressione completo | ~1 giorno | **Alto** (ogni feature) |
| **Totale** | **~3 giorni dev + 1 giorno test** | |

---

## 3. Mappa delle dipendenze MySQL

### 3.1 Driver e tipi (~22 file)

Tutto il data layer usa i tipi concreti `MySqlConnection`, `MySqlCommand`, `MySqlDataReader`, `MySqlParameter`.
File interessati (output di `rg "MySqlConnection|MySqlCommand|..." --type cs -c`):

```
Data/DbHelper.cs                 (7 occorrenze)  ← punto di ingresso
Data/UserRepository.cs           (18)
Data/SettingsRepository.cs       (4)
Data/CourseRepository.cs         (7)
Data/LessonRepository.cs         (6)
Data/QuizRepository.cs           (13)
Data/EnrollmentRepository.cs     (7)
Data/MaterialRepository.cs       (27)
Data/TranslationRepository.cs    (9)
Data/RolePermissionRepository.cs (4)
Data/AreaRepository.cs           (12)
Data/PlatformRepository.cs       (6)
Data/DocumentTypeRepository.cs   (8)
Data/ProgressRepository.cs       (2)
Data/CustomUserStore.cs          (10)
Data/CustomRoleStore.cs          (6)
Data/SystemLogRepository.cs      (7)
Data/ProductionScriptGenerator.cs (4)
Data/MySqlHealthCheck.cs         (1)  ← nome da rinominare
Controllers/HomeController.cs    (2)
Controllers/AccountController.cs (4)
BocconiLMS.Tests/Helpers/DbTestHelper.cs (28)
```

**Strategia consigliata:** introdurre un'astrazione minima in `DbHelper` che restituisca
`IDbConnection` (interfaccia ADO.NET standard). I repository usano già `using var conn = _db.GetConnection()`,
quindi il cambio è isolato a `DbHelper.cs` + un find-and-replace sui tipi `MySqlCommand` → `IDbCommand`
(o, più pragmaticamente, `SqlCommand` se si decide il provider definitivo).

### 3.2 Schema DDL (`schema.sql`)

Tutto MySQL puro. Conversioni necessarie:

| Costrutto MySQL | T-SQL equivalente |
|---|---|
| `INT AUTO_INCREMENT PRIMARY KEY` | `INT IDENTITY(1,1) PRIMARY KEY` |
| `TINYINT(1) NOT NULL DEFAULT 1` | `BIT NOT NULL DEFAULT 1` |
| `DATETIME(3)` | `DATETIME2(3)` |
| `DEFAULT CURRENT_TIMESTAMP(3)` | `DEFAULT SYSDATETIME()` |
| `ENGINE=InnoDB` | rimuovere |
| `CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci` | `COLLATE Latin1_General_100_CI_AS_SC_UTF8` (SQL Server 2019+) |
| `INDEX idx_x (col)` inline nel CREATE TABLE | spostare in `CREATE INDEX` separato |
| `VARCHAR(n)` | preferibilmente `NVARCHAR(n)` per Unicode |
| `MEDIUMTEXT` / `LONGTEXT` | `NVARCHAR(MAX)` |
| `INSERT IGNORE INTO` | `MERGE` o `IF NOT EXISTS (...) INSERT` |

### 3.3 Query MySQL-specific nel codice

#### 3.3.1 `ON DUPLICATE KEY UPDATE` → `MERGE`
Posizioni:
- `Data/TranslationRepository.cs:96, 114` — salvataggio traduzioni (chiamato ad ogni T["key"] mancante)
- `Data/SettingsRepository.cs:63` — upsert delle impostazioni
- `Data/ProductionScriptGenerator.cs:370` — generazione script produzione
- `BocconiLMS.Tests/Helpers/DbTestHelper.cs:35, 247, 376` — fixture di test

Conversione tipica:
```sql
-- MySQL
INSERT INTO settings (k, v) VALUES (@k, @v)
ON DUPLICATE KEY UPDATE v = @v;

-- SQL Server
MERGE settings AS t
USING (SELECT @k AS k, @v AS v) AS s ON t.k = s.k
WHEN MATCHED THEN UPDATE SET v = s.v
WHEN NOT MATCHED THEN INSERT (k, v) VALUES (s.k, s.v);
```

#### 3.3.2 `INSERT IGNORE` → controllo esistenza esplicito
Posizioni (10 occorrenze):
- `Data/RolePermissionRepository.cs:69`
- `Data/ProgressRepository.cs:15`
- `Data/MaterialRepository.cs:441`
- `Data/EnrollmentRepository.cs:45`
- `Data/AreaRepository.cs:77`
- `Data/CustomRoleStore.cs:17`
- `Data/TranslationRepository.cs:149`
- `Data/ProductionScriptGenerator.cs:299, 304, 325, 336`
- `BocconiLMS.Tests/Helpers/DbTestHelper.cs:48, 56, 125`

Conversione:
```sql
-- MySQL
INSERT IGNORE INTO enrollments (uid, cid) VALUES (@u, @c);

-- SQL Server
IF NOT EXISTS (SELECT 1 FROM enrollments WHERE uid=@u AND cid=@c)
    INSERT INTO enrollments (uid, cid) VALUES (@u, @c);
```

#### 3.3.3 `NOW()` → `SYSDATETIME()`
**Decine di occorrenze** (vedi grep `\bNOW\(\)` su tutto il codebase). Sostituzione meccanica:
```
NOW()  →  SYSDATETIME()    -- precisione DATETIME2
NOW()  →  GETDATE()        -- precisione DATETIME
```

#### 3.3.4 `LIMIT n` / `LIMIT n OFFSET m` → `TOP n` / `OFFSET FETCH`
- **`LIMIT 1`** in fine query (~15 occorrenze, mostly per `SELECT ... WHERE id=@id LIMIT 1`):
  ```sql
  -- MySQL
  SELECT * FROM users WHERE id=@id LIMIT 1
  -- SQL Server
  SELECT TOP 1 * FROM users WHERE id=@id
  ```
- **`LIMIT n OFFSET m`** (paginazione):
  - `Data/SystemLogRepository.cs:81`
  - `Data/MaterialRepository.cs:185`
  ```sql
  -- MySQL
  SELECT ... ORDER BY id DESC LIMIT @lim OFFSET @off
  -- SQL Server
  SELECT ... ORDER BY id DESC OFFSET @off ROWS FETCH NEXT @lim ROWS ONLY
  ```
- **`LIMIT 1 FOR UPDATE`** in `Controllers/AccountController.cs:200`:
  ```sql
  -- SQL Server: TOP 1 + WITH (UPDLOCK, ROWLOCK)
  SELECT TOP 1 ... WITH (UPDLOCK, ROWLOCK) WHERE ...
  ```

#### 3.3.5 `INTERVAL` syntax → `DATEADD`
- `Controllers/HomeController.cs:164`:
  ```sql
  -- MySQL
  SUM(CASE WHEN created_at >= NOW() - INTERVAL 30 DAY THEN 1 ELSE 0 END)
  -- SQL Server
  SUM(CASE WHEN created_at >= DATEADD(DAY, -30, SYSDATETIME()) THEN 1 ELSE 0 END)
  ```

#### 3.3.6 `LAST_INSERT_ID()` → `SCOPE_IDENTITY()`
Posizioni (tutte le INSERT che recuperano l'ID):
- `Data/UserRepository.cs:80`
- `Data/QuizRepository.cs:60, 126`
- … e altre

```sql
-- MySQL
INSERT INTO ...; SELECT LAST_INSERT_ID();
-- SQL Server
INSERT INTO ...; SELECT SCOPE_IDENTITY();
-- oppure con OUTPUT:
INSERT INTO ... OUTPUT INSERTED.id VALUES (...);
```

#### 3.3.7 `GROUP_CONCAT` → `STRING_AGG`
Posizioni:
- `Data/ProductionScriptGenerator.cs:127, 232` (usato per generare lo script di produzione)

```sql
-- MySQL
GROUP_CONCAT(COLUMN_NAME ORDER BY ORDINAL_POSITION SEPARATOR ',')
-- SQL Server (2017+)
STRING_AGG(COLUMN_NAME, ',') WITHIN GROUP (ORDER BY ORDINAL_POSITION)
```

#### 3.3.8 Backtick quoting
Tutti i `` `nome_colonna` `` vanno convertiti in `[nome_colonna]` (o doppi apici `"nome_colonna"`).

### 3.4 Connection string
Format diverso, da gestire in `Program.cs` / `appsettings.json`:
```
MySQL:      Server=...;Database=...;User ID=...;Password=...;Port=3306
SQL Server: Server=...;Database=...;User ID=...;Password=...;Encrypt=True;TrustServerCertificate=False
```

### 3.5 Rinominare il `MySqlHealthCheck`
`Data/MySqlHealthCheck.cs` → `DatabaseHealthCheck` (rimuove dipendenza nominale dal vendor).

### 3.6 `ProductionScriptGenerator` — caso speciale
Questo file **genera DDL/DML MySQL** per il deploy in produzione. In caso di migrazione
a SQL Server bisogna decidere se:
- **(a)** Riscriverlo per generare T-SQL, oppure
- **(b)** Eliminarlo e usare strumenti di deploy schema standard (Flyway, DbUp, sqlpackage).

L'opzione (b) è più sostenibile a lungo termine.

---

## 4. Strategia consigliata oggi (mentre restiamo su MySQL)

Per ridurre il costo di una futura migrazione **senza** rallentare lo sviluppo attuale,
applichiamo questi accorgimenti **incrementalmente** (senza refactoring di massa):

1. **Non aggiungere nuovo codice MySQL-specific** se esiste un'alternativa ANSI:
   - `IFNULL` → `COALESCE` (ANSI standard, funziona in entrambi)
   - `NOW()` → preferire `CURRENT_TIMESTAMP` (ANSI, ma senza precisione millisecondi)
2. **Centralizzare i pattern UPSERT** in un metodo helper di `DbHelper`
   (`UpsertAsync(table, keyColumns, valueColumns)`) — così la migrazione tocca un solo file.
3. **Nuove query con paginazione**: scrivere già con `OFFSET ... FETCH NEXT` se possibile
   (supportato anche da MySQL 8 nativo? No — meglio mantenere `LIMIT/OFFSET` per ora ma
   centralizzare in un helper).
4. **Mai più `REPLACE INTO`** — è già non usato, manteniamo così.
5. **Aggiornare questo documento** ogni volta che si introduce un costrutto MySQL-only,
   per tenere il conto sempre aggiornato.

---

## 5. Decisione finale

La migrazione a SQL Server **non è in roadmap** al momento (vincolo: DB MySQL obbligatorio
in fase corrente). Questo documento serve da:
- **Stima trasparente** per Bocconi se richiede SQL Server (~3 giorni dev + test)
- **Guida operativa** per chi eseguirà la migrazione
- **Promemoria** durante lo sviluppo per non peggiorare la situazione

In assenza di richieste specifiche da Bocconi, **manteniamo MySQL nativo** e sfruttiamo
appieno le sue feature (`ON DUPLICATE KEY UPDATE`, `INSERT IGNORE`, `LIMIT/OFFSET`).
