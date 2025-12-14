using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace BotGenerator.Core.Services;

/// <summary>
/// MySQL implementation of IMenuRepository.
/// </summary>
public class MenuRepository : IMenuRepository
{
    private readonly string _connectionString;
    private readonly ILogger<MenuRepository> _logger;

    public MenuRepository(IConfiguration configuration, ILogger<MenuRepository> logger)
    {
        _connectionString = configuration["MySQL:ConnectionString"]
            ?? throw new InvalidOperationException("MySQL:ConnectionString not configured");
        _logger = logger;
    }

    public async Task<List<string>> GetActiveRiceTypesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                SELECT DESCRIPCION
                FROM FINDE
                WHERE TIPO = 'ARROZ' AND active = 1
                ORDER BY DESCRIPCION";

            var result = await connection.QueryAsync<string>(sql);
            var riceTypes = result.ToList();

            _logger.LogInformation(
                "Retrieved {Count} active rice types from database",
                riceTypes.Count);

            return riceTypes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rice types from database");
            throw;
        }
    }
}
