using System.Text.RegularExpressions;
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;

namespace BotGenerator.Core.Agents;

/// <summary>
/// Agent for checking booking availability.
/// </summary>
public class AvailabilityCheckerAgent : IAgent
{
    private readonly ILogger<AvailabilityCheckerAgent> _logger;

    // Restaurant schedule (in production, load from config/database)
    private static readonly Dictionary<DayOfWeek, (TimeSpan Open, TimeSpan Close)> Schedule = new()
    {
        { DayOfWeek.Thursday, (new TimeSpan(13, 30, 0), new TimeSpan(17, 0, 0)) },
        { DayOfWeek.Friday, (new TimeSpan(13, 30, 0), new TimeSpan(17, 30, 0)) },
        { DayOfWeek.Saturday, (new TimeSpan(13, 30, 0), new TimeSpan(18, 0, 0)) },
        { DayOfWeek.Sunday, (new TimeSpan(13, 30, 0), new TimeSpan(18, 0, 0)) }
    };

    public AvailabilityCheckerAgent(ILogger<AvailabilityCheckerAgent> logger)
    {
        _logger = logger;
    }

    public Task<AgentResponse> ProcessAsync(
        WhatsAppMessage message,
        ConversationState? state,
        List<ChatMessage>? history,
        CancellationToken cancellationToken = default)
    {
        // Extract date and time from state or message
        var bookingData = ExtractBookingData(message, state);

        var result = CheckAvailability(bookingData);

        return Task.FromResult(new AgentResponse
        {
            Intent = IntentType.Normal,
            AiResponse = result.Message,
            Metadata = new Dictionary<string, object>
            {
                ["availabilityResult"] = result
            }
        });
    }

    /// <summary>
    /// Checks availability for a booking.
    /// </summary>
    public AvailabilityResult CheckAvailability(BookingData booking)
    {
        _logger.LogInformation(
            "Checking availability: {Date} {Time} for {People} people",
            booking.Date, booking.Time, booking.People);

        // Parse the date
        if (!TryParseDate(booking.Date, out var date))
        {
            return AvailabilityResult.Invalid("Fecha inválida");
        }

        // Check if restaurant is open on that day
        if (!Schedule.ContainsKey(date.DayOfWeek))
        {
            var closedDays = "lunes, martes y miércoles";
            return AvailabilityResult.Unavailable(
                $"Lo siento, estamos cerrados ese día. Cerramos los {closedDays}.");
        }

        // Check if the date is in the past
        if (date.Date < DateTime.Now.Date)
        {
            return AvailabilityResult.Invalid("No se puede reservar para una fecha pasada");
        }

        // Check if same day (policy: no same-day bookings)
        if (date.Date == DateTime.Now.Date)
        {
            return AvailabilityResult.SameDay(
                "No aceptamos reservas para el mismo día. " +
                "Por favor, llámanos al +34 638 857 294.");
        }

        // Parse the time
        if (!TryParseTime(booking.Time, out var time))
        {
            return AvailabilityResult.Invalid("Hora inválida");
        }

        // Check if time is within opening hours
        var (openTime, closeTime) = Schedule[date.DayOfWeek];
        if (time < openTime || time > closeTime.Add(TimeSpan.FromMinutes(-60)))
        {
            return AvailabilityResult.Unavailable(
                $"Ese día abrimos de {FormatTime(openTime)} a {FormatTime(closeTime)}. " +
                $"¿Te viene bien otra hora?");
        }

        // Check party size
        if (booking.People > 20)
        {
            return AvailabilityResult.Unavailable(
                "Para grupos de más de 20 personas, por favor llámanos directamente.");
        }

        // In production, check actual availability in database
        // For now, assume available

        return AvailabilityResult.Available(
            $"¡Perfecto! El {FormatDate(date)} a las {booking.Time} hay disponibilidad para {booking.People} personas.");
    }

    private BookingData ExtractBookingData(WhatsAppMessage message, ConversationState? state)
    {
        return new BookingData
        {
            Date = state?.Fecha ?? "",
            Time = state?.Hora ?? "",
            People = state?.Personas ?? 0,
            Name = message.PushName,
            Phone = message.SenderNumber
        };
    }

    private bool TryParseDate(string dateStr, out DateTime date)
    {
        date = default;

        if (string.IsNullOrEmpty(dateStr)) return false;

        // Try dd/MM/yyyy
        if (DateTime.TryParseExact(dateStr, "dd/MM/yyyy",
            null, System.Globalization.DateTimeStyles.None, out date))
        {
            return true;
        }

        // Try d/M/yyyy
        if (DateTime.TryParseExact(dateStr, "d/M/yyyy",
            null, System.Globalization.DateTimeStyles.None, out date))
        {
            return true;
        }

        return false;
    }

    private bool TryParseTime(string timeStr, out TimeSpan time)
    {
        time = default;

        if (string.IsNullOrEmpty(timeStr)) return false;

        // Try HH:mm
        if (TimeSpan.TryParseExact(timeStr, @"hh\:mm", null, out time))
        {
            return true;
        }

        // Try H:mm
        if (TimeSpan.TryParseExact(timeStr, @"h\:mm", null, out time))
        {
            return true;
        }

        return false;
    }

    private string FormatTime(TimeSpan time) =>
        $"{time.Hours}:{time.Minutes:D2}";

    private string FormatDate(DateTime date)
    {
        var days = new[] { "domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado" };
        return $"{days[(int)date.DayOfWeek]} {date.Day}/{date.Month}";
    }
}

/// <summary>
/// Result of availability check.
/// </summary>
public record AvailabilityResult
{
    public bool IsAvailable { get; init; }
    public bool IsSameDay { get; init; }
    public bool IsInvalid { get; init; }
    public string Message { get; init; } = "";
    public string? SuggestedTime { get; init; }

    public static AvailabilityResult Available(string message) => new()
    {
        IsAvailable = true,
        Message = message
    };

    public static AvailabilityResult Unavailable(string message) => new()
    {
        IsAvailable = false,
        Message = message
    };

    public static AvailabilityResult SameDay(string message) => new()
    {
        IsAvailable = false,
        IsSameDay = true,
        Message = message
    };

    public static AvailabilityResult Invalid(string message) => new()
    {
        IsAvailable = false,
        IsInvalid = true,
        Message = message
    };
}
