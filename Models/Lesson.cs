namespace BocconiLMS.Models;

public class Lesson
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public bool IsCompleted { get; set; } = false;
    public int? GroupId { get; set; }
}
