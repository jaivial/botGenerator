using BotGenerator.Core.Models;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace BotGenerator.Core.Services;

/// <summary>
/// MySQL implementation of IMessageRepository for persisting conversation history.
/// </summary>
public class MessageRepository : IMessageRepository
{
    private readonly string _connectionString;
    private readonly ILogger<MessageRepository> _logger;

    public MessageRepository(IConfiguration configuration, ILogger<MessageRepository> logger)
    {
        _connectionString = configuration["MySQL:ConnectionString"]
            ?? throw new InvalidOperationException("MySQL:ConnectionString not configured");
        _logger = logger;
    }

    public async Task<List<ChatMessage>> GetMessagesAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var normalizedPhone = NormalizePhone(phoneNumber);

            var sql = @"
                SELECT 
                    role,
                    content,
                    timestamp,
                    message_id as MessageId,
                    from_name as FromName
                FROM bot_conversation_messages
                WHERE phone_number = @Phone
                ORDER BY timestamp ASC
                LIMIT 200";

            var results = await connection.QueryAsync<dynamic>(sql, new { Phone = normalizedPhone });

            var messages = new List<ChatMessage>();
            foreach (var row in results)
            {
                messages.Add(new ChatMessage
                {
                    Role = row.role,
                    Content = row.content,
                    Timestamp = row.timestamp?.ToString("O"),
                    MessageId = row.MessageId,
                    FromName = row.FromName
                });
            }

            _logger.LogDebug(
                "Retrieved {Count} messages for phone {Phone}",
                messages.Count, normalizedPhone);

            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting messages for phone {Phone}", phoneNumber);
            return new List<ChatMessage>();
        }
    }

    public async Task SaveMessageAsync(
        string phoneNumber,
        ChatMessage message,
        CancellationToken cancellationToken = default)
    {
        await SaveMessagesDeduplicatedAsync(phoneNumber, new[] { message }, cancellationToken);
    }

    public async Task<int> SaveMessagesDeduplicatedAsync(
        string phoneNumber,
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var normalizedPhone = NormalizePhone(phoneNumber);

            var input = messages
                .Where(m => !string.IsNullOrWhiteSpace(m.Content))
                .ToList();

            if (input.Count == 0)
                return 0;

            var candidateIds = input
                .Select(m => m.MessageId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .Distinct()
                .ToList();

            HashSet<string> existingIds = new(StringComparer.Ordinal);
            if (candidateIds.Count > 0)
            {
                var existing = await connection.QueryAsync<string>(@"
                    SELECT message_id
                    FROM bot_conversation_messages
                    WHERE phone_number = @Phone
                      AND message_id IN @Ids",
                    new
                    {
                        Phone = normalizedPhone,
                        Ids = candidateIds
                    });

                existingIds = existing
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToHashSet(StringComparer.Ordinal);
            }

            var toInsert = input
                .Where(m => string.IsNullOrWhiteSpace(m.MessageId) || !existingIds.Contains(m.MessageId))
                .Select(m => new
                {
                    Phone = normalizedPhone,
                    Role = m.Role,
                    Content = m.Content,
                    Timestamp = ParseTimestampOrNow(m.Timestamp),
                    MessageId = m.MessageId,
                    FromName = m.FromName
                })
                .ToList();

            if (toInsert.Count == 0)
                return 0;

            var sql = @"
                INSERT INTO bot_conversation_messages (
                    phone_number,
                    role,
                    content,
                    timestamp,
                    message_id,
                    from_name
                ) VALUES (
                    @Phone,
                    @Role,
                    @Content,
                    @Timestamp,
                    @MessageId,
                    @FromName
                )";

            var inserted = await connection.ExecuteAsync(sql, toInsert);

            _logger.LogDebug(
                "Saved {Count} messages for phone {Phone} (deduplicated)",
                inserted,
                normalizedPhone);

            return inserted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving messages for phone {Phone}", phoneNumber);
            return 0;
        }
    }

    public async Task<List<string>> GetRecentMessageIdsAsync(
        string phoneNumber,
        int limit = 300,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var normalizedPhone = NormalizePhone(phoneNumber);
            var safeLimit = Math.Clamp(limit, 1, 1000);

            var ids = await connection.QueryAsync<string>(@"
                SELECT message_id
                FROM bot_conversation_messages
                WHERE phone_number = @Phone
                  AND message_id IS NOT NULL
                  AND message_id <> ''
                ORDER BY timestamp DESC
                LIMIT @Limit",
                new
                {
                    Phone = normalizedPhone,
                    Limit = safeLimit
                });

            return ids
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading recent message IDs for phone {Phone}", phoneNumber);
            return new List<string>();
        }
    }

    public async Task<bool> HasMessagesAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var normalizedPhone = NormalizePhone(phoneNumber);

            var sql = @"
                SELECT COUNT(*) 
                FROM bot_conversation_messages 
                WHERE phone_number = @Phone";

            var count = await connection.ExecuteScalarAsync<int>(sql, new { Phone = normalizedPhone });

            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking messages for phone {Phone}", phoneNumber);
            return false;
        }
    }

    public async Task ClearMessagesAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var normalizedPhone = NormalizePhone(phoneNumber);

            var sql = @"DELETE FROM bot_conversation_messages WHERE phone_number = @Phone";

            await connection.ExecuteAsync(sql, new { Phone = normalizedPhone });

            _logger.LogInformation(
                "Cleared all messages for phone {Phone}",
                normalizedPhone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing messages for phone {Phone}", phoneNumber);
        }
    }

    private static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return phone;

        // Keep digits only
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        
        // Store with country code if available, otherwise as-is
        // This ensures consistency with how phone numbers are stored elsewhere
        return digits;
    }

    private static DateTime ParseTimestampOrNow(string? timestamp)
    {
        if (string.IsNullOrWhiteSpace(timestamp))
            return DateTime.UtcNow;

        return DateTime.TryParse(timestamp, out var parsed)
            ? parsed
            : DateTime.UtcNow;
    }
}
