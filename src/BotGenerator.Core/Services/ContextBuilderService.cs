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
    private readonly RestaurantKnowledgeService? _knowledgeService;

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
        IOpeningHoursService? openingHoursService = null,
        RestaurantKnowledgeService? knowledgeService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _openingHoursService = openingHoursService;
        _knowledgeService = knowledgeService;
    }

    public Dictionary<string, object> BuildContext(
        WhatsAppMessage message,
        ConversationState? state,
        List<ChatMessage>? history,
        List<BookingRecord>? existingBookings = null,
        RestaurantConfig? restaurantConfig = null)
    {
        var now = DateTime.Now;
        var upcomingWeekends = GetUpcomingWeekends();
        var nextBooking = existingBookings?.FirstOrDefault();

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

            // ========== OPENING HOURS (defaults - use BuildContextWithHoursAsync for dynamic values) ==========
            ["openingTime"] = GetDefaultOpeningTime(now.DayOfWeek),
            ["closingTime"] = GetDefaultClosingTime(now.DayOfWeek),
            ["hasDinner"] = false, // Restaurant doesn't typically have dinner service
            ["availableSlots"] = GetDefaultAvailableSlots(now.DayOfWeek),

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
            ["menuUrl"] = restaurantConfig?.MenuUrl ?? "https://alqueriavillacarmen.com/menufindesemana.php",

            // ========== EXISTING BOOKINGS ==========
            ["hasExistingBookings"] = existingBookings?.Count > 0,
            ["existingBookingsCount"] = existingBookings?.Count ?? 0,
            ["existingBookingsSummary"] = FormatExistingBookings(existingBookings),
            ["nextBooking"] = FormatNextBooking(nextBooking),
            ["nextBookingDate"] = nextBooking?.DateFormatted ?? "",
            ["nextBookingTime"] = nextBooking?.TimeFormatted ?? "",
            ["nextBookingPeople"] = nextBooking?.PartySize ?? 0,
            ["nextBookingDayName"] = nextBooking != null ? DaysOfWeek[(int)nextBooking.ReservationDate.DayOfWeek] : "",
            ["nextBookingHasRice"] = !string.IsNullOrEmpty(nextBooking?.ArrozType),
            ["nextBookingRice"] = nextBooking?.ArrozType ?? "Sin arroz",
            ["nextBookingRiceServings"] = nextBooking?.ArrozServings ?? 0,

            // ========== FIRST MESSAGE DETECTION ==========
            ["isFirstMessage"] = (history?.Count ?? 0) == 0,
            ["isGreeting"] = IsGreetingMessage(message.MessageText),

            // ========== MESSAGE INTENT HINTS ==========
            // These help the AI understand user intent without doing full analysis
            ["mentionsRice"] = MentionsRice(message.MessageText),
            ["mentionsModification"] = MentionsModification(message.MessageText),
            ["mentionsCancellation"] = MentionsCancellation(message.MessageText),
            ["asksAboutReservation"] = AsksAboutReservation(message.MessageText),
            ["providesMultipleFields"] = ProvidesMultipleFields(message.MessageText),

            // ========== KNOWLEDGE BASE DATA (async - added in BuildContextWithKnowledgeAsync) ==========
            ["availableRiceTypes"] = "",
            ["relevantPolicies"] = "",
            ["relevantFlowSteps"] = ""
        };

        _logger.LogDebug(
            "Built context with {Count} values for {Customer}, hasExistingBookings={HasBookings}",
            context.Count, message.PushName, existingBookings?.Count > 0);

        return context;
    }

    /// <summary>
    /// Adds knowledge base data to the context asynchronously.
    /// </summary>
    public async Task<Dictionary<string, object>> BuildContextWithKnowledgeAsync(
        Dictionary<string, object> baseContext,
        string? userQuery = null,
        CancellationToken ct = default)
    {
        if (_knowledgeService == null)
        {
            return baseContext;
        }

        try
        {
            // Get rice types
            var riceTypes = await _knowledgeService.GetRiceTypesAsync(ct);
            var riceTypesStr = string.Join(", ", riceTypes);
            
            // Get relevant policies based on user query
            var policies = await _knowledgeService.GetRelevantPoliciesAsync(userQuery, ct);
            var policiesStr = string.Join("\n- ", policies);

            // Get relevant flow steps
            var flowSteps = await _knowledgeService.QueryAsync(userQuery ?? "", "flow_step", 3, ct);
            var flowStepsStr = string.Join("\n", flowSteps.Select(f => f.Content));

            // Update context
            baseContext["availableRiceTypes"] = riceTypesStr;
            baseContext["relevantPolicies"] = $"- {policiesStr}";
            baseContext["relevantFlowSteps"] = flowStepsStr;

            _logger.LogDebug(
                "Added knowledge base data: {RiceCount} rice types, {PolicyCount} policies",
                riceTypes.Count, policies.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load knowledge base data");
        }

        return baseContext;
    }

    /// <summary>
    /// Checks if message mentions rice/paella.
    /// </summary>
    private static bool MentionsRice(string text)
    {
        var t = text.ToLowerInvariant();
        var riceKeywords = new[] { "arroz", "paella", "fideuá", "fideua", "raciones" };
        return riceKeywords.Any(k => t.Contains(k));
    }

    /// <summary>
    /// Checks if message mentions modification intent.
    /// </summary>
    private static bool MentionsModification(string text)
    {
        var t = text.ToLowerInvariant();
        return t.Contains("modificar") || t.Contains("cambiar") || t.Contains("añadir") || 
               t.Contains("agregar") || t.Contains("quitar") || t.Contains("mi reserva");
    }

    /// <summary>
    /// Checks if message mentions cancellation intent.
    /// </summary>
    private static bool MentionsCancellation(string text)
    {
        var t = text.ToLowerInvariant();
        return t.Contains("cancelar") || t.Contains("anular") || t.Contains("eliminar reserva");
    }

    /// <summary>
    /// Checks if user is asking about their reservation.
    /// </summary>
    private static bool AsksAboutReservation(string text)
    {
        var t = text.ToLowerInvariant();
        return t.Contains("tengo reserva") || t.Contains("mi reserva") || t.Contains("mis reservas") ||
               t.Contains("he reservado") || t.Contains("reservé");
    }

    /// <summary>
    /// Checks if user provides multiple booking fields in one message.
    /// </summary>
    private static bool ProvidesMultipleFields(string text)
    {
        var t = text.ToLowerInvariant();
        int fieldCount = 0;
        
        // Date indicators
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"\b(lunes|martes|miércoles|miercoles|jueves|viernes|sábado|sabado|domingo|mañana|pasado)\b") ||
            System.Text.RegularExpressions.Regex.IsMatch(t, @"\d{1,2}[/-]\d{1,2}"))
            fieldCount++;
        
        // Time indicators
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"\b\d{1,2}:\d{2}\b") ||
            System.Text.RegularExpressions.Regex.IsMatch(t, @"\ba las \d"))
            fieldCount++;
        
        // People indicators
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"\b\d+\s*(personas|comensales|pax)\b") ||
            System.Text.RegularExpressions.Regex.IsMatch(t, @"\bsomos\s+\d+\b"))
            fieldCount++;
        
        return fieldCount >= 2;
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

    /// <summary>
    /// Formats existing bookings for display in the prompt.
    /// </summary>
    private string FormatExistingBookings(List<BookingRecord>? bookings)
    {
        if (bookings == null || bookings.Count == 0)
            return "No tiene reservas activas.";

        var sb = new StringBuilder();
        foreach (var b in bookings)
        {
            var dayName = DaysOfWeek[(int)b.ReservationDate.DayOfWeek];
            var rice = string.IsNullOrEmpty(b.ArrozType)
                ? "Sin arroz"
                : $"{b.ArrozType} ({b.ArrozServings} raciones)";
            sb.AppendLine($"- {dayName} {b.DateFormatted} a las {b.TimeFormatted}: {b.PartySize} personas | {rice}");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Formats a single booking for display.
    /// </summary>
    private string FormatNextBooking(BookingRecord? booking)
    {
        if (booking == null) return "";

        var dayName = DaysOfWeek[(int)booking.ReservationDate.DayOfWeek];
        var rice = string.IsNullOrEmpty(booking.ArrozType)
            ? "sin arroz"
            : $"con {booking.ArrozType} ({booking.ArrozServings} raciones)";
        return $"{dayName} {booking.DateFormatted} a las {booking.TimeFormatted} para {booking.PartySize} personas, {rice}";
    }

    /// <summary>
    /// Checks if the message is a greeting.
    /// </summary>
    private static bool IsGreetingMessage(string text)
    {
        var greetings = new[] 
        { 
            "hola", "ola", "buenos días", "buenos dias", "buenas tardes", "buenas noches", 
            "buenas", "hey", "qué tal", "que tal", "ey", "wenas", "hi", "hello",
            "buenas!", "hola!", "saludos", "holaa", "holaaa", "eyyy", "eyy"
        };
        var lower = text.ToLowerInvariant().Trim();
        
        // Check exact match or starts with greeting
        if (greetings.Any(g => lower == g || lower.StartsWith(g + " ") || lower.StartsWith(g + ",") || lower.StartsWith(g + "!")))
            return true;
        
        // Short messages that are just greetings with punctuation
        if (lower.Length <= 15 && greetings.Any(g => lower.Contains(g)))
            return true;
            
        return false;
    }

    /// <summary>
    /// Gets default opening time for a day of the week.
    /// </summary>
    private static string GetDefaultOpeningTime(DayOfWeek day)
    {
        // Restaurant always opens at 13:30
        return "13:30";
    }

    /// <summary>
    /// Gets default closing time for a day of the week.
    /// </summary>
    private static string GetDefaultClosingTime(DayOfWeek day)
    {
        return day switch
        {
            DayOfWeek.Monday or DayOfWeek.Tuesday or DayOfWeek.Wednesday or DayOfWeek.Thursday => "17:00",
            DayOfWeek.Friday => "17:30",
            DayOfWeek.Saturday or DayOfWeek.Sunday => "18:00",
            _ => "17:00"
        };
    }

    /// <summary>
    /// Gets default available time slots for a day of the week.
    /// </summary>
    private static string GetDefaultAvailableSlots(DayOfWeek day)
    {
        return day switch
        {
            DayOfWeek.Saturday or DayOfWeek.Sunday => "13:30, 14:00, 14:30, 15:00, 15:30, 16:00",
            DayOfWeek.Friday => "13:30, 14:00, 14:30, 15:00, 15:30",
            _ => "13:30, 14:00, 14:30, 15:00"
        };
    }

    #endregion

    public async Task<Dictionary<string, object>> BuildContextWithHoursAsync(
        WhatsAppMessage message,
        ConversationState? state,
        List<ChatMessage>? history,
        DateTime targetDate,
        List<BookingRecord>? existingBookings = null,
        RestaurantConfig? restaurantConfig = null,
        CancellationToken ct = default)
    {
        var context = BuildContext(message, state, history, existingBookings, restaurantConfig);

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
