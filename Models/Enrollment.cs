namespace BocconiLMS.Models;

public class Enrollment
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime EnrolledAt { get; set; }
    public int ProgressPercent { get; set; }
    public int CompletedLessons { get; set; }
    public int TotalLessons { get; set; }
}

public class DashboardStats
{
    public int TotalCourses { get; set; }
    public int TotalUsers { get; set; }
    public int TotalEnrollments { get; set; }
    public int TotalQuizAttempts { get; set; }
    public int ActiveStudents { get; set; }
    public int ActiveTeachers { get; set; }
}
