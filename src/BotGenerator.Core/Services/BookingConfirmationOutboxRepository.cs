using System.Data;
using BotGenerator.Core.Models;
using Dapper;
using MySqlConnector;

namespace BotGenerator.Core.Services;

/// <summary>
/// MySQL repository for booking-confirmation outbox records. Schema is supplied
/// by docs/migrations/20260731_booking_confirmation_outbox.sql and is never created at runtime.
/// </summary>
public sealed class BookingConfirmationOutboxRepository : IBookingConfirmationOutboxRepository
{
    private const string NotificationType = "booking_confirmation";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _connectionString;
    private readonly ILogger<BookingConfirmationOutboxRepository> _logger;

    public BookingConfirmationOutboxRepository(
        IConfiguration configuration,
        ILogger<BookingConfirmationOutboxRepository> logger)
    {
        _connectionString = configuration["MySQL:ConnectionString"]
            ?? throw new InvalidOperationException("MySQL:ConnectionString not configured");
        _logger = logger;
    }

    public async Task<BookingConfirmationOutboxMessage> EnqueueAsync(
        BookingConfirmationOutboxDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.Provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.PhoneNumber);

        var payloadJson = JsonSerializer.Serialize(draft.Payload, JsonOptions);
        const string insertSql = @"
            INSERT INTO booking_confirmation_outbox (
                booking_id, notification_type, provider, phone_number, payload_json,
                state, attempts, next_attempt_at, created_at, updated_at
            ) VALUES (
                @BookingId, @NotificationType, @Provider, @PhoneNumber, @PayloadJson,
                'pending', 0, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
            )
            ON DUPLICATE KEY UPDATE id = LAST_INSERT_ID(id);";

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                insertSql,
                new
                {
                    draft.BookingId,
                    NotificationType,
                    draft.Provider,
                    draft.PhoneNumber,
                    PayloadJson = payloadJson
                },
                cancellationToken: cancellationToken));

            var row = await GetByBookingIdAsync(connection, draft.BookingId, cancellationToken);
            if (row is null)
                throw new InvalidOperationException("Outbox insert completed but notification record was not found.");

            return ToMessage(row);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not enqueue booking confirmation for booking {BookingId}", draft.BookingId);
            throw;
        }
    }

    public Task<BookingConfirmationOutboxMessage?> ClaimByIdAsync(
        long outboxId,
        DateTime nowUtc,
        BookingConfirmationOutboxOptions options,
        CancellationToken cancellationToken = default) =>
        ClaimAsync(outboxId, nowUtc, options, cancellationToken);

    public Task<BookingConfirmationOutboxMessage?> ClaimNextDueAsync(
        DateTime nowUtc,
        BookingConfirmationOutboxOptions options,
        CancellationToken cancellationToken = default) =>
        ClaimAsync(null, nowUtc, options, cancellationToken);

    public async Task<bool> MarkAcceptedAsync(
        long outboxId,
        string claimToken,
        DateTime acceptedAtUtc,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE booking_confirmation_outbox
            SET state = 'accepted',
                next_attempt_at = NULL,
                accepted_at = @AcceptedAtUtc,
                last_error = NULL,
                claim_token = NULL,
                lease_expires_at = NULL,
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @OutboxId
              AND state = 'processing'
              AND claim_token = @ClaimToken;";

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var changed = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { OutboxId = outboxId, ClaimToken = claimToken, AcceptedAtUtc = EnsureUtc(acceptedAtUtc) },
            cancellationToken: cancellationToken));
        return changed == 1;
    }

    public async Task<bool> MarkFailedAsync(
        long outboxId,
        string claimToken,
        string error,
        DateTime? nextAttemptAtUtc,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE booking_confirmation_outbox
            SET state = 'failed',
                next_attempt_at = @NextAttemptAtUtc,
                last_error = @LastError,
                claim_token = NULL,
                lease_expires_at = NULL,
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @OutboxId
              AND state = 'processing'
              AND claim_token = @ClaimToken;";

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var changed = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                OutboxId = outboxId,
                ClaimToken = claimToken,
                LastError = TruncateError(error),
                NextAttemptAtUtc = nextAttemptAtUtc.HasValue ? EnsureUtc(nextAttemptAtUtc.Value) : (DateTime?)null
            },
            cancellationToken: cancellationToken));
        return changed == 1;
    }

    private async Task<BookingConfirmationOutboxMessage?> ClaimAsync(
        long? outboxId,
        DateTime nowUtc,
        BookingConfirmationOutboxOptions options,
        CancellationToken cancellationToken)
    {
        var normalizedNow = EnsureUtc(nowUtc);
        var claimToken = Guid.NewGuid().ToString();
        var leaseExpiresAtUtc = normalizedNow.Add(options.LeaseDuration);

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            const string releaseExpiredSql = @"
                UPDATE booking_confirmation_outbox
                SET state = 'failed',
                    next_attempt_at = @NowUtc,
                    last_error = COALESCE(last_error, 'Processing lease expired before provider acceptance was recorded.'),
                    claim_token = NULL,
                    lease_expires_at = NULL,
                    updated_at = UTC_TIMESTAMP(6)
                WHERE state = 'processing'
                  AND lease_expires_at <= @NowUtc;";
            await connection.ExecuteAsync(new CommandDefinition(
                releaseExpiredSql,
                new { NowUtc = normalizedNow },
                transaction,
                cancellationToken: cancellationToken));

            var selectSql = outboxId.HasValue
                ? @"
                    SELECT id AS Id, booking_id AS BookingId, notification_type AS NotificationType,
                           provider AS Provider, phone_number AS PhoneNumber, payload_json AS PayloadJson,
                           state AS State, attempts AS Attempts, next_attempt_at AS NextAttemptAtUtc,
                           last_error AS LastError, claim_token AS ClaimToken
                    FROM booking_confirmation_outbox
                    WHERE id = @OutboxId
                      AND state IN ('pending', 'failed')
                      AND attempts < @MaxAttempts
                      AND next_attempt_at <= @NowUtc
                    FOR UPDATE SKIP LOCKED;"
                : @"
                    SELECT id AS Id, booking_id AS BookingId, notification_type AS NotificationType,
                           provider AS Provider, phone_number AS PhoneNumber, payload_json AS PayloadJson,
                           state AS State, attempts AS Attempts, next_attempt_at AS NextAttemptAtUtc,
                           last_error AS LastError, claim_token AS ClaimToken
                    FROM booking_confirmation_outbox
                    WHERE state IN ('pending', 'failed')
                      AND attempts < @MaxAttempts
                      AND next_attempt_at <= @NowUtc
                    ORDER BY next_attempt_at, id
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED;";

            var row = await connection.QuerySingleOrDefaultAsync<OutboxRow>(new CommandDefinition(
                selectSql,
                new { OutboxId = outboxId, MaxAttempts = options.MaxAttempts, NowUtc = normalizedNow },
                transaction,
                cancellationToken: cancellationToken));
            if (row is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            const string claimSql = @"
                UPDATE booking_confirmation_outbox
                SET state = 'processing',
                    attempts = attempts + 1,
                    claim_token = @ClaimToken,
                    lease_expires_at = @LeaseExpiresAtUtc,
                    updated_at = UTC_TIMESTAMP(6)
                WHERE id = @OutboxId;";
            await connection.ExecuteAsync(new CommandDefinition(
                claimSql,
                new { OutboxId = row.Id, ClaimToken = claimToken, LeaseExpiresAtUtc = leaseExpiresAtUtc },
                transaction,
                cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);

            return ToMessage(row) with
            {
                State = BookingConfirmationOutboxState.Processing,
                Attempts = row.Attempts + 1,
                ClaimToken = claimToken
            };
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<OutboxRow?> GetByBookingIdAsync(
        MySqlConnection connection,
        long bookingId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT id AS Id, booking_id AS BookingId, notification_type AS NotificationType,
                   provider AS Provider, phone_number AS PhoneNumber, payload_json AS PayloadJson,
                   state AS State, attempts AS Attempts, next_attempt_at AS NextAttemptAtUtc,
                   last_error AS LastError, claim_token AS ClaimToken
            FROM booking_confirmation_outbox
            WHERE booking_id = @BookingId
              AND notification_type = 'booking_confirmation';";
        return await connection.QuerySingleOrDefaultAsync<OutboxRow>(new CommandDefinition(
            sql,
            new { BookingId = bookingId },
            cancellationToken: cancellationToken));
    }

    private static BookingConfirmationOutboxMessage ToMessage(OutboxRow row) => new()
    {
        Id = row.Id,
        BookingId = row.BookingId,
        NotificationType = row.NotificationType,
        Provider = row.Provider,
        PhoneNumber = row.PhoneNumber,
        PayloadJson = row.PayloadJson,
        State = Enum.TryParse<BookingConfirmationOutboxState>(row.State, true, out var state)
            ? state
            : throw new InvalidOperationException($"Unknown booking confirmation outbox state '{row.State}'."),
        Attempts = row.Attempts,
        NextAttemptAtUtc = row.NextAttemptAtUtc,
        LastError = row.LastError,
        ClaimToken = row.ClaimToken
    };

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string TruncateError(string error) =>
        string.IsNullOrWhiteSpace(error) ? "Provider did not accept confirmation." : error[..Math.Min(error.Length, 2000)];

    private sealed record OutboxRow
    {
        public long Id { get; init; }
        public long BookingId { get; init; }
        public string NotificationType { get; init; } = "";
        public string Provider { get; init; } = "";
        public string PhoneNumber { get; init; } = "";
        public string PayloadJson { get; init; } = "";
        public string State { get; init; } = "";
        public int Attempts { get; init; }
        public DateTime? NextAttemptAtUtc { get; init; }
        public string? LastError { get; init; }
        public string? ClaimToken { get; init; }
    }
}
