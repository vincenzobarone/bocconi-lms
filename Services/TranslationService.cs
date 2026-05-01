using BocconiLMS.Data;
using Microsoft.Extensions.Caching.Memory;

namespace BocconiLMS.Services;

public class TranslationService
{
    private readonly TranslationRepository _repo;
    private readonly SettingsRepository _settings;
    private readonly IMemoryCache _cache;
    private readonly IHttpContextAccessor _httpContext;

    public static readonly string[] AllSupportedCodes = ["en", "it", "es", "de"];
    private const string CachePrefix = "translations_";
    private const string EnabledLangsCacheKey = "enabled_languages";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan LangCacheDuration = TimeSpan.FromMinutes(10);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte>
        _autoInserted = new(StringComparer.OrdinalIgnoreCase);

    public TranslationService(
        TranslationRepository repo,
        SettingsRepository settings,
        IMemoryCache cache,
        IHttpContextAccessor httpContext)
    {
        _repo = repo;
        _settings = settings;
        _cache = cache;
        _httpContext = httpContext;
    }

    public IReadOnlyList<string> EnabledCodes =>
        _cache.GetOrCreate(EnabledLangsCacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = LangCacheDuration;
            return (IReadOnlyList<string>)_settings.GetEnabledLanguagesAsync().GetAwaiter().GetResult();
        }) ?? (IReadOnlyList<string>)AllSupportedCodes;

    public string CurrentLanguage
    {
        get
        {
            var cookie = _httpContext.HttpContext?.Request.Cookies["lang"] ?? "en";
            var enabled = EnabledCodes;
            return enabled.Contains(cookie) ? cookie : "en";
        }
    }

    public string T(string key)
    {
        var lang = CurrentLanguage;
        var dict = GetCachedLanguage(lang);
        if (dict.TryGetValue(key, out var val)) return val;

        // If the translation is missing, always return the key
        return key;
    }

    public string this[string key] => T(key);

    public void InvalidateCache()
    {
        foreach (var code in AllSupportedCodes)
            _cache.Remove(CachePrefix + code);
        _cache.Remove(EnabledLangsCacheKey);
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

    public IReadOnlyList<(string Code, string Flag, string Name)> EnabledLanguages
    {
        get
        {
            var enabled = EnabledCodes;
            return AllLanguages.Where(l => enabled.Contains(l.Code)).ToList();
        }
    }
}
