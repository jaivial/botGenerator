using System.Text.Json;
using BotGenerator.Core.Models;
using BotGenerator.Core.Pipeline;
using BotGenerator.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace BotGenerator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private static readonly string[] ManagementPhones =
    {
        "34692747052",
        "34638857294",
        "34686969914"
    };

    private readonly PipelineOrchestrator _pipeline;
    private readonly IConversationHistoryService _historyService;
    private readonly IContextBuilderService _contextBuilder;
    private readonly IBookingRepository _bookingRepository;
    private readonly IPendingBookingStore _pendingBookingStore;
    private readonly ICallAutoReplyStore _callAutoReplyStore;
    private readonly IWhatsAppService _whatsApp;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<WebhookController> _logger;
    private readonly IConversationVectorStore _vectorStore;
    private readonly IMemoryCache _memoryCache;

    public WebhookController(
        PipelineOrchestrator pipeline,
        IConversationHistoryService historyService,
        IContextBuilderService contextBuilder,
        IBookingRepository bookingRepository,
        IPendingBookingStore pendingBookingStore,
        ICallAutoReplyStore callAutoReplyStore,
        IWhatsAppService whatsApp,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<WebhookController> logger,
        IConversationVectorStore vectorStore,
        IMemoryCache memoryCache)
    {
        _pipeline = pipeline;
        _historyService = historyService;
        _contextBuilder = contextBuilder;
        _bookingRepository = bookingRepository;
        _pendingBookingStore = pendingBookingStore;
        _callAutoReplyStore = callAutoReplyStore;
        _whatsApp = whatsApp;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
        _vectorStore = vectorStore;
        _memoryCache = memoryCache;
    }

    [HttpGet("health")]
    public IActionResult Health() =>
        Ok(new { status = "healthy", timestamp = DateTime.UtcNow, version = "2.0.0-pipeline" });

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

        await _historyService.ClearHistoryAsync(normalized, cancellationToken);
        _pendingBookingStore.Clear(normalized);

        return Ok(new { cleared = true, phone = normalized });
    }

    [HttpPost("whatsapp-webhook")]
    public async Task<IActionResult> HandleWhatsAppWebhook(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
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
                "Processing message from {Sender} ({Phone}): {Text}",
                message.PushName, message.SenderNumber,
                message.MessageText.Length > 100
                    ? message.MessageText[..100] + "..."
                    : message.MessageText);

            // Dedup
            if (!string.IsNullOrWhiteSpace(message.MessageId))
            {
                var dedupeKey = $"webhook:wa:{message.SenderNumber}:{message.MessageId}";
                if (_memoryCache.TryGetValue(dedupeKey, out _))
                {
                    _logger.LogInformation("Duplicate webhook ignored for messageId={MessageId}", message.MessageId);
                    return Ok(new { processed = true, duplicate = true });
                }
                _memoryCache.Set(dedupeKey, 1,
                    new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) });
            }

            // Load context
            var history = await _historyService.GetHistoryAsync(message.SenderNumber, cancellationToken);
            var existingBookings = await _bookingRepository.FindBookingsByPhoneAsync(
                message.SenderNumber, cancellationToken);
            var pendingBooking = _pendingBookingStore.Get(message.SenderNumber);

            var now = DateTime.Now;
            var formattedHistory = _contextBuilder.FormatHistory(history);
            var restaurantId = GetRestaurantId(message.SenderNumber);

            // Build pipeline context
            var pipelineContext = new PipelineContext
            {
                Message = message,
                History = history,
                ExistingBookings = existingBookings,
                PendingBooking = pendingBooking,
                RestaurantId = restaurantId,
                PushName = message.PushName,
                FormattedHistory = formattedHistory,
                TodayES = FormatSpanishDate(now),
                TodayFormatted = now.ToString("dd/MM/yyyy")
            };

            // Run pipeline
            var result = await _pipeline.ProcessAsync(pipelineContext, cancellationToken);

            // Save user message to history
            await _historyService.AddMessageAsync(
                message.SenderNumber,
                ChatMessage.FromUser(message.MessageText, message.PushName, message.MessageId, message.Timestamp),
                cancellationToken);

            // Send response
            if (!string.IsNullOrEmpty(result.ResponseText))
            {
                if (result.Intent == PipelineIntent.ConfirmBooking && result.BookingToCreate != null)
                {
                    // Send confirmation with buttons
                    await SendBookingConfirmationAsync(
                        message.SenderNumber,
                        result.ResponseText,
                        result.CreatedBookingId,
                        cancellationToken);
                }
                else
                {
                    await _whatsApp.SendTextAsync(
                        message.SenderNumber, result.ResponseText, cancellationToken);
                }

                // Save bot response to history
                await _historyService.AddMessageAsync(
                    message.SenderNumber,
                    ChatMessage.FromAssistant(result.ResponseText),
                    cancellationToken);
            }

            // Clear pending booking if needed
            if (result.ShouldClearPending)
                _pendingBookingStore.Clear(message.SenderNumber);

            // Notify management for new bookings
            if (result.ShouldNotifyManagement)
                await NotifyManagementAsync(result, message, cancellationToken);

            return Ok(new { processed = true, intent = result.Intent.ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook");
            return Ok(new { processed = false, error = ex.Message });
        }
    }

    // === CALL HANDLING (unchanged) ===

    private async Task<IActionResult> HandleCallWebhookAsync(JsonElement body, CancellationToken ct)
    {
        var (phone, callId) = ExtractCallInfo(body);
        if (string.IsNullOrWhiteSpace(phone))
        {
            _logger.LogDebug("Call event received but could not extract phone, ignoring");
            return Ok();
        }

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

    // === NOTIFICATION HELPERS ===

    private async Task SendBookingConfirmationAsync(
        string phone, string confirmationText, long? bookingId,
        CancellationToken ct)
    {
        await _whatsApp.SendTextAsync(phone, confirmationText, ct);

        var buttons = new List<LinkButtonOption>();

        if (bookingId.HasValue)
        {
            var baseUrl = _configuration["ExternalBooking:BaseUrl"]
                          ?? "https://alqueriavillacarmen.com";
            buttons.Add(new LinkButtonOption(
                "Ver condiciones",
                $"{baseUrl}/conditions.php?id={bookingId.Value}"));
            buttons.Add(new LinkButtonOption(
                "Cancelar reserva",
                $"{baseUrl}/cancel.php?id={bookingId.Value}"));
        }

        if (buttons.Count > 0)
        {
            await _whatsApp.SendLinkButtonsAsync(
                phone,
                "¿Necesitas algo más?",
                buttons,
                ct);
        }
    }

    private async Task NotifyManagementAsync(
        PipelineResult result, WhatsAppMessage message,
        CancellationToken ct)
    {
        var booking = result.BookingToCreate;
        if (booking == null) return;

        var notificationText = $"🆕 *Nueva reserva*\n" +
                               $"👤 {booking.Name} ({message.SenderNumber})\n" +
                               $"📅 {booking.Date} a las {booking.Time}\n" +
                               $"👥 {booking.People} personas" +
                               (string.IsNullOrEmpty(booking.ArrozType) ? "" : $"\n🍚 {booking.ArrozType}");

        foreach (var phone in ManagementPhones)
        {
            try { await _whatsApp.SendTextAsync(phone, notificationText, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to notify {Phone}", phone); }
        }
    }

    // === MESSAGE EXTRACTION (unchanged) ===

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
        string? chatId = null;
        string? callId = null;

        if (body.TryGetProperty("call", out var callProp) && callProp.ValueKind == JsonValueKind.Object)
        {
            if (callProp.TryGetProperty("chatid", out var c1) && c1.ValueKind == JsonValueKind.String)
                chatId = c1.GetString();
            if (callProp.TryGetProperty("chatId", out var c2) && c2.ValueKind == JsonValueKind.String)
                chatId ??= c2.GetString();
            if (callProp.TryGetProperty("id", out var id1) && id1.ValueKind == JsonValueKind.String)
                callId = id1.GetString();
            if (callProp.TryGetProperty("callId", out var id2) && id2.ValueKind == JsonValueKind.String)
                callId ??= id2.GetString();
        }

        if (chatId == null && body.TryGetProperty("chatid", out var topChat) && topChat.ValueKind == JsonValueKind.String)
            chatId = topChat.GetString();

        if (chatId == null &&
            body.TryGetProperty("message", out var msgProp) &&
            msgProp.ValueKind == JsonValueKind.Object &&
            msgProp.TryGetProperty("chatid", out var msgChat) &&
            msgChat.ValueKind == JsonValueKind.String)
        {
            chatId = msgChat.GetString();
        }

        var phone = (chatId ?? "").Replace("@s.whatsapp.net", "", StringComparison.OrdinalIgnoreCase);
        phone = new string(phone.Where(char.IsDigit).ToArray());

        return (phone, callId);
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
