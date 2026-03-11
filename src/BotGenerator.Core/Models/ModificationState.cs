namespace BotGenerator.Core.Models;

/// <summary>
/// Represents the current state of a booking modification conversation.
/// </summary>
public record ModificationState
{
    /// <summary>
    /// The phone number of the customer (with country code).
    /// </summary>
    public string PhoneNumber { get; init; } = "";

    /// <summary>
    /// Current stage in the modification flow.
    /// </summary>
    public ModificationStage Stage { get; init; }

    /// <summary>
    /// List of bookings found for this phone number.
    /// </summary>
    public List<BookingRecord>? FoundBookings { get; init; }

    /// <summary>
    /// The booking selected for modification (null if not yet selected).
    /// </summary>
    public BookingRecord? SelectedBooking { get; init; }

    /// <summary>
    /// The field being modified: "date", "time", "rice", "party_size", "tronas", "carritos".
    /// </summary>
    public string? FieldToModify { get; init; }

    /// <summary>
    /// Pending changes to be applied after confirmation.
    /// </summary>
    public BookingUpdateData? PendingChanges { get; init; }

    /// <summary>
    /// Description of the change for confirmation display.
    /// </summary>
    public string? ChangeDescription { get; init; }

    /// <summary>
    /// When this state was created (for timeout handling).
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When this state was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;

    // ========== ACCUMULATOR PATTERN FIELDS ==========

    /// <summary>
    /// Accumulated partial field values extracted from user messages.
    /// Key: field name (date, time, party_size, rice, tronas, carritos)
    /// Value: extracted value (can be string, int, or parsed object)
    /// </summary>
    public Dictionary<string, object>? AccumulatedChanges { get; init; }

    /// <summary>
    /// List of field names that have been successfully extracted.
    /// Used to track which fields have been provided by the user.
    /// </summary>
    public List<string>? ExtractedFields { get; init; }

    /// <summary>
    /// The last field the bot asked the user about.
    /// Used for context-aware parsing (e.g., if bot asked for date and user says "14:30", infer time).
    /// </summary>
    public string? LastAskedField { get; init; }

    /// <summary>
    /// The user's goal for this modification (change_date, change_time, change_both, add_rice, etc.).
    /// Helps understand user intent across multiple turns.
    /// </summary>
    public string? UserGoal { get; init; }

    /// <summary>
    /// Number of conversation turns in this modification session.
    /// Used to detect stuck conversations and offer human handoff.
    /// </summary>
    public int ConversationTurn { get; init; } = 0;

    /// <summary>
    /// The last question the bot asked, stored for context.
    /// Example: "¿A qué hora?" → if user responds with time, we understand context.
    /// </summary>
    public string? PreviousBotQuestion { get; init; }
}

/// <summary>
/// Stages in the modification flow.
/// </summary>
public enum ModificationStage
{
    /// <summary>
    /// Multiple bookings found, user must select which one to modify.
    /// </summary>
    SelectingBooking,

    /// <summary>
    /// Booking selected, asking user what field they want to modify.
    /// </summary>
    SelectingField,

    /// <summary>
    /// User is providing the new value for the selected field.
    /// </summary>
    CollectingNewValue,

    /// <summary>
    /// All changes collected, waiting for user confirmation.
    /// </summary>
    AwaitingConfirmation,

    /// <summary>
    /// Modification complete, state should be cleared.
    /// </summary>
    Completed
}
