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
    private readonly IOpeningHoursService? _openingHoursService;

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

    // Default schedules - all days show hours, actual open/closed is in restaurant_days DB table
    private static readonly Dictionary<DayOfWeek, string> DefaultSchedule = new()
    {
        { DayOfWeek.Monday, "13:30 – 17:00" },
        { DayOfWeek.Tuesday, "13:30 – 17:00" },
        { DayOfWeek.Wednesday, "13:30 – 17:00" },
        { DayOfWeek.Thursday, "13:30 – 17:00" },
        { DayOfWeek.Friday, "13:30 – 17:30" },
        { DayOfWeek.Saturday, "13:30 – 18:00" },
        { DayOfWeek.Sunday, "13:30 – 18:00" }
    };

    public ContextBuilderService(
        ILogger<ContextBuilderService> logger,
        IOpeningHoursService? openingHoursService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _openingHoursService = openingHoursService;
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
            ["schedule_cerrado"] = "Ver disponibilidad en restaurant_days",

            // ========== BOOKING STATE ==========
            ["state_fecha"] = (object?)state?.Fecha ?? "",
            ["state_fecha_fullText"] = (object?)state?.FechaFullText ?? "",
            ["state_hora"] = (object?)state?.Hora ?? "",
            ["state_personas"] = (object?)state?.Personas ?? 0,
            // Rice: null = not decided; "" = decided (no rice)
            ["state_arroz_decided"] = state?.ArrozType != null,
            ["state_arroz_hasRice"] = !string.IsNullOrEmpty(state?.ArrozType),
            ["state_arroz_value"] = state?.ArrozType == null
                ? ""
                : (string.IsNullOrEmpty(state.ArrozType) ? "Sin arroz" : state.ArrozType),
            ["state_raciones"] = (object?)state?.ArrozServings ?? 0,
            ["state_raciones_needed"] = state?.ArrozType != null && !string.IsNullOrEmpty(state.ArrozType),
            ["state_raciones_decided"] = state?.ArrozType != null && !string.IsNullOrEmpty(state.ArrozType) && state.ArrozServings.HasValue,

            // Extras:
            // - null => not answered
            // - -1   => user said yes but count missing
            // - >=0  => final count
            ["state_tronas_answered"] = state?.HighChairs.HasValue == true && state.HighChairs.Value >= 0,
            ["state_tronas_needsCount"] = state?.HighChairs.HasValue == true && state.HighChairs.Value < 0,
            ["state_tronas_value"] = (state?.HighChairs.HasValue == true && state.HighChairs.Value >= 0) ? state.HighChairs.Value : 0,

            ["state_carritos_answered"] = state?.BabyStrollers.HasValue == true && state.BabyStrollers.Value >= 0,
            ["state_carritos_needsCount"] = state?.BabyStrollers.HasValue == true && state.BabyStrollers.Value < 0,
            ["state_carritos_value"] = (state?.BabyStrollers.HasValue == true && state.BabyStrollers.Value >= 0) ? state.BabyStrollers.Value : 0,
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

        // Default: assume open - actual status checked via restaurant_days DB table
        return true;
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
            : "13:30 – 17:00";
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

    public async Task<Dictionary<string, object>> BuildContextWithHoursAsync(
        WhatsAppMessage message,
        ConversationState? state,
        List<ChatMessage>? history,
        DateTime targetDate,
        RestaurantConfig? restaurantConfig = null,
        CancellationToken ct = default)
    {
        var context = BuildContext(message, state, history, restaurantConfig);

        // Add dynamic opening hours if service is available
        if (_openingHoursService != null)
        {
            try
            {
                var hours = await _openingHoursService.GetContextAwareHoursAsync(targetDate, ct);

                context["openingTime"] = hours.OpeningTimeFormatted;
                context["closingTime"] = hours.ClosingTimeFormatted;
                context["hasDinner"] = hours.HasDinner;
                context["hasLunch"] = hours.HasLunch;
                context["availableSlots"] = string.Join(", ", hours.AvailableSlots);
                context["hoursFromDatabase"] = hours.IsFromDatabase;

                _logger.LogDebug(
                    "Added dynamic hours to context: {Opening}-{Closing}, HasDinner={HasDinner}",
                    hours.OpeningTimeFormatted, hours.ClosingTimeFormatted, hours.HasDinner);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get dynamic opening hours, using defaults");
                // Use fallback defaults
                context["openingTime"] = "13:30";
                context["closingTime"] = "18:00";
                context["hasDinner"] = false;
                context["hasLunch"] = true;
                context["availableSlots"] = "13:30, 14:00, 15:00, 15:30";
                context["hoursFromDatabase"] = false;
            }
        }
        else
        {
            // No service available, use static defaults
            context["openingTime"] = "13:30";
            context["closingTime"] = "18:00";
            context["hasDinner"] = false;
            context["hasLunch"] = true;
            context["availableSlots"] = "13:30, 14:00, 15:00, 15:30";
            context["hoursFromDatabase"] = false;
        }

        return context;
    }
}
