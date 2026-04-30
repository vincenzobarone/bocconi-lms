using BocconiLMS.Tests.Fixtures;
using BocconiLMS.Tests.Helpers;

namespace BocconiLMS.Tests;

/// <summary>
/// Tests course, lesson and enrollment flows with dynamic roles.
/// No reference to hardcoded "Teacher"/"Student" role names.
/// </summary>
[Collection("Integration")]
public class CourseFlowTests : IAsyncLifetime
{
    private readonly LmsWebFactory _factory;
    private readonly DbTestHelper _db;

    private int _instructorId, _attendeeId;
    private int _courseId, _lessonId;
    private const string Password = "TestPass2024!";

    public CourseFlowTests()
    {
        _factory = new LmsWebFactory();
        _db = new DbTestHelper();
    }

    public async Task InitializeAsync()
    {
        await _db.CleanupOrphanTestDataAsync();
        await _db.EnsureSmtpDisabledAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var instructorRole = await _db.CreateRoleAsync($"TestRole_{suffix}_CrsInstructor", canTeach: true, canAttend: false);
        var attendeeRole   = await _db.CreateRoleAsync($"TestRole_{suffix}_CrsAttendee",   canTeach: false, canAttend: true);

        _instructorId = await _db.CreateUserAsync($"crs_instructor_{suffix}@test.it", "Crs", "Instructor", instructorRole, Password);
        _attendeeId   = await _db.CreateUserAsync($"crs_attendee_{suffix}@test.it",   "Crs", "Attendee",   attendeeRole,   Password);

        _courseId = await _db.CreateCourseAsync(_instructorId, $"Test Course {suffix}", isPublished: true);
        _lessonId = await _db.CreateLessonAsync(_courseId, $"Test Lesson {suffix}", isPublished: true);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        _factory.Dispose();
    }

    // ── Course list ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("instructor")]
    [InlineData("attendee")]
    public async Task CourseIndex_AuthenticatedUser_Returns200(string userKey)
    {
        var userId = userKey == "instructor" ? _instructorId : _attendeeId;
        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(userId), Password);

        var response = await client.GetAsync("/Course/Index");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // ── Create course (CanTeach only) ──────────────────────────────────────

    [Fact]
    public async Task CourseCreate_InstructorWithCanTeach_Returns200()
    {
        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_instructorId), Password);

        var response = await client.GetAsync("/Course/Create");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CourseCreate_AttendeeWithoutCanTeach_IsForbidden()
    {
        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_attendeeId), Password);

        var response = await client.GetAsync("/Course/Create");

        Assert.True(
            response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
            response.StatusCode == System.Net.HttpStatusCode.Redirect,
            $"Expected Forbidden or Redirect, got {response.StatusCode}");
    }

    // ── Course details ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("instructor")]
    [InlineData("attendee")]
    public async Task CourseDetails_AuthenticatedUser_Returns200(string userKey)
    {
        var userId = userKey == "instructor" ? _instructorId : _attendeeId;
        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(userId), Password);

        var response = await client.GetAsync($"/Course/Details/{_courseId}");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // ── Enrollment ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Enroll_AttendeeUser_SucceedsAndRedirects()
    {
        await _db.EnsureSmtpDisabledAsync();

        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_attendeeId), Password);

        var detailsPage = await client.GetAsync($"/Course/Details/{_courseId}");
        var html = await detailsPage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });
        var response = await client.PostAsync($"/Course/Enroll/{_courseId}", form);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);

        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlConnector.MySqlCommand(
            "SELECT COUNT(*) FROM enrollments WHERE user_id=@uid AND course_id=@cid", conn);
        cmd.Parameters.AddWithValue("@uid", _attendeeId);
        cmd.Parameters.AddWithValue("@cid", _courseId);
        Assert.True(Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0, "Enrollment should exist in DB.");
    }

    // ── Lesson details (after enrollment) ─────────────────────────────────

    [Fact]
    public async Task LessonDetails_EnrolledAttendee_Returns200()
    {
        await _db.EnrollStudentAsync(_attendeeId, _courseId);
        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_attendeeId), Password);

        var response = await client.GetAsync($"/Lesson/Details/{_lessonId}");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LessonDetails_NotEnrolledAttendee_IsForbidden()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var role = await _db.CreateRoleAsync($"TestRole_{suffix}_LsnAttendee", canTeach: false, canAttend: true);
        await _db.CreateUserAsync($"lsn_noenroll_{suffix}@test.it", "Lsn", "NoEnroll", role, Password);

        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, $"lsn_noenroll_{suffix}@test.it", Password);

        var response = await client.GetAsync($"/Lesson/Details/{_lessonId}");

        Assert.True(
            response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
            response.StatusCode == System.Net.HttpStatusCode.Redirect,
            $"Non-enrolled user should be blocked. Got {response.StatusCode}");
    }

    // ── Lesson create (CanTeach only) ──────────────────────────────────────

    [Fact]
    public async Task LessonCreate_InstructorWithCanTeach_Returns200()
    {
        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_instructorId), Password);

        var response = await client.GetAsync($"/Lesson/Create?courseId={_courseId}");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LessonCreate_AttendeeWithoutCanTeach_IsForbidden()
    {
        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_attendeeId), Password);

        var response = await client.GetAsync($"/Lesson/Create?courseId={_courseId}");

        Assert.True(
            response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
            response.StatusCode == System.Net.HttpStatusCode.Redirect,
            $"Expected Forbidden or Redirect, got {response.StatusCode}");
    }

    // ── Unauthenticated ───────────────────────────────────────────────────

    [Fact]
    public async Task CourseIndex_Unauthenticated_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Course/Index");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login",
            response.Headers.Location?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
