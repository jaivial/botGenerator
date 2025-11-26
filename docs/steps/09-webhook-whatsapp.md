# Step 09: Webhook & WhatsApp Integration

In this step, we'll implement the webhook controller and WhatsApp service to receive and send messages.

## 9.1 WhatsApp Service Interface

### src/BotGenerator.Core/Services/IWhatsAppService.cs

```csharp
namespace BotGenerator.Core.Services;

/// <summary>
/// Service for sending WhatsApp messages.
/// </summary>
public interface IWhatsAppService
{
    /// <summary>
    /// Sends a text message.
    /// </summary>
    Task<bool> SendTextAsync(
        string phoneNumber,
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message with buttons.
    /// </summary>
    Task<bool> SendButtonsAsync(
        string phoneNumber,
        string text,
        string footer,
        List<ButtonOption> buttons,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message with a menu/list.
    /// </summary>
    Task<bool> SendMenuAsync(
        string phoneNumber,
        string text,
        string buttonText,
        List<MenuSection> sections,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets conversation history from WhatsApp.
    /// </summary>
    Task<List<WhatsAppHistoryMessage>> GetHistoryAsync(
        string phoneNumber,
        int limit = 20,
        CancellationToken cancellationToken = default);
}

public record ButtonOption(string Id, string Text, string? Description = null);

public record MenuSection(string Title, List<MenuRow> Rows);

public record MenuRow(string Id, string Title, string? Description = null);

public record WhatsAppHistoryMessage
{
    public string Text { get; init; } = "";
    public bool FromMe { get; init; }
    public long Timestamp { get; init; }
    public string? SenderName { get; init; }
    public string? MessageId { get; init; }
}
```

## 9.2 WhatsApp Service Implementation

### src/BotGenerator.Core/Services/WhatsAppService.cs

```csharp
using System.Net.Http.Json;

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
            $"{_apiUrl}/send/text");

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
```

## 9.3 Webhook Controller

### src/BotGenerator.Api/Controllers/WebhookController.cs

```csharp
using System.Text.Json;
using BotGenerator.Core.Agents;
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace BotGenerator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly MainConversationAgent _mainAgent;
    private readonly IIntentRouterService _intentRouter;
    private readonly IConversationHistoryService _historyService;
    private readonly IWhatsAppService _whatsApp;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        MainConversationAgent mainAgent,
        IIntentRouterService intentRouter,
        IConversationHistoryService historyService,
        IWhatsAppService whatsApp,
        ILogger<WebhookController> logger)
    {
        _mainAgent = mainAgent;
        _intentRouter = intentRouter;
        _historyService = historyService;
        _whatsApp = whatsApp;
        _logger = logger;
    }

    /// <summary>
    /// Health check endpoint.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0"
        });
    }

    /// <summary>
    /// WhatsApp webhook endpoint.
    /// Receives incoming messages from WhatsApp via UAZAPI.
    /// </summary>
    [HttpPost("whatsapp-webhook")]
    public async Task<IActionResult> HandleWhatsAppWebhook(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        try
        {
            // Log raw payload for debugging
            _logger.LogDebug("Received webhook: {Body}", body.ToString());

            // 1. Extract message data
            var message = ExtractMessage(body);

            // Ignore our own messages
            if (message.FromMe)
            {
                _logger.LogDebug("Ignoring own message");
                return Ok();
            }

            // Ignore empty messages or media (for now)
            if (string.IsNullOrWhiteSpace(message.MessageText))
            {
                _logger.LogDebug("Ignoring empty/media message");
                return Ok();
            }

            _logger.LogInformation(
                "Processing message from {Sender} ({Phone}): {Text}",
                message.PushName,
                message.SenderNumber,
                message.MessageText.Length > 100
                    ? message.MessageText[..100] + "..."
                    : message.MessageText);

            // 2. Get conversation history
            var history = await _historyService.GetHistoryAsync(
                message.SenderNumber, cancellationToken);

            // 3. Extract conversation state
            var state = _historyService.ExtractState(history);

            // 4. Process with main agent
            var agentResponse = await _mainAgent.ProcessAsync(
                message, state, history, cancellationToken);

            // 5. Route based on intent
            var finalResponse = await _intentRouter.RouteAsync(
                agentResponse, message, state, cancellationToken);

            // 6. Send response
            var sent = await _whatsApp.SendTextAsync(
                message.SenderNumber,
                finalResponse.AiResponse,
                cancellationToken);

            if (!sent)
            {
                _logger.LogWarning(
                    "Failed to send response to {Phone}",
                    message.SenderNumber);
            }

            return Ok(new { processed = true });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON in webhook payload");
            return BadRequest(new { error = "Invalid JSON" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook");

            // Try to send error message to user
            try
            {
                if (body.TryGetProperty("message", out var msgProp) &&
                    msgProp.TryGetProperty("chatid", out var chatId))
                {
                    var phone = chatId.GetString()?.Replace("@s.whatsapp.net", "") ?? "";
                    if (!string.IsNullOrEmpty(phone))
                    {
                        await _whatsApp.SendTextAsync(
                            phone,
                            "Disculpa, hubo un error. Por favor, inténtalo de nuevo.");
                    }
                }
            }
            catch { /* Ignore errors in error handling */ }

            return StatusCode(500, new { error = "Internal error" });
        }
    }

    /// <summary>
    /// Extracts WhatsAppMessage from the webhook JSON payload.
    /// </summary>
    private WhatsAppMessage ExtractMessage(JsonElement body)
    {
        var messageBody = body.GetProperty("message");
        var chatId = messageBody.GetProperty("chatid").GetString() ?? "";
        var senderNumber = chatId.Replace("@s.whatsapp.net", "");

        // Extract message text
        var messageText = "";

        // Regular text message
        if (messageBody.TryGetProperty("text", out var textProp))
        {
            messageText = textProp.GetString() ?? "";
        }

        // Button response
        if (messageBody.TryGetProperty("vote", out var voteProp))
        {
            messageText = voteProp.GetString() ?? "";
        }

        // List response
        if (messageBody.TryGetProperty("content", out var contentProp) &&
            contentProp.TryGetProperty("Response", out var responseProp) &&
            responseProp.TryGetProperty("SelectedDisplayText", out var selectedProp))
        {
            messageText = selectedProp.GetString() ?? "";
        }

        // Determine message type
        var messageType = "text";
        if (messageBody.TryGetProperty("messageType", out var typeProp))
        {
            messageType = typeProp.GetString() ?? "text";
        }

        var isButtonResponse = messageType == "ButtonsResponseMessage" ||
                               messageType == "ListResponseMessage";

        // Get push name
        var pushName = "Cliente";
        if (body.TryGetProperty("chat", out var chatProp) &&
            chatProp.TryGetProperty("name", out var nameProp))
        {
            pushName = nameProp.GetString() ?? "Cliente";
        }

        // Get fromMe
        var fromMe = messageBody.TryGetProperty("fromMe", out var fromMeProp) &&
                    fromMeProp.GetBoolean();

        // Get timestamp
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (messageBody.TryGetProperty("messageTimestamp", out var tsProp))
        {
            timestamp = tsProp.GetInt64();
        }

        // Get button ID if present
        string? buttonId = null;
        if (messageBody.TryGetProperty("buttonOrListid", out var buttonIdProp))
        {
            buttonId = buttonIdProp.GetString();
        }

        return new WhatsAppMessage
        {
            SenderNumber = senderNumber,
            MessageText = messageText,
            MessageType = messageType,
            PushName = pushName,
            FromMe = fromMe,
            Timestamp = timestamp,
            IsButtonResponse = isButtonResponse,
            ButtonId = buttonId,
            ButtonText = isButtonResponse ? messageText : null,
            IsMediaMessage = messageType is "image" or "audio" or "video" or "document",
            RawPayload = body.ToString()
        };
    }
}
```

## 9.4 Register Services

### src/BotGenerator.Api/Program.cs (complete)

```csharp
using BotGenerator.Core.Agents;
using BotGenerator.Core.Handlers;
using BotGenerator.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// ========== Add Core Services ==========
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ========== HTTP Clients ==========
builder.Services.AddHttpClient<IGeminiService, GeminiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddHttpClient<IWhatsAppService, WhatsAppService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ========== Singleton Services ==========
builder.Services.AddSingleton<IPromptLoaderService, PromptLoaderService>();
builder.Services.AddSingleton<IContextBuilderService, ContextBuilderService>();
builder.Services.AddSingleton<IConversationHistoryService, ConversationHistoryService>();

// ========== Scoped Services ==========
builder.Services.AddScoped<IIntentRouterService, IntentRouterService>();

// ========== Agents ==========
builder.Services.AddScoped<MainConversationAgent>();
builder.Services.AddScoped<RiceValidatorAgent>();
builder.Services.AddScoped<DateParserAgent>();
builder.Services.AddScoped<AvailabilityCheckerAgent>();

// ========== Handlers ==========
builder.Services.AddScoped<BookingHandler>();
builder.Services.AddScoped<CancellationHandler>();
builder.Services.AddScoped<ModificationHandler>();

// ========== Logging ==========
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

var app = builder.Build();

// ========== Configure Pipeline ==========
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

## 9.5 Testing the Webhook

### Using curl:

```bash
# Test health endpoint
curl http://localhost:5000/api/webhook/health

# Simulate a WhatsApp message
curl -X POST http://localhost:5000/api/webhook/whatsapp-webhook \
  -H "Content-Type: application/json" \
  -d '{
    "message": {
      "chatid": "34612345678@s.whatsapp.net",
      "text": "Hola, quiero reservar para el sábado",
      "fromMe": false,
      "messageTimestamp": 1732536000
    },
    "chat": {
      "name": "Juan García"
    }
  }'
```

## 9.6 Exposing for WhatsApp (ngrok)

For local development, use ngrok to expose your webhook:

```bash
# Install ngrok
# Then run:
ngrok http 5000

# Configure the ngrok URL in your WhatsApp API provider
# Example: https://abc123.ngrok.io/api/webhook/whatsapp-webhook
```

## Summary

In this step, we:

1. Created `IWhatsAppService` for sending messages
2. Implemented `WhatsAppService` using UAZAPI
3. Created the `WebhookController` with:
   - Health check endpoint
   - WhatsApp webhook endpoint
   - Message extraction logic
4. Registered all services in DI
5. Created the complete `Program.cs`

## Next Step

Continue to [Step 10: Prompt Templates](./10-prompt-templates.md) where we'll create all the prompt files.
