namespace BotGenerator.Core.Models;

/// <summary>
/// Intent classification from the AI context analysis (Node 1).
/// </summary>
public enum PipelineIntent
{
    /// <summary>"gracias", "ok", "vale" — no booking context, just acknowledgment.</summary>
    Acknowledgment,
    /// <summary>Questions about menu, parking, hours, etc.</summary>
    OffTopic,
    /// <summary>Asking about existing reservations.</summary>
    InfoRequest,
    /// <summary>Wants to make a new reservation.</summary>
    NewBooking,
    /// <summary>Providing more data for a pending booking.</summary>
    ContinueBooking,
    /// <summary>Confirming a pending booking summary.</summary>
    ConfirmBooking,
    /// <summary>Declining after summary shown.</summary>
    DeclineBooking,
    /// <summary>Wants to change an existing booking.</summary>
    Modification,
    /// <summary>Wants to cancel an existing booking.</summary>
    Cancellation,
    /// <summary>Wants to book for today (reject + phone).</summary>
    SameDayBooking,
    /// <summary>Weddings, parties, large events.</summary>
    EventInquiry,
    /// <summary>Brief reply to promotional/broadcast message.</summary>
    BroadcastReply,
    /// <summary>Greeting with no specific intent.</summary>
    Greeting
}

/// <summary>
/// Input context passed through the pipeline.
/// </summary>
public class PipelineContext
{
    public WhatsAppMessage Message { get; init; } = null!;
    public List<ChatMessage> History { get; init; } = new();
    public List<BookingRecord> ExistingBookings { get; init; } = new();
    public BookingData? PendingBooking { get; init; }
    public string RestaurantId { get; init; } = "villacarmen";
    public string PushName { get; init; } = "Cliente";
    public string FormattedHistory { get; init; } = "";
    public string TodayES { get; init; } = "";
    public string TodayFormatted { get; init; } = "";
}

/// <summary>
/// Structured output from Node 1 (ContextAnalyzer).
/// </summary>
public class ContextAnalysisResult
{
    public PipelineIntent Intent { get; init; }
    public float Confidence { get; init; }
    public string Reasoning { get; init; } = "";

    // Extracted booking data
    public string? ExtractedDate { get; init; }
    public string? ExtractedTime { get; init; }
    public int? ExtractedPeople { get; init; }
    public string? ExtractedRiceType { get; init; }
    public int? ExtractedRiceServings { get; init; }
    public int? ExtractedHighChairs { get; init; }
    public int? ExtractedBabyStrollers { get; init; }
    public bool RiceDeclined { get; init; }

    // Modification/cancellation context
    public string? UserGoal { get; init; }

    // Off-topic handling
    public string? OffTopicSubject { get; init; }
}

/// <summary>
/// Deterministic validation results from Node 2 (ValidationEnrichment).
/// </summary>
public class ValidationResult
{
    public bool IsAvailable { get; init; } = true;
    public string? RejectionReason { get; init; }
    public string? SuggestionMessage { get; init; }
    public List<string>? AlternativeHours { get; init; }
    public RiceValidationResult? RiceValidation { get; init; }
    public bool HasDuplicateBooking { get; init; }
    public bool IsDayClosed { get; init; }
    public bool IsDayFull { get; init; }
    public bool IsSameDay { get; init; }
    public bool IsBeyond35Days { get; init; }
    public DateTime? ParsedDate { get; init; }
    public string? OpeningTime { get; init; }
    public string? ClosingTime { get; init; }
    public List<string>? AvailableSlots { get; init; }
    public string? DayName { get; init; }
}

/// <summary>
/// Final output of the pipeline — what to send back to the user.
/// </summary>
public class PipelineResult
{
    public PipelineIntent Intent { get; init; }
    public string ResponseText { get; init; } = "";
    public BookingData? BookingToCreate { get; init; }
    public BookingData? PendingBookingUpdate { get; init; }
    public bool ShouldClearPending { get; init; }
    public bool ShouldNotifyManagement { get; init; }
    public bool ShouldUpdatePending { get; init; }
    public long? CreatedBookingId { get; init; }
    public ModificationState? ModificationState { get; init; }
    public CancellationState? CancellationState { get; init; }
}
