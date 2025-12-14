namespace BotGenerator.Core.Models;

/// <summary>
/// Represents the current state of a booking cancellation conversation.
/// </summary>
public record CancellationState
{
    /// <summary>
    /// The phone number of the customer (with country code).
    /// </summary>
    public string PhoneNumber { get; init; } = "";

    /// <summary>
    /// Current stage in the cancellation flow.
    /// </summary>
    public CancellationStage Stage { get; init; }

    /// <summary>
    /// List of bookings found for this phone number.
    /// </summary>
    public List<BookingRecord>? FoundBookings { get; init; }

    /// <summary>
    /// The booking selected for cancellation (null if not yet selected).
    /// </summary>
    public BookingRecord? SelectedBooking { get; init; }

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
/// Stages in the cancellation flow.
/// </summary>
public enum CancellationStage
{
    /// <summary>
    /// Multiple bookings found, user must select which one to cancel.
    /// </summary>
    SelectingBooking,

    /// <summary>
    /// Booking selected, waiting for user confirmation.
    /// </summary>
    AwaitingConfirmation,

    /// <summary>
    /// Cancellation complete, state should be cleared.
    /// </summary>
    Completed
}
