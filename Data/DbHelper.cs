using MySqlConnector;

namespace BocconiLMS.Data;

public class DbHelper
{
    private readonly string _connectionString;

    public DbHelper(string connectionString)
    {
        _connectionString = connectionString;
    }

    public MySqlConnection GetConnection()
    {
        return new MySqlConnection(_connectionString);
    }

    public static async Task<int> GetLastInsertIdAsync(MySqlConnection conn)
    {
        using var cmd = new MySqlCommand("SELECT LAST_INSERT_ID();", conn);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
}
