using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace BocconiLMS.Services;

public sealed class AuditLogger : IAuditLogger
{
    private const string Tag = "[APP-AUDIT]";

    private readonly ILogger<AuditLogger> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public bool IsEnabled { get; }
    public string Level { get; }

    public AuditLogger(
        ILogger<AuditLogger> logger,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;

        var section = configuration.GetSection("AuditLog");
        IsEnabled = section.GetValue<bool>("Enabled", defaultValue: true);
        Level = (section.GetValue<string>("Level") ?? "standard").ToLowerInvariant();
    }

    /// <summary>
    /// Logs a standard-level event (written at Level=standard or verbose).
    /// Use for all write operations (create/edit/delete).
    /// </summary>
    public void Log(string action, string? target = null, string? outcome = "success",
                    string? user = null, string? ip = null)
    {
        if (!IsEnabled) return;
        if (Level == "minimal") return;
        Write(action, target, outcome, user, ip);
    }

    /// <summary>
    /// Logs a minimal-level event (written at ALL levels, including minimal).
    /// Use for authentication events (login, logout, password reset).
    /// </summary>
    public void LogMinimal(string action, string? target = null, string? outcome = "success",
                           string? user = null, string? ip = null)
    {
        if (!IsEnabled) return;
        Write(action, target, outcome, user, ip);
    }

    private void Write(string action, string? target, string? outcome, string? user, string? ip)
    {
        var ctx = _httpContextAccessor.HttpContext;
        var resolvedUser = user ?? ctx?.User?.FindFirstValue(ClaimTypes.Email)
                                ?? ctx?.User?.Identity?.Name
                                ?? "anonymous";
        var resolvedIp = ip ?? ctx?.Connection?.RemoteIpAddress?.ToString() ?? "-";
        var ts = DateTimeOffset.UtcNow.ToString("O");

        var targetPart = string.IsNullOrWhiteSpace(target) ? "" : $" | target={target}";
        _logger.LogInformation(
            $"{Tag} {ts} | user={{User}} | ip={{Ip}} | action={{Action}}{targetPart} | outcome={{Outcome}}",
            resolvedUser, resolvedIp, action, outcome);
    }
}
