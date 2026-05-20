using MySqlConnector;

namespace BocconiLMS.Data;

public class TranslationRepository
{
    private readonly DbHelper _db;

    public TranslationRepository(DbHelper db) => _db = db;

    public async Task<Dictionary<string, string>> GetByLanguageAsync(string languageCode)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT label_key, label_value FROM translations WHERE language_code=@lang", conn);
        cmd.Parameters.AddWithValue("@lang", languageCode);
        using var r = await cmd.ExecuteReaderAsync();
        while (r.Read())
            result[r.GetString(0)] = r.GetString(1);
        return result;
    }

    public async Task<List<TranslationRow>> GetAllGroupedAsync()
    {
        var dict = new Dictionary<string, TranslationRow>(StringComparer.OrdinalIgnoreCase);
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT t.language_code, t.label_key, t.label_value,
                   k.key_created_at
            FROM translations t
            JOIN (
                SELECT label_key, MIN(created_at) AS key_created_at
                FROM translations
                GROUP BY label_key
            ) k ON k.label_key = t.label_key
            ORDER BY t.label_key, t.language_code", conn);
        using var r = await cmd.ExecuteReaderAsync();
        while (r.Read())
        {
            var lang      = r.GetString("language_code");
            var key       = r.GetString("label_key");
            var val       = r.GetString("label_value");
            var createdAt = r.GetDateTime("key_created_at");
            if (!dict.TryGetValue(key, out var row))
            {
                row = new TranslationRow { Key = key, CreatedAt = createdAt };
                dict[key] = row;
            }
            else if (createdAt < row.CreatedAt)
            {
                row.CreatedAt = createdAt;
            }
            switch (lang)
            {
                case "en": row.En = val; break;
                case "it": row.It = val; break;
                case "es": row.Es = val; break;
                case "de": row.De = val; break;
            }
        }
        return dict.Values.OrderBy(x => x.Key).ToList();
    }

    public async Task<TranslationRow?> GetByKeyAsync(string key)
    {
        var rows = new Dictionary<string, string>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT language_code, label_value FROM translations WHERE label_key=@key", conn);
        cmd.Parameters.AddWithValue("@key", key);
        using var r = await cmd.ExecuteReaderAsync();
        while (r.Read())
            rows[r.GetString("language_code")] = r.GetString("label_value");
        if (rows.Count == 0) return null;
        return new TranslationRow
        {
            Key = key,
            En = rows.GetValueOrDefault("en", ""),
            It = rows.GetValueOrDefault("it", ""),
            Es = rows.GetValueOrDefault("es", ""),
            De = rows.GetValueOrDefault("de", "")
        };
    }

    public async Task UpsertAsync(string languageCode, string key, string value)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            INSERT INTO translations (language_code, label_key, label_value, created_at)
            VALUES (@lang, @key, @val, NOW())
            ON DUPLICATE KEY UPDATE label_value=@val, updated_at=NOW()", conn);
        cmd.Parameters.AddWithValue("@lang", languageCode);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@val", value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SaveRowAsync(TranslationRow row)
    {
        var langs = new[] { ("en", row.En), ("it", row.It), ("es", row.Es), ("de", row.De) };
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        foreach (var (lang, val) in langs)
        {
            if (string.IsNullOrWhiteSpace(val)) continue;
            using var cmd = new MySqlCommand(@"
                INSERT INTO translations (language_code, label_key, label_value)
                VALUES (@lang, @key, @val)
                ON DUPLICATE KEY UPDATE label_value=@val, updated_at=NOW()", conn);
            cmd.Parameters.AddWithValue("@lang", lang);
            cmd.Parameters.AddWithValue("@key", row.Key);
            cmd.Parameters.AddWithValue("@val", val.Trim());
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteKeyAsync(string key)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand("DELETE FROM translations WHERE label_key=@key", conn);
        cmd.Parameters.AddWithValue("@key", key);
        await cmd.ExecuteNonQueryAsync();
    }

    // Called fire-and-forget by TranslationService when a key is used in code
    // but not yet present in the DB. Inserts empty rows for all languages so
    // the key appears in Admin → Translations ready to be filled in.
    public async Task RegisterMissingKeyAsync(string key)
    {
        try
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            foreach (var lang in new[] { "en", "it", "es", "de" })
            {
                using var cmd = new MySqlCommand(@"
                    INSERT IGNORE INTO translations
                        (language_code, label_key, label_value, created_at, updated_at)
                    VALUES (@lang, @key, '', NOW(), NOW())", conn);
                cmd.Parameters.AddWithValue("@lang", lang);
                cmd.Parameters.AddWithValue("@key", key);
                await cmd.ExecuteNonQueryAsync();
            }
        }
        catch { /* fire-and-forget: never throw */ }
    }

    public async Task<bool> KeyExistsAsync(string key)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM translations WHERE label_key=@key LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@key", key);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task<int> FillMissingAsync(IEnumerable<string> targetLanguages)
    {
        int total = 0;
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        foreach (var lang in targetLanguages.Where(l => l != "en"))
        {
            using var cmd = new MySqlCommand(@"
                INSERT IGNORE INTO translations (language_code, label_key, label_value, created_at, updated_at)
                SELECT @lang, label_key, label_value, NOW(), NOW()
                FROM translations
                WHERE language_code = 'en'
                  AND label_key NOT IN (
                      SELECT label_key FROM translations WHERE language_code = @lang
                  )", conn);
            cmd.Parameters.AddWithValue("@lang", lang);
            total += await cmd.ExecuteNonQueryAsync();
        }
        return total;
    }

    public async Task<Dictionary<string, int>> GetMissingCountsAsync()
    {
        var result = new Dictionary<string, int>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        foreach (var lang in new[] { "it", "es", "de" })
        {
            using var cmd = new MySqlCommand(@"
                SELECT COUNT(*) FROM translations en
                WHERE en.language_code = 'en'
                  AND NOT EXISTS (
                      SELECT 1 FROM translations WHERE language_code=@lang AND label_key=en.label_key
                  )", conn);
            cmd.Parameters.AddWithValue("@lang", lang);
            result[lang] = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }
        return result;
    }
}

public class TranslationRow
{
    public string Key { get; set; } = "";
    public string En { get; set; } = "";
    public string It { get; set; } = "";
    public string Es { get; set; } = "";
    public string De { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
