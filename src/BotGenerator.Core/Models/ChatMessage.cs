namespace BotGenerator.Core.Models;

/// <summary>
/// Represents a message in the conversation history.
/// Used to provide context to the AI.
/// </summary>
public record ChatMessage
{
    /// <summary>
    /// Role of the message sender: "user" or "assistant".
    /// Maps to Gemini's "user" and "model" roles.
    /// </summary>
    public string Role { get; init; } = "user";

    /// <summary>
    /// The text content of the message.
    /// </summary>
    public string Content { get; init; } = "";

    /// <summary>
    /// ISO 8601 timestamp of the message.
    /// </summary>
    public string? Timestamp { get; init; }

    /// <summary>
    /// Unique message ID.
    /// </summary>
    public string? MessageId { get; init; }

    /// <summary>
    /// Display name of the sender.
    /// </summary>
    public string? FromName { get; init; }

    /// <summary>
    /// Creates a user message.
    /// </summary>
    public static ChatMessage FromUser(string content, string? fromName = null) =>
        new()
        {
            Role = "user",
            Content = content,
            FromName = fromName ?? "User",
            Timestamp = DateTime.UtcNow.ToString("O")
        };

    /// <summary>
    /// Creates an assistant message.
    /// </summary>
    public static ChatMessage FromAssistant(string content) =>
        new()
        {
            Role = "assistant",
            Content = content,
            FromName = "AI",
            Timestamp = DateTime.UtcNow.ToString("O")
        };
}
