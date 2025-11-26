# Step 05: Context Builder

In this step, we'll create the service that builds the dynamic context dictionary used to replace tokens in prompt templates.

## 5.1 What is Context?

Context is a dictionary of values that get injected into prompt templates at runtime:

```csharp
var context = new Dictionary<string, object>
{
    // Customer info
    ["pushName"] = "Juan García",
    ["senderNumber"] = "34612345678",

    // Date/time info
    ["todayES"] = "Martes, 25 de noviembre de 2025",
    ["todayFormatted"] = "25/11/2025",
    ["nextSaturday"] = "29/11/2025",

    // Booking state
    ["state_fecha"] = "29/11/2025",
    ["state_hora"] = null,  // Not collected yet

    // Conversation history
    ["formattedHistory"] = "👤: Hola\n🤖: ¡Hola! ¿En qué puedo ayudarte?"
};
```

These values replace tokens in prompts:
```
Hola {{pushName}}, hoy es {{todayES}}.
```
Becomes:
```
Hola Juan García, hoy es Martes, 25 de noviembre de 2025.
```

## 5.2 Create the Interface

### src/BotGenerator.Core/Services/IContextBuilderService.cs

```csharp
using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Service for building dynamic context dictionaries for prompt templates.
/// </summary>
public interface IContextBuilderService
{
    /// <summary>
    /// Builds a complete context dictionary for the main conversation agent.
    /// </summary>
    /// <param name="message">The incoming WhatsApp message.</param>
    /// <param name="state">Current conversation state (booking data collected).</param>
    /// <param name="history">Conversation history.</param>
    /// <param name="restaurantConfig">Restaurant-specific configuration.</param>
    /// <returns>Dictionary of context values.</returns>
    Dictionary<string, object> BuildContext(
        WhatsAppMessage message,
        ConversationState? state,
        List<ChatMessage>? history,
        RestaurantConfig? restaurantConfig = null);

    /// <summary>
    /// Builds context for a specialized agent (e.g., rice validator).
    /// </summary>
    /// <param name="baseContext">Base context to extend.</param>
    /// <param name="additionalValues">Additional values to merge.</param>
    /// <returns>Extended context dictionary.</returns>
    Dictionary<string, object> ExtendContext(
        Dictionary<string, object> baseContext,
        Dictionary<string, object> additionalValues);

    /// <summary>
    /// Gets upcoming weekend dates.
    /// </summary>
    List<WeekendDate> GetUpcomingWeekends(int count = 4);

    /// <summary>
    /// Formats a conversation history for display in prompts.
    /// </summary>
    string FormatHistory(List<ChatMessage>? history, int maxMessages = 10);
}

/// <summary>
/// Represents an upcoming weekend date.
/// </summary>
public record WeekendDate
{
    public string DayName { get; init; } = "";
    public string Formatted { get; init; } = "";
    public string FullText { get; init; } = "";
    public DateTime Date { get; init; }
    public bool IsSaturday => Date.DayOfWeek == DayOfWeek.Saturday;
    public bool IsSunday => Date.DayOfWeek == DayOfWeek.Sunday;
}
```

## 5.3 Implement the Service

### src/BotGenerator.Core/Services/ContextBuilderService.cs

```csharp
using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Implementation of IContextBuilderService.
/// Builds dynamic context dictionaries for prompt token replacement.
/// </summary>
public class ContextBuilderService : IContextBuilderService
{
    private readonly ILogger<ContextBuilderService> _logger;

    // Spanish day and month names
    private static readonly string[] DaysOfWeek =
    {
        "domingo", "lunes", "martes", "miércoles",
        "jueves", "viernes", "sábado"
    };

    private static readonly string[] MonthsES =
    {
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"
    };

    // Default schedules (can be overridden by RestaurantConfig)
    private static readonly Dictionary<DayOfWeek, string> DefaultSchedule = new()
    {
        { DayOfWeek.Monday, "Cerrado" },
        { DayOfWeek.Tuesday, "Cerrado" },
        { DayOfWeek.Wednesday, "Cerrado" },
        { DayOfWeek.Thursday, "13:30 – 17:00" },
        { DayOfWeek.Friday, "13:30 – 17:30" },
        { DayOfWeek.Saturday, "13:30 – 18:00" },
        { DayOfWeek.Sunday, "13:30 – 18:00" }
    };

    public ContextBuilderService(ILogger<ContextBuilderService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Dictionary<string, object> BuildContext(
        WhatsAppMessage message,
        ConversationState? state,
        List<ChatMessage>? history,
        RestaurantConfig? restaurantConfig = null)
    {
        var now = DateTime.Now;
        var upcomingWeekends = GetUpcomingWeekends();

        var context = new Dictionary<string, object>
        {
            // ========== CUSTOMER INFO ==========
            ["pushName"] = message.PushName,
            ["senderNumber"] = message.SenderNumber,
            ["messageText"] = message.MessageText,
            ["messageType"] = message.MessageType,
            ["isButtonResponse"] = message.IsButtonResponse,

            // ========== DATE/TIME INFO ==========
            ["currentYear"] = now.Year,
            ["currentMonth"] = now.Month,
            ["currentDay"] = now.Day,
            ["currentDayOfWeek"] = (int)now.DayOfWeek,
            ["todayDayName"] = DaysOfWeek[(int)now.DayOfWeek],
            ["todayMonthName"] = MonthsES[now.Month - 1],
            ["todayES"] = FormatSpanishDate(now),
            ["todayFormatted"] = now.ToString("dd/MM/yyyy"),
            ["todayISO"] = now.ToString("yyyy-MM-dd"),
            ["currentTime"] = now.ToString("HH:mm"),

            // ========== RESTAURANT STATUS ==========
            ["isOpenToday"] = IsRestaurantOpen(now.DayOfWeek, restaurantConfig),
            ["todaySchedule"] = GetScheduleForDay(now.DayOfWeek, restaurantConfig),

            // ========== UPCOMING DATES ==========
            ["upcomingWeekends"] = FormatUpcomingWeekends(upcomingWeekends),
            ["nextSaturday"] = GetNextDayFormatted(DayOfWeek.Saturday),
            ["nextSunday"] = GetNextDayFormatted(DayOfWeek.Sunday),
            ["nextSaturdayFull"] = GetNextDayFullText(DayOfWeek.Saturday),
            ["nextSundayFull"] = GetNextDayFullText(DayOfWeek.Sunday),
            ["nextOpenDay"] = GetNextOpenDayFormatted(restaurantConfig),

            // ========== SCHEDULE ==========
            ["schedule_jueves"] = GetScheduleForDay(DayOfWeek.Thursday, restaurantConfig),
            ["schedule_viernes"] = GetScheduleForDay(DayOfWeek.Friday, restaurantConfig),
            ["schedule_sabado"] = GetScheduleForDay(DayOfWeek.Saturday, restaurantConfig),
            ["schedule_domingo"] = GetScheduleForDay(DayOfWeek.Sunday, restaurantConfig),
            ["schedule_cerrado"] = "Lunes, Martes, Miércoles",

            // ========== BOOKING STATE ==========
            ["state_fecha"] = state?.Fecha,
            ["state_fecha_fullText"] = state?.FechaFullText,
            ["state_hora"] = state?.Hora,
            ["state_personas"] = state?.Personas,
            ["state_arroz"] = state?.ArrozType,
            ["state_raciones"] = state?.ArrozServings,
            ["state_isComplete"] = state?.IsComplete ?? false,
            ["state_stage"] = state?.Stage ?? "collecting_info",
            ["state_missingData"] = state?.MissingData != null
                ? string.Join(", ", state.MissingData)
                : "",

            // ========== CONVERSATION HISTORY ==========
            ["historyCount"] = history?.Count ?? 0,
            ["hasHistory"] = (history?.Count ?? 0) > 0,
            ["formattedHistory"] = FormatHistory(history),
            ["lastUserMessage"] = GetLastMessageByRole(history, "user"),
            ["lastAIMessage"] = GetLastMessageByRole(history, "assistant"),

            // ========== RESTAURANT INFO (from config) ==========
            ["restaurantName"] = restaurantConfig?.Name ?? "Alquería Villa Carmen",
            ["restaurantPhone"] = restaurantConfig?.ContactPhone ?? "+34 638 857 294",
            ["restaurantWeb"] = restaurantConfig?.WebsiteUrl ?? "https://alqueriavillacarmen.com",
            ["menuUrl"] = restaurantConfig?.MenuUrl ?? "https://alqueriavillacarmen.com/menufindesemana.php"
        };

        _logger.LogDebug(
            "Built context with {Count} values for {Customer}",
            context.Count, message.PushName);

        return context;
    }

    public Dictionary<string, object> ExtendContext(
        Dictionary<string, object> baseContext,
        Dictionary<string, object> additionalValues)
    {
        var extended = new Dictionary<string, object>(baseContext);

        foreach (var (key, value) in additionalValues)
        {
            extended[key] = value;
        }

        return extended;
    }

    public List<WeekendDate> GetUpcomingWeekends(int count = 4)
    {
        var weekends = new List<WeekendDate>();
        var current = DateTime.Now.Date.AddDays(1); // Start from tomorrow

        while (weekends.Count < count)
        {
            if (current.DayOfWeek == DayOfWeek.Saturday ||
                current.DayOfWeek == DayOfWeek.Sunday)
            {
                weekends.Add(new WeekendDate
                {
                    DayName = DaysOfWeek[(int)current.DayOfWeek],
                    Formatted = current.ToString("dd/MM/yyyy"),
                    FullText = FormatSpanishDate(current),
                    Date = current
                });
            }

            current = current.AddDays(1);
        }

        return weekends;
    }

    public string FormatHistory(List<ChatMessage>? history, int maxMessages = 10)
    {
        if (history == null || history.Count == 0)
        {
            return "Primer contacto con este cliente.";
        }

        var recentMessages = history.TakeLast(maxMessages).ToList();
        var sb = new StringBuilder();

        foreach (var msg in recentMessages)
        {
            var emoji = msg.Role == "user" ? "👤" : "🤖";
            var name = msg.Role == "user" ? (msg.FromName ?? "Cliente") : "Asistente";

            // Truncate long messages
            var content = msg.Content.Length > 200
                ? msg.Content[..200] + "..."
                : msg.Content;

            sb.AppendLine($"{emoji} {name}: {content}");
        }

        return sb.ToString().TrimEnd();
    }

    #region Private Helper Methods

    private string FormatSpanishDate(DateTime date)
    {
        var dayName = DaysOfWeek[(int)date.DayOfWeek];
        var monthName = MonthsES[date.Month - 1];

        // Capitalize first letter
        dayName = char.ToUpper(dayName[0]) + dayName[1..];

        return $"{dayName}, {date.Day} de {monthName} de {date.Year}";
    }

    private bool IsRestaurantOpen(DayOfWeek day, RestaurantConfig? config)
    {
        if (config?.ClosedDays != null && config.ClosedDays.Contains(day))
        {
            return false;
        }

        // Default: open Thursday-Sunday
        return day == DayOfWeek.Thursday ||
               day == DayOfWeek.Friday ||
               day == DayOfWeek.Saturday ||
               day == DayOfWeek.Sunday;
    }

    private string GetScheduleForDay(DayOfWeek day, RestaurantConfig? config)
    {
        if (config?.Schedule != null &&
            config.Schedule.TryGetValue(day, out var schedule))
        {
            return schedule.ToString();
        }

        return DefaultSchedule.TryGetValue(day, out var defaultSchedule)
            ? defaultSchedule
            : "Cerrado";
    }

    private string GetNextDayFormatted(DayOfWeek targetDay)
    {
        var current = DateTime.Now.Date.AddDays(1);

        while (current.DayOfWeek != targetDay)
        {
            current = current.AddDays(1);
        }

        return current.ToString("dd/MM/yyyy");
    }

    private string GetNextDayFullText(DayOfWeek targetDay)
    {
        var current = DateTime.Now.Date.AddDays(1);

        while (current.DayOfWeek != targetDay)
        {
            current = current.AddDays(1);
        }

        return FormatSpanishDate(current);
    }

    private string GetNextOpenDayFormatted(RestaurantConfig? config)
    {
        var current = DateTime.Now.Date.AddDays(1);
        var maxDays = 14;

        for (int i = 0; i < maxDays; i++)
        {
            if (IsRestaurantOpen(current.DayOfWeek, config))
            {
                return FormatSpanishDate(current);
            }
            current = current.AddDays(1);
        }

        return "próximo día de apertura";
    }

    private string FormatUpcomingWeekends(List<WeekendDate> weekends)
    {
        var sb = new StringBuilder();

        for (int i = 0; i < weekends.Count; i++)
        {
            var w = weekends[i];
            sb.AppendLine($"{i + 1}. {w.FullText} ({w.Formatted})");
        }

        return sb.ToString().TrimEnd();
    }

    private string GetLastMessageByRole(List<ChatMessage>? history, string role)
    {
        if (history == null || history.Count == 0)
        {
            return "";
        }

        var lastMessage = history
            .Where(m => m.Role == role)
            .LastOrDefault();

        return lastMessage?.Content ?? "";
    }

    #endregion
}
```

## 5.4 Register the Service

### src/BotGenerator.Api/Program.cs (partial)

```csharp
// Register context builder
builder.Services.AddSingleton<IContextBuilderService, ContextBuilderService>();
```

## 5.5 Usage Examples

### Basic Usage

```csharp
public class ConversationService
{
    private readonly IContextBuilderService _contextBuilder;
    private readonly IPromptLoaderService _promptLoader;

    public async Task<string> BuildSystemPromptAsync(
        WhatsAppMessage message,
        ConversationState state,
        List<ChatMessage> history)
    {
        // Build context with all dynamic values
        var context = _contextBuilder.BuildContext(message, state, history);

        // Use context to assemble prompt
        var systemPrompt = await _promptLoader.AssembleSystemPromptAsync(
            "villacarmen",
            context);

        return systemPrompt;
    }
}
```

### Extending Context for Specialized Agents

```csharp
public class RiceValidationService
{
    private readonly IContextBuilderService _contextBuilder;
    private readonly IPromptLoaderService _promptLoader;

    public async Task<string> GetRiceValidationPromptAsync(
        Dictionary<string, object> baseContext,
        string userRiceRequest,
        List<string> availableTypes)
    {
        // Extend base context with rice-specific values
        var extendedContext = _contextBuilder.ExtendContext(baseContext,
            new Dictionary<string, object>
            {
                ["userRiceRequest"] = userRiceRequest,
                ["availableRiceTypes"] = string.Join(", ", availableTypes),
                ["riceCount"] = availableTypes.Count
            });

        // Load specialized prompt with extended context
        return await _promptLoader.LoadSpecializedPromptAsync(
            "villacarmen",
            "rice-validation",
            extendedContext);
    }
}
```

## 5.6 Context Value Reference

Here's a complete reference of all context values:

| Key | Type | Description | Example |
|-----|------|-------------|---------|
| **Customer Info** |
| `pushName` | string | Customer's WhatsApp name | "Juan García" |
| `senderNumber` | string | Customer's phone | "34612345678" |
| `messageText` | string | Current message text | "Quiero reservar" |
| **Date/Time** |
| `currentYear` | int | Current year | 2025 |
| `todayES` | string | Spanish formatted date | "Martes, 25 de noviembre de 2025" |
| `todayFormatted` | string | DD/MM/YYYY format | "25/11/2025" |
| `nextSaturday` | string | Next Saturday | "29/11/2025" |
| `nextSunday` | string | Next Sunday | "30/11/2025" |
| **Restaurant** |
| `isOpenToday` | bool | Is restaurant open | true |
| `todaySchedule` | string | Today's hours | "13:30 – 18:00" |
| `restaurantName` | string | Restaurant name | "Alquería Villa Carmen" |
| **Booking State** |
| `state_fecha` | string? | Collected date | "29/11/2025" or null |
| `state_hora` | string? | Collected time | "14:00" or null |
| `state_personas` | int? | Number of people | 4 or null |
| `state_arroz` | string? | Rice type | "Arroz del señoret" or null |
| `state_isComplete` | bool | All data collected | false |
| **History** |
| `hasHistory` | bool | Has prior messages | true |
| `historyCount` | int | Number of messages | 5 |
| `formattedHistory` | string | Formatted history | "👤: Hola\n🤖: ..." |

## 5.7 Testing

### tests/BotGenerator.Core.Tests/Services/ContextBuilderServiceTests.cs

```csharp
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace BotGenerator.Core.Tests.Services;

public class ContextBuilderServiceTests
{
    private readonly ContextBuilderService _service;

    public ContextBuilderServiceTests()
    {
        _service = new ContextBuilderService(Mock.Of<ILogger<ContextBuilderService>>());
    }

    [Fact]
    public void BuildContext_IncludesCustomerInfo()
    {
        var message = new WhatsAppMessage
        {
            PushName = "Juan García",
            SenderNumber = "34612345678",
            MessageText = "Hola"
        };

        var context = _service.BuildContext(message, null, null);

        Assert.Equal("Juan García", context["pushName"]);
        Assert.Equal("34612345678", context["senderNumber"]);
        Assert.Equal("Hola", context["messageText"]);
    }

    [Fact]
    public void BuildContext_IncludesDateInfo()
    {
        var message = new WhatsAppMessage { PushName = "Test" };
        var context = _service.BuildContext(message, null, null);

        Assert.Equal(DateTime.Now.Year, context["currentYear"]);
        Assert.NotNull(context["todayES"]);
        Assert.NotNull(context["todayFormatted"]);
    }

    [Fact]
    public void BuildContext_IncludesBookingState()
    {
        var message = new WhatsAppMessage { PushName = "Test" };
        var state = new ConversationState
        {
            Fecha = "29/11/2025",
            Hora = "14:00",
            Personas = 4
        };

        var context = _service.BuildContext(message, state, null);

        Assert.Equal("29/11/2025", context["state_fecha"]);
        Assert.Equal("14:00", context["state_hora"]);
        Assert.Equal(4, context["state_personas"]);
    }

    [Fact]
    public void FormatHistory_ReturnsFirstContact_WhenEmpty()
    {
        var result = _service.FormatHistory(null);
        Assert.Contains("Primer contacto", result);
    }

    [Fact]
    public void FormatHistory_FormatsMessages()
    {
        var history = new List<ChatMessage>
        {
            ChatMessage.FromUser("Hola", "Juan"),
            ChatMessage.FromAssistant("¡Hola!")
        };

        var result = _service.FormatHistory(history);

        Assert.Contains("👤", result);
        Assert.Contains("🤖", result);
        Assert.Contains("Hola", result);
    }

    [Fact]
    public void GetUpcomingWeekends_ReturnsFourWeekends()
    {
        var weekends = _service.GetUpcomingWeekends();

        Assert.Equal(4, weekends.Count);
        Assert.All(weekends, w =>
            Assert.True(w.IsSaturday || w.IsSunday));
    }

    [Fact]
    public void ExtendContext_MergesValues()
    {
        var baseContext = new Dictionary<string, object>
        {
            ["key1"] = "value1"
        };

        var extended = _service.ExtendContext(baseContext,
            new Dictionary<string, object>
            {
                ["key2"] = "value2"
            });

        Assert.Equal("value1", extended["key1"]);
        Assert.Equal("value2", extended["key2"]);
    }
}
```

## Summary

In this step, we:

1. Defined `IContextBuilderService` interface
2. Implemented `ContextBuilderService` with:
   - Customer information extraction
   - Date/time formatting in Spanish
   - Restaurant schedule handling
   - Upcoming weekend calculation
   - Booking state mapping
   - Conversation history formatting
3. Created a complete reference of context values
4. Added unit tests

The context builder is the bridge between runtime data and prompt templates.

## Next Step

Continue to [Step 06: Main Agent](./06-main-agent.md) where we'll implement the main conversation agent.
