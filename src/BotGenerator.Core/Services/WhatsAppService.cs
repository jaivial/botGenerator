using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BotGenerator.Core.Services;

/// <summary>
/// Implementation of IWhatsAppService using UAZAPI.
/// </summary>
public class WhatsAppService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly string _rejectCallPath;
    private readonly ILogger<WhatsAppService> _logger;

    public WhatsAppService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<WhatsAppService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _token = configuration["WhatsApp:Token"]
            ?? throw new InvalidOperationException("WhatsApp:Token not configured");

        _rejectCallPath = configuration["WhatsApp:RejectCallPath"] ?? "/call/reject";
    }

    public async Task<bool> SendTextAsync(
        string phoneNumber,
        string text,
        CancellationToken cancellationToken = default)
    {
        var normalizedNumber = NormalizeRecipientNumber(phoneNumber);
        _logger.LogInformation(
            "Sending text message to {Phone}: {Preview}",
            normalizedNumber,
            text.Length > 50 ? text[..50] + "..." : text);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/send/text?token={Uri.EscapeDataString(_token)}");

        // Keep header token for backward compatibility (some UAZAPI setups accept it)
        request.Headers.Add("token", _token);
        request.Content = JsonContent.Create(new
        {
            number = normalizedNumber,
            text = text
        });

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Failed to send message. Status: {Status}, Error: {Error}",
                    response.StatusCode, error);
                return false;
            }

            _logger.LogDebug("Message sent successfully to {Phone}", phoneNumber);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to {Phone}", phoneNumber);
            return false;
        }
    }

    public async Task<bool> SendButtonsAsync(
        string phoneNumber,
        string text,
        string footer,
        List<ButtonOption> buttons,
        CancellationToken cancellationToken = default)
    {
        var normalizedNumber = NormalizeRecipientNumber(phoneNumber);
        _logger.LogInformation(
            "Sending buttons message to {Phone} with {Count} buttons",
            normalizedNumber, buttons.Count);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/send/menu?token={Uri.EscapeDataString(_token)}");

        request.Headers.Add("token", _token);

        // UAZAPI button format
        var choices = buttons.Select(b =>
            $"{b.Text}|{b.Id}|{b.Description ?? b.Text}").ToList();

        request.Content = JsonContent.Create(new
        {
            number = normalizedNumber,
            type = "button",
            text = text,
            footerText = footer,
            selectableCount = 1,
            choices = choices
        });

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending buttons to {Phone}", phoneNumber);
            return false;
        }
    }

    public async Task<bool> SendMenuAsync(
        string phoneNumber,
        string text,
        string buttonText,
        List<MenuSection> sections,
        CancellationToken cancellationToken = default)
    {
        var normalizedNumber = NormalizeRecipientNumber(phoneNumber);
        _logger.LogInformation(
            "Sending menu message to {Phone}",
            normalizedNumber);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/send/menu?token={Uri.EscapeDataString(_token)}");

        request.Headers.Add("token", _token);

        // Build choices array per UAZAPI docs:
        // "[Section Title]" for section headers
        // "text|id|description" for items (WhatsApp limits: text max 24 chars, description max 72 chars)
        var choices = new List<string>();
        foreach (var section in sections)
        {
            // Add section header in brackets
            var sectionTitle = section.Title?.Length > 24 ? section.Title[..24] : section.Title;
            choices.Add($"[{sectionTitle}]");

            // Add rows as "text|id|description"
            foreach (var row in section.Rows)
            {
                var title = row.Title?.Length > 24 ? row.Title[..21] + "..." : row.Title;
                var desc = row.Description?.Length > 72 ? row.Description[..69] + "..." : (row.Description ?? "");
                choices.Add($"{title}|{row.Id}|{desc}");
            }
        }

        request.Content = JsonContent.Create(new
        {
            number = normalizedNumber,
            type = "list",
            text = text,
            listButton = buttonText,  // UAZAPI uses "listButton" not "buttonText"
            choices = choices
        });

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("UAZAPI menu response {Status}: {Body}", (int)response.StatusCode, responseBody);
                return false;
            }

            _logger.LogDebug("UAZAPI menu response: {Body}", responseBody);

            // Check if response body contains error indicators
            if (responseBody.Contains("\"error\"") || responseBody.Contains("\"status\":\"error\""))
            {
                _logger.LogWarning("UAZAPI returned success status but body contains error: {Body}", responseBody);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending menu to {Phone}", phoneNumber);
            return false;
        }
    }

    public async Task<List<WhatsAppHistoryMessage>> GetHistoryAsync(
        string phoneNumber,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var page = await GetHistoryPageAsync(phoneNumber, limit, 0, cancellationToken);
        return page.Messages;
    }

    public async Task<WhatsAppHistoryPage> GetHistoryPageAsync(
        string phoneNumber,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var normalizedNumber = NormalizeChatIdNumber(phoneNumber);
        var safeLimit = Math.Clamp(limit, 1, 500);
        var safeOffset = Math.Max(0, offset);

        _logger.LogDebug(
            "Getting history page for {Phone}, limit: {Limit}, offset: {Offset}",
            normalizedNumber,
            safeLimit,
            safeOffset);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/message/find?token={Uri.EscapeDataString(_token)}");

        request.Headers.Add("token", _token);
        request.Content = JsonContent.Create(new
        {
            chatid = $"{normalizedNumber}@s.whatsapp.net",
            limit = safeLimit,
            offset = safeOffset
        });

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Failed to get history page for {Phone}. status={Status} body={Body}",
                    phoneNumber,
                    (int)response.StatusCode,
                    body);

                return new WhatsAppHistoryPage
                {
                    Limit = safeLimit,
                    Offset = safeOffset,
                    NextOffset = safeOffset,
                    HasMore = false
                };
            }

            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseHistoryPage(raw, safeLimit, safeOffset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting history page for {Phone}", phoneNumber);
            return new WhatsAppHistoryPage
            {
                Limit = safeLimit,
                Offset = safeOffset,
                NextOffset = safeOffset,
                HasMore = false
            };
        }
    }

    public async Task<List<WhatsAppHistoryMessage>> GetFullHistoryAsync(
        string phoneNumber,
        int pageSize = 100,
        int maxPages = 50,
        CancellationToken cancellationToken = default)
    {
        var safePageSize = Math.Clamp(pageSize, 1, 500);
        var safeMaxPages = Math.Clamp(maxPages, 1, 500);

        var dedup = new Dictionary<string, WhatsAppHistoryMessage>(StringComparer.Ordinal);
        var offset = 0;

        for (var i = 0; i < safeMaxPages; i++)
        {
            var page = await GetHistoryPageAsync(phoneNumber, safePageSize, offset, cancellationToken);
            if (page.Messages.Count == 0)
                break;

            foreach (var message in page.Messages)
            {
                var key = !string.IsNullOrWhiteSpace(message.MessageId)
                    ? message.MessageId!
                    : $"{message.FromMe}|{message.Timestamp}|{message.Text}";

                dedup[key] = message;
            }

            if (!page.HasMore)
                break;

            var nextOffset = page.NextOffset > offset
                ? page.NextOffset
                : offset + page.Messages.Count;

            if (nextOffset <= offset)
                break;

            offset = nextOffset;
        }

        return dedup.Values
            .OrderBy(m => NormalizeTimestamp(m.Timestamp))
            .ToList();
    }

    private WhatsAppHistoryPage ParseHistoryPage(string raw, int requestedLimit, int requestedOffset)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var list = ParseMessages(root);
                return new WhatsAppHistoryPage
                {
                    Messages = list,
                    Limit = requestedLimit,
                    Offset = requestedOffset,
                    NextOffset = requestedOffset + list.Count,
                    HasMore = list.Count >= requestedLimit
                };
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new WhatsAppHistoryPage
                {
                    Limit = requestedLimit,
                    Offset = requestedOffset,
                    NextOffset = requestedOffset,
                    HasMore = false
                };
            }

            var messagesElement = TryGetPropertyIgnoreCase(root, "messages", out var directMessages)
                ? directMessages
                : default;

            if (messagesElement.ValueKind != JsonValueKind.Array &&
                TryGetPropertyIgnoreCase(root, "result", out var resultElement) &&
                resultElement.ValueKind == JsonValueKind.Object &&
                TryGetPropertyIgnoreCase(resultElement, "messages", out var nestedMessages))
            {
                messagesElement = nestedMessages;
            }

            var messages = messagesElement.ValueKind == JsonValueKind.Array
                ? ParseMessages(messagesElement)
                : new List<WhatsAppHistoryMessage>();

            var limit = GetIntProperty(root, "limit") ?? requestedLimit;
            var offset = GetIntProperty(root, "offset") ?? requestedOffset;
            var nextOffset = GetIntProperty(root, "nextOffset") ?? (offset + messages.Count);
            var hasMore = GetBoolProperty(root, "hasMore") ?? (messages.Count >= limit);

            return new WhatsAppHistoryPage
            {
                Messages = messages,
                Limit = limit,
                Offset = offset,
                NextOffset = nextOffset,
                HasMore = hasMore
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse history response payload");
            return new WhatsAppHistoryPage
            {
                Limit = requestedLimit,
                Offset = requestedOffset,
                NextOffset = requestedOffset,
                HasMore = false
            };
        }
    }

    private static List<WhatsAppHistoryMessage> ParseMessages(JsonElement arrayElement)
    {
        var output = new List<WhatsAppHistoryMessage>();

        foreach (var item in arrayElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var text = GetStringProperty(item, "text")
                ?? GetStringProperty(item, "body")
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(text))
                continue;

            var fromMe = GetBoolProperty(item, "fromMe")
                ?? GetBoolProperty(item, "from_me")
                ?? false;

            var timestamp = GetLongProperty(item, "messageTimestamp")
                ?? GetLongProperty(item, "timestamp")
                ?? 0;

            var senderName = GetStringProperty(item, "senderName")
                ?? GetStringProperty(item, "sender");

            var messageId = GetStringProperty(item, "messageid")
                ?? GetStringProperty(item, "messageId")
                ?? GetStringProperty(item, "id");

            output.Add(new WhatsAppHistoryMessage
            {
                Text = text,
                FromMe = fromMe,
                Timestamp = timestamp,
                SenderName = senderName,
                MessageId = messageId
            });
        }

        return output;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static int? GetIntProperty(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
            return intValue;

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out intValue))
            return intValue;

        return null;
    }

    private static long? GetLongProperty(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var longValue))
            return longValue;

        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out longValue))
            return longValue;

        return null;
    }

    private static bool? GetBoolProperty(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.True)
            return true;
        if (value.ValueKind == JsonValueKind.False)
            return false;

        if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var boolValue))
            return boolValue;

        return null;
    }

    private static DateTime NormalizeTimestamp(long timestamp)
    {
        if (timestamp <= 0)
            return DateTime.MinValue;

        try
        {
            var isMilliseconds = timestamp > 10_000_000_000;
            return isMilliseconds
                ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime
                : DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    public async Task<bool> SendLinkButtonsAsync(
        string phoneNumber,
        string text,
        List<LinkButtonOption> buttons,
        CancellationToken cancellationToken = default)
    {
        var normalizedNumber = NormalizeRecipientNumber(phoneNumber);

        _logger.LogInformation(
            "Sending link buttons to {Phone} with {Count} buttons",
            normalizedNumber,
            buttons.Count);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/send/menu?token={Uri.EscapeDataString(_token)}");

        request.Headers.Add("token", _token);

        var choices = buttons
            .Select(b => $"{b.Text}|{b.Url}")
            .ToList();

        request.Content = JsonContent.Create(new
        {
            number = normalizedNumber,
            type = "button",
            text = text,
            choices = choices
        });

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending link buttons to {Phone}", normalizedNumber);
            return false;
        }
    }

    private static string NormalizeRecipientNumber(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return input;

        // Already Spain country code + 9 digits
        if (digits.StartsWith("34") && digits.Length == 11)
            return digits;

        // Local 9 digits -> prefix country code
        if (digits.Length == 9)
            return "34" + digits;

        // If longer, keep last 9 digits and prefix 34
        if (digits.Length > 9)
            return "34" + digits[^9..];

        // Fallback: return as-is
        return digits;
    }

    private static string NormalizeChatIdNumber(string input)
    {
        // For chatid lookups we want digits only, without the @ suffix (caller adds it)
        if (string.IsNullOrWhiteSpace(input)) return input;
        return new string(input.Where(char.IsDigit).ToArray());
    }

    public async Task<bool> SendContactCardAsync(
        string phoneNumber,
        string fullName,
        string contactPhoneNumber,
        string? organization = null,
        string? email = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedNumber = NormalizeRecipientNumber(phoneNumber);

        _logger.LogInformation(
            "Sending contact card to {Phone}: {FullName} ({ContactPhone})",
            normalizedNumber, fullName, contactPhoneNumber);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/send/contact?token={Uri.EscapeDataString(_token)}");

        request.Headers.Add("token", _token);
        request.Content = JsonContent.Create(new
        {
            number = normalizedNumber,
            fullName = fullName,
            phoneNumber = contactPhoneNumber,
            organization = organization ?? "",
            email = email ?? ""
        });

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Failed to send contact card. Status: {Status}, Error: {Error}",
                    response.StatusCode, error);
                return false;
            }

            _logger.LogDebug("Contact card sent successfully to {Phone}", phoneNumber);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending contact card to {Phone}", phoneNumber);
            return false;
        }
    }

    public async Task<bool> RejectCallAsync(
        string phoneNumber,
        string? callId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedNumber = NormalizeRecipientNumber(phoneNumber);

        _logger.LogInformation(
            "Rejecting call from {Phone} (callId={CallId})",
            normalizedNumber,
            string.IsNullOrWhiteSpace(callId) ? "(none)" : callId);

        var path = string.IsNullOrWhiteSpace(_rejectCallPath) ? "/call/reject" : _rejectCallPath;
        if (!path.StartsWith('/'))
            path = "/" + path;

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{path}?token={Uri.EscapeDataString(_token)}");

        request.Headers.Add("token", _token);

        object payload = string.IsNullOrWhiteSpace(callId)
            ? new { number = normalizedNumber }
            : new { number = normalizedNumber, callId = callId };

        request.Content = JsonContent.Create(payload);

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Failed to reject call for {Phone}. Status: {Status}, Error: {Error}",
                    normalizedNumber, (int)response.StatusCode, error);
                return false;
            }

            _logger.LogDebug("Call rejected successfully for {Phone}", normalizedNumber);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting call for {Phone}", normalizedNumber);
            return false;
        }
    }
}
