namespace BocconiLMS.Models;

public class Document
{
    public int Id { get; set; }
    public int LessonId { get; set; }
    public string LessonTitle { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int CurrentVersion { get; set; }
    public DocumentVersion? ActiveVersion { get; set; }
}

public class DocumentVersion
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public int VersionNumber { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int UploadedBy { get; set; }
    public string UploaderName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UploadedAt { get; set; }

    public string FileSizeFormatted => FileSizeBytes < 1024 * 1024
        ? $"{FileSizeBytes / 1024.0:F1} KB"
        : $"{FileSizeBytes / (1024.0 * 1024):F2} MB";
}
