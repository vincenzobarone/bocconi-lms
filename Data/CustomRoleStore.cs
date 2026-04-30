using Microsoft.AspNetCore.Identity;
using MySqlConnector;

namespace BocconiLMS.Data;

public class CustomRoleStore : IRoleStore<ApplicationRole>
{
    private readonly DbHelper _db;

    public CustomRoleStore(DbHelper db) => _db = db;

    public async Task<IdentityResult> CreateAsync(ApplicationRole role, CancellationToken ct)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync(ct);
        using var cmd = new MySqlCommand(@"
            INSERT IGNORE INTO roles (name, normalized_name, can_teach, can_attend)
            VALUES (@name, @nn, @ct, @ca);
            SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@name", role.Name);
        cmd.Parameters.AddWithValue("@nn", role.NormalizedName ?? role.Name!.ToUpperInvariant());
        cmd.Parameters.AddWithValue("@ct", role.CanTeach ? 1 : 0);
        cmd.Parameters.AddWithValue("@ca", role.CanAttend ? 1 : 0);
        role.Id = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(ApplicationRole role, CancellationToken ct)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync(ct);
        using var cmd = new MySqlCommand(
            "UPDATE roles SET name=@name, normalized_name=@nn, can_teach=@ct, can_attend=@ca WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@name", role.Name);
        cmd.Parameters.AddWithValue("@nn", role.NormalizedName ?? role.Name!.ToUpperInvariant());
        cmd.Parameters.AddWithValue("@ct", role.CanTeach ? 1 : 0);
        cmd.Parameters.AddWithValue("@ca", role.CanAttend ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", role.Id);
        await cmd.ExecuteNonQueryAsync(ct);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(ApplicationRole role, CancellationToken ct)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync(ct);
        using var cmd = new MySqlCommand("DELETE FROM roles WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", role.Id);
        await cmd.ExecuteNonQueryAsync(ct);
        return IdentityResult.Success;
    }

    public async Task<ApplicationRole?> FindByIdAsync(string roleId, CancellationToken ct)
    {
        if (!int.TryParse(roleId, out var id)) return null;
        using var conn = _db.GetConnection();
        await conn.OpenAsync(ct);
        using var cmd = new MySqlCommand(
            "SELECT id, name, normalized_name, can_teach, can_attend FROM roles WHERE id=@id LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var r = await cmd.ExecuteReaderAsync(ct);
        return r.Read() ? MapRole(r) : null;
    }

    public async Task<ApplicationRole?> FindByNameAsync(string normalizedRoleName, CancellationToken ct)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync(ct);
        using var cmd = new MySqlCommand(
            "SELECT id, name, normalized_name, can_teach, can_attend FROM roles WHERE normalized_name=@nn LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@nn", normalizedRoleName);
        using var r = await cmd.ExecuteReaderAsync(ct);
        return r.Read() ? MapRole(r) : null;
    }

    public Task<string> GetRoleIdAsync(ApplicationRole role, CancellationToken ct) =>
        Task.FromResult(role.Id.ToString());

    public Task<string?> GetRoleNameAsync(ApplicationRole role, CancellationToken ct) =>
        Task.FromResult(role.Name);

    public Task SetRoleNameAsync(ApplicationRole role, string? roleName, CancellationToken ct)
    {
        role.Name = roleName;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedRoleNameAsync(ApplicationRole role, CancellationToken ct) =>
        Task.FromResult(role.NormalizedName);

    public Task SetNormalizedRoleNameAsync(ApplicationRole role, string? normalizedName, CancellationToken ct)
    {
        role.NormalizedName = normalizedName;
        return Task.CompletedTask;
    }

    private static ApplicationRole MapRole(MySqlDataReader r) => new()
    {
        Id = r.GetInt32("id"),
        Name = r.GetString("name"),
        NormalizedName = r.GetString("normalized_name"),
        CanTeach = r.GetBoolean("can_teach"),
        CanAttend = r.GetBoolean("can_attend")
    };

    public void Dispose() { }
}
