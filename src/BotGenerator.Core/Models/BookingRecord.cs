namespace BotGenerator.Core.Models;

/// <summary>
/// Represents an existing booking retrieved from the database.
/// Used for modification and cancellation flows.
/// </summary>
public record BookingRecord
{
    /// <summary>
    /// Database primary key.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Customer name as stored in the booking.
    /// </summary>
    public string CustomerName { get; init; } = "";

    /// <summary>
    /// Date of the reservation.
    /// </summary>
    public DateTime ReservationDate { get; init; }

    /// <summary>
    /// Time of the reservation.
    /// </summary>
    public TimeSpan ReservationTime { get; init; }

    /// <summary>
    /// Number of people in the party.
    /// </summary>
    public int PartySize { get; init; }

    /// <summary>
    /// Type of rice ordered, null if no rice.
    /// </summary>
    public string? ArrozType { get; init; }

    /// <summary>
    /// Number of rice servings, null if no rice.
    /// </summary>
    public int? ArrozServings { get; init; }

    /// <summary>
    /// Number of high chairs requested.
    /// </summary>
    public int HighChairs { get; init; }

    /// <summary>
    /// Number of baby strollers.
    /// </summary>
    public int BabyStrollers { get; init; }

    /// <summary>
    /// Contact phone (9 digits without country code).
    /// </summary>
    public string ContactPhone { get; init; } = "";

    /// <summary>
    /// Gets the reservation date formatted for display (dd/MM/yyyy).
    /// </summary>
    public string DateFormatted => ReservationDate.ToString("dd/MM/yyyy");

    /// <summary>
    /// Gets the reservation time formatted for display (HH:mm).
    /// </summary>
    public string TimeFormatted => $"{ReservationTime.Hours:D2}:{ReservationTime.Minutes:D2}";

    /// <summary>
    /// Gets the Spanish day name for the reservation date.
    /// </summary>
    public string DayName => ReservationDate.DayOfWeek switch
    {
        DayOfWeek.Monday => "lunes",
        DayOfWeek.Tuesday => "martes",
        DayOfWeek.Wednesday => "miércoles",
        DayOfWeek.Thursday => "jueves",
        DayOfWeek.Friday => "viernes",
        DayOfWeek.Saturday => "sábado",
        DayOfWeek.Sunday => "domingo",
        _ => ""
    };

    /// <summary>
    /// Gets a short summary of the booking for display.
    /// </summary>
    public string Summary => $"{DayName} {DateFormatted} a las {TimeFormatted} para {PartySize} personas";
}
