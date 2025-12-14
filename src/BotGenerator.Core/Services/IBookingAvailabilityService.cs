namespace BotGenerator.Core.Services;

public interface IBookingAvailabilityService
{
    Task<DayStatusResult> CheckDayStatusAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<DailyLimitResult> GetDailyLimitAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<HourDataResult> GetHourDataAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates whether a booking can be accepted for a given date/time/party size.
    /// If not, provides suggested alternative hours or next available date.
    /// </summary>
    Task<BookingAvailabilityDecision> EvaluateAsync(
        DateTime date,
        int partySize,
        TimeSpan? time,
        CancellationToken cancellationToken = default);
}

public record DayStatusResult
{
    public required DateTime Date { get; init; }
    public required string Weekday { get; init; }
    public required bool IsOpen { get; init; }
    public required bool IsDefaultClosedDay { get; init; }
}

public record DailyLimitResult
{
    public required DateTime Date { get; init; }
    public required int DailyLimit { get; init; }
    public required int TotalPeople { get; init; }
    public required int FreeBookingSeats { get; init; }
}

public record HourDataResult
{
    public required DateTime Date { get; init; }
    public required bool IsDefaultData { get; init; }
    public required int DailyLimit { get; init; }
    public required int TotalPeople { get; init; }
    public required List<string> ActiveHours { get; init; }
    public required Dictionary<string, HourSlotData> HourData { get; init; }
}

public record HourSlotData
{
    public required string Status { get; init; } // available | limited | full | closed
    public required int Capacity { get; init; } // available capacity (free seats)
    public required int TotalCapacity { get; init; }
    public required int Bookings { get; init; } // booked seats
    public required double Percentage { get; init; }
    public required double Completion { get; init; } // 0-100
    public required bool IsClosed { get; init; }
}

public record BookingAvailabilityDecision
{
    public required bool IsAvailable { get; init; }
    public required string Reason { get; init; } // ok | closed_day | daily_full | hour_unavailable | invalid
    public string? Message { get; init; }

    public DateTime? SuggestedDate { get; init; }
    public List<string>? SuggestedHours { get; init; }
}

