using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BotGenerator.Core.Handlers;

/// <summary>
/// Handler for cancelling bookings.
/// Manages the multi-turn cancellation conversation flow.
/// Uses AI agents for human-like conversation understanding.
/// </summary>
public class CancellationHandler
{
    private readonly ILogger<CancellationHandler> _logger;
    private readonly IBookingRepository _bookingRepository;
    private readonly ICancellationStateStore _stateStore;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IGeminiService _gemini;
    private readonly IExternalReservationService _externalReservationService;

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
        ["primera"] = 0, ["uno"] = 0,
        ["segunda"] = 1, ["dos"] = 1,
        ["tercera"] = 2, ["tres"] = 2,
        ["cuarta"] = 3, ["cuatro"] = 3,
        ["quinta"] = 4, ["cinco"] = 4
    };

    /// <summary>
    /// Management team phone numbers for cancellation alerts.
    /// </summary>
    private static readonly string[] ManagementPhones = new[]
    {
        "34692747052",
        "34638857294",
        "34686969914"
    };

    public CancellationHandler(
        ILogger<CancellationHandler> logger,
        IBookingRepository bookingRepository,
        ICancellationStateStore stateStore,
        IWhatsAppService whatsAppService,
        IGeminiService gemini,
        IExternalReservationService externalReservationService)
    {
        _logger = logger;
        _gemini = gemini;
        _bookingRepository = bookingRepository;
        _stateStore = stateStore;
        _whatsAppService = whatsAppService;
        _externalReservationService = externalReservationService;
    }

    /// <summary>
    /// Main entry point for processing cancellation requests.
    /// </summary>
    public async Task<AgentResponse> ProcessCancellationAsync(
        WhatsAppMessage message,
        CancellationState? currentState,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Processing cancellation for {Phone}, Stage={Stage}",
            message.SenderNumber,
            currentState?.Stage.ToString() ?? "New");

        // Route based on current stage
        return currentState?.Stage switch
        {
            null => await StartCancellationFlowAsync(message, ct),
            CancellationStage.SelectingBooking => await HandleBookingSelectionAsync(message, currentState, ct),
            CancellationStage.AwaitingConfirmation => await HandleConfirmationAsync(message, currentState, ct),
            _ => await StartCancellationFlowAsync(message, ct)
        };
    }

    #region Flow Steps

    /// <summary>
    /// Step 1: Start cancellation flow - find bookings for this phone.
    /// </summary>
    private async Task<AgentResponse> StartCancellationFlowAsync(
        WhatsAppMessage message,
        CancellationToken ct)
    {
        _logger.LogInformation("Starting cancellation flow for {Phone}", message.SenderNumber);

        // Extract 9-digit phone
        var phone9 = NormalizePhoneTo9Digits(message.SenderNumber);

        // Find bookings in database
        var allBookings = await _bookingRepository.FindBookingsByPhoneAsync(phone9, ct);

        // Filter out same-day bookings - those must be cancelled by phone
        var today = DateTime.Now.Date;
        var bookings = allBookings.Where(b => b.ReservationDate > today).ToList();
        var sameDayBookings = allBookings.Where(b => b.ReservationDate <= today).ToList();

        // If all bookings are same-day, send contact card
        if (bookings.Count == 0 && sameDayBookings.Count > 0)
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

        if (bookings.Count == 0)
        {
            _stateStore.Clear(message.SenderNumber);
            return new AgentResponse
            {
                Intent = IntentType.Normal,
                AiResponse = ResponseVariations.CancellationNoBookingsFound()
            };
        }

        if (bookings.Count == 1)
        {
            // Auto-select the only booking, go to AwaitingConfirmation
            var state = new CancellationState
            {
                PhoneNumber = message.SenderNumber,
                Stage = CancellationStage.AwaitingConfirmation,
                FoundBookings = bookings,
                SelectedBooking = bookings[0]
            };
            _stateStore.Set(message.SenderNumber, state);

            return BuildConfirmationResponse(bookings[0]);
        }

        // Multiple bookings - ask which one
        var multiState = new CancellationState
        {
            PhoneNumber = message.SenderNumber,
            Stage = CancellationStage.SelectingBooking,
            FoundBookings = bookings
        };
        _stateStore.Set(message.SenderNumber, multiState);

        return BuildSelectBookingResponse(bookings);
    }

    /// <summary>
    /// Step 2: Handle booking selection from multiple bookings.
    /// Supports lazy answers like "la primera", "la del sábado", etc.
    /// </summary>
    private Task<AgentResponse> HandleBookingSelectionAsync(
        WhatsAppMessage message,
        CancellationState state,
        CancellationToken ct)
    {
        var bookings = state.FoundBookings ?? new List<BookingRecord>();
        var text = message.MessageText.ToLowerInvariant().Trim();

        // Try to parse the selection
        var selected = TryParseBookingSelection(text, bookings);

        if (selected == null)
        {
            // Couldn't understand, ask again
            return Task.FromResult(new AgentResponse
            {
                Intent = IntentType.Cancellation,
                AiResponse = ResponseVariations.BookingSelectionNotUnderstood()
            });
        }

        // Update state with selected booking
        var newState = state with
        {
            Stage = CancellationStage.AwaitingConfirmation,
            SelectedBooking = selected
        };
        _stateStore.Set(message.SenderNumber, newState);

        return Task.FromResult(BuildConfirmationResponse(selected));
    }

    /// <summary>
    /// Step 3: Handle confirmation (yes/no) using AI for natural language understanding.
    /// </summary>
    private async Task<AgentResponse> HandleConfirmationAsync(
        WhatsAppMessage message,
        CancellationState state,
        CancellationToken ct)
    {
        var text = message.MessageText.Trim();
        var booking = state.SelectedBooking!;

        // Use AI to understand the user's intent
        var userIntent = await AnalyzeConfirmationIntentAsync(text, booking, ct);

        _logger.LogDebug("AI analyzed confirmation intent: {Intent}", userIntent);

        if (userIntent == "CONFIRM")
        {
            // Archive to cancelled_bookings table
            var archiveSuccess = await _bookingRepository.InsertCancelledBookingAsync(
                booking,
                "AI_ASSISTANT",
                ct);

            if (!archiveSuccess)
            {
                _logger.LogWarning(
                    "Failed to archive cancelled booking {BookingId} to cancelled_bookings table",
                    booking.Id);
            }

            // Mark booking as cancelled
            var cancelSuccess = await _bookingRepository.CancelBookingAsync(booking.Id, ct);

            _stateStore.Clear(message.SenderNumber);

            if (cancelSuccess)
            {
                // Send notification to restaurant
                await SendCancellationNotificationAsync(booking, ct);

                // Sync cancellation to external PHP system
                await _externalReservationService.CancelReservationAsync(booking.Id, ct);

                return new AgentResponse
                {
                    Intent = IntentType.Normal,
                    AiResponse = ResponseVariations.CancellationSuccess(),
                    Metadata = new Dictionary<string, object>
                    {
                        ["cancelled"] = true,
                        ["bookingId"] = booking.Id
                    }
                };
            }
            else
            {
                return new AgentResponse
                {
                    Intent = IntentType.Normal,
                    AiResponse = ResponseVariations.CancellationError()
                };
            }
        }

        if (userIntent == "REJECT")
        {
            _stateStore.Clear(message.SenderNumber);
            return new AgentResponse
            {
                Intent = IntentType.Normal,
                AiResponse = ResponseVariations.CancellationAborted()
            };
        }

        // AI couldn't determine intent clearly, ask again
        return new AgentResponse
        {
            Intent = IntentType.Cancellation,
            AiResponse = ResponseVariations.CancellationConfirmationNotUnderstood()
        };
    }

    /// <summary>
    /// AI agent that analyzes user's confirmation response.
    /// Uses regex for simple cases, AI for complex natural language variations.
    /// </summary>
    private async Task<string> AnalyzeConfirmationIntentAsync(
        string userMessage,
        BookingRecord booking,
        CancellationToken ct)
    {
        var lowerText = userMessage.Trim().ToLowerInvariant();

        // FAST PATH: Check simple/obvious cases with regex first (more reliable, saves API calls)
        // Clear confirmations
        if (Regex.IsMatch(lowerText, @"^(sí|si|s[ií][ ,]|yes|ok|vale|claro|confirmo|cancelar?|cancela|adelante|por supuesto|afirmativo|correcto|exacto|eso)$", RegexOptions.IgnoreCase))
        {
            _logger.LogDebug("Regex matched CONFIRM for: {Message}", userMessage);
            return "CONFIRM";
        }

        // Clear rejections
        if (Regex.IsMatch(lowerText, @"^(no|nop|nope|nel|mejor no|dejalo|déjalo|mantener|nada|no quiero|cancelado no)$", RegexOptions.IgnoreCase))
        {
            _logger.LogDebug("Regex matched REJECT for: {Message}", userMessage);
            return "REJECT";
        }

        // Partial match for confirmations (not full match, but contains clear intent)
        if (Regex.IsMatch(lowerText, @"\b(sí|si)\s*(,|por favor|cancel|quiero)?", RegexOptions.IgnoreCase) &&
            !Regex.IsMatch(lowerText, @"\bno\b", RegexOptions.IgnoreCase))
        {
            _logger.LogDebug("Regex partial matched CONFIRM for: {Message}", userMessage);
            return "CONFIRM";
        }

        // Partial match for rejections
        if (Regex.IsMatch(lowerText, @"\b(no|mejor no|déjalo|dejalo|mantener|no cancel)", RegexOptions.IgnoreCase))
        {
            _logger.LogDebug("Regex partial matched REJECT for: {Message}", userMessage);
            return "REJECT";
        }

        // SLOW PATH: Use AI for complex/ambiguous responses
        try
        {
            _logger.LogDebug("Using AI to analyze confirmation intent for: {Message}", userMessage);

            var systemPrompt = @"Eres un analizador de intenciones. Tu tarea es determinar si el usuario CONFIRMA o RECHAZA la cancelación de una reserva.

Responde SOLO con una palabra: CONFIRM, REJECT o UNCLEAR.

- CONFIRM: sí, confirmo, acepta, quiere cancelar, adelante, ok, vale, claro, por supuesto, dale, hazlo, procede
- REJECT: no, mejor no, déjalo, no quiero, mantener la reserva, me arrepentí, no canceles
- UNCLEAR: preguntas, información adicional, respuestas ambiguas";

            var userPrompt = $@"Mensaje del usuario: ""{userMessage}""

Respuesta (solo CONFIRM, REJECT o UNCLEAR):";

            var config = new GeminiGenerationConfig
            {
                Temperature = 0.0,
                MaxOutputTokens = 10
            };

            var response = await _gemini.GenerateAsync(systemPrompt, userPrompt, null, config, ct);
            var intent = response.Trim().ToUpperInvariant();

            _logger.LogDebug("AI returned: {Response} for message: {Message}", intent, userMessage);

            // Validate response
            if (intent.Contains("CONFIRM"))
                return "CONFIRM";
            if (intent.Contains("REJECT"))
                return "REJECT";

            return "UNCLEAR";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AI confirmation analysis for: {Message}", userMessage);
            return "UNCLEAR";
        }
    }

    #endregion

    #region Helper Methods

    private BookingRecord? TryParseBookingSelection(string text, List<BookingRecord> bookings)
    {
        var normalized = text.Trim().ToLowerInvariant();

        // Try plain numeric input: "1", "2".
        if (int.TryParse(normalized, out var num) && num >= 1 && num <= bookings.Count)
        {
            return bookings[num - 1];
        }

        // Try article + number: "la 1", "el 2".
        var articleNumberMatch = Regex.Match(normalized, @"^(?:la|el)?\s*(\d+)$", RegexOptions.IgnoreCase);
        if (articleNumberMatch.Success &&
            int.TryParse(articleNumberMatch.Groups[1].Value, out var indexedNum) &&
            indexedNum >= 1 && indexedNum <= bookings.Count)
        {
            return bookings[indexedNum - 1];
        }

        // Try ordinal mapping ("la primera", "1", etc.)
        foreach (var (key, index) in OrdinalMappings)
        {
            if (Regex.IsMatch(normalized, $@"\b{Regex.Escape(key)}\b", RegexOptions.IgnoreCase) && index < bookings.Count)
            {
                return bookings[index];
            }
        }

        // Try by day name ("la del sábado")
        foreach (var (dayName, dayOfWeek) in SpanishDays)
        {
            if (normalized.Contains(dayName))
            {
                var match = bookings.FirstOrDefault(b => b.ReservationDate.DayOfWeek == dayOfWeek);
                if (match != null) return match;
            }
        }

        // Try by time with explicit context ("la de las 14:00" or "14:00").
        var timeMatch = Regex.Match(normalized, @"(?:a\s+las?\s+)?(\d{1,2}):(\d{2})\b");
        if (timeMatch.Success)
        {
            var hour = int.Parse(timeMatch.Groups[1].Value);
            var minute = int.Parse(timeMatch.Groups[2].Value);
            var match = bookings.FirstOrDefault(b => b.ReservationTime.Hours == hour && b.ReservationTime.Minutes == minute);
            if (match != null) return match;
        }

        var hourOnlyMatch = Regex.Match(normalized, @"a\s+las?\s+(\d{1,2})\b");
        if (hourOnlyMatch.Success)
        {
            var hour = int.Parse(hourOnlyMatch.Groups[1].Value);
            var match = bookings.FirstOrDefault(b => b.ReservationTime.Hours == hour && b.ReservationTime.Minutes == 0);
            if (match != null) return match;
        }

        // Try by party size ("la de 6 personas")
        var sizeMatch = Regex.Match(normalized, @"(\d+)\s*personas?");
        if (sizeMatch.Success)
        {
            var size = int.Parse(sizeMatch.Groups[1].Value);
            var match = bookings.FirstOrDefault(b => b.PartySize == size);
            if (match != null) return match;
        }

        // Try by date ("la del 21/12")
        var dateMatch = Regex.Match(normalized, @"(\d{1,2})[/\-](\d{1,2})");
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

    private static string NormalizePhoneTo9Digits(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length > 9 ? digits[^9..] : digits;
    }

    private AgentResponse BuildSelectBookingResponse(List<BookingRecord> bookings)
    {
        var sb = new StringBuilder();
        sb.AppendLine(ResponseVariations.CancellationSelectBooking());
        sb.AppendLine();

        for (int i = 0; i < bookings.Count; i++)
        {
            var b = bookings[i];
            sb.AppendLine($"*{i + 1}.* {b.Summary}");
        }

        sb.AppendLine();
        sb.AppendLine("¿Cuál quieres cancelar?");

        return new AgentResponse
        {
            Intent = IntentType.Cancellation,
            AiResponse = sb.ToString()
        };
    }

    private AgentResponse BuildConfirmationResponse(BookingRecord booking)
    {
        var riceInfo = string.IsNullOrEmpty(booking.ArrozType)
            ? ""
            : $"\n🍚 Arroz: {booking.ArrozType} ({booking.ArrozServings} raciones)";

        var tronasInfo = booking.HighChairs > 0 ? $"\n🪑 Tronas: {booking.HighChairs}" : "";
        var carritosInfo = booking.BabyStrollers > 0 ? $"\n🛒 Carritos: {booking.BabyStrollers}" : "";

        var sb = new StringBuilder();
        sb.AppendLine("Vas a cancelar esta reserva:");
        sb.AppendLine();
        sb.AppendLine($"📅 *{booking.DateFormatted}* ({booking.DayName})");
        sb.AppendLine($"🕐 *{booking.TimeFormatted}*");
        sb.AppendLine($"👥 *{booking.PartySize} personas*");
        if (!string.IsNullOrEmpty(riceInfo)) sb.Append(riceInfo);
        if (!string.IsNullOrEmpty(tronasInfo)) sb.Append(tronasInfo);
        if (!string.IsNullOrEmpty(carritosInfo)) sb.Append(carritosInfo);
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine(ResponseVariations.CancellationConfirmPrompt());

        return new AgentResponse
        {
            Intent = IntentType.Cancellation,
            AiResponse = sb.ToString()
        };
    }

    #endregion

    #region Notifications

    /// <summary>
    /// Sends a notification to the restaurant when a booking is cancelled.
    /// </summary>
    private async Task SendCancellationNotificationAsync(
        BookingRecord booking,
        CancellationToken ct)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("❌ *Reserva cancelada por Asistente de IA de Villa Carmen*");
            sb.AppendLine();
            sb.AppendLine($"👤 *Cliente:* {booking.CustomerName}");
            sb.AppendLine($"📱 *Teléfono:* {booking.ContactPhone}");
            sb.AppendLine($"📅 *Fecha:* {booking.DateFormatted} ({booking.DayName})");
            sb.AppendLine($"🕐 *Hora:* {booking.TimeFormatted}");
            sb.AppendLine($"👥 *Personas:* {booking.PartySize}");

            if (!string.IsNullOrEmpty(booking.ArrozType))
            {
                sb.AppendLine($"🍚 *Arroz:* {booking.ArrozType} ({booking.ArrozServings} raciones)");
            }
            else
            {
                sb.AppendLine("🍚 *Arroz:* Sin arroz");
            }

            sb.AppendLine($"🪑 *Tronas:* {booking.HighChairs}");
            sb.AppendLine($"🚼 *Carritos:* {booking.BabyStrollers}");

            sb.AppendLine();
            sb.AppendLine($"⏰ *Cancelada:* {DateTime.Now:dd/MM/yyyy HH:mm}");
            sb.AppendLine($"🆔 *ID Reserva:* {booking.Id}");

            var message = sb.ToString();
            foreach (var phone in ManagementPhones)
            {
                try
                {
                    await _whatsAppService.SendTextAsync(phone, message, ct);
                    _logger.LogDebug("Sent cancellation notification to {Phone}", phone);
                }
                catch (Exception phoneEx)
                {
                    _logger.LogError(phoneEx, "Failed to send cancellation notification to {Phone}", phone);
                }
            }

            _logger.LogInformation(
                "Sent cancellation notification for booking {BookingId} to management team",
                booking.Id);
        }
        catch (Exception ex)
        {
            // Log but don't fail the cancellation if notification fails
            _logger.LogError(ex,
                "Failed to send cancellation notification for booking {BookingId}",
                booking.Id);
        }
    }

    #endregion
}
