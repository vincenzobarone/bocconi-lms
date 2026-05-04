namespace BocconiLMS.Models;

public class DocumentType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int MaterialCount { get; set; }
}

public class Platform
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int MaterialCount { get; set; }
}

public class MaterialFolder
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class Material
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? AuthorName { get; set; }
    public int? OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string Language { get; set; } = "Italiano";
    public int? DocumentTypeId { get; set; }
    public string DocumentTypeName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = "draft";
    public int? ProtocolNumber { get; set; }
    public int? FolderId { get; set; }
    public string FolderName { get; set; } = string.Empty;

    public int? AreaId { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public DateTime? CatalogationDate { get; set; }
    public int? PageCount { get; set; }

    // ── Publish fields ────────────────────────────────────────────────────
    public bool IsPublishable { get; set; }
    public string? ExternalProtocolCode { get; set; }
    public int? PlatformId { get; set; }
    public string PlatformName { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public string? ExternalLink { get; set; }

    public int CurrentVersion { get; set; }
    public MaterialVersion? ActiveVersion { get; set; }

    public static readonly string[] Languages =
    [
        "Italiano", "Inglese", "Francese", "Tedesco", "Spagnolo",
        "Russo", "Cinese", "Arabo", "Portoghese", "Altro"
    ];

    public static readonly string[] Statuses = ["draft", "under_review", "verified"];

    public static string LangPrefix(string language) => language switch
    {
        "Italiano"   => "IT",
        "Inglese"    => "EN",
        "Francese"   => "FR",
        "Tedesco"    => "DE",
        "Spagnolo"   => "ES",
        "Russo"      => "RU",
        "Cinese"     => "ZH",
        "Arabo"      => "AR",
        "Portoghese" => "PT",
        _            => "XX"
    };
}

public class MaterialVersion
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
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

    private static readonly HashSet<string> _videoTypes =
        new(StringComparer.OrdinalIgnoreCase) { "MP4", "WEBM", "MOV", "AVI", "MKV" };

    public bool IsVideo => _videoTypes.Contains(FileType);

    public string VideoMimeType => FileType.ToUpperInvariant() switch
    {
        "MP4"  => "video/mp4",
        "WEBM" => "video/webm",
        "MOV"  => "video/quicktime",
        "AVI"  => "video/x-msvideo",
        "MKV"  => "video/x-matroska",
        _      => "video/octet-stream"
    };
}
