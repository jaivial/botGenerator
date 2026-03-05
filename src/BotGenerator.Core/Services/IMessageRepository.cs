using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Repository for persisting conversation messages to database.
/// </summary>
public interface IMessageRepository
{
    /// <summary>
    /// Gets conversation history for a phone number.
    /// </summary>
    Task<List<ChatMessage>> GetMessagesAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a message to the database.
    /// </summary>
    Task SaveMessageAsync(
        string phoneNumber,
        ChatMessage message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves multiple messages to the database, skipping duplicates by message_id when present.
    /// Returns number of inserted rows.
    /// </summary>
    Task<int> SaveMessagesDeduplicatedAsync(
        string phoneNumber,
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent external message IDs for deduplication.
    /// </summary>
    Task<List<string>> GetRecentMessageIdsAsync(
        string phoneNumber,
        int limit = 300,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a phone number has any messages in the database.
    /// </summary>
    Task<bool> HasMessagesAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all messages for a phone number.
    /// </summary>
    Task ClearMessagesAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default);
}
