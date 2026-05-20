using System.Reflection;
using System.Text.RegularExpressions;

namespace BocconiLMS.Services;

public class AppVersionService
{
    // Etichetta leggibile del build — sempre valorizzata dopo la compilazione.
    // Formato: "dd/MM/yyyy HH:mm (UTC)"  es. "20/05/2026 16:43 (UTC)"
    public string BuildLabel { get; }
    public string Environment { get; }

    public AppVersionService(IWebHostEnvironment env)
    {
        BuildLabel  = ReadBuildLabel();
        Environment = env.EnvironmentName;
    }

    private static string ReadBuildLabel()
    {
        var raw = Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?.Trim() ?? "";

        // Formato atteso dal target MSBuild EmbedBuildTimestamp: ddMMyyyyHHmm (12 cifre)
        // es. "200520261643" → "20/05/2026 16:43 (UTC)"
        var m = Regex.Match(raw, @"^(\d{2})(\d{2})(\d{4})(\d{2})(\d{2})$");
        if (m.Success)
        {
            var (dd, MM, yyyy, HH, mm) =
                (m.Groups[1].Value, m.Groups[2].Value,
                 m.Groups[3].Value, m.Groups[4].Value, m.Groups[5].Value);
            return $"{dd}/{MM}/{yyyy} {HH}:{mm} (UTC)";
        }

        // Fallback: mostra il valore raw se non è vuoto né il default SDK "1.0.0"
        if (!string.IsNullOrEmpty(raw) && raw != "1.0.0")
            return raw;

        return "";
    }
}
