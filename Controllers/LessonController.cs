using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BocconiLMS.Data;
using BocconiLMS.Models;
using BocconiLMS.Services;

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
    private readonly EmailService _email;
    private readonly ILogger<LessonController> _logger;
    private readonly MaterialRepository _materials;

    public LessonController(LessonRepository lessons, CourseRepository courses,
        DocumentRepository documents, QuizRepository quizzes,
        EnrollmentRepository enrollments, ProgressRepository progress,
        EmailService email, ILogger<LessonController> logger,
        MaterialRepository materials)
    {
        _lessons = lessons;
        _courses = courses;
        _documents = documents;
        _quizzes = quizzes;
        _enrollments = enrollments;
        _progress = progress;
        _email = email;
        _logger = logger;
        _materials = materials;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private string CurrentRole => User.FindFirst(ClaimTypes.Role)!.Value;

    private async Task<bool> IsOwnerOrAdminAsync(int courseId)
    {
        if (CurrentRole == "Admin") return true;
        var course = await _courses.GetByIdAsync(courseId);
        return course != null && course.TeacherId == CurrentUserId;
    }

    public async Task<IActionResult> Details(int id)
    {
        var lesson = await _lessons.GetByIdAsync(id, CurrentUserId);
        if (lesson == null) return NotFound();

        bool isOwner = await IsOwnerOrAdminAsync(lesson.CourseId);

        if (CurrentRole == "Student")
        {
            if (!lesson.IsPublished) return Forbid();
            bool enrolled = await _enrollments.IsEnrolledAsync(CurrentUserId, lesson.CourseId);
            if (!enrolled) return RedirectToAction("Details", "Course", new { id = lesson.CourseId });
        }
        else if (CurrentRole == "Teacher" && !isOwner)
        {
            return Forbid();
        }

        var documents = await _documents.GetByLessonAsync(id);
        var quizzes = await _quizzes.GetByLessonAsync(id);
        var linkedMaterials = await _materials.GetByLessonAsync(id);

        if (CurrentRole == "Student")
            await _progress.MarkLessonCompletedAsync(CurrentUserId, id);

        ViewBag.Documents = documents;
        ViewBag.Quizzes = quizzes;
        ViewBag.LinkedMaterials = linkedMaterials;
        ViewBag.IsOwner = isOwner;

        if (isOwner)
            ViewBag.AvailableMaterials = await _materials.GetNotLinkedToLessonAsync(id);

        return View(lesson);
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpGet]
    public async Task<IActionResult> Create(int courseId)
    {
        var course = await _courses.GetByIdAsync(courseId);
        if (course == null) return NotFound();
        if (!await IsOwnerOrAdminAsync(courseId)) return Forbid();
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
        if (!await IsOwnerOrAdminAsync(model.CourseId)) return Forbid();
        var lesson = new Lesson
        {
            CourseId = model.CourseId,
            Title = model.Title,
            Content = model.Content,
            SortOrder = model.SortOrder,
            IsPublished = model.IsPublished
        };
        await _lessons.CreateAsync(lesson);

        if (model.IsPublished)
            await NotifyEnrolledStudentsAsync(model.CourseId, model.Title);

        TempData["Success"] = "Lezione creata!";
        return RedirectToAction("Details", "Course", new { id = model.CourseId });
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var lesson = await _lessons.GetByIdAsync(id);
        if (lesson == null) return NotFound();
        if (!await IsOwnerOrAdminAsync(lesson.CourseId)) return Forbid();
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
        if (!await IsOwnerOrAdminAsync(lesson.CourseId)) return Forbid();
        bool wasPublished = lesson.IsPublished;
        lesson.Title = model.Title;
        lesson.Content = model.Content;
        lesson.SortOrder = model.SortOrder;
        lesson.IsPublished = model.IsPublished;
        await _lessons.UpdateAsync(lesson);

        if (!wasPublished && model.IsPublished)
            await NotifyEnrolledStudentsAsync(lesson.CourseId, model.Title);

        TempData["Success"] = "Lezione aggiornata!";
        return RedirectToAction("Details", new { id });
    }

    private async Task NotifyEnrolledStudentsAsync(int courseId, string lessonTitle)
    {
        try
        {
            var students = await _enrollments.GetEnrolledStudentContactsAsync(courseId);
            var course = await _courses.GetByIdAsync(courseId);
            if (course == null || students.Count == 0) return;

            foreach (var student in students)
            {
                try
                {
                    await _email.SendNewLessonNotificationAsync(
                        student.Email, student.FullName, lessonTitle, course.Title);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send new lesson notification to {Email}", student.Email);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send new lesson notifications for course {CourseId}", courseId);
        }
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var lesson = await _lessons.GetByIdAsync(id);
        if (lesson == null) return NotFound();
        if (!await IsOwnerOrAdminAsync(lesson.CourseId)) return Forbid();
        var courseId = lesson.CourseId;
        await _lessons.DeleteAsync(id);
        TempData["Success"] = "Lezione eliminata.";
        return RedirectToAction("Details", "Course", new { id = courseId });
    }
}
