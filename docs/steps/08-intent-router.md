# Step 08: Intent Router

In this step, we'll implement the intent router that directs the conversation flow based on detected intents.

## 8.1 Router Responsibilities

The Intent Router:
1. Receives the parsed response from the main agent
2. Routes to appropriate handlers based on intent
3. Calls specialized agents when needed
4. Returns the final response to send

```
MainAgent Response
        │
        ▼
┌───────────────────┐
│   Intent Router   │
├───────────────────┤
│ Switch on Intent: │
├───────────────────┤
│ Booking ──────────┼──> BookingHandler ──> RiceValidator?
│ Cancellation ─────┼──> CancellationHandler
│ Modification ─────┼──> ModificationHandler
│ SameDay ──────────┼──> SameDayHandler
│ Normal ───────────┼──> Return directly
└───────────────────┘
        │
        ▼
  Final Response
```

## 8.2 Create the Interface

### src/BotGenerator.Core/Services/IIntentRouterService.cs

```csharp
using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Service for routing conversation based on detected intent.
/// </summary>
public interface IIntentRouterService
{
    /// <summary>
    /// Routes the agent response to appropriate handlers based on intent.
    /// </summary>
    /// <param name="mainAgentResponse">Response from the main conversation agent.</param>
    /// <param name="originalMessage">The original WhatsApp message.</param>
    /// <param name="state">Current conversation state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Final response to send to user.</returns>
    Task<AgentResponse> RouteAsync(
        AgentResponse mainAgentResponse,
        WhatsAppMessage originalMessage,
        ConversationState? state,
        CancellationToken cancellationToken = default);
}
```

## 8.3 Implement the Router

### src/BotGenerator.Core/Services/IntentRouterService.cs

```csharp
using BotGenerator.Core.Agents;
using BotGenerator.Core.Handlers;
using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Implementation of IIntentRouterService.
/// Routes conversations based on detected intent.
/// </summary>
public class IntentRouterService : IIntentRouterService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<IntentRouterService> _logger;

    public IntentRouterService(
        IServiceProvider services,
        ILogger<IntentRouterService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task<AgentResponse> RouteAsync(
        AgentResponse mainAgentResponse,
        WhatsAppMessage originalMessage,
        ConversationState? state,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Routing intent: {Intent} for {Sender}",
            mainAgentResponse.Intent,
            originalMessage.PushName);

        try
        {
            return mainAgentResponse.Intent switch
            {
                IntentType.Booking => await HandleBookingAsync(
                    mainAgentResponse, originalMessage, state, cancellationToken),

                IntentType.Cancellation => await HandleCancellationAsync(
                    mainAgentResponse, originalMessage, cancellationToken),

                IntentType.Modification => await HandleModificationAsync(
                    mainAgentResponse, originalMessage, cancellationToken),

                IntentType.SameDay => HandleSameDay(mainAgentResponse),

                IntentType.Interactive => HandleInteractive(mainAgentResponse),

                IntentType.Error => HandleError(mainAgentResponse),

                _ => mainAgentResponse // Normal - return as-is
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error routing intent {Intent}", mainAgentResponse.Intent);
            return AgentResponse.Error("Error processing your request");
        }
    }

    #region Intent Handlers

    private async Task<AgentResponse> HandleBookingAsync(
        AgentResponse response,
        WhatsAppMessage message,
        ConversationState? state,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling booking intent");

        // Check if we need to validate rice type first
        var needsRiceValidation = response.ExtractedData?.ArrozType != null &&
                                  (response.Metadata?.ContainsKey("riceValidated") != true);

        if (needsRiceValidation && response.ExtractedData?.ArrozType != null)
        {
            var riceValidator = _services.GetRequiredService<RiceValidatorAgent>();
            var riceResult = await riceValidator.ValidateAsync(
                response.ExtractedData.ArrozType,
                "villacarmen",
                cancellationToken);

            if (!riceResult.IsValid)
            {
                _logger.LogInformation(
                    "Rice validation failed: {Status}",
                    riceResult.Status);

                return new AgentResponse
                {
                    Intent = IntentType.Normal,
                    AiResponse = riceResult.Message ?? "No se pudo validar el arroz",
                    Metadata = new Dictionary<string, object>
                    {
                        ["riceValidation"] = riceResult
                    }
                };
            }

            // Update booking data with validated rice name
            response = response with
            {
                ExtractedData = response.ExtractedData with
                {
                    ArrozType = riceResult.RiceName
                },
                Metadata = new Dictionary<string, object>(response.Metadata ?? new())
                {
                    ["riceValidated"] = true,
                    ["validatedRiceName"] = riceResult.RiceName ?? ""
                }
            };
        }

        // Check availability
        if (response.ExtractedData != null)
        {
            var availabilityChecker = _services.GetRequiredService<AvailabilityCheckerAgent>();
            var availability = availabilityChecker.CheckAvailability(response.ExtractedData);

            if (availability.IsSameDay)
            {
                return new AgentResponse
                {
                    Intent = IntentType.SameDay,
                    AiResponse = availability.Message
                };
            }

            if (!availability.IsAvailable)
            {
                _logger.LogInformation(
                    "Booking not available: {Message}",
                    availability.Message);

                return new AgentResponse
                {
                    Intent = IntentType.Normal,
                    AiResponse = availability.Message
                };
            }
        }

        // If we have all data, create the booking
        if (response.ExtractedData?.IsValid == true)
        {
            var bookingHandler = _services.GetRequiredService<BookingHandler>();
            return await bookingHandler.CreateBookingAsync(
                response.ExtractedData,
                message,
                cancellationToken);
        }

        // Return the main response (still collecting data)
        return response;
    }

    private async Task<AgentResponse> HandleCancellationAsync(
        AgentResponse response,
        WhatsAppMessage message,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling cancellation intent");

        if (response.ExtractedData == null)
        {
            return response; // Still collecting cancellation data
        }

        var cancellationHandler = _services.GetRequiredService<CancellationHandler>();
        return await cancellationHandler.ProcessCancellationAsync(
            response.ExtractedData,
            message.SenderNumber,
            cancellationToken);
    }

    private async Task<AgentResponse> HandleModificationAsync(
        AgentResponse response,
        WhatsAppMessage message,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling modification intent");

        var modificationHandler = _services.GetRequiredService<ModificationHandler>();
        return await modificationHandler.StartModificationFlowAsync(
            message.SenderNumber,
            cancellationToken);
    }

    private AgentResponse HandleSameDay(AgentResponse response)
    {
        _logger.LogDebug("Handling same-day booking rejection");

        var message = string.IsNullOrWhiteSpace(response.AiResponse)
            ? "Lo sentimos, no aceptamos reservas para el mismo día. " +
              "Por favor, llámanos al +34 638 857 294 para ver disponibilidad."
            : response.AiResponse;

        return response with { AiResponse = message };
    }

    private AgentResponse HandleInteractive(AgentResponse response)
    {
        _logger.LogDebug("Handling interactive response with URLs");

        // The response already contains URLs, just pass through
        // The WhatsApp service will format them appropriately
        return response;
    }

    private AgentResponse HandleError(AgentResponse response)
    {
        _logger.LogWarning("Handling error response");

        var message = "Disculpa, hubo un problema con el asistente. " +
                     "Por favor, llámanos al +34 638 857 294.";

        return response with { AiResponse = message };
    }

    #endregion
}
```

## 8.4 Create Handlers

### src/BotGenerator.Core/Handlers/BookingHandler.cs

```csharp
using BotGenerator.Core.Models;

namespace BotGenerator.Core.Handlers;

/// <summary>
/// Handler for creating bookings.
/// </summary>
public class BookingHandler
{
    private readonly ILogger<BookingHandler> _logger;
    private readonly IConfiguration _configuration;

    public BookingHandler(
        ILogger<BookingHandler> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<AgentResponse> CreateBookingAsync(
        BookingData booking,
        WhatsAppMessage message,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating booking for {Name}: {Date} {Time}, {People} people",
            booking.Name, booking.Date, booking.Time, booking.People);

        try
        {
            // In production, call your booking API here
            var success = await CreateBookingInDatabaseAsync(booking, cancellationToken);

            if (success)
            {
                var confirmationMessage = BuildConfirmationMessage(booking);

                return new AgentResponse
                {
                    Intent = IntentType.Booking,
                    AiResponse = confirmationMessage,
                    ExtractedData = booking,
                    Metadata = new Dictionary<string, object>
                    {
                        ["bookingCreated"] = true,
                        ["bookingId"] = Guid.NewGuid().ToString() // Replace with actual ID
                    }
                };
            }

            return AgentResponse.Error("No se pudo crear la reserva");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating booking");
            return AgentResponse.Error("Error al procesar la reserva");
        }
    }

    private async Task<bool> CreateBookingInDatabaseAsync(
        BookingData booking,
        CancellationToken cancellationToken)
    {
        // TODO: Implement actual database/API call
        // Example:
        // var apiUrl = _configuration["BookingApi:Url"];
        // var response = await _httpClient.PostAsJsonAsync(apiUrl, booking);
        // return response.IsSuccessStatusCode;

        await Task.Delay(100, cancellationToken); // Simulate API call
        return true;
    }

    private string BuildConfirmationMessage(BookingData booking)
    {
        var sb = new StringBuilder();
        sb.AppendLine("✅ *¡Reserva confirmada!*");
        sb.AppendLine();
        sb.AppendLine($"📅 *Fecha:* {booking.Date}");
        sb.AppendLine($"🕐 *Hora:* {booking.Time}");
        sb.AppendLine($"👥 *Personas:* {booking.People}");
        sb.AppendLine($"👤 *Nombre:* {booking.Name}");

        if (!string.IsNullOrEmpty(booking.ArrozType))
        {
            sb.AppendLine($"🍚 *Arroz:* {booking.ArrozType}");
            if (booking.ArrozServings.HasValue)
            {
                sb.AppendLine($"   *Raciones:* {booking.ArrozServings}");
            }
        }

        if (booking.HighChairs > 0)
        {
            sb.AppendLine($"🪑 *Tronas:* {booking.HighChairs}");
        }

        if (booking.BabyStrollers > 0)
        {
            sb.AppendLine($"🛒 *Carritos:* {booking.BabyStrollers}");
        }

        sb.AppendLine();
        sb.AppendLine("¡Te esperamos en Alquería Villa Carmen!");

        return sb.ToString();
    }
}
```

### src/BotGenerator.Core/Handlers/CancellationHandler.cs

```csharp
using BotGenerator.Core.Models;

namespace BotGenerator.Core.Handlers;

/// <summary>
/// Handler for cancelling bookings.
/// </summary>
public class CancellationHandler
{
    private readonly ILogger<CancellationHandler> _logger;

    public CancellationHandler(ILogger<CancellationHandler> logger)
    {
        _logger = logger;
    }

    public async Task<AgentResponse> ProcessCancellationAsync(
        BookingData cancellationData,
        string senderNumber,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing cancellation for {Name}: {Date}",
            cancellationData.Name, cancellationData.Date);

        try
        {
            // 1. Find the booking
            var booking = await FindBookingAsync(cancellationData, cancellationToken);

            if (booking == null)
            {
                return new AgentResponse
                {
                    Intent = IntentType.Normal,
                    AiResponse = "No se encontró ninguna reserva con esos datos. " +
                                "¿Puedes verificar la información?"
                };
            }

            // 2. Cancel the booking
            var success = await CancelBookingAsync(booking.Id, cancellationToken);

            if (success)
            {
                return new AgentResponse
                {
                    Intent = IntentType.Cancellation,
                    AiResponse = "✅ Reserva cancelada con éxito. " +
                                "Esperamos verte pronto en Alquería Villa Carmen.",
                    Metadata = new Dictionary<string, object>
                    {
                        ["cancelled"] = true,
                        ["bookingId"] = booking.Id
                    }
                };
            }

            return AgentResponse.Error("No se pudo cancelar la reserva");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing cancellation");
            return AgentResponse.Error("Error al procesar la cancelación");
        }
    }

    private async Task<BookingRecord?> FindBookingAsync(
        BookingData criteria,
        CancellationToken cancellationToken)
    {
        // TODO: Implement actual database lookup
        await Task.Delay(50, cancellationToken);

        // Simulate finding a booking
        return new BookingRecord
        {
            Id = "booking-123",
            CustomerName = criteria.Name,
            Date = criteria.Date,
            Time = criteria.Time
        };
    }

    private async Task<bool> CancelBookingAsync(
        string bookingId,
        CancellationToken cancellationToken)
    {
        // TODO: Implement actual cancellation
        await Task.Delay(50, cancellationToken);
        return true;
    }

    private record BookingRecord
    {
        public string Id { get; init; } = "";
        public string CustomerName { get; init; } = "";
        public string Date { get; init; } = "";
        public string Time { get; init; } = "";
    }
}
```

### src/BotGenerator.Core/Handlers/ModificationHandler.cs

```csharp
using BotGenerator.Core.Models;

namespace BotGenerator.Core.Handlers;

/// <summary>
/// Handler for modifying bookings.
/// </summary>
public class ModificationHandler
{
    private readonly ILogger<ModificationHandler> _logger;

    public ModificationHandler(ILogger<ModificationHandler> logger)
    {
        _logger = logger;
    }

    public async Task<AgentResponse> StartModificationFlowAsync(
        string senderNumber,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting modification flow for {Phone}",
            senderNumber);

        // Find existing bookings for this phone number
        var bookings = await FindBookingsForPhoneAsync(senderNumber, cancellationToken);

        if (bookings.Count == 0)
        {
            return new AgentResponse
            {
                Intent = IntentType.Normal,
                AiResponse = "No encontré reservas futuras asociadas a tu número. " +
                            "¿Quieres hacer una nueva reserva?"
            };
        }

        if (bookings.Count == 1)
        {
            // Only one booking, ask what they want to modify
            var booking = bookings[0];
            return new AgentResponse
            {
                Intent = IntentType.Modification,
                AiResponse = $"Encontré tu reserva para el *{booking.Date}* a las *{booking.Time}* " +
                            $"para *{booking.People} personas*.\n\n" +
                            "¿Qué te gustaría modificar?\n" +
                            "1️⃣ Fecha\n" +
                            "2️⃣ Hora\n" +
                            "3️⃣ Número de personas\n" +
                            "4️⃣ Tipo de arroz",
                Metadata = new Dictionary<string, object>
                {
                    ["modificationState"] = "selecting_field",
                    ["selectedBooking"] = booking
                }
            };
        }

        // Multiple bookings, ask which one
        var sb = new StringBuilder();
        sb.AppendLine("Encontré varias reservas a tu nombre:\n");

        for (int i = 0; i < bookings.Count; i++)
        {
            var b = bookings[i];
            sb.AppendLine($"*{i + 1}.* {b.Date} a las {b.Time} ({b.People} personas)");
        }

        sb.AppendLine("\n¿Cuál quieres modificar? (responde con el número)");

        return new AgentResponse
        {
            Intent = IntentType.Modification,
            AiResponse = sb.ToString(),
            Metadata = new Dictionary<string, object>
            {
                ["modificationState"] = "selecting_booking",
                ["bookings"] = bookings
            }
        };
    }

    private async Task<List<BookingInfo>> FindBookingsForPhoneAsync(
        string phoneNumber,
        CancellationToken cancellationToken)
    {
        // TODO: Implement actual database lookup
        await Task.Delay(50, cancellationToken);

        // Simulate finding bookings
        return new List<BookingInfo>
        {
            new()
            {
                Id = "booking-1",
                Date = "30/11/2025",
                Time = "14:00",
                People = 4
            }
        };
    }

    public record BookingInfo
    {
        public string Id { get; init; } = "";
        public string Date { get; init; } = "";
        public string Time { get; init; } = "";
        public int People { get; init; }
        public string? ArrozType { get; init; }
    }
}
```

## 8.5 Register Services

### src/BotGenerator.Api/Program.cs (partial)

```csharp
// Register intent router
builder.Services.AddScoped<IIntentRouterService, IntentRouterService>();

// Register handlers
builder.Services.AddScoped<BookingHandler>();
builder.Services.AddScoped<CancellationHandler>();
builder.Services.AddScoped<ModificationHandler>();
```

## Summary

In this step, we:

1. Created `IIntentRouterService` interface
2. Implemented `IntentRouterService` with routing logic
3. Created handlers for each intent:
   - `BookingHandler` - Creates bookings
   - `CancellationHandler` - Cancels bookings
   - `ModificationHandler` - Modifies bookings
4. Integrated specialized agents into the routing flow

The router orchestrates the entire conversation flow, calling specialized agents and handlers as needed.

## Next Step

Continue to [Step 09: Webhook & WhatsApp](./09-webhook-whatsapp.md) where we'll implement the webhook controller and WhatsApp integration.
