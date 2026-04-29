using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BocconiLMS.Data;
using BocconiLMS.Models;
using BocconiLMS.Services;

namespace BocconiLMS.Controllers;

[Authorize]
public class CourseController : Controller
{
    private readonly CourseRepository _courses;
    private readonly LessonRepository _lessons;
    private readonly EnrollmentRepository _enrollments;
    private readonly UserRepository _users;
    private readonly EmailService _email;
    private readonly SettingsRepository _settings;
    private readonly ILogger<CourseController> _logger;

    public CourseController(
        CourseRepository courses,
        LessonRepository lessons,
        EnrollmentRepository enrollments,
        UserRepository users,
        EmailService email,
        SettingsRepository settings,
        ILogger<CourseController> logger)
    {
        _courses = courses;
        _lessons = lessons;
        _enrollments = enrollments;
        _users = users;
        _email = email;
        _settings = settings;
        _logger = logger;
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
        bool isOwner = CurrentRole is "Admin" || course.TeacherId == CurrentUserId;
        if (!course.IsPublished && !isOwner)
            return NotFound();
        bool publishedOnly = !isOwner;
        var lessons = await _lessons.GetByCourseAsync(id, CurrentUserId, publishedOnly: publishedOnly);
        bool isEnrolled = await _enrollments.IsEnrolledAsync(CurrentUserId, id);
        ViewBag.Lessons = lessons;
        ViewBag.IsEnrolled = isEnrolled;
        ViewBag.IsOwner = isOwner;
        return View(course);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enroll(int id)
    {
        var course = await _courses.GetByIdAsync(id);
        if (course == null) return NotFound();
        bool isOwner = CurrentRole is "Admin" || course.TeacherId == CurrentUserId;
        if (!course.IsPublished && !isOwner)
            return NotFound();

        bool alreadyEnrolled = await _enrollments.IsEnrolledAsync(CurrentUserId, id);
        await _enrollments.EnrollAsync(CurrentUserId, id);

        if (!alreadyEnrolled)
        {
            var student = await _users.GetByIdAsync(CurrentUserId);
            if (student != null)
            {
                int capturedStudentId = CurrentUserId;
                int capturedCourseId  = id;
                _ = SendEnrollmentNotificationsAsync(student, course, capturedStudentId, capturedCourseId)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            _logger.LogError(t.Exception,
                                "Failed to send enrollment notifications for student {StudentId}, course {CourseId}.",
                                capturedStudentId, capturedCourseId);
                    }, TaskScheduler.Default);
            }
        }

        TempData["Success"] = "Iscrizione completata!";
        return RedirectToAction("Details", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unenroll(int id)
    {
        var course = await _courses.GetByIdAsync(id);
        if (course == null) return NotFound();
        bool isOwner = CurrentRole is "Admin" || course.TeacherId == CurrentUserId;
        if (!course.IsPublished && !isOwner)
            return NotFound();
        await _enrollments.UnenrollAsync(CurrentUserId, id);
        TempData["Success"] = "Disiscrizione completata.";
        return RedirectToAction("Details", new { id });
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new CourseFormViewModel { IsAdminView = CurrentRole == "Admin" };
        if (vm.IsAdminView)
            vm.AvailableTeachers = await GetTeacherOptionsAsync();
        return View(vm);
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CourseFormViewModel model)
    {
        model.IsAdminView = CurrentRole == "Admin";
        if (model.IsAdminView)
        {
            model.AvailableTeachers = await GetTeacherOptionsAsync();
            if (!model.TeacherId.HasValue || model.TeacherId == 0)
                ModelState.AddModelError("TeacherId", "Select a teacher.");
        }
        if (!ModelState.IsValid) return View(model);

        var course = new Course
        {
            Title       = model.Title,
            Description = model.Description,
            Category    = model.Category,
            TeacherId   = model.IsAdminView ? model.TeacherId!.Value : CurrentUserId,
            StartDate   = model.StartDate,
            EndDate     = model.EndDate,
            IsPublished = model.IsPublished,
            CreatedBy   = CurrentUserId
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
        var vm = new CourseFormViewModel
        {
            Id          = course.Id,
            Title       = course.Title,
            Description = course.Description,
            Category    = course.Category,
            StartDate   = course.StartDate,
            EndDate     = course.EndDate,
            IsPublished = course.IsPublished,
            TeacherId   = course.TeacherId,
            IsAdminView = CurrentRole == "Admin"
        };
        if (vm.IsAdminView)
            vm.AvailableTeachers = await GetTeacherOptionsAsync();
        return View(vm);
    }

    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CourseFormViewModel model)
    {
        model.IsAdminView = CurrentRole == "Admin";
        if (model.IsAdminView)
        {
            model.AvailableTeachers = await GetTeacherOptionsAsync();
            if (!model.TeacherId.HasValue || model.TeacherId == 0)
                ModelState.AddModelError("TeacherId", "Select a teacher.");
        }
        if (!ModelState.IsValid) return View(model);

        var course = await _courses.GetByIdAsync(id);
        if (course == null) return NotFound();
        if (CurrentRole != "Admin" && course.TeacherId != CurrentUserId) return Forbid();

        course.Title       = model.Title;
        course.Description = model.Description;
        course.Category    = model.Category;
        course.StartDate   = model.StartDate;
        course.EndDate     = model.EndDate;
        course.IsPublished = model.IsPublished;
        if (model.IsAdminView && model.TeacherId.HasValue)
            course.TeacherId = model.TeacherId.Value;

        await _courses.UpdateAsync(course);
        TempData["Success"] = "Corso aggiornato!";
        return RedirectToAction("Details", new { id });
    }

    private async Task<List<TeacherOption>> GetTeacherOptionsAsync()
    {
        var all = await _users.GetAllAsync();
        return all
            .Where(u => u.Role == "Teacher" && u.IsActive)
            .Select(u => new TeacherOption(u.Id, u.FullName))
            .OrderBy(t => t.FullName)
            .ToList();
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
        if (CurrentRole != "Admin" && course.TeacherId != CurrentUserId) return Forbid();
        var students = await _enrollments.GetByCourseAsync(id);
        ViewBag.Course = course;
        return View(students);
    }

    private async Task SendEnrollmentNotificationsAsync(
        User student, Course course,
        int studentId, int courseId)
    {
        if ((await _settings.GetAsync("Notifications:CoursesEnabled")) != "true") return;

        var notifyStudent = (await _settings.GetAsync("Notifications:StudentOnEnroll")) == "true";
        var notifyTeacher = (await _settings.GetAsync("Notifications:TeacherOnStudentEnrolled")) == "true";

        if (notifyStudent)
        {
            await _email.SendWelcomeEmailAsync(
                student.Email, student.FullName,
                course.Title, course.TeacherName);
            _logger.LogInformation(
                "Welcome email sent to student {StudentId} for course {CourseId}.", studentId, courseId);
        }

        if (notifyTeacher)
        {
            var teacher = await _users.GetByIdAsync(course.TeacherId);
            if (teacher != null)
            {
                await _email.SendTeacherEnrollmentNotificationAsync(
                    teacher.Email, teacher.FullName,
                    student.FullName, student.Email,
                    course.Title);
                _logger.LogInformation(
                    "Enrollment notification sent to teacher {TeacherId} for student {StudentId}, course {CourseId}.",
                    teacher.Id, studentId, courseId);
            }
        }
    }
}
