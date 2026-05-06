using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MySqlConnector;
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
    private readonly IAuditLogger _audit;
    private readonly LessonGroupRepository _groups;
    private readonly DbHelper _db;

    public CourseController(
        CourseRepository courses,
        LessonRepository lessons,
        EnrollmentRepository enrollments,
        UserRepository users,
        EmailService email,
        SettingsRepository settings,
        ILogger<CourseController> logger,
        IAuditLogger audit,
        LessonGroupRepository groups,
        DbHelper db)
    {
        _courses = courses;
        _lessons = lessons;
        _enrollments = enrollments;
        _users = users;
        _email = email;
        _settings = settings;
        _logger = logger;
        _audit = audit;
        _groups = groups;
        _db = db;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private string CurrentRole => User.FindFirst(ClaimTypes.Role)!.Value;

    [Authorize(Roles = "CanTeach,Admin")]
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
        var lessonGroups = await _groups.GetByCourseAsync(id);
        bool isEnrolled = await _enrollments.IsEnrolledAsync(CurrentUserId, id);
        ViewBag.Lessons = lessons;
        ViewBag.LessonGroups = lessonGroups;
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
            _audit.Log("course.enroll", $"course#{id} \"{course.Title}\"");

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

        TempData["Success"] = "§course.msg_enrolled";
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
        _audit.Log("course.unenroll", $"course#{id} \"{course.Title}\"");
        TempData["Success"] = "§course.msg_unenrolled";
        return RedirectToAction("Details", new { id });
    }

    [Authorize(Roles = "CanTeach,Admin")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new CourseFormViewModel { IsAdminView = CurrentRole == "Admin" };
        if (vm.IsAdminView)
            vm.AvailableTeachers = await GetTeacherOptionsAsync();
        return View(vm);
    }

    [Authorize(Roles = "CanTeach,Admin")]
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
        _audit.Log("course.create", $"course#{id} \"{course.Title}\"");
        if (model.IsPublished)
            _audit.Log("course.publish", $"course#{id} \"{course.Title}\"");
        TempData["Success"] = "§course.msg_created";
        return RedirectToAction("Details", new { id });
    }

    [Authorize(Roles = "CanTeach,Admin")]
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

    [Authorize(Roles = "CanTeach,Admin")]
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

        bool wasPublished = course.IsPublished;

        course.Title       = model.Title;
        course.Description = model.Description;
        course.Category    = model.Category;
        course.StartDate   = model.StartDate;
        course.EndDate     = model.EndDate;
        course.IsPublished = model.IsPublished;
        if (model.IsAdminView && model.TeacherId.HasValue)
            course.TeacherId = model.TeacherId.Value;

        await _courses.UpdateAsync(course);
        _audit.Log("course.edit", $"course#{id} \"{course.Title}\"");
        if (!wasPublished && model.IsPublished)
            _audit.Log("course.publish", $"course#{id} \"{course.Title}\"");
        else if (wasPublished && !model.IsPublished)
            _audit.Log("course.unpublish", $"course#{id} \"{course.Title}\"");
        TempData["Success"] = "§course.msg_updated";
        return RedirectToAction("Details", new { id });
    }

    private async Task<List<TeacherOption>> GetTeacherOptionsAsync()
    {
        var all = await _users.GetAllAsync();
        return all
            .Where(u => u.CanTeach && u.IsActive)
            .Select(u => new TeacherOption(u.Id, u.FullName))
            .OrderBy(t => t.FullName)
            .ToList();
    }

    [Authorize(Roles = "CanTeach,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var course = await _courses.GetByIdAsync(id);
        if (course == null) return NotFound();
        if (CurrentRole != "Admin" && course.TeacherId != CurrentUserId) return Forbid();
        await _courses.DeleteAsync(id);
        _audit.Log("course.delete", $"course#{id} \"{course.Title}\"");
        TempData["Success"] = "§course.msg_deleted";
        return RedirectToAction("Dashboard");
    }

    [Authorize(Roles = "CanTeach,Admin")]
    public async Task<IActionResult> Students(int id)
    {
        var course = await _courses.GetByIdAsync(id);
        if (course == null) return NotFound();
        if (CurrentRole != "Admin" && course.TeacherId != CurrentUserId) return Forbid();
        var students = await _enrollments.GetByCourseAsync(id);
        ViewBag.Course = course;
        return View(students);
    }

    [Authorize(Roles = "CanTeach,Admin")]
    public async Task<IActionResult> Stats(int id)
    {
        var course = await _courses.GetByIdAsync(id);
        if (course == null) return NotFound();
        if (CurrentRole != "Admin" && course.TeacherId != CurrentUserId) return Forbid();

        var vm = new CourseStatsViewModel
        {
            Course        = course,
            EnrolledCount = course.EnrolledCount,
        };

        using var conn = _db.GetConnection();
        await conn.OpenAsync();

        using (var cmd = new MySqlCommand(@"
            SELECT l.id, l.title,
                   COUNT(DISTINCT lp.user_id) AS completed_count
            FROM lessons l
            LEFT JOIN lesson_progress lp ON lp.lesson_id = l.id
            WHERE l.course_id = @cid AND l.is_published = 1
            GROUP BY l.id, l.title, l.sort_order
            ORDER BY l.sort_order", conn))
        {
            cmd.Parameters.AddWithValue("@cid", id);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                vm.LessonStats.Add(new LessonCompletionStat
                {
                    LessonId       = r.GetInt32("id"),
                    LessonTitle    = r.GetString("title"),
                    CompletedCount = r.GetInt32("completed_count"),
                });
        }

        using (var cmd = new MySqlCommand(@"
            SELECT q.id, q.title AS quiz_title, l.title AS lesson_title,
                   q.passing_score,
                   COUNT(qa.id)                                                 AS total_attempts,
                   COUNT(DISTINCT qa.user_id)                                   AS unique_students,
                   IFNULL(ROUND(AVG(qa.score), 1), 0)                           AS avg_score,
                   IFNULL(MAX(qa.score), 0)                                     AS max_score,
                   IFNULL(SUM(CASE WHEN qa.passed=1 THEN 1 ELSE 0 END), 0)     AS passed_count
            FROM quizzes q
            JOIN lessons l ON l.id = q.lesson_id
            LEFT JOIN quiz_attempts qa ON qa.quiz_id = q.id
            WHERE l.course_id = @cid
            GROUP BY q.id, q.title, l.title, q.passing_score, l.sort_order
            ORDER BY l.sort_order, q.id", conn))
        {
            cmd.Parameters.AddWithValue("@cid", id);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                vm.QuizStats.Add(new QuizStat
                {
                    QuizId         = r.GetInt32("id"),
                    QuizTitle      = r.GetString("quiz_title"),
                    LessonTitle    = r.GetString("lesson_title"),
                    PassingScore   = r.GetInt32("passing_score"),
                    TotalAttempts  = r.GetInt32("total_attempts"),
                    UniqueStudents = r.GetInt32("unique_students"),
                    AvgScore       = r.GetDouble("avg_score"),
                    MaxScore       = r.GetInt32("max_score"),
                    PassedCount    = r.GetInt32("passed_count"),
                });
        }

        return View(vm);
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
