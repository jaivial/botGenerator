namespace BotGenerator.Core.Models;

/// <summary>
/// Result of checking if user has future bookings.
/// </summary>
public record FutureBookingResult
{
    public bool HasFutureBooking { get; init; }
    public int BookingCount { get; init; }
    public FutureBookingSummary? NextBooking { get; init; }
}

public record FutureBookingSummary
{
    public int Id { get; init; }
    public string Date { get; init; } = "";
    public string Time { get; init; } = "";
    public int People { get; init; }
    public string? RiceType { get; init; }
}

/// <summary>
/// Opening hours with capacity per hour.
/// </summary>
public record OpeningHoursWithCapacityResult
{
    public string Date { get; init; } = "";
    public string Source { get; init; } = ""; // "database" or "default"
    public List<string> DefaultHours { get; init; } = new();
    public List<HourCapacityResult> Hours { get; init; } = new();
    public int TotalCapacity { get; init; }
    public int TotalBooked { get; init; }
    public int TotalFree { get; init; }
}

public record HourCapacityResult
{
    public string Hour { get; init; } = "";
    public bool Available { get; init; }
    public int Capacity { get; init; }
    public int Booked { get; init; }
    public int Free { get; init; }
    public bool IsClosed { get; init; }
}

/// <summary>
/// Hour configuration data from hour_configuration table.
/// </summary>
public record HourConfigurationResult
{
    public string Date { get; init; } = "";
    public bool HasCustomConfig { get; init; }
    public Dictionary<string, HourConfigSlot> HourData { get; init; } = new();
}

public record HourConfigSlot
{
    public int Capacity { get; init; }
    public int Bookings { get; init; }
    public double Percentage { get; init; }
    public bool IsClosed { get; init; }
    public string? Status { get; init; }
}

/// <summary>
/// Quick day capacity check result.
/// </summary>
public record DayCapacityResult
{
    public string Date { get; init; } = "";
    public string Status { get; init; } = ""; // "open", "full", or "closed"
    public int DailyLimit { get; init; }
    public int TotalBooked { get; init; }
    public int FreeSeats { get; init; }
    public bool IsFull { get; init; }
}

/// <summary>
/// Availability check for specific party size.
/// </summary>
public record AvailabilityForPartyResult
{
    public string Date { get; init; } = "";
    public int PartySize { get; init; }
    public bool Fits { get; init; }
    public int DailyLimit { get; init; }
    public int TotalBooked { get; init; }
    public int FreeSeats { get; init; }
    public string Message { get; init; } = "";
}
