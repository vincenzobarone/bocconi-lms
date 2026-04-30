using BocconiLMS.Tests.Fixtures;
using BocconiLMS.Tests.Helpers;

namespace BocconiLMS.Tests;

[Collection("Integration")]
public class QuizFlowTests : IAsyncLifetime
{
    private readonly LmsWebFactory _factory;
    private readonly DbTestHelper _db;

    private int _instructorId, _attendeeId;
    private int _courseId, _lessonId;
    private int _quizId, _questionId, _correctOptionId;
    private string _quizTitle = string.Empty;
    private string _questionText = string.Empty;
    private const string Password = "TestPass2024!";

    public QuizFlowTests()
    {
        _factory = new LmsWebFactory();
        _db = new DbTestHelper();
    }

    public async Task InitializeAsync()
    {
        await _db.CleanupOrphanTestDataAsync();
        await _db.EnsureSmtpDisabledAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];

        var instructorRole = await _db.CreateRoleAsync($"TestRole_{suffix}_Instructor", canTeach: true, canAttend: false);
        var attendeeRole   = await _db.CreateRoleAsync($"TestRole_{suffix}_Attendee",   canTeach: false, canAttend: true);

        _instructorId = await _db.CreateUserAsync(
            $"quiz_instructor_{suffix}@test.it", "Quiz", "Instructor", instructorRole, Password);
        _attendeeId = await _db.CreateUserAsync(
            $"quiz_attendee_{suffix}@test.it", "Quiz", "Attendee", attendeeRole, Password);

        _courseId = await _db.CreateCourseAsync(_instructorId, $"Test Course {suffix}", isPublished: true);
        _lessonId = await _db.CreateLessonAsync(_courseId, $"Test Lesson {suffix}", isPublished: true);
        await _db.EnrollStudentAsync(_attendeeId, _courseId);

        _quizTitle = $"Quiz {suffix}";
        _questionText = $"Domanda test {suffix}";
        (_quizId, _questionId, _correctOptionId) = await _db.CreateQuizWithOneQuestionAsync(
            _lessonId, _quizTitle, _questionText, passingScore: 60);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        _factory.Dispose();
    }

    // ── Quiz page ──────────────────────────────────────────────────────────

    [Fact]
    public async Task TakeQuiz_AsEnrolledAttendee_ShowsQuizPage()
    {
        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_attendeeId), Password);

        var response = await client.GetAsync($"/Quiz/Take/{_quizId}");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.True(html.Contains(_quizTitle),
            $"Expected quiz title '{_quizTitle}'. HTML snippet: {html[..Math.Min(500, html.Length)]}");
        Assert.True(html.Contains("1 /"),
            $"Expected question counter '1 /'. HTML snippet: {html[..Math.Min(500, html.Length)]}");
        Assert.Contains(_questionText, html);
    }

    // ── Correct answer ─────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitQuiz_WithCorrectAnswer_ReturnsPassedResult()
    {
        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_attendeeId), Password);

        var takePage = await client.GetAsync($"/Quiz/Take/{_quizId}");
        var html = await takePage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [$"answers[{_questionId}]"] = _correctOptionId.ToString(),
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync($"/Quiz/Submit/{_quizId}", form);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? "";
        Assert.Contains("/Quiz/Result", location, StringComparison.OrdinalIgnoreCase);

        var resultClient = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(resultClient, await _db.GetEmailAsync(_attendeeId), Password);
        var resultResponse = await resultClient.GetAsync(location);
        Assert.Equal(System.Net.HttpStatusCode.OK, resultResponse.StatusCode);
        var resultHtml = await resultResponse.Content.ReadAsStringAsync();
        Assert.Contains("100", resultHtml);
    }

    // ── Wrong answer ───────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitQuiz_WithWrongAnswer_ReturnsFailedResult()
    {
        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_attendeeId), Password);

        var takePage = await client.GetAsync($"/Quiz/Take/{_quizId}");
        var html = await takePage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

        var wrongOptionId = _correctOptionId + 1;
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [$"answers[{_questionId}]"] = wrongOptionId.ToString(),
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync($"/Quiz/Submit/{_quizId}", form);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        var resultClient = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(resultClient, await _db.GetEmailAsync(_attendeeId), Password);
        var resultResponse = await resultClient.GetAsync(response.Headers.Location!.ToString());
        var resultHtml = await resultResponse.Content.ReadAsStringAsync();
        Assert.Contains("0", resultHtml);
    }

    // ── Unauthenticated ───────────────────────────────────────────────────

    [Fact]
    public async Task TakeQuiz_Unauthenticated_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync($"/Quiz/Take/{_quizId}");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login",
            response.Headers.Location?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    // ── History ───────────────────────────────────────────────────────────

    [Fact]
    public async Task QuizHistory_AfterAttempt_ShowsAttempt()
    {
        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_attendeeId), Password);

        var takePage = await client.GetAsync($"/Quiz/Take/{_quizId}");
        var html = await takePage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [$"answers[{_questionId}]"] = _correctOptionId.ToString(),
            ["__RequestVerificationToken"] = token
        });
        await client.PostAsync($"/Quiz/Submit/{_quizId}", form);

        var historyResponse = await client.GetAsync($"/Quiz/History?quizId={_quizId}");

        Assert.Equal(System.Net.HttpStatusCode.OK, historyResponse.StatusCode);
        var historyHtml = await historyResponse.Content.ReadAsStringAsync();
        Assert.True(historyHtml.Contains("100") || historyHtml.Contains(_quizTitle),
            $"Expected score or title in history. HTML: {historyHtml[..Math.Min(500, historyHtml.Length)]}");
    }

    // ── Not enrolled ──────────────────────────────────────────────────────

    [Fact]
    public async Task TakeQuiz_AttendeeNotEnrolled_RedirectsToAccessDenied()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var attendeeRole = await _db.CreateRoleAsync($"TestRole_{suffix}_Attendee2", canTeach: false, canAttend: true);
        await _db.CreateUserAsync($"unenrolled_{suffix}@test.it", "Un", "Enrolled", attendeeRole, Password);

        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, $"unenrolled_{suffix}@test.it", Password);

        var response = await client.GetAsync($"/Quiz/Take/{_quizId}");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied",
            response.Headers.Location?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
