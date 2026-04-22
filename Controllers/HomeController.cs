using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BocconiLMS.Data;
using BocconiLMS.Models;

namespace BocconiLMS.Controllers;

public class HomeController : Controller
{
    private readonly CourseRepository _courses;

    public HomeController(CourseRepository courses)
    {
        _courses = courses;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var courses = await _courses.GetAllAsync(publishedOnly: true);
            return View(courses.Take(6).ToList());
        }
        catch
        {
            return View(new List<BocconiLMS.Models.Course>());
        }
    }

    [Route("/healthz")]
    public IActionResult Health() => Ok("ok");

    [Authorize]
    public IActionResult Dashboard()
    {
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        return role switch
        {
            "Admin" => RedirectToAction("Index", "Admin"),
            "Teacher" => RedirectToAction("Dashboard", "Course"),
            _ => RedirectToAction("Dashboard", "Student")
        };
    }

    public IActionResult Error()
    {
        return View();
    }
}
