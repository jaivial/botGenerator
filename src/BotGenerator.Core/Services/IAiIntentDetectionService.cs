namespace BotGenerator.Core.Services;

/// <summary>
/// AI-powered intent detection for user messages within multi-turn flows.
/// Replaces regex-based IsExitIntent, confirmation detection, and rice cancel patterns.
/// </summary>
public interface IAiIntentDetectionService
{
    /// <summary>
    /// Detects the user's intent from their message within a specific flow context.
    /// </summary>
    /// <param name="userMessage">The user's raw message text.</param>
    /// <param name="context">The flow context (e.g., "modification_exit", "cancellation_confirm", "rice_change").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detected intent string.</returns>
    Task<string> DetectIntentAsync(
        string userMessage,
        string context,
        CancellationToken cancellationToken = default);
}
