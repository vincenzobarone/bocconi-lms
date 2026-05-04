using System.Security.Cryptography;
using BocconiLMS.Data;
using BocconiLMS.Models;

namespace BocconiLMS.Services;

/// <summary>
/// Genera, valida e gestisce le API key.
/// Formato: dida_pk_&lt;8charPrefix&gt;_&lt;32charSecret&gt;
/// In DB salviamo: prefix in chiaro (per lookup) + BCrypt hash del secret.
/// </summary>
public class ApiKeyService
{
    public  const string KeyPrefixLiteral = "dida_pk_";
    private const int    PrefixLen = 8;
    private const int    SecretLen = 32;
    private const string Alphabet  =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    private readonly ApiKeyRepository _repo;
    public ApiKeyService(ApiKeyRepository repo) => _repo = repo;

    public async Task<GeneratedApiKeyVm> GenerateAsync(string name, string scopes, string? createdBy)
    {
        var prefix = RandomToken(PrefixLen);
        var secret = RandomToken(SecretLen);
        var plain  = $"{KeyPrefixLiteral}{prefix}_{secret}";

        var key = new ApiKey
        {
            Name      = name.Trim(),
            KeyPrefix = prefix,
            KeyHash   = BCrypt.Net.BCrypt.HashPassword(secret, workFactor: 11),
            Scopes    = string.IsNullOrWhiteSpace(scopes) ? "logs:read" : scopes.Trim(),
            CreatedBy = createdBy
        };

        var id = await _repo.InsertAsync(key);
        return new GeneratedApiKeyVm
        {
            Id        = id,
            Name      = key.Name,
            KeyPrefix = prefix,
            PlainKey  = plain,
            Scopes    = key.Scopes,
        };
    }

    /// <summary>Ritorna la chiave valida (non revocata, hash matching) o null.</summary>
    public async Task<ApiKey?> ValidateAsync(string? presented)
    {
        if (string.IsNullOrWhiteSpace(presented)) return null;
        if (!presented.StartsWith(KeyPrefixLiteral, StringComparison.Ordinal)) return null;

        var rest = presented[KeyPrefixLiteral.Length..];
        var sep  = rest.IndexOf('_');
        if (sep != PrefixLen) return null;

        var prefix = rest[..PrefixLen];
        var secret = rest[(PrefixLen + 1)..];
        if (secret.Length != SecretLen) return null;

        var key = await _repo.GetByPrefixAsync(prefix);
        if (key == null || key.IsRevoked) return null;

        bool ok;
        try { ok = BCrypt.Net.BCrypt.Verify(secret, key.KeyHash); }
        catch { ok = false; }
        if (!ok) return null;

        _repo.TouchLastUsedFireAndForget(key.Id);
        return key;
    }

    private static string RandomToken(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        var sb = new System.Text.StringBuilder(length);
        for (int i = 0; i < length; i++)
            sb.Append(Alphabet[bytes[i] % Alphabet.Length]);
        return sb.ToString();
    }
}
