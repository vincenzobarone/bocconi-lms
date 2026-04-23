using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BocconiLMS.Controllers;

[Authorize]
public class HelpController : Controller
{
    public IActionResult Guide() => View();
}
