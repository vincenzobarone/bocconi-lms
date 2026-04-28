using MySqlConnector;

namespace BocconiLMS.Data;

public class RolePermissionRepository
{
    private readonly DbHelper _db;
    public RolePermissionRepository(DbHelper db) => _db = db;

    public static readonly (string Key, string TranslationKey, bool CoursesOnly, bool MenuOnly)[] AllPermissions =
    {
        ("courses.teach",       "perm.courses_teach",       true,  false),
        ("courses.enroll",      "perm.courses_enroll",      true,  false),
        ("menu.materials",      "perm.menu_materials",      false, true),
        ("materials.create",    "perm.materials_create",    false, true),
        ("materials.edit",      "perm.materials_edit",      false, true),
        ("materials.approve",   "perm.materials_approve",   false, true),
        ("menu.users",          "perm.menu_users",          false, true),
        ("menu.translations",   "perm.menu_translations",   false, true),
    };

    public async Task<List<string>> GetRolePermissionsAsync(int roleId)
    {
        var list = new List<string>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT permission_key FROM role_permissions WHERE role_id = @rid", conn);
        cmd.Parameters.AddWithValue("@rid", roleId);
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(r.GetString(0));
        return list;
    }

    public async Task<bool> HasMenuPermissionAsync(string roleName, string permissionKey)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT COUNT(*) FROM role_permissions rp
            JOIN roles r ON r.id = rp.role_id
            WHERE r.normalized_name = @rn AND rp.permission_key = @pk", conn);
        cmd.Parameters.AddWithValue("@rn", roleName.ToUpperInvariant());
        cmd.Parameters.AddWithValue("@pk", permissionKey);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task SetRolePermissionsAsync(int roleId, List<string> permissions)
    {
        var valid = new HashSet<string>(AllPermissions.Select(p => p.Key));
        var toSet = permissions.Where(p => valid.Contains(p)).Distinct().ToList();

        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();
        try
        {
            using var del = new MySqlCommand(
                "DELETE FROM role_permissions WHERE role_id = @rid", conn, tx);
            del.Parameters.AddWithValue("@rid", roleId);
            await del.ExecuteNonQueryAsync();

            foreach (var perm in toSet)
            {
                using var ins = new MySqlCommand(
                    "INSERT IGNORE INTO role_permissions (role_id, permission_key) VALUES (@rid, @pk)", conn, tx);
                ins.Parameters.AddWithValue("@rid", roleId);
                ins.Parameters.AddWithValue("@pk", perm);
                await ins.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
