using MySqlConnector;

namespace BocconiLMS.Data;

public class SettingsRepository
{
    private readonly DbHelper _db;
    private readonly ILogger<SettingsRepository> _logger;

    public SettingsRepository(DbHelper db, ILogger<SettingsRepository> logger)
    {
        _db = db;
        _logger = logger;
        EnsureTableExists();
    }

    private void EnsureTableExists()
    {
        try
        {
            using var conn = _db.GetConnection();
            conn.Open();
            using var cmd = new MySqlCommand(@"
                CREATE TABLE IF NOT EXISTS app_settings (
                    `setting_key` VARCHAR(100) NOT NULL PRIMARY KEY,
                    `setting_value` TEXT,
                    `updated_at` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4", conn);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not create app_settings table. Settings persistence may be unavailable.");
        }
    }

    public async Task<string?> GetAsync(string key)
    {
        try
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "SELECT `setting_value` FROM app_settings WHERE `setting_key` = @key", conn);
            cmd.Parameters.AddWithValue("@key", key);
            var result = await cmd.ExecuteScalarAsync();
            return result == DBNull.Value ? null : result?.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read setting '{Key}' from database.", key);
            return null;
        }
    }

    public async Task SetAsync(string key, string? value)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            INSERT INTO app_settings (`setting_key`, `setting_value`)
            VALUES (@key, @value)
            ON DUPLICATE KEY UPDATE `setting_value` = @value", conn);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", (object?)value ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<Dictionary<string, string?>> GetByPrefixAsync(string prefix)
    {
        var result = new Dictionary<string, string?>();
        try
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "SELECT `setting_key`, `setting_value` FROM app_settings WHERE `setting_key` LIKE @prefix", conn);
            cmd.Parameters.AddWithValue("@prefix", prefix + "%");
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var key = reader.GetString(0);
                var val = reader.IsDBNull(1) ? null : reader.GetString(1);
                result[key] = val;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read settings with prefix '{Prefix}' from database.", prefix);
        }
        return result;
    }
}
