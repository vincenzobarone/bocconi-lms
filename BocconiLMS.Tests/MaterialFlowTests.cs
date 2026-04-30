using BocconiLMS.Tests.Fixtures;
using BocconiLMS.Tests.Helpers;

namespace BocconiLMS.Tests;

/// <summary>
/// Tests that materials access control respects the CanTeach / CanAttend capability flags,
/// not hardcoded "Teacher"/"Student" role names.
/// </summary>
[Collection("Integration")]
public class MaterialFlowTests : IAsyncLifetime
{
    private readonly LmsWebFactory _factory;
    private readonly DbTestHelper _db;

    private int _instructorId, _attendeeId, _adminId;
    private const string Password = "TestPass2024!";

    public MaterialFlowTests()
    {
        _factory = new LmsWebFactory();
        _db = new DbTestHelper();
    }

    public async Task InitializeAsync()
    {
        await _db.CleanupOrphanTestDataAsync();
        await _db.EnsureSmtpDisabledAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var instructorRole = await _db.CreateRoleAsync($"TestRole_{suffix}_MatInstructor", canTeach: true, canAttend: false);
        var attendeeRole   = await _db.CreateRoleAsync($"TestRole_{suffix}_MatAttendee",   canTeach: false, canAttend: true);

        _instructorId = await _db.CreateUserAsync($"mat_instructor_{suffix}@test.it", "Mat", "Instructor", instructorRole, Password);
        _attendeeId   = await _db.CreateUserAsync($"mat_attendee_{suffix}@test.it",   "Mat", "Attendee",   attendeeRole,   Password);
        _adminId      = await _db.CreateUserAsync($"mat_admin_{suffix}@test.it",       "Mat", "Admin",       "Admin",        Password);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        _factory.Dispose();
    }

    // ── Materials index ────────────────────────────────────────────────────

    [Theory]
    [InlineData("instructor")]
    [InlineData("attendee")]
    [InlineData("admin")]
    public async Task MaterialsIndex_AuthenticatedUser_Returns200(string userKey)
    {
        var userId = UserIdFor(userKey);
        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(userId), Password);

        var response = await client.GetAsync("/Materials/Index");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // ── Materials Create page ──────────────────────────────────────────────

    [Theory]
    [InlineData("instructor")]
    [InlineData("admin")]
    public async Task MaterialsCreate_CanTeachOrAdmin_Returns200(string userKey)
    {
        var userId = UserIdFor(userKey);
        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(userId), Password);

        var response = await client.GetAsync("/Materials/Create");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MaterialsCreate_CanAttendUser_IsForbidden()
    {
        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_attendeeId), Password);

        var response = await client.GetAsync("/Materials/Create");

        Assert.True(
            response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
            response.StatusCode == System.Net.HttpStatusCode.Redirect,
            $"Expected Forbidden or Redirect, got {response.StatusCode}");

        if (response.StatusCode == System.Net.HttpStatusCode.Redirect)
        {
            var location = response.Headers.Location?.ToString() ?? "";
            Assert.Contains("AccessDenied", location, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── Materials unauthenticated ──────────────────────────────────────────

    [Fact]
    public async Task MaterialsIndex_Unauthenticated_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Materials/Index");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login",
            response.Headers.Location?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    // ── Materials details ──────────────────────────────────────────────────

    [Fact]
    public async Task MaterialDetails_CanAttendUser_CanViewPublishedMaterial()
    {
        var materialTitle = $"TestMaterial_{Guid.NewGuid():N}";
        var materialId = await _db.CreateMaterialAsync(_instructorId, materialTitle);

        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlConnector.MySqlCommand(
            "UPDATE materials SET status='pubblicato' WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", materialId);
        await cmd.ExecuteNonQueryAsync();

        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_attendeeId), Password);

        var response = await client.GetAsync($"/Materials/Details/{materialId}");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(materialTitle, html);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private int UserIdFor(string key) => key switch
    {
        "instructor" => _instructorId,
        "attendee"   => _attendeeId,
        _            => _adminId
    };
}
