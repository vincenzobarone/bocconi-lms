namespace BocconiLMS.Models;

public class LessonGroup
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<Lesson> Lessons { get; set; } = new();
}
