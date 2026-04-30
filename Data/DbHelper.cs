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

    public MySqlConnection GetConnectionWithUserVariables()
    {
        var csb = new MySqlConnectionStringBuilder(_connectionString);
        csb.AllowUserVariables = true;
        return new MySqlConnection(csb.ConnectionString);
    }

    public static async Task<int> GetLastInsertIdAsync(MySqlConnection conn, MySqlTransaction? tx = null)
    {
        using var cmd = new MySqlCommand("SELECT LAST_INSERT_ID();", conn);
        if (tx != null) cmd.Transaction = tx;
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
}
