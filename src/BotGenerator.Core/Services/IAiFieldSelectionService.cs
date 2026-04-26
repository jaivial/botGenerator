namespace BotGenerator.Core.Services;

/// <summary>
/// AI-powered service for determining which field a user wants to modify in their booking.
/// Replaces regex-based field matching in HandleFieldSelectionAsync.
/// </summary>
public interface IAiFieldSelectionService
{
    /// <summary>
    /// Determines which booking field the user wants to modify from their natural language message.
    /// </summary>
    /// <param name="userMessage">The user's raw message text.</param>
    /// <param name="bookingSummary">Summary of the current booking for context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Field name ("date", "time", "party_size", "rice", "tronas", "carritos") or null.</returns>
    Task<string?> DetectFieldAsync(
        string userMessage,
        string bookingSummary,
        CancellationToken cancellationToken = default);
}
