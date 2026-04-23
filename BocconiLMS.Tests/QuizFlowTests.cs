using BocconiLMS.Tests.Fixtures;
using BocconiLMS.Tests.Helpers;

namespace BocconiLMS.Tests;

[Collection("Integration")]
public class QuizFlowTests : IAsyncLifetime
{
    private readonly LmsWebFactory _factory;
    private readonly DbTestHelper _db;

    private int _teacherId, _studentId;
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
        var suffix = Guid.NewGuid().ToString("N")[..8];
        _teacherId = await _db.CreateUserAsync(
            $"quiz_teacher_{suffix}@test.it",
            "Quiz", "Teacher", "Teacher", Password);
        _studentId = await _db.CreateUserAsync(
            $"quiz_student_{suffix}@test.it",
            "Quiz", "Student", "Student", Password);

        _courseId = await _db.CreateCourseAsync(_teacherId, $"Test Course {suffix}", isPublished: true);
        _lessonId = await _db.CreateLessonAsync(_courseId, $"Test Lesson {suffix}", isPublished: true);
        await _db.EnrollStudentAsync(_studentId, _courseId);

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

    [Fact]
    public async Task TakeQuiz_AsStudent_ShowsQuizPage()
    {
        var client = _factory.CreateClientWithCookies();
        await LoginAsync(client, await GetEmailAsync(_studentId), Password);

        var response = await client.GetAsync($"/Quiz/Take/{_quizId}");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.True(html.Contains(_quizTitle),
            $"Expected quiz title '{_quizTitle}' in page. HTML snippet: {html[..Math.Min(500, html.Length)]}");
        Assert.True(html.Contains("Domanda 1"),
            $"Expected 'Domanda 1' in page (questions not loaded?). HTML snippet: {html[..Math.Min(500, html.Length)]}");
        Assert.Contains(_questionText, html);
    }

    [Fact]
    public async Task SubmitQuiz_WithCorrectAnswer_ReturnsPassedResult()
    {
        var client = _factory.CreateClientWithCookies();
        await LoginAsync(client, await GetEmailAsync(_studentId), Password);

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
        await LoginAsync(resultClient, await GetEmailAsync(_studentId), Password);
        var resultResponse = await resultClient.GetAsync(location);
        Assert.Equal(System.Net.HttpStatusCode.OK, resultResponse.StatusCode);
        var resultHtml = await resultResponse.Content.ReadAsStringAsync();
        Assert.Contains("100", resultHtml);
    }

    [Fact]
    public async Task SubmitQuiz_WithWrongAnswer_ReturnsFailedResult()
    {
        var client = _factory.CreateClientWithCookies();
        await LoginAsync(client, await GetEmailAsync(_studentId), Password);

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
        var location = response.Headers.Location?.ToString() ?? "";
        Assert.Contains("/Quiz/Result", location, StringComparison.OrdinalIgnoreCase);

        var resultClient = _factory.CreateClientWithCookies();
        await LoginAsync(resultClient, await GetEmailAsync(_studentId), Password);
        var resultResponse = await resultClient.GetAsync(location);
        Assert.Equal(System.Net.HttpStatusCode.OK, resultResponse.StatusCode);
        var resultHtml = await resultResponse.Content.ReadAsStringAsync();
        Assert.Contains("0", resultHtml);
    }

    [Fact]
    public async Task TakeQuiz_Unauthenticated_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync($"/Quiz/Take/{_quizId}");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? "";
        Assert.Contains("/Account/Login", location, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QuizHistory_AfterAttempt_ShowsAttempt()
    {
        var client = _factory.CreateClientWithCookies();
        await LoginAsync(client, await GetEmailAsync(_studentId), Password);

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
            $"Expected score or quiz title in history page. HTML: {historyHtml[..Math.Min(500, historyHtml.Length)]}");
    }

    [Fact]
    public async Task TakeQuiz_StudentNotEnrolled_RedirectsToAccessDenied()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var unenrolledEmail = $"unenrolled_{suffix}@test.it";
        await _db.CreateUserAsync(
            unenrolledEmail,
            "Un", "Enrolled", "Student", Password);

        var client = _factory.CreateClientWithCookies();
        await LoginAsync(client, unenrolledEmail, Password);

        var response = await client.GetAsync($"/Quiz/Take/{_quizId}");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? "";
        Assert.Contains("/Account/AccessDenied", location, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> GetEmailAsync(int userId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlConnector.MySqlCommand("SELECT email FROM users WHERE id=@id LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@id", userId);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var loginPage = await client.GetAsync("/Account/Login");
        var html = await loginPage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        });
        await client.PostAsync("/Account/Login", form);
    }
}
