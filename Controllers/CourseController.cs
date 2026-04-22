using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BocconiLMS.Data;
using BocconiLMS.Models;

namespace BocconiLMS.Controllers;

[Authorize]
public class CourseController : Controller
{
    private readonly CourseRepository _courses;
    private readonly LessonRepository _lessons;
    private readonly EnrollmentRepository _enrollments;

    public CourseController(CourseRepository courses, LessonRepository lessons, EnrollmentRepository enrollments)
    {
        _courses = courses;
        _lessons = lessons;
        _enrollments = enrollments;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private string CurrentRole => User.FindFirst(ClaimTypes.Role)!.Value;

    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> Dashboard()
    {
        var courses = CurrentRole == "Admin"
            ? await _courses.GetAllAsync()
            : await _courses.GetByTeacherAsync(CurrentUserId);

        var enrollments = new List<Enrollment>();
        foreach (var c in courses.Take(5))
        {
            var e = await _enrollments.GetByCourseAsync(c.Id);
            enrollments.AddRange(e.Take(3));
        }

        var vm = new TeacherDashboard
        {
            Courses = courses,
            RecentEnrollments = enrollments,
            TotalStudents = enrollments.DistinctBy(e => e.UserId).Count()
        };
        return View(vm);
    }

    public async Task<IActionResult> Index()
    {
        var courses = await _courses.GetAllAsync(publishedOnly: true);
        return View(courses);
    }

    public async Task<IActionResult> Details(int id)
    {
        var course = await _courses.GetByIdAsync(id);
        if (course == null) return NotFound();
        var lessons = await _lessons.GetByCourseAsync(id, CurrentUserId);
        bool isEnrolled = await _enrollments.IsEnrolledAsync(CurrentUserId, id);
        bool isOwner = CurrentRole is "Admin" || course.TeacherId == CurrentUserId;
        ViewBag.Lessons = lessons;
        ViewBag.IsEnrolled = isEnrolled;
        ViewBag.IsOwner = isOwner;
        return View(course);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enroll(int id)
    {
        await _enrollments.EnrollAsync(CurrentUserId, id);
        TempData["Success"] = "Iscrizione completata!";
        return RedirectToAction("Details", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unenroll(int id)
    {
        await _enrollments.UnenrollAsync(CurrentUserId, id);
        TempData["Success"] = "Disiscrizione completata.";
        return RedirectToAction("Details", new { id });
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpGet]
    public IActionResult Create() => View(new CourseFormViewModel());

    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CourseFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var course = new Course
        {
            Title = model.Title,
            Description = model.Description,
            Category = model.Category,
            TeacherId = CurrentUserId,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            IsPublished = model.IsPublished
        };
        var id = await _courses.CreateAsync(course);
        TempData["Success"] = "Corso creato!";
        return RedirectToAction("Details", new { id });
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var course = await _courses.GetByIdAsync(id);
        if (course == null) return NotFound();
        if (CurrentRole != "Admin" && course.TeacherId != CurrentUserId) return Forbid();
        return View(new CourseFormViewModel
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            Category = course.Category,
            StartDate = course.StartDate,
            EndDate = course.EndDate,
            IsPublished = course.IsPublished
        });
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CourseFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var course = await _courses.GetByIdAsync(id);
        if (course == null) return NotFound();
        if (CurrentRole != "Admin" && course.TeacherId != CurrentUserId) return Forbid();
        course.Title = model.Title;
        course.Description = model.Description;
        course.Category = model.Category;
        course.StartDate = model.StartDate;
        course.EndDate = model.EndDate;
        course.IsPublished = model.IsPublished;
        await _courses.UpdateAsync(course);
        TempData["Success"] = "Corso aggiornato!";
        return RedirectToAction("Details", new { id });
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var course = await _courses.GetByIdAsync(id);
        if (course == null) return NotFound();
        if (CurrentRole != "Admin" && course.TeacherId != CurrentUserId) return Forbid();
        await _courses.DeleteAsync(id);
        TempData["Success"] = "Corso eliminato.";
        return RedirectToAction("Dashboard");
    }

    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> Students(int id)
    {
        var course = await _courses.GetByIdAsync(id);
        if (course == null) return NotFound();
        var students = await _enrollments.GetByCourseAsync(id);
        ViewBag.Course = course;
        return View(students);
    }
}
