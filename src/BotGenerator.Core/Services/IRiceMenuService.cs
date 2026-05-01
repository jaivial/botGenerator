using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Service to get rice menu from FINDE table.
/// </summary>
public interface IRiceMenuService
{
    /// <summary>
    /// Get all active rice types from FINDE where TIPO = 'ARROZ' and active = 1.
    /// </summary>
    Task<RiceMenuResult> GetRiceMenuAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a specific rice type is available.
    /// </summary>
    /// <param name="requestedRice">The rice name/description to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating if rice is available</returns>
    Task<RiceAvailabilityResult> CheckRiceAvailabilityAsync(string requestedRice, CancellationToken cancellationToken = default);
}
