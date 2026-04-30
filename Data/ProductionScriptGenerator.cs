using System.Text;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace BocconiLMS.Data;

/// <summary>
/// Generates a self-contained SQL script ready to be applied to a fresh
/// production MySQL database.  The script:
///   1. Runs a stored-procedure drift check — aborts if any table already
///      exists with a different column count.
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

        var colCounts = await GetColumnCountsAsync(conn);
        var createSql = await GetCreateStatementsAsync(conn, colCounts.Keys);

        List<TranslationRow> translationRows = [];
        if (includeTranslations)
            translationRows = await _translations.GetAllGroupedAsync();

        var tempPassword = GenerateTempPassword();
        var hash = BCrypt.Net.BCrypt.HashPassword(tempPassword, workFactor: 11);

        var sb = new StringBuilder();
        AppendHeader(sb, includeTranslations);
        AppendDriftProcedure(sb, colCounts);
        AppendCreateTables(sb, createSql);
        AppendSchemaOperationalTables(sb, colCounts);
        AppendSeedData(sb, hash);
        if (includeTranslations && translationRows.Count > 0)
            AppendTranslations(sb, translationRows);
        AppendFooter(sb);

        return (sb.ToString(), tempPassword);
    }

    // ── Schema introspection ──────────────────────────────────────────────────

    private static async Task<Dictionary<string, int>> GetColumnCountsAsync(MySqlConnection conn)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        using var cmd = new MySqlCommand(@"
            SELECT TABLE_NAME, COUNT(*) AS col_count
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME IN ('" + string.Join("','", AppTables) + @"')
            GROUP BY TABLE_NAME", conn);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[reader.GetString(0)] = reader.GetInt32(1);

        return result;
    }

    /// <summary>
    /// Returns SHOW CREATE TABLE text for tables that actually exist in the DB,
    /// keyed by lowercase table name.
    /// </summary>
    private static async Task<Dictionary<string, string>> GetCreateStatementsAsync(
        MySqlConnection conn, IEnumerable<string> existingTables)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tbl in existingTables)
        {
            // Skip operational tables — handled separately below.
            if (tbl is "schema_migrations" or "app_settings") continue;

            using var cmd = new MySqlCommand($"SHOW CREATE TABLE `{tbl}`", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var raw = reader.GetString(1);
                result[tbl] = CleanCreateStatement(raw);
            }
        }

        return result;
    }

    /// <summary>
    /// Cleans up the output of SHOW CREATE TABLE for use in a fresh-DB script:
    ///   - Adds IF NOT EXISTS
    ///   - Strips the AUTO_INCREMENT=N counter (reset to 1 on fresh DB)
    /// </summary>
    private static string CleanCreateStatement(string raw)
    {
        // Add IF NOT EXISTS
        var result = Regex.Replace(raw,
            @"^CREATE TABLE (`[^`]+`)",
            "CREATE TABLE IF NOT EXISTS $1",
            RegexOptions.Multiline);

        // Remove AUTO_INCREMENT=NNN at end of statement (trailing table options)
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
        sb.AppendLine("--   2. Il blocco di drift detection all'inizio blocca lo script");
        sb.AppendLine("--      se trova tabelle esistenti con struttura diversa da quella");
        sb.AppendLine("--      attesa — verificare manualmente e procedere.");
        sb.AppendLine("--   3. La password temporanea dell'utente admin è stata mostrata");
        sb.AppendLine("--      nel browser al momento del download. Cambiarla al primo accesso.");
        if (includeTranslations)
            sb.AppendLine("--   4. Le chiavi di traduzione sono incluse in questo script.");
        sb.AppendLine("--");
        sb.AppendLine("-- ATTENZIONE: questo script NON elimina dati esistenti.");
        sb.AppendLine("--   CREATE TABLE usa IF NOT EXISTS.");
        sb.AppendLine("--   INSERT usa INSERT IGNORE o ON DUPLICATE KEY UPDATE.");
        sb.AppendLine("-- =============================================================================");
        sb.AppendLine();
        sb.AppendLine("SET NAMES utf8mb4;");
        sb.AppendLine("SET FOREIGN_KEY_CHECKS = 0;");
        sb.AppendLine();
    }

    private static void AppendDriftProcedure(StringBuilder sb, Dictionary<string, int> colCounts)
    {
        if (colCounts.Count == 0) return;

        sb.AppendLine("-- =============================================================================");
        sb.AppendLine("-- DRIFT DETECTION");
        sb.AppendLine("-- Controlla che le tabelle eventualmente già presenti abbiano");
        sb.AppendLine("-- lo stesso numero di colonne atteso. In caso di discrepanza");
        sb.AppendLine("-- lo script si interrompe con un errore.");
        sb.AppendLine("-- =============================================================================");
        sb.AppendLine();
        sb.AppendLine("DROP PROCEDURE IF EXISTS `_didasco_drift_check`;");
        sb.AppendLine();
        sb.AppendLine("DELIMITER $$");
        sb.AppendLine("CREATE PROCEDURE `_didasco_drift_check`()");
        sb.AppendLine("BEGIN");
        sb.AppendLine("    DECLARE actual_cols INT DEFAULT 0;");
        sb.AppendLine();

        foreach (var (table, expected) in colCounts.OrderBy(k => k.Key))
        {
            sb.AppendLine($"    -- Tabella: {table} (colonne attese: {expected})");
            sb.AppendLine($"    SELECT COUNT(*) INTO actual_cols");
            sb.AppendLine($"        FROM information_schema.COLUMNS");
            sb.AppendLine($"        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{table}';");
            sb.AppendLine($"    IF actual_cols > 0 AND actual_cols != {expected} THEN");
            // MySQL SIGNAL MESSAGE_TEXT max 128 chars; must be a string literal (no variable interpolation)
            var msg = $"SCHEMA DRIFT: {table} ha colonne diverse da attese ({expected}). Verificare.";
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

        // Emit tables in AppTables order (preserves FK-safe order)
        foreach (var tbl in AppTables)
        {
            if (tbl is "schema_migrations" or "app_settings") continue;
            if (!createSql.TryGetValue(tbl, out var stmt)) continue;

            sb.AppendLine($"-- Tabella: {tbl}");
            sb.AppendLine(stmt + ";");
            sb.AppendLine();
        }
    }

    private static void AppendSchemaOperationalTables(
        StringBuilder sb, Dictionary<string, int> colCounts)
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
        // 12-character alphanumeric password from a GUID
        var raw = Guid.NewGuid().ToString("N"); // 32 hex chars
        // Mix case and add a digit prefix for "complexity"
        return "Tmp" + raw[..8] + "!";
    }
}
