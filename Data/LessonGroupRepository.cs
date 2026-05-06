using MySqlConnector;
using BocconiLMS.Models;

namespace BocconiLMS.Data;

public class LessonGroupRepository
{
    private readonly DbHelper _db;
    public LessonGroupRepository(DbHelper db) => _db = db;

    public async Task<List<LessonGroup>> GetByCourseAsync(int courseId)
    {
        var groups = new List<LessonGroup>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT id, course_id, title, sort_order FROM lesson_groups WHERE course_id=@cid ORDER BY sort_order, id",
            conn);
        cmd.Parameters.AddWithValue("@cid", courseId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (reader.Read())
            groups.Add(new LessonGroup
            {
                Id = reader.GetInt32("id"),
                CourseId = reader.GetInt32("course_id"),
                Title = reader.GetString("title"),
                SortOrder = reader.GetInt32("sort_order")
            });
        return groups;
    }

    public async Task<int> CreateAsync(int courseId, string title)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            INSERT INTO lesson_groups (course_id, title, sort_order, created_at)
            VALUES (@cid, @title,
                COALESCE((SELECT MAX(sort_order) FROM lesson_groups g2 WHERE g2.course_id=@cid), 0) + 1,
                NOW());
            SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@cid", courseId);
        cmd.Parameters.AddWithValue("@title", title);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task RenameAsync(int id, string title)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand("UPDATE lesson_groups SET title=@title WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var ungroup = new MySqlCommand(
            "UPDATE lessons SET group_id=NULL WHERE group_id=@id", conn);
        ungroup.Parameters.AddWithValue("@id", id);
        await ungroup.ExecuteNonQueryAsync();
        using var del = new MySqlCommand("DELETE FROM lesson_groups WHERE id=@id", conn);
        del.Parameters.AddWithValue("@id", id);
        await del.ExecuteNonQueryAsync();
    }

    public async Task SetLessonGroupAsync(int lessonId, int? groupId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "UPDATE lessons SET group_id=@gid WHERE id=@lid", conn);
        cmd.Parameters.AddWithValue("@gid", groupId.HasValue ? groupId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@lid", lessonId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<LessonGroup?> GetByIdAsync(int id)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT id, course_id, title, sort_order FROM lesson_groups WHERE id=@id LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = await cmd.ExecuteReaderAsync();
        if (!reader.Read()) return null;
        return new LessonGroup
        {
            Id = reader.GetInt32("id"),
            CourseId = reader.GetInt32("course_id"),
            Title = reader.GetString("title"),
            SortOrder = reader.GetInt32("sort_order")
        };
    }
}
