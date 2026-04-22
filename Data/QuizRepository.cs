using MySqlConnector;
using BocconiLMS.Models;

namespace BocconiLMS.Data;

public class QuizRepository
{
    private readonly DbHelper _db;
    public QuizRepository(DbHelper db) => _db = db;

    public async Task<List<Quiz>> GetByLessonAsync(int lessonId)
    {
        var quizzes = new List<Quiz>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT q.id, q.lesson_id, l.title AS lesson_title, l.course_id, q.title, q.description,
                   q.time_limit_minutes, q.passing_score, q.created_at
            FROM quizzes q JOIN lessons l ON l.id=q.lesson_id
            WHERE q.lesson_id=@lid ORDER BY q.created_at", conn);
        cmd.Parameters.AddWithValue("@lid", lessonId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (reader.Read()) quizzes.Add(MapQuiz(reader));
        return quizzes;
    }

    public async Task<Quiz?> GetByIdAsync(int id, bool withQuestions = false)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT q.id, q.lesson_id, l.title AS lesson_title, l.course_id, q.title, q.description,
                   q.time_limit_minutes, q.passing_score, q.created_at
            FROM quizzes q JOIN lessons l ON l.id=q.lesson_id
            WHERE q.id=@id LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = await cmd.ExecuteReaderAsync();
        if (!reader.Read()) return null;
        var quiz = MapQuiz(reader);
        await reader.CloseAsync();
        if (withQuestions)
            quiz.Questions = await GetQuestionsAsync(id, conn);
        return quiz;
    }

    public async Task<int> CreateAsync(Quiz quiz)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            INSERT INTO quizzes (lesson_id, title, description, time_limit_minutes, passing_score, created_at)
            VALUES (@lid, @title, @desc, @tlm, @ps, NOW()); SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@lid", quiz.LessonId);
        cmd.Parameters.AddWithValue("@title", quiz.Title);
        cmd.Parameters.AddWithValue("@desc", (object?)quiz.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@tlm", quiz.TimeLimitMinutes);
        cmd.Parameters.AddWithValue("@ps", quiz.PassingScore);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task AddQuestionAsync(QuizQuestion question)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            INSERT INTO quiz_questions (quiz_id, question_text, sort_order) VALUES (@qid, @qt, @so);
            SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@qid", question.QuizId);
        cmd.Parameters.AddWithValue("@qt", question.QuestionText);
        cmd.Parameters.AddWithValue("@so", question.SortOrder);
        var qId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        foreach (var opt in question.Options)
        {
            using var optCmd = new MySqlCommand(@"
                INSERT INTO quiz_options (question_id, option_text, is_correct, sort_order)
                VALUES (@qid, @ot, @ic, @so)", conn);
            optCmd.Parameters.AddWithValue("@qid", qId);
            optCmd.Parameters.AddWithValue("@ot", opt.OptionText);
            optCmd.Parameters.AddWithValue("@ic", opt.IsCorrect);
            optCmd.Parameters.AddWithValue("@so", opt.SortOrder);
            await optCmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteQuizAsync(int quizId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand("DELETE FROM quizzes WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", quizId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<QuizAttempt> SubmitAttemptAsync(int quizId, int userId, Dictionary<int, int> answers)
    {
        var quiz = await GetByIdAsync(quizId, withQuestions: true)
            ?? throw new InvalidOperationException("Quiz not found");

        int correct = 0;
        foreach (var q in quiz.Questions)
        {
            if (answers.TryGetValue(q.Id, out var selectedOpt))
            {
                var correctOpt = q.Options.FirstOrDefault(o => o.IsCorrect);
                if (correctOpt != null && correctOpt.Id == selectedOpt) correct++;
            }
        }

        int total = quiz.Questions.Count;
        int score = total > 0 ? (int)Math.Round((double)correct / total * 100) : 0;
        bool passed = score >= quiz.PassingScore;

        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            INSERT INTO quiz_attempts (quiz_id, user_id, score, total_questions, correct_answers, passed, attempted_at)
            VALUES (@qid, @uid, @score, @total, @correct, @passed, NOW()); SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@qid", quizId);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@score", score);
        cmd.Parameters.AddWithValue("@total", total);
        cmd.Parameters.AddWithValue("@correct", correct);
        cmd.Parameters.AddWithValue("@passed", passed);
        var attemptId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        return new QuizAttempt
        {
            Id = attemptId,
            QuizId = quizId,
            QuizTitle = quiz.Title,
            UserId = userId,
            Score = score,
            TotalQuestions = total,
            CorrectAnswers = correct,
            Passed = passed,
            AttemptedAt = DateTime.Now,
            PassingScore = quiz.PassingScore
        };
    }

    public async Task<List<QuizAttempt>> GetAttemptsAsync(int userId, int? quizId = null)
    {
        var attempts = new List<QuizAttempt>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        var where = quizId.HasValue ? "AND qa.quiz_id=@qid" : "";
        using var cmd = new MySqlCommand($@"
            SELECT qa.id, qa.quiz_id, q.title AS quiz_title, qa.user_id,
                   qa.score, qa.total_questions, qa.correct_answers, qa.passed, qa.attempted_at, q.passing_score
            FROM quiz_attempts qa JOIN quizzes q ON q.id=qa.quiz_id
            WHERE qa.user_id=@uid {where}
            ORDER BY qa.attempted_at DESC LIMIT 50", conn);
        cmd.Parameters.AddWithValue("@uid", userId);
        if (quizId.HasValue) cmd.Parameters.AddWithValue("@qid", quizId.Value);
        using var reader = await cmd.ExecuteReaderAsync();
        while (reader.Read())
        {
            attempts.Add(new QuizAttempt
            {
                Id = reader.GetInt32("id"),
                QuizId = reader.GetInt32("quiz_id"),
                QuizTitle = reader.GetString("quiz_title"),
                UserId = reader.GetInt32("user_id"),
                Score = reader.GetInt32("score"),
                TotalQuestions = reader.GetInt32("total_questions"),
                CorrectAnswers = reader.GetInt32("correct_answers"),
                Passed = reader.GetBoolean("passed"),
                AttemptedAt = reader.GetDateTime("attempted_at"),
                PassingScore = reader.GetInt32("passing_score")
            });
        }
        return attempts;
    }

    private async Task<List<QuizQuestion>> GetQuestionsAsync(int quizId, MySqlConnection conn)
    {
        var questions = new List<QuizQuestion>();
        using var cmd = new MySqlCommand(
            "SELECT id, quiz_id, question_text, sort_order FROM quiz_questions WHERE quiz_id=@qid ORDER BY sort_order", conn);
        cmd.Parameters.AddWithValue("@qid", quizId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (reader.Read())
        {
            questions.Add(new QuizQuestion
            {
                Id = reader.GetInt32("id"),
                QuizId = reader.GetInt32("quiz_id"),
                QuestionText = reader.GetString("question_text"),
                SortOrder = reader.GetInt32("sort_order")
            });
        }
        await reader.CloseAsync();
        foreach (var q in questions)
            q.Options = await GetOptionsAsync(q.Id, conn);
        return questions;
    }

    private async Task<List<QuizOption>> GetOptionsAsync(int questionId, MySqlConnection conn)
    {
        var opts = new List<QuizOption>();
        using var cmd = new MySqlCommand(
            "SELECT id, question_id, option_text, is_correct, sort_order FROM quiz_options WHERE question_id=@qid ORDER BY sort_order", conn);
        cmd.Parameters.AddWithValue("@qid", questionId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (reader.Read())
        {
            opts.Add(new QuizOption
            {
                Id = reader.GetInt32("id"),
                QuestionId = reader.GetInt32("question_id"),
                OptionText = reader.GetString("option_text"),
                IsCorrect = reader.GetBoolean("is_correct"),
                SortOrder = reader.GetInt32("sort_order")
            });
        }
        return opts;
    }

    private static Quiz MapQuiz(MySqlDataReader r) => new()
    {
        Id = r.GetInt32("id"),
        LessonId = r.GetInt32("lesson_id"),
        LessonTitle = r.GetString("lesson_title"),
        CourseId = r.GetInt32("course_id"),
        Title = r.GetString("title"),
        Description = r.IsDBNull(r.GetOrdinal("description")) ? null : r.GetString("description"),
        TimeLimitMinutes = r.GetInt32("time_limit_minutes"),
        PassingScore = r.GetInt32("passing_score"),
        CreatedAt = r.GetDateTime("created_at")
    };
}
