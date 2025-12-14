using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;

namespace BotGenerator.Core.Handlers;

/// <summary>
/// Handler for cancelling bookings.
/// Manages the multi-turn cancellation conversation flow.
/// </summary>
public class CancellationHandler
{
    private readonly ILogger<CancellationHandler> _logger;
    private readonly IBookingRepository _bookingRepository;
    private readonly ICancellationStateStore _stateStore;
    private readonly IWhatsAppService _whatsAppService;

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
        IWhatsAppService whatsAppService)
    {
        _logger = logger;
        _bookingRepository = bookingRepository;
        _stateStore = stateStore;
        _whatsAppService = whatsAppService;
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
    /// Step 3: Handle confirmation (yes/no).
    /// </summary>
    private async Task<AgentResponse> HandleConfirmationAsync(
        WhatsAppMessage message,
        CancellationState state,
        CancellationToken ct)
    {
        var text = message.MessageText.ToLowerInvariant().Trim();
        var booking = state.SelectedBooking!;

        // Check for confirmation (allow words anywhere in the message)
        if (Regex.IsMatch(text, @"\b(sí|si|yes|confirmo|vale|ok|cancelar?|cancela)\b"))
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

        // Check for rejection (allow words anywhere in the message)
        if (Regex.IsMatch(text, @"\b(no|mejor no|dejalo|déjalo|mantener|nada)\b"))
        {
            _stateStore.Clear(message.SenderNumber);
            return new AgentResponse
            {
                Intent = IntentType.Normal,
                AiResponse = ResponseVariations.CancellationAborted()
            };
        }

        // Didn't understand
        return new AgentResponse
        {
            Intent = IntentType.Cancellation,
            AiResponse = ResponseVariations.CancellationConfirmationNotUnderstood()
        };
    }

    #endregion

    #region Helper Methods

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
