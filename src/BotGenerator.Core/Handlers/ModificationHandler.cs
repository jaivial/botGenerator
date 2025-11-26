using BotGenerator.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace BotGenerator.Core.Handlers;

/// <summary>
/// Handler for modifying bookings.
/// </summary>
public class ModificationHandler
{
    private readonly ILogger<ModificationHandler> _logger;

    public ModificationHandler(ILogger<ModificationHandler> logger)
    {
        _logger = logger;
    }

    public async Task<AgentResponse> StartModificationFlowAsync(
        string senderNumber,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting modification flow for {Phone}",
            senderNumber);

        // Find existing bookings for this phone number
        var bookings = await FindBookingsForPhoneAsync(senderNumber, cancellationToken);

        if (bookings.Count == 0)
        {
            return new AgentResponse
            {
                Intent = IntentType.Normal,
                AiResponse = "No encontré reservas futuras asociadas a tu número. " +
                            "¿Quieres hacer una nueva reserva?"
            };
        }

        if (bookings.Count == 1)
        {
            // Only one booking, ask what they want to modify
            var booking = bookings[0];
            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = $"Encontré tu reserva para el *{booking.Date}* a las *{booking.Time}* " +
                            $"para *{booking.People} personas*.\n\n" +
                            "¿Qué te gustaría modificar?\n" +
                            "1️⃣ Fecha\n" +
                            "2️⃣ Hora\n" +
                            "3️⃣ Número de personas\n" +
                            "4️⃣ Tipo de arroz",
                Metadata = new Dictionary<string, object>
                {
                    ["modificationState"] = "selecting_field",
                    ["selectedBooking"] = booking
                }
            };
        }

        // Multiple bookings, ask which one
        var sb = new StringBuilder();
        sb.AppendLine("Encontré varias reservas a tu nombre:\n");

        for (int i = 0; i < bookings.Count; i++)
        {
            var b = bookings[i];
            sb.AppendLine($"*{i + 1}.* {b.Date} a las {b.Time} ({b.People} personas)");
        }

        sb.AppendLine("\n¿Cuál quieres modificar? (responde con el número)");

        return new AgentResponse
        {
            Intent = IntentType.Modification,
            AiResponse = sb.ToString(),
            Metadata = new Dictionary<string, object>
            {
                ["modificationState"] = "selecting_booking",
                ["bookings"] = bookings
            }
        };
    }

    private async Task<List<BookingInfo>> FindBookingsForPhoneAsync(
        string phoneNumber,
        CancellationToken cancellationToken)
    {
        // TODO: Implement actual database lookup
        await Task.Delay(50, cancellationToken);

        // Simulate finding bookings
        return new List<BookingInfo>
        {
            new()
            {
                Id = "booking-1",
                Date = "30/11/2025",
                Time = "14:00",
                People = 4
            }
        };
    }

    public record BookingInfo
    {
        public string Id { get; init; } = "";
        public string Date { get; init; } = "";
        public string Time { get; init; } = "";
        public int People { get; init; }
        public string? ArrozType { get; init; }
    }
}
