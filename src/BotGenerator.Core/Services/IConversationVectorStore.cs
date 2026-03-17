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
    /// Upserts a booking record for semantic search.
    /// </summary>
    Task UpsertBookingAsync(
        string phoneNumber,
        BookingRecord booking,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries semantically-relevant messages for a phone conversation.
    /// </summary>
    Task<List<ChatMessage>> QueryRelevantAsync(
        string phoneNumber,
        string query,
        int topK,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries both messages and bookings for a phone conversation.
    /// </summary>
    Task<List<ConversationDocument>> QueryPhoneContextAsync(
        string phoneNumber,
        string query,
        int topK = 10,
        string? filterType = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a document stored in the vector store (message or booking).
/// </summary>
public class ConversationDocument
{
    public string DocumentType { get; set; } = "message"; // "message" or "booking"
    public string Content { get; set; } = "";
    public string Role { get; set; } = "user";
    public DateTime? Timestamp { get; set; }
    public string? MessageId { get; set; }
    public string? FromName { get; set; }
    
    // Booking-specific fields
    public int? BookingId { get; set; }
    public DateTime? ReservationDate { get; set; }
    public string? ReservationTime { get; set; }
    public int? PartySize { get; set; }
    public string? CustomerName { get; set; }

    // Query result fields
    public double? Distance { get; set; }
    public Dictionary<string, object?>? Metadata { get; set; }
}
