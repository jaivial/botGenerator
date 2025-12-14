namespace BotGenerator.Core.Services;

/// <summary>
/// Store for pending rice selection when multiple options are found.
/// </summary>
public interface IPendingRiceStore
{
    /// <summary>
    /// Gets pending rice options for a phone number.
    /// </summary>
    PendingRiceSelection? Get(string phoneNumber);

    /// <summary>
    /// Sets pending rice options for a phone number.
    /// </summary>
    void Set(string phoneNumber, PendingRiceSelection selection);

    /// <summary>
    /// Clears pending rice options for a phone number.
    /// </summary>
    void Clear(string phoneNumber);
}

/// <summary>
/// Represents pending rice selection data.
/// </summary>
public record PendingRiceSelection
{
    /// <summary>
    /// Available options the user can choose from.
    /// </summary>
    public List<string> Options { get; init; } = new();

    /// <summary>
    /// Original user request (e.g., "paella valenciana").
    /// </summary>
    public string OriginalRequest { get; init; } = "";

    /// <summary>
    /// Timestamp when options were presented.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
