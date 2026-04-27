using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySqlConnector;
using BocconiLMS.Data;
using BocconiLMS.Models;
using BocconiLMS.Services;

namespace BocconiLMS.Controllers;

public class HomeController : Controller
{
    private readonly CourseRepository _courses;
    private readonly FeatureFlagService _features;

    public HomeController(CourseRepository courses, FeatureFlagService features)
    {
        _courses = courses;
        _features = features;
    }

    public async Task<IActionResult> Index()
    {
        var coursesEnabled = await _features.IsCoursesEnabledAsync();
        if (!coursesEnabled && User.Identity?.IsAuthenticated == true && !User.IsInRole("Admin"))
            return RedirectToAction("Index", "Materials");

        try
        {
            var courses = await _courses.GetAllAsync(publishedOnly: true);
            ViewBag.CoursesEnabled = coursesEnabled;
            return View(courses.Take(6).ToList());
        }
        catch (MySqlException)
        {
            ViewBag.DbError = true;
            ViewBag.CoursesEnabled = coursesEnabled;
            return View(new List<Course>());
        }
    }

    [Route("/healthz")]
    public IActionResult Health() => Ok("ok");

    [Authorize]
    public async Task<IActionResult> Dashboard()
    {
        var coursesEnabled = await _features.IsCoursesEnabledAsync();

        if (User.IsInRole("Admin"))
            return RedirectToAction("Index", "Admin");

        if (!coursesEnabled)
            return RedirectToAction("Index", "Materials");

        if (User.IsInRole("Teacher"))
            return RedirectToAction("Dashboard", "Course");

        return RedirectToAction("Dashboard", "Student");
    }

    public IActionResult Error()
    {
        return View();
    }
}
