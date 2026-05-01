using BotGenerator.Core.Models;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace BotGenerator.Core.Services;

/// <summary>
/// Implementation of rice menu from FINDE table.
/// </summary>
public class RiceMenuService : IRiceMenuService
{
    private readonly string _connectionString;
    private readonly ILogger<RiceMenuService> _logger;

    public RiceMenuService(IConfiguration configuration, ILogger<RiceMenuService> logger)
    {
        _connectionString = configuration["MySQL:ConnectionString"]
            ?? throw new InvalidOperationException("MySQL:ConnectionString not configured");
        _logger = logger;
    }

    public async Task<RiceMenuResult> GetRiceMenuAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                SELECT NUM as Id, DESCRIPCION as Descripcion, TIPO as Tipo, active as Active
                FROM FINDE 
                WHERE TIPO = 'ARROZ' AND active = 1
                ORDER BY NUM";

            var arroces = await connection.QueryAsync<RiceMenuItem>(sql);

            return new RiceMenuResult
            {
                Success = true,
                Arroces = arroces.ToList(),
                TotalCount = arroces.Count()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching rice menu from FINDE");
            return new RiceMenuResult
            {
                Success = false,
                Arroces = new List<RiceMenuItem>(),
                TotalCount = 0
            };
        }
    }

    public async Task<RiceAvailabilityResult> CheckRiceAvailabilityAsync(string requestedRice, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestedRice))
        {
            return new RiceAvailabilityResult
            {
                Success = false,
                Available = false,
                RequestedRice = requestedRice,
                Message = "No se proporcionó ningún tipo de arroz"
            };
        }

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Get all active arroces for matching
            const string sql = @"
                SELECT NUM as Id, DESCRIPCION as Descripcion, TIPO as Tipo, active as Active
                FROM FINDE 
                WHERE TIPO = 'ARROZ' AND active = 1
                ORDER BY NUM";

            var arroces = await connection.QueryAsync<RiceMenuItem>(sql);
            var arrozList = arroces.ToList();

            // Normalize search term
            var searchNormalized = NormalizeText(requestedRice);

            // Try to find a match - check if requested rice is in the list
            // Match can be partial (e.g., "paella" matches "Paella Valenciana")
            var matched = arrozList.FirstOrDefault(a => 
                NormalizeText(a.Descripcion).Contains(searchNormalized, StringComparison.OrdinalIgnoreCase) ||
                searchNormalized.Contains(NormalizeText(a.Descripcion).Split(' ')[0], StringComparison.OrdinalIgnoreCase));

            if (matched != null)
            {
                return new RiceAvailabilityResult
                {
                    Success = true,
                    Available = true,
                    RequestedRice = requestedRice,
                    MatchedRice = matched.Descripcion,
                    RiceId = matched.Id,
                    Message = $"El arroz '{matched.Descripcion}' está disponible"
                };
            }

            // No exact match - check if similar (for suggestions)
            var suggestions = arrozList
                .Where(a => CalculateSimilarity(NormalizeText(a.Descripcion), searchNormalized) > 0.3)
                .Take(3)
                .Select(a => a.Descripcion)
                .ToList();

            return new RiceAvailabilityResult
            {
                Success = true,
                Available = false,
                RequestedRice = requestedRice,
                MatchedRice = null,
                RiceId = null,
                Message = suggestions.Any()
                    ? $"El arroz '{requestedRice}' no está disponible. Opciones similares: {string.Join(", ", suggestions)}"
                    : $"El arroz '{requestedRice}' no está disponible actualmente"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rice availability for: {Rice}", requestedRice);
            return new RiceAvailabilityResult
            {
                Success = false,
                Available = false,
                RequestedRice = requestedRice,
                Message = "Error al verificar disponibilidad del arroz"
            };
        }
    }

    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        // Remove accents, convert to lowercase, trim
        return text
            .ToLowerInvariant()
            .Trim();
    }

    private static double CalculateSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
            return 0;

        // Simple word-based similarity
        var words1 = s1.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words2 = s2.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var commonWords = words1.Intersect(words2, StringComparer.OrdinalIgnoreCase).Count();
        var totalWords = Math.Max(words1.Length, words2.Length);
        
        return totalWords > 0 ? (double)commonWords / totalWords : 0;
    }
}
