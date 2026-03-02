using Microsoft.Extensions.Logging;

namespace BotGenerator.Core.Services;

/// <summary>
/// Interface for external reservation operations (PHP endpoint calls).
/// </summary>
public interface IExternalReservationService
{
    /// <summary>
    /// Updates a reservation field in the external PHP system.
    /// </summary>
    Task<bool> UpdateReservationFieldAsync(
        int bookingId,
        string field,
        string value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new reservation in the external PHP system.
    /// </summary>
    Task<(bool success, string? message)> CreateReservationAsync(
        string customerName,
        string phone,
        string date,
        int partySize,
        string time,
        string? arrozType = null,
        int? arrozServings = null,
        int highChairs = 0,
        int babyStrollers = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a reservation in the external PHP system.
    /// </summary>
    Task<bool> CancelReservationAsync(
        int bookingId,
        CancellationToken cancellationToken = default);
}
