using System.Text;
using BotGenerator.Core.Models;
using Microsoft.Extensions.Logging;

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
            ["state_fecha"] = (object?)state?.Fecha ?? "",
            ["state_fecha_fullText"] = (object?)state?.FechaFullText ?? "",
            ["state_hora"] = (object?)state?.Hora ?? "",
            ["state_personas"] = (object?)state?.Personas ?? 0,
            ["state_arroz"] = (object?)state?.ArrozType ?? "",
            ["state_raciones"] = (object?)state?.ArrozServings ?? 0,
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
