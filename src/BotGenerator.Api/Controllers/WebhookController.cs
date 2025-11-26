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
