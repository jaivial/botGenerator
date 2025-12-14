namespace BotGenerator.Core.Models;

/// <summary>
/// Data transfer object for updating an existing booking.
/// Only non-null fields will be updated.
/// </summary>
public record BookingUpdateData
{
    /// <summary>
    /// New reservation date (yyyy-MM-dd format for database).
    /// </summary>
    public string? ReservationDate { get; init; }

    /// <summary>
    /// New reservation time (HH:mm:ss format for database).
    /// </summary>
    public string? ReservationTime { get; init; }

    /// <summary>
    /// New party size.
    /// </summary>
    public int? PartySize { get; init; }

    /// <summary>
    /// New rice type. Use empty string to explicitly remove rice.
    /// </summary>
    public string? ArrozType { get; init; }

    /// <summary>
    /// New rice servings. Should be null if ArrozType is null/empty.
    /// </summary>
    public int? ArrozServings { get; init; }

    /// <summary>
    /// New number of high chairs.
    /// </summary>
    public int? HighChairs { get; init; }

    /// <summary>
    /// New number of baby strollers.
    /// </summary>
    public int? BabyStrollers { get; init; }

    /// <summary>
    /// Whether to clear rice (set both arroz_type and arroz_servings to NULL).
    /// </summary>
    public bool ClearRice { get; init; }
}
