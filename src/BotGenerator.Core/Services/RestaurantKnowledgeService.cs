using Microsoft.Extensions.Logging;

namespace BotGenerator.Core.Services;

/// <summary>
/// Service for accessing restaurant knowledge from ChromaDB.
/// </summary>
public class RestaurantKnowledgeService
{
    private readonly IConversationVectorStore _vectorStore;
    private readonly IMenuRepository _menuRepository;
    private readonly ILogger<RestaurantKnowledgeService> _logger;
    private List<string>? _cachedRiceTypes;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(1);

    public RestaurantKnowledgeService(
        IConversationVectorStore vectorStore,
        IMenuRepository menuRepository,
        ILogger<RestaurantKnowledgeService> logger)
    {
        _vectorStore = vectorStore;
        _menuRepository = menuRepository;
        _logger = logger;
    }

    /// <summary>
    /// Queries the knowledge base for relevant documents.
    /// </summary>
    public async Task<List<KnowledgeDocument>> QueryAsync(
        string query,
        string? documentType = null,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _vectorStore.QueryKnowledgeAsync(query, documentType, topK, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query knowledge base for: {Query}", query);
            return new List<KnowledgeDocument>();
        }
    }

    /// <summary>
    /// Gets all available rice types (cached for 1 hour).
    /// </summary>
    public async Task<List<string>> GetRiceTypesAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedRiceTypes != null && DateTime.UtcNow < _cacheExpiry)
        {
            return _cachedRiceTypes;
        }

        try
        {
            _cachedRiceTypes = await _menuRepository.GetActiveRiceTypesAsync(cancellationToken);
            _cacheExpiry = DateTime.UtcNow.Add(_cacheDuration);
            return _cachedRiceTypes;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get rice types from database");
            return _cachedRiceTypes ?? new List<string>();
        }
    }

    /// <summary>
    /// Gets policies relevant to a query (e.g., "menú infantil", "terraza").
    /// </summary>
    public async Task<List<string>> GetRelevantPoliciesAsync(
        string? userQuery = null,
        CancellationToken cancellationToken = default)
    {
        var policies = new List<string>();

        try
        {
            // If user asks about specific topics, query relevant policies
            if (!string.IsNullOrWhiteSpace(userQuery))
            {
                var results = await _vectorStore.QueryKnowledgeAsync(userQuery, "policy", 3, cancellationToken);
                policies.AddRange(results.Select(p => p.Content));
            }
            else
            {
                // Return all key policies
                var allPolicies = await _vectorStore.QueryKnowledgeAsync("", "policy", 10, cancellationToken);
                policies.AddRange(allPolicies.Select(p => p.Content));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get policies from knowledge base");
        }

        // Fallback to hardcoded defaults if ChromaDB fails
        if (policies.Count == 0)
        {
            policies.AddRange(GetDefaultPolicies());
        }

        return policies;
    }

    /// <summary>
    /// Seeds the knowledge base with initial data.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _vectorStore.SeedKnowledgeBaseAsync(cancellationToken);
            _logger.LogInformation("Knowledge base seeded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to seed knowledge base");
        }
    }

    /// <summary>
    /// Clears the cache to force refresh on next request.
    /// </summary>
    public void ClearCache()
    {
        _cachedRiceTypes = null;
        _cacheExpiry = DateTime.MinValue;
    }

    private static List<string> GetDefaultPolicies()
    {
        return new List<string>
        {
            "No tenemos menú infantil. Todos los comensales deben consumir un menú regular.",
            "No tenemos terraza. Solo Disponemos de espacio interior.",
            "El restaurante abre a las 13:30 y cierra entre 17:00-18:00."
        };
    }
}
