using BocconiLMS.Tests.Fixtures;
using BocconiLMS.Tests.Helpers;

namespace BocconiLMS.Tests;

/// <summary>
/// Tests for the Admin panel: user CRUD, area CRUD, document-type CRUD.
/// All endpoints verified as Admin (has full access) or non-Admin (blocked).
/// </summary>
[Collection("Integration")]
public class AdminCrudTests : IAsyncLifetime
{
    private readonly LmsWebFactory _factory;
    private readonly DbTestHelper _db;

    private int _adminId, _instructorId;
    private const string Password = "TestPass2024!";

    public AdminCrudTests()
    {
        _factory = new LmsWebFactory();
        _db = new DbTestHelper();
    }

    public async Task InitializeAsync()
    {
        await _db.CleanupOrphanTestDataAsync();
        await _db.EnsureSmtpDisabledAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var instructorRole = await _db.CreateRoleAsync($"TestRole_{suffix}_AdminInstructor", canTeach: true, canAttend: false);

        _adminId      = await _db.CreateUserAsync($"crud_admin_{suffix}@test.it",      "Crud", "Admin",      "Admin",         Password);
        _instructorId = await _db.CreateUserAsync($"crud_instructor_{suffix}@test.it", "Crud", "Instructor", instructorRole,  Password);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        _factory.Dispose();
    }

    // ── Users list ────────────────────────────────────────────────────────

    [Fact]
    public async Task UsersList_AsAdmin_Returns200()
    {
        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_adminId), Password);

        var response = await client.GetAsync("/Admin/Users");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UsersList_AsNonAdmin_IsForbidden()
    {
        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_instructorId), Password);

        var response = await client.GetAsync("/Admin/Users");

        Assert.True(
            response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
            response.StatusCode == System.Net.HttpStatusCode.Redirect,
            $"Non-admin should be blocked. Got {response.StatusCode}");
    }

    // ── Create user ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateUser_AsAdmin_PersistsUser()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"new_user_{suffix}@test.it";

        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_adminId), Password);

        var getPage = await client.GetAsync("/Admin/CreateUser");
        Assert.Equal(System.Net.HttpStatusCode.OK, getPage.StatusCode);
        var html = await getPage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"]     = email,
            ["FirstName"] = "Nuovo",
            ["LastName"]  = "Utente",
            ["Password"]  = Password,
            ["RoleName"]  = "Admin",
            ["__RequestVerificationToken"] = token
        });
        var response = await client.PostAsync("/Admin/CreateUser", form);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);

        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var check = new MySqlConnector.MySqlCommand(
            "SELECT id FROM users WHERE email=@email LIMIT 1", conn);
        check.Parameters.AddWithValue("@email", email);
        var newId = Convert.ToInt32(await check.ExecuteScalarAsync());
        Assert.True(newId > 0, $"User '{email}' should exist in DB after creation.");

        using var del = new MySqlConnector.MySqlCommand("DELETE FROM users WHERE id=@id", conn);
        del.Parameters.AddWithValue("@id", newId);
        await del.ExecuteNonQueryAsync();
    }

    // ── Area CRUD ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateArea_AsAdmin_PersistsArea()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var areaName = $"TestArea_{suffix}";

        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_adminId), Password);

        var dictPage = await client.GetAsync("/Admin/Dictionary?tab=aree");
        var html = await dictPage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["name"] = areaName,
            ["__RequestVerificationToken"] = token
        });
        var response = await client.PostAsync("/Admin/CreateArea", form);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);

        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var check = new MySqlConnector.MySqlCommand(
            "SELECT id FROM areas WHERE name=@name LIMIT 1", conn);
        check.Parameters.AddWithValue("@name", areaName);
        var areaId = Convert.ToInt32(await check.ExecuteScalarAsync());
        Assert.True(areaId > 0, $"Area '{areaName}' should exist.");

        using var del = new MySqlConnector.MySqlCommand("DELETE FROM areas WHERE id=@id", conn);
        del.Parameters.AddWithValue("@id", areaId);
        await del.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task DeleteArea_WithNoUsers_RemovesArea()
    {
        var areaId = await _db.CreateAreaAsync($"TestArea_{Guid.NewGuid():N}");

        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_adminId), Password);

        var dictPage = await client.GetAsync("/Admin/Dictionary?tab=aree");
        var html = await dictPage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });
        var response = await client.PostAsync($"/Admin/DeleteArea/{areaId}", form);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);

        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var check = new MySqlConnector.MySqlCommand(
            "SELECT COUNT(*) FROM areas WHERE id=@id", conn);
        check.Parameters.AddWithValue("@id", areaId);
        Assert.Equal(0, Convert.ToInt32(await check.ExecuteScalarAsync()));
    }

    // ── Document type CRUD ────────────────────────────────────────────────

    [Fact]
    public async Task CreateDocumentType_AsAdmin_PersistsDocType()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var docTypeName = $"TestDocType_{suffix}";

        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_adminId), Password);

        var dictPage = await client.GetAsync("/Admin/Dictionary?tab=doctypes");
        var html = await dictPage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = docTypeName,
            ["__RequestVerificationToken"] = token
        });
        var response = await client.PostAsync("/Admin/CreateDocumentType", form);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);

        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var check = new MySqlConnector.MySqlCommand(
            "SELECT id FROM document_types WHERE name=@name LIMIT 1", conn);
        check.Parameters.AddWithValue("@name", docTypeName);
        var dtId = Convert.ToInt32(await check.ExecuteScalarAsync());
        Assert.True(dtId > 0, $"Document type '{docTypeName}' should exist.");

        using var del = new MySqlConnector.MySqlCommand("DELETE FROM document_types WHERE id=@id", conn);
        del.Parameters.AddWithValue("@id", dtId);
        await del.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task DeleteDocumentType_WithNoMaterials_RemovesDocType()
    {
        var dtId = await _db.CreateDocumentTypeAsync($"TestDocType_{Guid.NewGuid():N}");

        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_adminId), Password);

        var dictPage = await client.GetAsync("/Admin/Dictionary?tab=doctypes");
        var html = await dictPage.Content.ReadAsStringAsync();
        var token = CsrfHelper.Extract(html);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });
        var response = await client.PostAsync($"/Admin/DeleteDocumentType/{dtId}", form);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);

        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var check = new MySqlConnector.MySqlCommand(
            "SELECT COUNT(*) FROM document_types WHERE id=@id", conn);
        check.Parameters.AddWithValue("@id", dtId);
        Assert.Equal(0, Convert.ToInt32(await check.ExecuteScalarAsync()));
    }

    // ── Dashboard ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AdminDashboard_AsAdmin_Returns200()
    {
        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_adminId), Password);

        var response = await client.GetAsync("/Admin/Dashboard");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminDashboard_AsNonAdmin_IsForbidden()
    {
        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_instructorId), Password);

        var response = await client.GetAsync("/Admin/Dashboard");

        Assert.True(
            response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
            response.StatusCode == System.Net.HttpStatusCode.Redirect,
            $"Non-admin should be blocked from Admin/Dashboard. Got {response.StatusCode}");
    }

    // ── App Settings (Admin only) ─────────────────────────────────────────

    [Fact]
    public async Task AppSettings_AsAdmin_Returns200()
    {
        var client = _factory.CreateClientWithCookies();
        await LoginFlowTests.LoginAsync(client, await _db.GetEmailAsync(_adminId), Password);

        var response = await client.GetAsync("/Admin/Settings");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
