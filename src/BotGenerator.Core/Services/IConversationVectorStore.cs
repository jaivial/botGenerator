using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Vector memory for long-term conversation recall.
/// </summary>
public interface IConversationVectorStore
{
    /// <summary>
    /// Upserts a single message for a phone conversation.
    /// </summary>
    Task UpsertMessageAsync(
        string phoneNumber,
        ChatMessage message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a batch of messages for a phone conversation.
    /// </summary>
    Task UpsertMessagesAsync(
        string phoneNumber,
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries semantically-relevant messages for a phone conversation.
    /// </summary>
    Task<List<ChatMessage>> QueryRelevantAsync(
        string phoneNumber,
        string query,
        int topK,
        CancellationToken cancellationToken = default);
}
