using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Delivers claimed confirmation records. Provider acceptance is persisted before
/// optional link buttons so a failed enhancement cannot resend accepted text.
/// </summary>
public sealed class BookingConfirmationOutboxProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IBookingConfirmationOutboxRepository _repository;
    private readonly IWhatsAppService _whatsApp;
    private readonly BookingConfirmationOutboxOptions _options;
    private readonly ILogger<BookingConfirmationOutboxProcessor> _logger;

    public BookingConfirmationOutboxProcessor(
        IBookingConfirmationOutboxRepository repository,
        IWhatsAppService whatsApp,
        BookingConfirmationOutboxOptions options,
        ILogger<BookingConfirmationOutboxProcessor> logger)
    {
        _repository = repository;
        _whatsApp = whatsApp;
        _options = options;
        _logger = logger;
    }

    public bool IsEnabled => _options.Enabled;

    public async Task<BookingConfirmationDeliveryResult> TryDeliverAsync(
        long outboxId,
        CancellationToken cancellationToken = default)
    {
        var message = await _repository.ClaimByIdAsync(outboxId, DateTime.UtcNow, _options, cancellationToken);
        return message is null
            ? new BookingConfirmationDeliveryResult { Status = BookingConfirmationDeliveryStatus.NotDue }
            : await DeliverClaimedAsync(message, cancellationToken);
    }

    public async Task<BookingConfirmationDeliveryResult> ProcessNextDueAsync(
        CancellationToken cancellationToken = default)
    {
        var message = await _repository.ClaimNextDueAsync(DateTime.UtcNow, _options, cancellationToken);
        return message is null
            ? new BookingConfirmationDeliveryResult { Status = BookingConfirmationDeliveryStatus.NotDue }
            : await DeliverClaimedAsync(message, cancellationToken);
    }

    private async Task<BookingConfirmationDeliveryResult> DeliverClaimedAsync(
        BookingConfirmationOutboxMessage message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.ClaimToken))
            throw new InvalidOperationException("Cannot deliver an outbox message without a claim token.");

        try
        {
            var payload = JsonSerializer.Deserialize<BookingConfirmationPayload>(message.PayloadJson, JsonOptions);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Text))
                throw new JsonException("Booking confirmation payload has no text.");

            var providerAccepted = await _whatsApp.SendTextAsync(message.PhoneNumber, payload.Text, cancellationToken);
            if (!providerAccepted)
                return await RecordFailureAsync(message, "Provider did not accept booking confirmation text.", cancellationToken);

            var markedAccepted = await _repository.MarkAcceptedAsync(
                message.Id,
                message.ClaimToken,
                DateTime.UtcNow,
                cancellationToken);
            if (!markedAccepted)
            {
                _logger.LogError(
                    "Provider accepted booking confirmation {OutboxId}, but accepted state could not be recorded; no second submission was attempted",
                    message.Id);
                return new BookingConfirmationDeliveryResult
                {
                    Status = BookingConfirmationDeliveryStatus.RetryScheduled,
                    ProviderAccepted = true,
                    Attempts = message.Attempts
                };
            }

            await TrySendLinkButtonsAsync(message, payload, cancellationToken);
            _logger.LogInformation(
                "Booking confirmation {OutboxId} accepted by {Provider}; API acceptance does not confirm WhatsApp delivery",
                message.Id,
                message.Provider);
            return new BookingConfirmationDeliveryResult
            {
                Status = BookingConfirmationDeliveryStatus.Accepted,
                ProviderAccepted = true,
                Attempts = message.Attempts
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Booking confirmation {OutboxId} attempt {Attempt} failed", message.Id, message.Attempts);
            return await RecordFailureAsync(message, ex.Message, cancellationToken);
        }
    }

    private async Task TrySendLinkButtonsAsync(
        BookingConfirmationOutboxMessage message,
        BookingConfirmationPayload payload,
        CancellationToken cancellationToken)
    {
        if (payload.LinkButtons.Count == 0)
            return;

        try
        {
            var accepted = await _whatsApp.SendLinkButtonsAsync(
                message.PhoneNumber,
                payload.LinkButtonsText,
                payload.LinkButtons.Select(button => new LinkButtonOption(button.Text, button.Url)).ToList(),
                cancellationToken);
            if (!accepted)
            {
                _logger.LogWarning(
                    "Booking confirmation link buttons were not accepted for outbox {OutboxId}; plain URLs remain in accepted text",
                    message.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Booking confirmation link buttons failed for outbox {OutboxId}; plain URLs remain in accepted text",
                message.Id);
        }
    }

    private async Task<BookingConfirmationDeliveryResult> RecordFailureAsync(
        BookingConfirmationOutboxMessage message,
        string error,
        CancellationToken cancellationToken)
    {
        var nextAttemptAtUtc = BookingConfirmationRetryPolicy.GetNextAttemptAtUtc(DateTime.UtcNow, message.Attempts, _options);
        var recorded = await _repository.MarkFailedAsync(
            message.Id,
            message.ClaimToken!,
            error,
            nextAttemptAtUtc,
            cancellationToken);
        if (!recorded)
        {
            _logger.LogError(
                "Could not record failed booking confirmation {OutboxId}; lease recovery will make it eligible again",
                message.Id);
        }

        return new BookingConfirmationDeliveryResult
        {
            Status = nextAttemptAtUtc.HasValue
                ? BookingConfirmationDeliveryStatus.RetryScheduled
                : BookingConfirmationDeliveryStatus.FailedPermanently,
            Attempts = message.Attempts
        };
    }
}
