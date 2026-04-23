using Microsoft.AspNetCore.Mvc;
using BocconiLMS.Services;

namespace BocconiLMS.Controllers;

public class LanguageController : Controller
{
    private static readonly string[] Supported = ["en", "it", "es", "de"];

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Set(string lang, string? returnUrl = null)
    {
        if (Supported.Contains(lang))
        {
            Response.Cookies.Append("lang", lang, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                HttpOnly = false,
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            });
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return Redirect(Request.Headers.Referer.ToString().IsNullOrEmpty()
            ? "/" : Request.Headers.Referer.ToString());
    }
}

internal static class StringExtensions
{
    public static bool IsNullOrEmpty(this string? s) => string.IsNullOrEmpty(s);
}
