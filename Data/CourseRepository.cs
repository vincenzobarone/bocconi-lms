using MySqlConnector;
using BocconiLMS.Models;

namespace BocconiLMS.Data;

public class CourseRepository
{
    private readonly DbHelper _db;
    public CourseRepository(DbHelper db) => _db = db;

    public async Task<List<Course>> GetAllAsync(bool publishedOnly = false)
    {
        var courses = new List<Course>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        var where = publishedOnly ? "WHERE c.is_published = 1" : "";
        using var cmd = new MySqlCommand($@"
            SELECT c.id, c.title, c.description, c.category, c.teacher_id,
                   CONCAT(u.first_name,' ',u.last_name) AS teacher_name,
                   c.start_date, c.end_date, c.is_published, c.created_at,
                   c.created_by,
                   CONCAT(cb.first_name,' ',cb.last_name) AS created_by_name,
                   (SELECT COUNT(*) FROM enrollments e WHERE e.course_id=c.id) AS enrolled_count,
                   (SELECT COUNT(*) FROM lessons l WHERE l.course_id=c.id) AS lesson_count
            FROM courses c
            LEFT JOIN users u  ON u.id  = c.teacher_id
            LEFT JOIN users cb ON cb.id = c.created_by
            {where}
            ORDER BY c.created_at DESC", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (reader.Read()) courses.Add(MapCourse(reader));
        return courses;
    }

    public async Task<List<Course>> GetByTeacherAsync(int teacherId)
    {
        var courses = new List<Course>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT c.id, c.title, c.description, c.category, c.teacher_id,
                   CONCAT(u.first_name,' ',u.last_name) AS teacher_name,
                   c.start_date, c.end_date, c.is_published, c.created_at,
                   c.created_by,
                   CONCAT(cb.first_name,' ',cb.last_name) AS created_by_name,
                   (SELECT COUNT(*) FROM enrollments e WHERE e.course_id=c.id) AS enrolled_count,
                   (SELECT COUNT(*) FROM lessons l WHERE l.course_id=c.id) AS lesson_count
            FROM courses c
            LEFT JOIN users u  ON u.id  = c.teacher_id
            LEFT JOIN users cb ON cb.id = c.created_by
            WHERE c.teacher_id = @tid
            ORDER BY c.created_at DESC", conn);
        cmd.Parameters.AddWithValue("@tid", teacherId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (reader.Read()) courses.Add(MapCourse(reader));
        return courses;
    }

    public async Task<Course?> GetByIdAsync(int id)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT c.id, c.title, c.description, c.category, c.teacher_id,
                   CONCAT(u.first_name,' ',u.last_name) AS teacher_name,
                   c.start_date, c.end_date, c.is_published, c.created_at,
                   c.created_by,
                   CONCAT(cb.first_name,' ',cb.last_name) AS created_by_name,
                   (SELECT COUNT(*) FROM enrollments e WHERE e.course_id=c.id) AS enrolled_count,
                   (SELECT COUNT(*) FROM lessons l WHERE l.course_id=c.id) AS lesson_count
            FROM courses c
            LEFT JOIN users u  ON u.id  = c.teacher_id
            LEFT JOIN users cb ON cb.id = c.created_by
            WHERE c.id = @id LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = await cmd.ExecuteReaderAsync();
        return reader.Read() ? MapCourse(reader) : null;
    }

    public async Task<int> CreateAsync(Course course)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            INSERT INTO courses (title, description, category, teacher_id, start_date, end_date, is_published, created_at, created_by)
            VALUES (@title, @desc, @cat, @tid, @sd, @ed, @pub, NOW(), @createdBy);
            SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@title", course.Title);
        cmd.Parameters.AddWithValue("@desc", course.Description);
        cmd.Parameters.AddWithValue("@cat", course.Category);
        cmd.Parameters.AddWithValue("@tid", course.TeacherId);
        cmd.Parameters.AddWithValue("@sd", (object?)course.StartDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ed", (object?)course.EndDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@pub", course.IsPublished);
        cmd.Parameters.AddWithValue("@createdBy", (object?)course.CreatedBy ?? DBNull.Value);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task UpdateAsync(Course course)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            UPDATE courses SET title=@title, description=@desc, category=@cat,
                start_date=@sd, end_date=@ed, is_published=@pub
            WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@title", course.Title);
        cmd.Parameters.AddWithValue("@desc", course.Description);
        cmd.Parameters.AddWithValue("@cat", course.Category);
        cmd.Parameters.AddWithValue("@sd", (object?)course.StartDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ed", (object?)course.EndDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@pub", course.IsPublished);
        cmd.Parameters.AddWithValue("@id", course.Id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();
        try
        {
            var steps = new[]
            {
                @"DELETE qa FROM quiz_attempts qa
                  JOIN quizzes q ON q.id = qa.quiz_id
                  JOIN lessons l ON l.id = q.lesson_id
                  WHERE l.course_id = @id",
                @"DELETE qo FROM quiz_options qo
                  JOIN quiz_questions qq ON qq.id = qo.question_id
                  JOIN quizzes q ON q.id = qq.quiz_id
                  JOIN lessons l ON l.id = q.lesson_id
                  WHERE l.course_id = @id",
                @"DELETE qq FROM quiz_questions qq
                  JOIN quizzes q ON q.id = qq.quiz_id
                  JOIN lessons l ON l.id = q.lesson_id
                  WHERE l.course_id = @id",
                @"DELETE q FROM quizzes q
                  JOIN lessons l ON l.id = q.lesson_id
                  WHERE l.course_id = @id",
                @"DELETE lp FROM lesson_progress lp
                  JOIN lessons l ON l.id = lp.lesson_id
                  WHERE l.course_id = @id",
                "DELETE FROM lessons WHERE course_id = @id",
                "DELETE FROM enrollments WHERE course_id = @id",
                "DELETE FROM courses WHERE id = @id"
            };
            foreach (var sql in steps)
            {
                using var cmd = new MySqlCommand(sql, conn, tx);
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private static Course MapCourse(MySqlDataReader r) => new()
    {
        Id = r.GetInt32("id"),
        Title = r.GetString("title"),
        Description = r.GetString("description"),
        Category = r.GetString("category"),
        TeacherId = r.GetInt32("teacher_id"),
        TeacherName = r.IsDBNull(r.GetOrdinal("teacher_name")) ? "" : r.GetString("teacher_name"),
        StartDate = r.IsDBNull(r.GetOrdinal("start_date")) ? null : r.GetDateTime("start_date"),
        EndDate = r.IsDBNull(r.GetOrdinal("end_date")) ? null : r.GetDateTime("end_date"),
        IsPublished = r.GetBoolean("is_published"),
        CreatedAt = r.GetDateTime("created_at"),
        CreatedBy = r.IsDBNull(r.GetOrdinal("created_by")) ? null : r.GetInt32("created_by"),
        CreatedByName = r.IsDBNull(r.GetOrdinal("created_by_name")) ? "" : r.GetString("created_by_name"),
        EnrolledCount = r.GetInt32("enrolled_count"),
        LessonCount = r.GetInt32("lesson_count")
    };
}
