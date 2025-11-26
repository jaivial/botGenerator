namespace BotGenerator.Core.Models;

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
    /// Validates that all required fields are present.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(Phone) &&
        !string.IsNullOrWhiteSpace(Date) &&
        !string.IsNullOrWhiteSpace(Time) &&
        People > 0;

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
    /// Converts date from dd/MM/yyyy to yyyy-MM-dd for database storage.
    /// </summary>
    public string? DateForDatabase
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Date)) return null;

            var parts = Date.Split('/');
            if (parts.Length != 3) return null;

            return $"{parts[2]}-{parts[1].PadLeft(2, '0')}-{parts[0].PadLeft(2, '0')}";
        }
    }
}
