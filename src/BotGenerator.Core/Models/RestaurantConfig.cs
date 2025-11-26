namespace BotGenerator.Core.Models;

/// <summary>
/// Configuration for a specific restaurant.
/// Loaded from configuration or database.
/// </summary>
public record RestaurantConfig
{
    /// <summary>
    /// Unique identifier for the restaurant.
    /// Used to load the correct prompt files.
    /// </summary>
    public string Id { get; init; } = "";

    /// <summary>
    /// Display name of the restaurant.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Phone number associated with this restaurant's WhatsApp.
    /// </summary>
    public string WhatsAppNumber { get; init; } = "";

    /// <summary>
    /// Contact phone number for customers.
    /// </summary>
    public string ContactPhone { get; init; } = "";

    /// <summary>
    /// Restaurant's website URL.
    /// </summary>
    public string? WebsiteUrl { get; init; }

    /// <summary>
    /// URL to the menu page.
    /// </summary>
    public string? MenuUrl { get; init; }

    /// <summary>
    /// Restaurant location/address.
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// Opening schedule by day of week.
    /// </summary>
    public Dictionary<DayOfWeek, ScheduleEntry> Schedule { get; init; } = new();

    /// <summary>
    /// Days the restaurant is closed.
    /// </summary>
    public List<DayOfWeek> ClosedDays { get; init; } = new();

    /// <summary>
    /// Whether same-day bookings are allowed.
    /// </summary>
    public bool AllowSameDayBookings { get; init; }

    /// <summary>
    /// Minimum hours advance for bookings.
    /// </summary>
    public int MinAdvanceHours { get; init; } = 2;

    /// <summary>
    /// Maximum party size allowed.
    /// </summary>
    public int MaxPartySize { get; init; } = 20;
}

/// <summary>
/// Represents opening hours for a specific day.
/// </summary>
public record ScheduleEntry
{
    public TimeOnly OpenTime { get; init; }
    public TimeOnly CloseTime { get; init; }
    public bool IsClosed { get; init; }

    public override string ToString() =>
        IsClosed ? "Cerrado" : $"{OpenTime:HH:mm} – {CloseTime:HH:mm}";
}
