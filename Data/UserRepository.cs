using MySqlConnector;
using BocconiLMS.Models;

namespace BocconiLMS.Data;

public class UserRepository
{
    private readonly DbHelper _db;

    public UserRepository(DbHelper db) => _db = db;

    public async Task<User?> GetByEmailAsync(string email)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT u.id, u.email, u.password_hash, u.first_name, u.last_name,
                   COALESCE(r.name, '') AS role, u.is_active, u.created_at,
                   EXISTS(SELECT 1 FROM role_permissions rp
                          WHERE rp.role_id = u.role_id AND rp.permission_key = 'courses.teach') AS can_teach,
                   EXISTS(SELECT 1 FROM role_permissions rp
                          WHERE rp.role_id = u.role_id AND rp.permission_key = 'courses.attend') AS can_attend
            FROM users u
            LEFT JOIN roles r ON r.id = u.role_id
            WHERE u.email = @email LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@email", email);
        using var reader = await cmd.ExecuteReaderAsync();
        return reader.Read() ? MapUser(reader) : null;
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT u.id, u.email, u.password_hash, u.first_name, u.last_name,
                   COALESCE(r.name, '') AS role, u.is_active, u.created_at,
                   EXISTS(SELECT 1 FROM role_permissions rp
                          WHERE rp.role_id = u.role_id AND rp.permission_key = 'courses.teach') AS can_teach,
                   EXISTS(SELECT 1 FROM role_permissions rp
                          WHERE rp.role_id = u.role_id AND rp.permission_key = 'courses.attend') AS can_attend
            FROM users u
            LEFT JOIN roles r ON r.id = u.role_id
            WHERE u.id = @id LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = await cmd.ExecuteReaderAsync();
        return reader.Read() ? MapUser(reader) : null;
    }

    public async Task<List<User>> GetAllAsync()
    {
        var users = new List<User>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT u.id, u.email, u.password_hash, u.first_name, u.last_name,
                   COALESCE(r.name, '') AS role, u.is_active, u.created_at,
                   EXISTS(SELECT 1 FROM role_permissions rp
                          WHERE rp.role_id = u.role_id AND rp.permission_key = 'courses.teach') AS can_teach,
                   EXISTS(SELECT 1 FROM role_permissions rp
                          WHERE rp.role_id = u.role_id AND rp.permission_key = 'courses.attend') AS can_attend,
                   (SELECT COUNT(*) FROM courses c WHERE c.teacher_id = u.id) AS course_count,
                   CONCAT(cb.first_name, ' ', cb.last_name) AS created_by_name
            FROM users u
            LEFT JOIN roles r ON r.id = u.role_id
            LEFT JOIN users cb ON cb.id = u.created_by
            WHERE COALESCE(r.name, '') != 'Admin'
            ORDER BY u.last_name, u.first_name", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (reader.Read())
        {
            var user = MapUser(reader);
            user.CourseCount = reader.GetInt32("course_count");
            user.CreatedByName = reader.IsDBNull(reader.GetOrdinal("created_by_name"))
                ? null : reader.GetString("created_by_name").Trim();
            users.Add(user);
        }
        return users;
    }

    public async Task<int> CreateAsync(User user)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "INSERT INTO users (email, password_hash, first_name, last_name, role_id, is_active, created_at) VALUES (@email, @hash, @fn, @ln, (SELECT id FROM roles WHERE name=@role), 1, NOW()); SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@email", user.Email);
        cmd.Parameters.AddWithValue("@hash", user.PasswordHash);
        cmd.Parameters.AddWithValue("@fn", user.FirstName);
        cmd.Parameters.AddWithValue("@ln", user.LastName);
        cmd.Parameters.AddWithValue("@role", user.Role);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task UpdateAsync(User user)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "UPDATE users SET first_name=@fn, last_name=@ln, role_id=(SELECT id FROM roles WHERE name=@role), is_active=@active WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@fn", user.FirstName);
        cmd.Parameters.AddWithValue("@ln", user.LastName);
        cmd.Parameters.AddWithValue("@role", user.Role);
        cmd.Parameters.AddWithValue("@active", user.IsActive);
        cmd.Parameters.AddWithValue("@id", user.Id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> CountActiveAdminsAsync()
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM users u JOIN roles r ON r.id = u.role_id WHERE r.name='Admin' AND u.is_active=1", conn);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<int> GetActiveCourseCountAsync(int userId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand("SELECT COUNT(*) FROM courses WHERE teacher_id=@uid", conn);
        cmd.Parameters.AddWithValue("@uid", userId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task DeleteWithCascadeAsync(int userId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();
        try
        {
            foreach (var sql in new[]
            {
                "DELETE FROM quiz_attempts WHERE user_id=@uid",
                "DELETE FROM lesson_progress WHERE user_id=@uid",
                "DELETE FROM enrollments WHERE user_id=@uid",
                "DELETE FROM user_areas WHERE user_id=@uid",
                "DELETE FROM users WHERE id=@uid"
            })
            {
                using var cmd = new MySqlCommand(sql, conn, tx);
                cmd.Parameters.AddWithValue("@uid", userId);
                await cmd.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand("SELECT COUNT(*) FROM users WHERE email=@email", conn);
        cmd.Parameters.AddWithValue("@email", email);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task<DashboardStats> GetStatsAsync()
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT
                (SELECT COUNT(*) FROM courses) AS total_courses,
                (SELECT COUNT(*) FROM users) AS total_users,
                (SELECT COUNT(*) FROM enrollments) AS total_enrollments,
                (SELECT COUNT(*) FROM quiz_attempts) AS total_attempts,
                (SELECT COUNT(*) FROM users u
                 JOIN role_permissions rp ON rp.role_id = u.role_id AND rp.permission_key = 'courses.attend'
                 WHERE u.is_active = 1) AS active_students,
                (SELECT COUNT(*) FROM users u
                 JOIN role_permissions rp ON rp.role_id = u.role_id AND rp.permission_key = 'courses.teach'
                 WHERE u.is_active = 1) AS active_teachers", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        if (!reader.Read()) return new DashboardStats();
        return new DashboardStats
        {
            TotalCourses = reader.GetInt32(0),
            TotalUsers = reader.GetInt32(1),
            TotalEnrollments = reader.GetInt32(2),
            TotalQuizAttempts = reader.GetInt32(3),
            ActiveStudents = reader.GetInt32(4),
            ActiveTeachers = reader.GetInt32(5)
        };
    }

    public async Task<List<RoleViewModel>> GetAllRolesWithCountAsync()
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT r.id, r.name, r.created_at,
                   EXISTS(SELECT 1 FROM role_permissions rp WHERE rp.role_id = r.id AND rp.permission_key = 'courses.teach') AS can_teach,
                   EXISTS(SELECT 1 FROM role_permissions rp WHERE rp.role_id = r.id AND rp.permission_key = 'courses.attend') AS can_attend,
                   (SELECT COUNT(*) FROM users u WHERE u.role_id = r.id) AS user_count,
                   CONCAT(cb.first_name, ' ', cb.last_name) AS created_by_name
            FROM roles r
            LEFT JOIN users cb ON cb.id = r.created_by
            WHERE r.name != 'Admin'
            ORDER BY can_teach DESC, can_attend DESC, r.name", conn);
        var list = new List<RoleViewModel>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(new RoleViewModel
            {
                Id = reader.GetInt32("id"),
                Name = reader.GetString("name"),
                UserCount = reader.GetInt32("user_count"),
                CanTeach = reader.GetBoolean("can_teach"),
                CanAttend = reader.GetBoolean("can_attend"),
                CreatedAt = reader.IsDBNull(reader.GetOrdinal("created_at")) ? null : reader.GetDateTime("created_at"),
                CreatedByName = reader.IsDBNull(reader.GetOrdinal("created_by_name"))
                    ? null : reader.GetString("created_by_name").Trim()
            });
        return list;
    }

    public async Task<int> CountUsersInRoleAsync(int roleId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM users WHERE role_id = @id", conn);
        cmd.Parameters.AddWithValue("@id", roleId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<List<string>> GetNonAdminRoleNamesAsync()
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT name FROM roles WHERE normalized_name != 'ADMIN' ORDER BY name", conn);
        var list = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(reader.GetString("name"));
        return list;
    }

    public async Task<List<(string Email, string FullName)>> GetDistinctRecipientsByRoleNamesAsync(IEnumerable<string> roleNames)
    {
        var names = roleNames.Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
        if (names.Count == 0) return new();

        var paramNames = names.Select((_, i) => $"@r{i}").ToList();
        var inList = string.Join(",", paramNames);

        var sql = $@"
            SELECT DISTINCT u.email, u.first_name, u.last_name
            FROM users u
            JOIN roles r ON r.id = u.role_id
            WHERE u.is_active = 1
              AND r.name IN ({inList})
            ORDER BY u.last_name, u.first_name";

        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlConnector.MySqlCommand(sql, conn);
        for (int i = 0; i < names.Count; i++)
            cmd.Parameters.AddWithValue($"@r{i}", names[i]);

        var result = new List<(string Email, string FullName)>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var email    = reader.GetString("email");
            var fullName = $"{reader.GetString("first_name")} {reader.GetString("last_name")}".Trim();
            result.Add((email, fullName));
        }
        return result;
    }

    public async Task<List<User>> GetTeachersAndAdminsAsync()
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(@"
            SELECT u.id, u.email, u.password_hash, u.first_name, u.last_name,
                   COALESCE(r.name, '') AS role, u.is_active, u.created_at,
                   EXISTS(SELECT 1 FROM role_permissions rp
                          WHERE rp.role_id = u.role_id AND rp.permission_key = 'courses.teach') AS can_teach,
                   EXISTS(SELECT 1 FROM role_permissions rp
                          WHERE rp.role_id = u.role_id AND rp.permission_key = 'courses.attend') AS can_attend
            FROM users u
            LEFT JOIN roles r ON r.id = u.role_id
            WHERE u.is_active = 1
              AND (EXISTS(SELECT 1 FROM role_permissions rp
                          WHERE rp.role_id = u.role_id AND rp.permission_key = 'courses.teach')
                   OR r.name = 'Admin')
            ORDER BY u.last_name, u.first_name",
            conn);
        var list = new List<User>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(MapUser(reader));
        return list;
    }

    public async Task SetUserCreatedByAsync(int userId, int createdById)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "UPDATE users SET created_by = @createdBy WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@createdBy", createdById);
        cmd.Parameters.AddWithValue("@id", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SetShibbolethIdAsync(int userId, string shibbolethId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "UPDATE users SET shibboleth_id = @sid WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@sid", shibbolethId);
        cmd.Parameters.AddWithValue("@id", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SetRoleCreatedByAsync(int roleId, int createdById)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "UPDATE roles SET created_by = @createdBy WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@createdBy", createdById);
        cmd.Parameters.AddWithValue("@id", roleId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static User MapUser(MySqlDataReader r) => new()
    {
        Id = r.GetInt32("id"),
        Email = r.GetString("email"),
        PasswordHash = r.GetString("password_hash"),
        FirstName = r.GetString("first_name"),
        LastName = r.GetString("last_name"),
        Role = r.GetString("role"),
        IsActive = r.GetBoolean("is_active"),
        CreatedAt = r.GetDateTime("created_at"),
        CanTeach = r.GetBoolean("can_teach"),
        CanAttend = r.GetBoolean("can_attend")
    };
}
