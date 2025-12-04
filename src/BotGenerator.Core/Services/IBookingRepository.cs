using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Repository interface for booking operations.
/// </summary>
public interface IBookingRepository
{
    /// <summary>
    /// Creates a new booking in the database.
    /// </summary>
    /// <param name="booking">The booking data to insert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ID of the created booking, or null if failed.</returns>
    Task<long?> CreateBookingAsync(BookingData booking, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a booking exists for the given date, time, and phone.
    /// </summary>
    Task<bool> BookingExistsAsync(string date, string time, string phone, CancellationToken cancellationToken = default);
}
