using System.Net;
using System.Net.Http.Headers;
using BocconiLMS.Tests.Fixtures;
using BocconiLMS.Tests.Helpers;

namespace BocconiLMS.Tests;

[Collection("Integration")]
public class DocumentVersioningTests : IAsyncLifetime
{
    private readonly LmsWebFactory _factory;
    private readonly DbTestHelper _db;

    private int _teacherId, _studentId;
    private int _courseId, _lessonId;
    private const string Password = "TestPass2024!";
    private string _teacherEmail = string.Empty;

    public DocumentVersioningTests()
    {
        _factory = new LmsWebFactory();
        _db = new DbTestHelper();
    }

    public async Task InitializeAsync()
    {
        await _db.CleanupOrphanTestDataAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        _teacherEmail = $"doc_teacher_{suffix}@test.it";
        _teacherId = await _db.CreateUserAsync(
            _teacherEmail,
            "Doc", "Teacher", "Teacher", Password);
        _studentId = await _db.CreateUserAsync(
            $"doc_student_{suffix}@test.it",
            "Doc", "Student", "Student", Password);

        _courseId = await _db.CreateCourseAsync(_teacherId, $"Doc Course {suffix}", isPublished: true);
        _lessonId = await _db.CreateLessonAsync(_courseId, $"Doc Lesson {suffix}", isPublished: true);
        await _db.EnrollStudentAsync(_studentId, _courseId);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        _factory.Dispose();
    }

    [Fact]
    public async Task DocumentDetails_ShowsVersionHistory()
    {
        var docId = await _db.CreateDocumentAsync(_lessonId, "Test Doc for Details");
        await _db.CreateDocumentVersionAsync(docId, _teacherId, 1, isActive: false, notes: "Version One");
        await _db.CreateDocumentVersionAsync(docId, _teacherId, 2, isActive: true, notes: "Version Two");

        var client = _factory.CreateClientWithCookies();
        await LoginAsync(client, _teacherEmail, Password);

        var response = await client.GetAsync($"/Document/Details/{docId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Version One", html);
        Assert.Contains("Version Two", html);
    }

    [Fact]
    public async Task UploadDocument_AsTeacher_CreatesNewVersion()
    {
        var client = _factory.CreateClientWithCookies();
        await LoginAsync(client, _teacherEmail, Password);

        var getPage = await client.GetAsync($"/Document/Upload?lessonId={_lessonId}");
        Assert.Equal(HttpStatusCode.OK, getPage.StatusCode);
        var html = await getPage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("Test file content"));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");

        var formData = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent(_lessonId.ToString()), "LessonId" },
            { new StringContent("Uploaded Test Doc"), "Title" },
            { new StringContent(""), "Notes" },
            { fileContent, "File", "test_upload.txt" }
        };

        var response = await client.PostAsync("/Document/Upload", formData);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? "";
        Assert.Contains("/Lesson/Details", location, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RestoreVersion_AsTeacher_ChangesActiveVersion()
    {
        var docId = await _db.CreateDocumentAsync(_lessonId, "Restore Test Doc");
        var v1Id = await _db.CreateDocumentVersionAsync(docId, _teacherId, 1, isActive: false, notes: "V1");
        await _db.CreateDocumentVersionAsync(docId, _teacherId, 2, isActive: true, notes: "V2");

        var activeBefore = await _db.GetActiveVersionNumberAsync(docId);
        Assert.Equal(2, activeBefore);

        var client = _factory.CreateClientWithCookies();
        await LoginAsync(client, _teacherEmail, Password);

        var detailsPage = await client.GetAsync($"/Document/Details/{docId}");
        var html = await detailsPage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["documentId"] = docId.ToString(),
            ["versionId"] = v1Id.ToString(),
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Document/Restore", form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var activeAfter = await _db.GetActiveVersionNumberAsync(docId);
        Assert.Equal(1, activeAfter);
    }

    [Fact]
    public async Task UploadNewVersion_AsTeacher_IncrementsVersionNumber()
    {
        var docId = await _db.CreateDocumentAsync(_lessonId, "Multi-Version Doc");
        await _db.CreateDocumentVersionAsync(docId, _teacherId, 1, isActive: true, notes: "Initial version");

        var client = _factory.CreateClientWithCookies();
        await LoginAsync(client, _teacherEmail, Password);

        var getPage = await client.GetAsync($"/Document/Upload?lessonId={_lessonId}&documentId={docId}");
        Assert.Equal(HttpStatusCode.OK, getPage.StatusCode);
        var html = await getPage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("Updated file content v2"));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");

        var formData = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent(_lessonId.ToString()), "LessonId" },
            { new StringContent(docId.ToString()), "DocumentId" },
            { new StringContent("Multi-Version Doc"), "Title" },
            { new StringContent("Second version"), "Notes" },
            { fileContent, "File", "update_v2.txt" }
        };

        var response = await client.PostAsync("/Document/Upload", formData);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var activeVersion = await _db.GetActiveVersionNumberAsync(docId);
        Assert.Equal(2, activeVersion);
    }

    [Fact]
    public async Task UploadDocument_AsStudent_RedirectsToAccessDenied()
    {
        var studentEmail = await GetEmailAsync(_studentId);
        var client = _factory.CreateClientWithCookies();
        await LoginAsync(client, studentEmail, Password);

        var response = await client.GetAsync($"/Document/Upload?lessonId={_lessonId}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
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
