namespace BotGenerator.Core.Models;

using System.Globalization;

/// <summary>
/// Represents the data for a reservation.
/// Extracted from AI response when BOOKING_REQUEST command is detected.
/// </summary>
public record BookingData
{
    /// <summary>
    /// Customer name for the reservation.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Customer phone number.
    /// </summary>
    public string Phone { get; init; } = "";

    /// <summary>
    /// Reservation date in dd/MM/yyyy format.
    /// </summary>
    public string Date { get; init; } = "";

    /// <summary>
    /// Reservation time in HH:mm format.
    /// </summary>
    public string Time { get; init; } = "";

    /// <summary>
    /// Number of people.
    /// </summary>
    public int People { get; init; }

    /// <summary>
    /// Type of rice ordered (null if no rice).
    /// </summary>
    public string? ArrozType { get; init; }

    /// <summary>
    /// Number of rice servings.
    /// </summary>
    public int? ArrozServings { get; init; }

    /// <summary>
    /// Number of high chairs needed.
    /// </summary>
    public int HighChairs { get; init; }

    /// <summary>
    /// Number of baby strollers.
    /// </summary>
    public int BabyStrollers { get; init; }

    /// <summary>
    /// Additional notes or comments.
    /// </summary>
    public string? Commentary { get; init; }

    /// <summary>
    /// True if the booking summary has been shown to the user.
    /// We require a summary review before creating the booking.
    /// </summary>
    public bool SummaryShown { get; init; }

    /// <summary>
    /// Validates that all required fields are present.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(Phone) &&
        !string.IsNullOrWhiteSpace(Date) &&
        !string.IsNullOrWhiteSpace(Time) &&
        People > 0 &&
        HighChairs >= 0 && HighChairs <= People &&
        BabyStrollers >= 0 && BabyStrollers <= People &&
        ((string.IsNullOrWhiteSpace(ArrozType) && !ArrozServings.HasValue) ||
         (!string.IsNullOrWhiteSpace(ArrozType) && ArrozServings is >= 2 && ArrozServings <= People));

    /// <summary>
    /// Returns a list of missing required fields.
    /// </summary>
    public List<string> GetMissingFields()
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(Name)) missing.Add("nombre");
        if (string.IsNullOrWhiteSpace(Phone)) missing.Add("teléfono");
        if (string.IsNullOrWhiteSpace(Date)) missing.Add("fecha");
        if (string.IsNullOrWhiteSpace(Time)) missing.Add("hora");
        if (People <= 0) missing.Add("personas");

        return missing;
    }

    /// <summary>
    /// Converts date to yyyy-MM-dd for database storage.
    /// Handles both dd/MM/yyyy and yyyy-MM-dd formats.
    /// </summary>
    public string? DateForDatabase
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Date)) return null;

            return DateTime.TryParseExact(
                Date.Trim(),
                new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" },
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed)
                ? parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : null;
        }
    }
}
