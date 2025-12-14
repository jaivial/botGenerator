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
    /// Extracts booking state from conversation history (sync, regex-based - legacy).
    /// </summary>
    ConversationState ExtractState(List<ChatMessage>? history);

    /// <summary>
    /// Extracts booking state from conversation history using AI (async, more robust).
    /// Understands natural language variations like "nah", "ninguna", "sin tronas", etc.
    /// </summary>
    Task<ConversationState> ExtractStateWithAiAsync(
        List<ChatMessage> history,
        CancellationToken cancellationToken = default);
}
