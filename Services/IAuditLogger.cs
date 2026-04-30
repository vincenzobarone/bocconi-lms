namespace BocconiLMS.Services;

public interface IAuditLogger
{
    void Log(string action, string? target = null, string? outcome = "success",
             string? user = null, string? ip = null);

    void LogMinimal(string action, string? target = null, string? outcome = "success",
                    string? user = null, string? ip = null);

    bool IsEnabled { get; }
    string Level { get; }
}
