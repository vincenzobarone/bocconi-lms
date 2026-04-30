namespace BocconiLMS.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedByName { get; set; }
    public int CourseCount { get; set; }
    public bool CanTeach { get; set; }
    public bool CanAttend { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}
