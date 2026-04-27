using MySqlConnector;
using BocconiLMS.Models;

namespace BocconiLMS.Data;

public class DocumentRepository
{
    private readonly DbHelper _db;
    public DocumentRepository(DbHelper db) => _db = db;

    public async Task<List<Document>> GetByLessonAsync(int lessonId)
    {
        var docs = new List<Document>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT d.id, d.lesson_id, l.title AS lesson_title, l.course_id, d.title, d.created_at,
                   (SELECT MAX(dv.version_number) FROM document_versions dv WHERE dv.document_id=d.id) AS current_version
            FROM documents d
            JOIN lessons l ON l.id = d.lesson_id
            WHERE d.lesson_id = @lid
            ORDER BY d.title", conn);
        cmd.Parameters.AddWithValue("@lid", lessonId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (reader.Read()) docs.Add(MapDocument(reader));
        return docs;
    }

    public async Task<Document?> GetByIdAsync(int id)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT d.id, d.lesson_id, l.title AS lesson_title, l.course_id, d.title, d.created_at,
                   (SELECT MAX(dv.version_number) FROM document_versions dv WHERE dv.document_id=d.id) AS current_version
            FROM documents d
            JOIN lessons l ON l.id = d.lesson_id
            WHERE d.id = @id LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = await cmd.ExecuteReaderAsync();
        if (!reader.Read()) return null;
        var doc = MapDocument(reader);
        await reader.CloseAsync();
        doc.ActiveVersion = await GetActiveVersionAsync(id, conn);
        return doc;
    }

    public async Task<int> CreateDocumentAsync(string title, int lessonId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "INSERT INTO documents (lesson_id, title, created_at) VALUES (@lid, @title, NOW())", conn);
        cmd.Parameters.AddWithValue("@lid", lessonId);
        cmd.Parameters.AddWithValue("@title", title);
        await cmd.ExecuteNonQueryAsync();
        return await DbHelper.GetLastInsertIdAsync(conn);
    }

    public async Task<int> AddVersionAsync(DocumentVersion version)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();
        using var deactivate = new MySqlCommand(
            "UPDATE document_versions SET is_active=0 WHERE document_id=@did", conn, tx);
        deactivate.Parameters.AddWithValue("@did", version.DocumentId);
        await deactivate.ExecuteNonQueryAsync();

        using var insert = new MySqlCommand(@"
            INSERT INTO document_versions (document_id, version_number, file_name, file_path, file_type, file_size_bytes, uploaded_by, notes, is_active, uploaded_at)
            VALUES (@did, @vn, @fn, @fp, @ft, @fs, @ub, @notes, 1, NOW())", conn, tx);
        insert.Parameters.AddWithValue("@did", version.DocumentId);
        insert.Parameters.AddWithValue("@vn", version.VersionNumber);
        insert.Parameters.AddWithValue("@fn", version.FileName);
        insert.Parameters.AddWithValue("@fp", version.FilePath);
        insert.Parameters.AddWithValue("@ft", version.FileType);
        insert.Parameters.AddWithValue("@fs", version.FileSizeBytes);
        insert.Parameters.AddWithValue("@ub", version.UploadedBy);
        insert.Parameters.AddWithValue("@notes", (object?)version.Notes ?? DBNull.Value);
        await insert.ExecuteNonQueryAsync();
        var newId = await DbHelper.GetLastInsertIdAsync(conn, tx);
        await tx.CommitAsync();
        return newId;
    }

    public async Task<List<DocumentVersion>> GetVersionsAsync(int documentId)
    {
        var versions = new List<DocumentVersion>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT dv.id, dv.document_id, dv.version_number, dv.file_name, dv.file_path,
                   dv.file_type, dv.file_size_bytes, dv.uploaded_by,
                   CONCAT(u.first_name,' ',u.last_name) AS uploader_name,
                   dv.notes, dv.is_active, dv.uploaded_at
            FROM document_versions dv
            LEFT JOIN users u ON u.id = dv.uploaded_by
            WHERE dv.document_id = @did
            ORDER BY dv.version_number DESC", conn);
        cmd.Parameters.AddWithValue("@did", documentId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (reader.Read()) versions.Add(MapVersion(reader));
        return versions;
    }

    public async Task<DocumentVersion?> GetVersionByIdAsync(int versionId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT dv.id, dv.document_id, dv.version_number, dv.file_name, dv.file_path,
                   dv.file_type, dv.file_size_bytes, dv.uploaded_by,
                   CONCAT(u.first_name,' ',u.last_name) AS uploader_name,
                   dv.notes, dv.is_active, dv.uploaded_at
            FROM document_versions dv
            LEFT JOIN users u ON u.id = dv.uploaded_by
            WHERE dv.id = @id LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@id", versionId);
        using var reader = await cmd.ExecuteReaderAsync();
        return reader.Read() ? MapVersion(reader) : null;
    }

    public async Task<int> GetNextVersionNumberAsync(int documentId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT COALESCE(MAX(version_number), 0) + 1 FROM document_versions WHERE document_id=@did", conn);
        cmd.Parameters.AddWithValue("@did", documentId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<List<string>> GetVersionFilePathsAsync(int documentId)
    {
        var paths = new List<string>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT file_path FROM document_versions WHERE document_id=@did", conn);
        cmd.Parameters.AddWithValue("@did", documentId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (reader.Read())
            paths.Add(reader.GetString(0));
        return paths;
    }

    public async Task DeleteDocumentAsync(int documentId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();
        try
        {
            using var del1 = new MySqlCommand(
                "DELETE FROM document_versions WHERE document_id=@did", conn, tx);
            del1.Parameters.AddWithValue("@did", documentId);
            await del1.ExecuteNonQueryAsync();

            using var del2 = new MySqlCommand(
                "DELETE FROM documents WHERE id=@did", conn, tx);
            del2.Parameters.AddWithValue("@did", documentId);
            await del2.ExecuteNonQueryAsync();

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> RestoreVersionAsync(int documentId, int versionId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        using var verify = new MySqlCommand(
            "SELECT COUNT(*) FROM document_versions WHERE id=@vid AND document_id=@did", conn, tx);
        verify.Parameters.AddWithValue("@vid", versionId);
        verify.Parameters.AddWithValue("@did", documentId);
        var count = Convert.ToInt32(await verify.ExecuteScalarAsync());
        if (count == 0)
        {
            await tx.RollbackAsync();
            return false;
        }

        using var deact = new MySqlCommand(
            "UPDATE document_versions SET is_active=0 WHERE document_id=@did", conn, tx);
        deact.Parameters.AddWithValue("@did", documentId);
        await deact.ExecuteNonQueryAsync();

        using var act = new MySqlCommand(
            "UPDATE document_versions SET is_active=1 WHERE id=@vid AND document_id=@did", conn, tx);
        act.Parameters.AddWithValue("@vid", versionId);
        act.Parameters.AddWithValue("@did", documentId);
        var affected = await act.ExecuteNonQueryAsync();
        if (affected == 0)
        {
            await tx.RollbackAsync();
            return false;
        }

        await tx.CommitAsync();
        return true;
    }

    private async Task<DocumentVersion?> GetActiveVersionAsync(int documentId, MySqlConnection conn)
    {
        using var cmd = new MySqlCommand(@"
            SELECT dv.id, dv.document_id, dv.version_number, dv.file_name, dv.file_path,
                   dv.file_type, dv.file_size_bytes, dv.uploaded_by,
                   CONCAT(u.first_name,' ',u.last_name) AS uploader_name,
                   dv.notes, dv.is_active, dv.uploaded_at
            FROM document_versions dv
            LEFT JOIN users u ON u.id = dv.uploaded_by
            WHERE dv.document_id = @did AND dv.is_active = 1 LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@did", documentId);
        using var reader = await cmd.ExecuteReaderAsync();
        return reader.Read() ? MapVersion(reader) : null;
    }

    private static Document MapDocument(MySqlDataReader r) => new()
    {
        Id = r.GetInt32("id"),
        LessonId = r.GetInt32("lesson_id"),
        LessonTitle = r.GetString("lesson_title"),
        CourseId = r.GetInt32("course_id"),
        Title = r.GetString("title"),
        CreatedAt = r.GetDateTime("created_at"),
        CurrentVersion = r.IsDBNull(r.GetOrdinal("current_version")) ? 0 : r.GetInt32("current_version")
    };

    private static DocumentVersion MapVersion(MySqlDataReader r) => new()
    {
        Id = r.GetInt32("id"),
        DocumentId = r.GetInt32("document_id"),
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
