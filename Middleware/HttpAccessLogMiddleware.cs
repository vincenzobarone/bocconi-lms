using System.Diagnostics;
using System.Security.Claims;

namespace BocconiLMS.Middleware;

public sealed class HttpAccessLogMiddleware
{
    private const string Tag = "[HTTP-ACCESS]";
    private readonly RequestDelegate _next;
    private readonly ILogger<HttpAccessLogMiddleware> _logger;

    private static readonly HashSet<string> SkippedPaths =
        new(StringComparer.OrdinalIgnoreCase) { "/health", "/favicon.ico" };

    public HttpAccessLogMiddleware(RequestDelegate next, ILogger<HttpAccessLogMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";

        if (SkippedPaths.Contains(path))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        await _next(context);
        sw.Stop();

        var user = context.User?.FindFirstValue(ClaimTypes.Email)
                ?? context.User?.Identity?.Name
                ?? "anonymous";
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "-";
        var method = context.Request.Method;
        var status = context.Response.StatusCode;
        var ms = (int)sw.ElapsedMilliseconds;

        _logger.LogInformation(
            $"{Tag} {{Method}} {{Path}} {{Status}} | user={{User}} | ip={{Ip}} | duration_ms={{Ms}}",
            method, path, status, user, ip, ms);
    }
}
