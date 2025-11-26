# Step 06: Main Agent

In this step, we'll implement the main conversation agent that orchestrates the AI interaction.

## 6.1 Agent Architecture

The main agent:
1. Receives an incoming WhatsApp message
2. Loads conversation history
3. Extracts booking state from history
4. Assembles the system prompt
5. Calls Gemini API
6. Parses the response for commands
7. Returns the structured response

```
WhatsAppMessage
       │
       ▼
┌──────────────────┐
│   Main Agent     │
├──────────────────┤
│ 1. Get History   │───> ConversationHistoryService
│ 2. Extract State │───> StateExtractor
│ 3. Build Context │───> ContextBuilderService
│ 4. Load Prompt   │───> PromptLoaderService
│ 5. Call AI       │───> GeminiService
│ 6. Parse Response│───> ResponseParser
└──────────────────┘
       │
       ▼
  AgentResponse
```

## 6.2 Create the Agent Interface

### src/BotGenerator.Core/Agents/IAgent.cs

```csharp
using BotGenerator.Core.Models;

namespace BotGenerator.Core.Agents;

/// <summary>
/// Interface for AI agents that process messages.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Processes a WhatsApp message and generates a response.
    /// </summary>
    /// <param name="message">The incoming message.</param>
    /// <param name="state">Current conversation state.</param>
    /// <param name="history">Conversation history.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent's response.</returns>
    Task<AgentResponse> ProcessAsync(
        WhatsAppMessage message,
        ConversationState? state,
        List<ChatMessage>? history,
        CancellationToken cancellationToken = default);
}
```

## 6.3 Implement the Main Agent

### src/BotGenerator.Core/Agents/MainConversationAgent.cs

```csharp
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;

namespace BotGenerator.Core.Agents;

/// <summary>
/// Main conversation agent that handles general restaurant inquiries and bookings.
/// </summary>
public class MainConversationAgent : IAgent
{
    private readonly IGeminiService _gemini;
    private readonly IPromptLoaderService _promptLoader;
    private readonly IContextBuilderService _contextBuilder;
    private readonly IConversationHistoryService _historyService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MainConversationAgent> _logger;

    public MainConversationAgent(
        IGeminiService gemini,
        IPromptLoaderService promptLoader,
        IContextBuilderService contextBuilder,
        IConversationHistoryService historyService,
        IConfiguration configuration,
        ILogger<MainConversationAgent> logger)
    {
        _gemini = gemini ?? throw new ArgumentNullException(nameof(gemini));
        _promptLoader = promptLoader ?? throw new ArgumentNullException(nameof(promptLoader));
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentResponse> ProcessAsync(
        WhatsAppMessage message,
        ConversationState? state,
        List<ChatMessage>? history,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Processing message from {Sender}: {Preview}",
                message.PushName,
                message.MessageText.Length > 50
                    ? message.MessageText[..50] + "..."
                    : message.MessageText);

            // 1. Get restaurant ID for this conversation
            var restaurantId = GetRestaurantId(message.SenderNumber);

            // 2. Get conversation history if not provided
            history ??= await _historyService.GetHistoryAsync(
                message.SenderNumber, cancellationToken);

            // 3. Extract conversation state if not provided
            state ??= _historyService.ExtractState(history);

            // 4. Build context with all dynamic values
            var context = _contextBuilder.BuildContext(message, state, history);

            // 5. Assemble the system prompt from external files
            var systemPrompt = await _promptLoader.AssembleSystemPromptAsync(
                restaurantId, context);

            _logger.LogDebug(
                "Assembled system prompt: {Length} chars",
                systemPrompt.Length);

            // 6. Call Gemini API
            var aiResponse = await _gemini.GenerateAsync(
                systemPrompt,
                message.MessageText,
                history,
                cancellationToken);

            _logger.LogDebug(
                "Received AI response: {Preview}",
                aiResponse.Length > 100 ? aiResponse[..100] + "..." : aiResponse);

            // 7. Parse the response for commands and clean it
            var parsedResponse = ParseAiResponse(aiResponse, message, state);

            // 8. Store the new messages in history
            await _historyService.AddMessageAsync(
                message.SenderNumber,
                ChatMessage.FromUser(message.MessageText, message.PushName),
                cancellationToken);

            await _historyService.AddMessageAsync(
                message.SenderNumber,
                ChatMessage.FromAssistant(parsedResponse.AiResponse),
                cancellationToken);

            return parsedResponse;
        }
        catch (GeminiApiException ex)
        {
            _logger.LogError(ex, "Gemini API error processing message");
            return AgentResponse.Error($"AI service error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing message");
            return AgentResponse.Error("Unexpected error occurred");
        }
    }

    #region Private Methods

    /// <summary>
    /// Gets the restaurant ID based on the sender's phone number.
    /// In production, this would look up from a database or configuration.
    /// </summary>
    private string GetRestaurantId(string senderNumber)
    {
        // Try to find mapping in configuration
        var mapping = _configuration
            .GetSection("Restaurants:Mapping")
            .GetChildren()
            .ToDictionary(x => x.Key, x => x.Value);

        if (mapping.TryGetValue(senderNumber, out var restaurantId))
        {
            return restaurantId ?? "villacarmen";
        }

        // Fall back to default
        return _configuration["Restaurants:Default"] ?? "villacarmen";
    }

    /// <summary>
    /// Parses the AI response to extract commands and clean the response text.
    /// </summary>
    private AgentResponse ParseAiResponse(
        string aiResponse,
        WhatsAppMessage message,
        ConversationState? state)
    {
        var intent = IntentType.Normal;
        BookingData? extractedData = null;
        var cleanResponse = aiResponse;
        var metadata = new Dictionary<string, object>();

        // Remove markdown escaping
        var unescapedResponse = aiResponse
            .Replace(@"\_", "_")
            .Replace(@"\|", "|")
            .Replace(@"\*", "*");

        // ========== CHECK FOR BOOKING_REQUEST ==========
        if (unescapedResponse.Contains("BOOKING_REQUEST|"))
        {
            intent = IntentType.Booking;

            var match = Regex.Match(
                unescapedResponse,
                @"BOOKING_REQUEST\|([^|]+)\|([^|]+)\|([^|]+)\|([^|]+)\|([^\n|]+)");

            if (match.Success)
            {
                extractedData = new BookingData
                {
                    Name = match.Groups[1].Value.Trim(),
                    Phone = match.Groups[2].Value.Trim(),
                    Date = match.Groups[3].Value.Trim(),
                    People = int.TryParse(match.Groups[4].Value.Trim(), out var p) ? p : 0,
                    Time = match.Groups[5].Value.Trim()
                };

                // Extract additional fields if present
                extractedData = ExtractAdditionalBookingFields(unescapedResponse, extractedData);

                _logger.LogInformation(
                    "Extracted booking: {Name}, {Date}, {Time}, {People} people",
                    extractedData.Name, extractedData.Date,
                    extractedData.Time, extractedData.People);
            }

            // Remove command from response
            cleanResponse = Regex.Replace(
                unescapedResponse,
                @"BOOKING_REQUEST\|[^\n]+",
                "").Trim();
        }

        // ========== CHECK FOR CANCELLATION_REQUEST ==========
        else if (unescapedResponse.Contains("CANCELLATION_REQUEST|"))
        {
            intent = IntentType.Cancellation;

            var match = Regex.Match(
                unescapedResponse,
                @"CANCELLATION_REQUEST\|([^|]+)\|([^|]+)\|([^|]+)\|([^|]+)\|([^\n|]+)");

            if (match.Success)
            {
                extractedData = new BookingData
                {
                    Name = match.Groups[1].Value.Trim(),
                    Phone = match.Groups[2].Value.Trim(),
                    Date = match.Groups[3].Value.Trim(),
                    People = int.TryParse(match.Groups[4].Value.Trim(), out var p) ? p : 0,
                    Time = match.Groups[5].Value.Trim()
                };
            }

            cleanResponse = Regex.Replace(
                unescapedResponse,
                @"CANCELLATION_REQUEST\|[^\n]+",
                "").Trim();
        }

        // ========== CHECK FOR MODIFICATION_INTENT ==========
        else if (unescapedResponse.Contains("MODIFICATION_INTENT"))
        {
            intent = IntentType.Modification;
            cleanResponse = unescapedResponse
                .Replace("MODIFICATION_INTENT", "")
                .Trim();
        }

        // ========== CHECK FOR SAME_DAY_BOOKING ==========
        else if (unescapedResponse.Contains("SAME_DAY_BOOKING"))
        {
            intent = IntentType.SameDay;
            cleanResponse = unescapedResponse
                .Replace("SAME_DAY_BOOKING", "")
                .Trim();

            // If response is empty, provide default message
            if (string.IsNullOrWhiteSpace(cleanResponse))
            {
                cleanResponse = "Lo sentimos, no aceptamos reservas para el mismo día. " +
                               "Por favor, llámanos al +34 638 857 294 para ver disponibilidad.";
            }
        }

        // ========== CHECK FOR URLs (INTERACTIVE) ==========
        else if (Regex.IsMatch(unescapedResponse, @"https?://[^\s]+"))
        {
            var urls = Regex.Matches(unescapedResponse, @"https?://[^\s]+")
                .Select(m => m.Value)
                .ToList();

            if (urls.Count > 0)
            {
                metadata["hasUrls"] = true;
                metadata["urls"] = urls;
            }
        }

        // Clean the response for WhatsApp
        cleanResponse = CleanForWhatsApp(cleanResponse);

        // Ensure we have some response
        if (string.IsNullOrWhiteSpace(cleanResponse))
        {
            cleanResponse = "Disculpa, no he entendido bien. ¿Puedes repetirlo?";
        }

        return new AgentResponse
        {
            Intent = intent,
            AiResponse = cleanResponse,
            ExtractedData = extractedData,
            Metadata = metadata.Count > 0 ? metadata : null,
            RawResponse = aiResponse
        };
    }

    /// <summary>
    /// Extracts additional booking fields like rice type, servings, etc.
    /// </summary>
    private BookingData ExtractAdditionalBookingFields(string response, BookingData booking)
    {
        // Extract rice type
        var riceMatch = Regex.Match(response,
            @"arroz\s+(del?\s+)?([a-záéíóúñ\s]+?)(?:\s*[,.]|\s*\d+|$)",
            RegexOptions.IgnoreCase);

        string? arrozType = null;
        if (riceMatch.Success)
        {
            var prep = riceMatch.Groups[1].Value.Trim();
            var name = riceMatch.Groups[2].Value.Trim();
            arrozType = string.IsNullOrEmpty(prep) ? name : $"{prep} {name}".Trim();
        }

        // Extract rice servings
        int? arrozServings = null;
        var servingsMatch = Regex.Match(response, @"(\d+)\s*raciones?", RegexOptions.IgnoreCase);
        if (servingsMatch.Success)
        {
            arrozServings = int.Parse(servingsMatch.Groups[1].Value);
        }

        // Extract high chairs
        var chairsMatch = Regex.Match(response, @"(\d+)\s*tronas?", RegexOptions.IgnoreCase);
        var highChairs = chairsMatch.Success ? int.Parse(chairsMatch.Groups[1].Value) : 0;

        // Extract strollers
        var strollersMatch = Regex.Match(response, @"(\d+)\s*carrit[oa]s?", RegexOptions.IgnoreCase);
        var babyStrollers = strollersMatch.Success ? int.Parse(strollersMatch.Groups[1].Value) : 0;

        return booking with
        {
            ArrozType = arrozType,
            ArrozServings = arrozServings,
            HighChairs = highChairs,
            BabyStrollers = babyStrollers
        };
    }

    /// <summary>
    /// Cleans the response text for WhatsApp formatting.
    /// </summary>
    private string CleanForWhatsApp(string text)
    {
        // Convert **text** to *text* for WhatsApp bold
        text = Regex.Replace(text, @"\*\*([^*]+)\*\*", "*$1*");

        // Remove escape backslashes
        text = text.Replace(@"\_", "_");
        text = text.Replace(@"\|", "|");

        // Remove multiple consecutive newlines
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        // Trim
        text = text.Trim();

        // Remove lines that are just whitespace
        var lines = text.Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        return string.Join("\n", lines);
    }

    #endregion
}
```

## 6.4 Conversation History Service

We need a service to manage conversation history:

### src/BotGenerator.Core/Services/IConversationHistoryService.cs

```csharp
using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Service for managing conversation history.
/// </summary>
public interface IConversationHistoryService
{
    /// <summary>
    /// Gets conversation history for a phone number.
    /// </summary>
    Task<List<ChatMessage>> GetHistoryAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a message to the conversation history.
    /// </summary>
    Task AddMessageAsync(
        string phoneNumber,
        ChatMessage message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears conversation history for a phone number.
    /// </summary>
    Task ClearHistoryAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts booking state from conversation history.
    /// </summary>
    ConversationState ExtractState(List<ChatMessage>? history);
}
```

### src/BotGenerator.Core/Services/ConversationHistoryService.cs

```csharp
using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// In-memory implementation of conversation history service.
/// In production, use Redis or a database.
/// </summary>
public class ConversationHistoryService : IConversationHistoryService
{
    private readonly Dictionary<string, List<ChatMessage>> _history = new();
    private readonly IContextBuilderService _contextBuilder;
    private readonly ILogger<ConversationHistoryService> _logger;
    private readonly int _maxMessages;
    private readonly TimeSpan _sessionTimeout;

    private readonly Dictionary<string, DateTime> _lastActivity = new();

    public ConversationHistoryService(
        IContextBuilderService contextBuilder,
        IConfiguration configuration,
        ILogger<ConversationHistoryService> logger)
    {
        _contextBuilder = contextBuilder;
        _logger = logger;
        _maxMessages = configuration.GetValue("History:MaxMessages", 30);
        _sessionTimeout = TimeSpan.FromMinutes(
            configuration.GetValue("History:SessionTimeoutMinutes", 30));
    }

    public Task<List<ChatMessage>> GetHistoryAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        // Check if session expired
        if (_lastActivity.TryGetValue(phoneNumber, out var lastActivity) &&
            DateTime.UtcNow - lastActivity > _sessionTimeout)
        {
            _history.Remove(phoneNumber);
            _lastActivity.Remove(phoneNumber);
            _logger.LogDebug("Session expired for {Phone}", phoneNumber);
        }

        if (_history.TryGetValue(phoneNumber, out var history))
        {
            return Task.FromResult(history.ToList());
        }

        return Task.FromResult(new List<ChatMessage>());
    }

    public Task AddMessageAsync(
        string phoneNumber,
        ChatMessage message,
        CancellationToken cancellationToken = default)
    {
        if (!_history.ContainsKey(phoneNumber))
        {
            _history[phoneNumber] = new List<ChatMessage>();
        }

        _history[phoneNumber].Add(message);
        _lastActivity[phoneNumber] = DateTime.UtcNow;

        // Trim to max messages
        if (_history[phoneNumber].Count > _maxMessages)
        {
            _history[phoneNumber] = _history[phoneNumber]
                .TakeLast(_maxMessages)
                .ToList();
        }

        return Task.CompletedTask;
    }

    public Task ClearHistoryAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        _history.Remove(phoneNumber);
        _lastActivity.Remove(phoneNumber);
        return Task.CompletedTask;
    }

    public ConversationState ExtractState(List<ChatMessage>? history)
    {
        if (history == null || history.Count == 0)
        {
            return ConversationState.Empty();
        }

        var state = new ConversationState();
        var missingData = new List<string>();

        // Extract date
        var fecha = ExtractDate(history);
        // Extract time
        var hora = ExtractTime(history);
        // Extract people count
        var personas = ExtractPeople(history);
        // Extract rice info
        var (arrozType, arrozServings) = ExtractRiceInfo(history);

        // Determine what's missing
        if (string.IsNullOrEmpty(fecha)) missingData.Add("fecha");
        if (string.IsNullOrEmpty(hora)) missingData.Add("hora");
        if (!personas.HasValue) missingData.Add("personas");
        if (arrozType == null && !WasAskedAboutRice(history)) missingData.Add("arroz_decision");
        if (arrozType != null && !arrozServings.HasValue) missingData.Add("arroz_servings");

        var isComplete = missingData.Count == 0;

        return new ConversationState
        {
            Fecha = fecha,
            Hora = hora,
            Personas = personas,
            ArrozType = arrozType,
            ArrozServings = arrozServings,
            MissingData = missingData,
            IsComplete = isComplete,
            Stage = isComplete ? "awaiting_confirmation" : "collecting_info"
        };
    }

    #region Extraction Methods

    private string? ExtractDate(List<ChatMessage> history)
    {
        var upcomingWeekends = _contextBuilder.GetUpcomingWeekends();

        for (int i = history.Count - 1; i >= 0; i--)
        {
            var msg = history[i];
            if (msg.Role != "user") continue;

            var text = msg.Content.ToLower();

            // Check for day names
            var dayMatch = Regex.Match(text, @"(?:para|el)\s+(domingo|sábado)");
            if (dayMatch.Success)
            {
                var dayName = dayMatch.Groups[1].Value;
                var weekend = upcomingWeekends.FirstOrDefault(
                    w => w.DayName == dayName);
                if (weekend != null)
                {
                    return weekend.Formatted;
                }
            }

            // Check for explicit date
            var dateMatch = Regex.Match(text, @"(\d{1,2})[/\-](\d{1,2})[/\-](\d{2,4})");
            if (dateMatch.Success)
            {
                return $"{dateMatch.Groups[1].Value}/{dateMatch.Groups[2].Value}/{dateMatch.Groups[3].Value}";
            }
        }

        return null;
    }

    private string? ExtractTime(List<ChatMessage> history)
    {
        for (int i = history.Count - 1; i >= 0; i--)
        {
            var msg = history[i];
            if (msg.Role != "user") continue;

            var match = Regex.Match(msg.Content,
                @"(?:a\s+las?|para\s+las?)\s*(\d{1,2}):?(\d{2})?",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var hour = match.Groups[1].Value;
                var minute = match.Groups[2].Success ? match.Groups[2].Value : "00";
                return $"{hour}:{minute}";
            }
        }

        return null;
    }

    private int? ExtractPeople(List<ChatMessage> history)
    {
        var patterns = new[]
        {
            @"(?:para|somos)\s+(\d+)\s*personas?",
            @"(\d+)\s*personas?",
            @"(?:para|somos)\s+(\d+)"
        };

        for (int i = history.Count - 1; i >= 0; i--)
        {
            var msg = history[i];
            if (msg.Role != "user") continue;

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(msg.Content, pattern, RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
                {
                    return count;
                }
            }
        }

        return null;
    }

    private (string? Type, int? Servings) ExtractRiceInfo(List<ChatMessage> history)
    {
        string? riceType = null;
        int? servings = null;

        // Look for rice validation confirmation from AI
        for (int i = history.Count - 1; i >= 0; i--)
        {
            var msg = history[i];

            if (msg.Role == "assistant" &&
                Regex.IsMatch(msg.Content, @"✅.*disponible", RegexOptions.IgnoreCase))
            {
                var match = Regex.Match(msg.Content, @"✅\s*(.+?)\s+disponible");
                if (match.Success)
                {
                    riceType = match.Groups[1].Value.Trim();
                    break;
                }
            }
        }

        // Look for "no rice" response
        for (int i = history.Count - 1; i >= 0 && riceType == null; i--)
        {
            var msg = history[i];
            if (msg.Role != "user") continue;

            // Check if previous message asked about rice
            if (i > 0 &&
                history[i - 1].Role == "assistant" &&
                Regex.IsMatch(history[i - 1].Content, @"queréis arroz", RegexOptions.IgnoreCase))
            {
                if (Regex.IsMatch(msg.Content, @"^(no|nada|sin arroz)", RegexOptions.IgnoreCase))
                {
                    riceType = ""; // Empty string means "no rice"
                }
            }
        }

        // Look for servings
        for (int i = history.Count - 1; i >= 0; i--)
        {
            var msg = history[i];
            if (msg.Role != "user") continue;

            var match = Regex.Match(msg.Content, @"(\d+)\s*raciones?", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                servings = int.Parse(match.Groups[1].Value);
                break;
            }
        }

        return (riceType, servings);
    }

    private bool WasAskedAboutRice(List<ChatMessage> history)
    {
        return history.Any(m =>
            m.Role == "assistant" &&
            Regex.IsMatch(m.Content, @"queréis arroz", RegexOptions.IgnoreCase));
    }

    #endregion
}
```

## 6.5 Register Services

### src/BotGenerator.Api/Program.cs (partial)

```csharp
// Register agents and services
builder.Services.AddHttpClient<IGeminiService, GeminiService>();
builder.Services.AddSingleton<IPromptLoaderService, PromptLoaderService>();
builder.Services.AddSingleton<IContextBuilderService, ContextBuilderService>();
builder.Services.AddSingleton<IConversationHistoryService, ConversationHistoryService>();
builder.Services.AddScoped<MainConversationAgent>();
```

## 6.6 Usage Example

```csharp
public class ConversationController
{
    private readonly MainConversationAgent _agent;

    public async Task<string> HandleMessageAsync(WhatsAppMessage message)
    {
        // Process the message
        var response = await _agent.ProcessAsync(message, null, null);

        // Check the intent and act accordingly
        switch (response.Intent)
        {
            case IntentType.Booking:
                // Handle booking
                return await ProcessBookingAsync(response);

            case IntentType.Cancellation:
                // Handle cancellation
                return await ProcessCancellationAsync(response);

            default:
                // Normal response
                return response.AiResponse;
        }
    }
}
```

## Summary

In this step, we:

1. Created the `IAgent` interface
2. Implemented `MainConversationAgent` with:
   - Restaurant ID mapping
   - Context building
   - Prompt assembly
   - Gemini API calls
   - Response parsing for commands
   - WhatsApp formatting
3. Created `ConversationHistoryService` with:
   - In-memory history storage
   - State extraction from history
   - Session timeout handling

## Next Step

Continue to [Step 07: Specialized Agents](./07-specialized-agents.md) where we'll build agents for rice validation and other specific tasks.
