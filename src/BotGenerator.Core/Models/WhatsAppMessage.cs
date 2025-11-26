namespace BotGenerator.Core.Models;

/// <summary>
/// Represents an incoming WhatsApp message extracted from the webhook payload.
/// </summary>
public record WhatsAppMessage
{
    /// <summary>
    /// The WhatsApp instance name (for multi-instance setups).
    /// </summary>
    public string InstanceName { get; init; } = "";

    /// <summary>
    /// The sender's phone number (without @s.whatsapp.net suffix).
    /// Example: "34612345678"
    /// </summary>
    public string SenderNumber { get; init; } = "";

    /// <summary>
    /// The text content of the message.
    /// For button responses, this contains the selected button text.
    /// </summary>
    public string MessageText { get; init; } = "";

    /// <summary>
    /// Type of message: "text", "image", "audio", "video", "document", "button_response", etc.
    /// </summary>
    public string MessageType { get; init; } = "text";

    /// <summary>
    /// True if the message contains media (image, audio, video, document).
    /// </summary>
    public bool IsMediaMessage { get; init; }

    /// <summary>
    /// True if this is a response to an interactive button message.
    /// </summary>
    public bool IsButtonResponse { get; init; }

    /// <summary>
    /// Unique message ID from WhatsApp.
    /// </summary>
    public string? MessageId { get; init; }

    /// <summary>
    /// Unix timestamp when the message was sent.
    /// </summary>
    public long Timestamp { get; init; }

    /// <summary>
    /// The sender's display name (push name) in WhatsApp.
    /// Falls back to "Cliente" if not available.
    /// </summary>
    public string PushName { get; init; } = "Cliente";

    /// <summary>
    /// True if this message was sent by us (the bot).
    /// Used to filter out our own messages.
    /// </summary>
    public bool FromMe { get; init; }

    /// <summary>
    /// For button responses, the ID of the selected button.
    /// </summary>
    public string? ButtonId { get; init; }

    /// <summary>
    /// For button responses, the text of the selected button.
    /// </summary>
    public string? ButtonText { get; init; }

    /// <summary>
    /// Original raw JSON payload for debugging.
    /// </summary>
    public string? RawPayload { get; init; }

    /// <summary>
    /// Gets a human-readable timestamp.
    /// </summary>
    public DateTime TimestampDateTime =>
        DateTimeOffset.FromUnixTimeSeconds(Timestamp).LocalDateTime;
}
