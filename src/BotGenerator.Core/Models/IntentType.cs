namespace BotGenerator.Core.Models;

/// <summary>
/// Represents the detected intent from an AI response.
/// Used to route the conversation to appropriate handlers.
/// </summary>
public enum IntentType
{
    /// <summary>
    /// Normal conversational response, no special action needed.
    /// </summary>
    Normal,

    /// <summary>
    /// User wants to make a reservation.
    /// AI has generated a BOOKING_REQUEST command.
    /// </summary>
    Booking,

    /// <summary>
    /// User wants to cancel an existing reservation.
    /// AI has generated a CANCELLATION_REQUEST command.
    /// </summary>
    Cancellation,

    /// <summary>
    /// User wants to modify an existing reservation.
    /// AI has generated a MODIFICATION_INTENT command.
    /// </summary>
    Modification,

    /// <summary>
    /// User is trying to book for the same day.
    /// Restaurant policy may not allow same-day bookings.
    /// </summary>
    SameDay,

    /// <summary>
    /// Response contains URLs or interactive elements.
    /// May need special handling for WhatsApp formatting.
    /// </summary>
    Interactive,

    /// <summary>
    /// An error occurred during processing.
    /// Should return a generic error message to user.
    /// </summary>
    Error
}
