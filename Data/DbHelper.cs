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
}
