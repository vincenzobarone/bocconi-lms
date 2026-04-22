using System.ComponentModel.DataAnnotations;

namespace BocconiLMS.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Email obbligatoria")]
    [EmailAddress(ErrorMessage = "Email non valida")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password obbligatoria")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Nome obbligatorio")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Cognome obbligatorio")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email obbligatoria")]
    [EmailAddress(ErrorMessage = "Email non valida")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password obbligatoria")]
    [MinLength(6, ErrorMessage = "Minimo 6 caratteri")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ruolo obbligatorio")]
    public string Role { get; set; } = "Student";
}

public class CourseFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Titolo obbligatorio")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Descrizione obbligatoria")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Categoria obbligatoria")]
    public string Category { get; set; } = string.Empty;

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsPublished { get; set; }
}

public class LessonFormViewModel
{
    public int Id { get; set; }
    public int CourseId { get; set; }

    [Required(ErrorMessage = "Titolo obbligatorio")]
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; }
}

public class DocumentUploadViewModel
{
    public int LessonId { get; set; }
    public int? DocumentId { get; set; }

    [Required(ErrorMessage = "Titolo obbligatorio")]
    public string Title { get; set; } = string.Empty;

    public string? Notes { get; set; }

    [Required(ErrorMessage = "File obbligatorio")]
    public IFormFile? File { get; set; }
}

public class QuizFormViewModel
{
    public int Id { get; set; }
    public int LessonId { get; set; }

    [Required(ErrorMessage = "Titolo obbligatorio")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Range(5, 180, ErrorMessage = "Tra 5 e 180 minuti")]
    public int TimeLimitMinutes { get; set; } = 30;

    [Range(1, 100, ErrorMessage = "Tra 1 e 100")]
    public int PassingScore { get; set; } = 60;
}

public class QuizSubmitViewModel
{
    public int QuizId { get; set; }
    public Dictionary<int, int> Answers { get; set; } = new();
}

public class StudentDashboard
{
    public List<Enrollment> Enrollments { get; set; } = new();
    public List<QuizAttempt> RecentAttempts { get; set; } = new();
    public int TotalCompleted { get; set; }
}

public class TeacherDashboard
{
    public List<Course> Courses { get; set; } = new();
    public List<Enrollment> RecentEnrollments { get; set; } = new();
    public int TotalStudents { get; set; }
}
