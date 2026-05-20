using BocconiLMS.Models;
using MySqlConnector;

namespace BocconiLMS.Data;

public class AuthorRepository
{
    private readonly DbHelper _db;
    public AuthorRepository(DbHelper db) => _db = db;

    public async Task<List<Author>> GetAllAsync()
    {
        var list = new List<Author>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT a.id, a.full_name, a.email, a.affiliation, a.created_at,
                   COUNT(ma.material_id) AS material_count
            FROM authors a
            LEFT JOIN material_authors ma ON ma.author_id = a.id
            GROUP BY a.id, a.full_name, a.email, a.affiliation, a.created_at
            ORDER BY a.full_name", conn);
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(MapAuthor(r));
        return list;
    }

    public async Task<Author?> GetByIdAsync(int id)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT a.id, a.full_name, a.email, a.affiliation, a.created_at,
                   COUNT(ma.material_id) AS material_count
            FROM authors a
            LEFT JOIN material_authors ma ON ma.author_id = a.id
            WHERE a.id = @id
            GROUP BY a.id, a.full_name, a.email, a.affiliation, a.created_at", conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? MapAuthor(r) : null;
    }

    public async Task<List<Author>> GetByMaterialIdAsync(int materialId)
    {
        var list = new List<Author>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT a.id, a.full_name, a.email, a.affiliation, a.created_at, 0 AS material_count
            FROM material_authors ma
            JOIN authors a ON a.id = ma.author_id
            WHERE ma.material_id = @mid
            ORDER BY ma.sort_order, a.full_name", conn);
        cmd.Parameters.AddWithValue("@mid", materialId);
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(MapAuthor(r));
        return list;
    }

    public async Task<int> CreateAsync(string fullName, string? email, string? affiliation)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            INSERT INTO authors (full_name, email, affiliation)
            VALUES (@name, @email, @aff)", conn);
        cmd.Parameters.AddWithValue("@name", fullName.Trim());
        cmd.Parameters.AddWithValue("@email",  (object?)email?.Trim()       ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@aff",    (object?)affiliation?.Trim() ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
        return await DbHelper.GetLastInsertIdAsync(conn);
    }

    public async Task UpdateAsync(int id, string fullName, string? email, string? affiliation)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            UPDATE authors SET full_name = @name, email = @email, affiliation = @aff
            WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@name",  fullName.Trim());
        cmd.Parameters.AddWithValue("@email", (object?)email?.Trim()       ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@aff",   (object?)affiliation?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand("DELETE FROM authors WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> GetMaterialCountAsync(int authorId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM material_authors WHERE author_id = @id", conn);
        cmd.Parameters.AddWithValue("@id", authorId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task SetMaterialAuthorsAsync(int materialId, List<int> authorIds)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();
        try
        {
            using var del = new MySqlCommand(
                "DELETE FROM material_authors WHERE material_id = @mid", conn, tx);
            del.Parameters.AddWithValue("@mid", materialId);
            await del.ExecuteNonQueryAsync();

            for (int i = 0; i < authorIds.Count; i++)
            {
                using var ins = new MySqlCommand(@"
                    INSERT IGNORE INTO material_authors (material_id, author_id, sort_order)
                    VALUES (@mid, @aid, @sort)", conn, tx);
                ins.Parameters.AddWithValue("@mid",  materialId);
                ins.Parameters.AddWithValue("@aid",  authorIds[i]);
                ins.Parameters.AddWithValue("@sort", i);
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

    /// <summary>
    /// Cerca un autore per nome esatto (case-insensitive).
    /// Se non esiste lo crea. Restituisce (Id, FullName, wasCreated).
    /// </summary>
    public async Task<(int Id, string FullName, bool Created)> FindOrCreateByNameAsync(string fullName)
    {
        fullName = fullName.Trim();
        if (string.IsNullOrEmpty(fullName)) throw new ArgumentException("Name required.");

        using var conn = _db.GetConnection();
        await conn.OpenAsync();

        using (var cmd = new MySqlCommand(
            "SELECT id, full_name FROM authors WHERE full_name = @name LIMIT 1", conn))
        {
            cmd.Parameters.AddWithValue("@name", fullName);
            using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
                return (r.GetInt32(0), r.GetString(1), false);
        }

        using (var cmd = new MySqlCommand(
            "INSERT INTO authors (full_name) VALUES (@name)", conn))
        {
            cmd.Parameters.AddWithValue("@name", fullName);
            await cmd.ExecuteNonQueryAsync();
            var newId = await DbHelper.GetLastInsertIdAsync(conn);
            return (newId, fullName, true);
        }
    }

    public async Task<bool> NameExistsAsync(string fullName, int excludeId = 0)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM authors WHERE full_name = @name AND id <> @excludeId", conn);
        cmd.Parameters.AddWithValue("@name",      fullName.Trim());
        cmd.Parameters.AddWithValue("@excludeId", excludeId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    private static Author MapAuthor(MySqlDataReader r) => new()
    {
        Id            = r.GetInt32("id"),
        FullName      = r.GetString("full_name"),
        Email         = r.IsDBNull(r.GetOrdinal("email"))       ? null : r.GetString("email"),
        Affiliation   = r.IsDBNull(r.GetOrdinal("affiliation")) ? null : r.GetString("affiliation"),
        MaterialCount = Convert.ToInt32(r["material_count"])
    };
}
