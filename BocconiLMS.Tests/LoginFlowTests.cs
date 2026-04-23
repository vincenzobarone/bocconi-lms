using BocconiLMS.Tests.Fixtures;
using BocconiLMS.Tests.Helpers;

namespace BocconiLMS.Tests;

[Collection("Integration")]
public class LoginFlowTests : IAsyncLifetime
{
    private readonly LmsWebFactory _factory;
    private readonly DbTestHelper _db;
    private int _studentId, _teacherId, _adminId;
    private const string Password = "TestPass2024!";

    public LoginFlowTests()
    {
        _factory = new LmsWebFactory();
        _db = new DbTestHelper();
    }

    public async Task InitializeAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        _studentId = await _db.CreateUserAsync(
            $"student_{suffix}@test.it",
            "Test", "Student", "Student", Password);
        _teacherId = await _db.CreateUserAsync(
            $"teacher_{suffix}@test.it",
            "Test", "Teacher", "Teacher", Password);
        _adminId = await _db.CreateUserAsync(
            $"admin_{suffix}@test.it",
            "Test", "Admin", "Admin", Password);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        _factory.Dispose();
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Teacher")]
    [InlineData("Admin")]
    public async Task Login_WithValidCredentials_RedirectsToRoleDashboard(string role)
    {
        var client = _factory.CreateClientWithCookies();

        var loginPage = await client.GetAsync("/Account/Login");
        loginPage.EnsureSuccessStatusCode();
        var html = await loginPage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

        var email = role switch
        {
            "Student" => await GetEmailAsync(_studentId),
            "Teacher" => await GetEmailAsync(_teacherId),
            _ => await GetEmailAsync(_adminId)
        };

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = Password,
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Account/Login", form);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? "";

        var expectedPath = role switch
        {
            "Admin" => "/Admin",
            "Teacher" => "/Course/Dashboard",
            _ => "/Student/Dashboard"
        };

        Assert.Contains(expectedPath, location, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShowsError()
    {
        var client = _factory.CreateClientWithCookies();

        var loginPage = await client.GetAsync("/Account/Login");
        var html = await loginPage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

        var email = await GetEmailAsync(_studentId);
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = "WrongPassword999!",
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Account/Login", form);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var responseHtml = await response.Content.ReadAsStringAsync();
        Assert.Contains("Credenziali non valide", responseHtml);
    }

    [Fact]
    public async Task Login_WithNonExistentEmail_ShowsError()
    {
        var client = _factory.CreateClientWithCookies();

        var loginPage = await client.GetAsync("/Account/Login");
        var html = await loginPage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "doesnotexist@bocconi.it",
            ["Password"] = Password,
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Account/Login", form);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var responseHtml = await response.Content.ReadAsStringAsync();
        Assert.Contains("Credenziali non valide", responseHtml);
    }

    [Theory]
    [InlineData("Student", "/Student/Dashboard")]
    [InlineData("Teacher", "/Course/Dashboard")]
    [InlineData("Admin", "/Admin")]
    public async Task Logout_WhenLoggedIn_RedirectsToHome(string role, string dashboardPath)
    {
        var client = _factory.CreateClientWithCookies();
        var email = role switch
        {
            "Student" => await GetEmailAsync(_studentId),
            "Teacher" => await GetEmailAsync(_teacherId),
            _ => await GetEmailAsync(_adminId)
        };
        await LoginAsync(client, email, Password);

        var dashPage = await client.GetAsync(dashboardPath);
        var html = await dashPage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });
        var response = await client.PostAsync("/Account/Logout", form);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? "";
        Assert.Contains("/", location);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Teacher")]
    [InlineData("Admin")]
    public async Task Login_AlreadyAuthenticated_RedirectsToDashboard(string role)
    {
        var client = _factory.CreateClientWithCookies();
        var email = role switch
        {
            "Student" => await GetEmailAsync(_studentId),
            "Teacher" => await GetEmailAsync(_teacherId),
            _ => await GetEmailAsync(_adminId)
        };
        await LoginAsync(client, email, Password);

        var response = await client.GetAsync("/Account/Login");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
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
