using Microsoft.Extensions.Diagnostics.HealthChecks;
using MySqlConnector;

namespace BocconiLMS.Data;

public class MySqlHealthCheck : IHealthCheck
{
    private readonly DbHelper _db;

    public MySqlHealthCheck(DbHelper db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync(cancellationToken);
            using var cmd = new MySqlCommand("SELECT 1", conn);
            await cmd.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy("MySQL connection OK");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MySQL connection failed", ex);
        }
    }
}
