namespace BotGenerator.Core.Services;

/// <summary>
/// Repository for accessing menu items from the database.
/// </summary>
public interface IMenuRepository
{
    /// <summary>
    /// Gets all active rice types from the database.
    /// </summary>
    Task<List<string>> GetActiveRiceTypesAsync(CancellationToken cancellationToken = default);
}
