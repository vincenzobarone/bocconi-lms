using System.Reflection;

namespace BocconiLMS.Services;

public class AppVersionService
{
    public string CommitHash { get; }
    public string Environment { get; }

    public AppVersionService(IWebHostEnvironment env)
    {
        CommitHash = ReadGitHash();
        Environment = env.EnvironmentName;
    }

    private static string ReadGitHash()
    {
        // 1. AssemblyInformationalVersion — incorporato dal target MSBuild EmbedGitHash
        //    al momento di dotnet build/publish. Funziona sempre, anche senza .git.
        var infoVersion = Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?.Trim();

        if (!string.IsNullOrEmpty(infoVersion) && infoVersion != "1.0.0")
        {
            var candidate = infoVersion.Length >= 7 ? infoVersion[..7] : infoVersion;
            if (IsValidShortHash(candidate)) return candidate;
        }

        // 2. Fallback runtime: legge .git/HEAD (funziona in sviluppo quando
        //    il processo gira nella stessa cartella del repo).
        try
        {
            var gitDir = FindGitDir(AppContext.BaseDirectory);
            if (gitDir == null) return "–";

            var headFile = Path.Combine(gitDir, "HEAD");
            if (!File.Exists(headFile)) return "–";

            var head = File.ReadAllText(headFile).Trim();
            string hash;

            if (head.StartsWith("ref: "))
            {
                var refRelative = head[5..].Replace('/', Path.DirectorySeparatorChar);
                var refFile = Path.Combine(gitDir, refRelative);
                if (!File.Exists(refFile)) return "–";
                hash = File.ReadAllText(refFile).Trim();
            }
            else
            {
                hash = head;
            }

            return hash.Length >= 7 ? hash[..7] : hash;
        }
        catch
        {
            return "–";
        }
    }

    private static bool IsValidShortHash(string s) =>
        s.Length is >= 5 and <= 10 && s.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F'));

    private static string? FindGitDir(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            var gitPath = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(gitPath)) return gitPath;
            dir = dir.Parent;
        }
        return null;
    }
}
