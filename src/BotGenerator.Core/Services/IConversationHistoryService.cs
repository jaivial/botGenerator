using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Service for managing conversation history.
/// </summary>
public interface IConversationHistoryService
{
    /// <summary>
    /// Gets conversation history for a phone number.
    /// </summary>
    Task<List<ChatMessage>> GetHistoryAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a message to the conversation history.
    /// </summary>
    Task AddMessageAsync(
        string phoneNumber,
        ChatMessage message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears conversation history for a phone number.
    /// </summary>
    Task ClearHistoryAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts booking state from conversation history.
    /// </summary>
    ConversationState ExtractState(List<ChatMessage>? history);
}
