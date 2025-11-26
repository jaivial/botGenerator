namespace BotGenerator.Core.Models;

/// <summary>
/// Represents the response from an AI agent.
/// Contains the detected intent, response text, and any extracted data.
/// </summary>
public record AgentResponse
{
    /// <summary>
    /// The detected intent from the AI response.
    /// </summary>
    public IntentType Intent { get; init; } = IntentType.Normal;

    /// <summary>
    /// The cleaned AI response text to send to the user.
    /// Commands like BOOKING_REQUEST are removed from this.
    /// </summary>
    public string AiResponse { get; init; } = "";

    /// <summary>
    /// Extracted booking data if intent is Booking or Cancellation.
    /// </summary>
    public BookingData? ExtractedData { get; init; }

    /// <summary>
    /// Additional metadata from the AI response.
    /// Used for passing data between agents.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Error message if Intent is Error.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The original raw AI response before parsing.
    /// Useful for debugging.
    /// </summary>
    public string? RawResponse { get; init; }

    /// <summary>
    /// Timestamp when the response was generated.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Creates an error response.
    /// </summary>
    public static AgentResponse Error(string message) => new()
    {
        Intent = IntentType.Error,
        AiResponse = "Disculpa, hubo un problema con el asistente. " +
                    "Por favor, llámanos al +34638857294.",
        ErrorMessage = message
    };

    /// <summary>
    /// Creates a normal response.
    /// </summary>
    public static AgentResponse Normal(string response) => new()
    {
        Intent = IntentType.Normal,
        AiResponse = response
    };
}
