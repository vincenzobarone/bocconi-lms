using BocconiLMS.Models;
using MySqlConnector;

namespace BocconiLMS.Data;

public class ApiKeyRepository
{
    private readonly DbHelper _db;
    public ApiKeyRepository(DbHelper db) => _db = db;

    public async Task<int> InsertAsync(ApiKey k)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            INSERT INTO api_keys (name, key_prefix, key_hash, scopes, created_by)
            VALUES (@n, @p, @h, @s, @cb);
            SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@n",  k.Name);
        cmd.Parameters.AddWithValue("@p",  k.KeyPrefix);
        cmd.Parameters.AddWithValue("@h",  k.KeyHash);
        cmd.Parameters.AddWithValue("@s",  k.Scopes);
        cmd.Parameters.AddWithValue("@cb", (object?)k.CreatedBy ?? DBNull.Value);
        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return id;
    }

    public async Task<List<ApiKey>> ListAsync()
    {
        var list = new List<ApiKey>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT id, name, key_prefix, key_hash, scopes, created_by,
                   created_at, last_used_at, revoked_at
            FROM api_keys
            ORDER BY (revoked_at IS NULL) DESC, created_at DESC", conn);
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(Map(r));
        return list;
    }

    public async Task<ApiKey?> GetByPrefixAsync(string prefix)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT id, name, key_prefix, key_hash, scopes, created_by,
                   created_at, last_used_at, revoked_at
            FROM api_keys
            WHERE key_prefix = @p
            LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@p", prefix);
        using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? Map(r) : null;
    }

    public async Task<bool> RevokeAsync(int id)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            UPDATE api_keys
            SET revoked_at = UTC_TIMESTAMP()
            WHERE id = @id AND revoked_at IS NULL", conn);
        cmd.Parameters.AddWithValue("@id", id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public void TouchLastUsedFireAndForget(int id)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    "UPDATE api_keys SET last_used_at = UTC_TIMESTAMP() WHERE id = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
            }
            catch { /* non bloccare la request */ }
        });
    }

    private static ApiKey Map(MySqlDataReader r) => new()
    {
        Id         = r.GetInt32("id"),
        Name       = r.GetString("name"),
        KeyPrefix  = r.GetString("key_prefix"),
        KeyHash    = r.GetString("key_hash"),
        Scopes     = r.GetString("scopes"),
        CreatedBy  = r.IsDBNull(r.GetOrdinal("created_by"))   ? null : r.GetString("created_by"),
        CreatedAt  = r.GetDateTime("created_at"),
        LastUsedAt = r.IsDBNull(r.GetOrdinal("last_used_at")) ? null : r.GetDateTime("last_used_at"),
        RevokedAt  = r.IsDBNull(r.GetOrdinal("revoked_at"))   ? null : r.GetDateTime("revoked_at"),
    };
}
