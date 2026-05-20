using BocconiLMS.Models;
using MySqlConnector;

namespace BocconiLMS.Data;

public class MaterialRepository
{
    private readonly DbHelper _db;

    public MaterialRepository(DbHelper db) => _db = db;

    // ── Folders ───────────────────────────────────────────────────────────

    public async Task<List<MaterialFolder>> GetAllFoldersAsync()
    {
        var list = new List<MaterialFolder>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT mf.id, mf.name, mf.created_at, mf.created_by,
                   CONCAT(u.first_name, ' ', u.last_name) AS created_by_name
            FROM material_folders mf
            LEFT JOIN users u ON u.id = mf.created_by
            ORDER BY mf.name", conn);
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new MaterialFolder
            {
                Id            = r.GetInt32("id"),
                Name          = r.GetString("name"),
                CreatedAt     = r.GetDateTime("created_at"),
                CreatedById   = r.IsDBNull(r.GetOrdinal("created_by")) ? null : r.GetInt32("created_by"),
                CreatedByName = r.IsDBNull(r.GetOrdinal("created_by_name")) ? null : r.GetString("created_by_name").Trim()
            });
        return list;
    }

    public async Task<int> GetOrCreateFolderAsync(string name, int? createdBy = null)
    {
        name = name.Trim();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();

        using var find = new MySqlCommand(
            "SELECT id FROM material_folders WHERE name = @name COLLATE utf8mb4_unicode_ci", conn);
        find.Parameters.AddWithValue("@name", name);
        var existing = await find.ExecuteScalarAsync();
        if (existing != null && existing != DBNull.Value)
            return Convert.ToInt32(existing);

        using var ins = new MySqlCommand(
            "INSERT INTO material_folders (name, created_by) VALUES (@name, @createdBy)", conn);
        ins.Parameters.AddWithValue("@name", name);
        ins.Parameters.AddWithValue("@createdBy", (object?)createdBy ?? DBNull.Value);
        await ins.ExecuteNonQueryAsync();
        return await DbHelper.GetLastInsertIdAsync(conn);
    }

    // ── Protocol ──────────────────────────────────────────────────────────

    private const string ProtocolPrefix = "d-";

    public async Task<string> GetNextProtocolCodeAsync()
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT COALESCE(MAX(CAST(SUBSTRING_INDEX(protocol_code, '-', -1) AS UNSIGNED)), 0) + 1
            FROM materials
            WHERE protocol_code IS NOT NULL", conn);
        var next = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return $"{ProtocolPrefix}{next}";
    }

    // ── Core CRUD ────────────────────────────────────────────────────────

    public async Task<List<Material>> GetAllAsync(
        string? searchTitle = null,
        string? language = null,
        int? documentTypeId = null,
        int? catalogationYear = null,
        int? modifiedYear = null,
        string? folderName = null,
        int? folderId = null)
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
        if (catalogationYear.HasValue)
            where.Add("YEAR(m.catalogation_date) = @catYear");
        if (modifiedYear.HasValue)
            where.Add("YEAR(m.updated_at) = @modYear");
        if (!string.IsNullOrWhiteSpace(folderName))
            where.Add("mf.name LIKE @folderName");
        if (folderId.HasValue)
            where.Add("m.folder_id = @folderId");

        var sql = $@"
            SELECT m.id, m.title, m.owner_id, m.language, m.document_type_id, m.created_at,
                   m.status, m.protocol_code, m.old_protocol, m.folder_id, mf.name AS folder_name,
                   m.area_id, m.catalogation_date, m.last_update, m.page_count,
                   m.is_publishable, m.external_protocol_code, m.platform_id,
                   m.is_published, m.external_link, m.course_code,
                   CONCAT(u.first_name,' ',u.last_name) AS owner_name,
                   dt.name AS type_name,
                   a.name AS area_name,
                   p.name AS platform_name,
                   COALESCE(mv.version_number,0) AS current_version,
                   mv.id AS ver_id, mv.file_name, mv.file_path,
                   mv.file_type, mv.file_size_bytes, mv.uploaded_at,
                   (SELECT GROUP_CONCAT(au.full_name ORDER BY ma.sort_order SEPARATOR ', ')
                    FROM material_authors ma JOIN authors au ON au.id = ma.author_id
                    WHERE ma.material_id = m.id) AS authors_display
            FROM materials m
            LEFT JOIN users u ON u.id = m.owner_id
            LEFT JOIN document_types dt ON dt.id = m.document_type_id
            LEFT JOIN areas a ON a.id = m.area_id
            LEFT JOIN material_folders mf ON mf.id = m.folder_id
            LEFT JOIN platforms p ON p.id = m.platform_id
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
        if (catalogationYear.HasValue)
            cmd.Parameters.AddWithValue("@catYear", catalogationYear.Value);
        if (modifiedYear.HasValue)
            cmd.Parameters.AddWithValue("@modYear", modifiedYear.Value);
        if (!string.IsNullOrWhiteSpace(folderName))
            cmd.Parameters.AddWithValue("@folderName", $"%{folderName}%");
        if (folderId.HasValue)
            cmd.Parameters.AddWithValue("@folderId", folderId.Value);

        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(MapWithVersion(r));
        return list;
    }

    public async Task<Material?> GetByIdAsync(int id)
    {
        Material? material;
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT m.id, m.title, m.owner_id, m.language, m.document_type_id, m.created_at,
                       m.status, m.protocol_code, m.old_protocol, m.folder_id, mf.name AS folder_name,
                       m.area_id, m.catalogation_date, m.last_update, m.page_count,
                       m.is_publishable, m.external_protocol_code, m.platform_id,
                       m.is_published, m.external_link, m.course_code,
                       CONCAT(u.first_name,' ',u.last_name) AS owner_name,
                       dt.name AS type_name,
                       a.name AS area_name,
                       p.name AS platform_name,
                       COALESCE(mv.version_number,0) AS current_version,
                       mv.id AS ver_id, mv.file_name, mv.file_path,
                       mv.file_type, mv.file_size_bytes, mv.uploaded_at,
                       (SELECT GROUP_CONCAT(au.full_name ORDER BY ma.sort_order SEPARATOR ', ')
                        FROM material_authors ma JOIN authors au ON au.id = ma.author_id
                        WHERE ma.material_id = m.id) AS authors_display
                FROM materials m
                LEFT JOIN users u ON u.id = m.owner_id
                LEFT JOIN document_types dt ON dt.id = m.document_type_id
                LEFT JOIN areas a ON a.id = m.area_id
                LEFT JOIN material_folders mf ON mf.id = m.folder_id
                LEFT JOIN platforms p ON p.id = m.platform_id
                LEFT JOIN material_versions mv ON mv.material_id = m.id AND mv.is_active = 1
                WHERE m.id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            using var r = await cmd.ExecuteReaderAsync();
            material = await r.ReadAsync() ? MapWithVersion(r) : null;
        }

        if (material is null) return null;

        // Populate the typed Authors list (distinct from AuthorsDisplay summary string)
        using var conn2 = _db.GetConnection();
        await conn2.OpenAsync();
        using var authCmd = new MySqlCommand(@"
            SELECT a.id, a.full_name, a.email, a.affiliation, a.created_at, 0 AS material_count
            FROM   material_authors ma
            JOIN   authors a ON a.id = ma.author_id
            WHERE  ma.material_id = @mid
            ORDER BY ma.sort_order, a.full_name", conn2);
        authCmd.Parameters.AddWithValue("@mid", id);
        using var ar = await authCmd.ExecuteReaderAsync();
        while (await ar.ReadAsync())
            material.Authors.Add(new Author
            {
                Id          = ar.GetInt32("id"),
                FullName    = ar.GetString("full_name"),
                Email       = ar.IsDBNull(ar.GetOrdinal("email"))       ? null : ar.GetString("email"),
                Affiliation = ar.IsDBNull(ar.GetOrdinal("affiliation")) ? null : ar.GetString("affiliation"),
            });

        return material;
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

    public async Task<List<(int Id, string Title, string Status)>> SearchSimilarTitlesAsync(string title, int limit = 6)
    {
        var list = new List<(int, string, string)>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT id, title, status FROM materials WHERE title LIKE @pat ORDER BY title LIMIT @lim", conn);
        cmd.Parameters.AddWithValue("@pat", $"%{title}%");
        cmd.Parameters.AddWithValue("@lim", limit);
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add((r.GetInt32(0), r.GetString(1), r.GetString(2)));
        return list;
    }

    public async Task<int> CreateAsync(
        string title, int? ownerId, string language,
        int? documentTypeId, string status = "draft",
        int? folderId = null, int? areaId = null, DateTime? catalogationDate = null,
        DateTime? lastUpdate = null,
        string? protocolCode = null, int? pageCount = null,
        bool isPublishable = false, string? externalProtocolCode = null,
        int? platformId = null, string? externalLink = null, string? courseCode = null,
        string? oldProtocol = null)
    {
        bool isPublished = isPublishable && !string.IsNullOrWhiteSpace(externalProtocolCode);
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            INSERT INTO materials
                (title, owner_id, language, document_type_id,
                 status, folder_id, area_id, catalogation_date, last_update, protocol_code, old_protocol, page_count,
                 is_publishable, external_protocol_code, platform_id, is_published, external_link, course_code)
            VALUES
                (@title, @ownerId, @lang, @typeId,
                 @status, @folderId, @areaId, @catDate, @lastUpdate, @proto, @oldProto, @pageCount,
                 @isPublishable, @extProto, @platformId, @isPublished, @extLink, @courseCode)", conn);
        cmd.Parameters.AddWithValue("@title", title.Trim());
        cmd.Parameters.AddWithValue("@ownerId", (object?)ownerId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@lang", language);
        cmd.Parameters.AddWithValue("@typeId", (object?)documentTypeId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@folderId", (object?)folderId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@areaId", (object?)areaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@catDate", (object?)catalogationDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@lastUpdate", (object?)lastUpdate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@proto", (object?)protocolCode?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@oldProto", (object?)oldProtocol?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@pageCount", (object?)pageCount ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@isPublishable", isPublishable);
        cmd.Parameters.AddWithValue("@extProto", (object?)externalProtocolCode?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@platformId", (object?)platformId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@isPublished", isPublished);
        cmd.Parameters.AddWithValue("@extLink", (object?)externalLink?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@courseCode", (object?)courseCode?.Trim() ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
        return await DbHelper.GetLastInsertIdAsync(conn);
    }

    public async Task UpdateAsync(
        int id, string title, int? ownerId, string language,
        int? documentTypeId, string status,
        int? folderId = null, int? areaId = null, DateTime? catalogationDate = null,
        DateTime? lastUpdate = null,
        string? protocolCode = null, int? pageCount = null,
        bool isPublishable = false, string? externalProtocolCode = null,
        int? platformId = null, string? externalLink = null, string? courseCode = null)
    {
        bool isPublished = isPublishable && !string.IsNullOrWhiteSpace(externalProtocolCode);
        using var conn = _db.GetConnection();
        await conn.OpenAsync();

        using var cmd = new MySqlCommand(@"
            UPDATE materials SET
                title                  = @title,
                owner_id               = @ownerId,
                language               = @lang,
                document_type_id       = @typeId,
                status                 = @status,
                folder_id              = CASE WHEN @folderId IS NOT NULL THEN @folderId ELSE folder_id END,
                area_id                = @areaId,
                catalogation_date      = @catDate,
                last_update            = @lastUpdate,
                protocol_code          = CASE WHEN @proto IS NOT NULL THEN @proto ELSE protocol_code END,
                page_count             = CASE WHEN @pageCount IS NOT NULL THEN @pageCount ELSE page_count END,
                is_publishable         = @isPublishable,
                external_protocol_code = @extProto,
                platform_id            = @platformId,
                is_published           = @isPublished,
                external_link          = @extLink,
                course_code            = @courseCode
            WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@title", title.Trim());
        cmd.Parameters.AddWithValue("@ownerId", (object?)ownerId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@lang", language);
        cmd.Parameters.AddWithValue("@typeId", (object?)documentTypeId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@folderId", (object?)folderId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@areaId", (object?)areaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@catDate", (object?)catalogationDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@lastUpdate", (object?)lastUpdate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@proto", (object?)protocolCode?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@pageCount", (object?)pageCount ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@isPublishable", isPublishable);
        cmd.Parameters.AddWithValue("@extProto", (object?)externalProtocolCode?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@platformId", (object?)platformId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@isPublished", isPublished);
        cmd.Parameters.AddWithValue("@extLink", (object?)externalLink?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@courseCode", (object?)courseCode?.Trim() ?? DBNull.Value);
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

    public async Task DeleteVersionAsync(int versionId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "DELETE FROM material_versions WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@id", versionId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> CountVersionsAsync(int materialId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM material_versions WHERE material_id = @mid", conn);
        cmd.Parameters.AddWithValue("@mid", materialId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    // ── Lesson links ─────────────────────────────────────────────────────

    public async Task<List<Material>> GetByLessonAsync(int lessonId)
    {
        var list = new List<Material>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT m.id, m.title, m.owner_id, m.language, m.document_type_id, m.created_at,
                   m.status, m.protocol_code, m.old_protocol, m.folder_id, mf.name AS folder_name,
                   m.area_id, m.catalogation_date, m.last_update, m.page_count,
                   m.is_publishable, m.external_protocol_code, m.platform_id,
                   m.is_published, m.external_link, m.course_code,
                   CONCAT(u.first_name,' ',u.last_name) AS owner_name,
                   dt.name AS type_name,
                   a.name AS area_name,
                   p.name AS platform_name,
                   COALESCE(mv.version_number,0) AS current_version,
                   mv.id AS ver_id, mv.file_name, mv.file_path,
                   mv.file_type, mv.file_size_bytes, mv.uploaded_at,
                   (SELECT GROUP_CONCAT(au.full_name ORDER BY ma.sort_order SEPARATOR ', ')
                    FROM material_authors ma JOIN authors au ON au.id = ma.author_id
                    WHERE ma.material_id = m.id) AS authors_display
            FROM lesson_materials lm
            JOIN materials m ON m.id = lm.material_id
            LEFT JOIN users u ON u.id = m.owner_id
            LEFT JOIN document_types dt ON dt.id = m.document_type_id
            LEFT JOIN areas a ON a.id = m.area_id
            LEFT JOIN material_folders mf ON mf.id = m.folder_id
            LEFT JOIN platforms p ON p.id = m.platform_id
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
            SELECT m.id, m.title, m.owner_id, m.language, m.document_type_id, m.created_at,
                   m.status, m.protocol_code, m.old_protocol, m.folder_id, mf.name AS folder_name,
                   m.area_id, m.catalogation_date, m.last_update, m.page_count,
                   m.is_publishable, m.external_protocol_code, m.platform_id,
                   m.is_published, m.external_link, m.course_code,
                   CONCAT(u.first_name,' ',u.last_name) AS owner_name,
                   dt.name AS type_name,
                   a.name AS area_name,
                   p.name AS platform_name,
                   COALESCE(mv.version_number,0) AS current_version,
                   mv.id AS ver_id, mv.file_name, mv.file_path,
                   mv.file_type, mv.file_size_bytes, mv.uploaded_at,
                   (SELECT GROUP_CONCAT(au.full_name ORDER BY ma.sort_order SEPARATOR ', ')
                    FROM material_authors ma JOIN authors au ON au.id = ma.author_id
                    WHERE ma.material_id = m.id) AS authors_display
            FROM materials m
            LEFT JOIN users u ON u.id = m.owner_id
            LEFT JOIN document_types dt ON dt.id = m.document_type_id
            LEFT JOIN areas a ON a.id = m.area_id
            LEFT JOIN material_folders mf ON mf.id = m.folder_id
            LEFT JOIN platforms p ON p.id = m.platform_id
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

    public async Task<int> GetLessonCountForMaterialAsync(int materialId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM lesson_materials WHERE material_id = @id", conn);
        cmd.Parameters.AddWithValue("@id", materialId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<Dictionary<int, int>> GetLessonCountsAsync()
    {
        var result = new Dictionary<int, int>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT material_id, COUNT(*) AS cnt FROM lesson_materials GROUP BY material_id", conn);
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            result[r.GetInt32("material_id")] = (int)r.GetInt64("cnt");
        return result;
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private static Material MapWithVersion(MySqlDataReader r)
    {
        var m = new Material
        {
            Id = r.GetInt32("id"),
            Title = r.GetString("title"),
            AuthorsDisplay = r.IsDBNull(r.GetOrdinal("authors_display")) ? "" : r.GetString("authors_display"),
            OwnerId = r.IsDBNull(r.GetOrdinal("owner_id")) ? null : r.GetInt32("owner_id"),
            OwnerName = r.IsDBNull(r.GetOrdinal("owner_name")) ? "" : r.GetString("owner_name"),
            FolderId = r.IsDBNull(r.GetOrdinal("folder_id")) ? null : r.GetInt32("folder_id"),
            FolderName = r.IsDBNull(r.GetOrdinal("folder_name")) ? "" : r.GetString("folder_name"),
            Language = r.GetString("language"),
            DocumentTypeId = r.IsDBNull(r.GetOrdinal("document_type_id")) ? null : r.GetInt32("document_type_id"),
            DocumentTypeName = r.IsDBNull(r.GetOrdinal("type_name")) ? "" : r.GetString("type_name"),
            CreatedAt = r.GetDateTime("created_at"),
            Status = r.IsDBNull(r.GetOrdinal("status")) ? "draft" : r.GetString("status"),
            ProtocolCode = r.IsDBNull(r.GetOrdinal("protocol_code")) ? null : r.GetString("protocol_code"),
            OldProtocol  = r.IsDBNull(r.GetOrdinal("old_protocol"))  ? null : r.GetString("old_protocol"),
            AreaId = r.IsDBNull(r.GetOrdinal("area_id")) ? null : r.GetInt32("area_id"),
            AreaName = r.IsDBNull(r.GetOrdinal("area_name")) ? "" : r.GetString("area_name"),
            CatalogationDate = r.IsDBNull(r.GetOrdinal("catalogation_date")) ? null : r.GetDateTime("catalogation_date"),
            LastUpdate = r.IsDBNull(r.GetOrdinal("last_update")) ? null : r.GetDateTime("last_update"),
            PageCount = r.IsDBNull(r.GetOrdinal("page_count")) ? null : r.GetInt32("page_count"),
            IsPublishable = !r.IsDBNull(r.GetOrdinal("is_publishable")) && r.GetBoolean("is_publishable"),
            ExternalProtocolCode = r.IsDBNull(r.GetOrdinal("external_protocol_code")) ? null : r.GetString("external_protocol_code"),
            PlatformId = r.IsDBNull(r.GetOrdinal("platform_id")) ? null : r.GetInt32("platform_id"),
            PlatformName = r.IsDBNull(r.GetOrdinal("platform_name")) ? "" : r.GetString("platform_name"),
            IsPublished = !r.IsDBNull(r.GetOrdinal("is_published")) && r.GetBoolean("is_published"),
            ExternalLink = r.IsDBNull(r.GetOrdinal("external_link")) ? null : r.GetString("external_link"),
            CourseCode = r.IsDBNull(r.GetOrdinal("course_code")) ? null : r.GetString("course_code"),
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
