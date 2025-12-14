using System.Text.Json;
using BotGenerator.Core.Agents;
using BotGenerator.Core.Handlers;
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace BotGenerator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly MainConversationAgent _mainAgent;
    private readonly IIntentRouterService _intentRouter;
    private readonly IConversationHistoryService _historyService;
    private readonly IAiStateExtractorService _aiStateExtractor;
    private readonly IPendingBookingStore _pendingBookingStore;
    private readonly IPendingRiceStore _pendingRiceStore;
    private readonly IWhatsAppService _whatsApp;
    private readonly IMenuRepository _menuRepository;
    private readonly IRiceValidatorService _riceValidator;
    private readonly IBookingAvailabilityService _availability;
    private readonly BookingHandler _bookingHandler;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        MainConversationAgent mainAgent,
        IIntentRouterService intentRouter,
        IConversationHistoryService historyService,
        IAiStateExtractorService aiStateExtractor,
        IPendingBookingStore pendingBookingStore,
        IPendingRiceStore pendingRiceStore,
        IWhatsAppService whatsApp,
        IMenuRepository menuRepository,
        IRiceValidatorService riceValidator,
        IBookingAvailabilityService availability,
        BookingHandler bookingHandler,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<WebhookController> logger)
    {
        _mainAgent = mainAgent;
        _intentRouter = intentRouter;
        _historyService = historyService;
        _aiStateExtractor = aiStateExtractor;
        _pendingBookingStore = pendingBookingStore;
        _pendingRiceStore = pendingRiceStore;
        _whatsApp = whatsApp;
        _menuRepository = menuRepository;
        _riceValidator = riceValidator;
        _availability = availability;
        _bookingHandler = bookingHandler;
        _configuration = configuration;
        _environment = environment;
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
    /// Test-control endpoint: clears in-memory state (conversation history + pending booking) for a phone.
    /// Enabled only in Development.
    /// </summary>
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

            // Check event type - only process actual messages
            if (body.TryGetProperty("EventType", out var eventTypeProp))
            {
                var eventType = eventTypeProp.GetString();
                if (eventType != "messages")
                {
                    _logger.LogDebug("Ignoring non-message event: {EventType}", eventType);
                    return Ok();
                }
            }

            // Also check for "message" property existence before trying to extract
            if (!body.TryGetProperty("message", out _))
            {
                _logger.LogDebug("No 'message' property in payload, ignoring");
                return Ok();
            }

            // 1. Extract message data
            var message = ExtractMessage(body);

            _logger.LogDebug(
                "Extracted message - Text: '{Text}', FromMe: {FromMe}, Type: {Type}",
                message.MessageText,
                message.FromMe,
                message.MessageType);

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

            // 2. Get conversation history (bot-side memory)
            var history = await _historyService.GetHistoryAsync(
                message.SenderNumber, cancellationToken);

            // 3. Extract conversation state using AI (more robust than regex)
            // AI understands natural language variations like "nah", "ninguna", "sin tronas", etc.
            var historyForState = history
                .Append(ChatMessage.FromUser(message.MessageText, message.PushName))
                .ToList();

            var state = await _aiStateExtractor.ExtractStateAsync(historyForState, cancellationToken);

            // 3b. Apply pre-checks (availability + rice constraints) before calling Gemini
            var restaurantId = GetRestaurantId(message.SenderNumber);
            var precheck = await TryHandlePreChecksAsync(
                restaurantId,
                message,
                state,
                cancellationToken);

            // Allow pre-checks to enrich the state (e.g., validated rice name)
            state = precheck.UpdatedState;

            // Prefer ArrozType from pending booking store (contains full validated name from DB)
            // This ensures the full rice name is used even when extracted from abbreviated AI text
            var pendingBookingForRice = _pendingBookingStore.Get(message.SenderNumber);
            if (pendingBookingForRice != null && !string.IsNullOrWhiteSpace(pendingBookingForRice.ArrozType))
            {
                state = state with { ArrozType = pendingBookingForRice.ArrozType };
            }

            if (precheck.Handled)
            {
                // Persist to conversation history so the bot keeps context
                await _historyService.AddMessageAsync(
                    message.SenderNumber,
                    ChatMessage.FromUser(message.MessageText, message.PushName),
                    cancellationToken);

                await _historyService.AddMessageAsync(
                    message.SenderNumber,
                    ChatMessage.FromAssistant(precheck.StoredAssistantText),
                    cancellationToken);

                return Ok(new { processed = true, shortCircuited = true });
            }

            // 3c. Deterministic booking creation:
            // If the user is confirming and we already have all required data in the extracted state,
            // create the booking directly (no need for the LLM to emit BOOKING_REQUEST).
            var isConfirming = IsUserConfirming(message.MessageText);
            var isReady = IsReadyToBook(state);
            if (isConfirming)
            {
                _logger.LogInformation(
                    "Confirm gate: confirming={Confirming} ready={Ready} state(fecha={Fecha}, hora={Hora}, personas={Personas}, arrozType={ArrozType}, arrozServings={ArrozServings}, tronas={Tronas}, carritos={Carritos})",
                    isConfirming,
                    isReady,
                    state.Fecha,
                    state.Hora,
                    state.Personas,
                    state.ArrozType,
                    state.ArrozServings,
                    state.HighChairs,
                    state.BabyStrollers);
            }

            if (isConfirming && isReady)
            {
                var arrozType = string.IsNullOrWhiteSpace(state.ArrozType) ? null : state.ArrozType;
                var arrozServings = arrozType == null ? null : state.ArrozServings;

                var booking = new BookingData
                {
                    Name = message.PushName,
                    Phone = message.SenderNumber,
                    Date = state.Fecha!,
                    Time = state.Hora!,
                    People = state.Personas!.Value,
                    ArrozType = arrozType,
                    ArrozServings = arrozServings,
                    HighChairs = Math.Clamp(state.HighChairs ?? 0, 0, 3),
                    BabyStrollers = Math.Clamp(state.BabyStrollers ?? 0, 0, 3)
                };

                var createdResponse = await _bookingHandler.CreateBookingAsync(
                    booking,
                    message,
                    cancellationToken);

                // Reuse the same sending logic below
                var finalResponseDirect = createdResponse;

                if (finalResponseDirect.Metadata != null &&
                    finalResponseDirect.Metadata.TryGetValue("bookingCreated", out var createdObj2) &&
                    createdObj2 is bool created2 &&
                    created2 &&
                    finalResponseDirect.ExtractedData != null)
                {
                    var bookingId2 = finalResponseDirect.Metadata.TryGetValue("bookingId", out var idObj2)
                        ? idObj2?.ToString() ?? ""
                        : "";

                    var customerText2 = BuildCustomerConfirmationWithButtons(
                        finalResponseDirect.ExtractedData,
                        bookingId2);

                    var buttons2 = new List<LinkButtonOption>
                    {
                        new("CONDICIONES", "https://alqueriavillacarmen.com/booking_policies.php")
                    };

                    if (!string.IsNullOrWhiteSpace(bookingId2))
                    {
                        buttons2.Add(new LinkButtonOption(
                            "Cancelar Reserva",
                            $"https://alqueriavillacarmen.com/cancel_reservation.php?id={bookingId2}"));
                    }

                    var sentButtons2 = await _whatsApp.SendLinkButtonsAsync(
                        message.SenderNumber,
                        customerText2,
                        buttons2,
                        cancellationToken);

                    if (!sentButtons2)
                    {
                        await _whatsApp.SendTextAsync(message.SenderNumber, customerText2, cancellationToken);
                    }

                    var adminText2 = BuildAdminNewBookingNotification(finalResponseDirect.ExtractedData, bookingId2);
                    await _whatsApp.SendTextAsync("34692747052", adminText2, cancellationToken);

                    // Store history for deterministic booking
                    await _historyService.AddMessageAsync(
                        message.SenderNumber,
                        ChatMessage.FromUser(message.MessageText, message.PushName),
                        cancellationToken);
                    await _historyService.AddMessageAsync(
                        message.SenderNumber,
                        ChatMessage.FromAssistant(customerText2),
                        cancellationToken);

                    return Ok(new { processed = true, bookingCreated = true, bookingId = bookingId2, deterministic = true });
                }

                // Fallback: just send whatever message the handler returned
                await _whatsApp.SendTextAsync(message.SenderNumber, finalResponseDirect.AiResponse, cancellationToken);

                // Store history for deterministic fallback
                await _historyService.AddMessageAsync(
                    message.SenderNumber,
                    ChatMessage.FromUser(message.MessageText, message.PushName),
                    cancellationToken);
                await _historyService.AddMessageAsync(
                    message.SenderNumber,
                    ChatMessage.FromAssistant(finalResponseDirect.AiResponse),
                    cancellationToken);

                return Ok(new { processed = true, deterministic = true });
            }

            // 4. Process with main agent
            var agentResponse = await _mainAgent.ProcessAsync(
                message, state, history, cancellationToken) ?? AgentResponse.Error("Main agent returned null");

            // 5. Route based on intent
            var finalResponse = await _intentRouter.RouteAsync(
                agentResponse, message, state, cancellationToken) ?? agentResponse;

            // 5b. Store conversation history AFTER routing (so the FINAL response is stored)
            // This is critical for multi-turn flows like tronas/carritos where IntentRouter
            // replaces the AI response with hardcoded questions.
            await _historyService.AddMessageAsync(
                message.SenderNumber,
                ChatMessage.FromUser(message.MessageText, message.PushName),
                cancellationToken);

            // 6. Send response
            // If booking was created, send the official confirmation with buttons (policies + cancel)
            if (finalResponse.Metadata != null &&
                finalResponse.Metadata.TryGetValue("bookingCreated", out var createdObj) &&
                createdObj is bool created &&
                created &&
                finalResponse.ExtractedData != null)
            {
                var bookingId = finalResponse.Metadata.TryGetValue("bookingId", out var idObj)
                    ? idObj?.ToString() ?? ""
                    : "";

                var customerText = BuildCustomerConfirmationWithButtons(
                    finalResponse.ExtractedData,
                    bookingId);

                var buttons = new List<LinkButtonOption>
                {
                    new("CONDICIONES", "https://alqueriavillacarmen.com/booking_policies.php")
                };

                if (!string.IsNullOrWhiteSpace(bookingId))
                {
                    buttons.Add(new LinkButtonOption(
                        "Cancelar Reserva",
                        $"https://alqueriavillacarmen.com/cancel_reservation.php?id={bookingId}"));
                }

                var sentButtons = await _whatsApp.SendLinkButtonsAsync(
                    message.SenderNumber,
                    customerText,
                    buttons,
                    cancellationToken);

                if (!sentButtons)
                {
                    // Fallback to plain text
                    await _whatsApp.SendTextAsync(message.SenderNumber, customerText, cancellationToken);
                }

                // Notify admin
                var adminText = BuildAdminNewBookingNotification(finalResponse.ExtractedData, bookingId);
                await _whatsApp.SendTextAsync("34692747052", adminText, cancellationToken);

                // Store assistant response (booking confirmation)
                await _historyService.AddMessageAsync(
                    message.SenderNumber,
                    ChatMessage.FromAssistant(customerText),
                    cancellationToken);

                return Ok(new { processed = true, bookingCreated = true, bookingId });
            }

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

            // Store assistant response (final response after routing)
            await _historyService.AddMessageAsync(
                message.SenderNumber,
                ChatMessage.FromAssistant(finalResponse.AiResponse),
                cancellationToken);

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
            _logger.LogDebug("Extracted text from 'text' property: '{Text}'", messageText);
        }
        else
        {
            _logger.LogDebug("No 'text' property found in message body");
        }

        // Button response (only override if vote is not empty)
        if (messageBody.TryGetProperty("vote", out var voteProp))
        {
            var vote = voteProp.GetString() ?? "";
            if (!string.IsNullOrWhiteSpace(vote))
            {
                messageText = vote;
            }
        }

        // List response (only if content is an object, not a string, and only override if not empty)
        if (messageBody.TryGetProperty("content", out var contentProp) &&
            contentProp.ValueKind == System.Text.Json.JsonValueKind.Object &&
            contentProp.TryGetProperty("Response", out var responseProp) &&
            responseProp.TryGetProperty("SelectedDisplayText", out var selectedProp))
        {
            var selectedText = selectedProp.GetString() ?? "";
            if (!string.IsNullOrWhiteSpace(selectedText))
            {
                messageText = selectedText;
            }
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

    private string GetRestaurantId(string senderNumber)
    {
        var mapping = _configuration
            .GetSection("Restaurants:Mapping")
            .GetChildren()
            .ToDictionary(x => x.Key, x => x.Value);

        if (mapping.TryGetValue(senderNumber, out var restaurantId) && !string.IsNullOrWhiteSpace(restaurantId))
        {
            return restaurantId!;
        }

        return _configuration["Restaurants:Default"] ?? "villacarmen";
    }

    private async Task<(bool Handled, string StoredAssistantText, ConversationState UpdatedState)> TryHandlePreChecksAsync(
        string restaurantId,
        WhatsAppMessage message,
        ConversationState state,
        CancellationToken cancellationToken)
    {
        var updatedState = state;

        // === Event booking detection (weddings, birthdays, communions, etc.) ===
        if (IsEventBookingRequest(message.MessageText))
        {
            _logger.LogInformation("Event booking request detected from {Phone}", message.SenderNumber);

            // Send informative message
            var eventMsg = "Para reservas de *eventos especiales* (bodas, comuniones, cumpleaños, celebraciones de empresa, etc.) " +
                           "te atenderá nuestro equipo de gestión de eventos.\n\n" +
                           "Te comparto su contacto para que puedas hablar directamente con ellos:";
            await _whatsApp.SendTextAsync(message.SenderNumber, eventMsg, cancellationToken);

            // Send contact card for events team
            await _whatsApp.SendContactCardAsync(
                message.SenderNumber,
                fullName: "Eventos Villa Carmen",
                contactPhoneNumber: "+34638857294",
                organization: "Alquería Villa Carmen",
                cancellationToken: cancellationToken);

            return (true, eventMsg, updatedState);
        }

        // === Same-day booking detection (must call restaurant directly) ===
        if (IsSameDayBookingRequest(message.MessageText))
        {
            _logger.LogInformation("Same-day booking request detected from {Phone}", message.SenderNumber);

            // Send informative message
            var sameDayIntro = BotGenerator.Core.Services.ResponseVariations.SameDayBookingIntro();
            await _whatsApp.SendTextAsync(message.SenderNumber, sameDayIntro, cancellationToken);

            // Send contact card
            await _whatsApp.SendContactCardAsync(
                message.SenderNumber,
                fullName: "Gestión Reservas Villa Carmen",
                contactPhoneNumber: "+34638857294",
                organization: "Alquería Villa Carmen",
                cancellationToken: cancellationToken);

            var sameDayMsg = BotGenerator.Core.Services.ResponseVariations.SameDayBookingRejection();
            return (true, sameDayMsg, updatedState);
        }

        // === FIRST-ORDER CHECK: 35-day booking window ===
        // Bookings must be within 35 days from today
        var extractedDate = TryExtractDateFromMessage(message.MessageText);
        if (extractedDate.HasValue)
        {
            var requestedDate = extractedDate.Value.Date;
            var today = DateTime.Now.Date;
            var maxBookingDate = today.AddDays(35);

            // Check if date is beyond 35-day window
            if (requestedDate > maxBookingDate)
            {
                _logger.LogInformation(
                    "Date {Date} is beyond 35-day window for {Phone}",
                    requestedDate.ToString("yyyy-MM-dd"),
                    message.SenderNumber);

                var daysAhead = (requestedDate - today).Days;
                var tooFarMsg = $"Lo siento, solo aceptamos reservas con un máximo de 35 días de antelación. " +
                                $"Esa fecha está a {daysAhead} días. ¿Te viene bien una fecha más cercana?";
                await _whatsApp.SendTextAsync(message.SenderNumber, tooFarMsg, cancellationToken);
                return (true, tooFarMsg, updatedState);
            }
        }

        // === COMPREHENSIVE VALIDATION: Date, Party Size, and Time ===
        // Extract values from current message
        var extractedPartySize = TryExtractPartySizeFromMessage(message.MessageText);
        var extractedTime = TryExtractTimeFromMessage(message.MessageText);

        // Determine effective values (from message or state)
        DateTime? effectiveDate = extractedDate?.Date ?? (state.Fecha != null ? ParseDateFromState(state.Fecha) : null);
        int? effectivePartySize = extractedPartySize ?? state.Personas;
        TimeSpan? effectiveTime = extractedTime ?? (state.Hora != null ? ParseTimeFromState(state.Hora) : null);

        // === DATE VALIDATION (when date is mentioned or when party size/time changes for existing date) ===
        if (effectiveDate.HasValue && effectiveDate.Value > DateTime.Now.Date)
        {
            var requestedDate = effectiveDate.Value;

            // 1. Check if day is open (default closed Mon/Tue/Wed + restaurant_days overrides)
            var dayStatus = await _availability.CheckDayStatusAsync(requestedDate, cancellationToken);
            if (!dayStatus.IsOpen)
            {
                _logger.LogInformation(
                    "Day {Date} ({Weekday}) is closed for {Phone}",
                    requestedDate.ToString("yyyy-MM-dd"),
                    dayStatus.Weekday,
                    message.SenderNumber);

                var closedMsg = $"Lo siento, el *{dayStatus.Weekday}* estamos cerrados. ¿Te viene bien otro día?";
                await _whatsApp.SendTextAsync(message.SenderNumber, closedMsg, cancellationToken);
                return (true, closedMsg, updatedState);
            }

            // Store validated date in state EARLY - even if capacity fails, date is remembered
            // (user can try with fewer people or different time on subsequent messages)
            if (extractedDate.HasValue)
            {
                updatedState = updatedState with { Fecha = requestedDate.ToString("dd/MM/yyyy") };
                _logger.LogDebug("Stored validated date in state: {Date}", updatedState.Fecha);
            }

            // 2. Check daily capacity - ALWAYS check if day is full, even without party size
            var dailyLimit = await _availability.GetDailyLimitAsync(requestedDate, cancellationToken);
            _logger.LogDebug(
                "Daily limit for {Date}: limit={Limit}, booked={Booked}, free={Free}",
                requestedDate.ToString("yyyy-MM-dd"),
                dailyLimit.DailyLimit,
                dailyLimit.TotalPeople,
                dailyLimit.FreeBookingSeats);

            // If day is completely full (no seats left), reject immediately
            if (dailyLimit.FreeBookingSeats <= 0)
            {
                _logger.LogInformation(
                    "Day {Date} is FULL ({Booked}/{Limit} people) for {Phone}",
                    requestedDate.ToString("yyyy-MM-dd"),
                    dailyLimit.TotalPeople,
                    dailyLimit.DailyLimit,
                    message.SenderNumber);

                var fullMsg = "Lo siento, ese día ya estamos completos. ¿Te viene bien otra fecha?";
                await _whatsApp.SendTextAsync(message.SenderNumber, fullMsg, cancellationToken);
                return (true, fullMsg, updatedState);
            }

            // If we have party size, check if there's enough capacity for that specific group
            if (effectivePartySize.HasValue && effectivePartySize.Value > 0 && dailyLimit.FreeBookingSeats < effectivePartySize.Value)
            {
                _logger.LogInformation(
                    "Day {Date} has insufficient capacity for {PartySize} (free: {Free}) for {Phone}",
                    requestedDate.ToString("yyyy-MM-dd"),
                    effectivePartySize.Value,
                    dailyLimit.FreeBookingSeats,
                    message.SenderNumber);

                var capacityMsg = $"Ese día solo nos quedan {dailyLimit.FreeBookingSeats} plazas, no podemos acoger {effectivePartySize.Value} personas. ¿Te viene bien otra fecha?";
                await _whatsApp.SendTextAsync(message.SenderNumber, capacityMsg, cancellationToken);
                return (true, capacityMsg, updatedState);
            }

            // 3. Check hour availability (if we have time from message or state)
            if (effectiveTime.HasValue && effectivePartySize.HasValue && effectivePartySize.Value > 0)
            {
                var hourData = await _availability.GetHourDataAsync(requestedDate, cancellationToken);
                var timeKey = $"{effectiveTime.Value.Hours:D2}:{effectiveTime.Value.Minutes:D2}";

                if (!hourData.HourData.TryGetValue(timeKey, out var slot))
                {
                    // Time not in available slots
                    var availableHours = hourData.ActiveHours.Take(5).ToList();
                    var hoursMsg = availableHours.Count > 0
                        ? $"A las {timeKey} no tenemos servicio. Nuestros horarios disponibles son: {string.Join(", ", availableHours)}. ¿Cuál te viene mejor?"
                        : $"A las {timeKey} no tenemos servicio. ¿A qué hora te gustaría venir?";

                    _logger.LogInformation("Time {Time} not available for {Phone}", timeKey, message.SenderNumber);
                    await _whatsApp.SendTextAsync(message.SenderNumber, hoursMsg, cancellationToken);
                    return (true, hoursMsg, updatedState);
                }

                if (slot.IsClosed)
                {
                    var availableHours = hourData.ActiveHours
                        .Where(h => hourData.HourData.TryGetValue(h, out var s) && !s.IsClosed)
                        .Take(5).ToList();
                    var closedHourMsg = availableHours.Count > 0
                        ? $"A las {timeKey} no tenemos disponibilidad. Tengo hueco a las {string.Join(", ", availableHours)}. ¿Te viene bien alguna?"
                        : $"A las {timeKey} no tenemos disponibilidad. ¿Te viene bien otra hora?";

                    _logger.LogInformation("Time {Time} is closed for {Phone}", timeKey, message.SenderNumber);
                    await _whatsApp.SendTextAsync(message.SenderNumber, closedHourMsg, cancellationToken);
                    return (true, closedHourMsg, updatedState);
                }

                if (slot.Capacity < effectivePartySize.Value)
                {
                    // Not enough capacity at this hour
                    var availableHours = hourData.ActiveHours
                        .Where(h => hourData.HourData.TryGetValue(h, out var s) && !s.IsClosed && s.Capacity >= effectivePartySize.Value)
                        .Take(5).ToList();

                    var capacityMsg = availableHours.Count > 0
                        ? $"A las {timeKey} ya no tenemos hueco para {effectivePartySize.Value} personas. Tengo disponibilidad a las {string.Join(", ", availableHours)}. ¿Te viene bien alguna?"
                        : $"A las {timeKey} ya no tenemos hueco para {effectivePartySize.Value} personas. ¿Te viene bien otra hora?";

                    _logger.LogInformation("Time {Time} full for {PartySize} (capacity: {Capacity}) for {Phone}",
                        timeKey, effectivePartySize.Value, slot.Capacity, message.SenderNumber);
                    await _whatsApp.SendTextAsync(message.SenderNumber, capacityMsg, cancellationToken);
                    return (true, capacityMsg, updatedState);
                }
            }
        }

        // Check if user is selecting from pending rice options (persistent store)
        // This must happen BEFORE party size extraction to avoid treating "1" (rice selection) as party size
        var pendingRice = _pendingRiceStore.Get(message.SenderNumber);
        var isSelectingRice = pendingRice?.Options?.Count > 0;

        // Store extracted party size in state for subsequent messages
        // But NOT if user is selecting from pending rice options (a simple "1" is rice selection, not party size)
        if (extractedPartySize.HasValue && extractedPartySize.Value > 0 && !isSelectingRice)
        {
            updatedState = updatedState with { Personas = extractedPartySize.Value };
            _logger.LogDebug("Stored party size in state: {Personas}", updatedState.Personas);
        }

        // Store extracted time in state for subsequent messages
        if (extractedTime.HasValue)
        {
            updatedState = updatedState with { Hora = $"{extractedTime.Value.Hours:D2}:{extractedTime.Value.Minutes:D2}" };
            _logger.LogDebug("Stored time in state: {Hora}", updatedState.Hora);
        }

        // === Rice constraints & validation (if user mentions a rice/paella) ===
        if (pendingRice?.Options?.Count > 0)
        {
            var selectedRice = TryParseRiceSelection(message.MessageText, pendingRice.Options);
            if (selectedRice != null)
            {
                _logger.LogInformation("User selected rice from pending options: {Rice}", selectedRice);

                // Clear pending options from persistent store
                _pendingRiceStore.Clear(message.SenderNumber);

                updatedState = updatedState with
                {
                    ArrozType = selectedRice,
                    PendingRiceOptions = null // Also clear from ephemeral state
                };

                // ALWAYS store the full rice name in pending booking store
                // This ensures the full DB name persists across messages (AI extractor might extract abbreviated names)
                var pendingBooking = _pendingBookingStore.Get(message.SenderNumber) ?? new BookingData();
                _pendingBookingStore.Set(message.SenderNumber, pendingBooking with { ArrozType = selectedRice });

                // Extract servings if mentioned
                if (TryExtractRiceServings(message.MessageText, out var servings))
                {
                    updatedState = updatedState with { ArrozServings = servings };
                }

                // Don't return here - let the conversation continue to ask for servings if needed
            }
            else
            {
                // User didn't select a valid option, prompt again
                _logger.LogInformation("Could not parse rice selection from: {Message}", message.MessageText);
                var formattedOptions = string.Join("\n", pendingRice.Options.Select((r, i) => $"{i + 1}. {r}"));
                var retryMsg = $"No he entendido tu elección. Por favor, dime el número de la opción que prefieres:\n\n{formattedOptions}";
                await _whatsApp.SendTextAsync(message.SenderNumber, retryMsg, cancellationToken);
                return (true, retryMsg, updatedState);
            }
        }

        if (DeclinesRice(message.MessageText))
        {
            updatedState = updatedState with { ArrozType = "", ArrozServings = null, PendingRiceOptions = null };
            _pendingRiceStore.Clear(message.SenderNumber); // Also clear persistent store
        }
        else if (pendingRice == null && MentionsRice(message.MessageText))
        {
            var validation = await _riceValidator.ValidateAsync(
                message.MessageText,
                restaurantId,
                cancellationToken);

            if (!validation.IsValid)
            {
                string text;

                // Handle multiple matches: send numbered list so user can say "1", "la primera", etc.
                if (validation.Status == "multiple" && validation.Options?.Count > 0)
                {
                    var numberedList = string.Join("\n", validation.Options.Select((r, i) => $"{i + 1}. {r}"));
                    text = $"He encontrado varias opciones parecidas. Elige una, por favor:\n\n{numberedList}\n\nPuedes decirme el número o el nombre del arroz.";

                    await _whatsApp.SendTextAsync(message.SenderNumber, text, cancellationToken);

                    // Store options in PERSISTENT store for later selection parsing (next turn)
                    _pendingRiceStore.Set(message.SenderNumber, new PendingRiceSelection
                    {
                        Options = validation.Options,
                        OriginalRequest = message.MessageText
                    });

                    // Also store in ephemeral state (for same-turn logic)
                    updatedState = updatedState with { PendingRiceOptions = validation.Options };
                }
                else
                {
                    // Rice not found: send link button to menu
                    var menuUrl = "https://alqueriavillacarmen.com/menufindesemana.php";
                    text = "Lo siento, no tenemos ese arroz. Puedes ver nuestra carta de arroces aquí:";

                    var sent = await _whatsApp.SendLinkButtonsAsync(
                        message.SenderNumber,
                        text,
                        new List<LinkButtonOption> { new("Ver carta de arroces", menuUrl) },
                        cancellationToken);

                    // Fallback to plain text if button fails
                    if (!sent)
                    {
                        await _whatsApp.SendTextAsync(
                            message.SenderNumber,
                            $"{text}\n{menuUrl}",
                            cancellationToken);
                    }
                }

                return (true, text, updatedState);
            }

            // Valid rice: keep normalized name in state for prompt + downstream enforcement
            if (!string.IsNullOrWhiteSpace(validation.RiceName))
            {
                updatedState = updatedState with { ArrozType = validation.RiceName };
            }

            // If user included servings in the same message, capture it
            if (TryExtractRiceServings(message.MessageText, out var servings))
            {
                updatedState = updatedState with { ArrozServings = servings };
            }

            // Deterministic short-circuit: if the rice is valid, continue the booking flow in code
            // to prevent the LLM from hallucinating that a DB-valid rice "is not in the menu".
            // We only guide to the next missing piece of info; other turns can proceed normally.
            if (!string.IsNullOrWhiteSpace(updatedState.ArrozType))
            {
                // Ask missing basics first
                if (string.IsNullOrWhiteSpace(updatedState.Fecha))
                {
                    var msg = $"✅ {updatedState.ArrozType} disponible. ¿Para qué *día* sería la reserva?";
                    await _whatsApp.SendTextAsync(message.SenderNumber, msg, cancellationToken);
                    return (true, msg, updatedState);
                }

                if (!updatedState.Personas.HasValue || updatedState.Personas.Value <= 0)
                {
                    var msg = $"✅ {updatedState.ArrozType} disponible. ¿Para cuántas *personas* sería?";
                    await _whatsApp.SendTextAsync(message.SenderNumber, msg, cancellationToken);
                    return (true, msg, updatedState);
                }

                if (string.IsNullOrWhiteSpace(updatedState.Hora))
                {
                    var msg = $"✅ {updatedState.ArrozType} disponible. ¿A qué *hora* os viene bien?";
                    await _whatsApp.SendTextAsync(message.SenderNumber, msg, cancellationToken);
                    return (true, msg, updatedState);
                }

                // If servings missing (and rice chosen), ask for servings
                if (!updatedState.ArrozServings.HasValue)
                {
                    var msg = $"✅ {updatedState.ArrozType} disponible. ¿Cuántas *raciones* queréis? (mínimo 2)";
                    await _whatsApp.SendTextAsync(message.SenderNumber, msg, cancellationToken);
                    return (true, msg, updatedState);
                }

                // Then ask mandatory extras
                if (!updatedState.HighChairs.HasValue)
                {
                    var msg = $"✅ {updatedState.ArrozType} disponible. Antes de confirmarla, ¿necesitáis *tronas*?";
                    await _whatsApp.SendTextAsync(message.SenderNumber, msg, cancellationToken);
                    return (true, msg, updatedState);
                }

                if (!updatedState.BabyStrollers.HasValue)
                {
                    var msg = $"✅ {updatedState.ArrozType} disponible. ¿Vais a traer *carrito de bebé*?";
                    await _whatsApp.SendTextAsync(message.SenderNumber, msg, cancellationToken);
                    return (true, msg, updatedState);
                }

                // If everything is present, ask for confirmation with a brief summary
                var arrozSummary = $"{updatedState.ArrozType} ({updatedState.ArrozServings.Value} raciones)";
                var tronas = updatedState.HighChairs.GetValueOrDefault(0);
                var carritos = updatedState.BabyStrollers.GetValueOrDefault(0);
                var confirmMsg =
                    $"Reserva para {updatedState.Personas} personas el *{updatedState.Fecha}* a las *{updatedState.Hora}*, " +
                    $"con *{arrozSummary}*, {tronas} tronas y {carritos} carritos. ¿Confirmo?";
                await _whatsApp.SendTextAsync(message.SenderNumber, confirmMsg, cancellationToken);
                return (true, confirmMsg, updatedState);
            }
        }

        // === Availability checks mirroring PHP scripts ===
        if (!string.IsNullOrWhiteSpace(updatedState.Fecha) && TryParseDate(updatedState.Fecha!, out var date))
        {
            // Day status check (even if people/time missing)
            var dayStatus = await _availability.CheckDayStatusAsync(date, cancellationToken);
            if (!dayStatus.IsOpen)
            {
                var msg = $"Lo siento, estamos cerrados el {dayStatus.Weekday}. ¿Te viene bien otro día?";
                await _whatsApp.SendTextAsync(message.SenderNumber, msg, cancellationToken);
                return (true, msg, updatedState);
            }

            // Daily capacity check (once party size is known)
            if (updatedState.Personas is > 0)
            {
                var decision = await _availability.EvaluateAsync(
                    date,
                    updatedState.Personas.Value,
                    null,
                    cancellationToken);

                if (!decision.IsAvailable && !string.IsNullOrWhiteSpace(decision.Message))
                {
                    await _whatsApp.SendTextAsync(message.SenderNumber, decision.Message, cancellationToken);
                    return (true, decision.Message, updatedState);
                }
            }

            // Hour feasibility check (once time known too)
            if (updatedState.Personas is > 0 && !string.IsNullOrWhiteSpace(updatedState.Hora) && TryParseTime(updatedState.Hora!, out var time))
            {
                var decision = await _availability.EvaluateAsync(
                    date,
                    updatedState.Personas.Value,
                    time,
                    cancellationToken);

                if (!decision.IsAvailable && !string.IsNullOrWhiteSpace(decision.Message))
                {
                    await _whatsApp.SendTextAsync(message.SenderNumber, decision.Message, cancellationToken);
                    return (true, decision.Message, updatedState);
                }
            }
        }

        // Enforce rice servings minimum when user already provided servings
        if (updatedState.ArrozType != null && !string.IsNullOrEmpty(updatedState.ArrozType) &&
            updatedState.ArrozServings.HasValue && updatedState.ArrozServings.Value < 2)
        {
            var msg = "Para los arroces el mínimo es *2 raciones*. ¿Cuántas queréis?";
            await _whatsApp.SendTextAsync(message.SenderNumber, msg, cancellationToken);
            return (true, msg, updatedState);
        }

        // Enforce max 3 for tronas/carritos if already provided
        if (updatedState.HighChairs.HasValue && updatedState.HighChairs.Value > 3)
        {
            var msg = "Podemos preparar como máximo *3* tronas. ¿Cuántas necesitáis?";
            await _whatsApp.SendTextAsync(message.SenderNumber, msg, cancellationToken);
            return (true, msg, updatedState);
        }

        if (updatedState.BabyStrollers.HasValue && updatedState.BabyStrollers.Value > 3)
        {
            var msg = "Podemos gestionar como máximo *3* carritos. ¿Cuántos vais a traer?";
            await _whatsApp.SendTextAsync(message.SenderNumber, msg, cancellationToken);
            return (true, msg, updatedState);
        }

        return (false, "", updatedState);
    }

    private static bool MentionsRice(string text)
    {
        var t = text.ToLowerInvariant();

        // Primary rice-related keywords - if any of these appear, trigger AI validation
        var riceKeywords = new[]
        {
            // Direct rice mentions
            "arroz", "paella", "fideu", "fideuá", "fideua",
            // Cooking styles
            "meloso", "caldoso", "seco", "banda", "abanda",
            // Common ingredients that suggest rice
            "señoret", "señorito", "señorita",
            "bogavante", "marisco", "mariscos", "langosta", "gambas",
            "pulpo", "sepia", "negro", "negra",
            "chorizo", "carrillada", "boletus",
            "valenciana", "valenciano", "albufera"
        };

        return riceKeywords.Any(keyword => t.Contains(keyword));
    }

    private static bool DeclinesRice(string text)
    {
        var t = text.ToLowerInvariant();
        return t.Contains("sin arroz") ||
               t.Contains("no quiero arroz") ||
               t.Contains("no queremos arroz") ||
               t.Contains("no, sin arroz") ||
               t == "no";
    }

    private static bool IsEventBookingRequest(string text)
    {
        var t = text.ToLowerInvariant();
        var eventKeywords = new[]
        {
            "boda", "bodas", "casamiento",
            "cumpleaños", "cumple",
            "comunión", "comunion", "comuniones",
            "bautizo", "bautizos",
            "bodas de oro", "bodas de plata",
            "aniversario",
            "celebración", "celebracion", "celebrar",
            "comida de empresa", "cena de empresa",
            "evento", "eventos",
            "despedida", "despedidas",
            "fiesta", "fiestas",
            "banquete", "banquetes"
        };

        return eventKeywords.Any(keyword => t.Contains(keyword));
    }

    private static bool IsSameDayBookingRequest(string text)
    {
        var t = text.ToLowerInvariant();

        // Direct "today" keywords
        var sameDayKeywords = new[]
        {
            "para hoy",
            "reservar hoy",
            "reserva hoy",
            "mesa hoy",
            "hoy para",
            "el día de hoy",
            "dia de hoy",
            "esta tarde",
            "esta noche",
            "ahora mismo",
            "hoy mismo"
        };

        if (sameDayKeywords.Any(keyword => t.Contains(keyword)))
        {
            return true;
        }

        // Check for standalone "hoy" with booking context
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"\bhoy\b"))
        {
            // Check if it's in a booking context (reservar, mesa, comer, etc.)
            var bookingContextWords = new[] { "reserv", "mesa", "comer", "personas", "gente", "sitio", "hueco" };
            if (bookingContextWords.Any(ctx => t.Contains(ctx)))
            {
                return true;
            }

            // Also catch simple "hoy" responses when likely answering date question
            // (short messages that just say "hoy" or "hoy a las X")
            if (t.Trim() == "hoy" || System.Text.RegularExpressions.Regex.IsMatch(t.Trim(), @"^hoy\s*(a\s*las)?\s*\d"))
            {
                return true;
            }
        }

        // Check for today's date in dd/MM or dd/MM/yyyy format
        var today = DateTime.Now;
        var todayPatterns = new[]
        {
            $"{today.Day}/{today.Month}",
            $"{today.Day:D2}/{today.Month:D2}",
            $"{today.Day}/{today.Month}/{today.Year}",
            $"{today.Day:D2}/{today.Month:D2}/{today.Year}"
        };

        if (todayPatterns.Any(pattern => t.Contains(pattern)))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to extract a date from the user's message text.
    /// Supports day names (sábado, domingo), relative days (mañana), date formats (21/12), and Spanish month names (24 de mayo).
    /// </summary>
    private static DateTime? TryExtractDateFromMessage(string text)
    {
        var t = text.ToLowerInvariant().Trim();
        var today = DateTime.Now.Date;

        // Spanish month names mapping
        var spanishMonths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["enero"] = 1,
            ["febrero"] = 2,
            ["marzo"] = 3,
            ["abril"] = 4,
            ["mayo"] = 5,
            ["junio"] = 6,
            ["julio"] = 7,
            ["agosto"] = 8,
            ["septiembre"] = 9,
            ["octubre"] = 10,
            ["noviembre"] = 11,
            ["diciembre"] = 12
        };

        // Check for "X de [month]" or "día X de [month]" patterns FIRST (highest priority)
        foreach (var (monthName, monthNum) in spanishMonths)
        {
            // Pattern: "24 de mayo", "el 24 de mayo", "día 24 de mayo", "para el 24 de mayo"
            var monthPattern = $@"(?:el\s+|día\s+|para\s+el\s+)?(\d{{1,2}})\s+de\s+{monthName}";
            var monthMatch = System.Text.RegularExpressions.Regex.Match(t, monthPattern);
            if (monthMatch.Success)
            {
                var day = int.Parse(monthMatch.Groups[1].Value);
                var year = today.Year;

                // If month has passed or is current month but day has passed, use next year
                if (monthNum < today.Month || (monthNum == today.Month && day <= today.Day))
                {
                    year = today.Year + 1;
                }

                try
                {
                    return new DateTime(year, monthNum, day);
                }
                catch
                {
                    // Invalid date (e.g., 31 de febrero)
                }
            }
        }

        // Day name mappings (Spanish)
        var dayNames = new Dictionary<string, DayOfWeek>(StringComparer.OrdinalIgnoreCase)
        {
            ["lunes"] = DayOfWeek.Monday,
            ["martes"] = DayOfWeek.Tuesday,
            ["miercoles"] = DayOfWeek.Wednesday,
            ["miércoles"] = DayOfWeek.Wednesday,
            ["jueves"] = DayOfWeek.Thursday,
            ["viernes"] = DayOfWeek.Friday,
            ["sabado"] = DayOfWeek.Saturday,
            ["sábado"] = DayOfWeek.Saturday,
            ["domingo"] = DayOfWeek.Sunday
        };

        // Check for day names
        foreach (var (name, dayOfWeek) in dayNames)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(t, $@"\b{name}\b"))
            {
                // Find the next occurrence of this day
                var daysUntil = ((int)dayOfWeek - (int)today.DayOfWeek + 7) % 7;
                if (daysUntil == 0) daysUntil = 7; // If today is that day, assume next week
                return today.AddDays(daysUntil);
            }
        }

        // Check for "mañana"
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"\bmañana\b"))
        {
            return today.AddDays(1);
        }

        // Check for "pasado mañana"
        if (t.Contains("pasado mañana"))
        {
            return today.AddDays(2);
        }

        // Check for date patterns: dd/MM, dd-MM, dd/MM/yyyy
        var dateMatch = System.Text.RegularExpressions.Regex.Match(t, @"\b(\d{1,2})[/\-](\d{1,2})(?:[/\-](\d{4}|\d{2}))?\b");
        if (dateMatch.Success)
        {
            var day = int.Parse(dateMatch.Groups[1].Value);
            var month = int.Parse(dateMatch.Groups[2].Value);
            var year = today.Year;

            if (dateMatch.Groups[3].Success)
            {
                var yearPart = dateMatch.Groups[3].Value;
                year = yearPart.Length == 2 ? 2000 + int.Parse(yearPart) : int.Parse(yearPart);
            }
            else if (month < today.Month || (month == today.Month && day < today.Day))
            {
                // If month already passed, assume next year
                year = today.Year + 1;
            }

            try
            {
                return new DateTime(year, month, day);
            }
            catch
            {
                return null;
            }
        }

        // Check for "día X" or "el X" patterns (day number only)
        var dayOnlyMatch = System.Text.RegularExpressions.Regex.Match(t, @"\b(?:día|el|para el)\s*(\d{1,2})\b");
        if (dayOnlyMatch.Success)
        {
            var day = int.Parse(dayOnlyMatch.Groups[1].Value);
            if (day >= 1 && day <= 31)
            {
                var month = today.Month;
                var year = today.Year;

                // If day already passed this month, use next month
                if (day <= today.Day)
                {
                    month++;
                    if (month > 12)
                    {
                        month = 1;
                        year++;
                    }
                }

                try
                {
                    return new DateTime(year, month, day);
                }
                catch
                {
                    return null;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Attempts to extract the party size from the user's message text.
    /// </summary>
    private static int? TryExtractPartySizeFromMessage(string text)
    {
        var t = text.ToLowerInvariant();

        // Pattern: "X personas" or "somos X" or "para X"
        var patterns = new[]
        {
            @"(\d+)\s*personas?",
            @"somos\s*(\d+)",
            @"seremos\s*(\d+)",
            @"mesa\s*(?:para|de)\s*(\d+)",
            @"para\s*(\d+)\s*(?:personas?|comensales?|adultos?)?",
            @"(\d+)\s*(?:comensales?|adultos?)"
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(t, pattern);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var size) && size > 0 && size <= 50)
            {
                return size;
            }
        }

        // Fallback: if the entire message is just a number (user answering "how many people?")
        var trimmed = t.Trim();
        if (int.TryParse(trimmed, out var bareNumber) && bareNumber > 0 && bareNumber <= 50)
        {
            return bareNumber;
        }

        return null;
    }

    /// <summary>
    /// Extracts time from user message (e.g., "a las 14:00", "14:30", "a las 14", "las dos y media").
    /// </summary>
    private static TimeSpan? TryExtractTimeFromMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.ToLowerInvariant();

        // Pattern: "14:00", "14:30", "a las 14:00"
        var timePattern = System.Text.RegularExpressions.Regex.Match(t, @"(\d{1,2})[:\.](\d{2})");
        if (timePattern.Success)
        {
            var hours = int.Parse(timePattern.Groups[1].Value);
            var mins = int.Parse(timePattern.Groups[2].Value);
            if (hours >= 0 && hours <= 23 && mins >= 0 && mins <= 59)
            {
                return new TimeSpan(hours, mins, 0);
            }
        }

        // Pattern: "a las 14", "las 14" (hour without minutes)
        var hourOnlyPattern = System.Text.RegularExpressions.Regex.Match(t, @"(?:a\s+)?las\s+(\d{1,2})(?:\s|$|[,\.])");
        if (hourOnlyPattern.Success)
        {
            var hours = int.Parse(hourOnlyPattern.Groups[1].Value);
            if (hours >= 12 && hours <= 23)
            {
                return new TimeSpan(hours, 0, 0);
            }
        }

        // Pattern: "a las dos", "a las tres y media"
        var spanishHours = new Dictionary<string, int>
        {
            ["una"] = 13, ["dos"] = 14, ["tres"] = 15, ["cuatro"] = 16,
            ["cinco"] = 17, ["seis"] = 18, ["siete"] = 19, ["ocho"] = 20,
            ["nueve"] = 21, ["diez"] = 22, ["once"] = 23, ["doce"] = 12
        };

        foreach (var (word, hour) in spanishHours)
        {
            if (t.Contains($"las {word}"))
            {
                var mins = t.Contains("y media") ? 30 : 0;
                return new TimeSpan(hour, mins, 0);
            }
        }

        return null;
    }

    /// <summary>
    /// Parses date from state format (dd/MM/yyyy).
    /// </summary>
    private static DateTime? ParseDateFromState(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;
        if (DateTime.TryParseExact(dateStr, "dd/MM/yyyy",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var date))
        {
            return date;
        }
        return null;
    }

    /// <summary>
    /// Parses time from state format (HH:mm).
    /// </summary>
    private static TimeSpan? ParseTimeFromState(string? timeStr)
    {
        if (string.IsNullOrWhiteSpace(timeStr)) return null;
        if (TimeSpan.TryParse(timeStr, out var time))
        {
            return time;
        }
        return null;
    }

    private static bool TryExtractRiceServings(string text, out int servings)
    {
        servings = 0;
        var match = System.Text.RegularExpressions.Regex.Match(text, @"(\d+)\s*raci(ón|ones)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var s))
        {
            servings = s;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to parse user selection from pending rice options.
    /// Supports: "1", "numero 1", "la primera", "la segunda", "el primero", or exact/partial name match.
    /// </summary>
    private static string? TryParseRiceSelection(string text, List<string> options)
    {
        if (options == null || options.Count == 0) return null;

        var t = (text ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(t)) return null;

        // Check for numeric selection: "1", "2", "numero 1", "el 1", "opcion 2"
        var numMatch = System.Text.RegularExpressions.Regex.Match(t, @"(?:numero|número|opci[oó]n|el|la)?\s*(\d+)");
        if (numMatch.Success && int.TryParse(numMatch.Groups[1].Value, out var num))
        {
            if (num >= 1 && num <= options.Count)
                return options[num - 1];
        }

        // Check for ordinal selection: "la primera", "el primero", "la segunda", etc.
        var ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["primera"] = 1, ["primero"] = 1, ["1ª"] = 1, ["1º"] = 1,
            ["segunda"] = 2, ["segundo"] = 2, ["2ª"] = 2, ["2º"] = 2,
            ["tercera"] = 3, ["tercero"] = 3, ["3ª"] = 3, ["3º"] = 3,
            ["cuarta"] = 4, ["cuarto"] = 4, ["4ª"] = 4, ["4º"] = 4,
            ["quinta"] = 5, ["quinto"] = 5, ["5ª"] = 5, ["5º"] = 5
        };

        foreach (var (ordinal, index) in ordinals)
        {
            if (t.Contains(ordinal) && index <= options.Count)
                return options[index - 1];
        }

        // Check for partial name match against options
        foreach (var option in options)
        {
            var optionLower = option.ToLowerInvariant();
            // Extract the base name (before any price/description markers)
            var baseName = System.Text.RegularExpressions.Regex.Replace(optionLower, @"\s*[\(\+].*$", "").Trim();

            if (t.Contains(baseName) || baseName.Contains(t))
                return option;

            // Also check if user typed key words from the option
            var userWords = t.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 3).ToList();
            var optionWords = baseName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 3).ToList();
            var matchCount = userWords.Count(uw => optionWords.Any(ow => ow.Contains(uw) || uw.Contains(ow)));
            if (matchCount >= 2 || (userWords.Count == 1 && matchCount == 1))
                return option;
        }

        return null;
    }

    private static bool IsUserConfirming(string text)
    {
        var t = (text ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(t)) return false;
        if (t == "si" || t == "sí" || t == "ok" || t == "vale") return true;
        return t.Contains("confirmo") || t.Contains("confirmar") || t.Contains("sí, confirmo") || t.Contains("si, confirmo");
    }

    private static bool IsReadyToBook(ConversationState state)
    {
        if (state.Personas is null || state.Personas <= 0) return false;
        if (string.IsNullOrWhiteSpace(state.Fecha)) return false;
        if (string.IsNullOrWhiteSpace(state.Hora)) return false;
        if (!state.HighChairs.HasValue) return false;
        if (!state.BabyStrollers.HasValue) return false;

        // Rice decision is mandatory: ArrozType == null means not decided yet
        if (state.ArrozType is null) return false;

        // No rice
        if (string.IsNullOrWhiteSpace(state.ArrozType)) return true;

        // With rice: servings required and minimum 2
        if (!state.ArrozServings.HasValue) return false;
        return state.ArrozServings.Value >= 2;
    }

    private static bool TryParseDate(string dateStr, out DateTime date)
    {
        date = default;
        return DateTime.TryParseExact(dateStr, "dd/MM/yyyy", null,
                   System.Globalization.DateTimeStyles.None, out date) ||
               DateTime.TryParseExact(dateStr, "d/M/yyyy", null,
                   System.Globalization.DateTimeStyles.None, out date);
    }

    private static bool TryParseTime(string timeStr, out TimeSpan time)
    {
        time = default;
        return TimeSpan.TryParseExact(timeStr, @"hh\:mm", null, out time) ||
               TimeSpan.TryParseExact(timeStr, @"h\:mm", null, out time);
    }

    private static string BuildCustomerConfirmationWithButtons(BookingData booking, string bookingId)
    {
        var arroz = string.IsNullOrWhiteSpace(booking.ArrozType)
            ? "Sin arroz"
            : (booking.ArrozServings.HasValue
                ? $"{booking.ArrozType} ({booking.ArrozServings} raciones)"
                : booking.ArrozType);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("*Confirmación de Reserva - Alquería Villa Carmen*");
        sb.AppendLine();
        sb.AppendLine($"Hola {booking.Name},");
        sb.AppendLine();
        sb.AppendLine("Gracias por elegir Alquería Villa Carmen. Su reserva ha sido confirmada:");
        sb.AppendLine();
        sb.AppendLine($"📅 *Fecha:* {booking.Date}");
        sb.AppendLine($"🕒 *Hora:* {booking.Time}");
        sb.AppendLine($"👥 *Personas:* {booking.People}");
        sb.AppendLine($"🍚 *Arroz:* {arroz}");
        sb.AppendLine($"👶 *Tronas:* {booking.HighChairs}");
        sb.AppendLine($"🍼 *Carros de bebé:* {booking.BabyStrollers}");
        sb.AppendLine();
        sb.AppendLine("Al hacer esta reserva, usted ha confirmado y aceptado las condiciones de reserva y políticas del restaurante, las cuales puede consultar en el botón de abajo.");

        return sb.ToString().TrimEnd();
    }

    private static string BuildAdminNewBookingNotification(BookingData booking, string bookingId)
    {
        var sb = new System.Text.StringBuilder();

        // Header
        sb.AppendLine("🤖 *NUEVA RESERVA - Asistente IA*");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine();

        // Booking ID
        if (!string.IsNullOrWhiteSpace(bookingId))
        {
            sb.AppendLine($"🆔 *ID:* #{bookingId}");
            sb.AppendLine();
        }

        // Customer info
        sb.AppendLine($"👤 *Cliente:* {booking.Name}");
        sb.AppendLine($"📱 *Teléfono:* {booking.Phone}");
        sb.AppendLine();

        // Booking details
        sb.AppendLine($"📅 *Fecha:* {booking.Date}");
        sb.AppendLine($"🕐 *Hora:* {booking.Time}");
        sb.AppendLine($"👥 *Personas:* {booking.People}");
        sb.AppendLine();

        // Rice info
        if (!string.IsNullOrWhiteSpace(booking.ArrozType))
        {
            var arrozDisplay = booking.ArrozServings.HasValue
                ? $"{booking.ArrozType} ({booking.ArrozServings} raciones)"
                : booking.ArrozType;
            sb.AppendLine($"🍚 *Arroz:* {arrozDisplay}");
        }
        else
        {
            sb.AppendLine("🍚 *Arroz:* Sin arroz");
        }

        // Extras (always show, even if 0)
        sb.AppendLine($"🪑 *Tronas:* {booking.HighChairs}");
        sb.AppendLine($"🚼 *Carritos:* {booking.BabyStrollers}");

        sb.AppendLine();
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine($"⏰ {DateTime.Now:dd/MM/yyyy HH:mm}");

        return sb.ToString().TrimEnd();
    }
}
