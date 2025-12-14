namespace BotGenerator.Core.Services;

/// <summary>
/// Service for retrieving opening hours from the database.
/// </summary>
public interface IOpeningHoursService
{
    /// <summary>
    /// Gets opening hours information for a specific date.
    /// </summary>
    Task<OpeningHoursInfo> GetOpeningHoursAsync(DateTime date, CancellationToken ct = default);

    /// <summary>
    /// Gets opening hours information for a specific date, context-aware based on current time.
    /// </summary>
    Task<OpeningHoursInfo> GetContextAwareHoursAsync(DateTime date, CancellationToken ct = default);
}

/// <summary>
/// Opening hours information for a specific date.
/// </summary>
public record OpeningHoursInfo
{
    /// <summary>
    /// List of available time slots (e.g., ["13:30", "14:00", "14:30", "15:00"])
    /// </summary>
    public List<string> AvailableSlots { get; init; } = new();

    /// <summary>
    /// Opening time for the relevant service period.
    /// </summary>
    public TimeSpan OpeningTime { get; init; }

    /// <summary>
    /// Closing time for the relevant service period.
    /// </summary>
    public TimeSpan ClosingTime { get; init; }

    /// <summary>
    /// True if dinner service is available (20:00+ slots exist).
    /// </summary>
    public bool HasDinner { get; init; }

    /// <summary>
    /// True if lunch service is available.
    /// </summary>
    public bool HasLunch { get; init; } = true;

    /// <summary>
    /// True if hours came from database, false if using defaults.
    /// </summary>
    public bool IsFromDatabase { get; init; }

    /// <summary>
    /// Formatted opening time string (e.g., "13:30").
    /// </summary>
    public string OpeningTimeFormatted => $"{OpeningTime.Hours:D2}:{OpeningTime.Minutes:D2}";

    /// <summary>
    /// Formatted closing time string (e.g., "18:00").
    /// </summary>
    public string ClosingTimeFormatted => $"{ClosingTime.Hours:D2}:{ClosingTime.Minutes:D2}";
}
