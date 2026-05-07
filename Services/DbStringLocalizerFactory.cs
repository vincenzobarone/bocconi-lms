using Microsoft.Extensions.Localization;

namespace BocconiLMS.Services;

/// <summary>
/// Bridges ASP.NET Core DataAnnotations localization to the DB-backed TranslationService.
/// When [Required(ErrorMessage="validation.required")] fires, this localizer
/// resolves the key against the current user's language cookie.
/// </summary>
public sealed class DbStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly IHttpContextAccessor _http;

    public DbStringLocalizerFactory(IHttpContextAccessor http) => _http = http;

    public IStringLocalizer Create(Type resourceSource) => new DbStringLocalizer(_http);
    public IStringLocalizer Create(string baseName, string location) => new DbStringLocalizer(_http);
}

internal sealed class DbStringLocalizer : IStringLocalizer
{
    private readonly IHttpContextAccessor _http;

    public DbStringLocalizer(IHttpContextAccessor http) => _http = http;

    private string Resolve(string name)
    {
        var ts = _http.HttpContext?.RequestServices.GetService<TranslationService>();
        if (ts == null) return name;
        var val = ts.T(name);
        return val;
    }

    public LocalizedString this[string name]
    {
        get
        {
            var val = Resolve(name);
            return new LocalizedString(name, val, val == name);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var val = Resolve(name);
            try { val = string.Format(val, arguments); } catch { /* keep original */ }
            return new LocalizedString(name, val, false);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => Enumerable.Empty<LocalizedString>();
}
