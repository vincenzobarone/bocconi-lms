using BocconiLMS.Models;
using MySqlConnector;

namespace BocconiLMS.Data;

public class PlatformRepository
{
    private readonly DbHelper _db;
    public PlatformRepository(DbHelper db) => _db = db;

    public async Task<List<Platform>> GetAllAsync()
    {
        var list = new List<Platform>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT p.id, p.name, p.sort_order,
                   (SELECT COUNT(*) FROM materials m WHERE m.platform_id = p.id) AS material_count
            FROM platforms p
            ORDER BY p.sort_order, p.name", conn);
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new Platform
            {
                Id            = r.GetInt32("id"),
                Name          = r.GetString("name"),
                SortOrder     = r.GetInt32("sort_order"),
                MaterialCount = r.GetInt32("material_count")
            });
        return list;
    }

    public async Task<int> CreateAsync(string name)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            INSERT INTO platforms (name, sort_order)
            SELECT @name, COALESCE(MAX(sort_order), 0) + 1 FROM platforms", conn);
        cmd.Parameters.AddWithValue("@name", name.Trim());
        await cmd.ExecuteNonQueryAsync();
        return await DbHelper.GetLastInsertIdAsync(conn);
    }

    public async Task<bool> NameExistsAsync(string name, int excludeId = 0)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM platforms WHERE name = @name AND id <> @ex", conn);
        cmd.Parameters.AddWithValue("@name", name.Trim());
        cmd.Parameters.AddWithValue("@ex", excludeId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task RenameAsync(int id, string name)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "UPDATE platforms SET name = @name WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@name", name.Trim());
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> CountMaterialsAsync(int id)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM materials WHERE platform_id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "DELETE FROM platforms WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }
}
