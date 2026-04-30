using BocconiLMS.Tests.Fixtures;
using BocconiLMS.Tests.Helpers;

namespace BocconiLMS.Tests;

[Collection("Integration")]
public class LoginFlowTests : IAsyncLifetime
{
    private readonly LmsWebFactory _factory;
    private readonly DbTestHelper _db;

    private int _instructorId, _attendeeId, _adminId;
    private string _instructorRole = string.Empty;
    private string _attendeeRole  = string.Empty;
    private const string Password = "TestPass2024!";

    public LoginFlowTests()
    {
        _factory = new LmsWebFactory();
        _db = new DbTestHelper();
    }

    public async Task InitializeAsync()
    {
        await _db.CleanupOrphanTestDataAsync();
        await _db.EnsureSmtpDisabledAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];

        _instructorRole = await _db.CreateRoleAsync($"TestRole_{suffix}_Instructor", canTeach: true, canAttend: false);
        _attendeeRole   = await _db.CreateRoleAsync($"TestRole_{suffix}_Attendee",   canTeach: false, canAttend: true);

        _instructorId = await _db.CreateUserAsync(
            $"instructor_{suffix}@test.it", "Test", "Instructor", _instructorRole, Password);
        _attendeeId = await _db.CreateUserAsync(
            $"attendee_{suffix}@test.it",   "Test", "Attendee",   _attendeeRole, Password);
        _adminId = await _db.CreateUserAsync(
            $"admin_{suffix}@test.it",       "Test", "Admin",       "Admin", Password);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        _factory.Dispose();
    }

    // ── Login valid ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("attendee")]
    [InlineData("instructor")]
    [InlineData("admin")]
    public async Task Login_WithValidCredentials_RedirectsToDashboard(string userKey)
    {
        var userId = userKey switch
        {
            "attendee"   => _attendeeId,
            "instructor" => _instructorId,
            _            => _adminId
        };
        var email = await _db.GetEmailAsync(userId);
        var client = _factory.CreateClientWithCookies();

        var loginPage = await client.GetAsync("/Account/Login");
        loginPage.EnsureSuccessStatusCode();
        var html = await loginPage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = Password,
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Account/Login", form);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? "";
        Assert.Contains("/Home/Dashboard", location, StringComparison.OrdinalIgnoreCase);
    }

    // ── Login invalid ──────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithWrongPassword_ShowsError()
    {
        var email = await _db.GetEmailAsync(_attendeeId);
        var client = _factory.CreateClientWithCookies();

        var loginPage = await client.GetAsync("/Account/Login");
        var html = await loginPage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

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
            ["Email"] = "doesnotexist_xyz@bocconi.it",
            ["Password"] = Password,
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Account/Login", form);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var responseHtml = await response.Content.ReadAsStringAsync();
        Assert.Contains("Credenziali non valide", responseHtml);
    }

    // ── Logout ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("attendee")]
    [InlineData("instructor")]
    [InlineData("admin")]
    public async Task Logout_WhenLoggedIn_RedirectsToHome(string userKey)
    {
        var userId = userKey switch
        {
            "attendee"   => _attendeeId,
            "instructor" => _instructorId,
            _            => _adminId
        };
        var email = await _db.GetEmailAsync(userId);
        var client = _factory.CreateClientWithCookies();
        await LoginAsync(client, email, Password);

        var dashPage = await client.GetAsync("/Home/Dashboard");
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

    // ── Already authenticated ─────────────────────────────────────────────

    [Theory]
    [InlineData("attendee")]
    [InlineData("instructor")]
    [InlineData("admin")]
    public async Task Login_AlreadyAuthenticated_RedirectsToDashboard(string userKey)
    {
        var userId = userKey switch
        {
            "attendee"   => _attendeeId,
            "instructor" => _instructorId,
            _            => _adminId
        };
        var email = await _db.GetEmailAsync(userId);
        var client = _factory.CreateClientWithCookies();
        await LoginAsync(client, email, Password);

        var response = await client.GetAsync("/Account/Login");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
    }

    // ── Unauthenticated redirect ──────────────────────────────────────────

    [Fact]
    public async Task ProtectedPage_Unauthenticated_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Home/Dashboard");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? "";
        Assert.Contains("/Account/Login", location, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    internal static async Task LoginAsync(HttpClient client, string email, string password)
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
