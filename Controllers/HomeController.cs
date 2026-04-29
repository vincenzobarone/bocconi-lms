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
        var coursesEnabled   = await _features.IsCoursesEnabledAsync();
        var materialsEnabled = await _features.IsMaterialsEnabledAsync();

        if (User.Identity?.IsAuthenticated == true && !User.IsInRole("Admin"))
        {
            if (!coursesEnabled && materialsEnabled)
                return RedirectToAction("Index", "Materials");
            if (!coursesEnabled && !materialsEnabled)
                return RedirectToAction("NoModules", "Home");
        }

        try
        {
            var courses = await _courses.GetAllAsync(publishedOnly: true);
            ViewBag.CoursesEnabled   = coursesEnabled;
            ViewBag.MaterialsEnabled = materialsEnabled;
            return View(courses.Take(6).ToList());
        }
        catch (MySqlException)
        {
            ViewBag.DbError = true;
            ViewBag.CoursesEnabled   = coursesEnabled;
            ViewBag.MaterialsEnabled = materialsEnabled;
            return View(new List<Course>());
        }
    }

    [Route("/healthz")]
    public IActionResult Health() => Ok("ok");

    [Authorize]
    public async Task<IActionResult> Dashboard()
    {
        if (User.IsInRole("Admin"))
            return RedirectToAction("PlatformFeatures", "Admin");

        var coursesEnabled   = await _features.IsCoursesEnabledAsync();
        var materialsEnabled = await _features.IsMaterialsEnabledAsync();

        if (!coursesEnabled && materialsEnabled)
            return RedirectToAction("Index", "Materials");

        if (!coursesEnabled && !materialsEnabled)
            return RedirectToAction("NoModules", "Home");

        if (User.IsInRole("Teacher"))
            return RedirectToAction("Dashboard", "Course");

        if (User.IsInRole("Student"))
            return RedirectToAction("Dashboard", "Student");

        return RedirectToAction("Index", "Materials");
    }

    [Authorize]
    public IActionResult NoModules()
    {
        if (User.IsInRole("Admin"))
            return RedirectToAction("PlatformFeatures", "Admin");
        return View();
    }

    public IActionResult Error()
    {
        return View();
    }
}
