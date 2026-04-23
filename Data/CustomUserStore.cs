using Microsoft.AspNetCore.Identity;
using MySqlConnector;

namespace BocconiLMS.Data;

public class CustomUserStore :
    IUserStore<ApplicationUser>,
    IUserPasswordStore<ApplicationUser>,
    IUserEmailStore<ApplicationUser>,
    IUserRoleStore<ApplicationUser>
{
    private readonly DbHelper _db;

    public CustomUserStore(DbHelper db) => _db = db;

    public async Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken ct)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync(ct);
        using var cmd = new MySqlCommand(@"
            INSERT INTO users (email, password_hash, first_name, last_name, role, is_active, created_at)
            VALUES (@email, @hash, @fn, @ln, 'Student', 1, NOW());
            SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@email", user.Email);
        cmd.Parameters.AddWithValue("@hash", user.PasswordHash ?? "");
        cmd.Parameters.AddWithValue("@fn", user.FirstName);
        cmd.Parameters.AddWithValue("@ln", user.LastName);
        user.Id = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken ct)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync(ct);
        using var cmd = new MySqlCommand(@"
            UPDATE users SET email=@email, password_hash=@hash,
                first_name=@fn, last_name=@ln, is_active=@active
            WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@email", user.Email);
        cmd.Parameters.AddWithValue("@hash", user.PasswordHash ?? "");
        cmd.Parameters.AddWithValue("@fn", user.FirstName);
        cmd.Parameters.AddWithValue("@ln", user.LastName);
        cmd.Parameters.AddWithValue("@active", user.IsActive);
        cmd.Parameters.AddWithValue("@id", user.Id);
        await cmd.ExecuteNonQueryAsync(ct);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken ct)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync(ct);
        using var cmd = new MySqlCommand("DELETE FROM users WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", user.Id);
        await cmd.ExecuteNonQueryAsync(ct);
        return IdentityResult.Success;
    }

    public async Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken ct)
    {
        if (!int.TryParse(userId, out var id)) return null;
        using var conn = _db.GetConnection();
        await conn.OpenAsync(ct);
        using var cmd = new MySqlCommand(
            "SELECT id, email, password_hash, first_name, last_name, role, is_active, created_at FROM users WHERE id=@id LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var r = await cmd.ExecuteReaderAsync(ct);
        return r.Read() ? MapUser(r) : null;
    }

    public async Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken ct)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync(ct);
        using var cmd = new MySqlCommand(
            "SELECT id, email, password_hash, first_name, last_name, role, is_active, created_at FROM users WHERE UPPER(email)=@un LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@un", normalizedUserName);
        using var r = await cmd.ExecuteReaderAsync(ct);
        return r.Read() ? MapUser(r) : null;
    }

    public async Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken ct)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync(ct);
        using var cmd = new MySqlCommand(
            "SELECT id, email, password_hash, first_name, last_name, role, is_active, created_at FROM users WHERE UPPER(email)=@email LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@email", normalizedEmail);
        using var r = await cmd.ExecuteReaderAsync(ct);
        return r.Read() ? MapUser(r) : null;
    }

    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken ct) =>
        Task.FromResult(user.Id.ToString());

    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken ct) =>
        Task.FromResult<string?>(user.UserName);

    public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken ct)
    {
        user.UserName = userName;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken ct) =>
        Task.FromResult<string?>(user.NormalizedUserName);

    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken ct)
    {
        user.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash, CancellationToken ct)
    {
        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken ct) =>
        Task.FromResult(user.PasswordHash);

    public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken ct) =>
        Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));

    public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken ct)
    {
        user.Email = email;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken ct) =>
        Task.FromResult(user.Email);

    public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken ct) =>
        Task.FromResult(true);

    public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken ct) =>
        Task.FromResult(user.NormalizedEmail);

    public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail, CancellationToken ct)
    {
        user.NormalizedEmail = normalizedEmail;
        return Task.CompletedTask;
    }

    public async Task AddToRoleAsync(ApplicationUser user, string roleName, CancellationToken ct)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync(ct);
        using var findRole = new MySqlCommand("SELECT id FROM roles WHERE normalized_name=@rn LIMIT 1", conn);
        findRole.Parameters.AddWithValue("@rn", roleName.ToUpperInvariant());
        var roleIdObj = await findRole.ExecuteScalarAsync(ct);
        if (roleIdObj == null) return;
        var roleId = Convert.ToInt32(roleIdObj);
        using var cmd = new MySqlCommand(
            "INSERT IGNORE INTO user_roles (user_id, role_id) VALUES (@uid, @rid)", conn);
        cmd.Parameters.AddWithValue("@uid", user.Id);
        cmd.Parameters.AddWithValue("@rid", roleId);
        await cmd.ExecuteNonQueryAsync(ct);

        await SyncRoleEnumAsync(user.Id, roleName, conn, ct);
    }

    public async Task RemoveFromRoleAsync(ApplicationUser user, string roleName, CancellationToken ct)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync(ct);
        using var findRole = new MySqlCommand("SELECT id FROM roles WHERE normalized_name=@rn LIMIT 1", conn);
        findRole.Parameters.AddWithValue("@rn", roleName.ToUpperInvariant());
        var roleIdObj = await findRole.ExecuteScalarAsync(ct);
        if (roleIdObj == null) return;
        var roleId = Convert.ToInt32(roleIdObj);
        using var cmd = new MySqlCommand(
            "DELETE FROM user_roles WHERE user_id=@uid AND role_id=@rid", conn);
        cmd.Parameters.AddWithValue("@uid", user.Id);
        cmd.Parameters.AddWithValue("@rid", roleId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken ct)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync(ct);
        using var cmd = new MySqlCommand(@"
            SELECT r.name FROM user_roles ur
            JOIN roles r ON r.id=ur.role_id
            WHERE ur.user_id=@uid", conn);
        cmd.Parameters.AddWithValue("@uid", user.Id);
        var roles = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (reader.Read()) roles.Add(reader.GetString(0));
        if (roles.Count == 0)
        {
            await reader.CloseAsync();
            using var fallback = new MySqlCommand("SELECT role FROM users WHERE id=@id LIMIT 1", conn);
            fallback.Parameters.AddWithValue("@id", user.Id);
            var r = await fallback.ExecuteScalarAsync(ct);
            if (r is string role && !string.IsNullOrEmpty(role)) roles.Add(role);
        }
        return roles;
    }

    public async Task<bool> IsInRoleAsync(ApplicationUser user, string roleName, CancellationToken ct)
    {
        var roles = await GetRolesAsync(user, ct);
        return roles.Contains(roleName, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName, CancellationToken ct)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync(ct);
        using var cmd = new MySqlCommand(@"
            SELECT u.id, u.email, u.password_hash, u.first_name, u.last_name, u.role, u.is_active, u.created_at
            FROM users u
            JOIN user_roles ur ON ur.user_id=u.id
            JOIN roles r ON r.id=ur.role_id
            WHERE r.normalized_name=@rn", conn);
        cmd.Parameters.AddWithValue("@rn", roleName.ToUpperInvariant());
        var list = new List<ApplicationUser>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (reader.Read()) list.Add(MapUser(reader));
        return list;
    }

    private static async Task SyncRoleEnumAsync(int userId, string roleName, MySqlConnection conn, CancellationToken ct)
    {
        var validRoles = new[] { "Student", "Teacher", "Admin" };
        var normalized = validRoles.FirstOrDefault(r => r.Equals(roleName, StringComparison.OrdinalIgnoreCase));
        if (normalized == null) return;
        using var cmd = new MySqlCommand("UPDATE users SET role=@role WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@role", normalized);
        cmd.Parameters.AddWithValue("@id", userId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static ApplicationUser MapUser(MySqlDataReader r) => new()
    {
        Id = r.GetInt32("id"),
        UserName = r.GetString("email"),
        NormalizedUserName = r.GetString("email").ToUpperInvariant(),
        Email = r.GetString("email"),
        NormalizedEmail = r.GetString("email").ToUpperInvariant(),
        PasswordHash = r.GetString("password_hash"),
        FirstName = r.GetString("first_name"),
        LastName = r.GetString("last_name"),
        IsActive = r.GetBoolean("is_active"),
        CreatedAt = r.GetDateTime("created_at")
    };

    public void Dispose() { }
}
