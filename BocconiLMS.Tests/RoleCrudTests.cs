using BocconiLMS.Tests.Fixtures;
using BocconiLMS.Tests.Helpers;

namespace BocconiLMS.Tests;

/// <summary>
/// Tests the role management endpoints (Admin only).
/// Roles use dynamic can_teach / can_attend flags — no hardcoded "Teacher"/"Student".
/// </summary>
[Collection("Integration")]
public class RoleCrudTests : IAsyncLifetime
{
    private readonly LmsWebFactory _factory;
    private readonly DbTestHelper _db;
    private int _adminId;
    private const string Password = "TestPass2024!";

    public RoleCrudTests()
    {
        _factory = new LmsWebFactory();
        _db = new DbTestHelper();
    }

    public async Task InitializeAsync()
    {
        await _db.CleanupOrphanTestDataAsync();
        await _db.EnsureSmtpDisabledAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        _adminId = await _db.CreateUserAsync($"role_admin_{suffix}@test.it", "Role", "Admin", "Admin", Password);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        _factory.Dispose();
    }

    // ── Create role ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateRole_AsAdmin_PersistsInDatabase()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var roleName = $"TestRole_{suffix}_Tutor";
        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_adminId), Password);

        var getPage = await client.GetAsync("/Admin/CreateRole");
        getPage.EnsureSuccessStatusCode();
        var html = await getPage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = roleName,
            ["__RequestVerificationToken"] = token
        });
        var response = await client.PostAsync("/Admin/CreateRole", form);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(await _db.RoleExistsAsync(roleName), $"Role '{roleName}' should exist in DB.");

        await DeleteRoleByNameAsync(roleName);
    }

    [Fact]
    public async Task CreateRole_WithCanTeachFlag_StoredCorrectly()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var roleName = $"TestRole_{suffix}_Docente";

        await _db.SetAppSettingAsync("Features:CoursesModule", "true");

        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_adminId), Password);

        var getPage = await client.GetAsync("/Admin/CreateRole");
        var html = await getPage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

        var form = new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
        {
            new("Name", roleName),
            new("permissions", "courses.teach"),
            new("__RequestVerificationToken", token)
        });
        var response = await client.PostAsync("/Admin/CreateRole", form);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(await _db.RoleExistsAsync(roleName));
        var (canTeach, canAttend) = await _db.GetRoleFlagsAsync(roleName);
        Assert.True(canTeach, "courses.teach permission should be stored.");
        Assert.False(canAttend, "courses.attend permission should not be stored.");

        await DeleteRoleByNameAsync(roleName);
    }

    // ── Roles list ────────────────────────────────────────────────────────

    [Fact]
    public async Task UsersPage_RolesTab_ReturnsOk_AsAdmin()
    {
        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_adminId), Password);

        var response = await client.GetAsync("/Admin/Users?tab=ruoli");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // ── Edit role ─────────────────────────────────────────────────────────

    [Fact]
    public async Task EditRole_AsAdmin_UpdatesName()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var oldName = $"TestRole_{suffix}_Before";
        var newName = $"TestRole_{suffix}_After";

        await _db.CreateRoleAsync(oldName, canTeach: false, canAttend: false);
        var roleId = await _db.GetRoleIdAsync(oldName);

        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_adminId), Password);

        var getPage = await client.GetAsync($"/Admin/EditRole/{roleId}");
        Assert.Equal(System.Net.HttpStatusCode.OK, getPage.StatusCode);
        var html = await getPage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"]   = roleId.ToString(),
            ["Name"] = newName,
            ["__RequestVerificationToken"] = token
        });
        var response = await client.PostAsync("/Admin/EditRole", form);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(await _db.RoleExistsAsync(newName), $"Role should be renamed to '{newName}'.");
        Assert.False(await _db.RoleExistsAsync(oldName), $"Old name '{oldName}' should no longer exist.");
    }

    // ── Delete role ───────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteRole_WithNoUsers_RemovesRole()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var roleName = $"TestRole_{suffix}_ToDelete";
        await _db.CreateRoleAsync(roleName, canTeach: false, canAttend: false);
        var roleId = await _db.GetRoleIdAsync(roleName);

        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_adminId), Password);

        var getPage = await client.GetAsync("/Admin/Users?tab=ruoli");
        var html = await getPage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });
        var response = await client.PostAsync($"/Admin/DeleteRole/{roleId}", form);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(await _db.RoleExistsAsync(roleName), $"Role '{roleName}' should have been deleted.");
    }

    [Fact]
    public async Task DeleteRole_Admin_IsProtected()
    {
        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_adminId), Password);

        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlConnector.MySqlCommand(
            "SELECT id FROM roles WHERE name='Admin' LIMIT 1", conn);
        var adminRoleId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        if (adminRoleId == 0) return;

        var getPage = await client.GetAsync("/Admin/Users?tab=ruoli");
        var html = await getPage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });
        var response = await client.PostAsync($"/Admin/DeleteRole/{adminRoleId}", form);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(await _db.RoleExistsAsync("Admin"), "Admin role must never be deleted.");
    }

    // ── Access control ─────────────────────────────────────────────────────

    [Fact]
    public async Task RoleManagement_Unauthenticated_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Admin/CreateRole");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login",
            response.Headers.Location?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    // ── Helper ────────────────────────────────────────────────────────────

    private async Task DeleteRoleByNameAsync(string name)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlConnector.MySqlCommand(
            "DELETE FROM roles WHERE name=@name", conn);
        cmd.Parameters.AddWithValue("@name", name);
        await cmd.ExecuteNonQueryAsync();
    }
}
