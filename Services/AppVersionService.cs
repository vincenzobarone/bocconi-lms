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
            var psi = new ProcessStartInfo("git", "rev-parse --short HEAD")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory
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
}
