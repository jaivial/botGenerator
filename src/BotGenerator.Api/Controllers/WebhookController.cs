using System.Text.Json;
using System.Text.RegularExpressions;
using BotGenerator.Core.Agents;
using BotGenerator.Core.Handlers;
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using BotGenerator.Core.Services.TurnAnalysis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BotGenerator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    /// <summary>
    /// Management team phone numbers for booking notifications.
    /// </summary>
    private static readonly string[] ManagementPhones = new[]
    {
        "34692747052",
        "34638857294",
        "34686969914"
    };

    private readonly MainConversationAgent _mainAgent;
    private readonly IIntentRouterService _intentRouter;
    private readonly IConversationHistoryService _historyService;
    private readonly IAiStateExtractorService _aiStateExtractor;
    private readonly IPendingBookingStore _pendingBookingStore;
    private readonly IPendingRiceStore _pendingRiceStore;
    private readonly ICallAutoReplyStore _callAutoReplyStore;
    private readonly IWhatsAppService _whatsApp;
    private readonly IMenuRepository _menuRepository;
    private readonly IRiceValidatorService _riceValidator;
    private readonly IBookingAvailabilityService _availability;
    private readonly IBookingRepository _bookingRepository;
    private readonly BookingHandler _bookingHandler;
    private readonly IGeminiService _gemini;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<WebhookController> _logger;
    private readonly IConversationVectorStore _vectorStore;
    private readonly IMemoryCache _memoryCache;

    public WebhookController(
        MainConversationAgent mainAgent,
        IIntentRouterService intentRouter,
        IConversationHistoryService historyService,
        IAiStateExtractorService aiStateExtractor,
        IPendingBookingStore pendingBookingStore,
        IPendingRiceStore pendingRiceStore,
        ICallAutoReplyStore callAutoReplyStore,
        IWhatsAppService whatsApp,
        IMenuRepository menuRepository,
        IRiceValidatorService riceValidator,
        IBookingAvailabilityService availability,
        IBookingRepository bookingRepository,
        BookingHandler bookingHandler,
        IGeminiService gemini,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<WebhookController> logger,
        IConversationVectorStore vectorStore,
        IMemoryCache memoryCache)
    {
        _mainAgent = mainAgent;
        _intentRouter = intentRouter;
        _historyService = historyService;
        _aiStateExtractor = aiStateExtractor;
        _pendingBookingStore = pendingBookingStore;
        _pendingRiceStore = pendingRiceStore;
        _callAutoReplyStore = callAutoReplyStore;
        _whatsApp = whatsApp;
        _menuRepository = menuRepository;
        _riceValidator = riceValidator;
        _availability = availability;
        _bookingRepository = bookingRepository;
        _bookingHandler = bookingHandler;
        _gemini = gemini;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
        _vectorStore = vectorStore;
        _memoryCache = memoryCache;
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

            // Check event type - process messages and calls (ignore everything else)
            if (body.TryGetProperty("EventType", out var eventTypeProp))
            {
                var eventType = eventTypeProp.GetString();
                if (!string.IsNullOrWhiteSpace(eventType) && eventType != "messages")
                {
                    if (IsCallEventType(eventType))
                    {
                        return await HandleCallWebhookAsync(body, cancellationToken);
                    }

                    _logger.LogDebug("Ignoring non-message event: {EventType}", eventType);
                    return Ok();
                }
            }

            // Some providers may omit EventType; handle call-shaped payloads too.
            if (!body.TryGetProperty("message", out _) && body.TryGetProperty("call", out _))
            {
                return await HandleCallWebhookAsync(body, cancellationToken);
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

            // Media/unsupported payloads often come without text.
            // Reply once so the user knows how to continue instead of silently ignoring.
            if (string.IsNullOrWhiteSpace(message.MessageText))
            {
                if (IsUnsupportedMessageType(message.MessageType))
                {
                    const string unsupportedReply = "Ahora mismo solo puedo gestionar mensajes de texto. ¿Me lo puedes escribir por aquí?";
                    await _whatsApp.SendTextAsync(message.SenderNumber, unsupportedReply, cancellationToken);
                    return Ok(new { processed = true, unsupportedContent = true });
                }

                _logger.LogDebug("Ignoring empty text message");
                return Ok();
            }

            _logger.LogInformation(
                "Processing message from {Sender} ({Phone}): {Text}",
                message.PushName,
                message.SenderNumber,
                message.MessageText.Length > 100
                    ? message.MessageText[..100] + "..."
                    : message.MessageText);

            // Idempotent webhook handling (provider retries / duplicate deliveries)
            if (!string.IsNullOrWhiteSpace(message.MessageId))
            {
                var dedupeKey = $"webhook:wa:{message.SenderNumber}:{message.MessageId}";
                if (_memoryCache.TryGetValue(dedupeKey, out _))
                {
                    _logger.LogInformation("Duplicate webhook ignored for messageId={MessageId}", message.MessageId);
                    return Ok(new { processed = true, duplicate = true });
                }

                _memoryCache.Set(
                    dedupeKey,
                    1,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                    });
            }

            // 2. Get conversation history (bot-side memory)
            var history = await _historyService.GetHistoryAsync(
                message.SenderNumber, cancellationToken);

            var requestServices = HttpContext?.RequestServices;

            // 2a. FETCH EXISTING BOOKINGS for context
            // This allows the AI to know about user's active reservations
            var existingBookings = await _bookingRepository.FindBookingsByPhoneAsync(
                message.SenderNumber, cancellationToken);

            _logger.LogInformation(
                "Found {Count} existing bookings for {Phone}",
                existingBookings.Count, message.SenderNumber);

            await SyncBookingsToVectorStoreIfNeededAsync(
                message.SenderNumber,
                history,
                existingBookings,
                cancellationToken);

            // 2b. RICE OFFER RESPONSE (history-aware, deterministic)
            // Handle decline/deferral before AI calls and before same-day guardrails.
            var lastBotMsg = history.Where(m => m.Role == "assistant").LastOrDefault()?.Content ?? "";
            if (TurnClassifier.IsRiceOfferMessage(lastBotMsg))
            {
                if (TurnClassifier.IsRiceDecisionDeferral(message.MessageText))
                {
                    var reply = TurnClassifier.BuildRiceDeferralReply();

                    await _historyService.AddMessageAsync(
                        message.SenderNumber,
                        ChatMessage.FromUser(message.MessageText, message.PushName, message.MessageId, message.Timestamp),
                        cancellationToken);

                    await _whatsApp.SendTextAsync(message.SenderNumber, reply, cancellationToken);

                    await _historyService.AddMessageAsync(
                        message.SenderNumber,
                        ChatMessage.FromAssistant(reply),
                        cancellationToken);

                    return Ok(new { processed = true, riceOfferDeferred = true });
                }

                if (TurnClassifier.IsRiceOfferDecline(message.MessageText))
                {
                    var reply = ResponseVariations.RiceOfferDeclined();

                    await _historyService.AddMessageAsync(
                        message.SenderNumber,
                        ChatMessage.FromUser(message.MessageText, message.PushName, message.MessageId, message.Timestamp),
                        cancellationToken);

                    await _whatsApp.SendTextAsync(message.SenderNumber, reply, cancellationToken);

                    await _historyService.AddMessageAsync(
                        message.SenderNumber,
                        ChatMessage.FromAssistant(reply),
                        cancellationToken);

                    return Ok(new { processed = true, riceOfferDeclined = true });
                }
            }

            // 2b. EARLY CANCELLATION DETECTION (AI-based)
            // Check for cancellation intent BEFORE state extraction to avoid day-full checks
            CancellationHandler? cancellationHandler = null;
            ICancellationStateStore? cancellationStateStore = null;

            if (requestServices != null)
            {
                cancellationHandler = requestServices.GetRequiredService<CancellationHandler>();
                cancellationStateStore = requestServices.GetRequiredService<ICancellationStateStore>();
            }

            var cancellationState = cancellationStateStore?.Get(message.SenderNumber);

            // Use AI to detect cancellation intent (understands natural language variations)
            var isCancellationIntent = cancellationHandler != null &&
                (cancellationState != null ||
                 await DetectCancellationIntentAsync(message.MessageText, history, cancellationToken));

            // Route to cancellation if: active session OR AI detected cancellation intent
            if (isCancellationIntent)
            {
                _logger.LogInformation(
                    "Routing to cancellation flow (activeSession={HasSession}, aiDetected={AiDetected})",
                    cancellationState != null, cancellationState == null && isCancellationIntent);

                var cancellationResponse = await cancellationHandler!.ProcessCancellationAsync(
                    message, cancellationState, cancellationToken);

                // Store in history
                await _historyService.AddMessageAsync(
                    message.SenderNumber,
                    ChatMessage.FromUser(message.MessageText, message.PushName, message.MessageId, message.Timestamp),
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(cancellationResponse.AiResponse))
                {
                    // Send response to user
                    await _whatsApp.SendTextAsync(
                        message.SenderNumber,
                        cancellationResponse.AiResponse,
                        cancellationToken);

                    await _historyService.AddMessageAsync(
                        message.SenderNumber,
                        ChatMessage.FromAssistant(cancellationResponse.AiResponse),
                        cancellationToken);
                }

                return Ok(new { processed = true, cancellationFlow = true });
            }

            // 2d. EARLY RICE MODIFICATION DETECTION
            // If user wants to add rice to existing booking, route directly to modification
            var (isRiceModification, extractedRiceType, extractedServings) = DetectRiceModificationIntent(
                message.MessageText, existingBookings.Count);

            if (isRiceModification && existingBookings.Count > 0)
            {
                if (requestServices == null)
                {
                    _logger.LogDebug("Skipping early modification routing because RequestServices is unavailable");
                }

                _logger.LogInformation(
                    "Rice modification detected for {Phone}, rice type: {Rice}, servings: {Servings}, bookings: {Count}",
                    message.SenderNumber, extractedRiceType ?? "(to be specified)", extractedServings?.ToString() ?? "N/A", existingBookings.Count);

                var modificationHandler = requestServices?.GetService<ModificationHandler>();
                if (modificationHandler == null)
                {
                    _logger.LogDebug("ModificationHandler not available, continuing with normal flow");
                }
                else
                {

                    AgentResponse modResponse;
                    if (existingBookings.Count == 1)
                    {
                        // Single booking - start modification with rice field pre-selected
                        modResponse = await modificationHandler.StartRiceModificationAsync(
                            message,
                            existingBookings[0],
                            extractedRiceType,
                            extractedServings,
                            cancellationToken);
                    }
                    else
                    {
                        // Multiple bookings - start modification flow asking which booking
                        // Store the pre-extracted rice info for later use
                        modResponse = await modificationHandler.StartRiceModificationWithSelectionAsync(
                            message,
                            existingBookings,
                            extractedRiceType,
                            extractedServings,
                            cancellationToken);
                    }

                    // Store in history
                    await _historyService.AddMessageAsync(
                        message.SenderNumber,
                        ChatMessage.FromUser(message.MessageText, message.PushName, message.MessageId, message.Timestamp),
                        cancellationToken);

                    if (!string.IsNullOrWhiteSpace(modResponse.AiResponse))
                    {
                        var skipSend = modResponse.Metadata != null &&
                            modResponse.Metadata.TryGetValue("outboundAlreadySent", out var sentObj) &&
                            sentObj is bool already && already;

                        if (!skipSend)
                        {
                            await _whatsApp.SendTextAsync(
                                message.SenderNumber,
                                modResponse.AiResponse,
                                cancellationToken);
                        }

                        await _historyService.AddMessageAsync(
                            message.SenderNumber,
                            ChatMessage.FromAssistant(modResponse.AiResponse),
                            cancellationToken);
                    }

                    return Ok(new { processed = true, riceModification = true });
                }
            }

            // 2e. DETECT FORWARDED CONFIRMATION MESSAGE
            // When user forwards their confirmation, acknowledge it and offer help
            if (IsForwardedConfirmation(message.MessageText))
            {
                var (parsedDate, parsedTime, parsedPeople) = ParseForwardedConfirmation(message.MessageText);
                
                // Try to match to one of the user's existing bookings
                var matchedBooking = existingBookings.FirstOrDefault(b =>
                    b.DateFormatted == parsedDate && b.TimeFormatted == parsedTime);

                if (matchedBooking != null)
                {
                    _logger.LogInformation(
                        "Matched forwarded confirmation to booking {BookingId} for {Phone}",
                        matchedBooking.Id, message.SenderNumber);

                    var riceInfo = string.IsNullOrEmpty(matchedBooking.ArrozType)
                        ? "sin arroz"
                        : $"con {matchedBooking.ArrozType} ({matchedBooking.ArrozServings} raciones)";

                    var responseMsg = $"Veo que tienes una reserva confirmada para el *{matchedBooking.DateFormatted}* a las *{matchedBooking.TimeFormatted}* para *{matchedBooking.PartySize} personas*, {riceInfo}.\n\n" +
                                     "¿En qué puedo ayudarte? ¿Quieres modificar algo de tu reserva?";

                    await _historyService.AddMessageAsync(
                        message.SenderNumber,
                        ChatMessage.FromUser(message.MessageText, message.PushName, message.MessageId, message.Timestamp),
                        cancellationToken);

                    await _whatsApp.SendTextAsync(
                        message.SenderNumber,
                        responseMsg,
                        cancellationToken);

                    await _historyService.AddMessageAsync(
                        message.SenderNumber,
                        ChatMessage.FromAssistant(responseMsg),
                        cancellationToken);

                    return Ok(new { processed = true, forwardedConfirmation = true, matchedBookingId = matchedBooking.Id });
                }
                else
                {
                    // Forwarded confirmation detected but no matching booking found
                    // This could happen if the booking was cancelled or the confirmation is old
                    _logger.LogInformation(
                        "Forwarded confirmation detected for {Phone} but no matching booking (parsed: {Date} {Time})",
                        message.SenderNumber, parsedDate, parsedTime);

                    string responseMsg;
                    if (existingBookings.Count > 0)
                    {
                        // User has other bookings
                        var bookingsSummary = string.Join("\n", existingBookings.Select(b =>
                            $"• *{b.DateFormatted}* a las *{b.TimeFormatted}* para {b.PartySize} personas"));

                        responseMsg = $"He visto tu mensaje de confirmación, pero no encuentro esa reserva exacta en el sistema.\n\n" +
                                     $"Estas son tus reservas activas:\n{bookingsSummary}\n\n" +
                                     "¿En qué puedo ayudarte?";
                    }
                    else
                    {
                        // User has no bookings at all
                        responseMsg = "He visto tu mensaje de confirmación, pero no encuentro ninguna reserva activa asociada a tu número.\n\n" +
                                     "Es posible que la reserva haya sido cancelada o que se hiciera con otro número de teléfono.\n\n" +
                                     "¿Te gustaría hacer una nueva reserva?";
                    }

                    await _historyService.AddMessageAsync(
                        message.SenderNumber,
                        ChatMessage.FromUser(message.MessageText, message.PushName, message.MessageId, message.Timestamp),
                        cancellationToken);

                    await _whatsApp.SendTextAsync(
                        message.SenderNumber,
                        responseMsg,
                        cancellationToken);

                    await _historyService.AddMessageAsync(
                        message.SenderNumber,
                        ChatMessage.FromAssistant(responseMsg),
                        cancellationToken);

                    return Ok(new { processed = true, forwardedConfirmation = true, matched = false });
                }
            }

            // 3. Extract conversation state using AI (more robust than regex)
            // AI understands natural language variations like "nah", "ninguna", "sin tronas", etc.
            var historyForState = history
                .Append(ChatMessage.FromUser(message.MessageText, message.PushName, message.MessageId, message.Timestamp))
                .ToList();

            var state = await _aiStateExtractor.ExtractStateAsync(historyForState, cancellationToken);

            // 3b. Apply pre-checks (availability + rice constraints) before calling Gemini
            // Pass existingBookings.Count so pre-checks can detect modification context
            var restaurantId = GetRestaurantId(message.SenderNumber);
            var precheck = await TryHandlePreChecksAsync(
                restaurantId,
                message,
                state,
                existingBookings.Count,
                history,
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
                    ChatMessage.FromUser(message.MessageText, message.PushName, message.MessageId, message.Timestamp),
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
            var isDeclining = IsUserDeclining(message.MessageText);
            var isReady = IsReadyToBook(state);
            var pendingBooking = _pendingBookingStore.Get(message.SenderNumber);
            var summaryWasShown = pendingBooking?.SummaryShown ?? false;

            if (isConfirming)
            {
                _logger.LogInformation(
                    "Confirm gate: confirming={Confirming} ready={Ready} summaryShown={SummaryShown} state(fecha={Fecha}, hora={Hora}, personas={Personas}, arrozType={ArrozType}, arrozServings={ArrozServings}, tronas={Tronas}, carritos={Carritos})",
                    isConfirming,
                    isReady,
                    summaryWasShown,
                    state.Fecha,
                    state.Hora,
                    state.Personas,
                    state.ArrozType,
                    state.ArrozServings,
                    state.HighChairs,
                    state.BabyStrollers);
            }

            // Handle decline after seeing summary
            if (isDeclining && summaryWasShown)
            {
                _logger.LogInformation("User declined booking after seeing summary for {Phone}", message.SenderNumber);
                _pendingBookingStore.Clear(message.SenderNumber);

                await _historyService.AddMessageAsync(
                    message.SenderNumber,
                    ChatMessage.FromUser(message.MessageText, message.PushName, message.MessageId, message.Timestamp),
                    cancellationToken);

                var declineResponse = "Entendido, he cancelado la reserva. ¿Te gustaría empezar de nuevo o necesitas algo más?";
                await _whatsApp.SendTextAsync(message.SenderNumber, declineResponse, cancellationToken);

                await _historyService.AddMessageAsync(
                    message.SenderNumber,
                    ChatMessage.FromAssistant(declineResponse),
                    cancellationToken);

                return Ok(new { processed = true, bookingDeclined = true });
            }

            // Only create booking if user confirms AND summary was shown
            if (isConfirming && isReady && summaryWasShown)
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
                    BabyStrollers = Math.Clamp(state.BabyStrollers ?? 0, 0, 3),
                    SummaryShown = true
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
                    await SendToManagementTeamAsync(adminText2, cancellationToken);

                    // Store history for deterministic booking
                    await _historyService.AddMessageAsync(
                        message.SenderNumber,
                        ChatMessage.FromUser(message.MessageText, message.PushName, message.MessageId, message.Timestamp),
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
                    ChatMessage.FromUser(message.MessageText, message.PushName, message.MessageId, message.Timestamp),
                    cancellationToken);
                await _historyService.AddMessageAsync(
                    message.SenderNumber,
                    ChatMessage.FromAssistant(finalResponseDirect.AiResponse),
                    cancellationToken);

                return Ok(new { processed = true, deterministic = true });
            }

            // 4. Process with main agent
            // Pass existing bookings context so AI knows about user's reservations
            var agentResponse = await _mainAgent.ProcessAsync(
                message, state, history, existingBookings, cancellationToken) ?? AgentResponse.Error("Main agent returned null");

            // 5. Route based on intent
            var finalResponse = await _intentRouter.RouteAsync(
                agentResponse, message, state, cancellationToken) ?? agentResponse;

            // 5b. Store conversation history AFTER routing (so the FINAL response is stored)
            // This is critical for multi-turn flows like tronas/carritos where IntentRouter
            // replaces the AI response with hardcoded questions.
            await _historyService.AddMessageAsync(
                message.SenderNumber,
                ChatMessage.FromUser(message.MessageText, message.PushName, message.MessageId, message.Timestamp),
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

                // Notify management team
                var adminText = BuildAdminNewBookingNotification(finalResponse.ExtractedData, bookingId);
                await SendToManagementTeamAsync(adminText, cancellationToken);

                // Store assistant response (booking confirmation)
                await _historyService.AddMessageAsync(
                    message.SenderNumber,
                    ChatMessage.FromAssistant(customerText),
                    cancellationToken);

                return Ok(new { processed = true, bookingCreated = true, bookingId });
            }

            if (!string.IsNullOrWhiteSpace(finalResponse.AiResponse))
            {
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
            }
            else
            {
                _logger.LogDebug("Skipping outbound send for empty final response ({Phone})", message.SenderNumber);
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
            if (tsProp.ValueKind == JsonValueKind.Number && tsProp.TryGetInt64(out var tsNumeric))
            {
                timestamp = tsNumeric;
            }
            else if (tsProp.ValueKind == JsonValueKind.String && long.TryParse(tsProp.GetString(), out var tsString))
            {
                timestamp = tsString;
            }
        }

        // Get external message ID for deduplication
        string? messageId = null;
        if (messageBody.TryGetProperty("messageid", out var messageIdProp) && messageIdProp.ValueKind == JsonValueKind.String)
        {
            messageId = messageIdProp.GetString();
        }
        else if (messageBody.TryGetProperty("messageId", out var messageIdCamelProp) && messageIdCamelProp.ValueKind == JsonValueKind.String)
        {
            messageId = messageIdCamelProp.GetString();
        }
        else if (messageBody.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
        {
            messageId = idProp.GetString();
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
            MessageId = messageId,
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

    private async Task SyncBookingsToVectorStoreIfNeededAsync(
        string phoneNumber,
        List<ChatMessage> history,
        List<BookingRecord> bookings,
        CancellationToken cancellationToken)
    {
        if (!_configuration.GetValue("Chroma:Enabled", true))
            return;

        if (bookings.Count == 0)
            return;

        if (HistoryContainsBookingConfirmation(history))
            return;

        try
        {
            foreach (var b in bookings)
            {
                await _vectorStore.UpsertBookingAsync(phoneNumber, b, cancellationToken);
            }

            _logger.LogInformation(
                "Synced {Count} booking document(s) to Chroma for {Phone} (no confirmation text in recent history)",
                bookings.Count,
                phoneNumber);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync bookings to Chroma for {Phone}", phoneNumber);
        }
    }

    private async Task<(bool Handled, string StoredAssistantText, ConversationState UpdatedState)> TryHandlePreChecksAsync(
        string restaurantId,
        WhatsAppMessage message,
        ConversationState state,
        int existingBookingsCount,
        List<ChatMessage> history,
        CancellationToken cancellationToken)
    {
        var updatedState = state;

        var isModificationContinuation = IsLikelyModificationContinuation(
            message.MessageText,
            history,
            existingBookingsCount);

        // === MODIFICATION CONTEXT DETECTION ===
        // If user has existing bookings and is talking about their reservation,
        // skip the pre-checks and let the AI route to modification flow
        if (IsModificationContext(message.MessageText, existingBookingsCount) || isModificationContinuation)
        {
            _logger.LogInformation(
                "Modification context detected for {Phone} (has {Count} bookings), skipping pre-checks",
                message.SenderNumber, existingBookingsCount);
            return (false, "", updatedState);
        }

        // Generic rice-modification requests (without concrete rice type) should route naturally
        // to modification flow instead of being rejected by menu rice validation pre-checks.
        if (IsGenericRiceModificationRequest(message.MessageText))
        {
            _logger.LogInformation(
                "Generic rice modification request detected for {Phone}, skipping rice pre-check validation",
                message.SenderNumber);
            return (false, "", updatedState);
        }

        // === EXISTING CUSTOMER: info / small talk (avoid treating stale state as a new slot request) ===
        if (IsExistingCustomerSupportOrInfoMessage(message.MessageText, existingBookingsCount))
        {
            _logger.LogInformation(
                "Existing-customer info/support message for {Phone}, skipping availability pre-checks",
                message.SenderNumber);
            return (false, "", updatedState);
        }

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
        var extractedPartySize = TryExtractPartySizeFromMessage(message.MessageText, history);
        var extractedTime = TryExtractTimeFromMessage(message.MessageText);

        // In active modification flow, short numeric replies ("1", "4") are often menu/servings inputs,
        // not new party-size declarations.
        if (isModificationContinuation && Regex.IsMatch(message.MessageText.Trim(), @"^\d{1,2}$"))
        {
            extractedPartySize = null;
        }

        // Determine effective values (from message or state)
        DateTime? effectiveDate = extractedDate?.Date ?? (state.Fecha != null ? ParseDateFromState(state.Fecha) : null);
        int? effectivePartySize = extractedPartySize ?? state.Personas;
        TimeSpan? effectiveTime = extractedTime ?? (state.Hora != null ? ParseTimeFromState(state.Hora) : null);

        var newBookingSignalsFromMessage = extractedDate.HasValue || extractedPartySize.HasValue || extractedTime.HasValue
            || MessageLooksLikeNewBookingIntent(message.MessageText);
        var pendingNewBooking = _pendingBookingStore.Get(message.SenderNumber) != null;
        var skipSlotPrechecksForReturningCustomer = existingBookingsCount > 0
            && !pendingNewBooking
            && !newBookingSignalsFromMessage;

        if (skipSlotPrechecksForReturningCustomer)
        {
            _logger.LogDebug(
                "Skipping date/slot availability pre-checks for {Phone} (returning customer, no new-booking signals in message)",
                message.SenderNumber);
        }

        // === DATE VALIDATION (when date is mentioned or when party size/time changes for existing date) ===
        if (!skipSlotPrechecksForReturningCustomer &&
            effectiveDate.HasValue && effectiveDate.Value > DateTime.Now.Date)
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
        var riceDeclined = DeclinesRice(message.MessageText, history);

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
            if (riceDeclined)
            {
                _pendingRiceStore.Clear(message.SenderNumber);
                updatedState = updatedState with { ArrozType = "", ArrozServings = null, PendingRiceOptions = null };
                _logger.LogInformation("User declined rice while selecting pending rice options");
            }
            else
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
                    // User didn't select a valid option, ask again
                    _logger.LogInformation("Could not parse rice selection from: {Message}", message.MessageText);
                    var formattedOptions = string.Join("\n", pendingRice.Options.Select((r, i) => $"{i + 1}. {r}"));
                    var retryMsg = $"No he entendido tu elección. Por favor, dime el número de la opción que prefieres:\n\n{formattedOptions}";
                    await _whatsApp.SendTextAsync(message.SenderNumber, retryMsg, cancellationToken);
                    return (true, retryMsg, updatedState);
                }
            }
        }

        if (riceDeclined)
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
            // Use smart batching to ask for multiple missing fields at once.
            if (!string.IsNullOrWhiteSpace(updatedState.ArrozType))
            {
                // Collect all missing basic fields
                var missingBasics = new List<string>();
                if (string.IsNullOrWhiteSpace(updatedState.Fecha)) missingBasics.Add("fecha");
                if (string.IsNullOrWhiteSpace(updatedState.Hora)) missingBasics.Add("hora");
                if (!updatedState.Personas.HasValue || updatedState.Personas.Value <= 0) missingBasics.Add("personas");

                // Smart batching: ask for multiple missing basic fields at once
                if (missingBasics.Count >= 2)
                {
                    var questionParts = missingBasics.Select(f => f switch
                    {
                        "fecha" => "qué *día*",
                        "hora" => "a qué *hora*",
                        "personas" => "*cuántas personas*",
                        _ => f
                    }).ToList();

                    var question = missingBasics.Count == 3
                        ? $"¿Para {questionParts[0]}, {questionParts[1]} y {questionParts[2]}?"
                        : $"¿Para {questionParts[0]} y {questionParts[1]}?";

                    var msg = $"✅ {updatedState.ArrozType} disponible. {question}";
                    await _whatsApp.SendTextAsync(message.SenderNumber, msg, cancellationToken);
                    return (true, msg, updatedState);
                }

                // Single missing basic field
                if (missingBasics.Count == 1)
                {
                    var msg = missingBasics[0] switch
                    {
                        "fecha" => $"✅ {updatedState.ArrozType} disponible. ¿Para qué *día* sería la reserva?",
                        "hora" => $"✅ {updatedState.ArrozType} disponible. ¿A qué *hora* os viene bien?",
                        "personas" => $"✅ {updatedState.ArrozType} disponible. ¿Para cuántas *personas* sería?",
                        _ => $"✅ {updatedState.ArrozType} disponible."
                    };
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

    private static bool DeclinesRice(string text, List<ChatMessage>? history = null)
    {
        var t = text.ToLowerInvariant().Trim();

        var explicitDecline = t.Contains("sin arroz") ||
                              t.Contains("no quiero arroz") ||
                              t.Contains("no queremos arroz") ||
                              t.Contains("no, sin arroz") ||
                              t.Contains("nada de arroz") ||
                              Regex.IsMatch(t, @"\bno\s+al\s+arroz\b", RegexOptions.IgnoreCase);

        if (explicitDecline)
            return true;

        // A plain "no" should only be interpreted as rice rejection when the latest
        // assistant turn was explicitly asking about rice.
        if (t == "no")
            return WasLatestAssistantAskingAboutRice(history);

        return false;
    }

    private static bool WasLatestAssistantAskingAboutRice(List<ChatMessage>? history)
    {
        if (history == null || history.Count == 0)
            return false;

        var lastAssistant = history
            .Where(m => m.Role == "assistant")
            .Select(m => m.Content)
            .LastOrDefault();

        if (string.IsNullOrWhiteSpace(lastAssistant))
            return false;

        return Regex.IsMatch(
            lastAssistant,
            @"(quer[eé]is|quieres|a[ñn]adir|a[ñn]adamos|reservar|apetece).*(arroz)|arroz.*\?",
            RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Detects if the user's message is about modifying an existing booking.
    /// Returns true if modification keywords are present AND user has existing bookings.
    /// </summary>
    private static bool IsModificationContext(string messageText, int existingBookingsCount)
    {
        if (existingBookingsCount == 0) return false;

        var text = messageText.ToLowerInvariant();

        // Patterns that indicate modification of existing booking
        // "añadir arroz a mi reserva", "incluir arroz en la reserva", etc.
        if (System.Text.RegularExpressions.Regex.IsMatch(text,
            @"(añadir|incluir|agregar|poner).*(mi\s+)?reserva"))
            return true;

        // "mi reserva", "la reserva", "esta reserva" (referencing existing booking)
        if (System.Text.RegularExpressions.Regex.IsMatch(text,
            @"(mi|la|esta)\s+reserva"))
            return true;

        // "modificar la reserva", "cambiar la hora", etc.
        if (System.Text.RegularExpressions.Regex.IsMatch(text,
            @"(modificar|cambiar).*(reserva|arroz|fecha|hora|personas)"))
            return true;

        // "para mi reserva", "a mi reserva", "en mi reserva"
        if (System.Text.RegularExpressions.Regex.IsMatch(text,
            @"(para|a|en)\s+(mi|la)\s+reserva"))
            return true;

        // "de los 6" pattern from the example: "arroz a banda para 4 de los 6"
        // This indicates user knows how many people are in their existing booking
        if (System.Text.RegularExpressions.Regex.IsMatch(text,
            @"para\s+\d+\s+de\s+(los|las)\s+\d+"))
            return true;

        return false;
    }

    private static bool IsGenericRiceModificationRequest(string messageText)
    {
        var text = messageText.ToLowerInvariant();
        var hasModificationVerb = Regex.IsMatch(text, @"\b(modificar|cambiar|añadir|anadir|agregar|incluir|poner)\b");
        return hasModificationVerb && IsGenericRiceReference(text);
    }

    private static bool IsGenericRiceReference(string text)
    {
        var normalized = Regex.Replace(text.ToLowerInvariant(), @"\s+", " ").Trim();

        if (!Regex.IsMatch(normalized, @"\b(arroz|paella|fideu[aá]?)\b"))
            return false;

        if (Regex.IsMatch(normalized, @"\b\d+\s*raciones?\b"))
            return false;

        return !ContainsSpecificRiceDescriptor(normalized);
    }

    private static bool ContainsSpecificRiceDescriptor(string text)
    {
        var hasNamedRice = Regex.IsMatch(
            text,
            @"\b(arroz|paella|fideu[aá]?)\s+(a\s+la|al|del?|de|con)?\s*[a-záéíóúñ]{3,}\b");

        var isReservationReference = Regex.IsMatch(
            text,
            @"\b(arroz|paella|fideu[aá]?)\s+(de\s+)?(mi|la|esta)\s+reserva\b");

        var hasKnownStyleKeyword = Regex.IsMatch(
            text,
            @"\b(a\s*banda|señoret|señorito|negro|valencian[oa]?|bogavante|marisco|mixto|meloso|caldoso|abanda)\b");

        return (hasNamedRice && !isReservationReference) || hasKnownStyleKeyword;
    }

    private static bool IsLikelyModificationContinuation(
        string messageText,
        List<ChatMessage> history,
        int existingBookingsCount)
    {
        if (existingBookingsCount == 0 || history.Count == 0)
            return false;

        var text = messageText.Trim().ToLowerInvariant();
        var isShortNumericReply = Regex.IsMatch(text, @"^\d{1,2}$");
        var isTimeReply = Regex.IsMatch(text, @"\b\d{1,2}:\d{2}\b") || text.StartsWith("a las ");
        var mentionsRiceOrServings = Regex.IsMatch(text, @"\b(arroz|paella|fideu[aá]?|raci[oó]n|raciones)\b");

        if (!isShortNumericReply && !isTimeReply && !mentionsRiceOrServings)
            return false;

        var lastAssistant = history
            .Where(m => m.Role == "assistant")
            .Select(m => m.Content.ToLowerInvariant())
            .LastOrDefault() ?? string.Empty;

        var assistantLooksLikeModificationFlow =
            lastAssistant.Contains("qué quieres modificar") ||
            lastAssistant.Contains("que quieres modificar") ||
            lastAssistant.Contains("qué necesitas cambiar") ||
            lastAssistant.Contains("que necesitas cambiar") ||
            lastAssistant.Contains("reserva actual") ||
            lastAssistant.Contains("cuántas raciones") ||
            lastAssistant.Contains("cuantas raciones") ||
            lastAssistant.Contains("tipo de arroz") ||
            lastAssistant.Contains("modificación") ||
            lastAssistant.Contains("modificar");

        if (assistantLooksLikeModificationFlow)
            return true;

        var recentWindow = history
            .TakeLast(4)
            .Select(m => m.Content.ToLowerInvariant())
            .ToList();

        return recentWindow.Any(c =>
            c.Contains("modificar") ||
            c.Contains("cambiar la reserva") ||
            c.Contains("añadir arroz") ||
            c.Contains("anadir arroz"));
    }

    /// <summary>
    /// Detects if user wants to add/modify rice on their existing booking.
    /// Returns extracted rice type if found.
    /// Note: The extracted rice type is a rough extraction - it will be validated/normalized
    /// by RiceValidatorAgent in the ModificationHandler.
    /// </summary>
    private static (bool IsRiceModification, string? RiceType, int? Servings) DetectRiceModificationIntent(
        string messageText,
        int existingBookingsCount)
    {
        if (existingBookingsCount == 0) return (false, null, null);

        var text = messageText.ToLowerInvariant();

        // Guard: if user is clearly trying to create a NEW booking, don't force modification.
        if (Regex.IsMatch(text, @"\b(reservar|nueva\s+reserva|hacer\s+una\s+reserva)\b", RegexOptions.IgnoreCase) &&
            !Regex.IsMatch(text, @"\b(modificar|cambiar|a[ñn]adir|incluir|agregar|poner)\b", RegexOptions.IgnoreCase))
        {
            return (false, null, null);
        }

        // Check for rice + reservation context combination
        var hasRiceKeyword = System.Text.RegularExpressions.Regex.IsMatch(text,
            @"\b(arroz|paella|fideu[aá]?|meloso|caldoso|banda|señoret|señorito|bogavante|negro|albufera|chorizo|mariscos?)\b");

        // Reservation context patterns (explicitly tied to an existing reservation)
        var hasReservationContext = System.Text.RegularExpressions.Regex.IsMatch(text,
            @"(añadir|incluir|agregar|poner|modificar|cambiar).*(reserva|para\s+\d+\s+de)|" +
            @"(mi|la|esta)\s+reserva|" +
            @"(en|a|para)\s+(mi|la)\s+reserva|" +
            @"para\s+\d+\s+de\s+(los|las)\s+\d+|" +
            @"(podría|podria|puedo).*(incluir|añadir|agregar|poner).*(reserva)?|" +
            @"(modificar|cambiar).*(arroz|paella|fideu[aá]?)");

        if (hasRiceKeyword && hasReservationContext)
        {
            // Try to extract the rice type with improved patterns
            string? riceType = null;

            // Pattern 1: "arroz a banda", "arroz del señoret", "arroz meloso con..."
            var riceMatch = System.Text.RegularExpressions.Regex.Match(text,
                @"(arroz\s+(?:a\s+la\s+|del?\s+|al?\s+)?[a-záéíóúñ]+(?:\s+(?:con|de)\s+[a-záéíóúñ]+)?)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (riceMatch.Success)
            {
                riceType = riceMatch.Value.Trim();
            }
            else
            {
                // Pattern 2: "paella valenciana", "paella de marisco"
                riceMatch = System.Text.RegularExpressions.Regex.Match(text,
                    @"(paella\s+(?:de\s+)?[a-záéíóúñ]+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (riceMatch.Success)
                {
                    riceType = riceMatch.Value.Trim();
                }
                else
                {
                    // Pattern 3: "fideuá", "fideuà de marisco"
                    riceMatch = System.Text.RegularExpressions.Regex.Match(text,
                        @"(fideu[aá]\s*(?:de\s+)?[a-záéíóúñ]*)",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (riceMatch.Success)
                    {
                        riceType = riceMatch.Value.Trim();
                    }
                }
            }

            // Clean up extracted rice type (remove extra spaces, normalize)
            if (riceType != null)
            {
                riceType = System.Text.RegularExpressions.Regex.Replace(riceType, @"\s+", " ").Trim();
                // Remove trailing prepositions if they got captured
                riceType = System.Text.RegularExpressions.Regex.Replace(riceType, @"\s+(para|en|a)$", "").Trim();

                if (IsGenericRiceReference(riceType))
                {
                    riceType = null;
                }
            }

            // Try to extract servings if mentioned
            int? servings = null;
            
            // Pattern: "para 4 de los 6", "para 4 personas", "4 raciones"
            var servingsMatch = System.Text.RegularExpressions.Regex.Match(text,
                @"(?:para\s+)?(\d+)\s+(?:de\s+(?:los|las)\s+\d+|personas?|raciones?)");
            if (servingsMatch.Success && int.TryParse(servingsMatch.Groups[1].Value, out var s))
            {
                servings = s;
            }
            else
            {
                // Also try just a number at the end: "arroz a banda para 4"
                var simpleServings = System.Text.RegularExpressions.Regex.Match(text,
                    @"para\s+(\d+)(?:\s|$)");
                if (simpleServings.Success && int.TryParse(simpleServings.Groups[1].Value, out var s2))
                {
                    servings = s2;
                }
            }

            return (true, riceType, servings);
        }

        return (false, null, null);
    }

    private static bool HistoryContainsBookingConfirmation(List<ChatMessage> history)
    {
        foreach (var m in history.TakeLast(40))
        {
            var c = m.Content ?? "";
            if (IsForwardedConfirmation(c))
                return true;

            if (c.Contains("Confirmación de Reserva", StringComparison.OrdinalIgnoreCase) ||
                c.Contains("Confirmacion de Reserva", StringComparison.OrdinalIgnoreCase))
                return true;

            if (c.Contains("Su reserva ha sido confirmada", StringComparison.OrdinalIgnoreCase) ||
                c.Contains("reserva ha sido confirmada", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool MessageLooksLikeNewBookingIntent(string messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText))
            return false;

        return Regex.IsMatch(
            messageText.Trim(),
            @"\b(quiero|quisiera|necesito|podr[ií]a|puedo|vamos\s+a|me\s+gustar[ií]a|hacer|hago|solicito)\s+.*\b(reservar|reserva|mesa|mesas|booking)\b|" +
            @"\b(nueva\s+reserva|otra\s+reserva|reservar\s+(una\s+)?mesa|mesa\s+para|book(ing)?\s+for)\b|" +
            @"\b(disponibilidad|hueco|plaza[s]?)\s+para\s+\d+",
            RegexOptions.IgnoreCase);
    }

    private static bool IsExistingCustomerSupportOrInfoMessage(string messageText, int existingBookingsCount)
    {
        if (existingBookingsCount == 0 || string.IsNullOrWhiteSpace(messageText))
            return false;

        if (MessageLooksLikeNewBookingIntent(messageText))
            return false;

        var t = messageText.Trim().ToLowerInvariant();

        if (Regex.IsMatch(t, @"^(hola|hi|hey|buenas|buenos)\b[^a-záéíóúñ]{0,15}$"))
            return true;

        if (Regex.IsMatch(t, @"\b(una\s+pregunta|tengo\s+una\s+pregunta|solo\s+una\s+pregunta)\b"))
            return true;

        if (Regex.IsMatch(t, @"\b(tengo|tenemos)\s+(una\s+)?reserva\b"))
            return true;

        if (Regex.IsMatch(t, @"\b(ten[eé]is|tienen|hay)\s+(men[uú]|menu|carta)\b"))
            return true;

        if (Regex.IsMatch(t, @"\b(men[uú]|menu|carta|precios?|horario|d[oó]nde\s+est[aá]is|aparcamiento|parking)\b"))
            return true;

        return false;
    }

    /// <summary>
    /// Detects if the message is a forwarded booking confirmation.
    /// Returns true if the message contains confirmation markers.
    /// </summary>
    private static bool IsForwardedConfirmation(string messageText)
    {
        // Check for confirmation message markers (with or without emojis)
        return (messageText.Contains("Confirmación de Reserva") ||
                messageText.Contains("Confirmacion de Reserva")) &&
               (messageText.Contains("Alquería Villa Carmen") ||
                messageText.Contains("Alqueria Villa Carmen"));
    }

    /// <summary>
    /// Parses booking details from a forwarded confirmation message.
    /// Returns parsed date/time if successful.
    /// </summary>
    private static (string? Date, string? Time, int? People) ParseForwardedConfirmation(string messageText)
    {
        string? date = null;
        string? time = null;
        int? people = null;

        // Extract date (pattern: "Fecha: 07/02/2026" with or without emoji)
        var dateMatch = System.Text.RegularExpressions.Regex.Match(
            messageText, @"Fecha:\s*(\d{2}/\d{2}/\d{4})");
        if (dateMatch.Success)
        {
            date = dateMatch.Groups[1].Value;
        }

        // Extract time (pattern: "Hora: 15:00" with or without emoji)
        var timeMatch = System.Text.RegularExpressions.Regex.Match(
            messageText, @"Hora:\s*(\d{2}:\d{2})");
        if (timeMatch.Success)
        {
            time = timeMatch.Groups[1].Value;
        }

        // Extract people count (pattern: "Personas: 6" with or without emoji)
        var peopleMatch = System.Text.RegularExpressions.Regex.Match(
            messageText, @"Personas:\s*(\d+)");
        if (peopleMatch.Success && int.TryParse(peopleMatch.Groups[1].Value, out var p))
        {
            people = p;
        }

        return (date, time, people);
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
        return SameDayDetector.IsSameDayBookingRequest(text);
    }

    private static bool IsCallEventType(string eventType)
        => eventType.Contains("call", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnsupportedMessageType(string? messageType)
    {
        if (string.IsNullOrWhiteSpace(messageType))
            return false;

        return messageType.Equals("audio", StringComparison.OrdinalIgnoreCase) ||
               messageType.Equals("image", StringComparison.OrdinalIgnoreCase) ||
               messageType.Equals("video", StringComparison.OrdinalIgnoreCase) ||
               messageType.Equals("document", StringComparison.OrdinalIgnoreCase) ||
               messageType.Equals("sticker", StringComparison.OrdinalIgnoreCase) ||
               messageType.Equals("location", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IActionResult> HandleCallWebhookAsync(
        JsonElement body,
        CancellationToken cancellationToken)
    {
        var (phone, callId) = ExtractCallInfo(body);
        if (string.IsNullOrWhiteSpace(phone))
        {
            _logger.LogDebug("Call event received but could not extract phone, ignoring");
            return Ok();
        }

        // Always attempt to reject the call.
        await _whatsApp.RejectCallAsync(phone, callId, cancellationToken);

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

        await _whatsApp.SendTextAsync(phone, text, cancellationToken);

        // Global contact card (per current desired behavior). Keep digits compact for provider compatibility.
        await _whatsApp.SendContactCardAsync(
            phone,
            fullName: "Gestión Reservas",
            contactPhoneNumber: "+34638857294",
            organization: "Alquería Villa Carmen",
            cancellationToken: cancellationToken);

        return Ok(new { processed = true, call = true, replied = true });
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

        // Check for "pasado mañana"
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"\bpasado\s+ma[ñn]ana\b"))
        {
            return today.AddDays(2);
        }

        // Check for "mañana"
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"\bma[ñn]ana\b"))
        {
            return today.AddDays(1);
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
    private static int? TryExtractPartySizeFromMessage(string text, List<ChatMessage>? history = null)
    {
        var t = text.ToLowerInvariant();

        // Pattern: "X personas" or "somos X"
        var patterns = new[]
        {
            @"(\d+)\s*personas?",
            @"somos\s*(\d+)",
            @"seremos\s*(\d+)",
            @"mesa\s*(?:para|de)\s*(\d+)",
            @"para\s*(\d+)\s*(?:personas?|comensales?|adultos?)\b",
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

        // Fallback: if the entire message is just a number, only accept it when
        // the previous assistant prompt was explicitly asking for party size.
        var trimmed = t.Trim();
        if (int.TryParse(trimmed, out var bareNumber) &&
            bareNumber > 0 &&
            bareNumber <= 50 &&
            WasLatestAssistantAskingForPartySize(history))
        {
            return bareNumber;
        }

        return null;
    }

    private static bool WasLatestAssistantAskingForPartySize(List<ChatMessage>? history)
    {
        if (history == null || history.Count == 0)
            return false;

        var lastAssistant = history
            .Where(m => m.Role == "assistant")
            .Select(m => m.Content)
            .LastOrDefault();

        if (string.IsNullOrWhiteSpace(lastAssistant))
            return false;

        return Regex.IsMatch(
            lastAssistant,
            @"(cu[aá]nt[oa]s?\s+(personas?|comensales?)|para\s+cu[aá]nt[oa]s?|n[úu]mero\s+de\s+personas?)",
            RegexOptions.IgnoreCase);
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

        if (IsUserDeclining(t))
            return false;

        if (t == "si" || t == "sí" || t == "ok" || t == "vale" || t == "perfecto" || t == "adelante")
            return true;

        return Regex.IsMatch(
                   t,
                   @"\b(confirmo|confirmar|si,?\s*confirmo|sí,?\s*confirmo|de\s+acuerdo)\b",
                   RegexOptions.IgnoreCase) &&
               !Regex.IsMatch(t, @"\bno\b", RegexOptions.IgnoreCase);
    }

    private static bool IsUserDeclining(string text)
    {
        var t = (text ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(t)) return false;

        if (t == "no" || t == "cancelar" || t == "anular" || t == "dejalo" || t == "déjalo")
            return true;

        return Regex.IsMatch(
            t,
            @"\b(no\s+confirmo|no\s+quiero|no\s+estoy\s+de\s+acuerdo|mejor\s+no|cancela|cancelar|anula|anular)\b",
            RegexOptions.IgnoreCase);
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

    /// <summary>
    /// Sends a message to all management team phone numbers.
    /// </summary>
    private async Task SendToManagementTeamAsync(string message, CancellationToken ct)
    {
        foreach (var phone in ManagementPhones)
        {
            try
            {
                await _whatsApp.SendTextAsync(phone, message, ct);
                _logger.LogDebug("Sent notification to management phone {Phone}", phone);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification to management phone {Phone}", phone);
            }
        }
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

    /// <summary>
    /// Uses AI to detect if the user message expresses cancellation intent.
    /// Much more robust than regex - understands natural language variations.
    /// </summary>
    private async Task<bool> DetectCancellationIntentAsync(
        string messageText,
        IEnumerable<ChatMessage> history,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(messageText))
            return false;

        try
        {
            // Build context from recent history (last 3 messages for context)
            var recentHistory = history.TakeLast(3).ToList();
            var contextSummary = recentHistory.Count > 0
                ? string.Join("\n", recentHistory.Select(m => $"{m.Role}: {m.Content}"))
                : "(sin historial previo)";

            var systemPrompt = @"Eres un detector de intenciones para un restaurante. Tu ÚNICA tarea es determinar si el mensaje del cliente indica que quiere CANCELAR una reserva existente.

Responde SOLO con: YES o NO

Ejemplos de CANCELACIÓN (responde YES):
- ""Quiero cancelar mi reserva""
- ""Cancela la reserva""
- ""No voy a poder ir""
- ""Al final no vamos a ir""
- ""Lo siento pero no puedo asistir""
- ""Me ha surgido algo y no podré ir""
- ""Tengo que anular la reserva""
- ""Ya no voy a ir""
- ""Cancelar""
- ""No iré""
- ""Ha surgido un imprevisto""

Ejemplos de NO cancelación (responde NO):
- ""Quiero reservar mesa""
- ""Para 4 personas""
- ""El sábado a las 14:00""
- ""Sí, confirmo""
- ""¿Tienen mesa libre?""
- ""Quiero modificar la reserva"" (esto es modificación, no cancelación)
- ""¿Puedo cambiar la hora?"" (esto es modificación)
- Preguntas sobre el menú, horarios, etc.";

            var userPrompt = $@"Historial reciente:
{contextSummary}

Mensaje actual del cliente: ""{messageText}""

¿Este mensaje indica intención de CANCELAR una reserva? (YES/NO):";

            var config = new GeminiGenerationConfig
            {
                Temperature = 0.0,
                MaxOutputTokens = 5
            };

            var response = await _gemini.GenerateAsync(systemPrompt, userPrompt, null, config, ct);
            var result = response.Trim().ToUpperInvariant();

            _logger.LogDebug(
                "AI cancellation intent detection for '{Message}': {Result}",
                messageText.Length > 50 ? messageText[..50] + "..." : messageText,
                result);

            return result.Contains("YES");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting cancellation intent with AI, falling back to false");
            return false;
        }
    }
}
