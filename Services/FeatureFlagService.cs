using BocconiLMS.Data;
using Microsoft.Extensions.Caching.Memory;

namespace BocconiLMS.Services;

public class FeatureFlagService
{
    private readonly SettingsRepository _settings;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5);

    public const string CoursesModuleKey = "Features:CoursesModule";

    public FeatureFlagService(SettingsRepository settings, IMemoryCache cache)
    {
        _settings = settings;
        _cache = cache;
    }

    public async Task<bool> IsCoursesEnabledAsync()
    {
        return await GetBoolAsync(CoursesModuleKey, defaultValue: false);
    }

    public async Task SetCoursesEnabledAsync(bool enabled)
    {
        await _settings.SetAsync(CoursesModuleKey, enabled ? "true" : "false");
        _cache.Remove(CoursesModuleKey);
    }

    private async Task<bool> GetBoolAsync(string key, bool defaultValue)
    {
        if (_cache.TryGetValue<bool?>(key, out var cached) && cached.HasValue)
            return cached.Value;

        var raw = await _settings.GetAsync(key);
        var value = raw == null ? defaultValue : raw.Equals("true", StringComparison.OrdinalIgnoreCase);
        _cache.Set(key, (bool?)value, _cacheTtl);
        return value;
    }
}
