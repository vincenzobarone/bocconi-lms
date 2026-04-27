namespace BocconiLMS.Models;

public class Area
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int UserCount { get; set; }
}
