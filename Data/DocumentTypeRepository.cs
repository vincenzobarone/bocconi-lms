using BocconiLMS.Models;
using MySqlConnector;

namespace BocconiLMS.Data;

public class DocumentTypeRepository
{
    private readonly DbHelper _db;

    public DocumentTypeRepository(DbHelper db) => _db = db;

    public async Task<List<DocumentType>> GetAllAsync()
    {
        var list = new List<DocumentType>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT dt.id, dt.name, dt.sort_order,
                   COUNT(m.id) AS material_count
            FROM document_types dt
            LEFT JOIN materials m ON m.document_type_id = dt.id
            GROUP BY dt.id, dt.name, dt.sort_order
            ORDER BY dt.sort_order, dt.name", conn);
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(Map(r));
        return list;
    }

    public async Task<DocumentType?> GetByIdAsync(int id)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT dt.id, dt.name, dt.sort_order,
                   COUNT(m.id) AS material_count
            FROM document_types dt
            LEFT JOIN materials m ON m.document_type_id = dt.id
            WHERE dt.id = @id
            GROUP BY dt.id, dt.name, dt.sort_order", conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? Map(r) : null;
    }

    public async Task<int> CreateAsync(string name)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            INSERT INTO document_types (name, sort_order)
            SELECT @name, COALESCE(MAX(sort_order),0)+1 FROM document_types", conn);
        cmd.Parameters.AddWithValue("@name", name.Trim());
        await cmd.ExecuteNonQueryAsync();
        return await DbHelper.GetLastInsertIdAsync(conn);
    }

    public async Task UpdateAsync(int id, string name)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "UPDATE document_types SET name = @name WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@name", name.Trim());
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> NameExistsAsync(string name, int excludeId = 0)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM document_types WHERE name = @name AND id <> @excludeId", conn);
        cmd.Parameters.AddWithValue("@name", name.Trim());
        cmd.Parameters.AddWithValue("@excludeId", excludeId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task<int> CountMaterialsAsync(int id)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM materials WHERE document_type_id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "DELETE FROM document_types WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    private static DocumentType Map(MySqlDataReader r) => new()
    {
        Id = r.GetInt32("id"),
        Name = r.GetString("name"),
        SortOrder = r.GetInt32("sort_order"),
        MaterialCount = r.GetInt32("material_count")
    };
}
