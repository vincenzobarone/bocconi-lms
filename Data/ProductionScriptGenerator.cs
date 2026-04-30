using System.Text;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace BocconiLMS.Data;

/// <summary>
/// Generates a self-contained SQL script ready to be applied to a fresh
/// production MySQL database.  The script:
///   1. Runs a stored-procedure drift check — aborts with SIGNAL SQLSTATE '45000'
///      if any table already exists with different column names or ordering.
///   2. Issues CREATE TABLE IF NOT EXISTS for every application table.
///   3. Seeds the Admin role and a temporary admin user (BCrypt hash).
///   4. Optionally inserts all translation keys.
/// </summary>
public class ProductionScriptGenerator
{
    private readonly DbHelper _db;
    private readonly TranslationRepository _translations;

    // Application tables in FK-safe creation order.
    // 'documents' and 'document_versions' are intentionally excluded:
    // they were dropped by migration 008.
    private static readonly string[] AppTables =
    [
        "users", "roles", "areas", "document_types", "material_folders",
        "platforms", "courses", "lessons", "quizzes", "quiz_questions",
        "quiz_options", "enrollments", "lesson_progress", "quiz_attempts",
        "password_reset_tokens", "user_areas", "translations", "materials",
        "material_versions", "lesson_materials", "role_permissions",
        "schema_migrations", "app_settings",
    ];

    public ProductionScriptGenerator(DbHelper db, TranslationRepository translations)
    {
        _db = db;
        _translations = translations;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates the full production SQL script.
    /// </summary>
    /// <param name="includeTranslations">When true, appends all translation INSERT statements.</param>
    /// <returns>The SQL text and the plain-text temporary admin password.</returns>
    public async Task<(string Sql, string TempPassword)> GenerateAsync(bool includeTranslations)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();

        // Map: table name → comma-separated column names in ORDINAL_POSITION order
        var colNames = await GetColumnNamesAsync(conn);
        var createSql = await GetCreateStatementsAsync(conn, colNames.Keys);

        // Fail fast: every application table (excluding operational tables with fixed DDL)
        // must be present in the source DB.  A missing table means migrations have not been
        // fully applied and the generated script would be incomplete.
        var operationalTables = new HashSet<string>(["schema_migrations", "app_settings"],
            StringComparer.OrdinalIgnoreCase);
        var missingTables = AppTables
            .Where(t => !operationalTables.Contains(t) && !createSql.ContainsKey(t))
            .ToList();
        if (missingTables.Count > 0)
        {
            throw new InvalidOperationException(
                $"Impossibile generare lo script: le seguenti tabelle attese non esistono " +
                $"nel database corrente — {string.Join(", ", missingTables)}. " +
                "Assicurarsi che tutte le migrazioni siano state applicate prima di generare lo script.");
        }

        List<TranslationRow> translationRows = [];
        if (includeTranslations)
            translationRows = await _translations.GetAllGroupedAsync();

        var tempPassword = GenerateTempPassword();
        var hash = BCrypt.Net.BCrypt.HashPassword(tempPassword, workFactor: 11);

        var sb = new StringBuilder();
        AppendHeader(sb, includeTranslations);
        AppendDriftProcedure(sb, colNames);
        AppendCreateTables(sb, createSql);
        AppendSchemaOperationalTables(sb);
        AppendSeedData(sb, hash);
        if (includeTranslations && translationRows.Count > 0)
            AppendTranslations(sb, translationRows);
        AppendFooter(sb);

        return (sb.ToString(), tempPassword);
    }

    // ── Schema introspection ──────────────────────────────────────────────────

    /// <summary>
    /// Returns a map of table name → comma-separated column names ordered by ORDINAL_POSITION.
    /// Only tables that actually exist in the DB are returned.
    /// </summary>
    private static async Task<Dictionary<string, string>> GetColumnNamesAsync(MySqlConnection conn)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var cmd = new MySqlCommand(@"
            SELECT TABLE_NAME,
                   GROUP_CONCAT(COLUMN_NAME ORDER BY ORDINAL_POSITION SEPARATOR ',') AS col_list
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME IN ('" + string.Join("','", AppTables) + @"')
            GROUP BY TABLE_NAME", conn);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[reader.GetString(0)] = reader.GetString(1);

        return result;
    }

    /// <summary>
    /// Returns SHOW CREATE TABLE text for tables that actually exist in the DB,
    /// keyed by table name.  'schema_migrations' and 'app_settings' are skipped
    /// (emitted separately with a fixed DDL).
    /// </summary>
    private static async Task<Dictionary<string, string>> GetCreateStatementsAsync(
        MySqlConnection conn, IEnumerable<string> existingTables)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tbl in existingTables)
        {
            if (tbl is "schema_migrations" or "app_settings") continue;

            using var cmd = new MySqlCommand($"SHOW CREATE TABLE `{tbl}`", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                result[tbl] = CleanCreateStatement(reader.GetString(1));
        }

        return result;
    }

    /// <summary>
    /// Cleans the output of SHOW CREATE TABLE for use in a fresh-DB script:
    /// adds IF NOT EXISTS and strips the AUTO_INCREMENT=N counter.
    /// </summary>
    private static string CleanCreateStatement(string raw)
    {
        var result = Regex.Replace(raw,
            @"^CREATE TABLE (`[^`]+`)",
            "CREATE TABLE IF NOT EXISTS $1",
            RegexOptions.Multiline);

        result = Regex.Replace(result,
            @"\bAUTO_INCREMENT=\d+\s*",
            "",
            RegexOptions.IgnoreCase);

        return result.Trim();
    }

    // ── SQL generation ────────────────────────────────────────────────────────

    private static void AppendHeader(StringBuilder sb, bool includeTranslations)
    {
        sb.AppendLine("-- =============================================================================");
        sb.AppendLine("-- Didasco LMS — Script di installazione per produzione");
        sb.AppendLine($"-- Generato il: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine("-- =============================================================================");
        sb.AppendLine("--");
        sb.AppendLine("-- ISTRUZIONI:");
        sb.AppendLine("--   1. Eseguire su un database MySQL vuoto con:");
        sb.AppendLine("--        mysql -u<user> -p <db_name> < questo_file.sql");
        sb.AppendLine("--   2. Il blocco di drift detection blocca lo script se trova tabelle");
        sb.AppendLine("--      con colonne diverse da quelle attese — verificare manualmente.");
        sb.AppendLine("--   3. La password temporanea dell'utente admin è stata mostrata nel browser");
        sb.AppendLine("--      al momento della generazione. Cambiarla al primo accesso.");
        sb.AppendLine("--      Email admin: admin@bocconi.it");
        if (includeTranslations)
            sb.AppendLine("--   4. Le chiavi di traduzione sono incluse in questo script.");
        sb.AppendLine("--");
        sb.AppendLine("-- CREATE TABLE usa IF NOT EXISTS; INSERT usa INSERT IGNORE / ON DUPLICATE KEY.");
        sb.AppendLine("-- =============================================================================");
        sb.AppendLine();
        sb.AppendLine("SET NAMES utf8mb4;");
        sb.AppendLine("SET FOREIGN_KEY_CHECKS = 0;");
        sb.AppendLine();
    }

    private static void AppendDriftProcedure(StringBuilder sb, Dictionary<string, string> colNames)
    {
        if (colNames.Count == 0) return;

        sb.AppendLine("-- =============================================================================");
        sb.AppendLine("-- DRIFT DETECTION");
        sb.AppendLine("-- Controlla che ogni tabella già presente nel DB di destinazione");
        sb.AppendLine("-- abbia esattamente le stesse colonne (nomi + ordine) dello schema");
        sb.AppendLine("-- atteso.  Lo script si interrompe con SIGNAL se la lista non coincide.");
        sb.AppendLine("-- =============================================================================");
        sb.AppendLine();
        sb.AppendLine("DROP PROCEDURE IF EXISTS `_didasco_drift_check`;");
        sb.AppendLine();
        sb.AppendLine("DELIMITER $$");
        sb.AppendLine("CREATE PROCEDURE `_didasco_drift_check`()");
        sb.AppendLine("BEGIN");
        sb.AppendLine("    DECLARE actual_cols VARCHAR(4000) DEFAULT '';");
        sb.AppendLine();

        foreach (var (table, expectedCols) in colNames.OrderBy(k => k.Key))
        {
            // Skip operational tables — emitted separately with fixed DDL
            if (table is "schema_migrations" or "app_settings") continue;

            sb.AppendLine($"    -- Tabella: {table}");
            sb.AppendLine($"    SELECT GROUP_CONCAT(COLUMN_NAME ORDER BY ORDINAL_POSITION SEPARATOR ',')");
            sb.AppendLine($"        INTO actual_cols");
            sb.AppendLine($"        FROM information_schema.COLUMNS");
            sb.AppendLine($"        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{table}';");
            sb.AppendLine($"    IF actual_cols IS NOT NULL AND actual_cols != '{expectedCols}' THEN");

            var msg = $"SCHEMA DRIFT: {table}: colonne diverse da attese. Verificare prima di procedere.";
            if (msg.Length > 128) msg = msg[..128];
            sb.AppendLine($"        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = '{msg}';");
            sb.AppendLine($"    END IF;");
            sb.AppendLine();
        }

        sb.AppendLine("END$$");
        sb.AppendLine("DELIMITER ;");
        sb.AppendLine();
        sb.AppendLine("CALL `_didasco_drift_check`();");
        sb.AppendLine("DROP PROCEDURE IF EXISTS `_didasco_drift_check`;");
        sb.AppendLine();
    }

    private static void AppendCreateTables(StringBuilder sb, Dictionary<string, string> createSql)
    {
        sb.AppendLine("-- =============================================================================");
        sb.AppendLine("-- SCHEMA TABELLE");
        sb.AppendLine("-- =============================================================================");
        sb.AppendLine();

        foreach (var tbl in AppTables)
        {
            if (tbl is "schema_migrations" or "app_settings") continue;
            if (!createSql.TryGetValue(tbl, out var stmt)) continue;

            sb.AppendLine($"-- Tabella: {tbl}");
            sb.AppendLine(stmt + ";");
            sb.AppendLine();
        }
    }

    private static void AppendSchemaOperationalTables(StringBuilder sb)
    {
        sb.AppendLine("-- =============================================================================");
        sb.AppendLine("-- TABELLE OPERATIVE (create al boot dall'applicazione)");
        sb.AppendLine("-- =============================================================================");
        sb.AppendLine();

        sb.AppendLine("-- Tabella: schema_migrations");
        sb.AppendLine(@"CREATE TABLE IF NOT EXISTS `schema_migrations` (
    `id`         INT          NOT NULL AUTO_INCREMENT PRIMARY KEY,
    `name`       VARCHAR(255) NOT NULL,
    `applied_at` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY `uk_name` (`name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");
        sb.AppendLine();

        sb.AppendLine("-- Tabella: app_settings");
        sb.AppendLine(@"CREATE TABLE IF NOT EXISTS `app_settings` (
    `setting_key`   VARCHAR(100) NOT NULL PRIMARY KEY,
    `setting_value` TEXT,
    `updated_at`    DATETIME     DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");
        sb.AppendLine();
    }

    private static void AppendSeedData(StringBuilder sb, string bcryptHash)
    {
        var escapedHash = bcryptHash.Replace("'", "''");

        sb.AppendLine("-- =============================================================================");
        sb.AppendLine("-- SEED: DATI VITALI");
        sb.AppendLine("-- =============================================================================");
        sb.AppendLine();
        sb.AppendLine("SET FOREIGN_KEY_CHECKS = 1;");
        sb.AppendLine();

        sb.AppendLine("-- Ruolo Admin");
        sb.AppendLine("INSERT IGNORE INTO `roles` (`name`, `normalized_name`, `created_at`)");
        sb.AppendLine("VALUES ('Admin', 'ADMIN', NOW());");
        sb.AppendLine();

        sb.AppendLine("-- Utente admin (password temporanea — da cambiare al primo accesso)");
        sb.AppendLine("INSERT IGNORE INTO `users`");
        sb.AppendLine("    (`email`, `password_hash`, `first_name`, `last_name`, `role`, `is_active`, `created_at`)");
        sb.AppendLine("VALUES");
        sb.AppendLine($"    ('admin@bocconi.it', '{escapedHash}', 'Amministratore', 'Bocconi', 'Admin', 1, NOW());");
        sb.AppendLine();
    }

    private static void AppendTranslations(StringBuilder sb, List<TranslationRow> rows)
    {
        sb.AppendLine("-- =============================================================================");
        sb.AppendLine("-- SEED: CHIAVI DI TRADUZIONE");
        sb.AppendLine($"-- ({rows.Count} chiavi — 4 lingue: it, en, es, de)");
        sb.AppendLine("-- =============================================================================");
        sb.AppendLine();

        string[] langs = ["it", "en", "es", "de"];
        foreach (var row in rows)
        {
            foreach (var lang in langs)
            {
                var val = lang switch
                {
                    "it" => row.It,
                    "en" => row.En,
                    "es" => row.Es,
                    "de" => row.De,
                    _ => null,
                };
                if (string.IsNullOrEmpty(val)) continue;

                var escapedKey = row.Key.Replace("'", "''");
                var escapedVal = val.Replace("'", "''");

                sb.AppendLine($"INSERT INTO `translations` (`language_code`, `label_key`, `label_value`)");
                sb.AppendLine($"VALUES ('{lang}', '{escapedKey}', '{escapedVal}')");
                sb.AppendLine($"ON DUPLICATE KEY UPDATE `label_value` = VALUES(`label_value`);");
            }
        }

        sb.AppendLine();
    }

    private static void AppendFooter(StringBuilder sb)
    {
        sb.AppendLine("-- =============================================================================");
        sb.AppendLine("-- FINE SCRIPT");
        sb.AppendLine("-- =============================================================================");
        sb.AppendLine("SET FOREIGN_KEY_CHECKS = 1;");
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static string GenerateTempPassword()
    {
        // 20-char password: 3 prefix + 16 random hex + 1 special char.
        // Satisfies typical password complexity policies while remaining
        // easy to copy-paste from the one-time UI banner.
        var raw = Guid.NewGuid().ToString("N"); // 32 hex chars
        return "Tmp" + raw[..16] + "!";
    }
}
