using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ProductsCRUD_API;

public class Db(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

    public SqlConnection CreateConnection() => new(_connectionString);

    public async Task<IEnumerable<T>> QueryAsync<T>(string storedProcedure, object? parameters = null)
    {
        using var connection = CreateConnection();
        return await connection.QueryAsync<T>(storedProcedure, parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(string storedProcedure, object? parameters = null)
    {
        using var connection = CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<T>(storedProcedure, parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task<T> QuerySingleAsync<T>(string storedProcedure, object? parameters = null)
    {
        using var connection = CreateConnection();
        return await connection.QuerySingleAsync<T>(storedProcedure, parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task<SqlMapper.GridReader> QueryMultipleAsync(string storedProcedure, object? parameters = null)
    {
        var connection = CreateConnection();
        return await connection.QueryMultipleAsync(storedProcedure, parameters, commandType: CommandType.StoredProcedure);
    }
}
