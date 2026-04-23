using BocconiLMS.Data;
using Microsoft.Extensions.Caching.Memory;

namespace BocconiLMS.Services;

public class TranslationService
{
    private readonly TranslationRepository _repo;
    private readonly IMemoryCache _cache;
    private readonly IHttpContextAccessor _httpContext;

    private static readonly string[] SupportedLanguages = ["en", "it", "es", "de"];
    private const string CachePrefix = "translations_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public TranslationService(
        TranslationRepository repo,
        IMemoryCache cache,
        IHttpContextAccessor httpContext)
    {
        _repo = repo;
        _cache = cache;
        _httpContext = httpContext;
    }

    public string CurrentLanguage
    {
        get
        {
            var cookie = _httpContext.HttpContext?.Request.Cookies["lang"] ?? "en";
            return SupportedLanguages.Contains(cookie) ? cookie : "en";
        }
    }

    public string T(string key, string defaultValue = "")
    {
        var lang = CurrentLanguage;
        var dict = GetCachedLanguage(lang);
        if (dict.TryGetValue(key, out var val)) return val;

        if (lang != "en")
        {
            var enDict = GetCachedLanguage("en");
            if (enDict.TryGetValue(key, out var enVal)) return enVal;
        }

        return string.IsNullOrEmpty(defaultValue) ? key : defaultValue;
    }

    public string this[string key] => T(key);

    public void InvalidateCache()
    {
        foreach (var lang in SupportedLanguages)
            _cache.Remove(CachePrefix + lang);
    }

    private Dictionary<string, string> GetCachedLanguage(string lang)
    {
        return _cache.GetOrCreate(CachePrefix + lang, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return _repo.GetByLanguageAsync(lang).GetAwaiter().GetResult();
        }) ?? new Dictionary<string, string>();
    }

    public static IReadOnlyList<(string Code, string Flag, string Name)> AllLanguages =>
    [
        ("en", "🇬🇧", "English"),
        ("it", "🇮🇹", "Italiano"),
        ("es", "🇪🇸", "Español"),
        ("de", "🇩🇪", "Deutsch")
    ];
}
