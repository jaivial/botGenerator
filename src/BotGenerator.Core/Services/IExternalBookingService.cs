namespace BotGenerator.Core.Services;

/// <summary>
/// Service for fetching booking information from external systems.
/// </summary>
public interface IExternalBookingService
{
    /// <summary>
    /// Fetches booking information for a phone number from the external API.
    /// Returns null if no booking found.
    /// </summary>
    Task<ExternalBookingInfo?> GetBookingByPhoneAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents booking information from the external system.
/// </summary>
public record ExternalBookingInfo
{
    /// <summary>
    /// Customer name.
    /// </summary>
    public string CustomerName { get; init; } = "";

    /// <summary>
    /// Booking date in dd/MM/yyyy format.
    /// </summary>
    public string Date { get; init; } = "";

    /// <summary>
    /// Booking time in HH:mm format.
    /// </summary>
    public string Time { get; init; } = "";

    /// <summary>
    /// Number of people.
    /// </summary>
    public int People { get; init; }

    /// <summary>
    /// Type of rice ordered, null if no rice.
    /// </summary>
    public string? ArrozType { get; init; }

    /// <summary>
    /// Number of rice servings.
    /// </summary>
    public int? ArrozServings { get; init; }

    /// <summary>
    /// Number of high chairs.
    /// </summary>
    public int HighChairs { get; init; }

    /// <summary>
    /// Number of baby strollers.
    /// </summary>
    public int BabyStrollers { get; init; }

    /// <summary>
    /// The original confirmation message from the external system.
    /// </summary>
    public string OriginalConfirmationMessage { get; init; } = "";
}
