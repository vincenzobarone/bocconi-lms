using BocconiLMS.Data;
using BocconiLMS.Middleware;
using BocconiLMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BocconiLMS.Controllers.Api;

/// <summary>
/// Endpoint pubblico (autenticato via X-Api-Key) per la lettura dei log
/// di sistema da parte di sistemi esterni (es. SIEM, monitoring).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/logs")]
[Produces("application/json")]
public sealed class LogsController : ControllerBase
{
    private const string RequiredScope = "logs:read";
    private const int    MaxPageSize   = 1000;
    private const int    DefaultPageSize = 100;

    private readonly SystemLogRepository _logRepo;
    private readonly IAuditLogger _audit;
    private readonly ILogger<LogsController> _logger;

    public LogsController(
        SystemLogRepository logRepo,
        IAuditLogger audit,
        ILogger<LogsController> logger)
    {
        _logRepo = logRepo;
        _audit   = audit;
        _logger  = logger;
    }

    /// <summary>
    /// GET /api/v1/logs
    /// Filtri opzionali: logType, user (LIKE), outcome, dateFrom (yyyy-MM-dd),
    /// dateTo (yyyy-MM-dd, esclusivo del giorno successivo), page (1-based),
    /// pageSize (max 1000, default 100).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? logType,
        [FromQuery] string? user,
        [FromQuery] string? outcome,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize)
    {
        if (!HttpContext.ApiKeyHasScope(RequiredScope))
        {
            _audit.Log(
                action: "api.logs.read",
                target: HttpContext.GetApiKey()?.KeyPrefix,
                outcome: "forbidden");
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "insufficient_scope",
                message = $"This API key does not have the required scope '{RequiredScope}'.",
                required_scope = RequiredScope
            });
        }

        if (!_logRepo.WriteToDatabase)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "logs_db_disabled",
                message = "Database log persistence is disabled (AuditLog:WriteToDatabase=false). " +
                          "Logs are only available on STDOUT in this configuration."
            });
        }

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = DefaultPageSize;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        var (logs, total) = await _logRepo.GetPagedAsync(
            logType, user, outcome, dateFrom, dateTo, page, pageSize);

        var key = HttpContext.GetApiKey();
        _audit.Log(
            action: "api.logs.read",
            target: $"prefix={key?.KeyPrefix} count={logs.Count} total={total}",
            outcome: "success",
            user: $"apikey:{key?.Name}");

        var totalPages = pageSize > 0 ? (int)Math.Ceiling((double)total / pageSize) : 0;

        return Ok(new
        {
            page,
            page_size  = pageSize,
            total,
            total_pages = totalPages,
            count      = logs.Count,
            filters    = new { logType, user, outcome, dateFrom, dateTo },
            logs = logs.Select(l => new
            {
                id          = l.Id,
                timestamp   = l.CreatedAt.ToString("O"),
                log_type    = l.LogType,
                user_email  = l.UserEmail,
                ip          = l.Ip,
                action      = l.Action,
                target      = l.Target,
                outcome     = l.Outcome,
                duration_ms = l.DurationMs,
            })
        });
    }
}
