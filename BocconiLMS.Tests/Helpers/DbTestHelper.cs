using MySqlConnector;

namespace BocconiLMS.Tests.Helpers;

public class DbTestHelper : IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly List<(string table, string condition)> _cleanups = new();

    public DbTestHelper()
    {
        _connectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING")
            ?? "Server=localhost;Port=3306;Database=bocconi_lms;User=root;Password=;";
    }

    public MySqlConnection GetConnection()
    {
        return new MySqlConnection(_connectionString);
    }

    public async Task<int> CreateUserAsync(string email, string firstName, string lastName,
        string role, string password = "TestPassword1!")
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = new MySqlCommand(@"
            INSERT INTO users (email, password_hash, first_name, last_name, role, is_active, created_at)
            VALUES (@email, @hash, @fn, @ln, @role, 1, NOW());
            SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@email", email);
        cmd.Parameters.AddWithValue("@hash", hash);
        cmd.Parameters.AddWithValue("@fn", firstName);
        cmd.Parameters.AddWithValue("@ln", lastName);
        cmd.Parameters.AddWithValue("@role", role);
        var userId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        using var roleCmd = new MySqlCommand(@"
            INSERT INTO user_roles (user_id, role_id)
            SELECT @uid, id FROM roles WHERE name = @role", conn);
        roleCmd.Parameters.AddWithValue("@uid", userId);
        roleCmd.Parameters.AddWithValue("@role", role);
        await roleCmd.ExecuteNonQueryAsync();

        _cleanups.Add(("users", $"id = {userId}"));
        return userId;
    }

    public async Task<int> CreateCourseAsync(int teacherId, string title, bool isPublished = true)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            INSERT INTO courses (title, description, category, teacher_id, is_published, created_at)
            VALUES (@title, 'Test description', 'Test', @tid, @pub, NOW());
            SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@tid", teacherId);
        cmd.Parameters.AddWithValue("@pub", isPublished ? 1 : 0);
        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        _cleanups.Add(("courses", $"id = {id}"));
        return id;
    }

    public async Task<int> CreateLessonAsync(int courseId, string title, bool isPublished = true)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            INSERT INTO lessons (course_id, title, content, sort_order, is_published, created_at)
            VALUES (@cid, @title, 'Test content', 1, @pub, NOW());
            SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@cid", courseId);
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@pub", isPublished ? 1 : 0);
        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        _cleanups.Add(("lessons", $"id = {id}"));
        return id;
    }

    public async Task EnrollStudentAsync(int userId, int courseId)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            INSERT IGNORE INTO enrollments (user_id, course_id, enrolled_at) VALUES (@uid, @cid, NOW())", conn);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@cid", courseId);
        await cmd.ExecuteNonQueryAsync();
        _cleanups.Add(("enrollments", $"user_id = {userId} AND course_id = {courseId}"));
    }

    public async Task<(int quizId, int questionId, int correctOptionId)> CreateQuizWithOneQuestionAsync(
        int lessonId, string quizTitle, string questionText = "Test question A or B", int passingScore = 60)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var qCmd = new MySqlCommand(@"
            INSERT INTO quizzes (lesson_id, title, description, time_limit_minutes, passing_score, created_at)
            VALUES (@lid, @title, 'Test quiz', 30, @ps, NOW());
            SELECT LAST_INSERT_ID();", conn);
        qCmd.Parameters.AddWithValue("@lid", lessonId);
        qCmd.Parameters.AddWithValue("@title", quizTitle);
        qCmd.Parameters.AddWithValue("@ps", passingScore);
        var quizId = Convert.ToInt32(await qCmd.ExecuteScalarAsync());
        _cleanups.Add(("quizzes", $"id = {quizId}"));

        using var qqCmd = new MySqlCommand(@"
            INSERT INTO quiz_questions (quiz_id, question_text, sort_order)
            VALUES (@qid, @qtext, 1);
            SELECT LAST_INSERT_ID();", conn);
        qqCmd.Parameters.AddWithValue("@qid", quizId);
        qqCmd.Parameters.AddWithValue("@qtext", questionText);
        var questionId = Convert.ToInt32(await qqCmd.ExecuteScalarAsync());

        int correctOptId = 0;
        string[] options = { "3", "4", "5", "6" };
        for (int i = 0; i < options.Length; i++)
        {
            bool isCorrect = i == 1;
            using var optCmd = new MySqlCommand(@"
                INSERT INTO quiz_options (question_id, option_text, is_correct, sort_order)
                VALUES (@qid, @text, @correct, @so);
                SELECT LAST_INSERT_ID();", conn);
            optCmd.Parameters.AddWithValue("@qid", questionId);
            optCmd.Parameters.AddWithValue("@text", options[i]);
            optCmd.Parameters.AddWithValue("@correct", isCorrect);
            optCmd.Parameters.AddWithValue("@so", i + 1);
            var optId = Convert.ToInt32(await optCmd.ExecuteScalarAsync());
            if (isCorrect) correctOptId = optId;
        }

        return (quizId, questionId, correctOptId);
    }

    public async Task<int> CreateDocumentAsync(int lessonId, string title)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            INSERT INTO documents (lesson_id, title, created_at)
            VALUES (@lid, @title, NOW());
            SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@lid", lessonId);
        cmd.Parameters.AddWithValue("@title", title);
        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        _cleanups.Add(("documents", $"id = {id}"));
        return id;
    }

    public async Task<int> CreateDocumentVersionAsync(int documentId, int uploadedBy,
        int versionNumber, bool isActive = true, string? notes = null)
    {
        var fakeFilePath = $"/uploads/{documentId}/v{versionNumber}_test.txt";
        using var conn = GetConnection();
        await conn.OpenAsync();

        if (isActive)
        {
            using var deact = new MySqlCommand(
                "UPDATE document_versions SET is_active=0 WHERE document_id=@did", conn);
            deact.Parameters.AddWithValue("@did", documentId);
            await deact.ExecuteNonQueryAsync();
        }

        using var cmd = new MySqlCommand(@"
            INSERT INTO document_versions (document_id, version_number, file_name, file_path,
                file_type, file_size_bytes, uploaded_by, notes, is_active, uploaded_at)
            VALUES (@did, @vn, @fn, @fp, 'TXT', 100, @ub, @notes, @active, NOW());
            SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@did", documentId);
        cmd.Parameters.AddWithValue("@vn", versionNumber);
        cmd.Parameters.AddWithValue("@fn", $"test_v{versionNumber}.txt");
        cmd.Parameters.AddWithValue("@fp", fakeFilePath);
        cmd.Parameters.AddWithValue("@ub", uploadedBy);
        cmd.Parameters.AddWithValue("@notes", (object?)notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@active", isActive ? 1 : 0);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<int?> GetActiveVersionNumberAsync(int documentId)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT version_number FROM document_versions WHERE document_id=@did AND is_active=1 LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@did", documentId);
        var result = await cmd.ExecuteScalarAsync();
        return result is DBNull || result is null ? null : Convert.ToInt32(result);
    }

    public async ValueTask DisposeAsync()
    {
        using var conn = GetConnection();
        await conn.OpenAsync();
        foreach (var (table, condition) in Enumerable.Reverse(_cleanups))
        {
            try
            {
                using var cmd = new MySqlCommand($"DELETE FROM {table} WHERE {condition}", conn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
            }
        }
    }

    public async Task CleanupOrphanTestDataAsync()
    {
        using var conn = GetConnection();
        await conn.OpenAsync();
        var tables = new[]
        {
            ("quiz_attempts",   "user_id IN (SELECT id FROM users WHERE email LIKE '%@test.it')"),
            ("lesson_progress", "user_id IN (SELECT id FROM users WHERE email LIKE '%@test.it')"),
            ("enrollments",     "user_id IN (SELECT id FROM users WHERE email LIKE '%@test.it')"),
            ("users",           "email LIKE '%@test.it'"),
        };
        foreach (var (table, condition) in tables)
        {
            try
            {
                using var cmd = new MySqlCommand($"DELETE FROM {table} WHERE {condition}", conn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch { }
        }
    }
}
