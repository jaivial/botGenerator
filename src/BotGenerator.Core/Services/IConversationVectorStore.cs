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

    /// <summary>
    /// Queries the restaurant knowledge base for relevant information.
    /// </summary>
    Task<List<KnowledgeDocument>> QueryKnowledgeAsync(
        string query,
        string? documentType = null,
        int topK = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a document to the restaurant knowledge base.
    /// </summary>
    Task UpsertKnowledgeAsync(
        KnowledgeDocument document,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeds the knowledge base with initial data if empty.
    /// </summary>
    Task SeedKnowledgeBaseAsync(CancellationToken cancellationToken = default);
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

/// <summary>
/// Represents a document in the restaurant knowledge base.
/// </summary>
public class KnowledgeDocument
{
    public string DocumentType { get; set; } = "policy"; // "policy", "rice", "response", "flow_step"
    public string Key { get; set; } = "";
    public string Content { get; set; } = "";
    public List<string> Keywords { get; set; } = new();
    
    // Rice-specific fields
    public decimal? PriceModifier { get; set; }
    public bool? Available { get; set; }
    public int? MinServings { get; set; }
    
    // Query result fields
    public double? Distance { get; set; }
}
