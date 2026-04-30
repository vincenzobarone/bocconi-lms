using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using MySqlConnector;
using BocconiLMS.Data;
using BocconiLMS.Models;
using BocconiLMS.Services;

namespace BocconiLMS.Controllers;

public class HomeController : Controller
{
    private readonly CourseRepository     _courses;
    private readonly MaterialRepository   _materials;
    private readonly EnrollmentRepository _enrollments;
    private readonly UserRepository       _users;
    private readonly FeatureFlagService   _features;
    private readonly DbHelper             _db;
    private readonly SettingsRepository   _settings;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment  _env;

    public HomeController(
        CourseRepository courses,
        MaterialRepository materials,
        EnrollmentRepository enrollments,
        UserRepository users,
        FeatureFlagService features,
        DbHelper db,
        SettingsRepository settings,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment env)
    {
        _courses     = courses;
        _materials   = materials;
        _enrollments = enrollments;
        _users       = users;
        _features    = features;
        _db          = db;
        _settings    = settings;
        _userManager = userManager;
        _env         = env;
    }

    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Dashboard");

        var coursesEnabled   = await _features.IsCoursesEnabledAsync();
        var materialsEnabled = await _features.IsMaterialsEnabledAsync();

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
        var materialsEnabled = await _features.IsMaterialsEnabledAsync();
        var coursesEnabled   = await _features.IsCoursesEnabledAsync();

        var hasAnyRole = User.Claims.Any(c =>
            c.Type == System.Security.Claims.ClaimTypes.Role &&
            !string.IsNullOrWhiteSpace(c.Value));

        var timezone = await _settings.GetAsync("Platform:Timezone") ?? "Europe/Rome";

        var vm = new DashboardViewModel
        {
            IsAdmin           = User.IsInRole("Admin"),
            IsTeacher         = User.IsInRole("CanTeach"),
            IsStudent         = User.IsInRole("CanAttend"),
            IsPending         = !hasAnyRole,
            MaterialsEnabled  = materialsEnabled,
            CoursesEnabled    = coursesEnabled,
            PlatformTimezone  = timezone,
        };

        if (vm.IsAdmin)
        {
            vm.AdminStats = await _users.GetStatsAsync();
            if (materialsEnabled)
                (vm.TotalMaterials, vm.RecentMaterials) = await GetMaterialCountsAsync();
            (vm.MigrationApplied, vm.MigrationTotal) = await GetMigrationCountsAsync();
            return View(vm);
        }

        if (vm.IsPending)
            return View(vm);

        // stats materiali (tutti gli utenti autenticati con un ruolo)
        if (materialsEnabled)
        {
            (vm.TotalMaterials, vm.RecentMaterials) = await GetMaterialCountsAsync();
        }

        if (coursesEnabled)
        {
            var appUser = await _userManager.GetUserAsync(User);
            if (appUser != null)
            {
                var userId = appUser.Id;

                if (vm.IsTeacher)
                {
                    var myCourses = await _courses.GetByTeacherAsync(userId);
                    vm.TeacherCourseCount  = myCourses.Count;
                    vm.TeacherStudentCount = await GetTeacherStudentCountAsync(userId);
                }
                else if (vm.IsStudent)
                {
                    var enrollments = await _enrollments.GetByUserAsync(userId);
                    vm.StudentEnrolledCount    = enrollments.Count;
                    vm.StudentCompletedLessons = enrollments.Sum(e => e.CompletedLessons);
                }
            }
        }

        return View(vm);
    }

    [Authorize]
    public IActionResult NoModules()
    {
        if (User.IsInRole("Admin"))
            return RedirectToAction("Dashboard");
        return View();
    }

    public IActionResult Error()
    {
        return View();
    }

    // ── helpers privati ───────────────────────────────────────────────────────

    private async Task<(int total, int recent)> GetMaterialCountsAsync()
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT COUNT(*) AS total,
                   SUM(CASE WHEN created_at >= NOW() - INTERVAL 30 DAY THEN 1 ELSE 0 END) AS recent
            FROM materials", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return (0, 0);
        return (reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                reader.IsDBNull(1) ? 0 : reader.GetInt32(1));
    }

    private async Task<int> GetTeacherStudentCountAsync(int teacherId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT COUNT(DISTINCT e.user_id)
            FROM enrollments e
            JOIN courses c ON e.course_id = c.id
            WHERE c.teacher_id = @tid", conn);
        cmd.Parameters.AddWithValue("@tid", teacherId);
        var result = await cmd.ExecuteScalarAsync();
        return result is long l ? (int)l : result is int i ? i : 0;
    }

    private async Task<(int applied, int total)> GetMigrationCountsAsync()
    {
        try
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(@"
                SELECT
                    (SELECT COUNT(*) FROM schema_migrations) AS applied,
                    @totalFiles AS total", conn);
            var migDir = Path.Combine(_env.ContentRootPath, "Migrations");
            int totalFiles = Directory.Exists(migDir)
                ? Directory.GetFiles(migDir, "*.sql").Length
                : 0;
            cmd.Parameters.AddWithValue("@totalFiles", totalFiles);
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return (0, totalFiles);
            return (reader.IsDBNull(0) ? 0 : reader.GetInt32(0), totalFiles);
        }
        catch { return (0, 0); }
    }
}
