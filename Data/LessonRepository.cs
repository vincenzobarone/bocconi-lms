using MySqlConnector;
using BocconiLMS.Models;

namespace BocconiLMS.Data;

public class LessonRepository
{
    private readonly DbHelper _db;
    public LessonRepository(DbHelper db) => _db = db;

    public async Task<List<Lesson>> GetByCourseAsync(int courseId, int? userId = null, bool publishedOnly = false)
    {
        var lessons = new List<Lesson>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        var sql = @"
            SELECT l.id, l.course_id, c.title AS course_title, l.title, l.content,
                   l.sort_order, l.is_published, l.created_at, l.group_id,
                   CASE WHEN @uid IS NOT NULL AND EXISTS(SELECT 1 FROM lesson_progress lp WHERE lp.lesson_id=l.id AND lp.user_id=@uid) THEN 1 ELSE 0 END AS is_completed
            FROM lessons l
            JOIN courses c ON c.id = l.course_id
            WHERE l.course_id = @cid";
        if (publishedOnly) sql += " AND l.is_published = 1";
        sql += " ORDER BY l.sort_order, l.id";
        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@cid", courseId);
        cmd.Parameters.AddWithValue("@uid", userId.HasValue ? userId.Value : DBNull.Value);
        using var reader = await cmd.ExecuteReaderAsync();
        while (reader.Read()) lessons.Add(MapLesson(reader));
        return lessons;
    }

    public async Task<Lesson?> GetByIdAsync(int id, int? userId = null)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT l.id, l.course_id, c.title AS course_title, l.title, l.content,
                   l.sort_order, l.is_published, l.created_at, l.group_id,
                   CASE WHEN @uid IS NOT NULL AND EXISTS(SELECT 1 FROM lesson_progress lp WHERE lp.lesson_id=l.id AND lp.user_id=@uid) THEN 1 ELSE 0 END AS is_completed
            FROM lessons l
            JOIN courses c ON c.id = l.course_id
            WHERE l.id = @id LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@uid", userId.HasValue ? userId.Value : DBNull.Value);
        using var reader = await cmd.ExecuteReaderAsync();
        return reader.Read() ? MapLesson(reader) : null;
    }

    public async Task<int> GetMaxSortOrderAsync(int courseId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT COALESCE(MAX(sort_order), 0) FROM lessons WHERE course_id=@cid", conn);
        cmd.Parameters.AddWithValue("@cid", courseId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<int> CreateAsync(Lesson lesson)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            INSERT INTO lessons (course_id, title, content, sort_order, is_published, created_at)
            VALUES (@cid, @title, @content, @sort, @pub, NOW());
            SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@cid", lesson.CourseId);
        cmd.Parameters.AddWithValue("@title", lesson.Title);
        cmd.Parameters.AddWithValue("@content", lesson.Content);
        cmd.Parameters.AddWithValue("@sort", lesson.SortOrder);
        cmd.Parameters.AddWithValue("@pub", lesson.IsPublished);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task UpdateAsync(Lesson lesson)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            UPDATE lessons SET title=@title, content=@content, sort_order=@sort, is_published=@pub
            WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@title", lesson.Title);
        cmd.Parameters.AddWithValue("@content", lesson.Content);
        cmd.Parameters.AddWithValue("@sort", lesson.SortOrder);
        cmd.Parameters.AddWithValue("@pub", lesson.IsPublished);
        cmd.Parameters.AddWithValue("@id", lesson.Id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ReorderAsync(int courseId, List<int> orderedIds)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        for (int i = 0; i < orderedIds.Count; i++)
        {
            using var cmd = new MySqlCommand(
                "UPDATE lessons SET sort_order=@sort WHERE id=@id AND course_id=@cid", conn);
            cmd.Parameters.AddWithValue("@sort", i + 1);
            cmd.Parameters.AddWithValue("@id", orderedIds[i]);
            cmd.Parameters.AddWithValue("@cid", courseId);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand("DELETE FROM lessons WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    private static Lesson MapLesson(MySqlDataReader r) => new()
    {
        Id = r.GetInt32("id"),
        CourseId = r.GetInt32("course_id"),
        CourseTitle = r.GetString("course_title"),
        Title = r.GetString("title"),
        Content = r.IsDBNull(r.GetOrdinal("content")) ? "" : r.GetString("content"),
        SortOrder = r.GetInt32("sort_order"),
        IsPublished = r.GetBoolean("is_published"),
        CreatedAt = r.GetDateTime("created_at"),
        IsCompleted = r.GetBoolean("is_completed"),
        GroupId = r.IsDBNull(r.GetOrdinal("group_id")) ? null : r.GetInt32("group_id")
    };
}
