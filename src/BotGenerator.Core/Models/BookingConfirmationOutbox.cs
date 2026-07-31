namespace BotGenerator.Core.Models;

/// <summary>
/// Persistent state for a booking confirmation submitted to a WhatsApp provider.
/// Accepted means provider API acceptance only; delivery/read receipts are not inferred.
/// </summary>
public enum BookingConfirmationOutboxState
{
    Pending,
    Processing,
    Accepted,
    Failed
}

public sealed record BookingConfirmationLink(string Text, string Url);

public sealed record BookingConfirmationPayload
{
    public required string Text { get; init; }
    public required string LinkButtonsText { get; init; }
    public List<BookingConfirmationLink> LinkButtons { get; init; } = [];
}

public sealed record BookingConfirmationOutboxDraft
{
    public required long BookingId { get; init; }
    public required string Provider { get; init; }
    public required string PhoneNumber { get; init; }
    public required BookingConfirmationPayload Payload { get; init; }
}

public sealed record BookingConfirmationOutboxMessage
{
    public required long Id { get; init; }
    public required long BookingId { get; init; }
    public required string NotificationType { get; init; }
    public required string Provider { get; init; }
    public required string PhoneNumber { get; init; }
    public required string PayloadJson { get; init; }
    public required BookingConfirmationOutboxState State { get; init; }
    public required int Attempts { get; init; }
    public DateTime? NextAttemptAtUtc { get; init; }
    public string? LastError { get; init; }
    public string? ClaimToken { get; init; }
}

public enum BookingConfirmationDeliveryStatus
{
    NotDue,
    Accepted,
    RetryScheduled,
    FailedPermanently
}

public sealed record BookingConfirmationDeliveryResult
{
    public required BookingConfirmationDeliveryStatus Status { get; init; }
    public bool ProviderAccepted { get; init; }
    public int Attempts { get; init; }
}

public sealed record BookingConfirmationOutboxOptions
{
    // Enable only after manual outbox schema migration.
    public bool Enabled { get; init; }
    public int MaxAttempts { get; init; } = 5;
    public int InitialRetryDelaySeconds { get; init; } = 30;
    public int MaxRetryDelaySeconds { get; init; } = 1800;
    public int LeaseSeconds { get; init; } = 120;
    public int PollIntervalSeconds { get; init; } = 10;
    public int BatchSize { get; init; } = 20;

    public TimeSpan InitialRetryDelay => TimeSpan.FromSeconds(InitialRetryDelaySeconds);
    public TimeSpan MaxRetryDelay => TimeSpan.FromSeconds(MaxRetryDelaySeconds);
    public TimeSpan LeaseDuration => TimeSpan.FromSeconds(LeaseSeconds);
    public TimeSpan PollInterval => TimeSpan.FromSeconds(PollIntervalSeconds);

    public static BookingConfirmationOutboxOptions FromConfiguration(IConfiguration configuration) => new()
    {
        Enabled = configuration.GetValue("BookingConfirmationOutbox:Enabled", false),
        MaxAttempts = Math.Clamp(configuration.GetValue("BookingConfirmationOutbox:MaxAttempts", 5), 1, 20),
        InitialRetryDelaySeconds = Math.Clamp(configuration.GetValue("BookingConfirmationOutbox:InitialRetryDelaySeconds", 30), 1, 3600),
        MaxRetryDelaySeconds = Math.Clamp(configuration.GetValue("BookingConfirmationOutbox:MaxRetryDelaySeconds", 1800), 1, 86400),
        LeaseSeconds = Math.Clamp(configuration.GetValue("BookingConfirmationOutbox:LeaseSeconds", 120), 30, 3600),
        PollIntervalSeconds = Math.Clamp(configuration.GetValue("BookingConfirmationOutbox:PollIntervalSeconds", 10), 1, 300),
        BatchSize = Math.Clamp(configuration.GetValue("BookingConfirmationOutbox:BatchSize", 20), 1, 100)
    };
}

public static class BookingConfirmationRetryPolicy
{
    public static DateTime? GetNextAttemptAtUtc(
        DateTime nowUtc,
        int attempts,
        BookingConfirmationOutboxOptions options)
    {
        if (attempts >= options.MaxAttempts)
            return null;

        var exponent = Math.Clamp(attempts - 1, 0, 30);
        var delaySeconds = Math.Min(
            options.InitialRetryDelay.TotalSeconds * Math.Pow(2, exponent),
            options.MaxRetryDelay.TotalSeconds);

        return nowUtc.AddSeconds(delaySeconds);
    }
}
