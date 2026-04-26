using BotGenerator.Core.Agents;
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using Microsoft.Extensions.Logging;
using System.Text;

namespace BotGenerator.Core.Handlers;

/// <summary>
/// Handler for modifying existing bookings.
/// Manages the multi-turn modification conversation flow with natural language understanding.
/// </summary>
public class ModificationHandler
{
    private readonly ILogger<ModificationHandler> _logger;
    private readonly IBookingRepository _bookingRepository;
    private readonly IModificationStateStore _stateStore;
    private readonly IBookingAvailabilityService _availabilityService;
    private readonly RiceValidatorAgent _riceValidator;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IContextBuilderService _contextBuilder;
    private readonly IExternalReservationService _externalReservationService;
    private readonly IFieldAccumulatorService _fieldAccumulator;
    private readonly INaturalLanguageModificationParser _nlParser;
    private readonly IPendingRiceStore _pendingRiceStore;
    private readonly IAiBookingSelectionService _bookingSelection;
    private readonly IAiFieldSelectionService _fieldSelection;
    private readonly IAiIntentDetectionService _intentDetection;
    private readonly IAiRiceUnderstandingService _riceUnderstanding;

    public ModificationHandler(
        ILogger<ModificationHandler> logger,
        IBookingRepository bookingRepository,
        IModificationStateStore stateStore,
        IBookingAvailabilityService availabilityService,
        RiceValidatorAgent riceValidator,
        IWhatsAppService whatsAppService,
        IContextBuilderService contextBuilder,
        IExternalReservationService externalReservationService,
        IFieldAccumulatorService fieldAccumulator,
        INaturalLanguageModificationParser nlParser,
        IPendingRiceStore pendingRiceStore,
        IAiBookingSelectionService bookingSelection,
        IAiFieldSelectionService fieldSelection,
        IAiIntentDetectionService intentDetection,
        IAiRiceUnderstandingService riceUnderstanding)
    {
        _logger = logger;
        _bookingRepository = bookingRepository;
        _stateStore = stateStore;
        _availabilityService = availabilityService;
        _riceValidator = riceValidator;
        _whatsAppService = whatsAppService;
        _contextBuilder = contextBuilder;
        _externalReservationService = externalReservationService;
        _fieldAccumulator = fieldAccumulator;
        _nlParser = nlParser;
        _pendingRiceStore = pendingRiceStore;
        _bookingSelection = bookingSelection;
        _fieldSelection = fieldSelection;
        _intentDetection = intentDetection;
        _riceUnderstanding = riceUnderstanding;
    }

    /// <summary>
    /// Main entry point for processing modification requests.
    /// </summary>
    public async Task<AgentResponse> ProcessModificationAsync(
        WhatsAppMessage message,
        ModificationState? currentState,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Processing modification for {Phone}, Stage={Stage}",
            message.SenderNumber,
            currentState?.Stage.ToString() ?? "New");

        // Check for unsupported content (media/audio)
        if (IsUnsupportedContent(message))
        {
            return await HandleUnsupportedContentAsync(message, ct);
        }

        // Route based on current stage
        return currentState?.Stage switch
        {
            null => await StartModificationFlowAsync(message, ct),
            ModificationStage.SelectingBooking => await HandleBookingSelectionAsync(message, currentState, ct),
            ModificationStage.SelectingField => await HandleFieldSelectionAsync(message, currentState, ct),
            ModificationStage.CollectingNewValue => await HandleNewValueAsync(message, currentState, ct),
            ModificationStage.AwaitingConfirmation => await HandleConfirmationAsync(message, currentState, ct),
            _ => await StartModificationFlowAsync(message, ct)
        };
    }

    #region Flow Steps

    /// <summary>
    /// Step 1: Start modification flow - find bookings for this phone.
    /// </summary>
    private async Task<AgentResponse> StartModificationFlowAsync(
        WhatsAppMessage message,
        CancellationToken ct)
    {
        _logger.LogInformation("Starting modification flow for {Phone}", message.SenderNumber);

        // Extract 9-digit phone
        var phone9 = NormalizePhoneTo9Digits(message.SenderNumber);

        // Find bookings in database
        var bookings = await _bookingRepository.FindBookingsByPhoneAsync(phone9, ct);

        if (bookings.Count == 0)
        {
            _stateStore.Clear(message.SenderNumber);
            return new AgentResponse
            {
                Intent = IntentType.Normal,
                AiResponse = ResponseVariations.ModificationNoBookingsFound()
            };
        }

        if (bookings.Count == 1)
        {
            // Auto-select the only booking, go to SelectingField
            var state = new ModificationState
            {
                PhoneNumber = message.SenderNumber,
                Stage = ModificationStage.SelectingField,
                FoundBookings = bookings,
                SelectedBooking = bookings[0]
            };
            _stateStore.Set(message.SenderNumber, state);

            return BuildSelectFieldResponse(bookings[0]);
        }

        // Multiple bookings - ask which one
        var multiState = new ModificationState
        {
            PhoneNumber = message.SenderNumber,
            Stage = ModificationStage.SelectingBooking,
            FoundBookings = bookings
        };
        _stateStore.Set(message.SenderNumber, multiState);

        return BuildSelectBookingResponse(bookings);
    }

    /// <summary>
    /// Step 2: Handle booking selection from multiple bookings using AI.
    /// </summary>
    private async Task<AgentResponse> HandleBookingSelectionAsync(
        WhatsAppMessage message,
        ModificationState state,
        CancellationToken ct)
    {
        var bookings = state.FoundBookings ?? new List<BookingRecord>();

        var selected = await _bookingSelection.SelectBookingAsync(
            message.MessageText, bookings, ct);

        if (selected == null)
        {
            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = ResponseVariations.BookingSelectionNotUnderstood()
            };
        }

        // Check if field was pre-selected (e.g., rice modification shortcut)
        if (!string.IsNullOrEmpty(state.FieldToModify))
        {
            _logger.LogInformation(
                "Booking selected with pre-selected field: {Field}",
                state.FieldToModify);

            // Update state with selected booking, move to CollectingNewValue
            var newStateWithField = state with
            {
                Stage = ModificationStage.CollectingNewValue,
                SelectedBooking = selected
            };
            _stateStore.Set(message.SenderNumber, newStateWithField);

            // If we have pre-extracted rice info, try to use it
            if (state.FieldToModify == "rice" && state.PendingChanges?.ArrozType != null)
            {
                // Call HandleRiceChangeAsync with a synthetic message containing the rice type
                var syntheticMessage = message with { MessageText = state.PendingChanges.ArrozType };
                return await HandleRiceChangeAsync(syntheticMessage, newStateWithField, ct);
            }

            // Ask for the new value
            return BuildAskNewValueResponse(state.FieldToModify, selected);
        }

        // Update state with selected booking
        var newState = state with
        {
            Stage = ModificationStage.SelectingField,
            SelectedBooking = selected
        };
        _stateStore.Set(message.SenderNumber, newState);

        return BuildSelectFieldResponse(selected);
    }

    /// <summary>
    /// Step 3: Handle field selection (what to modify).
    /// Uses AI for field detection and natural language extraction.
    /// </summary>
    private async Task<AgentResponse> HandleFieldSelectionAsync(
        WhatsAppMessage message,
        ModificationState state,
        CancellationToken ct)
    {
        // Check for exit/cancel intent using AI
        var exitIntent = await _intentDetection.DetectIntentAsync(
            message.MessageText, "modification_exit", ct);

        if (exitIntent == "exit")
        {
            _logger.LogInformation("User wants to exit modification flow without changes");
            _stateStore.Clear(message.SenderNumber);
            return new AgentResponse
            {
                Intent = IntentType.Normal,
                AiResponse = ResponseVariations.ModificationExitConfirmation()
            };
        }

        // Use natural language parser to extract all possible fields
        var extractedFields = _nlParser.ExtractFields(message.MessageText, state);

        _logger.LogInformation(
            "Extracted {Count} fields from message: {Fields}",
            extractedFields.Count,
            string.Join(", ", extractedFields.Keys));

        // If multiple fields extracted, handle combined modification (e.g., date+time)
        if (extractedFields.Count > 1)
        {
            return await HandleMultiFieldModificationAsync(message, state, extractedFields, ct);
        }

        // Use AI field selection service
        var booking = state.SelectedBooking!;
        var bookingSummary = $"{booking.DateFormatted} ({booking.DayName}) a las {booking.TimeFormatted}, {booking.PartySize} personas";

        string? field = await _fieldSelection.DetectFieldAsync(
            message.MessageText, bookingSummary, ct);

        // Fallback: use extracted fields from parser
        if (field == null && extractedFields.Count > 0)
        {
            field = extractedFields.Keys.First();
        }

        if (field == null)
        {
            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = ResponseVariations.FieldSelectionNotUnderstood()
            };
        }

        // Update state with accumulator pattern
        var newState = state with
        {
            Stage = ModificationStage.CollectingNewValue,
            FieldToModify = field,
            // NEW: Initialize accumulator if needed
            AccumulatedChanges = extractedFields.Count > 0 ? extractedFields : new Dictionary<string, object>(),
            ExtractedFields = extractedFields.Keys.ToList(),
            ConversationTurn = state.ConversationTurn + 1,
            LastAskedField = field
        };
        _stateStore.Set(message.SenderNumber, newState);

        // If we already extracted the value, skip asking and go directly to processing
        if (extractedFields.TryGetValue(field, out var extractedValue))
        {
            _logger.LogInformation("Field {Field} already extracted from message, processing directly", field);
            
            // Create synthetic message with extracted value for processing
            var syntheticMessage = message with { MessageText = extractedValue.ToString() ?? "" };
            return await HandleNewValueAsync(syntheticMessage, newState, ct);
        }

        // Ask for the new value
        return BuildAskNewValueResponse(field, state.SelectedBooking!);
    }

    /// <summary>
    /// Handles modifications where multiple fields were extracted from a single message.
    /// Example: "domingo 15 a las 14:30" → date + time together.
    /// </summary>
    private async Task<AgentResponse> HandleMultiFieldModificationAsync(
        WhatsAppMessage message,
        ModificationState state,
        Dictionary<string, object> extractedFields,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Handling multi-field modification with {Count} fields: {Fields}",
            extractedFields.Count,
            string.Join(", ", extractedFields.Keys));

        var booking = state.SelectedBooking!;
        var changeDescriptions = new List<string>();
        
        // Variables to build BookingUpdateData
        string? reservationDate = null;
        string? reservationTime = null;
        int? partySize = null;
        string? arrozType = null;
        int? arrozServings = null;
        int? highChairs = null;
        int? babyStrollers = null;
        bool clearRice = false;

        // Process date if present
        if (extractedFields.TryGetValue("date", out var dateObj) && dateObj is DateTime newDate)
        {
            // Check availability for the new date with current time
            var timeToCheck = extractedFields.ContainsKey("time") && extractedFields["time"] is TimeSpan newTime
                ? newTime
                : booking.ReservationTime;

            var decision = await _availabilityService.EvaluateAsync(
                newDate,
                booking.PartySize,
                timeToCheck,
                booking.Id,
                ct);

            if (!decision.IsAvailable)
            {
                // Handle availability rejection
                if (decision.Reason == "same_day")
                {
                    _stateStore.Clear(message.SenderNumber);
                    await _whatsAppService.SendTextAsync(
                        message.SenderNumber,
                        ResponseVariations.SameDayBookingIntro(),
                        ct);
                    await _whatsAppService.SendContactCardAsync(
                        message.SenderNumber,
                        fullName: "Gestión Reservas Villa Carmen",
                        contactPhoneNumber: "34638857294",
                        organization: "Alquería Villa Carmen",
                        email: null,
                        cancellationToken: ct);
                    return new AgentResponse
                    {
                        Intent = IntentType.Normal,
                        AiResponse = ResponseVariations.SameDayBookingRejection()
                    };
                }

                // Suggest alternatives
                if (decision.SuggestedHours?.Count > 0)
                {
                    return new AgentResponse
                    {
                        Intent = IntentType.Modification,
                        AiResponse = $"El {newDate:dd/MM/yyyy} a las {timeToCheck.Hours:D2}:{timeToCheck.Minutes:D2} no está disponible. " +
                                    $"Horas disponibles: {string.Join(", ", decision.SuggestedHours)}. " +
                                    "¿Prefieres alguna de estas?"
                    };
                }

                return new AgentResponse
                {
                    Intent = IntentType.Modification,
                    AiResponse = $"No tengo disponibilidad para el {newDate:dd/MM/yyyy} a las {timeToCheck.Hours:D2}:{timeToCheck.Minutes:D2}. " +
                                "¿Qué otra fecha u hora te viene bien?"
                };
            }

            reservationDate = newDate.ToString("yyyy-MM-dd");
            changeDescriptions.Add($"fecha al {newDate:dd/MM/yyyy}");
        }

        // Process time if present
        if (extractedFields.TryGetValue("time", out var timeObj) && timeObj is TimeSpan time)
        {
            var dateToCheck = extractedFields.ContainsKey("date") && extractedFields["date"] is DateTime dt
                ? dt
                : booking.ReservationDate;

            // Only check availability if date wasn't already checked
            if (!extractedFields.ContainsKey("date"))
            {
                var decision = await _availabilityService.EvaluateAsync(
                    dateToCheck,
                    booking.PartySize,
                    time,
                    booking.Id,
                    ct);

                if (!decision.IsAvailable)
                {
                    if (decision.Reason == "same_day")
                    {
                        _stateStore.Clear(message.SenderNumber);
                        await _whatsAppService.SendTextAsync(
                            message.SenderNumber,
                            ResponseVariations.SameDayBookingIntro(),
                            ct);
                        await _whatsAppService.SendContactCardAsync(
                            message.SenderNumber,
                            fullName: "Gestión Reservas Villa Carmen",
                            contactPhoneNumber: "34638857294",
                            organization: "Alquería Villa Carmen",
                            email: null,
                            cancellationToken: ct);
                        return new AgentResponse
                        {
                            Intent = IntentType.Normal,
                            AiResponse = ResponseVariations.SameDayBookingRejection()
                        };
                    }

                    if (decision.SuggestedHours?.Count > 0)
                    {
                        return new AgentResponse
                        {
                            Intent = IntentType.Modification,
                            AiResponse = $"Las {time.Hours:D2}:{time.Minutes:D2} no está disponible. " +
                                        $"Horas disponibles: {string.Join(", ", decision.SuggestedHours)}. " +
                                        "¿Cuál prefieres?"
                        };
                    }

                    return new AgentResponse
                    {
                        Intent = IntentType.Modification,
                        AiResponse = ResponseVariations.ModificationTimeUnavailable() + " ¿Qué otra hora te vendría bien?"
                    };
                }
            }

            reservationTime = $"{time.Hours:D2}:{time.Minutes:D2}:00";
            changeDescriptions.Add($"hora a las {time.Hours:D2}:{time.Minutes:D2}");
        }

        // Process party size if present
        if (extractedFields.TryGetValue("party_size", out var partyObj) && partyObj is int pSize)
        {
            if (pSize > 10)
            {
                await _whatsAppService.SendTextAsync(
                    message.SenderNumber,
                    ResponseVariations.LargeGroupIntro(),
                    ct);
                await _whatsAppService.SendContactCardAsync(
                    message.SenderNumber,
                    fullName: "Gestión Reservas Villa Carmen",
                    contactPhoneNumber: "34638857294",
                    organization: "Alquería Villa Carmen",
                    cancellationToken: ct);
                _stateStore.Clear(message.SenderNumber);
                return new AgentResponse
                {
                    Intent = IntentType.Normal,
                    AiResponse = ResponseVariations.ModificationLargeGroupVCard()
                };
            }

            partySize = pSize;
            changeDescriptions.Add($"de {booking.PartySize} a {pSize} personas");
        }

        // Build BookingUpdateData with object initializer
        var pendingChanges = new BookingUpdateData
        {
            ReservationDate = reservationDate,
            ReservationTime = reservationTime,
            PartySize = partySize,
            ArrozType = arrozType,
            ArrozServings = arrozServings,
            HighChairs = highChairs,
            BabyStrollers = babyStrollers,
            ClearRice = clearRice
        };

        // Build confirmation message
        var changeDescription = string.Join(", ", changeDescriptions);
        var newState = state with
        {
            Stage = ModificationStage.AwaitingConfirmation,
            PendingChanges = pendingChanges,
            ChangeDescription = changeDescription,
            AccumulatedChanges = extractedFields,
            ExtractedFields = extractedFields.Keys.ToList(),
            ConversationTurn = state.ConversationTurn + 1
        };
        _stateStore.Set(message.SenderNumber, newState);

        // Smart confirmation message
        var confirmationMessage = extractedFields.Count switch
        {
            2 when extractedFields.ContainsKey("date") && extractedFields.ContainsKey("time") =>
                $"Perfecto, cambio tu reserva al {((DateTime)extractedFields["date"]):dd/MM/yyyy} a las {((TimeSpan)extractedFields["time"]).Hours:D2}:{((TimeSpan)extractedFields["time"]).Minutes:D2}. ¿Confirmas? (Sí/No)",
            
            _ => $"Vas a cambiar {changeDescription}. ¿Confirmas? (Sí/No)"
        };

        return new AgentResponse
        {
            Intent = IntentType.Modification,
            AiResponse = confirmationMessage
        };
    }

    /// <summary>
    /// Step 4: Handle the new value provided by the user.
    /// Enhanced with context-aware parsing and accumulator pattern.
    /// </summary>
    private async Task<AgentResponse> HandleNewValueAsync(
        WhatsAppMessage message,
        ModificationState state,
        CancellationToken ct)
    {
        var field = state.FieldToModify;
        var booking = state.SelectedBooking!;

        // NEW: Extract fields from message using natural language parser
        var extractedFields = _nlParser.ExtractFields(message.MessageText, state) ?? new Dictionary<string, object>();
        
        // NEW: Detect if user is making a correction
        if (_nlParser.IsCorrection(message.MessageText))
        {
            _logger.LogInformation("User is making a correction: {Message}", message.MessageText);
            
            // If user provides a different field than what we're asking for, handle it
            if (extractedFields.Count > 0 && !extractedFields.ContainsKey(field))
            {
                _logger.LogInformation(
                    "User provided different field {NewField} instead of {ExpectedField}",
                    string.Join(", ", extractedFields.Keys),
                    field);
                
                // Switch to the field the user actually provided
                var correctedField = extractedFields.Keys.First();
                var syntheticMessage = message with 
                { 
                    MessageText = extractedFields[correctedField].ToString() ?? "" 
                };
                
                var correctedState = state with { FieldToModify = correctedField };
                return correctedField switch
                {
                    "date" => await HandleDateChangeAsync(syntheticMessage, correctedState, ct),
                    "time" => await HandleTimeChangeAsync(syntheticMessage, correctedState, ct),
                    "party_size" => await HandlePartySizeChangeAsync(syntheticMessage, correctedState, ct),
                    _ => await HandleFieldExtractionAsync(syntheticMessage, correctedState, extractedFields, ct)
                };
            }
        }

        // NEW: Accumulate extracted fields
        if (extractedFields.Count > 0)
        {
            var accumulated = state.AccumulatedChanges ?? new Dictionary<string, object>();
            foreach (var kvp in extractedFields)
            {
                accumulated[kvp.Key] = kvp.Value;
            }

            var updatedState = state with
            {
                AccumulatedChanges = accumulated,
                ExtractedFields = accumulated.Keys.ToList(),
                ConversationTurn = state.ConversationTurn + 1
            };
            _stateStore.Set(message.SenderNumber, updatedState);

            // If we have the field we're looking for, process it
            if (extractedFields.TryGetValue(field, out var value))
            {
                var syntheticMessage = message with { MessageText = value.ToString() ?? "" };
                return field switch
                {
                    "date" => await HandleDateChangeAsync(syntheticMessage, updatedState, ct),
                    "time" => await HandleTimeChangeAsync(syntheticMessage, updatedState, ct),
                    "party_size" => await HandlePartySizeChangeAsync(syntheticMessage, updatedState, ct),
                    "rice" => await HandleRiceChangeAsync(syntheticMessage, updatedState, ct),
                    "tronas" => await HandleTronasChangeAsync(message, updatedState, ct),
                    "carritos" => await HandleCarritosChangeAsync(message, updatedState, ct),
                    _ => new AgentResponse
                    {
                        Intent = IntentType.Normal,
                        AiResponse = ResponseVariations.ModificationUnknownError()
                    }
                };
            }
        }

        // Fall back to original field-specific handlers
        return field switch
        {
            "date" => await HandleDateChangeAsync(message, state, ct),
            "time" => await HandleTimeChangeAsync(message, state, ct),
            "party_size" => await HandlePartySizeChangeAsync(message, state, ct),
            "rice" => await HandleRiceChangeAsync(message, state, ct),
            "tronas" => await HandleTronasChangeAsync(message, state, ct),
            "carritos" => await HandleCarritosChangeAsync(message, state, ct),
            _ => new AgentResponse
            {
                Intent = IntentType.Normal,
                AiResponse = ResponseVariations.ModificationUnknownError()
            }
        };
    }

    /// <summary>
    /// Handles field extraction when user provides unexpected fields.
    /// </summary>
    private async Task<AgentResponse> HandleFieldExtractionAsync(
        WhatsAppMessage message,
        ModificationState state,
        Dictionary<string, object> extractedFields,
        CancellationToken ct)
    {
        // If multiple fields extracted, use multi-field handler
        if (extractedFields.Count > 1)
        {
            return await HandleMultiFieldModificationAsync(message, state, extractedFields, ct);
        }

        // Single field - redirect to appropriate handler
        var field = extractedFields.Keys.First();
        var syntheticMessage = message with 
        { 
            MessageText = extractedFields[field].ToString() ?? "" 
        };

        return field switch
        {
            "date" => await HandleDateChangeAsync(syntheticMessage, state, ct),
            "time" => await HandleTimeChangeAsync(syntheticMessage, state, ct),
            "party_size" => await HandlePartySizeChangeAsync(syntheticMessage, state, ct),
            _ => new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = "No entendí ese cambio. ¿Puedes repetirlo?"
            }
        };
    }

    /// <summary>
    /// Step 5: Handle confirmation (yes/no) using AI intent detection.
    /// </summary>
    private async Task<AgentResponse> HandleConfirmationAsync(
        WhatsAppMessage message,
        ModificationState state,
        CancellationToken ct)
    {
        var userIntent = await _intentDetection.DetectIntentAsync(
            message.MessageText, "modification_confirm", ct);

        _logger.LogDebug("AI analyzed modification confirmation intent: {Intent}", userIntent);

        if (userIntent == "confirm")
        {
            var originalBooking = state.SelectedBooking!;
            var pendingChanges = state.PendingChanges!;

            // Apply the changes
            var success = await _bookingRepository.UpdateBookingAsync(
                originalBooking.Id,
                pendingChanges,
                ct);

            _stateStore.Clear(message.SenderNumber);

            if (success)
            {
                // Get the updated booking from DB
                var updatedBooking = await _bookingRepository.GetBookingByIdAsync(originalBooking.Id, ct);

                // Send notification to restaurant
                await SendModificationNotificationAsync(
                    originalBooking,
                    updatedBooking ?? originalBooking,
                    pendingChanges,
                    state.ChangeDescription ?? "Modificación",
                    ct);

                // Call external PHP endpoint to sync modification
                await SyncModificationToExternalSystemAsync(originalBooking.Id, pendingChanges, ct);

                return new AgentResponse
                {
                    Intent = IntentType.Normal,
                    AiResponse = ResponseVariations.ModificationSuccess()
                };
            }
            else
            {
                return new AgentResponse
                {
                    Intent = IntentType.Normal,
                    AiResponse = ResponseVariations.ModificationSaveError()
                };
            }
        }

        if (userIntent == "reject")
        {
            _stateStore.Clear(message.SenderNumber);
            return new AgentResponse
            {
                Intent = IntentType.Normal,
                AiResponse = ResponseVariations.ModificationCancelled()
            };
        }

        // Didn't understand
        return new AgentResponse
        {
            Intent = IntentType.Modification,
            AiResponse = ResponseVariations.ConfirmationNotUnderstood()
        };
    }

    #endregion

    #region Field-Specific Handlers

    private async Task<AgentResponse> HandleDateChangeAsync(
        WhatsAppMessage message,
        ModificationState state,
        CancellationToken ct)
    {
        var booking = state.SelectedBooking!;
        var text = message.MessageText.Trim();

        // Use AI-based parser to extract the date
        var extractedFields = _nlParser.ExtractFields(text, state);
        DateTime? newDate = extractedFields.TryGetValue("date", out var dateObj) && dateObj is DateTime dt
            ? dt
            : null;

        if (newDate == null)
        {
            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = ResponseVariations.DateNotUnderstood()
            };
        }

        // Use the same time as the current booking
        var time = booking.ReservationTime;

        // Check availability
        var decision = await _availabilityService.EvaluateAsync(
            newDate.Value,
            booking.PartySize,
            time,
            booking.Id,
            ct);

        if (!decision.IsAvailable)
        {
            // Same-day modifications: send intro message + contact card and end flow
            if (decision.Reason == "same_day")
            {
                _stateStore.Clear(message.SenderNumber);

                await _whatsAppService.SendTextAsync(
                    message.SenderNumber,
                    ResponseVariations.SameDayBookingIntro(),
                    ct);

                await _whatsAppService.SendContactCardAsync(
                    message.SenderNumber,
                    fullName: "Gestión Reservas Villa Carmen",
                    contactPhoneNumber: "34638857294",
                    organization: "Alquería Villa Carmen",
                    email: null,
                    cancellationToken: ct);

                return new AgentResponse
                {
                    Intent = IntentType.Normal,
                    AiResponse = ResponseVariations.SameDayBookingRejection()
                };
            }

            // 35-day window exceeded for date modification
            if (decision.Reason == "too_far_ahead")
            {
                return new AgentResponse
                {
                    Intent = IntentType.Modification,
                    AiResponse = decision.Message + " ¿Qué otra fecha te vendría bien?"
                };
            }

            // Suggest alternatives if available
            if (decision.SuggestedHours?.Count > 0)
            {
                return new AgentResponse
                {
                    Intent = IntentType.Modification,
                    AiResponse = $"El {newDate.Value:dd/MM/yyyy} a las {booking.TimeFormatted} no está disponible. " +
                                $"Horas disponibles: {string.Join(", ", decision.SuggestedHours)}. " +
                                "¿Prefieres alguna de estas o quieres otra fecha?"
                };
            }

            if (decision.SuggestedDate.HasValue)
            {
                return new AgentResponse
                {
                    Intent = IntentType.Modification,
                    AiResponse = $"{decision.Message} ¿Te viene bien el {decision.SuggestedDate.Value:dd/MM/yyyy}?"
                };
            }

            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = ResponseVariations.ModificationDateUnavailable() + " ¿Qué otra fecha te vendría bien?"
            };
        }

        // Store pending changes and ask for confirmation
        var dateStr = newDate.Value.ToString("yyyy-MM-dd");
        var pendingChanges = new BookingUpdateData { ReservationDate = dateStr };

        var newState = state with
        {
            Stage = ModificationStage.AwaitingConfirmation,
            PendingChanges = pendingChanges,
            ChangeDescription = $"cambiar la fecha del {booking.DateFormatted} al {newDate.Value:dd/MM/yyyy}"
        };
        _stateStore.Set(message.SenderNumber, newState);

        return new AgentResponse
        {
            Intent = IntentType.Modification,
            AiResponse = $"Vas a {newState.ChangeDescription}. ¿Confirmas? (Sí/No)"
        };
    }

    private async Task<AgentResponse> HandleTimeChangeAsync(
        WhatsAppMessage message,
        ModificationState state,
        CancellationToken ct)
    {
        var booking = state.SelectedBooking!;
        var text = message.MessageText.Trim();

        // Use AI-based parser to extract the time
        var extractedFields = _nlParser.ExtractFields(text, state);
        TimeSpan? newTime = extractedFields.TryGetValue("time", out var timeObj) && timeObj is TimeSpan ts
            ? ts
            : null;

        if (newTime == null)
        {
            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = ResponseVariations.TimeNotUnderstood()
            };
        }

        // Check availability
        var decision = await _availabilityService.EvaluateAsync(
            booking.ReservationDate,
            booking.PartySize,
            newTime.Value,
            booking.Id,
            ct);

        if (!decision.IsAvailable)
        {
            // Same-day modifications: send intro message + contact card and end flow
            if (decision.Reason == "same_day")
            {
                _stateStore.Clear(message.SenderNumber);

                await _whatsAppService.SendTextAsync(
                    message.SenderNumber,
                    ResponseVariations.SameDayBookingIntro(),
                    ct);

                await _whatsAppService.SendContactCardAsync(
                    message.SenderNumber,
                    fullName: "Gestión Reservas Villa Carmen",
                    contactPhoneNumber: "34638857294",
                    organization: "Alquería Villa Carmen",
                    email: null,
                    cancellationToken: ct);

                return new AgentResponse
                {
                    Intent = IntentType.Normal,
                    AiResponse = ResponseVariations.SameDayBookingRejection()
                };
            }

            if (decision.SuggestedHours?.Count > 0)
            {
                return new AgentResponse
                {
                    Intent = IntentType.Modification,
                    AiResponse = $"Las {newTime.Value.Hours:D2}:{newTime.Value.Minutes:D2} no está disponible. " +
                                $"Horas disponibles: {string.Join(", ", decision.SuggestedHours)}. " +
                                "¿Cuál prefieres?"
                };
            }

            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = ResponseVariations.ModificationTimeUnavailable() + " ¿Qué otra hora te vendría bien?"
            };
        }

        // Store pending changes
        var timeStr = $"{newTime.Value.Hours:D2}:{newTime.Value.Minutes:D2}:00";
        var pendingChanges = new BookingUpdateData { ReservationTime = timeStr };

        var newState = state with
        {
            Stage = ModificationStage.AwaitingConfirmation,
            PendingChanges = pendingChanges,
            ChangeDescription = $"cambiar la hora de las {booking.TimeFormatted} a las {newTime.Value.Hours:D2}:{newTime.Value.Minutes:D2}"
        };
        _stateStore.Set(message.SenderNumber, newState);

        return new AgentResponse
        {
            Intent = IntentType.Modification,
            AiResponse = $"Vas a {newState.ChangeDescription}. ¿Confirmas? (Sí/No)"
        };
    }

    private async Task<AgentResponse> HandlePartySizeChangeAsync(
        WhatsAppMessage message,
        ModificationState state,
        CancellationToken ct)
    {
        var booking = state.SelectedBooking!;
        var text = message.MessageText.Trim();

        // Use AI-based parser to extract party size
        var extractedFields = _nlParser.ExtractFields(text, state);
        int? newSize = extractedFields.TryGetValue("party_size", out var sizeObj) && sizeObj is int s
            ? s
            : null;

        // Fallback: simple digit extraction for straightforward cases like "8" or "8 personas"
        if (newSize == null)
        {
            var digits = new string(text.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var fallbackSize) && fallbackSize > 0)
                newSize = fallbackSize;
        }

        if (newSize == null || newSize <= 0)
        {
            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = ResponseVariations.PartySizeNotUnderstood()
            };
        }

        // Check if >10 people
        if (newSize.Value > 10)
        {
            await _whatsAppService.SendTextAsync(
                message.SenderNumber,
                ResponseVariations.LargeGroupIntro(),
                ct);

            await _whatsAppService.SendContactCardAsync(
                message.SenderNumber,
                fullName: "Gestión Reservas Villa Carmen",
                contactPhoneNumber: "34638857294",
                organization: "Alquería Villa Carmen",
                cancellationToken: ct);

            _stateStore.Clear(message.SenderNumber);

            return new AgentResponse
            {
                Intent = IntentType.Normal,
                AiResponse = ResponseVariations.ModificationLargeGroupVCard()
            };
        }

        // Check availability for new party size
        var decision = await _availabilityService.EvaluateAsync(
            booking.ReservationDate,
            newSize.Value,
            booking.ReservationTime,
            booking.Id,
            ct);

        if (!decision.IsAvailable)
        {
            // Same-day modifications: send intro message + contact card and end flow
            if (decision.Reason == "same_day")
            {
                _stateStore.Clear(message.SenderNumber);

                await _whatsAppService.SendTextAsync(
                    message.SenderNumber,
                    ResponseVariations.SameDayBookingIntro(),
                    ct);

                await _whatsAppService.SendContactCardAsync(
                    message.SenderNumber,
                    fullName: "Gestión Reservas Villa Carmen",
                    contactPhoneNumber: "34638857294",
                    organization: "Alquería Villa Carmen",
                    email: null,
                    cancellationToken: ct);

                return new AgentResponse
                {
                    Intent = IntentType.Normal,
                    AiResponse = ResponseVariations.SameDayBookingRejection()
                };
            }

            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = $"No hay sitio para {newSize} personas en esa fecha/hora. " +
                            $"{decision.Message ?? ""} ¿Quieres probar con otro número o cambiar la fecha?"
            };
        }

        // Store pending changes (keep original tronas/carritos)
        var pendingChanges = new BookingUpdateData { PartySize = newSize };

        var newState = state with
        {
            Stage = ModificationStage.AwaitingConfirmation,
            PendingChanges = pendingChanges,
            ChangeDescription = $"cambiar de {booking.PartySize} a {newSize} personas"
        };
        _stateStore.Set(message.SenderNumber, newState);

        return new AgentResponse
        {
            Intent = IntentType.Modification,
            AiResponse = $"Vas a {newState.ChangeDescription}. ¿Confirmas? (Sí/No)"
        };
    }

    private async Task<AgentResponse> HandleRiceChangeAsync(
        WhatsAppMessage message,
        ModificationState state,
        CancellationToken ct)
    {
        var booking = state.SelectedBooking!;
        var text = message.MessageText.Trim();
        var pendingRiceType = state.PendingChanges?.ArrozType;
        var bookingSummary = $"{booking.DateFormatted} ({booking.DayName}) a las {booking.TimeFormatted}, {booking.PartySize} personas";

        // Use AI rice understanding for comprehensive analysis
        var riceAnalysis = await _riceUnderstanding.AnalyzeAsync(text, bookingSummary, ct);

        _logger.LogInformation(
            "AiRiceUnderstanding for '{Message}': GenericRef={Generic}, WantsCancel={Cancel}, RiceType={Type}, Servings={Servings}, ServingsOnly={ServingsOnly}",
            text, riceAnalysis.IsGenericReference, riceAnalysis.WantsCancel,
            riceAnalysis.RiceTypeMentioned, riceAnalysis.ServingsMentioned, riceAnalysis.IsServingsOnly);

        // PRIORITY 0: Check if user is selecting from pending rice options (numbered list)
        var pendingRiceSelection = _pendingRiceStore.Get(message.SenderNumber);
        if (pendingRiceSelection?.Options != null && pendingRiceSelection.Options.Count > 0)
        {
            // Use AI to select from numbered rice options
            var selectedRice = await SelectRiceOptionAsync(text, pendingRiceSelection.Options, ct);
            if (!string.IsNullOrEmpty(selectedRice))
            {
                _logger.LogInformation(
                    "User selected rice option: {Selected} from {Count} options",
                    selectedRice,
                    pendingRiceSelection.Options.Count);

                _pendingRiceStore.Clear(message.SenderNumber);

                // Check if servings were also mentioned
                if (riceAnalysis.ServingsMentioned.HasValue)
                {
                    var pendingServings = riceAnalysis.ServingsMentioned.Value;
                    if (pendingServings < 2)
                    {
                        return new AgentResponse
                        {
                            Intent = IntentType.Modification,
                            AiResponse = ResponseVariations.MinRicePortions()
                        };
                    }

                    if (pendingServings > booking.PartySize)
                    {
                        return new AgentResponse
                        {
                            Intent = IntentType.Modification,
                            AiResponse = ResponseVariations.RiceServingsExceedPartySize(booking.PartySize)
                        };
                    }

                    var pendingChanges = new BookingUpdateData
                    {
                        ArrozType = selectedRice,
                        ArrozServings = pendingServings
                    };
                    var confirmedState = state with
                    {
                        Stage = ModificationStage.AwaitingConfirmation,
                        PendingChanges = pendingChanges,
                        ChangeDescription = $"cambiar a {selectedRice} ({pendingServings} raciones)"
                    };
                    _stateStore.Set(message.SenderNumber, confirmedState);

                    return new AgentResponse
                    {
                        Intent = IntentType.Modification,
                        AiResponse = $"Vas a {confirmedState.ChangeDescription}. ¿Confirmas? (Sí/No)"
                    };
                }

                // Only rice type selected - ask for servings
                var tempState = state with
                {
                    PendingChanges = new BookingUpdateData { ArrozType = selectedRice }
                };
                _stateStore.Set(message.SenderNumber, tempState);

                return new AgentResponse
                {
                    Intent = IntentType.Modification,
                    AiResponse = $"✅ {selectedRice} disponible. ¿Cuántas raciones quieres? (mínimo 2, máximo {booking.PartySize})"
                };
            }
            else
            {
                _logger.LogInformation("Could not parse rice selection from: {Message}", text);
                var formattedOptions = string.Join("\n", pendingRiceSelection.Options.Select((r, i) => $"{i + 1}. {r}"));
                var retryMsg = $"No he entendido tu elección. Por favor, dime el número de la opción que prefieres:\n\n{formattedOptions}";
                return new AgentResponse
                {
                    Intent = IntentType.Modification,
                    AiResponse = retryMsg
                };
            }
        }

        // Check if canceling rice using AI intent detection
        var riceCancelIntent = await _intentDetection.DetectIntentAsync(text, "rice_cancel", ct);
        if (riceCancelIntent == "cancel_rice" || riceAnalysis.WantsCancel)
        {
            var pendingChanges = new BookingUpdateData { ClearRice = true };
            var newState = state with
            {
                Stage = ModificationStage.AwaitingConfirmation,
                PendingChanges = pendingChanges,
                ChangeDescription = "cancelar el arroz de la reserva"
            };
            _stateStore.Set(message.SenderNumber, newState);

            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = $"Vas a {newState.ChangeDescription}. ¿Confirmas? (Sí/No)"
            };
        }

        // Check if changing servings only
        if (riceAnalysis.IsServingsOnly && riceAnalysis.ServingsMentioned.HasValue)
        {
            var newServings = riceAnalysis.ServingsMentioned.Value;

            if (newServings < 2)
            {
                return new AgentResponse
                {
                    Intent = IntentType.Modification,
                    AiResponse = ResponseVariations.MinRicePortions()
                };
            }

            if (newServings > booking.PartySize)
            {
                return new AgentResponse
                {
                    Intent = IntentType.Modification,
                    AiResponse = ResponseVariations.RiceServingsExceedPartySize(booking.PartySize)
                };
            }

            var pendingChanges = new BookingUpdateData
            {
                ArrozType = pendingRiceType,
                ArrozServings = newServings
            };
            var newState = state with
            {
                Stage = ModificationStage.AwaitingConfirmation,
                PendingChanges = pendingChanges,
                ChangeDescription = !string.IsNullOrWhiteSpace(pendingRiceType)
                    ? $"cambiar a {pendingRiceType} ({newServings} raciones)"
                    : $"cambiar a {newServings} raciones de arroz"
            };
            _stateStore.Set(message.SenderNumber, newState);

            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = $"Vas a {newState.ChangeDescription}. ¿Confirmas? (Sí/No)"
            };
        }

        // Check for generic rice reference (user says "arroz" without specific type)
        if (riceAnalysis.IsGenericReference)
        {
            var stateAskRiceType = state with
            {
                Stage = ModificationStage.CollectingNewValue,
                FieldToModify = "rice",
                PendingChanges = null
            };
            _stateStore.Set(message.SenderNumber, stateAskRiceType);

            var currentRice = string.IsNullOrWhiteSpace(booking.ArrozType)
                ? "Actualmente no tienes arroz en la reserva."
                : $"Actualmente tienes {booking.ArrozType} ({booking.ArrozServings} raciones).";

            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = $"{currentRice}\n\nPerfecto, te ayudo a cambiarlo. ¿Qué arroz quieres poner? Puedes verlo aquí: https://alqueriavillacarmen.com/menufindesemana.php"
            };
        }

        // If a specific rice type was mentioned, validate it
        var riceTextToValidate = riceAnalysis.RiceTypeMentioned ?? text;
        var validation = await _riceValidator.ValidateAsync(riceTextToValidate, "villacarmen", ct);

        if (!validation.IsValid)
        {
            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = validation.Message ?? "No tenemos ese tipo de arroz. Puedes ver el menú en: https://alqueriavillacarmen.com/menufindesemana.php"
            };
        }

        // Check if servings were also provided
        if (riceAnalysis.ServingsMentioned.HasValue)
        {
            var servings = riceAnalysis.ServingsMentioned.Value;
            if (servings < 2)
            {
                return new AgentResponse
                {
                    Intent = IntentType.Modification,
                    AiResponse = ResponseVariations.MinRicePortions()
                };
            }

            if (servings > booking.PartySize)
            {
                return new AgentResponse
                {
                    Intent = IntentType.Modification,
                    AiResponse = ResponseVariations.RiceServingsExceedPartySize(booking.PartySize)
                };
            }

            var changes = new BookingUpdateData
            {
                ArrozType = validation.RiceName,
                ArrozServings = servings
            };
            var finalState = state with
            {
                Stage = ModificationStage.AwaitingConfirmation,
                PendingChanges = changes,
                ChangeDescription = $"cambiar a {validation.RiceName} ({servings} raciones)"
            };
            _stateStore.Set(message.SenderNumber, finalState);

            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = $"Vas a {finalState.ChangeDescription}. ¿Confirmas? (Sí/No)"
            };
        }

        // Valid rice but no servings - ask for servings
        var tempState2 = state with
        {
            PendingChanges = new BookingUpdateData { ArrozType = validation.RiceName }
        };
        _stateStore.Set(message.SenderNumber, tempState2);

        return new AgentResponse
        {
            Intent = IntentType.Modification,
            AiResponse = $"✅ {validation.RiceName} disponible. ¿Cuántas raciones quieres? (mínimo 2, máximo {booking.PartySize})"
        };
    }

    private Task<AgentResponse> HandleTronasChangeAsync(
        WhatsAppMessage message,
        ModificationState state,
        CancellationToken ct)
    {
        var text = message.MessageText.Trim();
        var digits = new string(text.Where(char.IsDigit).ToArray());

        if (!int.TryParse(digits, out var newCount) || newCount < 0)
        {
            return Task.FromResult(new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = ResponseVariations.TronasNotUnderstood()
            });
        }

        if (newCount > 3)
        {
            return Task.FromResult(new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = ResponseVariations.MaxTronas()
            });
        }

        var pendingChanges = new BookingUpdateData { HighChairs = newCount };
        var newState = state with
        {
            Stage = ModificationStage.AwaitingConfirmation,
            PendingChanges = pendingChanges,
            ChangeDescription = $"cambiar a {newCount} tronas"
        };
        _stateStore.Set(message.SenderNumber, newState);

        return Task.FromResult(new AgentResponse
        {
            Intent = IntentType.Modification,
            AiResponse = $"Vas a {newState.ChangeDescription}. ¿Confirmas? (Sí/No)"
        });
    }

    private Task<AgentResponse> HandleCarritosChangeAsync(
        WhatsAppMessage message,
        ModificationState state,
        CancellationToken ct)
    {
        var text = message.MessageText.Trim();
        var digits = new string(text.Where(char.IsDigit).ToArray());

        if (!int.TryParse(digits, out var newCount) || newCount < 0)
        {
            return Task.FromResult(new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = ResponseVariations.CarritosNotUnderstood()
            });
        }

        if (newCount > 3)
        {
            return Task.FromResult(new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = ResponseVariations.MaxCarritos()
            });
        }

        var pendingChanges = new BookingUpdateData { BabyStrollers = newCount };
        var newState = state with
        {
            Stage = ModificationStage.AwaitingConfirmation,
            PendingChanges = pendingChanges,
            ChangeDescription = $"cambiar a {newCount} carritos"
        };
        _stateStore.Set(message.SenderNumber, newState);

        return Task.FromResult(new AgentResponse
        {
            Intent = IntentType.Modification,
            AiResponse = $"Vas a {newState.ChangeDescription}. ¿Confirmas? (Sí/No)"
        });
    }

    #endregion

    #region Helper Methods

    private bool IsUnsupportedContent(WhatsAppMessage message)
    {
        // Check for media types
        var mediaTypes = new[] { "audio", "image", "video", "document", "sticker", "location" };
        return mediaTypes.Contains(message.MessageType?.ToLowerInvariant());
    }

    private async Task<AgentResponse> HandleUnsupportedContentAsync(
        WhatsAppMessage message,
        CancellationToken ct)
    {
        await _whatsAppService.SendTextAsync(
            message.SenderNumber,
            ResponseVariations.ModificationUnsupportedRequest(),
            ct);

        await _whatsAppService.SendContactCardAsync(
            message.SenderNumber,
            fullName: "Gestión Reservas Villa Carmen",
            contactPhoneNumber: "34638857294",
            organization: "Alquería Villa Carmen",
            cancellationToken: ct);

        // Continue conversation - don't clear state
        return new AgentResponse
        {
            Intent = IntentType.Normal,
            AiResponse = "" // Already sent via WhatsApp
        };
    }

    private static string NormalizePhoneTo9Digits(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length > 9 ? digits[^9..] : digits;
    }

    /// <summary>
    /// Uses AI to select a rice option from a numbered list.
    /// Replaces regex-based TryParseRiceSelection.
    /// </summary>
    private async Task<string?> SelectRiceOptionAsync(
        string userMessage,
        List<string> options,
        CancellationToken ct)
    {
        try
        {
            // Use AI booking selection service pattern for rice option selection
            var text = userMessage.Trim().ToLowerInvariant();

            // Simple number selection: "1", "2"
            if (int.TryParse(text, out var num) && num >= 1 && num <= options.Count)
                return options[num - 1];

            // Use AI rice understanding to check if user mentioned a specific rice type
            var bookingSummary = "selección de arroz en reserva";
            var analysis = await _riceUnderstanding.AnalyzeAsync(userMessage, bookingSummary, ct);

            // If AI identified a rice type, match it against options
            if (!string.IsNullOrEmpty(analysis.RiceTypeMentioned))
            {
                var mentioned = analysis.RiceTypeMentioned.ToLowerInvariant();
                foreach (var option in options)
                {
                    if (option.ToLowerInvariant().Contains(mentioned) || mentioned.Contains(option.ToLowerInvariant()))
                        return option;
                }
            }

            // Partial name matching as fallback
            foreach (var option in options)
            {
                var optionLower = option.ToLowerInvariant();
                if (optionLower.Contains(text) || text.Contains(optionLower))
                    return option;
            }

            // Check key words
            var words = text.Split(new[] { ' ', ',', '.' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var option in options)
            {
                var optionLower = option.ToLowerInvariant();
                foreach (var word in words)
                {
                    if (word.Length >= 3 && optionLower.Contains(word))
                        return option;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SelectRiceOptionAsync failed for message: '{Message}'", userMessage);
            return null;
        }
    }

    private AgentResponse BuildSelectBookingResponse(List<BookingRecord> bookings)
    {
        var sb = new StringBuilder();
        sb.AppendLine(ResponseVariations.ModificationSelectBooking());
        sb.AppendLine();

        for (int i = 0; i < bookings.Count; i++)
        {
            var b = bookings[i];
            sb.AppendLine($"*{i + 1}.* {b.Summary}");
        }

        sb.AppendLine();
        sb.AppendLine("¿Cuál quieres modificar?");

        return new AgentResponse
        {
            Intent = IntentType.Modification,
            AiResponse = sb.ToString()
        };
    }

    private AgentResponse BuildSelectFieldResponse(BookingRecord booking)
    {
        var riceInfo = string.IsNullOrEmpty(booking.ArrozType)
            ? "Sin arroz"
            : $"{booking.ArrozType} ({booking.ArrozServings} raciones)";

        var sb = new StringBuilder();
        sb.AppendLine($"Reserva: *{booking.Summary}*");
        sb.AppendLine($"Arroz: {riceInfo}");
        sb.AppendLine($"Tronas: {booking.HighChairs}, Carritos: {booking.BabyStrollers}");
        sb.AppendLine();
        
        // NEW: More natural, conversational prompt
        sb.AppendLine("Cuéntame, ¿qué quieres cambiar?");
        sb.AppendLine();
        sb.AppendLine("Puedes decirme directamente lo que quieres modificar, por ejemplo:");
        sb.AppendLine("• \"cambiar la fecha\" o \"para el domingo\"");
        sb.AppendLine("• \"cambiar la hora\" o \"a las 14:30\"");
        sb.AppendLine("• \"más personas\" o \"somos 8\"");
        sb.AppendLine("• \"quiero arroz\" o \"paella para 4\"");
        sb.AppendLine();
        sb.AppendLine("O elige una opción:");
        sb.AppendLine("1️⃣ Fecha");
        sb.AppendLine("2️⃣ Hora");
        sb.AppendLine("3️⃣ Personas");
        sb.AppendLine("4️⃣ Arroz");
        sb.AppendLine("5️⃣ Tronas");
        sb.AppendLine("6️⃣ Carritos");

        return new AgentResponse
        {
            Intent = IntentType.Modification,
            AiResponse = sb.ToString()
        };
    }

    private AgentResponse BuildAskNewValueResponse(string field, BookingRecord booking)
    {
        var prompt = field switch
        {
            "date" => $"La reserva actual es para el {booking.DateFormatted}. {ResponseVariations.ModificationAskNewDate()}",
            "time" => $"La hora actual es {booking.TimeFormatted}. {ResponseVariations.ModificationAskNewTime()}",
            "party_size" => $"Actualmente son {booking.PartySize} personas. {ResponseVariations.ModificationAskNewPartySize()}",
            "rice" => booking.ArrozType != null
                ? $"Actualmente tienes {booking.ArrozType} ({booking.ArrozServings} raciones). {ResponseVariations.ModificationAskNewRice()}"
                : "Actualmente no tienes arroz. ¿Quieres añadir arroz? Indica el tipo y las raciones.",
            "tronas" => $"Actualmente tienes {booking.HighChairs} tronas. ¿Cuántas necesitas? (máximo 3)",
            "carritos" => $"Actualmente tienes {booking.BabyStrollers} carritos. ¿Cuántos traes? (máximo 3)",
            _ => "¿Cuál es el nuevo valor?"
        };

        return new AgentResponse
        {
            Intent = IntentType.Modification,
            AiResponse = prompt
        };
    }

    #endregion

    #region Notifications

    /// <summary>
    /// Management team phone numbers for modification alerts.
    /// </summary>
    private static readonly string[] ManagementPhones = new[]
    {
        "34692747052",
        "34638857294",
        "34686969914"
    };

    /// <summary>
    /// Sends a notification to the restaurant when a booking is modified.
    /// </summary>
    private async Task SendModificationNotificationAsync(
        BookingRecord originalBooking,
        BookingRecord updatedBooking,
        BookingUpdateData changes,
        string changeDescription,
        CancellationToken ct)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("🔔 *Reserva modificada por Asistente de IA de Villa Carmen*");
            sb.AppendLine();
            sb.AppendLine("📋 *INFORMACIÓN DE LA RESERVA:*");
            sb.AppendLine($"👤 Nombre: {updatedBooking.CustomerName}");
            sb.AppendLine($"📞 Teléfono: {updatedBooking.ContactPhone}");
            sb.AppendLine($"📅 Fecha: {updatedBooking.DateFormatted} ({updatedBooking.DayName})");
            sb.AppendLine($"🕐 Hora: {updatedBooking.TimeFormatted}");
            sb.AppendLine($"👥 Personas: {updatedBooking.PartySize}");

            if (!string.IsNullOrEmpty(updatedBooking.ArrozType))
            {
                sb.AppendLine($"🍚 Arroz: {updatedBooking.ArrozType} ({updatedBooking.ArrozServings} raciones)");
            }
            else
            {
                sb.AppendLine("🍚 Arroz: Sin arroz");
            }

            sb.AppendLine($"🪑 Tronas: {updatedBooking.HighChairs}");
            sb.AppendLine($"🛒 Carritos: {updatedBooking.BabyStrollers}");
            sb.AppendLine();
            sb.AppendLine("✏️ *CAMBIOS REALIZADOS:*");

            // Show before/after for each changed field
            if (changes.ReservationDate != null)
            {
                sb.AppendLine($"📅 Fecha: {originalBooking.DateFormatted} → {updatedBooking.DateFormatted}");
            }

            if (changes.ReservationTime != null)
            {
                sb.AppendLine($"🕐 Hora: {originalBooking.TimeFormatted} → {updatedBooking.TimeFormatted}");
            }

            if (changes.PartySize.HasValue)
            {
                sb.AppendLine($"👥 Personas: {originalBooking.PartySize} → {updatedBooking.PartySize}");
            }

            if (changes.ClearRice)
            {
                var originalRice = originalBooking.ArrozType != null
                    ? $"{originalBooking.ArrozType} ({originalBooking.ArrozServings} raciones)"
                    : "Sin arroz";
                sb.AppendLine($"🍚 Arroz: {originalRice} → Sin arroz");
            }
            else if (changes.ArrozType != null || changes.ArrozServings.HasValue)
            {
                var originalRice = originalBooking.ArrozType != null
                    ? $"{originalBooking.ArrozType} ({originalBooking.ArrozServings} raciones)"
                    : "Sin arroz";
                var newRice = updatedBooking.ArrozType != null
                    ? $"{updatedBooking.ArrozType} ({updatedBooking.ArrozServings} raciones)"
                    : "Sin arroz";
                sb.AppendLine($"🍚 Arroz: {originalRice} → {newRice}");
            }

            if (changes.HighChairs.HasValue)
            {
                sb.AppendLine($"🪑 Tronas: {originalBooking.HighChairs} → {updatedBooking.HighChairs}");
            }

            if (changes.BabyStrollers.HasValue)
            {
                sb.AppendLine($"🛒 Carritos: {originalBooking.BabyStrollers} → {updatedBooking.BabyStrollers}");
            }

            sb.AppendLine();
            sb.AppendLine($"🆔 ID Reserva: {updatedBooking.Id}");

            var message = sb.ToString();
            foreach (var phone in ManagementPhones)
            {
                try
                {
                    await _whatsAppService.SendTextAsync(phone, message, ct);
                    _logger.LogDebug("Sent modification notification to {Phone}", phone);
                }
                catch (Exception phoneEx)
                {
                    _logger.LogError(phoneEx, "Failed to send modification notification to {Phone}", phone);
                }
            }

            _logger.LogInformation(
                "Sent modification notification for booking {BookingId} to management team",
                updatedBooking.Id);
        }
        catch (Exception ex)
        {
            // Log but don't fail the modification if notification fails
            _logger.LogError(ex,
                "Failed to send modification notification for booking {BookingId}",
                updatedBooking.Id);
        }
    }

    /// <summary>
    /// Syncs booking modifications to the external PHP system via HTTP API.
    /// This ensures the external restaurant system is updated with changes made through the bot.
    /// </summary>
    private async Task SyncModificationToExternalSystemAsync(
        int bookingId,
        BookingUpdateData changes,
        CancellationToken ct)
    {
        try
        {
            // Map BookingUpdateData fields to external API calls
            var tasks = new List<Task<bool>>();

            if (changes.ReservationDate != null)
            {
                tasks.Add(_externalReservationService.UpdateReservationFieldAsync(
                    bookingId, "reservation_date", changes.ReservationDate, ct));
            }

            if (changes.ReservationTime != null)
            {
                // Format time as HH:MM:SS
                var timeValue = changes.ReservationTime;
                if (!timeValue.Contains(":"))
                {
                    timeValue = timeValue + ":00";
                }
                tasks.Add(_externalReservationService.UpdateReservationFieldAsync(
                    bookingId, "reservation_time", timeValue, ct));
            }

            if (changes.PartySize.HasValue)
            {
                tasks.Add(_externalReservationService.UpdateReservationFieldAsync(
                    bookingId, "party_size", changes.PartySize.Value.ToString(), ct));
            }

            if (changes.ClearRice)
            {
                tasks.Add(_externalReservationService.UpdateReservationFieldAsync(
                    bookingId, "rice_type", "", ct));
            }
            else if (changes.ArrozType != null)
            {
                tasks.Add(_externalReservationService.UpdateReservationFieldAsync(
                    bookingId, "rice_type", changes.ArrozType, ct));
            }

            if (changes.ArrozServings.HasValue)
            {
                tasks.Add(_externalReservationService.UpdateReservationFieldAsync(
                    bookingId, "arroz_servings", changes.ArrozServings.Value.ToString(), ct));
            }

            if (changes.HighChairs.HasValue)
            {
                tasks.Add(_externalReservationService.UpdateReservationFieldAsync(
                    bookingId, "high_chairs", changes.HighChairs.Value.ToString(), ct));
            }

            if (changes.BabyStrollers.HasValue)
            {
                tasks.Add(_externalReservationService.UpdateReservationFieldAsync(
                    bookingId, "baby_strollers", changes.BabyStrollers.Value.ToString(), ct));
            }

            // Execute all updates in parallel
            if (tasks.Count > 0)
            {
                var results = await Task.WhenAll(tasks);
                var successCount = results.Count(r => r);
                
                _logger.LogInformation(
                    "External sync for booking {BookingId}: {Success}/{Total} fields updated",
                    bookingId, successCount, tasks.Count);
            }
            else
            {
                _logger.LogDebug(
                    "No fields to sync to external system for booking {BookingId}",
                    bookingId);
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail - local DB update already succeeded
            _logger.LogWarning(ex,
                "Failed to sync some fields to external system for booking {BookingId}",
                bookingId);
        }
    }

    #endregion

    #region Legacy Method (kept for backwards compatibility)

    /// <summary>
    /// Legacy entry point - redirects to new ProcessModificationAsync.
    /// </summary>
    public async Task<AgentResponse> StartModificationFlowAsync(
        string senderNumber,
        CancellationToken cancellationToken = default)
    {
        var message = new WhatsAppMessage
        {
            SenderNumber = senderNumber,
            PushName = "Cliente",
            MessageText = "",
            MessageType = "text"
        };

        return await ProcessModificationAsync(message, null, cancellationToken);
    }

    #endregion

    #region Rice Modification Shortcut

    /// <summary>
    /// Starts a modification flow pre-configured for rice changes.
    /// Skips the "what do you want to modify?" step.
    /// </summary>
    public async Task<AgentResponse> StartRiceModificationAsync(
        WhatsAppMessage message,
        BookingRecord booking,
        string? preExtractedRiceType,
        int? preExtractedServings,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(preExtractedRiceType))
        {
            var bookingSummary = $"{booking.DateFormatted} ({booking.DayName}) a las {booking.TimeFormatted}, {booking.PartySize} personas";
            var riceCheck = await _riceUnderstanding.AnalyzeAsync(preExtractedRiceType, bookingSummary, ct);
            if (riceCheck.IsGenericReference)
                preExtractedRiceType = null;
        }

        _logger.LogInformation(
            "Starting rice modification for booking {BookingId}, pre-extracted rice: {Rice}, servings: {Servings}",
            booking.Id, preExtractedRiceType ?? "(none)", preExtractedServings?.ToString() ?? "N/A");

        // NEW: Check if booking is for same day BEFORE showing rice options.
        // Same-day rice modifications must be handled by phone.
        if (booking.ReservationDate.Date <= DateTime.Now.Date)
        {
            _logger.LogInformation(
                "Same-day rice modification rejected for booking {BookingId} (date: {Date})",
                booking.Id, booking.ReservationDate.ToString("yyyy-MM-dd"));

            _stateStore.Clear(message.SenderNumber);

            await _whatsAppService.SendTextAsync(
                message.SenderNumber,
                ResponseVariations.SameDayBookingIntro(),
                ct);

            await _whatsAppService.SendContactCardAsync(
                message.SenderNumber,
                fullName: "Gestión Reservas Villa Carmen",
                contactPhoneNumber: "34638857294",
                organization: "Alquería Villa Carmen",
                email: null,
                cancellationToken: ct);

            var followUp = ResponseVariations.SameDayBookingRejection();
            await _whatsAppService.SendTextAsync(message.SenderNumber, followUp, ct);

            return new AgentResponse
            {
                Intent = IntentType.Normal,
                AiResponse = followUp,
                Metadata = new Dictionary<string, object> { ["outboundAlreadySent"] = true }
            };
        }

        // If rice type was extracted, validate it first
        if (!string.IsNullOrWhiteSpace(preExtractedRiceType))
        {
            var validation = await _riceValidator.ValidateAsync(
                preExtractedRiceType, "villacarmen", ct);

            if (validation.IsValid && !string.IsNullOrEmpty(validation.RiceName))
            {
                // Valid rice - check if we also have servings
                if (preExtractedServings.HasValue && preExtractedServings.Value >= 2)
                {
                    // We have both rice and valid servings - go straight to confirmation
                    var pendingChanges = new BookingUpdateData
                    {
                        ArrozType = validation.RiceName,
                        ArrozServings = preExtractedServings.Value
                    };

                    var state = new ModificationState
                    {
                        PhoneNumber = message.SenderNumber,
                        Stage = ModificationStage.AwaitingConfirmation,
                        FoundBookings = new List<BookingRecord> { booking },
                        SelectedBooking = booking,
                        FieldToModify = "rice",
                        PendingChanges = pendingChanges,
                        ChangeDescription = $"añadir {validation.RiceName} ({preExtractedServings.Value} raciones)"
                    };
                    _stateStore.Set(message.SenderNumber, state);

                    var currentRice = string.IsNullOrEmpty(booking.ArrozType)
                        ? "sin arroz"
                        : $"{booking.ArrozType} ({booking.ArrozServings} raciones)";

                    return new AgentResponse
                    {
                        Intent = IntentType.Modification,
                        AiResponse = $"Vas a {state.ChangeDescription} a tu reserva del {booking.DateFormatted} (actualmente {currentRice}). ¿Confirmas? (Sí/No)"
                    };
                }

                // Valid rice but need servings
                var stateNeedServings = new ModificationState
                {
                    PhoneNumber = message.SenderNumber,
                    Stage = ModificationStage.CollectingNewValue,
                    FoundBookings = new List<BookingRecord> { booking },
                    SelectedBooking = booking,
                    FieldToModify = "rice",
                    PendingChanges = new BookingUpdateData { ArrozType = validation.RiceName }
                };
                _stateStore.Set(message.SenderNumber, stateNeedServings);

                var currentRiceInfo = string.IsNullOrEmpty(booking.ArrozType)
                    ? "sin arroz"
                    : $"{booking.ArrozType} ({booking.ArrozServings} raciones)";

                return new AgentResponse
                {
                    Intent = IntentType.Modification,
                    AiResponse = $"Perfecto, {validation.RiceName} disponible.\n\n" +
                                $"Tu reserva del {booking.DateFormatted}: actualmente {currentRiceInfo}.\n\n" +
                                $"¿Cuántas raciones queréis? (mínimo 2, máximo {booking.PartySize})"
                };
            }

            if (validation.Status == "multiple" && validation.Options?.Count > 0)
            {
                // Multiple matches - ask user to choose
                var state = new ModificationState
                {
                    PhoneNumber = message.SenderNumber,
                    Stage = ModificationStage.CollectingNewValue,
                    FoundBookings = new List<BookingRecord> { booking },
                    SelectedBooking = booking,
                    FieldToModify = "rice"
                };
                _stateStore.Set(message.SenderNumber, state);

                // Store options in PENDING RICE STORE for later selection parsing
                _pendingRiceStore.Set(message.SenderNumber, new PendingRiceSelection
                {
                    Options = validation.Options,
                    OriginalRequest = preExtractedRiceType ?? ""
                });

                var options = string.Join("\n", validation.Options.Select((r, i) => $"{i + 1}. {r}"));
                return new AgentResponse
                {
                    Intent = IntentType.Modification,
                    AiResponse = $"He encontrado varias opciones parecidas. Elige una, por favor:\n\n{options}\n\nPuedes decirme el número o el nombre del arroz."
                };
            }

            // Invalid rice
            var invalidState = new ModificationState
            {
                PhoneNumber = message.SenderNumber,
                Stage = ModificationStage.CollectingNewValue,
                FoundBookings = new List<BookingRecord> { booking },
                SelectedBooking = booking,
                FieldToModify = "rice"
            };
            _stateStore.Set(message.SenderNumber, invalidState);

            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = validation.Message ??
                    "No tenemos ese arroz. Puedes ver la carta en: https://alqueriavillacarmen.com/menufindesemana.php\n\n¿Qué arroz te gustaría añadir?"
            };
        }

        // No rice type extracted - set up for rice modification and ask
        var modState = new ModificationState
        {
            PhoneNumber = message.SenderNumber,
            Stage = ModificationStage.CollectingNewValue,
            FoundBookings = new List<BookingRecord> { booking },
            SelectedBooking = booking,
            FieldToModify = "rice"
        };
        _stateStore.Set(message.SenderNumber, modState);

        var currentRiceInfoGeneric = string.IsNullOrEmpty(booking.ArrozType)
            ? "Actualmente no tienes arroz en la reserva."
            : $"Actualmente tienes {booking.ArrozType} ({booking.ArrozServings} raciones).";

        return new AgentResponse
        {
            Intent = IntentType.Modification,
            AiResponse = $"{currentRiceInfoGeneric}\n\n¿Qué arroz te gustaría? Puedes ver la carta en: https://alqueriavillacarmen.com/menufindesemana.php"
        };
    }

    /// <summary>
    /// Starts a rice modification flow when user has multiple bookings.
    /// Asks which booking to modify first, storing the pre-extracted rice info.
    /// </summary>
    public async Task<AgentResponse> StartRiceModificationWithSelectionAsync(
        WhatsAppMessage message,
        List<BookingRecord> bookings,
        string? preExtractedRiceType,
        int? preExtractedServings,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(preExtractedRiceType))
        {
            var firstBooking = bookings.FirstOrDefault();
            var bookingSummary = firstBooking != null
                ? $"{firstBooking.DateFormatted} ({firstBooking.DayName}) a las {firstBooking.TimeFormatted}, {firstBooking.PartySize} personas"
                : "reserva de restaurante";
            var riceCheck = await _riceUnderstanding.AnalyzeAsync(preExtractedRiceType, bookingSummary, ct);
            if (riceCheck.IsGenericReference)
            {
                preExtractedRiceType = null;
                preExtractedServings = null;
            }
        }

        _logger.LogInformation(
            "Starting rice modification with selection for {Phone}, {Count} bookings, pre-extracted rice: {Rice}",
            message.SenderNumber, bookings.Count, preExtractedRiceType ?? "(none)");

        // Store state with pre-extracted rice info for later use
        var state = new ModificationState
        {
            PhoneNumber = message.SenderNumber,
            Stage = ModificationStage.SelectingBooking,
            FoundBookings = bookings,
            FieldToModify = "rice", // Pre-select rice field
            // Store extracted rice info in PendingChanges for later
            PendingChanges = preExtractedRiceType != null
                ? new BookingUpdateData 
                { 
                    ArrozType = preExtractedRiceType,
                    ArrozServings = preExtractedServings
                }
                : null
        };
        _stateStore.Set(message.SenderNumber, state);

        // Build booking selection response
        var sb = new StringBuilder();
        sb.AppendLine("Tienes varias reservas activas. ¿A cuál quieres añadir arroz?");
        sb.AppendLine();

        for (int i = 0; i < bookings.Count; i++)
        {
            var b = bookings[i];
            var riceInfo = string.IsNullOrEmpty(b.ArrozType)
                ? "sin arroz"
                : $"{b.ArrozType} ({b.ArrozServings} raciones)";
            sb.AppendLine($"{i + 1}. *{b.DateFormatted}* a las *{b.TimeFormatted}* ({b.PartySize} personas, {riceInfo})");
        }

        sb.AppendLine();
        sb.Append("Responde con el número o di algo como \"la del sábado\"");

        return new AgentResponse
        {
            Intent = IntentType.Modification,
            AiResponse = sb.ToString()
        };
    }

    #endregion
}
