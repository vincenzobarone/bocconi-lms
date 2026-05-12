using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
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
    private readonly QuizRepository _quizzes;
    private readonly EnrollmentRepository _enrollments;
    private readonly ProgressRepository _progress;
    private readonly EmailService _email;
    private readonly ILogger<LessonController> _logger;
    private readonly MaterialRepository _materials;
    private readonly SettingsRepository _settings;
    private readonly IAuditLogger _audit;
    private readonly IWebHostEnvironment _env;
    private readonly LessonGroupRepository _groups;
    private readonly StorageOptions _storage;

    public LessonController(LessonRepository lessons, CourseRepository courses,
        QuizRepository quizzes,
        EnrollmentRepository enrollments, ProgressRepository progress,
        EmailService email, ILogger<LessonController> logger,
        MaterialRepository materials, SettingsRepository settings,
        IAuditLogger audit, IWebHostEnvironment env,
        LessonGroupRepository groups,
        IOptions<StorageOptions> storage)
    {
        _lessons = lessons;
        _courses = courses;
        _quizzes = quizzes;
        _enrollments = enrollments;
        _progress = progress;
        _email = email;
        _logger = logger;
        _materials = materials;
        _settings = settings;
        _audit = audit;
        _env = env;
        _groups = groups;
        _storage = storage.Value;
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

        if (User.IsInRole("CanAttend"))
        {
            if (!lesson.IsPublished) return Forbid();
            bool enrolled = await _enrollments.IsEnrolledAsync(CurrentUserId, lesson.CourseId);
            if (!enrolled) return RedirectToAction("Details", "Course", new { id = lesson.CourseId });
        }
        else if (User.IsInRole("CanTeach") && !isOwner)
        {
            return Forbid();
        }

        var quizzes = await _quizzes.GetByLessonAsync(id);
        var linkedMaterials = await _materials.GetByLessonAsync(id);

        if (User.IsInRole("CanAttend"))
            await _progress.MarkLessonCompletedAsync(CurrentUserId, id);

        // Determina quali materiali hanno il file fisicamente assente su disco
        var missingFileIds = new HashSet<int>();
        foreach (var mat in linkedMaterials)
        {
            if (mat.ActiveVersion != null)
            {
                var fullPath = Path.Combine(_env.WebRootPath, _storage.UploadRoot, mat.ActiveVersion.FilePath);
                if (!System.IO.File.Exists(fullPath))
                    missingFileIds.Add(mat.Id);
            }
        }

        ViewBag.Quizzes = quizzes;
        ViewBag.LinkedMaterials = linkedMaterials;
        ViewBag.IsOwner = isOwner;
        ViewBag.MissingFileIds = missingFileIds;

        if (isOwner)
            ViewBag.AvailableMaterials = await _materials.GetNotLinkedToLessonAsync(id);

        return View(lesson);
    }

    [Authorize(Roles = "CanTeach,Admin")]
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

    [Authorize(Roles = "CanTeach,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LessonFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        if (!await IsOwnerOrAdminAsync(model.CourseId)) return Forbid();
        var nextOrder = await _lessons.GetMaxSortOrderAsync(model.CourseId) + 1;
        var lesson = new Lesson
        {
            CourseId = model.CourseId,
            Title = model.Title,
            Content = model.Content,
            SortOrder = nextOrder,
            IsPublished = model.IsPublished
        };
        var lessonId = await _lessons.CreateAsync(lesson);
        _audit.Log("lesson.create", $"lesson#{lessonId} \"{lesson.Title}\" course#{lesson.CourseId}");

        if (model.IsPublished)
            await NotifyEnrolledStudentsAsync(model.CourseId, model.Title);

        TempData["Success"] = "§lesson.msg_created";
        return RedirectToAction("Details", "Course", new { id = model.CourseId });
    }

    [Authorize(Roles = "CanTeach,Admin")]
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
            IsPublished = lesson.IsPublished
        });
    }

    [Authorize(Roles = "CanTeach,Admin")]
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
        lesson.IsPublished = model.IsPublished;
        await _lessons.UpdateAsync(lesson);
        _audit.Log("lesson.edit", $"lesson#{id} \"{lesson.Title}\" course#{lesson.CourseId}");

        if (!wasPublished && model.IsPublished)
            await NotifyEnrolledStudentsAsync(lesson.CourseId, model.Title);

        TempData["Success"] = "§lesson.msg_updated";
        return RedirectToAction("Details", new { id });
    }

    private async Task NotifyEnrolledStudentsAsync(int courseId, string lessonTitle)
    {
        try
        {
            if ((await _settings.GetAsync("Notifications:CoursesEnabled")) != "true") return;

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

    [Authorize(Roles = "CanTeach,Admin")]
    [HttpPost]
    public async Task<IActionResult> Reorder([FromBody] ReorderRequest req)
    {
        if (req?.Ids == null || req.Ids.Count == 0)
            return BadRequest();
        if (!await IsOwnerOrAdminAsync(req.CourseId)) return Forbid();
        await _lessons.ReorderAsync(req.CourseId, req.Ids);
        _audit.Log("lesson.reorder", $"course#{req.CourseId} ids=[{string.Join(",", req.Ids)}]");
        return Ok();
    }

    [Authorize(Roles = "CanTeach,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var lesson = await _lessons.GetByIdAsync(id);
        if (lesson == null) return NotFound();
        if (!await IsOwnerOrAdminAsync(lesson.CourseId)) return Forbid();
        var courseId = lesson.CourseId;
        await _lessons.DeleteAsync(id);
        _audit.Log("lesson.delete", $"lesson#{id} \"{lesson.Title}\" course#{courseId}");
        TempData["Success"] = "§lesson.msg_deleted";
        return RedirectToAction("Details", "Course", new { id = courseId });
    }

    // ── Lesson Groups ────────────────────────────────────────────────────────

    [Authorize(Roles = "CanTeach,Admin")]
    [HttpPost]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Title)) return BadRequest();
        if (!await IsOwnerOrAdminAsync(req.CourseId)) return Forbid();
        var id = await _groups.CreateAsync(req.CourseId, req.Title.Trim());
        _audit.Log("lesson.group.create", $"group#{id} \"{req.Title}\" course#{req.CourseId}");
        return Ok(new { id, title = req.Title.Trim() });
    }

    [Authorize(Roles = "CanTeach,Admin")]
    [HttpPost]
    public async Task<IActionResult> RenameGroup([FromBody] RenameGroupRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Title)) return BadRequest();
        var group = await _groups.GetByIdAsync(req.GroupId);
        if (group == null) return NotFound();
        if (!await IsOwnerOrAdminAsync(group.CourseId)) return Forbid();
        await _groups.RenameAsync(req.GroupId, req.Title.Trim());
        _audit.Log("lesson.group.rename", $"group#{req.GroupId} \"{req.Title}\"");
        return Ok();
    }

    [Authorize(Roles = "CanTeach,Admin")]
    [HttpPost]
    public async Task<IActionResult> DeleteGroup([FromBody] DeleteGroupRequest req)
    {
        if (req == null) return BadRequest();
        var group = await _groups.GetByIdAsync(req.GroupId);
        if (group == null) return NotFound();
        if (!await IsOwnerOrAdminAsync(group.CourseId)) return Forbid();
        await _groups.DeleteAsync(req.GroupId);
        _audit.Log("lesson.group.delete", $"group#{req.GroupId} course#{group.CourseId}");
        return Ok();
    }

    [Authorize(Roles = "CanTeach,Admin")]
    [HttpPost]
    public async Task<IActionResult> SetLessonGroup([FromBody] SetLessonGroupRequest req)
    {
        if (req == null) return BadRequest();
        var lesson = await _lessons.GetByIdAsync(req.LessonId);
        if (lesson == null) return NotFound();
        if (!await IsOwnerOrAdminAsync(lesson.CourseId)) return Forbid();
        await _groups.SetLessonGroupAsync(req.LessonId, req.GroupId);
        _audit.Log("lesson.group.assign", $"lesson#{req.LessonId} → group#{req.GroupId?.ToString() ?? "none"}");
        return Ok();
    }
}

public record CreateGroupRequest(int CourseId, string Title);
public record RenameGroupRequest(int GroupId, string Title);
public record DeleteGroupRequest(int GroupId);
public record SetLessonGroupRequest(int LessonId, int? GroupId);
