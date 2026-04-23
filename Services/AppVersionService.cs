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
