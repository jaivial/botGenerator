using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

public interface IBookingConfirmationOutboxRepository
{
    Task<BookingConfirmationOutboxMessage> EnqueueAsync(
        BookingConfirmationOutboxDraft draft,
        CancellationToken cancellationToken = default);

    Task<BookingConfirmationOutboxMessage?> ClaimByIdAsync(
        long outboxId,
        DateTime nowUtc,
        BookingConfirmationOutboxOptions options,
        CancellationToken cancellationToken = default);

    Task<BookingConfirmationOutboxMessage?> ClaimNextDueAsync(
        DateTime nowUtc,
        BookingConfirmationOutboxOptions options,
        CancellationToken cancellationToken = default);

    Task<bool> MarkAcceptedAsync(
        long outboxId,
        string claimToken,
        DateTime acceptedAtUtc,
        CancellationToken cancellationToken = default);

    Task<bool> MarkFailedAsync(
        long outboxId,
        string claimToken,
        string error,
        DateTime? nextAttemptAtUtc,
        CancellationToken cancellationToken = default);
}
