using BotGenerator.Core.Models;
using Microsoft.Extensions.Logging;

namespace BotGenerator.Core.Handlers;

/// <summary>
/// Handler for cancelling bookings.
/// </summary>
public class CancellationHandler
{
    private readonly ILogger<CancellationHandler> _logger;

    public CancellationHandler(ILogger<CancellationHandler> logger)
    {
        _logger = logger;
    }

    public async Task<AgentResponse> ProcessCancellationAsync(
        BookingData cancellationData,
        string senderNumber,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing cancellation for {Name}: {Date}",
            cancellationData.Name, cancellationData.Date);

        try
        {
            // 1. Find the booking
            var booking = await FindBookingAsync(cancellationData, cancellationToken);

            if (booking == null)
            {
                return new AgentResponse
                {
                    Intent = IntentType.Normal,
                    AiResponse = "No se encontró ninguna reserva con esos datos. " +
                                "¿Puedes verificar la información?"
                };
            }

            // 2. Cancel the booking
            var success = await CancelBookingAsync(booking.Id, cancellationToken);

            if (success)
            {
                return new AgentResponse
                {
                    Intent = IntentType.Cancellation,
                    AiResponse = "✅ Reserva cancelada con éxito. " +
                                "Esperamos verte pronto en Alquería Villa Carmen.",
                    Metadata = new Dictionary<string, object>
                    {
                        ["cancelled"] = true,
                        ["bookingId"] = booking.Id
                    }
                };
            }

            return AgentResponse.Error("No se pudo cancelar la reserva");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing cancellation");
            return AgentResponse.Error("Error al procesar la cancelación");
        }
    }

    private async Task<BookingRecord?> FindBookingAsync(
        BookingData criteria,
        CancellationToken cancellationToken)
    {
        // TODO: Implement actual database lookup
        await Task.Delay(50, cancellationToken);

        // Simulate finding a booking
        return new BookingRecord
        {
            Id = "booking-123",
            CustomerName = criteria.Name,
            Date = criteria.Date,
            Time = criteria.Time
        };
    }

    private async Task<bool> CancelBookingAsync(
        string bookingId,
        CancellationToken cancellationToken)
    {
        // TODO: Implement actual cancellation
        await Task.Delay(50, cancellationToken);
        return true;
    }

    private record BookingRecord
    {
        public string Id { get; init; } = "";
        public string CustomerName { get; init; } = "";
        public string Date { get; init; } = "";
        public string Time { get; init; } = "";
    }
}
