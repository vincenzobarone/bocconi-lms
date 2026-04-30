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

    /// <summary>
    /// Called at app startup. Throws if any migration fails (fail-fast).
    /// </summary>
    public async Task RunAsync()
    {
        using var conn = _dbHelper.GetConnection();
        await conn.OpenAsync();
        await EnsureSchemaTable(conn);
        await PreSeedIfExistingDb(conn);

        // Execute pending migrations on a connection with AllowUserVariables=true
        // (needed for conditional ALTER TABLE statements using PREPARE/EXECUTE).
        using var migConn = _dbHelper.GetConnectionWithUserVariables();
        await migConn.OpenAsync();
        await ApplyPendingInternal(migConn, failFast: true);
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

    /// <summary>
    /// Called from the admin UI. Stops at first failure and returns the error.
    /// Does NOT continue after a failed migration.
    /// </summary>
    public async Task<List<(string Name, string? Error)>> ApplyPendingAsync()
    {
        using var migConn = _dbHelper.GetConnectionWithUserVariables();
        await migConn.OpenAsync();
        await EnsureSchemaTable(migConn);
        return await ApplyPendingInternal(migConn, failFast: false);
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
    // don't re-run schema changes on an existing database.
    private async Task PreSeedIfExistingDb(MySqlConnection conn)
    {
        using var countCmd = new MySqlCommand("SELECT COUNT(*) FROM schema_migrations;", conn);
        var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
        if (count > 0) return;

        using var checkMat = new MySqlCommand(@"
            SELECT COUNT(*) FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'materials';", conn);
        var materialsExists = Convert.ToInt32(await checkMat.ExecuteScalarAsync()) > 0;
        if (!materialsExists) return;

        // Existing DB: mark all current migration files as applied (without running them).
        foreach (var file in GetMigrationFiles())
        {
            var name = Path.GetFileName(file);
            using var ins = new MySqlCommand(
                "INSERT IGNORE INTO schema_migrations (name, applied_at) VALUES (@n, NOW());", conn);
            ins.Parameters.AddWithValue("@n", name);
            await ins.ExecuteNonQueryAsync();
        }
    }

    /// <param name="failFast">
    /// true  → throws MigrationException on the first failure (startup use).
    /// false → records the error, stops iteration, returns results (admin UI use).
    /// </param>
    private async Task<List<(string Name, string? Error)>> ApplyPendingInternal(
        MySqlConnection conn, bool failFast)
    {
        var results = new List<(string Name, string? Error)>();
        var applied = await GetAppliedMigrations(conn);

        foreach (var file in GetMigrationFiles())
        {
            var name = Path.GetFileName(file);
            if (applied.ContainsKey(name)) continue;

            var sql = await File.ReadAllTextAsync(file);
            try
            {
                foreach (var stmt in SplitStatements(sql))
                {
                    if (string.IsNullOrWhiteSpace(stmt)) continue;
                    using var cmd = new MySqlCommand(stmt, conn);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Record migration as applied only after all statements succeed.
                using var rec = new MySqlCommand(
                    "INSERT IGNORE INTO schema_migrations (name, applied_at) VALUES (@n, NOW());", conn);
                rec.Parameters.AddWithValue("@n", name);
                await rec.ExecuteNonQueryAsync();

                results.Add((name, null));
            }
            catch (Exception ex)
            {
                var msg = $"Migration '{name}' failed: {ex.Message}";
                results.Add((name, ex.Message));

                if (failFast)
                    throw new MigrationException(name, ex);

                // In non-failFast mode (admin UI), stop after first failure.
                break;
            }
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

    // Split SQL file into individual statements by semicolons.
    // Strips single-line -- comments before splitting.
    private static IEnumerable<string> SplitStatements(string sql)
    {
        var lines = sql.Split('\n');
        var cleaned = string.Join('\n', lines.Select(l =>
        {
            var idx = l.IndexOf("--", StringComparison.Ordinal);
            return idx >= 0 ? l[..idx] : l;
        }));

        return cleaned.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s));
    }
}

// ── Custom exception ───────────────────────────────────────────────────────────

public class MigrationException : Exception
{
    public string MigrationName { get; }

    public MigrationException(string name, Exception inner)
        : base($"Migration '{name}' failed: {inner.Message}", inner)
    {
        MigrationName = name;
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
