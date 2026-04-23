using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySqlConnector;
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
        catch (MySqlException)
        {
            ViewBag.DbError = true;
            return View(new List<Course>());
        }
    }

    [Route("/healthz")]
    public IActionResult Health() => Ok("ok");

    [Authorize]
    public IActionResult Dashboard()
    {
        if (User.IsInRole("Admin"))
            return RedirectToAction("Index", "Admin");
        if (User.IsInRole("Teacher"))
            return RedirectToAction("Dashboard", "Course");
        return RedirectToAction("Dashboard", "Student");
    }

    public IActionResult Error()
    {
        return View();
    }
}
