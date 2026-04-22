using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BocconiLMS.Data;
using BocconiLMS.Models;

namespace BocconiLMS.Controllers;

[Authorize]
public class LessonController : Controller
{
    private readonly LessonRepository _lessons;
    private readonly CourseRepository _courses;
    private readonly DocumentRepository _documents;
    private readonly QuizRepository _quizzes;
    private readonly EnrollmentRepository _enrollments;
    private readonly ProgressRepository _progress;

    public LessonController(LessonRepository lessons, CourseRepository courses,
        DocumentRepository documents, QuizRepository quizzes,
        EnrollmentRepository enrollments, ProgressRepository progress)
    {
        _lessons = lessons;
        _courses = courses;
        _documents = documents;
        _quizzes = quizzes;
        _enrollments = enrollments;
        _progress = progress;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private string CurrentRole => User.FindFirst(ClaimTypes.Role)!.Value;

    public async Task<IActionResult> Details(int id)
    {
        var lesson = await _lessons.GetByIdAsync(id, CurrentUserId);
        if (lesson == null) return NotFound();

        bool hasAccess = CurrentRole is "Admin" or "Teacher" ||
            await _enrollments.IsEnrolledAsync(CurrentUserId, lesson.CourseId);
        if (!hasAccess) return RedirectToAction("Details", "Course", new { id = lesson.CourseId });

        var documents = await _documents.GetByLessonAsync(id);
        var quizzes = await _quizzes.GetByLessonAsync(id);

        if (CurrentRole == "Student")
            await _progress.MarkLessonCompletedAsync(CurrentUserId, id);

        ViewBag.Documents = documents;
        ViewBag.Quizzes = quizzes;
        ViewBag.IsOwner = CurrentRole is "Admin" || (await _courses.GetByIdAsync(lesson.CourseId))?.TeacherId == CurrentUserId;
        return View(lesson);
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpGet]
    public async Task<IActionResult> Create(int courseId)
    {
        var course = await _courses.GetByIdAsync(courseId);
        if (course == null) return NotFound();
        var model = new LessonFormViewModel { CourseId = courseId };
        ViewBag.CourseTitle = course.Title;
        return View(model);
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LessonFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var lesson = new Lesson
        {
            CourseId = model.CourseId,
            Title = model.Title,
            Content = model.Content,
            SortOrder = model.SortOrder,
            IsPublished = model.IsPublished
        };
        await _lessons.CreateAsync(lesson);
        TempData["Success"] = "Lezione creata!";
        return RedirectToAction("Details", "Course", new { id = model.CourseId });
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var lesson = await _lessons.GetByIdAsync(id);
        if (lesson == null) return NotFound();
        return View(new LessonFormViewModel
        {
            Id = lesson.Id,
            CourseId = lesson.CourseId,
            Title = lesson.Title,
            Content = lesson.Content,
            SortOrder = lesson.SortOrder,
            IsPublished = lesson.IsPublished
        });
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, LessonFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var lesson = await _lessons.GetByIdAsync(id);
        if (lesson == null) return NotFound();
        lesson.Title = model.Title;
        lesson.Content = model.Content;
        lesson.SortOrder = model.SortOrder;
        lesson.IsPublished = model.IsPublished;
        await _lessons.UpdateAsync(lesson);
        TempData["Success"] = "Lezione aggiornata!";
        return RedirectToAction("Details", new { id });
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var lesson = await _lessons.GetByIdAsync(id);
        if (lesson == null) return NotFound();
        var courseId = lesson.CourseId;
        await _lessons.DeleteAsync(id);
        TempData["Success"] = "Lezione eliminata.";
        return RedirectToAction("Details", "Course", new { id = courseId });
    }
}
