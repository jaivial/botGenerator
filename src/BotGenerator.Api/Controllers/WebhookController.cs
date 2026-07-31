using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace BotGenerator.Api.Controllers;

/// <summary>
/// Simplified webhook controller using single AI agent with tool calls.
/// Replaces the legacy multi-node pipeline approach.
/// </summary>
[ApiController]
[Route("api/bot")]
public class WebhookController : ControllerBase
{
    private const long EvolutionWebhookMaxBodyBytes = 64 * 1024;

    private static readonly string[] ManagementPhones =
    {
        "34692747052",
        "34638857294",
        "34686969914"
    };

    private readonly AgentOrchestrator _agentOrchestrator;
    private readonly IBookingRepository _bookingRepository;
    private readonly IPendingBookingStore _pendingBookingStore;
    private readonly ICallAutoReplyStore _callAutoReplyStore;
    private readonly IWhatsAppService _whatsApp;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<WebhookController> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly IRestaurantConfigRepository _restaurantConfigRepo;
    private readonly IEvolutionWebhookDedupe _evolutionWebhookDedupe;

    public WebhookController(
        AgentOrchestrator agentOrchestrator,
        IBookingRepository bookingRepository,
        IPendingBookingStore pendingBookingStore,
        ICallAutoReplyStore callAutoReplyStore,
        IWhatsAppService whatsApp,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<WebhookController> logger,
        IMemoryCache memoryCache,
        IRestaurantConfigRepository restaurantConfigRepo,
        IEvolutionWebhookDedupe evolutionWebhookDedupe)
    {
        _agentOrchestrator = agentOrchestrator;
        _bookingRepository = bookingRepository;
        _pendingBookingStore = pendingBookingStore;
        _callAutoReplyStore = callAutoReplyStore;
        _whatsApp = whatsApp;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
        _memoryCache = memoryCache;
        _restaurantConfigRepo = restaurantConfigRepo;
        _evolutionWebhookDedupe = evolutionWebhookDedupe;
    }

    [HttpGet("health")]
    public IActionResult Health() =>
        Ok(new { status = "healthy", timestamp = DateTime.UtcNow, version = "3.0.0-simplified-agent" });

    [HttpPost("test/clear-state")]
    public async Task<IActionResult> ClearTestState(
        [FromQuery] string? phone = null,
        CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        phone ??= "34692747052";
        var normalized = new string(phone.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(normalized))
            return BadRequest(new { error = "Invalid phone" });

        _pendingBookingStore.Clear(normalized);

        return Ok(new { cleared = true, phone = normalized });
    }

    /// <summary>
    /// Main webhook endpoint - uses single AI agent with tool calls.
    /// The AI handles all conversation flow through tool calls.
    /// </summary>
    [HttpPost("whatsapp-webhook")]
    public async Task<IActionResult> HandleWebhook(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(_configuration["WhatsApp:Provider"], "uazapi", StringComparison.OrdinalIgnoreCase))
            return NotFound();

        try
        {
            _logger.LogDebug("Received webhook: {Body}", body.ToString());

            // Handle call events
            if (body.TryGetProperty("EventType", out var eventTypeProp))
            {
                var eventType = eventTypeProp.GetString();
                if (!string.IsNullOrWhiteSpace(eventType) && eventType != "messages")
                {
                    if (IsCallEventType(eventType))
                        return await HandleCallWebhookAsync(body, cancellationToken);
                    _logger.LogDebug("Ignoring non-message event: {EventType}", eventType);
                    return Ok();
                }
            }

            if (!body.TryGetProperty("message", out _) && body.TryGetProperty("call", out _))
                return await HandleCallWebhookAsync(body, cancellationToken);

            if (!body.TryGetProperty("message", out _))
            {
                _logger.LogDebug("No 'message' property in payload, ignoring");
                return Ok();
            }

            // Extract message
            var message = ExtractMessage(body);

            if (message.FromMe)
                return Ok();

            // Unsupported media
            if (string.IsNullOrWhiteSpace(message.MessageText))
            {
                if (IsUnsupportedMessageType(message.MessageType))
                {
                    await _whatsApp.SendTextAsync(
                        message.SenderNumber,
                        "Ahora mismo solo puedo gestionar mensajes de texto. ¿Me lo puedes escribir por aquí?",
                        cancellationToken);
                    return Ok(new { processed = true, unsupportedContent = true });
                }
                return Ok();
            }

            _logger.LogInformation(
                "[AGENT] Processing message from {Sender} ({Phone}): {Text}",
                message.PushName, message.SenderNumber,
                message.MessageText.Length > 100
                    ? message.MessageText[..100] + "..."
                    : message.MessageText);

            // Dedup
            if (!string.IsNullOrWhiteSpace(message.MessageId))
            {
                var dedupeKey = $"agent:wa:{message.SenderNumber}:{message.MessageId}";
                if (_memoryCache.TryGetValue(dedupeKey, out _))
                {
                    _logger.LogInformation("Duplicate webhook ignored for messageId={MessageId}", message.MessageId);
                    return Ok(new { processed = true, duplicate = true });
                }
                _memoryCache.Set(dedupeKey, 1,
                    new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) });
            }

            // Acknowledge the user's message by sending a reaction
            if (!string.IsNullOrWhiteSpace(message.MessageId))
            {
                try
                {
                    await _whatsApp.SendReactionAsync(
                        message.SenderNumber,
                        message.MessageId,
                        "👀",
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send reaction for message {MessageId}", message.MessageId);
                }
            }

            // Get restaurant info for the agent
            var restaurantId = GetRestaurantId(message.SenderNumber);
            var restaurantConfig = await _restaurantConfigRepo.GetBySlugAsync(restaurantId, cancellationToken);

            var restaurantInfo = restaurantConfig != null
                ? $"Teléfono: {restaurantConfig.ContactPhone}\nEmail: {restaurantConfig.ContactEmail}\nDirección: {restaurantConfig.Location}\nWeb: {restaurantConfig.WebsiteUrl}"
                : "Información del restaurante no disponible.";

            // Run the AI Agent with tool calls
            var agentResult = await _agentOrchestrator.ProcessAsync(
                message.SenderNumber,
                message.MessageText,
                message.PushName,
                FormatSpanishDate(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Central European Standard Time")),
                restaurantInfo,
                cancellationToken);

            _logger.LogInformation(
                "[AGENT] Result for {Phone}: Success={Success}, MessagesSent={MsgCount}, ToolCalls={Tools}, Iterations={Iterations}",
                message.SenderNumber,
                agentResult.Success,
                agentResult.SentMessages.Count,
                string.Join(", ", agentResult.ToolCalls),
                agentResult.Iterations);

            if (!agentResult.Success && agentResult.Error != null)
            {
                _logger.LogWarning("[AGENT] Error for {Phone}: {Error}", message.SenderNumber, agentResult.Error);
            }

            return Ok(new
            {
                processed = true,
                agent = true,
                success = agentResult.Success,
                messagesSent = agentResult.SentMessages.Count,
                toolCalls = agentResult.ToolCalls,
                iterations = agentResult.Iterations,
                error = agentResult.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook");
            return Ok(new { processed = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Authenticated Evolution API v2.4.0-rc2 inbound webhook. It intentionally does not share
    /// UAZAPI parsing or in-memory dedupe, which preserves existing production behavior.
    /// </summary>
    [HttpPost("evolution-webhook")]
    [RequestSizeLimit(EvolutionWebhookMaxBodyBytes)]
    public async Task<IActionResult> HandleEvolutionWebhook(
        [FromHeader(Name = "X-Evolution-Webhook-Secret")] string? secret,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        if (!IsEvolutionProvider())
            return NotFound();

        var configuredSecret = _configuration["WhatsApp:Evolution:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(configuredSecret) || !SecretsMatch(configuredSecret, secret ?? string.Empty))
        {
            _logger.LogWarning("Rejected Evolution webhook with invalid secret");
            return Unauthorized();
        }

        if (body.ValueKind != JsonValueKind.Object)
        {
            _logger.LogWarning("Rejected Evolution webhook with invalid JSON root");
            return BadRequest();
        }

        var expectedInstance = _configuration["WhatsApp:Evolution:InstanceName"];
        if (!TryGetString(body, "instance", out var instanceName) ||
            !string.Equals(instanceName, expectedInstance, StringComparison.Ordinal))
        {
            _logger.LogWarning("Rejected Evolution webhook for an unexpected instance");
            return BadRequest();
        }

        if (!TryGetString(body, "event", out var eventName))
        {
            _logger.LogDebug("Ignoring Evolution webhook without an event name");
            return Ok(new { processed = false, ignored = true });
        }

        var normalizedEventName = eventName!.Replace('.', '_').Replace('-', '_');

        if (string.Equals(normalizedEventName, "CALL", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Evolution API v2.4.0-rc2 has no call-reject endpoint; attempting configured call auto-reply only");
            return await HandleCallWebhookAsync(body, cancellationToken, rejectCall: false);
        }

        if (!string.Equals(normalizedEventName, "MESSAGES_UPSERT", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Ignoring unsupported Evolution webhook event {Event}", eventName);
            return Ok(new { processed = false, ignored = true });
        }

        if (!body.TryGetProperty("data", out var data) ||
            !EvolutionMessageParser.TryParseInboundMessage(data, out var message))
        {
            _logger.LogDebug("Ignoring Evolution webhook with an invalid message envelope");
            return Ok(new { processed = false, ignored = true });
        }

        if (message.FromMe)
            return Ok(new { processed = false, ignored = true });

        var claim = await _evolutionWebhookDedupe.TryClaimAsync(
            expectedInstance!,
            message.MessageId!,
            cancellationToken);
        if (claim.State == EvolutionWebhookDedupeState.Completed)
        {
            _logger.LogInformation("Ignored duplicate Evolution message {MessageId}", message.MessageId);
            return Ok(new { processed = true, duplicate = true });
        }

        if (claim.State is EvolutionWebhookDedupeState.Processing or EvolutionWebhookDedupeState.Unavailable)
        {
            // Ask Evolution to retry: another handler may still fail or Redis may be unavailable.
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { processed = false, retry = true });
        }

        try
        {
            if (string.IsNullOrWhiteSpace(message.MessageText))
            {
                if (message.IsMediaMessage)
                {
                    var accepted = await _whatsApp.SendTextAsync(
                        message.SenderNumber,
                        "Ahora mismo solo puedo gestionar mensajes de texto. ¿Me lo puedes escribir por aquí?",
                        cancellationToken);
                    if (!accepted)
                        throw new InvalidOperationException("Evolution did not accept the unsupported-content response.");

                    if (!await _evolutionWebhookDedupe.CompleteAsync(claim, cancellationToken))
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, new { processed = false, retry = true });
                    return Ok(new { processed = true, unsupportedContent = true });
                }

                if (!await _evolutionWebhookDedupe.CompleteAsync(claim, cancellationToken))
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new { processed = false, retry = true });
                return Ok(new { processed = false, ignored = true });
            }

            _logger.LogInformation(
                "[EVOLUTION] Processing inbound message {MessageId} from {Phone}",
                message.MessageId,
                message.SenderNumber);

            try
            {
                await _whatsApp.SendReactionAsync(
                    message.SenderNumber,
                    message.MessageId!,
                    "👀",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Evolution reaction failed for message {MessageId}", message.MessageId);
            }

            var restaurantId = GetRestaurantId(message.SenderNumber);
            var restaurantConfig = await _restaurantConfigRepo.GetBySlugAsync(restaurantId, cancellationToken);
            var restaurantInfo = restaurantConfig != null
                ? $"Teléfono: {restaurantConfig.ContactPhone}\nEmail: {restaurantConfig.ContactEmail}\nDirección: {restaurantConfig.Location}\nWeb: {restaurantConfig.WebsiteUrl}"
                : "Información del restaurante no disponible.";

            var agentResult = await _agentOrchestrator.ProcessAsync(
                message.SenderNumber,
                message.MessageText,
                message.PushName,
                FormatSpanishDate(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Central European Standard Time")),
                restaurantInfo,
                cancellationToken);

            _logger.LogInformation(
                "[EVOLUTION] Result for {Phone}: Success={Success}, MessagesSent={MsgCount}, ToolCalls={Tools}, Iterations={Iterations}",
                message.SenderNumber,
                agentResult.Success,
                agentResult.SentMessages.Count,
                string.Join(", ", agentResult.ToolCalls),
                agentResult.Iterations);

            if (!agentResult.Success && agentResult.Error != null)
                _logger.LogWarning("[EVOLUTION] Agent error for {Phone}: {Error}", message.SenderNumber, agentResult.Error);

            if (!agentResult.Success)
                throw new InvalidOperationException("Evolution agent processing did not complete successfully.");

            if (!await _evolutionWebhookDedupe.CompleteAsync(claim, cancellationToken))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { processed = false, retry = true });

            return Ok(new
            {
                processed = true,
                agent = true,
                success = agentResult.Success,
                messagesSent = agentResult.SentMessages.Count,
                toolCalls = agentResult.ToolCalls,
                iterations = agentResult.Iterations,
                error = agentResult.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Evolution webhook");
            await _evolutionWebhookDedupe.ReleaseAsync(claim, CancellationToken.None);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { processed = false, retry = true });
        }
    }

    // === CALL HANDLING ===

    private async Task<IActionResult> HandleCallWebhookAsync(
        JsonElement body,
        CancellationToken ct,
        bool rejectCall = true)
    {
        var (phone, callId) = ExtractCallInfo(body);
        if (string.IsNullOrWhiteSpace(phone))
        {
            _logger.LogDebug("Call event received but could not extract phone, ignoring");
            return Ok();
        }

        if (rejectCall)
            await _whatsApp.RejectCallAsync(phone, callId, ct);

        var cooldownMinutes = _configuration.GetValue("WhatsApp:CallAutoReplyCooldownMinutes", 15);
        var shouldReply = _callAutoReplyStore.TryMarkReplied(
            phone,
            TimeSpan.FromMinutes(Math.Max(1, cooldownMinutes)),
            DateTime.UtcNow);

        if (!shouldReply)
        {
            _logger.LogInformation("Skipping call auto-reply due to cooldown for {Phone}", phone);
            return Ok(new { processed = true, call = true, replied = false });
        }

        var text = _configuration["WhatsApp:CallAutoReplyText"]
                   ?? "Hola. Soy el asistente automático de reservas por WhatsApp. Para hablar con el restaurante, por favor llama al +34 638 857 294.";

        await _whatsApp.SendTextAsync(phone, text, ct);
        await _whatsApp.SendContactCardAsync(
            phone,
            fullName: "Gestión Reservas",
            contactPhoneNumber: "+34638857294",
            organization: "Alquería Villa Carmen",
            cancellationToken: ct);

        return Ok(new { processed = true, call = true, replied = true });
    }

    // === MESSAGE EXTRACTION ===

    private WhatsAppMessage ExtractMessage(JsonElement body)
    {
        var messageBody = body.GetProperty("message");
        var chatId = messageBody.GetProperty("chatid").GetString() ?? "";
        var senderNumber = chatId.Replace("@s.whatsapp.net", "");

        var messageText = "";
        if (messageBody.TryGetProperty("text", out var textProp))
            messageText = textProp.GetString() ?? "";

        if (messageBody.TryGetProperty("vote", out var voteProp))
        {
            var vote = voteProp.GetString() ?? "";
            if (!string.IsNullOrWhiteSpace(vote))
                messageText = vote;
        }

        if (messageBody.TryGetProperty("content", out var contentProp) &&
            contentProp.ValueKind == JsonValueKind.Object &&
            contentProp.TryGetProperty("Response", out var responseProp) &&
            responseProp.TryGetProperty("SelectedDisplayText", out var selectedProp))
        {
            var selectedText = selectedProp.GetString() ?? "";
            if (!string.IsNullOrWhiteSpace(selectedText))
                messageText = selectedText;
        }

        var messageType = "text";
        if (messageBody.TryGetProperty("messageType", out var typeProp))
            messageType = typeProp.GetString() ?? "text";
        else if (messageBody.TryGetProperty("type", out var typeProp2))
            messageType = typeProp2.GetString() ?? "text";

        var isButtonResponse = messageType is "ButtonsResponseMessage" or "ListResponseMessage";

        var pushName = "Cliente";
        if (messageBody.TryGetProperty("pushname", out var pushnameProp))
            pushName = pushnameProp.GetString() ?? "Cliente";
        else if (body.TryGetProperty("chat", out var chatProp) &&
                 chatProp.TryGetProperty("name", out var nameProp))
            pushName = nameProp.GetString() ?? "Cliente";

        var fromMe = messageBody.TryGetProperty("fromMe", out var fromMeProp) && fromMeProp.GetBoolean();

        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (messageBody.TryGetProperty("messageTimestamp", out var tsProp))
        {
            if (tsProp.ValueKind == JsonValueKind.Number && tsProp.TryGetInt64(out var tsNumeric))
                timestamp = tsNumeric;
            else if (tsProp.ValueKind == JsonValueKind.String && long.TryParse(tsProp.GetString(), out var tsString))
                timestamp = tsString;
        }
        else if (messageBody.TryGetProperty("timestamp", out var tsProp2))
        {
            if (tsProp2.ValueKind == JsonValueKind.Number && tsProp2.TryGetInt64(out var tsNumeric))
                timestamp = tsNumeric;
            else if (tsProp2.ValueKind == JsonValueKind.String && long.TryParse(tsProp2.GetString(), out var tsString))
                timestamp = tsString;
        }

        string? messageId = null;
        if (messageBody.TryGetProperty("messageid", out var mid1) && mid1.ValueKind == JsonValueKind.String)
            messageId = mid1.GetString();
        else if (messageBody.TryGetProperty("messageId", out var mid2) && mid2.ValueKind == JsonValueKind.String)
            messageId = mid2.GetString();
        else if (messageBody.TryGetProperty("id", out var mid3) && mid3.ValueKind == JsonValueKind.String)
            messageId = mid3.GetString();

        string? buttonId = null;
        if (messageBody.TryGetProperty("buttonOrListid", out var bid))
            buttonId = bid.GetString();

        return new WhatsAppMessage
        {
            SenderNumber = senderNumber,
            MessageText = messageText,
            MessageType = messageType,
            PushName = pushName,
            FromMe = fromMe,
            Timestamp = timestamp,
            MessageId = messageId,
            IsButtonResponse = isButtonResponse,
            ButtonId = buttonId,
            ButtonText = isButtonResponse ? messageText : null,
            IsMediaMessage = messageType is "image" or "audio" or "video" or "document",
            RawPayload = body.ToString()
        };
    }

    // === UTILITY METHODS ===

    private string GetRestaurantId(string senderNumber)
    {
        var mapping = _configuration
            .GetSection("Restaurants:Mapping")
            .GetChildren()
            .ToDictionary(x => x.Key, x => x.Value);

        if (mapping.TryGetValue(senderNumber, out var restaurantId) && !string.IsNullOrWhiteSpace(restaurantId))
            return restaurantId!;

        return _configuration["Restaurants:Default"] ?? "villacarmen";
    }

    private static bool IsCallEventType(string eventType)
        => eventType.Contains("call", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnsupportedMessageType(string? messageType)
    {
        if (string.IsNullOrWhiteSpace(messageType)) return false;
        return messageType is "audio" or "image" or "video" or "document" or "sticker" or "location";
    }

    private static (string Phone, string? CallId) ExtractCallInfo(JsonElement body)
    {
        string? caller = null;
        string? callId = null;

        var containers = new List<JsonElement> { body };
        if (body.TryGetProperty("call", out var callProp) && callProp.ValueKind == JsonValueKind.Object)
            containers.Insert(0, callProp);
        if (body.TryGetProperty("data", out var dataProp))
        {
            if (dataProp.ValueKind == JsonValueKind.Array && dataProp.GetArrayLength() > 0)
                dataProp = dataProp[0];
            if (dataProp.ValueKind == JsonValueKind.Object)
            {
                containers.Insert(0, dataProp);
                if (dataProp.TryGetProperty("call", out var nestedCall) && nestedCall.ValueKind == JsonValueKind.Object)
                    containers.Insert(0, nestedCall);
            }
        }

        if (body.TryGetProperty("message", out var messageProp) && messageProp.ValueKind == JsonValueKind.Object)
            containers.Insert(0, messageProp);

        foreach (var container in containers)
        {
            caller ??= GetFirstString(container, "from", "remoteJid", "chatid", "chatId", "callCreator");
            callId ??= GetFirstString(container, "id", "callId");
        }

        if (string.IsNullOrWhiteSpace(caller) ||
            (caller.Contains('@') &&
             !caller.EndsWith("@s.whatsapp.net", StringComparison.OrdinalIgnoreCase) &&
             !caller.EndsWith("@c.us", StringComparison.OrdinalIgnoreCase)))
        {
            return (string.Empty, callId);
        }

        var phone = new string(caller.Where(char.IsDigit).ToArray());
        if (phone.Length is < 7 or > 15)
            phone = string.Empty;

        return (phone, callId);
    }

    private static string? GetFirstString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }

        return null;
    }

    private bool IsEvolutionProvider() =>
        string.Equals(_configuration["WhatsApp:Provider"], "evolution", StringComparison.OrdinalIgnoreCase);

    private static bool SecretsMatch(string expected, string supplied)
    {
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied ?? string.Empty));
        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        value = null;
        return false;
    }

    private static string FormatSpanishDate(DateTime date)
    {
        var days = new[] { "domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado" };
        var months = new[] { "enero", "febrero", "marzo", "abril", "mayo", "junio",
            "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };

        var dayName = days[(int)date.DayOfWeek];
        dayName = char.ToUpper(dayName[0]) + dayName[1..];
        return $"{dayName}, {date.Day} de {months[date.Month - 1]} de {date.Year}";
    }
}
