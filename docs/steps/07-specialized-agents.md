# Step 07: Specialized Agents

In this step, we'll create specialized AI agents that handle specific tasks during the conversation flow.

## 7.1 Why Specialized Agents?

The main agent handles general conversation, but some tasks need specialized processing:

1. **Rice Validator**: Validates rice types against the menu
2. **Date Parser**: Converts natural language dates to proper format
3. **Availability Checker**: Checks if a time slot is available

These agents:
- Have their own focused prompts
- Are called mid-flow when needed
- Return structured results

## 7.2 Rice Validator Agent

### src/BotGenerator.Core/Agents/RiceValidatorAgent.cs

```csharp
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;

namespace BotGenerator.Core.Agents;

/// <summary>
/// Specialized agent for validating rice types against the menu.
/// </summary>
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
        List<ChatMessage>? history,
        CancellationToken cancellationToken = default)
    {
        // This agent is typically called with rice request in message.MessageText
        var userRiceRequest = message.MessageText;

        _logger.LogInformation(
            "Validating rice request: {Request}",
            userRiceRequest);

        // Get available rice types (in production, fetch from API)
        var availableTypes = await GetAvailableRiceTypesAsync(cancellationToken);

        // Build context for rice validation prompt
        var context = new Dictionary<string, object>
        {
            ["userRiceRequest"] = userRiceRequest,
            ["availableRiceTypes"] = string.Join(", ", availableTypes),
            ["riceCount"] = availableTypes.Count
        };

        // Load rice validation prompt
        var systemPrompt = await _promptLoader.LoadSpecializedPromptAsync(
            "villacarmen", // Get from config
            "rice-validation",
            context);

        // Call Gemini with focused prompt
        var aiResponse = await _gemini.GenerateAsync(
            systemPrompt,
            userRiceRequest,
            cancellationToken: cancellationToken);

        // Parse the validation result
        return ParseValidationResult(aiResponse, userRiceRequest);
    }

    /// <summary>
    /// Validates a rice request and returns structured result.
    /// </summary>
    public async Task<RiceValidationResult> ValidateAsync(
        string userRiceRequest,
        string restaurantId,
        CancellationToken cancellationToken = default)
    {
        var availableTypes = await GetAvailableRiceTypesAsync(cancellationToken);

        var context = new Dictionary<string, object>
        {
            ["userRiceRequest"] = userRiceRequest,
            ["availableRiceTypes"] = string.Join(", ", availableTypes)
        };

        var systemPrompt = await _promptLoader.LoadSpecializedPromptAsync(
            restaurantId,
            "rice-validation",
            context);

        var aiResponse = await _gemini.GenerateAsync(
            systemPrompt,
            userRiceRequest,
            cancellationToken: cancellationToken);

        return ParseToValidationResult(aiResponse, userRiceRequest);
    }

    private async Task<List<string>> GetAvailableRiceTypesAsync(
        CancellationToken cancellationToken)
    {
        // In production, this would call an API
        // For now, return hardcoded list
        return new List<string>
        {
            "Arroz meloso de pulpo y gambones (+5€)",
            "Arroz de señoret (+3€)",
            "Paella valenciana de la Albufera",
            "Arroz Negro",
            "Arroz a banda",
            "Arroz meloso de carrillada con boletus",
            "Arroz seco de carrillada con boletus",
            "Fideuá de marisco"
        };
    }

    private AgentResponse ParseValidationResult(string aiResponse, string originalRequest)
    {
        var result = ParseToValidationResult(aiResponse, originalRequest);

        return new AgentResponse
        {
            Intent = IntentType.Normal,
            AiResponse = result.Message ?? "",
            Metadata = new Dictionary<string, object>
            {
                ["riceValidation"] = result,
                ["riceStatus"] = result.Status,
                ["riceName"] = result.RiceName ?? ""
            }
        };
    }

    private RiceValidationResult ParseToValidationResult(string aiResponse, string originalRequest)
    {
        if (aiResponse.Contains("RICE_VALID|"))
        {
            var parts = aiResponse.Split("RICE_VALID|");
            var riceName = parts.Length > 1 ? parts[1].Trim().Split('\n')[0] : "";

            return RiceValidationResult.Valid(riceName, originalRequest);
        }

        if (aiResponse.Contains("RICE_NOT_FOUND|"))
        {
            return RiceValidationResult.NotFound(originalRequest);
        }

        if (aiResponse.Contains("RICE_MULTIPLE|"))
        {
            var parts = aiResponse.Split("RICE_MULTIPLE|");
            var options = parts.Length > 1
                ? parts[1].Trim().Split(" y ").Select(s => s.Trim()).ToList()
                : new List<string>();

            return RiceValidationResult.Multiple(options, originalRequest);
        }

        _logger.LogWarning(
            "Unexpected rice validation response: {Response}",
            aiResponse);

        return RiceValidationResult.NotFound(originalRequest);
    }
}
```

## 7.3 Date Parser Agent

### src/BotGenerator.Core/Agents/DateParserAgent.cs

```csharp
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;

namespace BotGenerator.Core.Agents;

/// <summary>
/// Specialized agent for parsing natural language dates.
/// </summary>
public class DateParserAgent : IAgent
{
    private readonly IContextBuilderService _contextBuilder;
    private readonly ILogger<DateParserAgent> _logger;

    // Mapping of Spanish day names to DayOfWeek
    private static readonly Dictionary<string, DayOfWeek> SpanishDays = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lunes"] = DayOfWeek.Monday,
        ["martes"] = DayOfWeek.Tuesday,
        ["miércoles"] = DayOfWeek.Wednesday,
        ["miercoles"] = DayOfWeek.Wednesday,
        ["jueves"] = DayOfWeek.Thursday,
        ["viernes"] = DayOfWeek.Friday,
        ["sábado"] = DayOfWeek.Saturday,
        ["sabado"] = DayOfWeek.Saturday,
        ["domingo"] = DayOfWeek.Sunday
    };

    // Spanish month names
    private static readonly Dictionary<string, int> SpanishMonths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["enero"] = 1, ["febrero"] = 2, ["marzo"] = 3, ["abril"] = 4,
        ["mayo"] = 5, ["junio"] = 6, ["julio"] = 7, ["agosto"] = 8,
        ["septiembre"] = 9, ["octubre"] = 10, ["noviembre"] = 11, ["diciembre"] = 12
    };

    public DateParserAgent(
        IContextBuilderService contextBuilder,
        ILogger<DateParserAgent> logger)
    {
        _contextBuilder = contextBuilder;
        _logger = logger;
    }

    public Task<AgentResponse> ProcessAsync(
        WhatsAppMessage message,
        ConversationState? state,
        List<ChatMessage>? history,
        CancellationToken cancellationToken = default)
    {
        var result = ParseDate(message.MessageText);

        return Task.FromResult(new AgentResponse
        {
            Intent = IntentType.Normal,
            AiResponse = result.Success
                ? $"Fecha interpretada: {result.FormattedDate}"
                : "No se pudo interpretar la fecha",
            Metadata = new Dictionary<string, object>
            {
                ["dateParseResult"] = result
            }
        });
    }

    /// <summary>
    /// Parses a natural language date string.
    /// </summary>
    public DateParseResult ParseDate(string input)
    {
        var lowerInput = input.ToLower().Trim();
        var now = DateTime.Now;

        _logger.LogDebug("Parsing date input: {Input}", input);

        // Try explicit date format first: dd/mm/yyyy or dd-mm-yyyy
        var explicitMatch = Regex.Match(lowerInput, @"(\d{1,2})[/\-](\d{1,2})[/\-](\d{2,4})");
        if (explicitMatch.Success)
        {
            var day = int.Parse(explicitMatch.Groups[1].Value);
            var month = int.Parse(explicitMatch.Groups[2].Value);
            var year = int.Parse(explicitMatch.Groups[3].Value);

            if (year < 100) year += 2000;

            try
            {
                var date = new DateTime(year, month, day);
                return DateParseResult.FromDate(date);
            }
            catch
            {
                return DateParseResult.Failed("Fecha inválida");
            }
        }

        // Try "day + month" format: "15 de noviembre"
        var dayMonthMatch = Regex.Match(lowerInput,
            @"(\d{1,2})\s*(?:de\s+)?(" + string.Join("|", SpanishMonths.Keys) + ")");
        if (dayMonthMatch.Success)
        {
            var day = int.Parse(dayMonthMatch.Groups[1].Value);
            var monthName = dayMonthMatch.Groups[2].Value;
            var month = SpanishMonths[monthName];
            var year = now.Year;

            // If the date has passed, use next year
            var potentialDate = new DateTime(year, month, day);
            if (potentialDate < now.Date)
            {
                year++;
            }

            try
            {
                var date = new DateTime(year, month, day);
                return DateParseResult.FromDate(date);
            }
            catch
            {
                return DateParseResult.Failed("Fecha inválida");
            }
        }

        // Try day names: "el sábado", "el próximo domingo"
        var dayNameMatch = Regex.Match(lowerInput,
            @"(?:el\s+)?(?:próximo\s+)?(" + string.Join("|", SpanishDays.Keys) + ")");
        if (dayNameMatch.Success)
        {
            var dayName = dayNameMatch.Groups[1].Value;
            var targetDay = SpanishDays[dayName];

            var date = GetNextDayOfWeek(targetDay);
            return DateParseResult.FromDate(date);
        }

        // Try relative dates
        if (lowerInput.Contains("mañana"))
        {
            return DateParseResult.FromDate(now.Date.AddDays(1));
        }

        if (lowerInput.Contains("pasado mañana"))
        {
            return DateParseResult.FromDate(now.Date.AddDays(2));
        }

        if (lowerInput.Contains("hoy"))
        {
            return DateParseResult.FromDate(now.Date);
        }

        // Try "este fin de semana"
        if (Regex.IsMatch(lowerInput, @"este\s+fin\s+de\s+semana"))
        {
            var nextSaturday = GetNextDayOfWeek(DayOfWeek.Saturday);
            return DateParseResult.FromDate(nextSaturday);
        }

        return DateParseResult.Failed("No se pudo interpretar la fecha");
    }

    private DateTime GetNextDayOfWeek(DayOfWeek targetDay)
    {
        var current = DateTime.Now.Date.AddDays(1); // Start from tomorrow

        while (current.DayOfWeek != targetDay)
        {
            current = current.AddDays(1);
        }

        return current;
    }
}

/// <summary>
/// Result of date parsing.
/// </summary>
public record DateParseResult
{
    public bool Success { get; init; }
    public DateTime? ParsedDate { get; init; }
    public string? FormattedDate { get; init; }
    public string? FullText { get; init; }
    public string? Error { get; init; }

    private static readonly string[] MonthsES =
    {
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"
    };

    private static readonly string[] DaysES =
    {
        "domingo", "lunes", "martes", "miércoles",
        "jueves", "viernes", "sábado"
    };

    public static DateParseResult FromDate(DateTime date)
    {
        var dayName = DaysES[(int)date.DayOfWeek];
        var monthName = MonthsES[date.Month - 1];

        return new DateParseResult
        {
            Success = true,
            ParsedDate = date,
            FormattedDate = date.ToString("dd/MM/yyyy"),
            FullText = $"{char.ToUpper(dayName[0])}{dayName[1..]}, {date.Day} de {monthName} de {date.Year}"
        };
    }

    public static DateParseResult Failed(string error) => new()
    {
        Success = false,
        Error = error
    };
}
```

## 7.4 Availability Checker Agent

### src/BotGenerator.Core/Agents/AvailabilityCheckerAgent.cs

```csharp
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;

namespace BotGenerator.Core.Agents;

/// <summary>
/// Agent for checking booking availability.
/// </summary>
public class AvailabilityCheckerAgent : IAgent
{
    private readonly ILogger<AvailabilityCheckerAgent> _logger;

    // Restaurant schedule (in production, load from config/database)
    private static readonly Dictionary<DayOfWeek, (TimeSpan Open, TimeSpan Close)> Schedule = new()
    {
        { DayOfWeek.Thursday, (new TimeSpan(13, 30, 0), new TimeSpan(17, 0, 0)) },
        { DayOfWeek.Friday, (new TimeSpan(13, 30, 0), new TimeSpan(17, 30, 0)) },
        { DayOfWeek.Saturday, (new TimeSpan(13, 30, 0), new TimeSpan(18, 0, 0)) },
        { DayOfWeek.Sunday, (new TimeSpan(13, 30, 0), new TimeSpan(18, 0, 0)) }
    };

    public AvailabilityCheckerAgent(ILogger<AvailabilityCheckerAgent> logger)
    {
        _logger = logger;
    }

    public Task<AgentResponse> ProcessAsync(
        WhatsAppMessage message,
        ConversationState? state,
        List<ChatMessage>? history,
        CancellationToken cancellationToken = default)
    {
        // Extract date and time from state or message
        var bookingData = ExtractBookingData(message, state);

        var result = CheckAvailability(bookingData);

        return Task.FromResult(new AgentResponse
        {
            Intent = IntentType.Normal,
            AiResponse = result.Message,
            Metadata = new Dictionary<string, object>
            {
                ["availabilityResult"] = result
            }
        });
    }

    /// <summary>
    /// Checks availability for a booking.
    /// </summary>
    public AvailabilityResult CheckAvailability(BookingData booking)
    {
        _logger.LogInformation(
            "Checking availability: {Date} {Time} for {People} people",
            booking.Date, booking.Time, booking.People);

        // Parse the date
        if (!TryParseDate(booking.Date, out var date))
        {
            return AvailabilityResult.Invalid("Fecha inválida");
        }

        // Check if restaurant is open on that day
        if (!Schedule.ContainsKey(date.DayOfWeek))
        {
            var closedDays = "lunes, martes y miércoles";
            return AvailabilityResult.Unavailable(
                $"Lo siento, estamos cerrados ese día. Cerramos los {closedDays}.");
        }

        // Check if the date is in the past
        if (date.Date < DateTime.Now.Date)
        {
            return AvailabilityResult.Invalid("No se puede reservar para una fecha pasada");
        }

        // Check if same day (policy: no same-day bookings)
        if (date.Date == DateTime.Now.Date)
        {
            return AvailabilityResult.SameDay(
                "No aceptamos reservas para el mismo día. " +
                "Por favor, llámanos al +34 638 857 294.");
        }

        // Parse the time
        if (!TryParseTime(booking.Time, out var time))
        {
            return AvailabilityResult.Invalid("Hora inválida");
        }

        // Check if time is within opening hours
        var (openTime, closeTime) = Schedule[date.DayOfWeek];
        if (time < openTime || time > closeTime.Add(TimeSpan.FromMinutes(-60)))
        {
            return AvailabilityResult.Unavailable(
                $"Ese día abrimos de {FormatTime(openTime)} a {FormatTime(closeTime)}. " +
                $"¿Te viene bien otra hora?");
        }

        // Check party size
        if (booking.People > 20)
        {
            return AvailabilityResult.Unavailable(
                "Para grupos de más de 20 personas, por favor llámanos directamente.");
        }

        // In production, check actual availability in database
        // For now, assume available

        return AvailabilityResult.Available(
            $"¡Perfecto! El {FormatDate(date)} a las {booking.Time} hay disponibilidad para {booking.People} personas.");
    }

    private BookingData ExtractBookingData(WhatsAppMessage message, ConversationState? state)
    {
        return new BookingData
        {
            Date = state?.Fecha ?? "",
            Time = state?.Hora ?? "",
            People = state?.Personas ?? 0,
            Name = message.PushName,
            Phone = message.SenderNumber
        };
    }

    private bool TryParseDate(string dateStr, out DateTime date)
    {
        date = default;

        if (string.IsNullOrEmpty(dateStr)) return false;

        // Try dd/MM/yyyy
        if (DateTime.TryParseExact(dateStr, "dd/MM/yyyy",
            null, System.Globalization.DateTimeStyles.None, out date))
        {
            return true;
        }

        // Try d/M/yyyy
        if (DateTime.TryParseExact(dateStr, "d/M/yyyy",
            null, System.Globalization.DateTimeStyles.None, out date))
        {
            return true;
        }

        return false;
    }

    private bool TryParseTime(string timeStr, out TimeSpan time)
    {
        time = default;

        if (string.IsNullOrEmpty(timeStr)) return false;

        // Try HH:mm
        if (TimeSpan.TryParseExact(timeStr, @"hh\:mm", null, out time))
        {
            return true;
        }

        // Try H:mm
        if (TimeSpan.TryParseExact(timeStr, @"h\:mm", null, out time))
        {
            return true;
        }

        return false;
    }

    private string FormatTime(TimeSpan time) =>
        $"{time.Hours}:{time.Minutes:D2}";

    private string FormatDate(DateTime date)
    {
        var days = new[] { "domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado" };
        return $"{days[(int)date.DayOfWeek]} {date.Day}/{date.Month}";
    }
}

/// <summary>
/// Result of availability check.
/// </summary>
public record AvailabilityResult
{
    public bool IsAvailable { get; init; }
    public bool IsSameDay { get; init; }
    public bool IsInvalid { get; init; }
    public string Message { get; init; } = "";
    public string? SuggestedTime { get; init; }

    public static AvailabilityResult Available(string message) => new()
    {
        IsAvailable = true,
        Message = message
    };

    public static AvailabilityResult Unavailable(string message) => new()
    {
        IsAvailable = false,
        Message = message
    };

    public static AvailabilityResult SameDay(string message) => new()
    {
        IsAvailable = false,
        IsSameDay = true,
        Message = message
    };

    public static AvailabilityResult Invalid(string message) => new()
    {
        IsAvailable = false,
        IsInvalid = true,
        Message = message
    };
}
```

## 7.5 Register Specialized Agents

### src/BotGenerator.Api/Program.cs (partial)

```csharp
// Register specialized agents
builder.Services.AddScoped<RiceValidatorAgent>();
builder.Services.AddScoped<DateParserAgent>();
builder.Services.AddScoped<AvailabilityCheckerAgent>();
```

## 7.6 Usage in Intent Router

Specialized agents are typically called from the Intent Router:

```csharp
public class IntentRouterService
{
    private readonly RiceValidatorAgent _riceValidator;
    private readonly AvailabilityCheckerAgent _availabilityChecker;

    public async Task<AgentResponse> HandleBookingAsync(
        AgentResponse mainResponse,
        WhatsAppMessage message,
        ConversationState state)
    {
        // If user mentioned rice, validate it
        if (!string.IsNullOrEmpty(state.ArrozType) &&
            state.ArrozType != "validated")
        {
            var riceResult = await _riceValidator.ValidateAsync(
                state.ArrozType,
                "villacarmen");

            if (!riceResult.IsValid)
            {
                return new AgentResponse
                {
                    Intent = IntentType.Normal,
                    AiResponse = riceResult.Message ?? ""
                };
            }
        }

        // Check availability before creating booking
        if (mainResponse.ExtractedData != null)
        {
            var availability = _availabilityChecker.CheckAvailability(
                mainResponse.ExtractedData);

            if (!availability.IsAvailable)
            {
                return new AgentResponse
                {
                    Intent = IntentType.Normal,
                    AiResponse = availability.Message
                };
            }
        }

        // Proceed with booking
        return mainResponse;
    }
}
```

## Summary

In this step, we created three specialized agents:

| Agent | Purpose | Output |
|-------|---------|--------|
| `RiceValidatorAgent` | Validates rice types | `RiceValidationResult` |
| `DateParserAgent` | Parses natural dates | `DateParseResult` |
| `AvailabilityCheckerAgent` | Checks slot availability | `AvailabilityResult` |

These agents:
- Have focused, specialized prompts
- Return structured results
- Are called mid-flow by the Intent Router
- Can be tested independently

## Next Step

Continue to [Step 08: Intent Router](./08-intent-router.md) where we'll implement the routing logic.
