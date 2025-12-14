using BotGenerator.Core.Agents;
using BotGenerator.Core.Handlers;
using BotGenerator.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BotGenerator.Core.Services;

/// <summary>
/// Implementation of IIntentRouterService.
/// Routes conversations based on detected intent.
/// </summary>
public class IntentRouterService : IIntentRouterService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<IntentRouterService> _logger;
    private readonly IPendingBookingStore _pendingBookingStore;
    private readonly IModificationStateStore _modificationStateStore;
    private readonly ICancellationStateStore _cancellationStateStore;
    private readonly IPendingRiceStore _pendingRiceStore;

    public IntentRouterService(
        IServiceProvider services,
        IPendingBookingStore pendingBookingStore,
        IModificationStateStore modificationStateStore,
        ICancellationStateStore cancellationStateStore,
        IPendingRiceStore pendingRiceStore,
        ILogger<IntentRouterService> logger)
    {
        _services = services;
        _pendingBookingStore = pendingBookingStore;
        _modificationStateStore = modificationStateStore;
        _cancellationStateStore = cancellationStateStore;
        _pendingRiceStore = pendingRiceStore;
        _logger = logger;
    }

    public async Task<AgentResponse> RouteAsync(
        AgentResponse mainAgentResponse,
        WhatsAppMessage originalMessage,
        ConversationState? state,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Routing intent: {Intent} for {Sender}",
            mainAgentResponse.Intent,
            originalMessage.PushName);

        try
        {
            // ========== EARLY CHECK FOR LARGE GROUPS ==========
            // Check if user mentions >10 people before booking flow starts
            var largeGroupMatch = System.Text.RegularExpressions.Regex.Match(
                originalMessage.MessageText,
                @"(\d+)\s*personas",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (largeGroupMatch.Success)
            {
                var partySize = int.Parse(largeGroupMatch.Groups[1].Value);
                if (partySize > 10)
                {
                    _logger.LogInformation(
                        "Early large group detection ({People} people) for {Phone}",
                        partySize, originalMessage.SenderNumber);

                    var whatsApp = _services.GetRequiredService<IWhatsAppService>();

                    await whatsApp.SendTextAsync(
                        originalMessage.SenderNumber,
                        ResponseVariations.LargeGroupIntro(),
                        cancellationToken);

                    await whatsApp.SendContactCardAsync(
                        originalMessage.SenderNumber,
                        fullName: "Gestión Reservas Villa Carmen",
                        contactPhoneNumber: "34638857294",
                        organization: "Alquería Villa Carmen",
                        cancellationToken: cancellationToken);

                    _pendingBookingStore.Clear(originalMessage.SenderNumber);

                    return new AgentResponse
                    {
                        Intent = IntentType.Normal,
                        AiResponse = ResponseVariations.LargeGroupVCard()
                    };
                }
            }

            // ========== CHECK FOR SPECIAL REQUESTS (cakes, celebrations) ==========
            // These require contacting the restaurant directly
            var specialRequestPatterns = new[]
            {
                @"tarta.*cumplea[ñn]os",
                @"cumplea[ñn]os.*tarta",
                @"pastel.*cumplea[ñn]os",
                @"celebraci[oó]n.*especial",
                @"evento.*privado",
                @"tarta.*personalizada",
                @"decoraci[oó]n.*especial",
                @"fiesta.*privada"
            };

            foreach (var pattern in specialRequestPatterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(
                    originalMessage.MessageText, pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    _logger.LogInformation(
                        "Special request detected for {Phone}: {Pattern}",
                        originalMessage.SenderNumber, pattern);

                    var whatsApp = _services.GetRequiredService<IWhatsAppService>();

                    await whatsApp.SendTextAsync(
                        originalMessage.SenderNumber,
                        ResponseVariations.SpecialRequestIntro(),
                        cancellationToken);

                    await whatsApp.SendContactCardAsync(
                        originalMessage.SenderNumber,
                        fullName: "Gestión Reservas Villa Carmen",
                        contactPhoneNumber: "34638857294",
                        organization: "Alquería Villa Carmen",
                        cancellationToken: cancellationToken);

                    return new AgentResponse
                    {
                        Intent = IntentType.Normal,
                        AiResponse = ResponseVariations.SpecialRequestVCard()
                    };
                }
            }

            // ========== CHECK FOR ACTIVE MODIFICATION SESSION ==========
            // If user has an active modification session, route ALL messages to the handler
            // This ensures multi-turn modification flows work correctly
            var modificationState = _modificationStateStore.Get(originalMessage.SenderNumber);
            if (modificationState != null || mainAgentResponse.Intent == IntentType.Modification)
            {
                _logger.LogInformation(
                    "Routing to modification handler (activeSession={HasSession}, intent={Intent})",
                    modificationState != null, mainAgentResponse.Intent);

                return await HandleModificationAsync(
                    mainAgentResponse, originalMessage, modificationState, cancellationToken);
            }

            // ========== CHECK FOR ACTIVE CANCELLATION SESSION ==========
            // If user has an active cancellation session, route ALL messages to the handler
            // This ensures multi-turn cancellation flows work correctly
            var cancellationState = _cancellationStateStore.Get(originalMessage.SenderNumber);
            if (cancellationState != null || mainAgentResponse.Intent == IntentType.Cancellation)
            {
                _logger.LogInformation(
                    "Routing to cancellation handler (activeSession={HasSession}, intent={Intent})",
                    cancellationState != null, mainAgentResponse.Intent);

                return await HandleCancellationAsync(
                    mainAgentResponse, originalMessage, cancellationState, cancellationToken);
            }

            // ========== CHECK FOR PENDING RICE SELECTION ==========
            // If user was asked to choose between multiple rice options, process their selection
            var pendingRice = _pendingRiceStore.Get(originalMessage.SenderNumber);
            if (pendingRice != null)
            {
                var selectedRice = TryParseRiceSelection(originalMessage.MessageText, pendingRice.Options);
                if (selectedRice != null)
                {
                    _logger.LogInformation(
                        "User selected rice: {Rice} from options",
                        selectedRice);

                    // Clear pending rice selection
                    _pendingRiceStore.Clear(originalMessage.SenderNumber);

                    // Update the pending booking with selected rice
                    var pendingBooking = _pendingBookingStore.Get(originalMessage.SenderNumber);
                    if (pendingBooking != null)
                    {
                        var updatedBooking = pendingBooking with { ArrozType = selectedRice };
                        _pendingBookingStore.Set(originalMessage.SenderNumber, updatedBooking);

                        // Continue with booking flow
                        var synthetic = mainAgentResponse with
                        {
                            Intent = IntentType.Booking,
                            ExtractedData = updatedBooking,
                            Metadata = new Dictionary<string, object>
                            {
                                ["riceValidated"] = true,
                                ["validatedRiceName"] = selectedRice
                            }
                        };

                        return await HandleBookingAsync(
                            synthetic,
                            originalMessage,
                            state,
                            cancellationToken);
                    }
                }
                else
                {
                    // User's response didn't match any option, ask again
                    _logger.LogInformation("Could not parse rice selection from: {Message}", originalMessage.MessageText);
                    var formattedOptions = FormatRiceOptionsForSelection(pendingRice.Options);
                    return new AgentResponse
                    {
                        Intent = IntentType.Normal,
                        AiResponse = ResponseVariations.RiceSelectionNotUnderstood(formattedOptions)
                    };
                }
            }

            // If we have a pending BOOKING_REQUEST from a previous turn, we can keep progressing
            // even if the AI doesn't re-emit BOOKING_REQUEST on follow-up messages.
            // This is critical when we ask mandatory follow-up questions (rice decision/servings, tronas, carritos).
            if (mainAgentResponse.Intent != IntentType.Booking)
            {
                var pending = _pendingBookingStore.Get(originalMessage.SenderNumber);
                if (pending != null)
                {
                    var synthetic = mainAgentResponse with
                    {
                        Intent = IntentType.Booking,
                        ExtractedData = pending
                    };

                    return await HandleBookingAsync(
                        synthetic,
                        originalMessage,
                        state,
                        cancellationToken);
                }
            }

            return mainAgentResponse.Intent switch
            {
                IntentType.Booking => await HandleBookingAsync(
                    mainAgentResponse, originalMessage, state, cancellationToken),

                // Note: Cancellation is handled earlier via state check, this is fallback
                IntentType.Cancellation => await HandleCancellationAsync(
                    mainAgentResponse, originalMessage, null, cancellationToken),

                // Note: Modification is handled earlier via state check, this is fallback
                IntentType.Modification => await HandleModificationAsync(
                    mainAgentResponse, originalMessage, null, cancellationToken),

                IntentType.SameDay => HandleSameDay(mainAgentResponse),

                IntentType.Interactive => HandleInteractive(mainAgentResponse),

                IntentType.Error => HandleError(mainAgentResponse),

                _ => mainAgentResponse // Normal - return as-is
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error routing intent {Intent}", mainAgentResponse.Intent);
            return AgentResponse.Error("Error processing your request");
        }
    }

    #region Intent Handlers

    private async Task<AgentResponse> HandleBookingAsync(
        AgentResponse response,
        WhatsAppMessage message,
        ConversationState? state,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling booking intent");

        // Store the latest extracted booking so we can resume on follow-up turns without requiring
        // the AI to re-emit BOOKING_REQUEST.
        if (response.ExtractedData != null)
        {
            _pendingBookingStore.Set(message.SenderNumber, response.ExtractedData);
        }

        // Check for large groups (>10 people) - redirect to reservations team
        if (response.ExtractedData?.People > 10 || state?.Personas > 10)
        {
            var partySize = response.ExtractedData?.People ?? state?.Personas ?? 0;
            _logger.LogInformation(
                "Large group detected ({People} people) for {Phone}, sending contact card",
                partySize, message.SenderNumber);

            var whatsApp = _services.GetRequiredService<IWhatsAppService>();

            // Send introductory message
            await whatsApp.SendTextAsync(
                message.SenderNumber,
                ResponseVariations.LargeGroupIntro(),
                cancellationToken);

            // Send contact card
            await whatsApp.SendContactCardAsync(
                message.SenderNumber,
                fullName: "Gestión Reservas Villa Carmen",
                contactPhoneNumber: "34638857294",
                organization: "Alquería Villa Carmen",
                cancellationToken: cancellationToken);

            // Clear pending booking
            _pendingBookingStore.Clear(message.SenderNumber);

            return new AgentResponse
            {
                Intent = IntentType.Normal,
                AiResponse = ResponseVariations.LargeGroupVCard()
            };
        }

        // Check if we need to validate rice type first
        var needsRiceValidation = response.ExtractedData?.ArrozType != null &&
                                  (response.Metadata?.ContainsKey("riceValidated") != true);

        if (needsRiceValidation && response.ExtractedData?.ArrozType != null)
        {
            var riceValidator = _services.GetRequiredService<RiceValidatorAgent>();
            var riceResult = await riceValidator.ValidateAsync(
                response.ExtractedData.ArrozType,
                "villacarmen",
                cancellationToken);

            // Handle MULTIPLE matches - ask user to choose
            if (riceResult.Status == "multiple" && riceResult.Options?.Count > 1)
            {
                _logger.LogInformation(
                    "Multiple rice matches found: {Options}",
                    string.Join(", ", riceResult.Options));

                // Store pending rice selection
                _pendingRiceStore.Set(message.SenderNumber, new PendingRiceSelection
                {
                    Options = riceResult.Options,
                    OriginalRequest = response.ExtractedData.ArrozType
                });

                // Format options with numbers for easy selection
                var formattedOptions = FormatRiceOptionsForSelection(riceResult.Options);

                return new AgentResponse
                {
                    Intent = IntentType.Normal,
                    AiResponse = ResponseVariations.MultipleRiceOptionsSelection(formattedOptions),
                    Metadata = new Dictionary<string, object>
                    {
                        ["riceValidation"] = riceResult,
                        ["pendingRiceSelection"] = true
                    }
                };
            }

            if (!riceResult.IsValid)
            {
                _logger.LogInformation(
                    "Rice validation failed: {Status}",
                    riceResult.Status);

                return new AgentResponse
                {
                    Intent = IntentType.Normal,
                    AiResponse = riceResult.Message ?? ResponseVariations.RiceTypeNotFound(),
                    Metadata = new Dictionary<string, object>
                    {
                        ["riceValidation"] = riceResult
                    }
                };
            }

            // Update booking data with validated rice name (full database name)
            response = response with
            {
                ExtractedData = response.ExtractedData with
                {
                    ArrozType = riceResult.RiceName
                },
                Metadata = new Dictionary<string, object>(response.Metadata ?? new())
                {
                    ["riceValidated"] = true,
                    ["validatedRiceName"] = riceResult.RiceName ?? ""
                }
            };
        }

        // Check availability
        // If user confirmed and AI emitted BOOKING_REQUEST, we still enforce required fields in code
        // before actually creating the booking.

        // If we have all data, create the booking
        if (response.ExtractedData?.IsValid == true)
        {
            if (state == null)
            {
                return response with
                {
                    Intent = IntentType.Normal,
                    AiResponse = ResponseVariations.IncompleteBookingPrompt()
                };
            }

            // 0a) If time was rejected (availability issue), ask for new time before proceeding
            if (state.InvalidHora)
            {
                _logger.LogInformation("Time was rejected, asking for new time");
                _pendingBookingStore.Clear(message.SenderNumber);
                return response with
                {
                    Intent = IntentType.Normal,
                    AiResponse = ResponseVariations.AskTime()
                };
            }

            // 0b) If date was rejected (no capacity), ask for new date before proceeding
            if (state.InvalidFecha)
            {
                _logger.LogInformation("Date was rejected (no availability), asking for new date");
                _pendingBookingStore.Clear(message.SenderNumber);
                return response with
                {
                    Intent = IntentType.Normal,
                    AiResponse = ResponseVariations.AskDate()
                };
            }

            // 1) Enforce mandatory "extras" collection (must be asked & answered)
            if (state.HighChairs is null)
            {
                return response with
                {
                    Intent = IntentType.Normal,
                    AiResponse = ResponseVariations.AskTronas()
                };
            }

            // User said "sí" but didn't specify how many yet (we store -1)
            if (state.HighChairs.Value < 0)
            {
                return response with
                {
                    Intent = IntentType.Normal,
                    AiResponse = ResponseVariations.AskTronasCount()
                };
            }

            if (state.HighChairs.Value > 3)
            {
                return response with
                {
                    Intent = IntentType.Normal,
                    AiResponse = ResponseVariations.MaxTronas()
                };
            }

            if (state.BabyStrollers is null)
            {
                return response with
                {
                    Intent = IntentType.Normal,
                    AiResponse = ResponseVariations.AskCarritos()
                };
            }

            if (state.BabyStrollers.Value < 0)
            {
                return response with
                {
                    Intent = IntentType.Normal,
                    AiResponse = ResponseVariations.AskCarritosCount()
                };
            }

            if (state.BabyStrollers.Value > 3)
            {
                return response with
                {
                    Intent = IntentType.Normal,
                    AiResponse = ResponseVariations.MaxCarritos()
                };
            }

            // 2) Enforce rice flow rules
            // Prefer validated rice name from ExtractedData when rice was validated
            var riceWasValidated = response.Metadata?.ContainsKey("riceValidated") == true &&
                                   response.Metadata["riceValidated"] is bool validated && validated;
            var arrozType = riceWasValidated && !string.IsNullOrEmpty(response.ExtractedData?.ArrozType)
                ? response.ExtractedData.ArrozType
                : state?.ArrozType;
            var arrozServings = state?.ArrozServings;

            // Not decided yet -> must ask
            if (arrozType is null)
            {
                return response with
                {
                    Intent = IntentType.Normal,
                    AiResponse = ResponseVariations.AskRice()
                };
            }

            // No rice
            if (string.IsNullOrEmpty(arrozType))
            {
                arrozType = null;
                arrozServings = null;
            }
            else
            {
                // Rice chosen: servings required and minimum 2
                if (!arrozServings.HasValue)
                {
                    return response with
                    {
                        Intent = IntentType.Normal,
                        AiResponse = ResponseVariations.AskRicePortions()
                    };
                }

                if (arrozServings.Value < 2)
                {
                    return response with
                    {
                        Intent = IntentType.Normal,
                        AiResponse = ResponseVariations.MinRicePortions()
                    };
                }
            }

            // 3) Merge canonical data (name/phone from WhatsApp + extras from state)
            var highChairsFinal = Math.Clamp(state!.HighChairs.GetValueOrDefault(0), 0, 3);
            var babyStrollersFinal = Math.Clamp(state.BabyStrollers.GetValueOrDefault(0), 0, 3);

            // Use validated rice name from arrozType (already resolved above to prefer ExtractedData)
            var booking = response.ExtractedData with
            {
                Name = message.PushName,
                Phone = message.SenderNumber,
                ArrozType = arrozType,  // Uses validated name if riceWasValidated
                ArrozServings = arrozServings,
                HighChairs = highChairsFinal,
                BabyStrollers = babyStrollersFinal
            };

            _logger.LogInformation(
                "Creating booking with rice: {ArrozType} (validated={Validated})",
                arrozType ?? "(none)", riceWasValidated);

            // 4) DB-backed availability checks (mirror PHP scripts)
            if (TryParseDate(booking.Date, out var date) &&
                TryParseTime(booking.Time, out var time))
            {
                var availabilityService = _services.GetRequiredService<IBookingAvailabilityService>();
                var decision = await availabilityService.EvaluateAsync(
                    date, booking.People, time, cancellationToken);

                if (!decision.IsAvailable && !string.IsNullOrWhiteSpace(decision.Message))
                {
                    // Clear the pending booking with invalid time/date to prevent loops
                    // The user needs to provide new valid data
                    if (decision.Reason == "hour_unavailable" || decision.Reason == "closed_day" || decision.Reason == "daily_full" || decision.Reason == "same_day")
                    {
                        _pendingBookingStore.Clear(message.SenderNumber);
                        _logger.LogInformation(
                            "Cleared pending booking for {Phone} due to availability issue: {Reason}",
                            message.SenderNumber, decision.Reason);
                    }

                    // Same-day bookings: send intro message + contact card
                    if (decision.Reason == "same_day")
                    {
                        var whatsApp = _services.GetRequiredService<IWhatsAppService>();

                        await whatsApp.SendTextAsync(
                            message.SenderNumber,
                            ResponseVariations.SameDayBookingIntro(),
                            cancellationToken);

                        await whatsApp.SendContactCardAsync(
                            message.SenderNumber,
                            fullName: "Gestión Reservas Villa Carmen",
                            contactPhoneNumber: "34638857294",
                            organization: "Alquería Villa Carmen",
                            cancellationToken: cancellationToken);
                    }

                    return new AgentResponse
                    {
                        Intent = IntentType.Normal,
                        AiResponse = decision.Message
                    };
                }
            }

            var bookingHandler = _services.GetRequiredService<BookingHandler>();
            var created = await bookingHandler.CreateBookingAsync(
                booking,
                message,
                cancellationToken);

            if (created.Metadata != null &&
                created.Metadata.TryGetValue("bookingCreated", out var createdObj) &&
                createdObj is bool createdBool &&
                createdBool)
            {
                _pendingBookingStore.Clear(message.SenderNumber);
            }

            return created;
        }

        // Return the main response (still collecting data)
        return response;
    }

    private static bool TryParseDate(string dateStr, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(dateStr)) return false;

        return DateTime.TryParseExact(dateStr, "dd/MM/yyyy", null,
                   System.Globalization.DateTimeStyles.None, out date) ||
               DateTime.TryParseExact(dateStr, "d/M/yyyy", null,
                   System.Globalization.DateTimeStyles.None, out date);
    }

    private static bool TryParseTime(string timeStr, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(timeStr)) return false;

        return TimeSpan.TryParseExact(timeStr, @"hh\:mm", null, out time) ||
               TimeSpan.TryParseExact(timeStr, @"h\:mm", null, out time);
    }

    private async Task<AgentResponse> HandleCancellationAsync(
        AgentResponse response,
        WhatsAppMessage message,
        CancellationState? currentState,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Handling cancellation intent (stage={Stage})",
            currentState?.Stage.ToString() ?? "New");

        var cancellationHandler = _services.GetRequiredService<CancellationHandler>();
        return await cancellationHandler.ProcessCancellationAsync(
            message,
            currentState,
            cancellationToken);
    }

    private async Task<AgentResponse> HandleModificationAsync(
        AgentResponse response,
        WhatsAppMessage message,
        ModificationState? currentState,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Handling modification intent (stage={Stage})",
            currentState?.Stage.ToString() ?? "New");

        var modificationHandler = _services.GetRequiredService<ModificationHandler>();
        return await modificationHandler.ProcessModificationAsync(
            message,
            currentState,
            cancellationToken);
    }

    private AgentResponse HandleSameDay(AgentResponse response)
    {
        _logger.LogDebug("Handling same-day booking rejection");

        var message = string.IsNullOrWhiteSpace(response.AiResponse)
            ? ResponseVariations.SameDayBookingRejection()
            : response.AiResponse;

        return response with { AiResponse = message };
    }

    private AgentResponse HandleInteractive(AgentResponse response)
    {
        _logger.LogDebug("Handling interactive response with URLs");

        // The response already contains URLs, just pass through
        // The WhatsApp service will format them appropriately
        return response;
    }

    private AgentResponse HandleError(AgentResponse response)
    {
        _logger.LogWarning("Handling error response");

        var message = ResponseVariations.GenericError();

        return response with { AiResponse = message };
    }

    #endregion

    #region Rice Selection Helpers

    /// <summary>
    /// Tries to parse user's rice selection from their message.
    /// Supports: numbers (1, 2), ordinals (la primera, la segunda), partial names.
    /// </summary>
    private static string? TryParseRiceSelection(string userMessage, List<string> options)
    {
        var text = userMessage.Trim().ToLowerInvariant();

        // Direct number selection: "1", "2", etc.
        if (int.TryParse(text, out var num) && num >= 1 && num <= options.Count)
        {
            return options[num - 1];
        }

        // Ordinal selection: "la primera", "la 1", "la segunda", "la 2"
        var ordinalPatterns = new Dictionary<string, int>
        {
            [@"^(la\s+)?primer[ao]?$"] = 1,
            [@"^(la\s+)?1$"] = 1,
            [@"^(el\s+)?1$"] = 1,
            [@"^(la\s+)?segund[ao]?$"] = 2,
            [@"^(la\s+)?2$"] = 2,
            [@"^(el\s+)?2$"] = 2,
            [@"^(la\s+)?tercer[ao]?$"] = 3,
            [@"^(la\s+)?3$"] = 3,
            [@"^(el\s+)?3$"] = 3,
            [@"^(la\s+)?cuart[ao]?$"] = 4,
            [@"^(la\s+)?4$"] = 4,
            [@"^(el\s+)?4$"] = 4
        };

        foreach (var (pattern, index) in ordinalPatterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(text, pattern) && index <= options.Count)
            {
                return options[index - 1];
            }
        }

        // Partial name match: "la de Albufera", "la del pato", "paella valenciana"
        foreach (var option in options)
        {
            var optionLower = option.ToLowerInvariant();
            // Check if user message contains significant words from option
            var significantWords = ExtractSignificantWords(optionLower);
            var userWords = ExtractSignificantWords(text);

            // If user mentions at least one significant word that uniquely identifies this option
            foreach (var word in userWords)
            {
                if (word.Length >= 4 && optionLower.Contains(word))
                {
                    // Check this word doesn't appear in other options
                    var matchCount = options.Count(o => o.ToLowerInvariant().Contains(word));
                    if (matchCount == 1)
                    {
                        return option;
                    }
                }
            }
        }

        // Check for "de la" pattern: "la de la Albufera"
        var delaMatch = System.Text.RegularExpressions.Regex.Match(text, @"(?:la\s+)?de\s+(?:la\s+)?(\w+)");
        if (delaMatch.Success)
        {
            var keyword = delaMatch.Groups[1].Value;
            foreach (var option in options)
            {
                if (option.ToLowerInvariant().Contains(keyword))
                {
                    return option;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts significant words (ignoring common articles and prepositions).
    /// </summary>
    private static HashSet<string> ExtractSignificantWords(string text)
    {
        var stopWords = new HashSet<string>
        {
            "el", "la", "los", "las", "un", "una", "de", "del", "con", "y", "por", "para",
            "arroz", "paella", "fideua", "fideuá", "encargo", "€"
        };

        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3 && !stopWords.Contains(w))
            .Select(w => System.Text.RegularExpressions.Regex.Replace(w, @"[^\w]", ""))
            .Where(w => w.Length >= 3)
            .ToHashSet();
    }

    /// <summary>
    /// Formats rice options with numbers for display.
    /// </summary>
    private static string FormatRiceOptionsForSelection(List<string> options)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < options.Count; i++)
        {
            sb.AppendLine($"{i + 1}. {options[i]}");
        }
        return sb.ToString().TrimEnd();
    }

    #endregion
}
