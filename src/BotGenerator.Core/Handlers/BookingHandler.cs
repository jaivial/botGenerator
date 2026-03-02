using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace BotGenerator.Core.Handlers;

/// <summary>
/// Handler for creating bookings.
/// </summary>
public class BookingHandler
{
    private readonly ILogger<BookingHandler> _logger;
    private readonly IConfiguration _configuration;
    private readonly IBookingRepository _bookingRepository;
    private readonly IExternalReservationService _externalReservationService;

    public BookingHandler(
        ILogger<BookingHandler> logger,
        IConfiguration configuration,
        IBookingRepository bookingRepository,
        IExternalReservationService externalReservationService)
    {
        _logger = logger;
        _configuration = configuration;
        _bookingRepository = bookingRepository;
        _externalReservationService = externalReservationService;
    }

    public async Task<AgentResponse> CreateBookingAsync(
        BookingData booking,
        WhatsAppMessage message,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating booking for {Name}: {Date} {Time}, {People} people",
            booking.Name, booking.Date, booking.Time, booking.People);

        try
        {
            var (success, bookingId) = await CreateBookingInDatabaseAsync(booking, cancellationToken);

            if (success && bookingId.HasValue)
            {
                // Sync to external PHP system
                var (externalSuccess, externalMessage) = await _externalReservationService.CreateReservationAsync(
                    booking.Name,
                    booking.Phone,
                    booking.Date,
                    booking.People,
                    booking.Time,
                    booking.ArrozType,
                    booking.ArrozServings,
                    booking.HighChairs,
                    booking.BabyStrollers,
                    cancellationToken);

                if (!externalSuccess)
                {
                    _logger.LogWarning(
                        "Failed to sync booking {BookingId} to external system: {Message}",
                        bookingId.Value, externalMessage);
                }
                else
                {
                    _logger.LogInformation(
                        "Successfully synced booking {BookingId} to external system",
                        bookingId.Value);
                }

                var confirmationMessage = BuildConfirmationMessage(booking);

                return new AgentResponse
                {
                    Intent = IntentType.Booking,
                    AiResponse = confirmationMessage,
                    ExtractedData = booking,
                    Metadata = new Dictionary<string, object>
                    {
                        ["bookingCreated"] = true,
                        ["bookingId"] = bookingId.Value.ToString()
                    }
                };
            }

            return AgentResponse.Error(ResponseVariations.BookingCreationFailed());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating booking");
            return AgentResponse.Error(ResponseVariations.BookingProcessingError());
        }
    }

    private async Task<(bool success, long? bookingId)> CreateBookingInDatabaseAsync(
        BookingData booking,
        CancellationToken cancellationToken)
    {
        var bookingId = await _bookingRepository.CreateBookingAsync(booking, cancellationToken);
        return (bookingId.HasValue, bookingId);
    }

    private string BuildConfirmationMessage(BookingData booking)
    {
        var sb = new StringBuilder();
        sb.AppendLine("✅ *¡Reserva confirmada!*");
        sb.AppendLine();
        sb.AppendLine($"📅 *Fecha:* {booking.Date}");
        sb.AppendLine($"🕐 *Hora:* {booking.Time}");
        sb.AppendLine($"👥 *Personas:* {booking.People}");
        sb.AppendLine($"👤 *Nombre:* {booking.Name}");

        if (!string.IsNullOrEmpty(booking.ArrozType))
        {
            sb.AppendLine($"🍚 *Arroz:* {booking.ArrozType}");
            if (booking.ArrozServings.HasValue)
            {
                sb.AppendLine($"   *Raciones:* {booking.ArrozServings}");
            }
        }

        if (booking.HighChairs > 0)
        {
            sb.AppendLine($"🪑 *Tronas:* {booking.HighChairs}");
        }

        if (booking.BabyStrollers > 0)
        {
            sb.AppendLine($"🛒 *Carritos:* {booking.BabyStrollers}");
        }

        sb.AppendLine();
        sb.AppendLine("¡Te esperamos en Alquería Villa Carmen!");

        return sb.ToString();
    }
}
