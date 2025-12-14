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
