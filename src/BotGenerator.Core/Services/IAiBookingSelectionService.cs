using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// AI-powered service for determining which booking a user is referring to.
/// Replaces regex-based TryParseBookingSelection in both handlers.
/// </summary>
public interface IAiBookingSelectionService
{
    /// <summary>
    /// Determines which booking the user is referring to from their natural language message.
    /// Handles typos ("14;30"), partial descriptions, ordinal references, etc.
    /// </summary>
    /// <param name="userMessage">The user's raw message text.</param>
    /// <param name="bookings">List of the user's active bookings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching booking, or null if unclear.</returns>
    Task<BookingRecord?> SelectBookingAsync(
        string userMessage,
        List<BookingRecord> bookings,
        CancellationToken cancellationToken = default);
}
