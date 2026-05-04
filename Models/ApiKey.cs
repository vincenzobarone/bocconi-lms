using System.ComponentModel.DataAnnotations;

namespace BocconiLMS.Models;

public class ApiKey
{
    public int       Id           { get; set; }
    public string    Name         { get; set; } = "";
    public string    KeyPrefix    { get; set; } = "";
    public string    KeyHash      { get; set; } = "";
    public string    Scopes       { get; set; } = "logs:read";
    public string?   CreatedBy    { get; set; }
    public DateTime  CreatedAt    { get; set; }
    public DateTime? LastUsedAt   { get; set; }
    public DateTime? RevokedAt    { get; set; }

    public bool IsRevoked => RevokedAt.HasValue;

    public string[] ScopeList =>
        (Scopes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries
                                 | StringSplitOptions.TrimEntries);
}

public class CreateApiKeyVm
{
    [Required(ErrorMessage = "Il nome è obbligatorio.")]
    [StringLength(100)]
    public string Name { get; set; } = "";

    public string Scopes { get; set; } = "logs:read";
}

public class GeneratedApiKeyVm
{
    public int    Id        { get; set; }
    public string Name      { get; set; } = "";
    public string KeyPrefix { get; set; } = "";
    public string PlainKey  { get; set; } = "";
    public string Scopes    { get; set; } = "";
}

public class ApiKeysListVm
{
    public List<ApiKey>          Keys              { get; set; } = new();
    public GeneratedApiKeyVm?    NewlyGeneratedKey { get; set; }
}
