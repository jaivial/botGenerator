using BotGenerator.Core.Models;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System.Text.Json;

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
                FROM conversation_messages
                WHERE phone_number = @Phone
                ORDER BY timestamp ASC
                LIMIT 100";

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
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var normalizedPhone = NormalizePhone(phoneNumber);

            var sql = @"
                INSERT INTO conversation_messages (
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

            var parameters = new
            {
                Phone = normalizedPhone,
                Role = message.Role,
                Content = message.Content,
                Timestamp = message.Timestamp != null 
                    ? DateTime.Parse(message.Timestamp) 
                    : DateTime.UtcNow,
                MessageId = message.MessageId,
                FromName = message.FromName
            };

            await connection.ExecuteAsync(sql, parameters);

            _logger.LogDebug(
                "Saved message for phone {Phone}, role {Role}",
                normalizedPhone, message.Role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving message for phone {Phone}", phoneNumber);
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
                FROM conversation_messages 
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

            var sql = @"DELETE FROM conversation_messages WHERE phone_number = @Phone";

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
}
