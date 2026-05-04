using System.Text.Json;
using BocconiLMS.Models;
using BocconiLMS.Services;

namespace BocconiLMS.Middleware;

/// <summary>
/// Intercetta tutte le richieste sotto /api/* e richiede un header
/// X-Api-Key valido. Se mancante o non valida → 401 JSON.
/// La chiave validata viene depositata in HttpContext.Items["ApiKey"].
/// </summary>
public sealed class ApiKeyAuthenticationMiddleware
{
    public const string HeaderName    = "X-Api-Key";
    public const string ItemsKey      = "ApiKey";
    public const string PathPrefix    = "/api/";

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx, ApiKeyService apiKeys)
    {
        var path = ctx.Request.Path.Value ?? "/";
        if (!path.StartsWith(PathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        var presented = ctx.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(presented))
        {
            await WriteUnauthorizedAsync(ctx, "missing_api_key",
                $"Missing {HeaderName} header.");
            return;
        }

        var key = await apiKeys.ValidateAsync(presented);
        if (key == null)
        {
            _logger.LogWarning(
                "[$API-AUTH] invalid api key attempt | ip={Ip} | path={Path}",
                ctx.Connection.RemoteIpAddress?.ToString() ?? "-", path);
            await WriteUnauthorizedAsync(ctx, "invalid_api_key",
                "API key is invalid or revoked.");
            return;
        }

        ctx.Items[ItemsKey] = key;
        await _next(ctx);
    }

    private static async Task WriteUnauthorizedAsync(
        HttpContext ctx, string error, string message)
    {
        ctx.Response.StatusCode  = StatusCodes.Status401Unauthorized;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.Headers["WWW-Authenticate"] = "ApiKey";
        var body = JsonSerializer.Serialize(new { error, message });
        await ctx.Response.WriteAsync(body);
    }
}

public static class ApiKeyHttpContextExtensions
{
    public static ApiKey? GetApiKey(this HttpContext ctx) =>
        ctx.Items.TryGetValue(ApiKeyAuthenticationMiddleware.ItemsKey, out var v)
            ? v as ApiKey : null;

    public static bool ApiKeyHasScope(this HttpContext ctx, string scope)
    {
        var k = ctx.GetApiKey();
        if (k == null) return false;
        foreach (var s in k.ScopeList)
            if (string.Equals(s, scope, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
