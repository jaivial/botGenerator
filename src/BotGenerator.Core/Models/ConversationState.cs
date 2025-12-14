namespace BotGenerator.Core.Models;

/// <summary>
/// Represents the current state of a booking conversation.
/// Extracted from conversation history to track what data has been collected.
/// </summary>
public record ConversationState
{
    /// <summary>
    /// The reservation date in dd/MM/yyyy format.
    /// Null if not yet collected.
    /// </summary>
    public string? Fecha { get; init; }

    /// <summary>
    /// Full text representation of the date (e.g., "Sábado, 30 de noviembre de 2025").
    /// </summary>
    public string? FechaFullText { get; init; }

    /// <summary>
    /// The reservation time in HH:mm format.
    /// Null if not yet collected.
    /// </summary>
    public string? Hora { get; init; }

    /// <summary>
    /// True if the extracted time was rejected by the bot (e.g., outside opening hours).
    /// When true, Hora should be treated as missing and re-asked.
    /// </summary>
    public bool InvalidHora { get; init; }

    /// <summary>
    /// True if the extracted date was rejected by the bot (e.g., no availability for party size).
    /// When true, Fecha should be treated as missing and re-asked.
    /// </summary>
    public bool InvalidFecha { get; init; }

    /// <summary>
    /// Number of people for the reservation.
    /// Null if not yet collected.
    /// </summary>
    public int? Personas { get; init; }

    /// <summary>
    /// Type of rice requested.
    /// Null if not asked yet, empty string if user said "no rice".
    /// </summary>
    public string? ArrozType { get; init; }

    /// <summary>
    /// Number of rice servings requested.
    /// </summary>
    public int? ArrozServings { get; init; }

    /// <summary>
    /// Number of high chairs needed.
    /// </summary>
    public int? HighChairs { get; init; }

    /// <summary>
    /// Number of baby strollers.
    /// </summary>
    public int? BabyStrollers { get; init; }

    /// <summary>
    /// List of data fields that are still missing.
    /// </summary>
    public List<string> MissingData { get; init; } = new();

    /// <summary>
    /// True if all required data has been collected.
    /// </summary>
    public bool IsComplete { get; init; }

    /// <summary>
    /// True if we need to ask for confirmation before creating the booking.
    /// </summary>
    public bool NeedsConfirmation { get; init; }

    /// <summary>
    /// Current stage of the conversation:
    /// - "collecting_info": Still gathering booking data
    /// - "awaiting_confirmation": All data collected, waiting for user to confirm
    /// - "ready_to_book": User has confirmed, ready to create booking
    /// - "validating_rice": Waiting for rice validation result
    /// </summary>
    public string Stage { get; init; } = "collecting_info";

    /// <summary>
    /// Confidence levels for each extracted field.
    /// </summary>
    public Dictionary<string, string> Confidence { get; init; } = new();

    /// <summary>
    /// Creates an empty state for a new conversation.
    /// </summary>
    public static ConversationState Empty() => new()
    {
        MissingData = new List<string> { "fecha", "hora", "personas", "arroz_decision", "tronas", "carritos" },
        Stage = "collecting_info"
    };
}
