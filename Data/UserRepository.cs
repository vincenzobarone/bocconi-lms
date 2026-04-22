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
        using var cmd = new MySqlCommand(
            "SELECT id, username, email, password_hash, first_name, last_name, role, is_active, created_at FROM users WHERE email = @email LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@email", email);
        using var reader = await cmd.ExecuteReaderAsync();
        return reader.Read() ? MapUser(reader) : null;
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT id, username, email, password_hash, first_name, last_name, role, is_active, created_at FROM users WHERE id = @id LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = await cmd.ExecuteReaderAsync();
        return reader.Read() ? MapUser(reader) : null;
    }

    public async Task<List<User>> GetAllAsync()
    {
        var users = new List<User>();
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "SELECT id, username, email, password_hash, first_name, last_name, role, is_active, created_at FROM users ORDER BY last_name, first_name", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (reader.Read()) users.Add(MapUser(reader));
        return users;
    }

    public async Task<int> CreateAsync(User user)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var cmd = new MySqlCommand(
            "INSERT INTO users (username, email, password_hash, first_name, last_name, role, is_active, created_at) VALUES (@username, @email, @hash, @fn, @ln, @role, 1, NOW()); SELECT LAST_INSERT_ID();", conn);
        cmd.Parameters.AddWithValue("@username", user.Username);
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
            "UPDATE users SET username=@username, first_name=@fn, last_name=@ln, role=@role, is_active=@active WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@username", user.Username);
        cmd.Parameters.AddWithValue("@fn", user.FirstName);
        cmd.Parameters.AddWithValue("@ln", user.LastName);
        cmd.Parameters.AddWithValue("@role", user.Role);
        cmd.Parameters.AddWithValue("@active", user.IsActive);
        cmd.Parameters.AddWithValue("@id", user.Id);
        await cmd.ExecuteNonQueryAsync();
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
                (SELECT COUNT(*) FROM users WHERE role='Student' AND is_active=1) AS active_students,
                (SELECT COUNT(*) FROM users WHERE role='Teacher' AND is_active=1) AS active_teachers", conn);
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

    private static User MapUser(MySqlDataReader r) => new()
    {
        Id = r.GetInt32("id"),
        Username = r.GetString("username"),
        Email = r.GetString("email"),
        PasswordHash = r.GetString("password_hash"),
        FirstName = r.GetString("first_name"),
        LastName = r.GetString("last_name"),
        Role = r.GetString("role"),
        IsActive = r.GetBoolean("is_active"),
        CreatedAt = r.GetDateTime("created_at")
    };
}
