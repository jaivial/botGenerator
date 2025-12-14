using BotGenerator.Core.Agents;
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;

namespace BotGenerator.Core.Handlers;

/// <summary>
/// Handler for modifying existing bookings.
/// Manages the multi-turn modification conversation flow.
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

    // Spanish day names for lazy response parsing
    private static readonly Dictionary<string, DayOfWeek> SpanishDays = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lunes"] = DayOfWeek.Monday,
        ["martes"] = DayOfWeek.Tuesday,
        ["miércoles"] = DayOfWeek.Wednesday,
        ["miercoles"] = DayOfWeek.Wednesday,
        ["jueves"] = DayOfWeek.Thursday,
        ["viernes"] = DayOfWeek.Friday,
        ["sábado"] = DayOfWeek.Saturday,
        ["sabado"] = DayOfWeek.Saturday,
        ["domingo"] = DayOfWeek.Sunday
    };

    // Ordinal mappings for "la primera", "la segunda", etc.
    private static readonly Dictionary<string, int> OrdinalMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["primera"] = 0, ["1"] = 0, ["la 1"] = 0, ["uno"] = 0,
        ["segunda"] = 1, ["2"] = 1, ["la 2"] = 1, ["dos"] = 1,
        ["tercera"] = 2, ["3"] = 2, ["la 3"] = 2, ["tres"] = 2,
        ["cuarta"] = 3, ["4"] = 3, ["la 4"] = 3, ["cuatro"] = 3,
        ["quinta"] = 4, ["5"] = 4, ["la 5"] = 4, ["cinco"] = 4
    };

    public ModificationHandler(
        ILogger<ModificationHandler> logger,
        IBookingRepository bookingRepository,
        IModificationStateStore stateStore,
        IBookingAvailabilityService availabilityService,
        RiceValidatorAgent riceValidator,
        IWhatsAppService whatsAppService,
        IContextBuilderService contextBuilder)
    {
        _logger = logger;
        _bookingRepository = bookingRepository;
        _stateStore = stateStore;
        _availabilityService = availabilityService;
        _riceValidator = riceValidator;
        _whatsAppService = whatsAppService;
        _contextBuilder = contextBuilder;
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
    /// Step 2: Handle booking selection from multiple bookings.
    /// Supports lazy answers like "la primera", "la del sábado", etc.
    /// </summary>
    private async Task<AgentResponse> HandleBookingSelectionAsync(
        WhatsAppMessage message,
        ModificationState state,
        CancellationToken ct)
    {
        var bookings = state.FoundBookings ?? new List<BookingRecord>();
        var text = message.MessageText.ToLowerInvariant().Trim();

        BookingRecord? selected = null;

        // Try to parse the selection
        selected = TryParseBookingSelection(text, bookings);

        if (selected == null)
        {
            // Couldn't understand, ask again
            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = ResponseVariations.BookingSelectionNotUnderstood()
            };
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
    /// </summary>
    private async Task<AgentResponse> HandleFieldSelectionAsync(
        WhatsAppMessage message,
        ModificationState state,
        CancellationToken ct)
    {
        var text = message.MessageText.ToLowerInvariant().Trim();
        string? field = null;

        // Parse which field to modify
        if (Regex.IsMatch(text, @"\b(fecha|día|dia)\b"))
            field = "date";
        else if (Regex.IsMatch(text, @"\b(hora|horario)\b"))
            field = "time";
        else if (Regex.IsMatch(text, @"\b(personas?|comensales?|gente)\b") || text == "3")
            field = "party_size";
        else if (Regex.IsMatch(text, @"\b(arroz|paella|raciones?)\b") || text == "4")
            field = "rice";
        else if (Regex.IsMatch(text, @"\b(tronas?|sillas?)\b") || text == "5")
            field = "tronas";
        else if (Regex.IsMatch(text, @"\b(carritos?|cochecitos?)\b") || text == "6")
            field = "carritos";
        else if (text == "1")
            field = "date";
        else if (text == "2")
            field = "time";

        if (field == null)
        {
            // Couldn't understand, ask again
            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = ResponseVariations.FieldSelectionNotUnderstood()
            };
        }

        // Update state
        var newState = state with
        {
            Stage = ModificationStage.CollectingNewValue,
            FieldToModify = field
        };
        _stateStore.Set(message.SenderNumber, newState);

        // Ask for the new value
        return BuildAskNewValueResponse(field, state.SelectedBooking!);
    }

    /// <summary>
    /// Step 4: Handle the new value provided by the user.
    /// </summary>
    private async Task<AgentResponse> HandleNewValueAsync(
        WhatsAppMessage message,
        ModificationState state,
        CancellationToken ct)
    {
        var field = state.FieldToModify;
        var booking = state.SelectedBooking!;

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
    /// Step 5: Handle confirmation (yes/no).
    /// </summary>
    private async Task<AgentResponse> HandleConfirmationAsync(
        WhatsAppMessage message,
        ModificationState state,
        CancellationToken ct)
    {
        var text = message.MessageText.ToLowerInvariant().Trim();

        // Check for confirmation (allow words anywhere in the message)
        if (Regex.IsMatch(text, @"\b(sí|si|yes|confirmo|vale|ok|perfecto|de acuerdo)\b"))
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

        // Check for cancellation (allow words anywhere in the message)
        if (Regex.IsMatch(text, @"\b(no|cancelar|nada|dejalo|déjalo)\b"))
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

        // Parse the new date
        var newDate = ParseDate(text);
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

        // Parse the new time
        var newTime = ParseTime(text);
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

        // Parse the new party size
        var match = Regex.Match(text, @"(\d+)");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var newSize) || newSize <= 0)
        {
            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = ResponseVariations.PartySizeNotUnderstood()
            };
        }

        // Check if >10 people
        if (newSize > 10)
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
            newSize,
            booking.ReservationTime,
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
        var text = message.MessageText.ToLowerInvariant().Trim();

        // Check if canceling rice
        if (Regex.IsMatch(text, @"(cancelar|quitar|sin|no|nada|eliminar)\s*(el\s+)?(arroz)?"))
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
        var servingsMatch = Regex.Match(text, @"(\d+)\s*raciones?");
        if (servingsMatch.Success && !Regex.IsMatch(text, @"(arroz|paella)"))
        {
            var newServings = int.Parse(servingsMatch.Groups[1].Value);

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

            var pendingChanges = new BookingUpdateData { ArrozServings = newServings };
            var newState = state with
            {
                Stage = ModificationStage.AwaitingConfirmation,
                PendingChanges = pendingChanges,
                ChangeDescription = $"cambiar a {newServings} raciones de arroz"
            };
            _stateStore.Set(message.SenderNumber, newState);

            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = $"Vas a {newState.ChangeDescription}. ¿Confirmas? (Sí/No)"
            };
        }

        // Changing rice type - validate it
        var validation = await _riceValidator.ValidateAsync(text, "villacarmen", ct);

        if (!validation.IsValid)
        {
            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = validation.Message ?? "No tenemos ese tipo de arroz. Puedes ver el menú en: https://alqueriavillacarmen.com/menufindesemana.php"
            };
        }

        // Ask for servings if not provided
        if (!servingsMatch.Success)
        {
            // Store the rice type and ask for servings
            var tempState = state with
            {
                PendingChanges = new BookingUpdateData { ArrozType = validation.RiceName }
            };
            _stateStore.Set(message.SenderNumber, tempState);

            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = $"✅ {validation.RiceName} disponible. ¿Cuántas raciones quieres? (mínimo 2, máximo {booking.PartySize})"
            };
        }

        // Have both rice type and servings
        var servings = int.Parse(servingsMatch.Groups[1].Value);
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

    private async Task<AgentResponse> HandleTronasChangeAsync(
        WhatsAppMessage message,
        ModificationState state,
        CancellationToken ct)
    {
        var text = message.MessageText.Trim();
        var match = Regex.Match(text, @"(\d+)");

        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var newCount))
        {
            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = ResponseVariations.TronasNotUnderstood()
            };
        }

        if (newCount > 3)
        {
            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = ResponseVariations.MaxTronas()
            };
        }

        var pendingChanges = new BookingUpdateData { HighChairs = newCount };
        var newState = state with
        {
            Stage = ModificationStage.AwaitingConfirmation,
            PendingChanges = pendingChanges,
            ChangeDescription = $"cambiar a {newCount} tronas"
        };
        _stateStore.Set(message.SenderNumber, newState);

        return new AgentResponse
        {
            Intent = IntentType.Modification,
            AiResponse = $"Vas a {newState.ChangeDescription}. ¿Confirmas? (Sí/No)"
        };
    }

    private async Task<AgentResponse> HandleCarritosChangeAsync(
        WhatsAppMessage message,
        ModificationState state,
        CancellationToken ct)
    {
        var text = message.MessageText.Trim();
        var match = Regex.Match(text, @"(\d+)");

        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var newCount))
        {
            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = ResponseVariations.CarritosNotUnderstood()
            };
        }

        if (newCount > 3)
        {
            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = ResponseVariations.MaxCarritos()
            };
        }

        var pendingChanges = new BookingUpdateData { BabyStrollers = newCount };
        var newState = state with
        {
            Stage = ModificationStage.AwaitingConfirmation,
            PendingChanges = pendingChanges,
            ChangeDescription = $"cambiar a {newCount} carritos"
        };
        _stateStore.Set(message.SenderNumber, newState);

        return new AgentResponse
        {
            Intent = IntentType.Modification,
            AiResponse = $"Vas a {newState.ChangeDescription}. ¿Confirmas? (Sí/No)"
        };
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

    private BookingRecord? TryParseBookingSelection(string text, List<BookingRecord> bookings)
    {
        // Try ordinal mapping ("la primera", "1", etc.)
        foreach (var (key, index) in OrdinalMappings)
        {
            if (text.Contains(key) && index < bookings.Count)
            {
                return bookings[index];
            }
        }

        // Try plain number
        if (int.TryParse(text, out var num) && num >= 1 && num <= bookings.Count)
        {
            return bookings[num - 1];
        }

        // Try by day name ("la del sábado")
        foreach (var (dayName, dayOfWeek) in SpanishDays)
        {
            if (text.Contains(dayName))
            {
                var match = bookings.FirstOrDefault(b => b.ReservationDate.DayOfWeek == dayOfWeek);
                if (match != null) return match;
            }
        }

        // Try by time ("la de las 14:00")
        var timeMatch = Regex.Match(text, @"(\d{1,2}):?(\d{2})?");
        if (timeMatch.Success)
        {
            var hour = int.Parse(timeMatch.Groups[1].Value);
            var minute = timeMatch.Groups[2].Success ? int.Parse(timeMatch.Groups[2].Value) : 0;
            var target = new TimeSpan(hour, minute, 0);
            var match = bookings.FirstOrDefault(b => b.ReservationTime.Hours == hour && b.ReservationTime.Minutes == minute);
            if (match != null) return match;
        }

        // Try by party size ("la de 6 personas")
        var sizeMatch = Regex.Match(text, @"(\d+)\s*personas?");
        if (sizeMatch.Success)
        {
            var size = int.Parse(sizeMatch.Groups[1].Value);
            var match = bookings.FirstOrDefault(b => b.PartySize == size);
            if (match != null) return match;
        }

        // Try by date ("la del 21/12")
        var dateMatch = Regex.Match(text, @"(\d{1,2})[/\-](\d{1,2})");
        if (dateMatch.Success)
        {
            var day = int.Parse(dateMatch.Groups[1].Value);
            var month = int.Parse(dateMatch.Groups[2].Value);
            var match = bookings.FirstOrDefault(b =>
                b.ReservationDate.Day == day && b.ReservationDate.Month == month);
            if (match != null) return match;
        }

        return null;
    }

    private DateTime? ParseDate(string text)
    {
        text = text.ToLowerInvariant();

        // Try day name ("el sábado", "domingo")
        foreach (var (dayName, dayOfWeek) in SpanishDays)
        {
            if (text.Contains(dayName))
            {
                // Find the next occurrence of this day
                var today = DateTime.Today;
                var daysUntil = ((int)dayOfWeek - (int)today.DayOfWeek + 7) % 7;
                if (daysUntil == 0) daysUntil = 7; // Next week if today
                return today.AddDays(daysUntil);
            }
        }

        // Try explicit date (21/12, 21-12, 21/12/2025)
        var match = Regex.Match(text, @"(\d{1,2})[/\-](\d{1,2})(?:[/\-](\d{2,4}))?");
        if (match.Success)
        {
            var day = int.Parse(match.Groups[1].Value);
            var month = int.Parse(match.Groups[2].Value);
            var year = match.Groups[3].Success
                ? int.Parse(match.Groups[3].Value)
                : DateTime.Today.Year;
            if (year < 100) year += 2000;

            try
            {
                return new DateTime(year, month, day);
            }
            catch
            {
                return null;
            }
        }

        // Try "21 de diciembre"
        var months = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["enero"] = 1, ["febrero"] = 2, ["marzo"] = 3, ["abril"] = 4,
            ["mayo"] = 5, ["junio"] = 6, ["julio"] = 7, ["agosto"] = 8,
            ["septiembre"] = 9, ["octubre"] = 10, ["noviembre"] = 11, ["diciembre"] = 12
        };

        foreach (var (monthName, monthNum) in months)
        {
            if (text.Contains(monthName))
            {
                var dayMatch = Regex.Match(text, @"(\d{1,2})");
                if (dayMatch.Success)
                {
                    var day = int.Parse(dayMatch.Groups[1].Value);
                    try
                    {
                        return new DateTime(DateTime.Today.Year, monthNum, day);
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
        }

        return null;
    }

    private TimeSpan? ParseTime(string text)
    {
        // Try HH:mm pattern
        var match = Regex.Match(text, @"(\d{1,2}):(\d{2})");
        if (match.Success)
        {
            var hours = int.Parse(match.Groups[1].Value);
            var minutes = int.Parse(match.Groups[2].Value);
            if (hours >= 0 && hours < 24 && minutes >= 0 && minutes < 60)
            {
                return new TimeSpan(hours, minutes, 0);
            }
        }

        // Try "a las N"
        match = Regex.Match(text, @"a\s+las?\s+(\d{1,2})");
        if (match.Success)
        {
            var hours = int.Parse(match.Groups[1].Value);
            if (hours >= 0 && hours < 24)
            {
                return new TimeSpan(hours, 0, 0);
            }
        }

        return null;
    }

    private static string NormalizePhoneTo9Digits(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length > 9 ? digits[^9..] : digits;
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
        sb.AppendLine(ResponseVariations.ModificationSelectField());
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
}
