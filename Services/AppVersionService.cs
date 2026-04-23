using System.Diagnostics;

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
            var repoRoot = FindGitRoot(AppContext.BaseDirectory);
            if (repoRoot == null) return "–";

            var psi = new ProcessStartInfo("git", "rev-parse --short HEAD")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = repoRoot
            };
            using var proc = Process.Start(psi);
            var hash = proc?.StandardOutput.ReadToEnd().Trim() ?? "";
            proc?.WaitForExit();
            return string.IsNullOrEmpty(hash) ? "–" : hash;
        }
        catch
        {
            return "–";
        }
    }

    private static string? FindGitRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
