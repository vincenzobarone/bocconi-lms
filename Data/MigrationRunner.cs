using MySqlConnector;

namespace BocconiLMS.Data;

public class MigrationRunner
{
    private readonly DbHelper _dbHelper;
    private readonly string _migrationsPath;

    public MigrationRunner(DbHelper dbHelper, IWebHostEnvironment env)
    {
        _dbHelper = dbHelper;
        _migrationsPath = Path.Combine(env.ContentRootPath, "Migrations");
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task RunAsync()
    {
        using var conn = _dbHelper.GetConnection();
        await conn.OpenAsync();

        await EnsureSchemaTable(conn);
        await PreSeedIfExistingDb(conn);
        await ApplyPending(conn);
    }

    public async Task<MigrationStatus> GetStatusAsync()
    {
        using var conn = _dbHelper.GetConnection();
        await conn.OpenAsync();

        await EnsureSchemaTable(conn);

        var allFiles = GetMigrationFiles();
        var applied  = await GetAppliedMigrations(conn);

        var rows = allFiles.Select(f =>
        {
            var n = Path.GetFileName(f);
            applied.TryGetValue(n, out var dt);
            return new MigrationRow
            {
                Name      = n,
                IsApplied = applied.ContainsKey(n),
                AppliedAt = dt
            };
        }).ToList();

        return new MigrationStatus
        {
            Rows         = rows,
            TotalCount   = rows.Count,
            AppliedCount = rows.Count(r => r.IsApplied),
            PendingCount = rows.Count(r => !r.IsApplied)
        };
    }

    public async Task<List<(string Name, string? Error)>> ApplyPendingAsync()
    {
        using var conn = _dbHelper.GetConnection();
        await conn.OpenAsync();

        await EnsureSchemaTable(conn);
        return await ApplyPending(conn);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task EnsureSchemaTable(MySqlConnection conn)
    {
        using var cmd = new MySqlCommand(@"
            CREATE TABLE IF NOT EXISTS schema_migrations (
                id         INT AUTO_INCREMENT PRIMARY KEY,
                name       VARCHAR(255) NOT NULL,
                applied_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UNIQUE KEY uk_name (name)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    // If the DB is already initialised (materials table exists) but schema_migrations
    // is empty, pre-seed all current migration file names as already applied so we
    // don't re-run schema changes that Program.cs already executed.
    private async Task PreSeedIfExistingDb(MySqlConnection conn)
    {
        using var countCmd = new MySqlCommand(
            "SELECT COUNT(*) FROM schema_migrations;", conn);
        var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
        if (count > 0) return;

        using var checkMat = new MySqlCommand(@"
            SELECT COUNT(*) FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'materials';", conn);
        var materialsExists = Convert.ToInt32(await checkMat.ExecuteScalarAsync()) > 0;
        if (!materialsExists) return;

        // Existing DB — mark all current files as applied
        foreach (var file in GetMigrationFiles())
        {
            var name = Path.GetFileName(file);
            using var ins = new MySqlCommand(
                "INSERT IGNORE INTO schema_migrations (name, applied_at) VALUES (@n, NOW());", conn);
            ins.Parameters.AddWithValue("@n", name);
            await ins.ExecuteNonQueryAsync();
        }
    }

    private async Task<List<(string Name, string? Error)>> ApplyPending(MySqlConnection conn)
    {
        var results = new List<(string Name, string? Error)>();
        var applied = await GetAppliedMigrations(conn);

        foreach (var file in GetMigrationFiles())
        {
            var name = Path.GetFileName(file);
            if (applied.ContainsKey(name)) continue;

            var sql = await File.ReadAllTextAsync(file);
            string? error = null;

            try
            {
                // Execute each statement separately (split on semicolons at line ends)
                foreach (var stmt in SplitStatements(sql))
                {
                    if (string.IsNullOrWhiteSpace(stmt)) continue;
                    using var cmd = new MySqlCommand(stmt, conn);
                    await cmd.ExecuteNonQueryAsync();
                }

                using var rec = new MySqlCommand(
                    "INSERT IGNORE INTO schema_migrations (name, applied_at) VALUES (@n, NOW());", conn);
                rec.Parameters.AddWithValue("@n", name);
                await rec.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            results.Add((name, error));
        }

        return results;
    }

    private async Task<Dictionary<string, DateTime?>> GetAppliedMigrations(MySqlConnection conn)
    {
        var dict = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
        using var cmd = new MySqlCommand(
            "SELECT name, applied_at FROM schema_migrations ORDER BY applied_at;", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            dict[reader.GetString(0)] = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
        return dict;
    }

    private List<string> GetMigrationFiles()
    {
        if (!Directory.Exists(_migrationsPath)) return [];
        return Directory.GetFiles(_migrationsPath, "*.sql")
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // Split SQL file into individual statements by semicolons that are not inside strings.
    // Simple approach: split on ";\n" or ";\r\n" boundaries.
    private static IEnumerable<string> SplitStatements(string sql)
    {
        // Remove SQL comments (-- ... to end of line)
        var lines = sql.Split('\n');
        var cleaned = string.Join('\n', lines.Select(l =>
        {
            var idx = l.IndexOf("--", StringComparison.Ordinal);
            return idx >= 0 ? l[..idx] : l;
        }));

        // Split on semicolons
        return cleaned.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s));
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public class MigrationStatus
{
    public List<MigrationRow> Rows         { get; set; } = [];
    public int TotalCount   { get; set; }
    public int AppliedCount { get; set; }
    public int PendingCount { get; set; }
}

public class MigrationRow
{
    public string    Name      { get; set; } = "";
    public bool      IsApplied { get; set; }
    public DateTime? AppliedAt { get; set; }
}
