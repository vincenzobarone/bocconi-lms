using MySqlConnector;
using BocconiLMS.Models;

namespace BocconiLMS.Data;

public class SystemLogRepository
{
    private readonly DbHelper _db;
    public bool WriteToDatabase { get; }

    private static readonly HashSet<string> SkipExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".js", ".css", ".map", ".ico", ".png", ".jpg", ".jpeg", ".gif", ".svg",
          ".woff", ".woff2", ".ttf", ".eot", ".webp", ".avif" };

    public SystemLogRepository(DbHelper db, IConfiguration config)
    {
        _db = db;
        WriteToDatabase = config.GetSection("AuditLog").GetValue<bool>("WriteToDatabase", true);
    }

    public bool ShouldSkipPath(string path)
    {
        var ext = Path.GetExtension(path);
        return !string.IsNullOrEmpty(ext) && SkipExtensions.Contains(ext);
    }

    public void InsertFireAndForget(SystemLogEntry entry)
    {
        if (!WriteToDatabase) return;
        _ = Task.Run(async () =>
        {
            try { await InsertAsync(entry); }
            catch { /* non bloccare mai la request */ }
        });
    }

    private async Task InsertAsync(SystemLogEntry entry)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            INSERT INTO system_logs
                (log_type, user_email, ip, action, target, outcome, duration_ms, created_at)
            VALUES
                (@type, @user, @ip, @action, @target, @outcome, @dur, UTC_TIMESTAMP(3))", conn);
        cmd.Parameters.AddWithValue("@type",   entry.LogType);
        cmd.Parameters.AddWithValue("@user",   (object?)entry.UserEmail  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ip",     (object?)entry.Ip         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@action", entry.Action);
        cmd.Parameters.AddWithValue("@target", (object?)entry.Target     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@outcome",(object?)entry.Outcome    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@dur",    (object?)entry.DurationMs ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<(List<SystemLogEntry> Logs, int TotalCount)> GetPagedAsync(
        string? logType, string? userEmail, string? outcome,
        DateTime? dateFrom, DateTime? dateTo,
        int page, int pageSize)
    {
        var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(logType))   where.Add("log_type = @type");
        if (!string.IsNullOrWhiteSpace(userEmail)) where.Add("user_email LIKE @user");
        if (!string.IsNullOrWhiteSpace(outcome))   where.Add("outcome = @outcome");
        if (dateFrom.HasValue)                      where.Add("created_at >= @from");
        if (dateTo.HasValue)                        where.Add("created_at < @to");

        var whereClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        var offset = (page - 1) * pageSize;

        using var conn = _db.GetConnection();
        await conn.OpenAsync();

        int total;
        using (var countCmd = new MySqlCommand(
            $"SELECT COUNT(*) FROM system_logs {whereClause}", conn))
        {
            AddParams(countCmd, logType, userEmail, outcome, dateFrom, dateTo);
            total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
        }

        var list = new List<SystemLogEntry>();
        using var cmd = new MySqlCommand(
            $@"SELECT id, log_type, user_email, ip, action, target, outcome, duration_ms, created_at
               FROM system_logs {whereClause}
               ORDER BY created_at DESC
               LIMIT @limit OFFSET @offset", conn);
        AddParams(cmd, logType, userEmail, outcome, dateFrom, dateTo);
        cmd.Parameters.AddWithValue("@limit",  pageSize);
        cmd.Parameters.AddWithValue("@offset", offset);

        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(Map(r));

        return (list, total);
    }

    public async Task<int> DeleteOlderThanAsync(int days)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "DELETE FROM system_logs WHERE created_at < DATE_SUB(UTC_TIMESTAMP(), INTERVAL @d DAY)", conn);
        cmd.Parameters.AddWithValue("@d", days);
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> DeleteAllAsync()
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand("DELETE FROM system_logs", conn);
        return await cmd.ExecuteNonQueryAsync();
    }

    private static void AddParams(MySqlCommand cmd, string? logType, string? userEmail,
        string? outcome, DateTime? dateFrom, DateTime? dateTo)
    {
        if (!string.IsNullOrWhiteSpace(logType))   cmd.Parameters.AddWithValue("@type",    logType);
        if (!string.IsNullOrWhiteSpace(userEmail)) cmd.Parameters.AddWithValue("@user",    $"%{userEmail}%");
        if (!string.IsNullOrWhiteSpace(outcome))   cmd.Parameters.AddWithValue("@outcome", outcome);
        if (dateFrom.HasValue)                      cmd.Parameters.AddWithValue("@from",    dateFrom.Value);
        if (dateTo.HasValue)                        cmd.Parameters.AddWithValue("@to",      dateTo.Value.AddDays(1));
    }

    private static SystemLogEntry Map(MySqlDataReader r) => new()
    {
        Id          = r.GetInt64("id"),
        LogType     = r.GetString("log_type"),
        UserEmail   = r.IsDBNull(r.GetOrdinal("user_email"))  ? null : r.GetString("user_email"),
        Ip          = r.IsDBNull(r.GetOrdinal("ip"))          ? null : r.GetString("ip"),
        Action      = r.GetString("action"),
        Target      = r.IsDBNull(r.GetOrdinal("target"))      ? null : r.GetString("target"),
        Outcome     = r.IsDBNull(r.GetOrdinal("outcome"))     ? null : r.GetString("outcome"),
        DurationMs  = r.IsDBNull(r.GetOrdinal("duration_ms")) ? null : r.GetInt32("duration_ms"),
        CreatedAt   = r.GetDateTime("created_at"),
    };
}
