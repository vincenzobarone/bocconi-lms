namespace BocconiLMS.Models;

public class Quiz
{
    public int Id { get; set; }
    public int LessonId { get; set; }
    public string LessonTitle { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int TimeLimitMinutes { get; set; } = 30;
    public int PassingScore { get; set; } = 60;
    public DateTime CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public List<QuizQuestion> Questions { get; set; } = new();
}

public class QuizQuestion
{
    public int Id { get; set; }
    public int QuizId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<QuizOption> Options { get; set; } = new();
}

public class QuizOption
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string OptionText { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int SortOrder { get; set; }
}

public class QuizAttempt
{
    public int Id { get; set; }
    public int QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public int UserId { get; set; }
    public int Score { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public bool Passed { get; set; }
    public DateTime AttemptedAt { get; set; }
    public int PassingScore { get; set; }
}
