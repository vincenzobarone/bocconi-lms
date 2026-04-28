using BocconiLMS.Models;
using MySqlConnector;

namespace BocconiLMS.Data;

public class MaterialRepository
{
    private readonly DbHelper _db;

    public MaterialRepository(DbHelper db) => _db = db;

    // ── Core CRUD ────────────────────────────────────────────────────────

    public async Task<List<Material>> GetAllAsync(
        string? searchTitle = null,
        string? language = null,
        int? documentTypeId = null)
    {
        var list = new List<Material>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(searchTitle))
            where.Add("m.title LIKE @title");
        if (!string.IsNullOrWhiteSpace(language))
            where.Add("m.language = @lang");
        if (documentTypeId.HasValue)
            where.Add("m.document_type_id = @typeId");

        var sql = $@"
            SELECT m.id, m.title, m.author_name, m.owner_id, m.language, m.document_type_id, m.created_at,
                   m.status, m.protocol_number, m.folder,
                   CONCAT(u.first_name,' ',u.last_name) AS owner_name,
                   dt.name AS type_name,
                   COALESCE(mv.version_number,0) AS current_version,
                   mv.id AS ver_id, mv.file_name, mv.file_path,
                   mv.file_type, mv.file_size_bytes, mv.uploaded_at
            FROM materials m
            LEFT JOIN users u ON u.id = m.owner_id
            LEFT JOIN document_types dt ON dt.id = m.document_type_id
            LEFT JOIN material_versions mv ON mv.material_id = m.id AND mv.is_active = 1
            {(where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "")}
            ORDER BY m.title";

        using var cmd = new MySqlCommand(sql, conn);
        if (!string.IsNullOrWhiteSpace(searchTitle))
            cmd.Parameters.AddWithValue("@title", $"%{searchTitle}%");
        if (!string.IsNullOrWhiteSpace(language))
            cmd.Parameters.AddWithValue("@lang", language);
        if (documentTypeId.HasValue)
            cmd.Parameters.AddWithValue("@typeId", documentTypeId.Value);

        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(MapWithVersion(r));
        return list;
    }

    public async Task<Material?> GetByIdAsync(int id)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT m.id, m.title, m.author_name, m.owner_id, m.language, m.document_type_id, m.created_at,
                   m.status, m.protocol_number, m.folder,
                   CONCAT(u.first_name,' ',u.last_name) AS owner_name,
                   dt.name AS type_name,
                   COALESCE(mv.version_number,0) AS current_version,
                   mv.id AS ver_id, mv.file_name, mv.file_path,
                   mv.file_type, mv.file_size_bytes, mv.uploaded_at
            FROM materials m
            LEFT JOIN users u ON u.id = m.owner_id
            LEFT JOIN document_types dt ON dt.id = m.document_type_id
            LEFT JOIN material_versions mv ON mv.material_id = m.id AND mv.is_active = 1
            WHERE m.id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? MapWithVersion(r) : null;
    }

    public async Task<bool> TitleExistsAsync(string title, int excludeId = 0)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM materials WHERE title = @title AND id <> @excludeId", conn);
        cmd.Parameters.AddWithValue("@title", title.Trim());
        cmd.Parameters.AddWithValue("@excludeId", excludeId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task<int> CreateAsync(string title, string? authorName, int? ownerId, string language, int? documentTypeId, string status = "bozza", string? folder = null)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            INSERT INTO materials (title, author_name, owner_id, language, document_type_id, status, folder)
            VALUES (@title, @authorName, @ownerId, @lang, @typeId, @status, @folder)", conn);
        cmd.Parameters.AddWithValue("@title", title.Trim());
        cmd.Parameters.AddWithValue("@authorName", (object?)authorName?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ownerId", (object?)ownerId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@lang", language);
        cmd.Parameters.AddWithValue("@typeId", (object?)documentTypeId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@folder", (object?)folder?.Trim() ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
        return await DbHelper.GetLastInsertIdAsync(conn);
    }

    public async Task UpdateAsync(int id, string title, string? authorName, int? ownerId, string language, int? documentTypeId, string status, string? folder = null)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();

        string? newProtocolNumber = null;
        if (status == "verificato")
        {
            using var checkCmd = new MySqlCommand(
                "SELECT protocol_number FROM materials WHERE id = @id", conn);
            checkCmd.Parameters.AddWithValue("@id", id);
            var existing = await checkCmd.ExecuteScalarAsync();
            if (existing == DBNull.Value || existing == null)
            {
                var year = DateTime.Now.Year;
                using var seqCmd = new MySqlCommand(
                    "SELECT COALESCE(MAX(CAST(SUBSTRING_INDEX(protocol_number,'-',-1) AS UNSIGNED)),0)+1 " +
                    "FROM materials WHERE protocol_number LIKE @pattern", conn);
                seqCmd.Parameters.AddWithValue("@pattern", $"PROT-{year}-%");
                var seq = Convert.ToInt32(await seqCmd.ExecuteScalarAsync());
                newProtocolNumber = $"PROT-{year}-{seq:D4}";
            }
        }

        using var cmd = new MySqlCommand(@"
            UPDATE materials SET title = @title, author_name = @authorName, owner_id = @ownerId,
                language = @lang, document_type_id = @typeId,
                status = @status, folder = @folder,
                protocol_number = CASE
                    WHEN @proto IS NOT NULL THEN @proto
                    ELSE protocol_number
                END
            WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@title", title.Trim());
        cmd.Parameters.AddWithValue("@authorName", (object?)authorName?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ownerId", (object?)ownerId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@lang", language);
        cmd.Parameters.AddWithValue("@typeId", (object?)documentTypeId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@folder", (object?)folder?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@proto", (object?)newProtocolNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand("DELETE FROM materials WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Versions ─────────────────────────────────────────────────────────

    public async Task<List<MaterialVersion>> GetVersionsAsync(int materialId)
    {
        var list = new List<MaterialVersion>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT mv.*, CONCAT(u.first_name,' ',u.last_name) AS uploader_name
            FROM material_versions mv
            LEFT JOIN users u ON u.id = mv.uploaded_by
            WHERE mv.material_id = @id
            ORDER BY mv.version_number DESC", conn);
        cmd.Parameters.AddWithValue("@id", materialId);
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(MapVersion(r));
        return list;
    }

    public async Task<int> GetNextVersionNumberAsync(int materialId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT COALESCE(MAX(version_number),0) + 1 FROM material_versions WHERE material_id = @id", conn);
        cmd.Parameters.AddWithValue("@id", materialId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<int> AddVersionAsync(MaterialVersion v)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();

        using var deactivate = new MySqlCommand(
            "UPDATE material_versions SET is_active = 0 WHERE material_id = @id", conn);
        deactivate.Parameters.AddWithValue("@id", v.MaterialId);
        await deactivate.ExecuteNonQueryAsync();

        using var insert = new MySqlCommand(@"
            INSERT INTO material_versions
                (material_id, version_number, file_name, file_path, file_type, file_size_bytes, uploaded_by, notes, is_active)
            VALUES
                (@mid, @ver, @fname, @fpath, @ftype, @fsize, @uploader, @notes, 1)", conn);
        insert.Parameters.AddWithValue("@mid", v.MaterialId);
        insert.Parameters.AddWithValue("@ver", v.VersionNumber);
        insert.Parameters.AddWithValue("@fname", v.FileName);
        insert.Parameters.AddWithValue("@fpath", v.FilePath);
        insert.Parameters.AddWithValue("@ftype", v.FileType);
        insert.Parameters.AddWithValue("@fsize", v.FileSizeBytes);
        insert.Parameters.AddWithValue("@uploader", v.UploadedBy);
        insert.Parameters.AddWithValue("@notes", (object?)v.Notes ?? DBNull.Value);
        await insert.ExecuteNonQueryAsync();
        return await DbHelper.GetLastInsertIdAsync(conn);
    }

    public async Task RestoreVersionAsync(int materialId, int versionId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var d = new MySqlCommand(
            "UPDATE material_versions SET is_active = 0 WHERE material_id = @mid", conn);
        d.Parameters.AddWithValue("@mid", materialId);
        await d.ExecuteNonQueryAsync();
        using var a = new MySqlCommand(
            "UPDATE material_versions SET is_active = 1 WHERE id = @vid", conn);
        a.Parameters.AddWithValue("@vid", versionId);
        await a.ExecuteNonQueryAsync();
    }

    public async Task<MaterialVersion?> GetVersionByIdAsync(int versionId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT mv.*, CONCAT(u.first_name,' ',u.last_name) AS uploader_name
            FROM material_versions mv
            LEFT JOIN users u ON u.id = mv.uploaded_by
            WHERE mv.id = @id", conn);
        cmd.Parameters.AddWithValue("@id", versionId);
        using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? MapVersion(r) : null;
    }

    // ── Lesson links ─────────────────────────────────────────────────────

    public async Task<List<Material>> GetByLessonAsync(int lessonId)
    {
        var list = new List<Material>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT m.id, m.title, m.author_name, m.owner_id, m.language, m.document_type_id, m.created_at,
                   m.status, m.protocol_number, m.folder,
                   CONCAT(u.first_name,' ',u.last_name) AS owner_name,
                   dt.name AS type_name,
                   COALESCE(mv.version_number,0) AS current_version,
                   mv.id AS ver_id, mv.file_name, mv.file_path,
                   mv.file_type, mv.file_size_bytes, mv.uploaded_at
            FROM lesson_materials lm
            JOIN materials m ON m.id = lm.material_id
            LEFT JOIN users u ON u.id = m.owner_id
            LEFT JOIN document_types dt ON dt.id = m.document_type_id
            LEFT JOIN material_versions mv ON mv.material_id = m.id AND mv.is_active = 1
            WHERE lm.lesson_id = @lid
            ORDER BY m.title", conn);
        cmd.Parameters.AddWithValue("@lid", lessonId);
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(MapWithVersion(r));
        return list;
    }

    public async Task LinkToLessonAsync(int lessonId, int materialId, int addedBy)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            INSERT IGNORE INTO lesson_materials (lesson_id, material_id, added_by)
            VALUES (@lid, @mid, @by)", conn);
        cmd.Parameters.AddWithValue("@lid", lessonId);
        cmd.Parameters.AddWithValue("@mid", materialId);
        cmd.Parameters.AddWithValue("@by", addedBy);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UnlinkFromLessonAsync(int lessonId, int materialId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "DELETE FROM lesson_materials WHERE lesson_id = @lid AND material_id = @mid", conn);
        cmd.Parameters.AddWithValue("@lid", lessonId);
        cmd.Parameters.AddWithValue("@mid", materialId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Material>> GetNotLinkedToLessonAsync(int lessonId)
    {
        var list = new List<Material>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT m.id, m.title, m.author_name, m.owner_id, m.language, m.document_type_id, m.created_at,
                   m.status, m.protocol_number, m.folder,
                   CONCAT(u.first_name,' ',u.last_name) AS owner_name,
                   dt.name AS type_name,
                   COALESCE(mv.version_number,0) AS current_version,
                   mv.id AS ver_id, mv.file_name, mv.file_path,
                   mv.file_type, mv.file_size_bytes, mv.uploaded_at
            FROM materials m
            LEFT JOIN users u ON u.id = m.owner_id
            LEFT JOIN document_types dt ON dt.id = m.document_type_id
            LEFT JOIN material_versions mv ON mv.material_id = m.id AND mv.is_active = 1
            WHERE m.id NOT IN (
                SELECT material_id FROM lesson_materials WHERE lesson_id = @lid
            )
            ORDER BY m.title", conn);
        cmd.Parameters.AddWithValue("@lid", lessonId);
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(MapWithVersion(r));
        return list;
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private static Material MapWithVersion(MySqlDataReader r)
    {
        var m = new Material
        {
            Id = r.GetInt32("id"),
            Title = r.GetString("title"),
            AuthorName = r.IsDBNull(r.GetOrdinal("author_name")) ? null : r.GetString("author_name"),
            OwnerId = r.IsDBNull(r.GetOrdinal("owner_id")) ? null : r.GetInt32("owner_id"),
            OwnerName = r.IsDBNull(r.GetOrdinal("owner_name")) ? "" : r.GetString("owner_name"),
            Folder = r.IsDBNull(r.GetOrdinal("folder")) ? null : r.GetString("folder"),
            Language = r.GetString("language"),
            DocumentTypeId = r.IsDBNull(r.GetOrdinal("document_type_id")) ? null : r.GetInt32("document_type_id"),
            DocumentTypeName = r.IsDBNull(r.GetOrdinal("type_name")) ? "" : r.GetString("type_name"),
            CreatedAt = r.GetDateTime("created_at"),
            Status = r.IsDBNull(r.GetOrdinal("status")) ? "bozza" : r.GetString("status"),
            ProtocolNumber = r.IsDBNull(r.GetOrdinal("protocol_number")) ? null : r.GetString("protocol_number"),
            CurrentVersion = r.GetInt32("current_version")
        };
        if (!r.IsDBNull(r.GetOrdinal("ver_id")))
        {
            m.ActiveVersion = new MaterialVersion
            {
                Id = r.GetInt32("ver_id"),
                MaterialId = m.Id,
                VersionNumber = m.CurrentVersion,
                FileName = r.GetString("file_name"),
                FilePath = r.GetString("file_path"),
                FileType = r.GetString("file_type"),
                FileSizeBytes = r.GetInt64("file_size_bytes"),
                UploadedAt = r.GetDateTime("uploaded_at"),
                IsActive = true
            };
        }
        return m;
    }

    private static MaterialVersion MapVersion(MySqlDataReader r) => new()
    {
        Id = r.GetInt32("id"),
        MaterialId = r.GetInt32("material_id"),
        VersionNumber = r.GetInt32("version_number"),
        FileName = r.GetString("file_name"),
        FilePath = r.GetString("file_path"),
        FileType = r.GetString("file_type"),
        FileSizeBytes = r.GetInt64("file_size_bytes"),
        UploadedBy = r.GetInt32("uploaded_by"),
        UploaderName = r.IsDBNull(r.GetOrdinal("uploader_name")) ? "" : r.GetString("uploader_name"),
        Notes = r.IsDBNull(r.GetOrdinal("notes")) ? null : r.GetString("notes"),
        IsActive = r.GetBoolean("is_active"),
        UploadedAt = r.GetDateTime("uploaded_at")
    };
}
