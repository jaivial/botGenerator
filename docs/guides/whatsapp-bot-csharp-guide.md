# WhatsApp Bot with Google Gemini 2.5 Flash - C# Implementation Guide

This guide explains how to build a WhatsApp reservation bot similar to the n8n workflow for Alquería Villa Carmen, but implemented entirely in C# using Google AI Studio API (Gemini 2.5 Flash). The key feature is **external system prompt files** that allow easy customization for different restaurants.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                         WEBHOOK ENDPOINT                             │
│                    (Receives WhatsApp messages)                      │
└─────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      MESSAGE EXTRACTOR                               │
│            Parses: sender, text, type, button response              │
└─────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    CONTEXT BUILDER                                   │
│     Loads: System prompts, dates, schedules, restaurant info        │
└─────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│                  CONVERSATION HISTORY                                │
│           Fetches and formats WhatsApp history                       │
└─────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│              CONVERSATION STATE EXTRACTOR                            │
│     Extracts: date, time, people, rice preferences                  │
└─────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│                 SYSTEM PROMPT ASSEMBLER                              │
│   Combines prompt modules from external .txt/.md files              │
└─────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    MAIN AI AGENT                                     │
│             Google Gemini 2.5 Flash API                              │
└─────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│                   INTENT ROUTER                                      │
│   Routes: booking, cancellation, modification, normal               │
└─────────────────────────────────────────────────────────────────────┘
                                   │
            ┌──────────────────────┼──────────────────────┐
            ▼                      ▼                      ▼
┌───────────────────┐  ┌───────────────────┐  ┌───────────────────┐
│  BOOKING HANDLER  │  │ CANCELLATION HDL  │  │ MODIFICATION HDL  │
└───────────────────┘  └───────────────────┘  └───────────────────┘
            │                      │                      │
            ▼                      ▼                      ▼
┌─────────────────────────────────────────────────────────────────────┐
│              SPECIALIZED AI AGENTS (Mid-path)                        │
│   - Rice Validator Agent                                            │
│   - Date Parser Agent                                                │
│   - Availability Checker Agent                                       │
└─────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│                  WHATSAPP SENDER                                     │
│              Sends response back to user                            │
└─────────────────────────────────────────────────────────────────────┘
```

## Project Structure

```
BotGenerator/
├── src/
│   ├── BotGenerator.Api/
│   │   ├── Controllers/
│   │   │   └── WebhookController.cs
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   ├── BotGenerator.Core/
│   │   ├── Agents/
│   │   │   ├── MainConversationAgent.cs
│   │   │   ├── RiceValidatorAgent.cs
│   │   │   ├── DateParserAgent.cs
│   │   │   └── IAgent.cs
│   │   │
│   │   ├── Services/
│   │   │   ├── GeminiService.cs
│   │   │   ├── WhatsAppService.cs
│   │   │   ├── ConversationHistoryService.cs
│   │   │   ├── ContextBuilderService.cs
│   │   │   └── IntentRouterService.cs
│   │   │
│   │   ├── Handlers/
│   │   │   ├── BookingHandler.cs
│   │   │   ├── CancellationHandler.cs
│   │   │   └── ModificationHandler.cs
│   │   │
│   │   └── Models/
│   │       ├── WhatsAppMessage.cs
│   │       ├── ConversationState.cs
│   │       ├── BookingData.cs
│   │       └── AgentResponse.cs
│   │
│   └── BotGenerator.Prompts/        <-- EXTERNAL PROMPT FILES
│       ├── restaurants/
│       │   ├── villacarmen/
│       │   │   ├── system-main.txt
│       │   │   ├── booking-flow.txt
│       │   │   ├── cancellation-flow.txt
│       │   │   ├── modification-flow.txt
│       │   │   ├── rice-validation.txt
│       │   │   └── restaurant-info.txt
│       │   │
│       │   └── other-restaurant/
│       │       ├── system-main.txt
│       │       └── ... (same structure)
│       │
│       └── shared/
│           ├── date-parsing.txt
│           ├── whatsapp-history-rules.txt
│           └── common-responses.txt
│
├── tests/
└── BotGenerator.sln
```

## Step 1: Core Models

### WhatsAppMessage.cs

```csharp
namespace BotGenerator.Core.Models;

public record WhatsAppMessage
{
    public string InstanceName { get; init; } = "";
    public string SenderNumber { get; init; } = "";
    public string MessageText { get; init; } = "";
    public string MessageType { get; init; } = "text";
    public bool IsMediaMessage { get; init; }
    public bool IsButtonResponse { get; init; }
    public string? MessageId { get; init; }
    public long Timestamp { get; init; }
    public string PushName { get; init; } = "Cliente";
    public bool FromMe { get; init; }
    public string? ButtonId { get; init; }
    public string? ButtonText { get; init; }
}

public record ConversationState
{
    public string? Fecha { get; init; }
    public string? FechaFullText { get; init; }
    public string? Hora { get; init; }
    public int? Personas { get; init; }
    public string? ArrozType { get; init; }
    public int? ArrozServings { get; init; }
    public List<string> MissingData { get; init; } = new();
    public bool IsComplete { get; init; }
    public string Stage { get; init; } = "collecting_info";
}

public record BookingData
{
    public string Name { get; init; } = "";
    public string Phone { get; init; } = "";
    public string Date { get; init; } = "";
    public string Time { get; init; } = "";
    public int People { get; init; }
    public string? ArrozType { get; init; }
    public int? ArrozServings { get; init; }
    public int HighChairs { get; init; }
    public int BabyStrollers { get; init; }
}

public enum IntentType
{
    Normal,
    Booking,
    Cancellation,
    Modification,
    SameDay,
    Interactive,
    Error
}

public record AgentResponse
{
    public IntentType Intent { get; init; }
    public string AiResponse { get; init; } = "";
    public BookingData? ExtractedData { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}
```

## Step 2: Google Gemini Service

### GeminiService.cs

```csharp
using System.Net.Http.Json;
using System.Text.Json;

namespace BotGenerator.Core.Services;

public interface IGeminiService
{
    Task<string> GenerateAsync(string systemPrompt, string userMessage,
        List<ChatMessage>? history = null);
}

public record ChatMessage(string Role, string Content);

public class GeminiService : IGeminiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<GeminiService> _logger;

    public GeminiService(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _apiKey = config["GoogleAI:ApiKey"]
            ?? throw new ArgumentNullException("GoogleAI:ApiKey");
        _model = config["GoogleAI:Model"] ?? "gemini-2.5-flash-preview-05-20";
        _logger = logger;
    }

    public async Task<string> GenerateAsync(
        string systemPrompt,
        string userMessage,
        List<ChatMessage>? history = null)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        // Build contents array with history
        var contents = new List<object>();

        if (history != null)
        {
            foreach (var msg in history)
            {
                contents.Add(new
                {
                    role = msg.Role == "assistant" ? "model" : "user",
                    parts = new[] { new { text = msg.Content } }
                });
            }
        }

        // Add current user message
        contents.Add(new
        {
            role = "user",
            parts = new[] { new { text = userMessage } }
        });

        var requestBody = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = contents,
            generationConfig = new
            {
                temperature = 0.7,
                topP = 0.95,
                topK = 40,
                maxOutputTokens = 2048
            }
        };

        var response = await _httpClient.PostAsJsonAsync(url, requestBody);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Gemini API error: {Error}", error);
            throw new Exception($"Gemini API error: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        return result
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "";
    }
}
```

## Step 3: System Prompt Loader

### PromptLoaderService.cs

```csharp
using System.Text;

namespace BotGenerator.Core.Services;

public interface IPromptLoaderService
{
    Task<string> LoadPromptAsync(string restaurantId, string promptName);
    Task<string> LoadSharedPromptAsync(string promptName);
    Task<string> AssembleSystemPromptAsync(string restaurantId,
        Dictionary<string, object> context);
}

public class PromptLoaderService : IPromptLoaderService
{
    private readonly string _promptsBasePath;
    private readonly ILogger<PromptLoaderService> _logger;
    private readonly Dictionary<string, string> _promptCache = new();

    public PromptLoaderService(IConfiguration config, ILogger<PromptLoaderService> logger)
    {
        _promptsBasePath = config["Prompts:BasePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "prompts");
        _logger = logger;
    }

    public async Task<string> LoadPromptAsync(string restaurantId, string promptName)
    {
        var cacheKey = $"{restaurantId}:{promptName}";

        if (_promptCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var filePath = Path.Combine(_promptsBasePath, "restaurants",
            restaurantId, $"{promptName}.txt");

        if (!File.Exists(filePath))
        {
            // Try markdown
            filePath = Path.Combine(_promptsBasePath, "restaurants",
                restaurantId, $"{promptName}.md");
        }

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Prompt file not found: {Path}", filePath);
            return "";
        }

        var content = await File.ReadAllTextAsync(filePath);
        _promptCache[cacheKey] = content;
        return content;
    }

    public async Task<string> LoadSharedPromptAsync(string promptName)
    {
        var cacheKey = $"shared:{promptName}";

        if (_promptCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var filePath = Path.Combine(_promptsBasePath, "shared", $"{promptName}.txt");

        if (!File.Exists(filePath))
        {
            filePath = Path.Combine(_promptsBasePath, "shared", $"{promptName}.md");
        }

        if (!File.Exists(filePath))
            return "";

        var content = await File.ReadAllTextAsync(filePath);
        _promptCache[cacheKey] = content;
        return content;
    }

    public async Task<string> AssembleSystemPromptAsync(
        string restaurantId,
        Dictionary<string, object> context)
    {
        var sb = new StringBuilder();

        // Load main system prompt
        var mainPrompt = await LoadPromptAsync(restaurantId, "system-main");
        sb.AppendLine(ReplaceTokens(mainPrompt, context));
        sb.AppendLine("\n---\n");

        // Load restaurant info
        var restaurantInfo = await LoadPromptAsync(restaurantId, "restaurant-info");
        sb.AppendLine(ReplaceTokens(restaurantInfo, context));
        sb.AppendLine("\n---\n");

        // Load booking flow
        var bookingFlow = await LoadPromptAsync(restaurantId, "booking-flow");
        sb.AppendLine(ReplaceTokens(bookingFlow, context));
        sb.AppendLine("\n---\n");

        // Load cancellation flow
        var cancellationFlow = await LoadPromptAsync(restaurantId, "cancellation-flow");
        sb.AppendLine(ReplaceTokens(cancellationFlow, context));
        sb.AppendLine("\n---\n");

        // Load modification flow
        var modificationFlow = await LoadPromptAsync(restaurantId, "modification-flow");
        sb.AppendLine(ReplaceTokens(modificationFlow, context));
        sb.AppendLine("\n---\n");

        // Load shared prompts
        var whatsappRules = await LoadSharedPromptAsync("whatsapp-history-rules");
        sb.AppendLine(ReplaceTokens(whatsappRules, context));
        sb.AppendLine("\n---\n");

        var dateParsing = await LoadSharedPromptAsync("date-parsing");
        sb.AppendLine(ReplaceTokens(dateParsing, context));

        return sb.ToString();
    }

    private string ReplaceTokens(string template, Dictionary<string, object> context)
    {
        var result = template;

        foreach (var (key, value) in context)
        {
            result = result.Replace($"{{{{{key}}}}}", value?.ToString() ?? "");
            result = result.Replace($"${{{key}}}", value?.ToString() ?? "");
        }

        return result;
    }
}
```

## Step 4: Context Builder

### ContextBuilderService.cs

```csharp
namespace BotGenerator.Core.Services;

public interface IContextBuilderService
{
    Dictionary<string, object> BuildContext(
        WhatsAppMessage message,
        ConversationState? state,
        List<ChatMessage>? history);
}

public class ContextBuilderService : IContextBuilderService
{
    private static readonly string[] DaysOfWeek =
        { "domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado" };
    private static readonly string[] MonthsES =
        { "enero", "febrero", "marzo", "abril", "mayo", "junio",
          "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };

    public Dictionary<string, object> BuildContext(
        WhatsAppMessage message,
        ConversationState? state,
        List<ChatMessage>? history)
    {
        var now = DateTime.Now;
        var context = new Dictionary<string, object>
        {
            // Customer info
            ["pushName"] = message.PushName,
            ["senderNumber"] = message.SenderNumber,
            ["messageText"] = message.MessageText,

            // Date/Time info
            ["currentYear"] = now.Year,
            ["currentMonth"] = now.Month,
            ["currentDay"] = now.Day,
            ["todayDayName"] = DaysOfWeek[(int)now.DayOfWeek],
            ["todayMonthName"] = MonthsES[now.Month - 1],
            ["todayES"] = FormatSpanishDate(now),
            ["todayFormatted"] = now.ToString("dd/MM/yyyy"),
            ["isOpenToday"] = IsRestaurantOpen(now.DayOfWeek),
            ["todaySchedule"] = GetTodaySchedule(now.DayOfWeek),

            // Upcoming weekends
            ["upcomingWeekends"] = GetUpcomingWeekends(),
            ["nextSaturday"] = GetNextDay(DayOfWeek.Saturday),
            ["nextSunday"] = GetNextDay(DayOfWeek.Sunday),

            // Booking state
            ["state_fecha"] = state?.Fecha ?? "FALTA",
            ["state_hora"] = state?.Hora ?? "FALTA",
            ["state_personas"] = state?.Personas?.ToString() ?? "FALTA",
            ["state_arroz"] = state?.ArrozType ?? "FALTA PREGUNTAR",
            ["state_raciones"] = state?.ArrozServings?.ToString() ?? "",
            ["state_isComplete"] = state?.IsComplete ?? false,

            // History summary
            ["historyCount"] = history?.Count ?? 0,
            ["hasHistory"] = (history?.Count ?? 0) > 0,
            ["formattedHistory"] = FormatHistory(history)
        };

        return context;
    }

    private string FormatSpanishDate(DateTime date)
    {
        var dayName = DaysOfWeek[(int)date.DayOfWeek];
        var monthName = MonthsES[date.Month - 1];
        return $"{char.ToUpper(dayName[0])}{dayName[1..]}, {date.Day} de {monthName} de {date.Year}";
    }

    private bool IsRestaurantOpen(DayOfWeek day)
    {
        return day == DayOfWeek.Thursday || day == DayOfWeek.Friday ||
               day == DayOfWeek.Saturday || day == DayOfWeek.Sunday;
    }

    private string GetTodaySchedule(DayOfWeek day)
    {
        return day switch
        {
            DayOfWeek.Thursday => "13:30 – 17:00",
            DayOfWeek.Friday => "13:30 – 17:30",
            DayOfWeek.Saturday => "13:30 – 18:00",
            DayOfWeek.Sunday => "13:30 – 18:00",
            _ => "Cerrado"
        };
    }

    private List<object> GetUpcomingWeekends()
    {
        var weekends = new List<object>();
        var current = DateTime.Now.Date.AddDays(1);

        for (int i = 0; i < 14 && weekends.Count < 4; i++)
        {
            var checkDate = current.AddDays(i);
            if (checkDate.DayOfWeek == DayOfWeek.Saturday ||
                checkDate.DayOfWeek == DayOfWeek.Sunday)
            {
                weekends.Add(new
                {
                    dayName = DaysOfWeek[(int)checkDate.DayOfWeek],
                    formatted = checkDate.ToString("dd/MM/yyyy"),
                    fullText = FormatSpanishDate(checkDate)
                });
            }
        }

        return weekends;
    }

    private string GetNextDay(DayOfWeek targetDay)
    {
        var current = DateTime.Now.Date.AddDays(1);
        while (current.DayOfWeek != targetDay)
        {
            current = current.AddDays(1);
        }
        return current.ToString("dd/MM/yyyy");
    }

    private string FormatHistory(List<ChatMessage>? history)
    {
        if (history == null || history.Count == 0)
            return "Primer contacto con este cliente";

        var sb = new StringBuilder();
        foreach (var msg in history.TakeLast(10))
        {
            var emoji = msg.Role == "user" ? "👤" : "🤖";
            sb.AppendLine($"{emoji}: {msg.Content}");
        }
        return sb.ToString();
    }
}
```

## Step 5: Main Conversation Agent

### MainConversationAgent.cs

```csharp
namespace BotGenerator.Core.Agents;

public interface IAgent
{
    Task<AgentResponse> ProcessAsync(
        WhatsAppMessage message,
        ConversationState? state,
        List<ChatMessage>? history);
}

public class MainConversationAgent : IAgent
{
    private readonly IGeminiService _gemini;
    private readonly IPromptLoaderService _promptLoader;
    private readonly IContextBuilderService _contextBuilder;
    private readonly ILogger<MainConversationAgent> _logger;

    public MainConversationAgent(
        IGeminiService gemini,
        IPromptLoaderService promptLoader,
        IContextBuilderService contextBuilder,
        ILogger<MainConversationAgent> logger)
    {
        _gemini = gemini;
        _promptLoader = promptLoader;
        _contextBuilder = contextBuilder;
        _logger = logger;
    }

    public async Task<AgentResponse> ProcessAsync(
        WhatsAppMessage message,
        ConversationState? state,
        List<ChatMessage>? history)
    {
        // Get restaurant ID from configuration or message
        var restaurantId = GetRestaurantId(message.SenderNumber);

        // Build context with all dynamic values
        var context = _contextBuilder.BuildContext(message, state, history);

        // Assemble system prompt from external files
        var systemPrompt = await _promptLoader.AssembleSystemPromptAsync(
            restaurantId, context);

        _logger.LogDebug("System prompt assembled ({Length} chars)",
            systemPrompt.Length);

        // Call Gemini
        var aiResponse = await _gemini.GenerateAsync(
            systemPrompt,
            message.MessageText,
            history);

        // Parse the response for intents and data
        return ParseAiResponse(aiResponse, message);
    }

    private string GetRestaurantId(string senderNumber)
    {
        // In production, you'd look this up from a database
        // based on which WhatsApp number received the message
        return "villacarmen";
    }

    private AgentResponse ParseAiResponse(string aiResponse, WhatsAppMessage message)
    {
        var intent = IntentType.Normal;
        BookingData? extractedData = null;
        var cleanResponse = aiResponse;

        // Check for BOOKING_REQUEST
        if (aiResponse.Contains("BOOKING_REQUEST|"))
        {
            intent = IntentType.Booking;
            var match = System.Text.RegularExpressions.Regex.Match(
                aiResponse,
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
            }

            cleanResponse = System.Text.RegularExpressions.Regex.Replace(
                aiResponse, @"BOOKING_REQUEST\|[^\n]+", "").Trim();
        }
        // Check for CANCELLATION_REQUEST
        else if (aiResponse.Contains("CANCELLATION_REQUEST|"))
        {
            intent = IntentType.Cancellation;
            // Similar parsing...
        }
        // Check for MODIFICATION_INTENT
        else if (aiResponse.Contains("MODIFICATION_INTENT"))
        {
            intent = IntentType.Modification;
            cleanResponse = aiResponse.Replace("MODIFICATION_INTENT", "").Trim();
        }
        // Check for SAME_DAY_BOOKING
        else if (aiResponse.Contains("SAME_DAY_BOOKING"))
        {
            intent = IntentType.SameDay;
            cleanResponse = aiResponse.Replace("SAME_DAY_BOOKING", "").Trim();
        }

        // Clean markdown formatting for WhatsApp
        cleanResponse = CleanForWhatsApp(cleanResponse);

        return new AgentResponse
        {
            Intent = intent,
            AiResponse = cleanResponse,
            ExtractedData = extractedData
        };
    }

    private string CleanForWhatsApp(string text)
    {
        // Convert **text** to *text* for WhatsApp bold
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"\*\*([^*]+)\*\*", "*$1*");

        // Remove escape backslashes
        text = text.Replace(@"\_", "_").Replace(@"\|", "|");

        return text.Trim();
    }
}
```

## Step 6: Specialized Agents (Mid-path Calls)

### RiceValidatorAgent.cs

```csharp
namespace BotGenerator.Core.Agents;

public class RiceValidatorAgent : IAgent
{
    private readonly IGeminiService _gemini;
    private readonly IPromptLoaderService _promptLoader;
    private readonly ILogger<RiceValidatorAgent> _logger;

    public RiceValidatorAgent(
        IGeminiService gemini,
        IPromptLoaderService promptLoader,
        ILogger<RiceValidatorAgent> logger)
    {
        _gemini = gemini;
        _promptLoader = promptLoader;
        _logger = logger;
    }

    public async Task<AgentResponse> ProcessAsync(
        WhatsAppMessage message,
        ConversationState? state,
        List<ChatMessage>? history)
    {
        // Get restaurant-specific rice validation prompt
        var restaurantId = "villacarmen"; // Get from config
        var ricePrompt = await _promptLoader.LoadPromptAsync(
            restaurantId, "rice-validation");

        // Fetch available rice types (from API or config)
        var availableRiceTypes = await GetAvailableRiceTypesAsync(restaurantId);

        var context = new Dictionary<string, object>
        {
            ["userRiceRequest"] = message.MessageText,
            ["availableRiceTypes"] = string.Join(", ", availableRiceTypes)
        };

        // Replace tokens in prompt
        var systemPrompt = ReplaceTokens(ricePrompt, context);

        var aiResponse = await _gemini.GenerateAsync(
            systemPrompt,
            message.MessageText);

        return ParseRiceValidationResponse(aiResponse);
    }

    private async Task<List<string>> GetAvailableRiceTypesAsync(string restaurantId)
    {
        // In production, fetch from API or database
        return new List<string>
        {
            "Arroz meloso de pulpo y gambones (+5€)",
            "Arroz de señoret (+3€)",
            "Paella valenciana de la Albufera",
            "Arroz Negro",
            "Arroz a banda"
        };
    }

    private AgentResponse ParseRiceValidationResponse(string aiResponse)
    {
        if (aiResponse.Contains("RICE_VALID|"))
        {
            var parts = aiResponse.Split("RICE_VALID|");
            return new AgentResponse
            {
                Intent = IntentType.Normal,
                AiResponse = $"✅ {parts[1].Trim()} disponible.",
                Metadata = new Dictionary<string, object>
                {
                    ["riceStatus"] = "valid",
                    ["riceName"] = parts[1].Trim()
                }
            };
        }
        else if (aiResponse.Contains("RICE_NOT_FOUND|"))
        {
            return new AgentResponse
            {
                Intent = IntentType.Normal,
                AiResponse = "Lo siento, no tenemos ese tipo de arroz. " +
                            "¿Te gustaría ver nuestros arroces disponibles?",
                Metadata = new Dictionary<string, object>
                {
                    ["riceStatus"] = "not_found"
                }
            };
        }
        else if (aiResponse.Contains("RICE_MULTIPLE|"))
        {
            var parts = aiResponse.Split("RICE_MULTIPLE|");
            return new AgentResponse
            {
                Intent = IntentType.Normal,
                AiResponse = $"Tenemos varias opciones: {parts[1].Trim()}. " +
                            "¿Cuál prefieres?",
                Metadata = new Dictionary<string, object>
                {
                    ["riceStatus"] = "multiple",
                    ["options"] = parts[1].Trim()
                }
            };
        }

        return new AgentResponse
        {
            Intent = IntentType.Error,
            AiResponse = "Hubo un problema validando el arroz."
        };
    }

    private string ReplaceTokens(string template, Dictionary<string, object> context)
    {
        var result = template;
        foreach (var (key, value) in context)
        {
            result = result.Replace($"{{{{{key}}}}}", value?.ToString() ?? "");
        }
        return result;
    }
}
```

## Step 7: Intent Router

### IntentRouterService.cs

```csharp
namespace BotGenerator.Core.Services;

public interface IIntentRouterService
{
    Task<AgentResponse> RouteAsync(
        AgentResponse mainAgentResponse,
        WhatsAppMessage originalMessage,
        ConversationState? state);
}

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
        ConversationState? state)
    {
        _logger.LogInformation("Routing intent: {Intent}", mainAgentResponse.Intent);

        switch (mainAgentResponse.Intent)
        {
            case IntentType.Booking:
                return await HandleBookingAsync(mainAgentResponse, originalMessage, state);

            case IntentType.Cancellation:
                return await HandleCancellationAsync(mainAgentResponse, originalMessage);

            case IntentType.Modification:
                return await HandleModificationAsync(mainAgentResponse, originalMessage);

            case IntentType.SameDay:
                return new AgentResponse
                {
                    Intent = IntentType.SameDay,
                    AiResponse = "Lo sentimos, no aceptamos reservas para el mismo día. " +
                                "Por favor, llámanos al +34638857294 para ver disponibilidad."
                };

            default:
                return mainAgentResponse;
        }
    }

    private async Task<AgentResponse> HandleBookingAsync(
        AgentResponse response,
        WhatsAppMessage message,
        ConversationState? state)
    {
        // Check if we need to validate rice
        if (state?.ArrozType != null &&
            response.Metadata?.ContainsKey("needsRiceValidation") == true)
        {
            // Call Rice Validator Agent mid-path
            var riceValidator = _services.GetRequiredService<RiceValidatorAgent>();
            var riceResult = await riceValidator.ProcessAsync(message, state, null);

            if (riceResult.Metadata?["riceStatus"]?.ToString() == "valid")
            {
                // Continue with booking
                return await CreateBookingAsync(response.ExtractedData!, riceResult);
            }
            else
            {
                return riceResult; // Ask user to clarify rice
            }
        }

        // Normal booking flow
        if (response.ExtractedData != null)
        {
            return await CreateBookingAsync(response.ExtractedData, null);
        }

        return response;
    }

    private async Task<AgentResponse> CreateBookingAsync(
        BookingData data,
        AgentResponse? riceValidation)
    {
        var bookingHandler = _services.GetRequiredService<BookingHandler>();
        return await bookingHandler.CreateBookingAsync(data);
    }

    private Task<AgentResponse> HandleCancellationAsync(
        AgentResponse response,
        WhatsAppMessage message)
    {
        var handler = _services.GetRequiredService<CancellationHandler>();
        return handler.ProcessAsync(response.ExtractedData!, message.SenderNumber);
    }

    private Task<AgentResponse> HandleModificationAsync(
        AgentResponse response,
        WhatsAppMessage message)
    {
        var handler = _services.GetRequiredService<ModificationHandler>();
        return handler.ProcessAsync(message.SenderNumber);
    }
}
```

## Step 8: External Prompt Files

### prompts/restaurants/villacarmen/system-main.txt

```
# SISTEMA DE ASISTENTE DE RESERVAS - {{restaurantName}}

## IDENTIDAD Y CONTEXTO

Eres el asistente virtual de **{{restaurantName}}** en Valencia. Estás conversando con **{{pushName}}** por WhatsApp.

**INFORMACIÓN DEL CLIENTE:**
- Nombre: {{pushName}}
- Teléfono: {{senderNumber}}
- Mensaje: "{{messageText}}"

**INFORMACIÓN DE FECHA Y HORA:**
- HOY ES: {{todayES}}
- FECHA: {{todayFormatted}}
- AÑO: {{currentYear}}

## ESTADO ACTUAL DE LA RESERVA

**DATOS YA RECOPILADOS DE LA CONVERSACIÓN:**
{{#if state_fecha}}✅ Fecha: {{state_fecha}}{{else}}❌ Fecha: FALTA{{/if}}
{{#if state_hora}}✅ Hora: {{state_hora}}{{else}}❌ Hora: FALTA{{/if}}
{{#if state_personas}}✅ Personas: {{state_personas}}{{else}}❌ Personas: FALTA{{/if}}
{{#if state_arroz}}✅ Arroz: {{state_arroz}}{{else}}❌ Arroz: FALTA PREGUNTAR{{/if}}

**⚠️ REGLAS CRÍTICAS:**
1. **NUNCA preguntes por datos que ya tienen ✅**
2. **SOLO pregunta por datos que tienen ❌ FALTA**
3. **Sé breve y natural** - Como un humano real

## HISTORIAL DE CONVERSACIÓN

{{formattedHistory}}
```

### prompts/restaurants/villacarmen/restaurant-info.txt

```
## INFORMACIÓN DEL RESTAURANTE

**HORARIOS:**
- Jueves: 13:30 – 17:00
- Viernes: 13:30 – 17:30
- Sábado: 13:30 – 18:00
- Domingo: 13:30 – 18:00
- Cerrado: Lunes, Martes, Miércoles

**MENÚS:**
- Fin de semana: https://alqueriavillacarmen.com/menufindesemana.php
- Navidad: https://alqueriavillacarmen.com/menuNavidad.php

**PRÓXIMOS FINES DE SEMANA DISPONIBLES:**
{{#each upcomingWeekends}}
- {{dayName}} {{formatted}}
{{/each}}

**INTERPRETACIÓN DE FECHAS:**
- "el sábado" → Usa: {{nextSaturday}}
- "el domingo" → Usa: {{nextSunday}}
- NO pidas la fecha exacta si el usuario ya indicó el día
```

### prompts/restaurants/villacarmen/booking-flow.txt

```
## PROCESO DE RESERVAS

### DATOS NECESARIOS PARA COMPLETAR RESERVA:
- Nombre (tienes: {{pushName}})
- Teléfono (tienes: {{senderNumber}})
- Fecha, Hora, Personas
- **Arroz (OBLIGATORIO preguntar)**

### FLUJO COMPLETO DE RESERVA:

**PASO 1: Recoger datos básicos**
1. Usuario dice "quiero reservar para el domingo" → Fecha ✓
2. Preguntas: "¿Para cuántas personas?" → Personas ✓
3. Usuario: "5 personas a las 14:00" → Personas ✓, Hora ✓

**PASO 2: PREGUNTA OBLIGATORIA DE ARROZ**
4. **SIEMPRE pregunta:** "¿Queréis arroz?"

**CASO A: Usuario dice NO quiere arroz**
- TÚ: "Perfecto, sin arroz entonces."
- Marcas: arroz_type = null ✓
- **Tienes TODO** → Resume y pide confirmación

**CASO B: Usuario dice SÍ o menciona tipo de arroz**
- Sistema valida el arroz automáticamente
- DESPUÉS de ver ✅ disponible: Pregunta raciones
- TÚ: "¿Cuántas raciones de arroz queréis?"

**PASO 3: Confirmación final**
- Resume TODO y pregunta: "¿Confirmo la reserva?"
- Usuario: "sí" / "confirma" / "vale"
- Genera: BOOKING_REQUEST|nombre|teléfono|fecha|personas|hora

### EJEMPLOS DE PREGUNTAS NATURALES:
- ✅ "Perfecto! ¿Para cuántas personas?"
- ✅ "¿A qué hora te viene bien?"
- ❌ "¿Para cuántas *personas* y a qué *hora*?" (dos preguntas a la vez)
```

### prompts/restaurants/villacarmen/rice-validation.txt

```
# SISTEMA DE VALIDACIÓN DE ARROZ

Tu tarea es validar si el tipo de arroz solicitado por el cliente existe en nuestro menú.

**TIPOS DE ARROZ DISPONIBLES:**
{{availableRiceTypes}}

**ARROZ SOLICITADO POR EL CLIENTE:**
{{userRiceRequest}}

**INSTRUCCIONES:**

1. Compara el arroz solicitado con los tipos disponibles
2. Acepta coincidencias parciales por ingredientes clave:
   - "pulpo y gambones" coincide con "Arroz meloso de pulpo y gambones (+5€)"
   - "señoret" o "del señoret" coincide con "Arroz de señoret (+3€)"

3. Ignora diferencias en:
   - Mayúsculas/minúsculas
   - Acentos (señoret = senyoret)
   - Artículos (del, de, de la, etc.)

4. **AL DEVOLVER EL NOMBRE:**
   - Devuelve el nombre COMPLETO del arroz
   - ELIMINA precios y texto entre paréntesis

5. Responde EXACTAMENTE en este formato:

**Si el arroz EXISTE (único):**
RICE_VALID|[nombre completo sin precios]

**Si el arroz NO EXISTE:**
RICE_NOT_FOUND|[nombre solicitado]

**Si hay MÚLTIPLES variantes:**
RICE_MULTIPLE|[nombres completos separados por " y "]
```

### prompts/shared/whatsapp-history-rules.txt

```
## REGLAS PARA USO DEL HISTORIAL DE WHATSAPP

**REGLAS:**
1. ✅ **Contexto Completo**: Este historial incluye TODO - tus mensajes Y los del cliente
2. ✅ **Continuidad**: Si mencionan algo del pasado, lo tienes en el historial
3. ✅ **No Repitas**: NUNCA pidas información que YA dieron en mensajes anteriores
4. ✅ **Cambios de Tema**: Si cambian de tema abruptamente, reconoce ambos naturalmente

**EJEMPLOS DE USO CORRECTO:**
- ✅ "Antes dijiste que querías reservar para 4 personas, ¿quieres cambiar eso?"
- ✅ "Perfecto, entonces mantenemos las 4 personas que mencionaste"

**EJEMPLOS DE USO INCORRECTO:**
- ❌ "¿Para cuántas personas?" (cuando ya lo dijeron en el historial)
- ❌ "¿Qué día querías?" (cuando ya está en el historial)
```

## Step 9: Webhook Controller

### WebhookController.cs

```csharp
using Microsoft.AspNetCore.Mvc;

namespace BotGenerator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly MainConversationAgent _mainAgent;
    private readonly IIntentRouterService _intentRouter;
    private readonly IConversationHistoryService _historyService;
    private readonly IWhatsAppService _whatsApp;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        MainConversationAgent mainAgent,
        IIntentRouterService intentRouter,
        IConversationHistoryService historyService,
        IWhatsAppService whatsApp,
        ILogger<WebhookController> logger)
    {
        _mainAgent = mainAgent;
        _intentRouter = intentRouter;
        _historyService = historyService;
        _whatsApp = whatsApp;
        _logger = logger;
    }

    [HttpPost("whatsapp-webhook")]
    public async Task<IActionResult> HandleWhatsAppWebhook([FromBody] JsonElement body)
    {
        try
        {
            // 1. Extract message data
            var message = ExtractMessage(body);

            if (message.FromMe || string.IsNullOrEmpty(message.MessageText))
            {
                return Ok(); // Ignore our own messages
            }

            _logger.LogInformation(
                "Received message from {Sender}: {Text}",
                message.PushName, message.MessageText);

            // 2. Get conversation history
            var history = await _historyService.GetHistoryAsync(message.SenderNumber);

            // 3. Extract conversation state from history
            var state = _historyService.ExtractState(history);

            // 4. Process with main AI agent
            var agentResponse = await _mainAgent.ProcessAsync(message, state, history);

            // 5. Route based on intent (may call specialized agents)
            var finalResponse = await _intentRouter.RouteAsync(
                agentResponse, message, state);

            // 6. Send response via WhatsApp
            await _whatsApp.SendTextAsync(
                message.SenderNumber,
                finalResponse.AiResponse);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook");
            return StatusCode(500);
        }
    }

    private WhatsAppMessage ExtractMessage(JsonElement body)
    {
        var messageBody = body.GetProperty("message");
        var chatId = messageBody.GetProperty("chatid").GetString() ?? "";
        var senderNumber = chatId.Replace("@s.whatsapp.net", "");

        var messageText = "";
        if (messageBody.TryGetProperty("text", out var textProp))
        {
            messageText = textProp.GetString() ?? "";
        }
        else if (messageBody.TryGetProperty("vote", out var voteProp))
        {
            // Button response
            messageText = voteProp.GetString() ?? "";
        }

        return new WhatsAppMessage
        {
            SenderNumber = senderNumber,
            MessageText = messageText,
            MessageType = messageBody.TryGetProperty("messageType", out var typeProp)
                ? typeProp.GetString() ?? "text"
                : "text",
            PushName = body.TryGetProperty("chat", out var chatProp) &&
                      chatProp.TryGetProperty("name", out var nameProp)
                ? nameProp.GetString() ?? "Cliente"
                : "Cliente",
            FromMe = messageBody.TryGetProperty("fromMe", out var fromMeProp) &&
                    fromMeProp.GetBoolean(),
            IsButtonResponse = messageBody.TryGetProperty("messageType", out var msgType) &&
                              msgType.GetString() == "ButtonsResponseMessage",
            Timestamp = messageBody.TryGetProperty("messageTimestamp", out var ts)
                ? ts.GetInt64()
                : DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }
}
```

## Step 10: Configuration

### appsettings.json

```json
{
  "GoogleAI": {
    "ApiKey": "YOUR_GOOGLE_AI_STUDIO_API_KEY",
    "Model": "gemini-2.5-flash-preview-05-20"
  },
  "WhatsApp": {
    "ApiUrl": "https://your-instance.uazapi.com",
    "Token": "YOUR_UAZAPI_TOKEN"
  },
  "Prompts": {
    "BasePath": "./prompts"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddHttpClient<IGeminiService, GeminiService>();
builder.Services.AddSingleton<IPromptLoaderService, PromptLoaderService>();
builder.Services.AddSingleton<IContextBuilderService, ContextBuilderService>();
builder.Services.AddScoped<MainConversationAgent>();
builder.Services.AddScoped<RiceValidatorAgent>();
builder.Services.AddScoped<IIntentRouterService, IntentRouterService>();
builder.Services.AddScoped<IConversationHistoryService, ConversationHistoryService>();
builder.Services.AddScoped<IWhatsAppService, WhatsAppService>();
builder.Services.AddScoped<BookingHandler>();
builder.Services.AddScoped<CancellationHandler>();
builder.Services.AddScoped<ModificationHandler>();

var app = builder.Build();

app.MapControllers();
app.Run();
```

## Adding a New Restaurant

To add a new restaurant, simply:

1. **Create new prompt folder:**
   ```
   prompts/restaurants/new-restaurant/
   ```

2. **Copy and customize the prompt files:**
   - `system-main.txt` - Main identity and behavior
   - `restaurant-info.txt` - Hours, menus, location
   - `booking-flow.txt` - Booking process
   - `cancellation-flow.txt` - Cancellation rules
   - `modification-flow.txt` - Modification rules
   - `rice-validation.txt` (or similar specialized prompts)

3. **Configure the restaurant ID mapping** in your database or config

4. **Customize tokens** in the prompt files:
   - Change restaurant name
   - Update schedules
   - Modify menu URLs
   - Adjust validation rules

## Key Benefits of This Architecture

1. **External Prompt Files**: Easy to edit AI behavior without recompiling
2. **Multi-Agent Architecture**: Specialized agents for specific tasks (rice validation, date parsing)
3. **Intent Routing**: Clean separation of concerns
4. **Reusable**: Just create new prompt folder for new restaurants
5. **Maintainable**: Prompts are in readable text files
6. **Testable**: Each agent can be tested independently
7. **Scalable**: Add more specialized agents as needed

## Testing Locally

```bash
# Start the API
dotnet run --project src/BotGenerator.Api

# Test with curl
curl -X POST http://localhost:5000/api/webhook/whatsapp-webhook \
  -H "Content-Type: application/json" \
  -d '{
    "message": {
      "chatid": "34612345678@s.whatsapp.net",
      "text": "Hola, quiero reservar para el domingo",
      "fromMe": false
    },
    "chat": {
      "name": "Juan García"
    }
  }'
```
