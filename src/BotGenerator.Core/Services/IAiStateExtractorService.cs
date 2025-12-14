using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Uses AI (Gemini) to extract booking state from conversation history.
/// More robust than regex - understands natural language variations.
/// </summary>
public interface IAiStateExtractorService
{
    /// <summary>
    /// Extracts booking state from conversation history using AI.
    /// </summary>
    Task<ConversationState> ExtractStateAsync(
        List<ChatMessage> history,
        CancellationToken cancellationToken = default);
}
