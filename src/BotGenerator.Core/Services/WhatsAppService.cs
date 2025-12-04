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
    private readonly string _apiUrl;
    private readonly string _token;
    private readonly ILogger<WhatsAppService> _logger;

    public WhatsAppService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<WhatsAppService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _apiUrl = configuration["WhatsApp:ApiUrl"]
            ?? throw new InvalidOperationException("WhatsApp:ApiUrl not configured");
        _token = configuration["WhatsApp:Token"]
            ?? throw new InvalidOperationException("WhatsApp:Token not configured");
    }

    public async Task<bool> SendTextAsync(
        string phoneNumber,
        string text,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending text message to {Phone}: {Preview}",
            phoneNumber,
            text.Length > 50 ? text[..50] + "..." : text);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/send/text");

        request.Headers.Add("token", _token);
        request.Content = JsonContent.Create(new
        {
            number = phoneNumber,
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
        _logger.LogInformation(
            "Sending buttons message to {Phone} with {Count} buttons",
            phoneNumber, buttons.Count);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_apiUrl}/send/menu");

        request.Headers.Add("token", _token);

        // UAZAPI button format
        var choices = buttons.Select(b =>
            $"{b.Text}|{b.Id}|{b.Description ?? b.Text}").ToList();

        request.Content = JsonContent.Create(new
        {
            number = phoneNumber,
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
        _logger.LogInformation(
            "Sending menu message to {Phone}",
            phoneNumber);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_apiUrl}/send/menu");

        request.Headers.Add("token", _token);

        // Build menu structure
        var menuSections = sections.Select(s => new
        {
            title = s.Title,
            rows = s.Rows.Select(r => new
            {
                id = r.Id,
                title = r.Title,
                description = r.Description ?? ""
            }).ToList()
        }).ToList();

        request.Content = JsonContent.Create(new
        {
            number = phoneNumber,
            type = "list",
            text = text,
            buttonText = buttonText,
            sections = menuSections
        });

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
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
        _logger.LogDebug("Getting history for {Phone}, limit: {Limit}", phoneNumber, limit);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_apiUrl}/message/find");

        request.Headers.Add("token", _token);
        request.Content = JsonContent.Create(new
        {
            chatid = $"{phoneNumber}@s.whatsapp.net",
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
}
