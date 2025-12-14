using System.Net.Http.Json;
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
        var normalizedNumber = NormalizeChatIdNumber(phoneNumber);
        _logger.LogDebug("Getting history for {Phone}, limit: {Limit}", normalizedNumber, limit);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/message/find?token={Uri.EscapeDataString(_token)}");

        request.Headers.Add("token", _token);
        request.Content = JsonContent.Create(new
        {
            chatid = $"{normalizedNumber}@s.whatsapp.net",
            limit = limit
        });

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get history for {Phone}", phoneNumber);
                return new List<WhatsAppHistoryMessage>();
            }

            var result = await response.Content.ReadFromJsonAsync<HistoryResponse>(
                cancellationToken: cancellationToken);

            return result?.Messages?.Select(m => new WhatsAppHistoryMessage
            {
                Text = m.Text ?? "",
                FromMe = m.FromMe,
                Timestamp = m.MessageTimestamp,
                SenderName = m.SenderName,
                MessageId = m.MessageId
            }).ToList() ?? new List<WhatsAppHistoryMessage>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting history for {Phone}", phoneNumber);
            return new List<WhatsAppHistoryMessage>();
        }
    }

    private class HistoryResponse
    {
        public List<HistoryMessage>? Messages { get; set; }
    }

    private class HistoryMessage
    {
        public string? Text { get; set; }
        public bool FromMe { get; set; }
        public long MessageTimestamp { get; set; }
        public string? SenderName { get; set; }
        public string? MessageId { get; set; }
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
}
