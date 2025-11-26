# Step 02: Core Models

In this step, we'll define all the data models and types used throughout the application.

## 2.1 Intent Types Enumeration

### src/BotGenerator.Core/Models/IntentType.cs

```csharp
namespace BotGenerator.Core.Models;

/// <summary>
/// Represents the detected intent from an AI response.
/// Used to route the conversation to appropriate handlers.
/// </summary>
public enum IntentType
{
    /// <summary>
    /// Normal conversational response, no special action needed.
    /// </summary>
    Normal,

    /// <summary>
    /// User wants to make a reservation.
    /// AI has generated a BOOKING_REQUEST command.
    /// </summary>
    Booking,

    /// <summary>
    /// User wants to cancel an existing reservation.
    /// AI has generated a CANCELLATION_REQUEST command.
    /// </summary>
    Cancellation,

    /// <summary>
    /// User wants to modify an existing reservation.
    /// AI has generated a MODIFICATION_INTENT command.
    /// </summary>
    Modification,

    /// <summary>
    /// User is trying to book for the same day.
    /// Restaurant policy may not allow same-day bookings.
    /// </summary>
    SameDay,

    /// <summary>
    /// Response contains URLs or interactive elements.
    /// May need special handling for WhatsApp formatting.
    /// </summary>
    Interactive,

    /// <summary>
    /// An error occurred during processing.
    /// Should return a generic error message to user.
    /// </summary>
    Error
}
```

## 2.2 WhatsApp Message Model

### src/BotGenerator.Core/Models/WhatsAppMessage.cs

```csharp
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
```

## 2.3 Chat Message Model (for History)

### src/BotGenerator.Core/Models/ChatMessage.cs

```csharp
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
```

## 2.4 Conversation State Model

### src/BotGenerator.Core/Models/ConversationState.cs

```csharp
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
    public int HighChairs { get; init; }

    /// <summary>
    /// Number of baby strollers.
    /// </summary>
    public int BabyStrollers { get; init; }

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
        MissingData = new List<string> { "fecha", "hora", "personas", "arroz_decision" },
        Stage = "collecting_info"
    };
}
```

## 2.5 Booking Data Model

### src/BotGenerator.Core/Models/BookingData.cs

```csharp
namespace BotGenerator.Core.Models;

/// <summary>
/// Represents the data for a reservation.
/// Extracted from AI response when BOOKING_REQUEST command is detected.
/// </summary>
public record BookingData
{
    /// <summary>
    /// Customer name for the reservation.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Customer phone number.
    /// </summary>
    public string Phone { get; init; } = "";

    /// <summary>
    /// Reservation date in dd/MM/yyyy format.
    /// </summary>
    public string Date { get; init; } = "";

    /// <summary>
    /// Reservation time in HH:mm format.
    /// </summary>
    public string Time { get; init; } = "";

    /// <summary>
    /// Number of people.
    /// </summary>
    public int People { get; init; }

    /// <summary>
    /// Type of rice ordered (null if no rice).
    /// </summary>
    public string? ArrozType { get; init; }

    /// <summary>
    /// Number of rice servings.
    /// </summary>
    public int? ArrozServings { get; init; }

    /// <summary>
    /// Number of high chairs needed.
    /// </summary>
    public int HighChairs { get; init; }

    /// <summary>
    /// Number of baby strollers.
    /// </summary>
    public int BabyStrollers { get; init; }

    /// <summary>
    /// Additional notes or comments.
    /// </summary>
    public string? Commentary { get; init; }

    /// <summary>
    /// Validates that all required fields are present.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(Phone) &&
        !string.IsNullOrWhiteSpace(Date) &&
        !string.IsNullOrWhiteSpace(Time) &&
        People > 0;

    /// <summary>
    /// Returns a list of missing required fields.
    /// </summary>
    public List<string> GetMissingFields()
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(Name)) missing.Add("nombre");
        if (string.IsNullOrWhiteSpace(Phone)) missing.Add("teléfono");
        if (string.IsNullOrWhiteSpace(Date)) missing.Add("fecha");
        if (string.IsNullOrWhiteSpace(Time)) missing.Add("hora");
        if (People <= 0) missing.Add("personas");

        return missing;
    }

    /// <summary>
    /// Converts date from dd/MM/yyyy to yyyy-MM-dd for database storage.
    /// </summary>
    public string? DateForDatabase
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Date)) return null;

            var parts = Date.Split('/');
            if (parts.Length != 3) return null;

            return $"{parts[2]}-{parts[1].PadLeft(2, '0')}-{parts[0].PadLeft(2, '0')}";
        }
    }
}
```

## 2.6 Agent Response Model

### src/BotGenerator.Core/Models/AgentResponse.cs

```csharp
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
```

## 2.7 Restaurant Configuration Model

### src/BotGenerator.Core/Models/RestaurantConfig.cs

```csharp
namespace BotGenerator.Core.Models;

/// <summary>
/// Configuration for a specific restaurant.
/// Loaded from configuration or database.
/// </summary>
public record RestaurantConfig
{
    /// <summary>
    /// Unique identifier for the restaurant.
    /// Used to load the correct prompt files.
    /// </summary>
    public string Id { get; init; } = "";

    /// <summary>
    /// Display name of the restaurant.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Phone number associated with this restaurant's WhatsApp.
    /// </summary>
    public string WhatsAppNumber { get; init; } = "";

    /// <summary>
    /// Contact phone number for customers.
    /// </summary>
    public string ContactPhone { get; init; } = "";

    /// <summary>
    /// Restaurant's website URL.
    /// </summary>
    public string? WebsiteUrl { get; init; }

    /// <summary>
    /// URL to the menu page.
    /// </summary>
    public string? MenuUrl { get; init; }

    /// <summary>
    /// Restaurant location/address.
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// Opening schedule by day of week.
    /// </summary>
    public Dictionary<DayOfWeek, ScheduleEntry> Schedule { get; init; } = new();

    /// <summary>
    /// Days the restaurant is closed.
    /// </summary>
    public List<DayOfWeek> ClosedDays { get; init; } = new();

    /// <summary>
    /// Whether same-day bookings are allowed.
    /// </summary>
    public bool AllowSameDayBookings { get; init; }

    /// <summary>
    /// Minimum hours advance for bookings.
    /// </summary>
    public int MinAdvanceHours { get; init; } = 2;

    /// <summary>
    /// Maximum party size allowed.
    /// </summary>
    public int MaxPartySize { get; init; } = 20;
}

/// <summary>
/// Represents opening hours for a specific day.
/// </summary>
public record ScheduleEntry
{
    public TimeOnly OpenTime { get; init; }
    public TimeOnly CloseTime { get; init; }
    public bool IsClosed { get; init; }

    public override string ToString() =>
        IsClosed ? "Cerrado" : $"{OpenTime:HH:mm} – {CloseTime:HH:mm}";
}
```

## 2.8 Rice Validation Models

### src/BotGenerator.Core/Models/RiceValidation.cs

```csharp
namespace BotGenerator.Core.Models;

/// <summary>
/// Result of rice type validation.
/// </summary>
public record RiceValidationResult
{
    /// <summary>
    /// Status of the validation: "valid", "not_found", "multiple".
    /// </summary>
    public string Status { get; init; } = "unknown";

    /// <summary>
    /// The validated rice name (cleaned, without prices).
    /// </summary>
    public string? RiceName { get; init; }

    /// <summary>
    /// The original user request.
    /// </summary>
    public string? OriginalRequest { get; init; }

    /// <summary>
    /// Multiple options if status is "multiple".
    /// </summary>
    public List<string>? Options { get; init; }

    /// <summary>
    /// Message to show to the user.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Whether the rice was successfully validated.
    /// </summary>
    public bool IsValid => Status == "valid";

    /// <summary>
    /// Creates a valid result.
    /// </summary>
    public static RiceValidationResult Valid(string riceName, string originalRequest) => new()
    {
        Status = "valid",
        RiceName = riceName,
        OriginalRequest = originalRequest,
        Message = $"✅ {riceName} disponible."
    };

    /// <summary>
    /// Creates a not found result.
    /// </summary>
    public static RiceValidationResult NotFound(string originalRequest) => new()
    {
        Status = "not_found",
        OriginalRequest = originalRequest,
        Message = "Lo siento, no tenemos ese tipo de arroz. " +
                 "¿Te gustaría ver nuestros arroces disponibles?"
    };

    /// <summary>
    /// Creates a multiple options result.
    /// </summary>
    public static RiceValidationResult Multiple(List<string> options, string originalRequest) => new()
    {
        Status = "multiple",
        Options = options,
        OriginalRequest = originalRequest,
        Message = $"Tenemos varias opciones: {string.Join(" y ", options)}. ¿Cuál prefieres?"
    };
}

/// <summary>
/// Represents an available rice type.
/// </summary>
public record RiceType
{
    public string Name { get; init; } = "";
    public decimal ExtraPrice { get; init; }
    public string? Description { get; init; }
    public bool IsAvailable { get; init; } = true;
}
```

## 2.9 Summary File (Global Usings)

Create a file to centralize global usings:

### src/BotGenerator.Core/GlobalUsings.cs

```csharp
global using System.Text;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using System.Text.RegularExpressions;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.Logging;
```

## Summary

In this step, we created all the core data models:

| Model | Purpose |
|-------|---------|
| `IntentType` | Enum for different conversation intents |
| `WhatsAppMessage` | Incoming WhatsApp message data |
| `ChatMessage` | Message in conversation history |
| `ConversationState` | Current booking state extracted from history |
| `BookingData` | Complete booking information |
| `AgentResponse` | Response from AI agent |
| `RestaurantConfig` | Restaurant-specific configuration |
| `RiceValidationResult` | Result of rice type validation |

## Next Step

Continue to [Step 03: Gemini Service](./03-gemini-service.md) where we'll implement the Google AI Studio API client.
