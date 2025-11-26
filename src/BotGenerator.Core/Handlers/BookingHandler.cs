using BotGenerator.Core.Models;
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

    public BookingHandler(
        ILogger<BookingHandler> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
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
            // In production, call your booking API here
            var success = await CreateBookingInDatabaseAsync(booking, cancellationToken);

            if (success)
            {
                var confirmationMessage = BuildConfirmationMessage(booking);

                return new AgentResponse
                {
                    Intent = IntentType.Booking,
                    AiResponse = confirmationMessage,
                    ExtractedData = booking,
                    Metadata = new Dictionary<string, object>
                    {
                        ["bookingCreated"] = true,
                        ["bookingId"] = Guid.NewGuid().ToString() // Replace with actual ID
                    }
                };
            }

            return AgentResponse.Error("No se pudo crear la reserva");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating booking");
            return AgentResponse.Error("Error al procesar la reserva");
        }
    }

    private async Task<bool> CreateBookingInDatabaseAsync(
        BookingData booking,
        CancellationToken cancellationToken)
    {
        // TODO: Implement actual database/API call
        // Example:
        // var apiUrl = _configuration["BookingApi:Url"];
        // var response = await _httpClient.PostAsJsonAsync(apiUrl, booking);
        // return response.IsSuccessStatusCode;

        await Task.Delay(100, cancellationToken); // Simulate API call
        return true;
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
