using MySqlConnector;
using BocconiLMS.Models;

namespace BocconiLMS.Data;

public class AreaRepository
{
    private readonly DbHelper _db;
    public AreaRepository(DbHelper db) => _db = db;

    public async Task<List<Area>> GetAllAsync()
    {
        var list = new List<Area>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT a.id, a.name, a.sort_order,
                   (SELECT COUNT(*) FROM user_areas ua WHERE ua.area_id = a.id) AS user_count
            FROM areas a
            ORDER BY a.sort_order, a.name", conn);
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(Map(r));
        return list;
    }

    public async Task<List<int>> GetUserAreaIdsAsync(int userId)
    {
        var ids = new List<int>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT area_id FROM user_areas WHERE user_id = @uid", conn);
        cmd.Parameters.AddWithValue("@uid", userId);
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            ids.Add(r.GetInt32(0));
        return ids;
    }

    public async Task<List<Area>> GetUserAreasAsync(int userId)
    {
        var list = new List<Area>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT a.id, a.name, a.sort_order, 0 AS user_count
            FROM areas a
            INNER JOIN user_areas ua ON ua.area_id = a.id
            WHERE ua.user_id = @uid
            ORDER BY a.sort_order, a.name", conn);
        cmd.Parameters.AddWithValue("@uid", userId);
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(Map(r));
        return list;
    }

    public async Task SetUserAreasAsync(int userId, List<int> areaIds)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();
        try
        {
            using var del = new MySqlCommand(
                "DELETE FROM user_areas WHERE user_id = @uid", conn, tx);
            del.Parameters.AddWithValue("@uid", userId);
            await del.ExecuteNonQueryAsync();

            foreach (var areaId in areaIds.Distinct())
            {
                using var ins = new MySqlCommand(
                    "INSERT IGNORE INTO user_areas (user_id, area_id) VALUES (@uid, @aid)", conn, tx);
                ins.Parameters.AddWithValue("@uid", userId);
                ins.Parameters.AddWithValue("@aid", areaId);
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

    public async Task<int> CreateAsync(string name)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            INSERT INTO areas (name, sort_order)
            SELECT @name, COALESCE(MAX(sort_order), 0) + 1 FROM areas", conn);
        cmd.Parameters.AddWithValue("@name", name.Trim());
        await cmd.ExecuteNonQueryAsync();
        return await DbHelper.GetLastInsertIdAsync(conn);
    }

    public async Task<bool> NameExistsAsync(string name, int excludeId = 0)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM areas WHERE name = @name AND id <> @ex", conn);
        cmd.Parameters.AddWithValue("@name", name.Trim());
        cmd.Parameters.AddWithValue("@ex", excludeId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task<int> CountUsersAsync(int id)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM user_areas WHERE area_id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();
        try
        {
            using var del1 = new MySqlCommand(
                "DELETE FROM user_areas WHERE area_id = @id", conn, tx);
            del1.Parameters.AddWithValue("@id", id);
            await del1.ExecuteNonQueryAsync();

            using var del2 = new MySqlCommand(
                "DELETE FROM areas WHERE id = @id", conn, tx);
            del2.Parameters.AddWithValue("@id", id);
            await del2.ExecuteNonQueryAsync();

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private static Area Map(MySqlDataReader r) => new()
    {
        Id = r.GetInt32("id"),
        Name = r.GetString("name"),
        SortOrder = r.GetInt32("sort_order"),
        UserCount = r.GetInt32("user_count")
    };
}
