using System.Text.RegularExpressions;
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
