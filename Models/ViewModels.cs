using System.ComponentModel.DataAnnotations;

namespace BocconiLMS.Models;

public class ResetLandingPostModel
{
    public string Token { get; set; } = string.Empty;
}

public class DashboardViewModel
{
    public bool IsAdmin { get; set; }
    public bool IsTeacher { get; set; }
    public bool IsStudent { get; set; }
    public bool IsPending { get; set; }
    public bool MaterialsEnabled { get; set; }
    public bool CoursesEnabled { get; set; }
    // Materiali
    public int TotalMaterials { get; set; }
    public int RecentMaterials { get; set; }
    // Teacher
    public int TeacherCourseCount { get; set; }
    public int TeacherStudentCount { get; set; }
    public List<Course> TeacherCourses { get; set; } = [];
    // Student
    public int StudentEnrolledCount { get; set; }
    public int StudentCompletedLessons { get; set; }
    public List<Enrollment>    StudentEnrollments    { get; set; } = [];
    public List<QuizAttempt>   StudentRecentAttempts { get; set; } = [];
    // Admin
    public DashboardStats? AdminStats { get; set; }
    // Platform
    public string PlatformTimezone { get; set; } = "Europe/Rome";
}

public class CourseStatsViewModel
{
    public Course Course { get; set; } = null!;
    public int EnrolledCount { get; set; }
    public List<LessonCompletionStat> LessonStats { get; set; } = [];
    public List<QuizStat>             QuizStats    { get; set; } = [];
}

public class LessonCompletionStat
{
    public int    LessonId      { get; set; }
    public string LessonTitle   { get; set; } = "";
    public int    CompletedCount { get; set; }
}

public class QuizStat
{
    public int    QuizId        { get; set; }
    public string QuizTitle     { get; set; } = "";
    public string LessonTitle   { get; set; } = "";
    public int    PassingScore  { get; set; }
    public int    TotalAttempts { get; set; }
    public int    UniqueStudents { get; set; }
    public double AvgScore      { get; set; }
    public int    MaxScore      { get; set; }
    public int    PassedCount   { get; set; }
}

public class AjaxToggleRequest
{
    public bool Value { get; set; }
}

public class AjaxRolesRequest
{
    public List<string> Roles { get; set; } = new();
}

public class AjaxCourseNotifyRequest
{
    public string Item { get; set; } = string.Empty;
    public bool Value { get; set; }
}

public class LoginViewModel
{
    [Required(ErrorMessage = "validation.required")]
    [EmailAddress(ErrorMessage = "validation.email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "validation.required")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public class PublicRegisterViewModel
{
    [Required(ErrorMessage = "validation.required")]
    [MaxLength(100, ErrorMessage = "validation.max_length")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "validation.required")]
    [MaxLength(100, ErrorMessage = "validation.max_length")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "validation.required")]
    [EmailAddress(ErrorMessage = "validation.email")]
    [MaxLength(255, ErrorMessage = "validation.max_length")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "validation.required")]
    [MinLength(8, ErrorMessage = "validation.min_length")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "validation.required")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "validation.password_match")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string MathCaptchaAnswer { get; set; } = string.Empty;
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "validation.required")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "validation.required")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "validation.required")]
    [EmailAddress(ErrorMessage = "validation.email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "validation.required")]
    [MinLength(8, ErrorMessage = "validation.min_length")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "validation.required")]
    public string Role { get; set; } = "Student";
}

public class CourseFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "validation.required")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "validation.required")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "validation.required")]
    public string Category { get; set; } = string.Empty;

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsPublished { get; set; }

    public int? TeacherId { get; set; }
    public bool IsAdminView { get; set; }
    public List<TeacherOption> AvailableTeachers { get; set; } = new();
}

public record TeacherOption(int Id, string FullName);

public class ReorderRequest
{
    public int CourseId { get; set; }
    public List<int> Ids { get; set; } = new();
}

public class LessonFormViewModel
{
    public int Id { get; set; }
    public int CourseId { get; set; }

    [Required(ErrorMessage = "validation.required")]
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
}

public class DocumentUploadViewModel
{
    public int LessonId { get; set; }
    public int? DocumentId { get; set; }

    [Required(ErrorMessage = "validation.required")]
    public string Title { get; set; } = string.Empty;

    public string? Notes { get; set; }

    [Required(ErrorMessage = "validation.required")]
    public IFormFile? File { get; set; }
}

public class QuizFormViewModel
{
    public int Id { get; set; }
    public int LessonId { get; set; }

    [Required(ErrorMessage = "validation.required")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Range(5, 180, ErrorMessage = "validation.range")]
    public int TimeLimitMinutes { get; set; } = 30;

    [Range(1, 100, ErrorMessage = "validation.range")]
    public int PassingScore { get; set; } = 60;
}

public class QuizSubmitViewModel
{
    public int QuizId { get; set; }
    public Dictionary<int, int> Answers { get; set; } = new();
}

public class EmailSettingsViewModel
{
    public bool Enabled { get; set; }

    [Display(Name = "Host SMTP")]
    public string Host { get; set; } = string.Empty;

    [Display(Name = "Porta")]
    [Range(1, 65535, ErrorMessage = "validation.range")]
    public int Port { get; set; } = 587;

    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Display(Name = "Password")]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [Display(Name = "Email mittente")]
    [EmailAddress(ErrorMessage = "validation.email")]
    public string FromEmail { get; set; } = string.Empty;

    [Display(Name = "Nome mittente")]
    public string FromName { get; set; } = "Bocconi LMS";

    [Display(Name = "Usa SSL")]
    public bool UseSsl { get; set; }

    [Display(Name = "Email destinatario test")]
    [EmailAddress(ErrorMessage = "validation.email")]
    public string? TestEmailRecipient { get; set; }

    // ── Notifiche materiali (3 gruppi separati) ──────────────────────────
    public bool NotifyMaterialCreated { get; set; }
    public List<string> MaterialCreatedRoles { get; set; } = new();

    public bool NotifyMaterialUpdated { get; set; }
    public List<string> MaterialUpdatedRoles { get; set; } = new();

    public bool NotifyMaterialDeleted { get; set; }
    public List<string> MaterialDeletedRoles { get; set; } = new();

    public List<string> AvailableRoles { get; set; } = new();

    public bool CoursesNotificationsEnabled { get; set; }
    public bool NotifyStudentOnEnroll { get; set; }
    public bool NotifyStudentOnQuizCompleted { get; set; }
    public bool NotifyTeacherOnQuizCompleted { get; set; }
    public bool NotifyTeacherOnStudentEnrolled { get; set; }
    public bool CourseModuleEnabled { get; set; }
}


public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "validation.required")]
    [MinLength(8, ErrorMessage = "validation.min_length")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "validation.required")]
    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "validation.password_match")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "validation.required")]
    [EmailAddress(ErrorMessage = "validation.email")]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "validation.required")]
    [MinLength(8, ErrorMessage = "validation.min_length")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "validation.required")]
    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "validation.password_match")]
    public string ConfirmPassword { get; set; } = string.Empty;
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

public class RoleViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int UserCount { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? CreatedByName { get; set; }
    public bool CanTeach { get; set; }
    public bool CanAttend { get; set; }
    public bool IsAdmin => Name.Equals("Admin", StringComparison.OrdinalIgnoreCase);
}

public class UsersAndRolesViewModel
{
    public List<User> Users { get; set; } = new();
    public List<RoleViewModel> Roles { get; set; } = new();
    public List<Area> Areas { get; set; } = new();
    public string ActiveTab { get; set; } = "utenti";
}

public class RoleFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "validation.required")]
    [MaxLength(50, ErrorMessage = "validation.max_length")]
    [RegularExpression(@"^[a-zA-Z0-9_\s]+$", ErrorMessage = "validation.role_name_format")]
    public string Name { get; set; } = string.Empty;

    public List<string> Permissions { get; set; } = new();
}

public class MaterialFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "validation.required")]
    [MaxLength(255, ErrorMessage = "validation.max_length")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(255, ErrorMessage = "validation.max_length")]
    public string? AuthorName { get; set; }

    public int? OwnerId { get; set; }

    [Required(ErrorMessage = "validation.required")]
    public string Language { get; set; } = "Italiano";

    [Required]
    public int? DocumentTypeId { get; set; }

    public string Status { get; set; } = "draft";

    public string? Notes { get; set; }

    public IFormFile? File { get; set; }

    public bool ConvertToPdf { get; set; }

    [Required(ErrorMessage = "validation.required")]
    public int? AreaId { get; set; }

    [Required(ErrorMessage = "validation.required")]
    public DateTime? CatalogationDate { get; set; }

    public int? PageCount { get; set; }

    // ── Verified fields (set via modal) ──────────────────────────────────
    public int? FolderId { get; set; }
    [MaxLength(255)]
    public string? FolderName { get; set; }

    // ── Publish fields ────────────────────────────────────────────────────
    public bool IsPublishable { get; set; }
    [MaxLength(100)]
    public string? ExternalProtocolCode { get; set; }
    public int? PlatformId { get; set; }
    [MaxLength(500)]
    public string? ExternalLink { get; set; }
}

public class DocumentTypeFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "validation.required")]
    [MaxLength(255, ErrorMessage = "validation.max_length")]
    public string Name { get; set; } = string.Empty;
}

public class MaterialsIndexViewModel
{
    public List<Material> Materials { get; set; } = new();
    public string? SearchTitle { get; set; }
    public string? FilterLanguage { get; set; }
    public int? FilterTypeId { get; set; }
    public List<DocumentType> DocumentTypes { get; set; } = new();
    public int? FilterCatalogationYear { get; set; }
    public int? FilterModifiedYear { get; set; }
    public string? FilterFolderName { get; set; }
    public int? FilterFolderId { get; set; }
}

public class SystemLogEntry
{
    public long Id { get; set; }
    public string LogType { get; set; } = "";
    public string? UserEmail { get; set; }
    public string? Ip { get; set; }
    public string Action { get; set; } = "";
    public string? Target { get; set; }
    public string? Outcome { get; set; }
    public int? DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SystemLogsViewModel
{
    public List<SystemLogEntry> Logs { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 200;
    public string? FilterType { get; set; }
    public string? FilterUser { get; set; }
    public string? FilterOutcome { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}
