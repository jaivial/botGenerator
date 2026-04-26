using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using Microsoft.Extensions.Logging;
using System.Text;

namespace BotGenerator.Core.Handlers;

/// <summary>
/// Handler for cancelling bookings.
/// Manages the multi-turn cancellation conversation flow.
/// Uses AI agents for all message understanding.
/// </summary>
public class CancellationHandler
{
    private readonly ILogger<CancellationHandler> _logger;
    private readonly IBookingRepository _bookingRepository;
    private readonly ICancellationStateStore _stateStore;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IAiBookingSelectionService _bookingSelection;
    private readonly IAiIntentDetectionService _intentDetection;
    private readonly IExternalReservationService _externalReservationService;

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
        IAiBookingSelectionService bookingSelection,
        IAiIntentDetectionService intentDetection,
        IExternalReservationService externalReservationService)
    {
        _logger = logger;
        _bookingRepository = bookingRepository;
        _stateStore = stateStore;
        _whatsAppService = whatsAppService;
        _bookingSelection = bookingSelection;
        _intentDetection = intentDetection;
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

        var phone9 = NormalizePhoneTo9Digits(message.SenderNumber);

        var allBookings = await _bookingRepository.FindBookingsByPhoneAsync(phone9, ct);

        var today = DateTime.Now.Date;
        var bookings = allBookings.Where(b => b.ReservationDate > today).ToList();
        var sameDayBookings = allBookings.Where(b => b.ReservationDate <= today).ToList();

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
    /// Step 2: Handle booking selection from multiple bookings using AI.
    /// </summary>
    private async Task<AgentResponse> HandleBookingSelectionAsync(
        WhatsAppMessage message,
        CancellationState state,
        CancellationToken ct)
    {
        var bookings = state.FoundBookings ?? new List<BookingRecord>();

        var selected = await _bookingSelection.SelectBookingAsync(
            message.MessageText, bookings, ct);

        if (selected == null)
        {
            return new AgentResponse
            {
                Intent = IntentType.Cancellation,
                AiResponse = ResponseVariations.BookingSelectionNotUnderstood()
            };
        }

        var newState = state with
        {
            Stage = CancellationStage.AwaitingConfirmation,
            SelectedBooking = selected
        };
        _stateStore.Set(message.SenderNumber, newState);

        return BuildConfirmationResponse(selected);
    }

    /// <summary>
    /// Step 3: Handle confirmation (yes/no) using AI.
    /// Includes post-cancellation verification to prevent wrong booking cancellation.
    /// </summary>
    private async Task<AgentResponse> HandleConfirmationAsync(
        WhatsAppMessage message,
        CancellationState state,
        CancellationToken ct)
    {
        var booking = state.SelectedBooking!;

        var userIntent = await _intentDetection.DetectIntentAsync(
            message.MessageText, "cancellation_confirm", ct);

        _logger.LogDebug("AI analyzed cancellation confirmation intent: {Intent}", userIntent);

        if (userIntent == "confirm")
        {
            // VERIFY: Re-fetch booking from DB to ensure it still exists and matches
            var freshBooking = await _bookingRepository.GetBookingByIdAsync(booking.Id, ct);
            if (freshBooking == null)
            {
                _stateStore.Clear(message.SenderNumber);
                return new AgentResponse
                {
                    Intent = IntentType.Normal,
                    AiResponse = "Esta reserva ya no existe o ha sido cancelada anteriormente."
                };
            }

            _logger.LogWarning(
                "CANCELLING booking {BookingId}: {Date} {Time} {People}pax for {Phone}",
                booking.Id, booking.DateFormatted, booking.TimeFormatted,
                booking.PartySize, message.SenderNumber);

            // Archive to cancelled_bookings table
            var archiveSuccess = await _bookingRepository.InsertCancelledBookingAsync(
                booking, "AI_ASSISTANT", ct);

            if (!archiveSuccess)
            {
                _logger.LogWarning(
                    "Failed to archive cancelled booking {BookingId}", booking.Id);
            }

            var cancelSuccess = await _bookingRepository.CancelBookingAsync(booking.Id, ct);

            _stateStore.Clear(message.SenderNumber);

            if (cancelSuccess)
            {
                await SendCancellationNotificationAsync(booking, ct);
                await _externalReservationService.CancelReservationAsync(booking.Id, ct);

                // Include booking details in success message to verify correct booking was cancelled
                var successMsg = $"❌ *Reserva cancelada.*\n\n" +
                                 $"📅 *{booking.DateFormatted}* ({booking.DayName})\n" +
                                 $"🕐 *{booking.TimeFormatted}*\n" +
                                 $"👥 *{booking.PartySize} personas*\n";

                if (!string.IsNullOrEmpty(booking.ArrozType))
                    successMsg += $"🍚 *{booking.ArrozType}* ({booking.ArrozServings} raciones)\n";

                successMsg += "\nTe esperamos en Alquería Villa Carmen. 😊";

                return new AgentResponse
                {
                    Intent = IntentType.Normal,
                    AiResponse = successMsg,
                    Metadata = new Dictionary<string, object>
                    {
                        ["cancelled"] = true,
                        ["bookingId"] = booking.Id
                    }
                };
            }

            return new AgentResponse
            {
                Intent = IntentType.Normal,
                AiResponse = ResponseVariations.CancellationError()
            };
        }

        if (userIntent == "reject")
        {
            _stateStore.Clear(message.SenderNumber);
            return new AgentResponse
            {
                Intent = IntentType.Normal,
                AiResponse = ResponseVariations.CancellationAborted()
            };
        }

        return new AgentResponse
        {
            Intent = IntentType.Cancellation,
            AiResponse = ResponseVariations.CancellationConfirmationNotUnderstood()
        };
    }

    #endregion

    #region Helper Methods

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
                sb.AppendLine($"🍚 *Arroz:* {booking.ArrozType} ({booking.ArrozServings} raciones)");
            else
                sb.AppendLine("🍚 *Arroz:* Sin arroz");

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
            _logger.LogError(ex,
                "Failed to send cancellation notification for booking {BookingId}",
                booking.Id);
        }
    }

    #endregion
}
