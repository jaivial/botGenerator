using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BotGenerator.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BotGenerator.Core.Services;

/// <summary>
/// Evolution API 2.x implementation of <see cref="IWhatsAppService"/>.
/// A successful result means Evolution accepted a message with a returned key, not WhatsApp delivery.
/// </summary>
public sealed class EvolutionWhatsAppService : IWhatsAppService
{
    private const int MaxReplyButtons = 3;
    private const int MaxCtaButtons = 2;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _instanceName;
    private readonly ILogger<EvolutionWhatsAppService> _logger;

    public EvolutionWhatsAppService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<EvolutionWhatsAppService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["WhatsApp:Evolution:ApiKey"]
            ?? throw new InvalidOperationException("WhatsApp:Evolution:ApiKey not configured");
        _instanceName = configuration["WhatsApp:Evolution:InstanceName"]
            ?? throw new InvalidOperationException("WhatsApp:Evolution:InstanceName not configured");
        _logger = logger;
    }

    public Task<bool> SendTextAsync(
        string phoneNumber,
        string text,
        CancellationToken cancellationToken = default) =>
        SendAcceptedAsync(
            "sendText",
            NormalizeRecipientNumber(phoneNumber),
            new EvolutionTextRequest(NormalizeRecipientNumber(phoneNumber), WhatsAppMessageSanitizer.Sanitize(text)),
            cancellationToken);

    public Task<bool> SendButtonsAsync(
        string phoneNumber,
        string text,
        string footer,
        List<ButtonOption> buttons,
        CancellationToken cancellationToken = default)
    {
        if (buttons.Count is < 1 or > MaxReplyButtons)
        {
            _logger.LogWarning("Evolution sendButtons skipped because reply button count must be between 1 and {MaxButtons}", MaxReplyButtons);
            return Task.FromResult(false);
        }

        var normalizedNumber = NormalizeRecipientNumber(phoneNumber);
        var request = new EvolutionButtonsRequest(
            normalizedNumber,
            text,
            string.Empty,
            footer,
            buttons.Select(button => new EvolutionButton("reply", button.Text, button.Id, null)).ToList());

        return SendAcceptedAsync("sendButtons", normalizedNumber, request, cancellationToken);
    }

    public Task<bool> SendMenuAsync(
        string phoneNumber,
        string text,
        string buttonText,
        List<MenuSection> sections,
        CancellationToken cancellationToken = default)
    {
        if (sections.Count == 0 || sections.All(section => section.Rows.Count == 0))
        {
            _logger.LogWarning("Evolution sendList skipped because no menu rows were supplied");
            return Task.FromResult(false);
        }

        var normalizedNumber = NormalizeRecipientNumber(phoneNumber);
        var request = new EvolutionListRequest(
            normalizedNumber,
            text,
            string.Empty,
            string.Empty,
            buttonText,
            sections.Select(section => new EvolutionListSection(
                section.Title,
                section.Rows.Select(row => new EvolutionListRow(row.Title, row.Description ?? string.Empty, row.Id)).ToList())).ToList());

        return SendAcceptedAsync("sendList", normalizedNumber, request, cancellationToken);
    }

    public Task<bool> SendLinkButtonsAsync(
        string phoneNumber,
        string text,
        List<LinkButtonOption> buttons,
        CancellationToken cancellationToken = default)
    {
        if (buttons.Count is < 1 or > MaxCtaButtons || buttons.Any(button => !IsHttpUrl(button.Url)))
        {
            _logger.LogWarning("Evolution sendButtons skipped because URL buttons violate RC2 count or URL rules");
            return Task.FromResult(false);
        }

        var normalizedNumber = NormalizeRecipientNumber(phoneNumber);
        var request = new EvolutionButtonsRequest(
            normalizedNumber,
            text,
            string.Empty,
            string.Empty,
            buttons.Select(button => new EvolutionButton("url", button.Text, null, button.Url)).ToList());

        return SendAcceptedAsync("sendButtons", normalizedNumber, request, cancellationToken);
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
        var safeLimit = Math.Clamp(limit, 1, 500);
        var safeOffset = Math.Max(0, offset);
        var normalizedNumber = NormalizeRecipientNumber(phoneNumber);
        var pageNumber = (safeOffset / safeLimit) + 1;
        var payload = new EvolutionHistoryRequest(
            new EvolutionHistoryWhere(new EvolutionHistoryKey($"{normalizedNumber}@s.whatsapp.net")),
            pageNumber,
            safeLimit);

        try
        {
            using var request = CreateRequest(HttpMethod.Post, "chat/findMessages", payload);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Evolution findMessages failed for {Phone} with HTTP {StatusCode}",
                    normalizedNumber,
                    (int)response.StatusCode);
                return EmptyHistoryPage(safeLimit, safeOffset);
            }

            EvolutionHistoryResponse? result;
            try
            {
                result = JsonSerializer.Deserialize<EvolutionHistoryResponse>(body, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Evolution findMessages returned an unreadable response for {Phone}", normalizedNumber);
                return EmptyHistoryPage(safeLimit, safeOffset);
            }

            var records = result?.Messages?.Records ?? [];
            var messages = records
                .Select(record => new WhatsAppHistoryMessage
                {
                    Text = EvolutionMessageParser.ExtractText(record.Message),
                    FromMe = record.Key?.FromMe ?? false,
                    Timestamp = record.MessageTimestamp,
                    SenderName = record.PushName,
                    MessageId = record.Key?.Id
                })
                .Where(message => !string.IsNullOrWhiteSpace(message.Text))
                .ToList();
            var currentPage = Math.Max(1, result?.Messages?.CurrentPage ?? pageNumber);
            var totalPages = Math.Max(currentPage, result?.Messages?.Pages ?? currentPage);
            var hasMore = currentPage < totalPages && records.Count > 0;

            return new WhatsAppHistoryPage
            {
                Messages = messages,
                Limit = safeLimit,
                Offset = safeOffset,
                NextOffset = hasMore ? safeOffset + records.Count : safeOffset,
                HasMore = hasMore
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Evolution findMessages failed for {Phone}", normalizedNumber);
            return EmptyHistoryPage(safeLimit, safeOffset);
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
        var messages = new Dictionary<string, WhatsAppHistoryMessage>(StringComparer.Ordinal);
        var offset = 0;

        for (var pageIndex = 0; pageIndex < safeMaxPages; pageIndex++)
        {
            var page = await GetHistoryPageAsync(phoneNumber, safePageSize, offset, cancellationToken);
            foreach (var message in page.Messages)
            {
                var key = !string.IsNullOrWhiteSpace(message.MessageId)
                    ? message.MessageId
                    : $"{message.FromMe}|{message.Timestamp}|{message.Text}";
                messages[key!] = message;
            }

            if (!page.HasMore || page.NextOffset <= offset)
                break;

            offset = page.NextOffset;
        }

        return messages.Values.OrderBy(message => message.Timestamp).ToList();
    }

    public Task<bool> SendContactCardAsync(
        string phoneNumber,
        string fullName,
        string contactPhoneNumber,
        string? organization = null,
        string? email = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedNumber = NormalizeRecipientNumber(phoneNumber);
        var contactNumber = NormalizeRecipientNumber(contactPhoneNumber);
        var request = new EvolutionContactRequest(
            normalizedNumber,
            [new EvolutionContact(fullName, contactNumber, contactNumber, organization, email)]);

        return SendAcceptedAsync("sendContact", normalizedNumber, request, cancellationToken);
    }

    public Task<bool> RejectCallAsync(
        string phoneNumber,
        string? callId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Evolution call rejection is unsupported: no verified reject-call endpoint is configured. No request was sent for {Phone}",
            NormalizeRecipientNumber(phoneNumber));
        return Task.FromResult(false);
    }

    public Task<bool> SendReactionAsync(
        string phoneNumber,
        string messageId,
        string emoji = "👀",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            _logger.LogWarning("Evolution sendReaction skipped because message ID is empty");
            return Task.FromResult(false);
        }

        var normalizedNumber = NormalizeRecipientNumber(phoneNumber);
        var request = new EvolutionReactionRequest(
            new EvolutionMessageKey(messageId, $"{normalizedNumber}@s.whatsapp.net", false),
            emoji);

        return SendAcceptedAsync("sendReaction", normalizedNumber, request, cancellationToken);
    }

    public async Task<bool> MarkAsReadAsync(
        string phoneNumber,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            _logger.LogWarning("Evolution markMessageAsRead skipped because message ID is empty");
            return false;
        }

        var normalizedNumber = NormalizeRecipientNumber(phoneNumber);
        var payload = new EvolutionReadRequest(
            [new EvolutionMessageKey(messageId, $"{normalizedNumber}@s.whatsapp.net", false)]);

        try
        {
            using var request = CreateRequest(HttpMethod.Post, "chat/markMessageAsRead", payload);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode != System.Net.HttpStatusCode.Created)
            {
                _logger.LogWarning(
                    "Evolution markMessageAsRead failed for {Phone} with HTTP {StatusCode}",
                    normalizedNumber,
                    (int)response.StatusCode);
                return false;
            }

            if (!TryGetReadSuccess(body))
            {
                _logger.LogWarning(
                    "Evolution markMessageAsRead returned HTTP 201 without RC2 read success for {Phone}",
                    normalizedNumber);
                return false;
            }

            _logger.LogInformation("Evolution markMessageAsRead succeeded for {Phone}", normalizedNumber);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Evolution markMessageAsRead request failed for {Phone}", normalizedNumber);
            return false;
        }
    }

    private async Task<bool> SendAcceptedAsync(
        string operation,
        string phoneNumber,
        object payload,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Post, $"message/{operation}", payload);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Evolution {Operation} failed for {Phone} with HTTP {StatusCode}",
                    operation,
                    phoneNumber,
                    (int)response.StatusCode);
                return false;
            }

            if (!TryGetAcceptedMessageId(body, out _))
            {
                _logger.LogWarning(
                    "Evolution {Operation} returned HTTP {StatusCode} without an accepted message key for {Phone}",
                    operation,
                    (int)response.StatusCode,
                    phoneNumber);
                return false;
            }

            if (operation is "sendButtons" or "sendList")
            {
                _logger.LogInformation(
                    "Evolution {Operation} accepted submission for {Phone}; API acceptance does not confirm WhatsApp delivery",
                    operation,
                    phoneNumber);
            }
            else
            {
                _logger.LogInformation("Evolution {Operation} accepted submission for {Phone}", operation, phoneNumber);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Evolution {Operation} request failed for {Phone}", operation, phoneNumber);
            return false;
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string operation, object payload)
    {
        var instance = Uri.EscapeDataString(_instanceName);
        var request = new HttpRequestMessage(method, $"{operation}/{instance}")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        request.Headers.Add("apikey", _apiKey);

        // Evolution v2.4.0-rc2 applies its CORS policy to server-to-server requests too.
        // Use this client's origin, which staging explicitly allows.
        if (_httpClient.BaseAddress is { IsAbsoluteUri: true } baseAddress)
            request.Headers.TryAddWithoutValidation("Origin", baseAddress.GetLeftPart(UriPartial.Authority));

        return request;
    }

    private bool TryGetAcceptedMessageId(string responseBody, out string? messageId)
    {
        messageId = null;
        try
        {
            var response = JsonSerializer.Deserialize<EvolutionApiResponse>(responseBody, JsonOptions);
            if (response is null ||
                response.Success is false ||
                string.IsNullOrWhiteSpace(response.Key?.Id) ||
                IsErrorStatus(response.Status))
                return false;

            messageId = response.Key.Id;
            return true;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Evolution returned an unreadable send response");
            return false;
        }
    }

    private bool TryGetReadSuccess(string responseBody)
    {
        try
        {
            var response = JsonSerializer.Deserialize<EvolutionReadResponse>(responseBody, JsonOptions);
            return response is not null &&
                   string.Equals(response.Message, "Read messages", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(response.Read, "success", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Evolution returned an unreadable markMessageAsRead response");
            return false;
        }
    }

    private static bool IsErrorStatus(JsonElement status) =>
        status.ValueKind == JsonValueKind.String &&
        string.Equals(status.GetString(), "ERROR", StringComparison.OrdinalIgnoreCase);

    private static WhatsAppHistoryPage EmptyHistoryPage(int limit, int offset) => new()
    {
        Limit = limit,
        Offset = offset,
        NextOffset = offset,
        HasMore = false
    };

    private static string NormalizeRecipientNumber(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
            return input;
        if (digits.Length == 9)
            return "34" + digits;

        return digits;
    }

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private sealed record EvolutionTextRequest(
        [property: JsonPropertyName("number")] string Number,
        [property: JsonPropertyName("text")] string Text);

    private sealed record EvolutionButton(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("displayText")] string DisplayText,
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("url")] string? Url);

    private sealed record EvolutionButtonsRequest(
        [property: JsonPropertyName("number")] string Number,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("footer")] string Footer,
        [property: JsonPropertyName("buttons")] List<EvolutionButton> Buttons);

    private sealed record EvolutionListRow(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("rowId")] string RowId);

    private sealed record EvolutionListSection(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("rows")] List<EvolutionListRow> Rows);

    private sealed record EvolutionListRequest(
        [property: JsonPropertyName("number")] string Number,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("footerText")] string FooterText,
        [property: JsonPropertyName("buttonText")] string ButtonText,
        [property: JsonPropertyName("sections")] List<EvolutionListSection> Sections);

    private sealed record EvolutionContact(
        [property: JsonPropertyName("fullName")] string FullName,
        [property: JsonPropertyName("wuid")] string Wuid,
        [property: JsonPropertyName("phoneNumber")] string PhoneNumber,
        [property: JsonPropertyName("organization")] string? Organization,
        [property: JsonPropertyName("email")] string? Email);

    private sealed record EvolutionContactRequest(
        [property: JsonPropertyName("number")] string Number,
        [property: JsonPropertyName("contact")] List<EvolutionContact> Contact);

    private sealed record EvolutionMessageKey(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("remoteJid")] string RemoteJid,
        [property: JsonPropertyName("fromMe")] bool FromMe);

    private sealed record EvolutionReactionRequest(
        [property: JsonPropertyName("key")] EvolutionMessageKey Key,
        [property: JsonPropertyName("reaction")] string Reaction);

    private sealed record EvolutionReadRequest(
        [property: JsonPropertyName("readMessages")] List<EvolutionMessageKey> ReadMessages);

    private sealed record EvolutionHistoryKey(
        [property: JsonPropertyName("remoteJid")] string RemoteJid);

    private sealed record EvolutionHistoryWhere(
        [property: JsonPropertyName("key")] EvolutionHistoryKey Key);

    private sealed record EvolutionHistoryRequest(
        [property: JsonPropertyName("where")] EvolutionHistoryWhere Where,
        [property: JsonPropertyName("page")] int Page,
        [property: JsonPropertyName("offset")] int Offset);

    private sealed record EvolutionApiResponse
    {
        [JsonPropertyName("key")]
        public EvolutionResponseKey? Key { get; init; }

        [JsonPropertyName("success")]
        public bool? Success { get; init; }

        [JsonPropertyName("status")]
        public JsonElement Status { get; init; }
    }

    private sealed record EvolutionResponseKey
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }
    }

    private sealed record EvolutionReadResponse
    {
        [JsonPropertyName("message")]
        public string? Message { get; init; }

        [JsonPropertyName("read")]
        public string? Read { get; init; }
    }

    private sealed record EvolutionHistoryResponse
    {
        [JsonPropertyName("messages")]
        public EvolutionHistoryMessages? Messages { get; init; }
    }

    private sealed record EvolutionHistoryMessages
    {
        [JsonPropertyName("pages")]
        public int Pages { get; init; }

        [JsonPropertyName("currentPage")]
        public int CurrentPage { get; init; }

        [JsonPropertyName("records")]
        public List<EvolutionHistoryRecord>? Records { get; init; }
    }

    private sealed record EvolutionHistoryRecord
    {
        [JsonPropertyName("key")]
        public EvolutionHistoryKeyResponse? Key { get; init; }

        [JsonPropertyName("pushName")]
        public string? PushName { get; init; }

        [JsonPropertyName("message")]
        public JsonElement Message { get; init; }

        [JsonPropertyName("messageTimestamp")]
        public long MessageTimestamp { get; init; }
    }

    private sealed record EvolutionHistoryKeyResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("fromMe")]
        public bool? FromMe { get; init; }
    }
}
