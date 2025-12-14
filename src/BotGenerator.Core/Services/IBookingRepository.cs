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

    /// <summary>
    /// Finds all future bookings for a given phone number.
    /// </summary>
    /// <param name="phone9Digits">The phone number (9 digits, without country code).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of bookings ordered by date/time ascending.</returns>
    Task<List<BookingRecord>> FindBookingsByPhoneAsync(string phone9Digits, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing booking with new data.
    /// </summary>
    /// <param name="bookingId">The booking ID to update.</param>
    /// <param name="data">The fields to update (only non-null fields are updated).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the update succeeded, false otherwise.</returns>
    Task<bool> UpdateBookingAsync(int bookingId, BookingUpdateData data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single booking by ID.
    /// </summary>
    /// <param name="bookingId">The booking ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The booking record, or null if not found.</returns>
    Task<BookingRecord?> GetBookingByIdAsync(int bookingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a booking from the bookings table.
    /// Should be called AFTER inserting into cancelled_bookings archive.
    /// </summary>
    /// <param name="bookingId">The booking ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the deletion succeeded, false otherwise.</returns>
    Task<bool> CancelBookingAsync(int bookingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a cancelled booking record into the cancelled_bookings archive table.
    /// </summary>
    /// <param name="booking">The booking record to archive.</param>
    /// <param name="cancelledBy">Who cancelled the booking (e.g., "AI_ASSISTANT").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the insert succeeded, false otherwise.</returns>
    Task<bool> InsertCancelledBookingAsync(BookingRecord booking, string cancelledBy, CancellationToken cancellationToken = default);
}
