using System.Text.RegularExpressions;

namespace BocconiLMS.Tests.Helpers;

public static class CsrfHelper
{
    private static readonly Regex TokenRegex = new(
        @"<input[^>]+name=""__RequestVerificationToken""[^>]+value=""([^""]+)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Extract(string html)
    {
        var match = TokenRegex.Match(html);
        if (!match.Success)
            throw new InvalidOperationException(
                "CSRF token not found in the response HTML. Ensure the page contains a form with antiforgery token.");
        return match.Groups[1].Value;
    }
}
