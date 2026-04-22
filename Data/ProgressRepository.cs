using MySqlConnector;

namespace BocconiLMS.Data;

public class ProgressRepository
{
    private readonly DbHelper _db;
    public ProgressRepository(DbHelper db) => _db = db;

    public async Task MarkLessonCompletedAsync(int userId, int lessonId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "INSERT IGNORE INTO lesson_progress (user_id, lesson_id, completed_at) VALUES (@uid, @lid, NOW())", conn);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@lid", lessonId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> GetCourseProgressAsync(int userId, int courseId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT
                COUNT(DISTINCT lp.lesson_id) AS completed,
                (SELECT COUNT(*) FROM lessons l WHERE l.course_id=@cid AND l.is_published=1) AS total
            FROM lesson_progress lp
            JOIN lessons l ON l.id=lp.lesson_id
            WHERE lp.user_id=@uid AND l.course_id=@cid", conn);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@cid", courseId);
        using var reader = await cmd.ExecuteReaderAsync();
        if (!reader.Read()) return 0;
        var completed = reader.GetInt32("completed");
        var total = reader.GetInt32("total");
        return total > 0 ? (int)Math.Round((double)completed / total * 100) : 0;
    }
}
