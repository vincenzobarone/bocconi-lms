using MySqlConnector;
using BocconiLMS.Models;

namespace BocconiLMS.Data;

public class EnrollmentReminderInfo
{
    public int UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserFirstName { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public int IncompleteLessons { get; set; }
}

public class EnrolledStudentContact
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

public class EnrollmentRepository
{
    private readonly DbHelper _db;
    public EnrollmentRepository(DbHelper db) => _db = db;

    public async Task<bool> IsEnrolledAsync(int userId, int courseId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM enrollments WHERE user_id=@uid AND course_id=@cid", conn);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@cid", courseId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task EnrollAsync(int userId, int courseId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "INSERT IGNORE INTO enrollments (user_id, course_id, enrolled_at) VALUES (@uid, @cid, NOW())", conn);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@cid", courseId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UnenrollAsync(int userId, int courseId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "DELETE FROM enrollments WHERE user_id=@uid AND course_id=@cid", conn);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@cid", courseId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Enrollment>> GetByUserAsync(int userId)
    {
        var list = new List<Enrollment>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT e.id, e.course_id, c.title AS course_title, e.user_id,
                   CONCAT(u.first_name,' ',u.last_name) AS user_name, e.enrolled_at,
                   (SELECT COUNT(*) FROM lessons l WHERE l.course_id=e.course_id AND l.is_published=1) AS total_lessons,
                   (SELECT COUNT(*) FROM lesson_progress lp WHERE lp.user_id=e.user_id AND lp.lesson_id IN (SELECT id FROM lessons WHERE course_id=e.course_id)) AS completed_lessons
            FROM enrollments e
            JOIN courses c ON c.id=e.course_id
            JOIN users u ON u.id=e.user_id
            WHERE e.user_id=@uid
            ORDER BY e.enrolled_at DESC", conn);
        cmd.Parameters.AddWithValue("@uid", userId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (reader.Read())
        {
            var total = reader.GetInt32("total_lessons");
            var completed = reader.GetInt32("completed_lessons");
            list.Add(new Enrollment
            {
                Id = reader.GetInt32("id"),
                CourseId = reader.GetInt32("course_id"),
                CourseTitle = reader.GetString("course_title"),
                UserId = reader.GetInt32("user_id"),
                UserName = reader.GetString("user_name"),
                EnrolledAt = reader.GetDateTime("enrolled_at"),
                TotalLessons = total,
                CompletedLessons = completed,
                ProgressPercent = total > 0 ? (int)Math.Round((double)completed / total * 100) : 0
            });
        }
        return list;
    }

    public async Task<List<Enrollment>> GetByCourseAsync(int courseId)
    {
        var list = new List<Enrollment>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT e.id, e.course_id, c.title AS course_title, e.user_id,
                   CONCAT(u.first_name,' ',u.last_name) AS user_name, e.enrolled_at,
                   (SELECT COUNT(*) FROM lessons l WHERE l.course_id=e.course_id AND l.is_published=1) AS total_lessons,
                   (SELECT COUNT(*) FROM lesson_progress lp WHERE lp.user_id=e.user_id AND lp.lesson_id IN (SELECT id FROM lessons WHERE course_id=e.course_id)) AS completed_lessons
            FROM enrollments e
            JOIN courses c ON c.id=e.course_id
            JOIN users u ON u.id=e.user_id
            WHERE e.course_id=@cid
            ORDER BY u.last_name, u.first_name", conn);
        cmd.Parameters.AddWithValue("@cid", courseId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (reader.Read())
        {
            var total = reader.GetInt32("total_lessons");
            var completed = reader.GetInt32("completed_lessons");
            list.Add(new Enrollment
            {
                Id = reader.GetInt32("id"),
                CourseId = reader.GetInt32("course_id"),
                CourseTitle = reader.GetString("course_title"),
                UserId = reader.GetInt32("user_id"),
                UserName = reader.GetString("user_name"),
                EnrolledAt = reader.GetDateTime("enrolled_at"),
                TotalLessons = total,
                CompletedLessons = completed,
                ProgressPercent = total > 0 ? (int)Math.Round((double)completed / total * 100) : 0
            });
        }
        return list;
    }

    public async Task<List<EnrolledStudentContact>> GetEnrolledStudentContactsAsync(int courseId)
    {
        var list = new List<EnrolledStudentContact>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT u.id AS user_id, u.email, u.first_name,
                   CONCAT(u.first_name, ' ', u.last_name) AS full_name
            FROM enrollments e
            JOIN users u ON u.id = e.user_id
            WHERE e.course_id = @cid AND u.is_active = 1
            ORDER BY u.last_name, u.first_name", conn);
        cmd.Parameters.AddWithValue("@cid", courseId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (reader.Read())
        {
            list.Add(new EnrolledStudentContact
            {
                UserId = reader.GetInt32("user_id"),
                Email = reader.GetString("email"),
                FirstName = reader.GetString("first_name"),
                FullName = reader.GetString("full_name")
            });
        }
        return list;
    }

    public async Task<List<EnrollmentReminderInfo>> GetIncompleteEnrollmentsForReminderAsync()
    {
        var list = new List<EnrollmentReminderInfo>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT e.user_id, u.email, u.first_name, e.course_id, c.title AS course_title,
                   (SELECT COUNT(*) FROM lessons l
                    WHERE l.course_id=e.course_id AND l.is_published=1
                      AND NOT EXISTS(SELECT 1 FROM lesson_progress lp WHERE lp.lesson_id=l.id AND lp.user_id=e.user_id)
                   ) AS incomplete_lessons
            FROM enrollments e
            JOIN users u ON u.id=e.user_id
            JOIN courses c ON c.id=e.course_id
            WHERE u.is_active=1 AND c.is_published=1
            HAVING incomplete_lessons > 0
            ORDER BY u.last_name, u.first_name, c.title", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (reader.Read())
        {
            list.Add(new EnrollmentReminderInfo
            {
                UserId = reader.GetInt32("user_id"),
                UserEmail = reader.GetString("email"),
                UserFirstName = reader.GetString("first_name"),
                CourseId = reader.GetInt32("course_id"),
                CourseTitle = reader.GetString("course_title"),
                IncompleteLessons = reader.GetInt32("incomplete_lessons")
            });
        }
        return list;
    }
}
