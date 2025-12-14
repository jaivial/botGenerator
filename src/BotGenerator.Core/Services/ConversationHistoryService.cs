using BotGenerator.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace BotGenerator.Core.Services;

/// <summary>
/// In-memory implementation of conversation history service.
/// In production, use Redis or a database.
/// </summary>
public class ConversationHistoryService : IConversationHistoryService
{
    private readonly Dictionary<string, List<ChatMessage>> _history = new();
    private readonly IContextBuilderService _contextBuilder;
    private readonly IAiStateExtractorService? _aiStateExtractor;
    private readonly ILogger<ConversationHistoryService> _logger;
    private readonly int _maxMessages;
    private readonly TimeSpan _sessionTimeout;

    private readonly Dictionary<string, DateTime> _lastActivity = new();

    public ConversationHistoryService(
        IContextBuilderService contextBuilder,
        IConfiguration configuration,
        ILogger<ConversationHistoryService> logger,
        IAiStateExtractorService? aiStateExtractor = null)
    {
        _contextBuilder = contextBuilder;
        _aiStateExtractor = aiStateExtractor;
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
        // Check if date was rejected by the bot (no availability)
        var invalidFecha = !string.IsNullOrEmpty(fecha) && WasDateRejected(history);
        // Extract time
        var hora = ExtractTime(history);
        // Check if time was rejected by the bot (outside opening hours)
        var invalidHora = !string.IsNullOrEmpty(hora) && WasTimeRejected(history);
        // Extract people count
        var personas = ExtractPeople(history);
        // Extract rice info
        var (arrozType, arrozServings) = ExtractRiceInfo(history);
        // Extract high chairs and strollers (0 is a valid answer, null means unknown)
        var highChairs = ExtractHighChairs(history);
        var babyStrollers = ExtractBabyStrollers(history);

        // Determine what's missing
        // If date was rejected (no availability), treat it as missing so user is asked again
        if (string.IsNullOrEmpty(fecha) || invalidFecha) missingData.Add("fecha");
        // If time was rejected, treat it as missing so user is asked again
        if (string.IsNullOrEmpty(hora) || invalidHora) missingData.Add("hora");
        if (!personas.HasValue) missingData.Add("personas");
        if (arrozType == null && !WasAskedAboutRice(history)) missingData.Add("arroz_decision");
        if (arrozType != null && !string.IsNullOrEmpty(arrozType) && !arrozServings.HasValue) missingData.Add("arroz_servings");
        // Enforce minimum rice servings of 2 when rice is ordered
        if (arrozType != null && !string.IsNullOrEmpty(arrozType) && arrozServings.HasValue && arrozServings.Value < 2)
            missingData.Add("arroz_servings_min2");

        // Always ask for these extras
        // - null => decision missing (yes/no)
        // - -1  => user said yes but count missing
        // - >=0 => final count (0..N) captured (validated later)
        if (!highChairs.HasValue) missingData.Add("tronas");
        else if (highChairs.Value < 0) missingData.Add("tronas_count");

        if (!babyStrollers.HasValue) missingData.Add("carritos");
        else if (babyStrollers.Value < 0) missingData.Add("carritos_count");

        var isComplete = missingData.Count == 0;

        return new ConversationState
        {
            Fecha = invalidFecha ? null : fecha, // Clear invalid date so AI asks again
            InvalidFecha = invalidFecha,
            Hora = invalidHora ? null : hora, // Clear invalid time so AI asks again
            InvalidHora = invalidHora,
            Personas = personas,
            ArrozType = arrozType,
            ArrozServings = arrozServings,
            HighChairs = highChairs,
            BabyStrollers = babyStrollers,
            MissingData = missingData,
            IsComplete = isComplete,
            Stage = isComplete ? "awaiting_confirmation" : "collecting_info"
        };
    }

    /// <summary>
    /// Extracts booking state using AI (Gemini) - more robust than regex.
    /// Falls back to regex-based extraction if AI service is not available.
    /// </summary>
    public async Task<ConversationState> ExtractStateWithAiAsync(
        List<ChatMessage> history,
        CancellationToken cancellationToken = default)
    {
        if (_aiStateExtractor == null)
        {
            _logger.LogWarning("AI state extractor not available, falling back to regex extraction");
            return ExtractState(history);
        }

        try
        {
            var aiState = await _aiStateExtractor.ExtractStateAsync(history, cancellationToken);
            _logger.LogInformation("AI extraction complete: {State}", aiState);
            return aiState;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI state extraction failed, falling back to regex extraction");
            return ExtractState(history);
        }
    }

    #region Extraction Methods

    private string? ExtractDate(List<ChatMessage> history)
    {
        var upcomingWeekends = _contextBuilder.GetUpcomingWeekends();

        // Spanish month names to number mapping
        var monthNames = new Dictionary<string, int>
        {
            ["enero"] = 1, ["febrero"] = 2, ["marzo"] = 3, ["abril"] = 4,
            ["mayo"] = 5, ["junio"] = 6, ["julio"] = 7, ["agosto"] = 8,
            ["septiembre"] = 9, ["octubre"] = 10, ["noviembre"] = 11, ["diciembre"] = 12
        };

        for (int i = history.Count - 1; i >= 0; i--)
        {
            var msg = history[i];
            if (msg.Role != "user") continue;

            var text = msg.Content.ToLower();

            // Check for "21 de diciembre" format (with or without year)
            var spanishDateMatch = Regex.Match(text, @"(\d{1,2})\s+de\s+(enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|octubre|noviembre|diciembre)(?:\s+(?:de\s+)?(\d{4}))?", RegexOptions.IgnoreCase);
            if (spanishDateMatch.Success)
            {
                var day = int.Parse(spanishDateMatch.Groups[1].Value);
                var monthName = spanishDateMatch.Groups[2].Value.ToLower();
                var year = spanishDateMatch.Groups[3].Success
                    ? int.Parse(spanishDateMatch.Groups[3].Value)
                    : DateTime.Now.Year;

                if (monthNames.TryGetValue(monthName, out var month))
                {
                    // If the date is in the past this year, assume next year
                    var candidateDate = new DateTime(year, month, day);
                    if (candidateDate < DateTime.Now.Date && !spanishDateMatch.Groups[3].Success)
                    {
                        candidateDate = candidateDate.AddYears(1);
                    }
                    return candidateDate.ToString("dd/MM/yyyy");
                }
            }

            // Check for day names (domingo, sábado, viernes, jueves)
            var dayMatch = Regex.Match(text, @"(?:para\s+el|el|para)\s+(domingo|sábado|viernes|jueves)");
            if (dayMatch.Success)
            {
                var dayName = dayMatch.Groups[1].Value;
                // Try to find in upcoming weekends first
                var weekend = upcomingWeekends.FirstOrDefault(
                    w => w.DayName == dayName);
                if (weekend != null)
                {
                    return weekend.Formatted;
                }
                // Otherwise calculate next occurrence of this day
                var targetDay = dayName switch
                {
                    "domingo" => DayOfWeek.Sunday,
                    "sábado" => DayOfWeek.Saturday,
                    "viernes" => DayOfWeek.Friday,
                    "jueves" => DayOfWeek.Thursday,
                    _ => DayOfWeek.Sunday
                };
                var today = DateTime.Now.Date;
                var daysUntil = ((int)targetDay - (int)today.DayOfWeek + 7) % 7;
                if (daysUntil == 0) daysUntil = 7; // Next week if today
                var nextDate = today.AddDays(daysUntil);
                return nextDate.ToString("dd/MM/yyyy");
            }

            // Check for explicit date (dd/mm/yyyy or dd-mm-yyyy)
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

            // Try pattern with "a las", "para las"
            var match = Regex.Match(msg.Content,
                @"(?:a\s+las?|para\s+las?)\s*(\d{1,2}):?(\d{2})?",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var hour = match.Groups[1].Value;
                var minute = match.Groups[2].Success ? match.Groups[2].Value : "00";
                return $"{hour}:{minute}";
            }

            // Try pattern with just "las" (e.g., "Vale, las 14:00")
            match = Regex.Match(msg.Content,
                @"\blas?\s+(\d{1,2}):(\d{2})",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var hour = match.Groups[1].Value;
                var minute = match.Groups[2].Value;
                return $"{hour}:{minute}";
            }

            // Try pattern with just time (e.g., "14:00" or "1400")
            match = Regex.Match(msg.Content, @"\b(\d{1,2}):(\d{2})\b");
            if (match.Success)
            {
                var hour = match.Groups[1].Value;
                var minute = match.Groups[2].Value;
                return $"{hour}:{minute}";
            }
        }

        return null;
    }

    private int? ExtractPeople(List<ChatMessage> history)
    {
        for (int i = history.Count - 1; i >= 0; i--)
        {
            var msg = history[i];
            if (msg.Role != "user") continue;

            // Prefer explicit mentions of "personas" to avoid misreading counts for tronas/carritos.
            var match = Regex.Match(msg.Content, @"(?:para|somos)\s+(\d+)\s*personas?", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var count1))
                return count1;

            match = Regex.Match(msg.Content, @"(\d+)\s*personas?", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var count2))
                return count2;

            // Plain numeric answer only counts as people if it was asked in the immediately previous assistant message.
            if (i > 0 && WasAskedAboutPeople(history[i - 1]))
            {
                var text = msg.Content.Trim();
                if (Regex.IsMatch(text, @"^\d+$") && int.TryParse(text, out var numeric))
                    return numeric;
            }
        }

        return null;
    }

    private bool WasAskedAboutPeople(ChatMessage assistantMsg)
    {
        return assistantMsg.Role == "assistant" &&
               Regex.IsMatch(assistantMsg.Content, @"personas?|cu[aá]ntos\s+sois|cuantas\s+sois", RegexOptions.IgnoreCase);
    }

    private (string? Type, int? Servings) ExtractRiceInfo(List<ChatMessage> history)
    {
        string? riceType = null;
        int? servings = null;

        // Look for rice validation confirmation from AI
        // Priority 1: "Has elegido" pattern (from multiple options selection)
        for (int i = history.Count - 1; i >= 0; i--)
        {
            var msg = history[i];
            if (msg.Role != "assistant") continue;

            // Pattern for when user selects from multiple options: "Has elegido la Paella valenciana de la Albufera"
            var elegidoMatch = Regex.Match(msg.Content, @"Has elegido (?:la |el )?(.+?)(?:\.|¿|$)", RegexOptions.IgnoreCase);
            if (elegidoMatch.Success)
            {
                riceType = elegidoMatch.Groups[1].Value.Trim();
                break;
            }

            // Pattern for single match: "✅ Paella valenciana disponible"
            if (Regex.IsMatch(msg.Content, @"✅.*disponible", RegexOptions.IgnoreCase))
            {
                var match = Regex.Match(msg.Content, @"✅\s*(.+?)\s+disponible");
                if (match.Success)
                {
                    riceType = match.Groups[1].Value.Trim();
                    break;
                }
            }
        }

        // Look for "no rice" response - check ANY user message that rejects rice
        for (int i = history.Count - 1; i >= 0 && riceType == null; i--)
        {
            var msg = history[i];
            if (msg.Role != "user") continue;

            // Handle various "no rice" patterns anywhere in conversation
            // These patterns explicitly reject rice, regardless of context
            if (Regex.IsMatch(msg.Content,
                @"(no\s+queremos\s+arroz|sin\s+arroz|nada\s+de\s+arroz|no\s+quiero\s+arroz)",
                RegexOptions.IgnoreCase))
            {
                riceType = ""; // Empty string means "no rice"
                break;
            }

            // Also check for simple "no" or "nada" if previous message asked about rice
            if (i > 0 &&
                history[i - 1].Role == "assistant" &&
                Regex.IsMatch(history[i - 1].Content, @"queréis arroz", RegexOptions.IgnoreCase))
            {
                if (Regex.IsMatch(msg.Content, @"^(no|nada|no\s+gracias)$", RegexOptions.IgnoreCase))
                {
                    riceType = ""; // Empty string means "no rice"
                    break;
                }
            }
        }

        // Look for rice type directly in user messages (when user specifies rice + servings)
        // This catches cases like "paella valenciana, 3 raciones" or "arroz negro, 2 raciones"
        if (riceType == null)
        {
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var msg = history[i];
                if (msg.Role != "user") continue;

                // Pattern 1: "arroz [type], N raciones" or "arroz de [type], N raciones"
                var match = Regex.Match(msg.Content,
                    @"arroz\s+(?:del?\s+)?([a-záéíóúñ\s]+?)\s*,?\s*\d+\s*raciones?",
                    RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var extracted = match.Groups[1].Value.Trim();
                    // Filter out non-rice words
                    if (!IsNonRiceWord(extracted))
                    {
                        riceType = "Arroz " + extracted;
                        break;
                    }
                }

                // Pattern 2: "paella [type], N raciones"
                match = Regex.Match(msg.Content,
                    @"paella\s+([a-záéíóúñ\s]+?)\s*,?\s*\d+\s*raciones?",
                    RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var extracted = match.Groups[1].Value.Trim();
                    if (!IsNonRiceWord(extracted))
                    {
                        riceType = "Paella " + extracted;
                        break;
                    }
                }

                // Pattern 3: Just "paella valenciana" or "arroz negro" without servings
                // but only if servings are mentioned somewhere in conversation
                match = Regex.Match(msg.Content,
                    @"(?:solo\s+)?(?:quiero|queremos|pues)?\s*(paella\s+[a-záéíóúñ]+|arroz\s+(?:del?\s+)?[a-záéíóúñ\s]+?)(?:\s*$|[,.])",
                    RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var extracted = match.Groups[1].Value.Trim();
                    // Additional filter: reject if too generic
                    if (extracted.Length > 5 && !IsNonRiceWord(extracted))
                    {
                        riceType = char.ToUpper(extracted[0]) + extracted.Substring(1).ToLower();
                        break;
                    }
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

    private static bool IsNonRiceWord(string text)
    {
        var invalidWords = new[] { "gracias", "por favor", "porfa", "porfavor", "vale", "ok", "si", "no", "entonces" };
        return invalidWords.Contains(text.ToLower().Trim());
    }

    private bool WasAskedAboutRice(List<ChatMessage> history)
    {
        return history.Any(m =>
            m.Role == "assistant" &&
            Regex.IsMatch(m.Content, @"queréis arroz", RegexOptions.IgnoreCase));
    }

    /// <summary>
    /// Checks if the bot has rejected the user's time choice (e.g., outside opening hours).
    /// Looks for recent bot messages indicating time rejection after a time was extracted.
    /// </summary>
    private bool WasTimeRejected(List<ChatMessage> history)
    {
        // Find the index of the last user message containing a time
        int lastTimeMessageIndex = -1;
        for (int i = history.Count - 1; i >= 0; i--)
        {
            var msg = history[i];
            if (msg.Role != "user") continue;

            // Check if this message contains a time pattern
            if (Regex.IsMatch(msg.Content, @"\b\d{1,2}:\d{2}\b|\ba\s+las?\s+\d{1,2}", RegexOptions.IgnoreCase))
            {
                lastTimeMessageIndex = i;
                break;
            }
        }

        if (lastTimeMessageIndex < 0) return false;

        // Check if any bot message AFTER the time message indicates rejection
        for (int i = lastTimeMessageIndex + 1; i < history.Count; i++)
        {
            var msg = history[i];
            if (msg.Role != "assistant") continue;

            // Patterns indicating time was rejected
            if (Regex.IsMatch(msg.Content, @"cerramos\s+a\s+las", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(msg.Content, @"no\s+podemos.*esa\s+hora", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(msg.Content, @"hora.*no.*disponible", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(msg.Content, @"fuera\s+de\s+horario", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(msg.Content, @"hora\s+más\s+temprano", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(msg.Content, @"no\s+tenemos\s+hueco", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(msg.Content, @"ya\s+no\s+tenemos\s+hueco", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(msg.Content, @"no\s+hay\s+disponibilidad", RegexOptions.IgnoreCase))
            {
                // Check if user has provided a NEW valid time after this rejection
                // by looking for a user time message after this rejection
                bool hasNewerValidTime = false;
                for (int j = i + 1; j < history.Count; j++)
                {
                    var laterMsg = history[j];
                    if (laterMsg.Role == "user" &&
                        Regex.IsMatch(laterMsg.Content, @"\b\d{1,2}:\d{2}\b|\ba\s+las?\s+\d{1,2}", RegexOptions.IgnoreCase))
                    {
                        // Check if THIS time was also rejected
                        bool thisTimeRejected = false;
                        for (int k = j + 1; k < history.Count; k++)
                        {
                            if (history[k].Role == "assistant" &&
                                (Regex.IsMatch(history[k].Content, @"cerramos\s+a\s+las", RegexOptions.IgnoreCase) ||
                                 Regex.IsMatch(history[k].Content, @"hora\s+más\s+temprano", RegexOptions.IgnoreCase) ||
                                 Regex.IsMatch(history[k].Content, @"no\s+tenemos\s+hueco", RegexOptions.IgnoreCase) ||
                                 Regex.IsMatch(history[k].Content, @"ya\s+no\s+tenemos\s+hueco", RegexOptions.IgnoreCase) ||
                                 Regex.IsMatch(history[k].Content, @"no\s+hay\s+disponibilidad", RegexOptions.IgnoreCase)))
                            {
                                thisTimeRejected = true;
                                break;
                            }
                        }

                        if (!thisTimeRejected)
                        {
                            hasNewerValidTime = true;
                            break;
                        }
                    }
                }

                // If most recent time was rejected and no newer valid time exists
                if (!hasNewerValidTime)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if the bot has rejected the user's date choice (e.g., no availability for party size).
    /// Looks for recent bot messages indicating date/capacity rejection.
    /// </summary>
    private bool WasDateRejected(List<ChatMessage> history)
    {
        // Find the index of the last user message containing a date
        int lastDateMessageIndex = -1;
        for (int i = history.Count - 1; i >= 0; i--)
        {
            var msg = history[i];
            if (msg.Role != "user") continue;

            // Check if this message contains a date pattern (day names, dates, relative dates)
            if (Regex.IsMatch(msg.Content, @"\b(lunes|martes|miércoles|miercoles|jueves|viernes|sábado|sabado|domingo)\b", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(msg.Content, @"\b\d{1,2}[/-]\d{1,2}([/-]\d{2,4})?\b") ||
                Regex.IsMatch(msg.Content, @"\b(mañana|pasado\s+mañana|próximo|proximo|que\s+viene)\b", RegexOptions.IgnoreCase))
            {
                lastDateMessageIndex = i;
                break;
            }
        }

        if (lastDateMessageIndex < 0) return false;

        // Check if any bot message AFTER the date message indicates rejection
        for (int i = lastDateMessageIndex + 1; i < history.Count; i++)
        {
            var msg = history[i];
            if (msg.Role != "assistant") continue;

            // Patterns indicating date/capacity was rejected
            if (Regex.IsMatch(msg.Content, @"no\s+tenemos\s+sitio\s+para", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(msg.Content, @"ya\s+no\s+tenemos\s+sitio", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(msg.Content, @"ese\s+día.*no\s+tenemos\s+hueco", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(msg.Content, @"está\s+completo\s+para", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(msg.Content, @"ese\s+día\s+está\s+completo", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(msg.Content, @"no\s+hay\s+disponibilidad.*ese\s+día", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(msg.Content, @"te\s+viene\s+bien\s+(el|otra)", RegexOptions.IgnoreCase)) // Alternative date suggestion
            {
                // Check if user has accepted the alternative date
                bool hasAcceptedAlternative = false;
                for (int j = i + 1; j < history.Count; j++)
                {
                    var laterMsg = history[j];
                    if (laterMsg.Role == "user")
                    {
                        // Check if user accepted (sí, vale, perfecto, de acuerdo)
                        if (Regex.IsMatch(laterMsg.Content, @"^\s*(sí|si|vale|ok|perfecto|de\s+acuerdo|bien)\s*$", RegexOptions.IgnoreCase))
                        {
                            hasAcceptedAlternative = true;
                            break;
                        }
                        // Check if user provided a NEW date
                        if (Regex.IsMatch(laterMsg.Content, @"\b(lunes|martes|miércoles|miercoles|jueves|viernes|sábado|sabado|domingo)\b", RegexOptions.IgnoreCase) ||
                            Regex.IsMatch(laterMsg.Content, @"\b\d{1,2}[/-]\d{1,2}([/-]\d{2,4})?\b"))
                        {
                            // Check if THIS date was also rejected
                            bool thisDateRejected = false;
                            for (int k = j + 1; k < history.Count; k++)
                            {
                                if (history[k].Role == "assistant" &&
                                    (Regex.IsMatch(history[k].Content, @"no\s+tenemos\s+sitio\s+para", RegexOptions.IgnoreCase) ||
                                     Regex.IsMatch(history[k].Content, @"ya\s+no\s+tenemos\s+sitio", RegexOptions.IgnoreCase) ||
                                     Regex.IsMatch(history[k].Content, @"está\s+completo\s+para", RegexOptions.IgnoreCase) ||
                                     Regex.IsMatch(history[k].Content, @"te\s+viene\s+bien\s+(el|otra)", RegexOptions.IgnoreCase)))
                                {
                                    thisDateRejected = true;
                                    break;
                                }
                            }

                            if (!thisDateRejected)
                            {
                                hasAcceptedAlternative = true;
                                break;
                            }
                        }
                    }
                }

                // If date was rejected and user hasn't accepted alternative
                if (!hasAcceptedAlternative)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private int? ExtractHighChairs(List<ChatMessage> history)
    {
        // Look from newest to oldest
        for (int i = history.Count - 1; i >= 0; i--)
        {
            var msg = history[i];
            if (msg.Role != "user") continue;

            var text = msg.Content.ToLowerInvariant();

            // Explicit no
            if (Regex.IsMatch(text, @"\b(sin\s+trona(s)?|no\s+necesitamos\s+trona(s)?|ninguna\s+trona)\b", RegexOptions.IgnoreCase))
            {
                return 0;
            }

            // Count
            var match = Regex.Match(text, @"(\d+)\s*tronas?", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
            {
                // Do NOT clamp here: we need to detect >3 to respond naturally ("máximo 3")
                return count;
            }

            // Mention without count -> needs follow-up ("¿Cuántas?")
            if (text.Contains("trona"))
            {
                return -1;
            }

            // Contextual answers after being asked
            if (i > 0 && WasAskedAboutHighChairs(history[i - 1]))
            {
                // Flexible "no" patterns: "no", "no, ninguna", "ninguna", "nada", "0", "no gracias", etc.
                if (Regex.IsMatch(text, @"^(no\s*,?\s*)?(nada|ninguna?|0)(\s*,?\s*no)?$", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(text, @"^no(\s+gracias)?$", RegexOptions.IgnoreCase))
                    return 0;

                // Plain "sí" => needs count
                if (Regex.IsMatch(text, @"^(sí|si)(\s+gracias)?$", RegexOptions.IgnoreCase))
                    return -1;

                // Plain number after question => count
                if (Regex.IsMatch(text, @"^\d+$") && int.TryParse(text, out var numeric))
                    return numeric;
            }
        }

        return null; // not answered yet
    }

    private int? ExtractBabyStrollers(List<ChatMessage> history)
    {
        for (int i = history.Count - 1; i >= 0; i--)
        {
            var msg = history[i];
            if (msg.Role != "user") continue;

            var text = msg.Content.ToLowerInvariant();

            // Explicit no
            if (Regex.IsMatch(text, @"\b(sin\s+carrito(s)?|no\s+traemos\s+carrito(s)?|no\s+traemos\s+cochecito(s)?|ningún\s+carrito|ningun\s+carrito)\b", RegexOptions.IgnoreCase))
            {
                return 0;
            }

            // Count
            var match = Regex.Match(text, @"(\d+)\s*(carritos?|cochecitos?)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
            {
                return count;
            }

            // Mention without count -> needs follow-up ("¿Cuántos?")
            if (text.Contains("carrito") || text.Contains("cochecito"))
            {
                return -1;
            }

            // Contextual answers after being asked
            if (i > 0 && WasAskedAboutStrollers(history[i - 1]))
            {
                // Flexible "no" patterns: "no", "no, ninguno", "ninguno", "nada", "0", "no gracias", etc.
                if (Regex.IsMatch(text, @"^(no\s*,?\s*)?(nada|ninguno?|ninguna?|0)(\s*,?\s*no)?$", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(text, @"^no(\s+gracias)?$", RegexOptions.IgnoreCase))
                    return 0;

                // Plain "sí" => needs count
                if (Regex.IsMatch(text, @"^(sí|si)(\s+gracias)?$", RegexOptions.IgnoreCase))
                    return -1;

                // Plain number after question => count
                if (Regex.IsMatch(text, @"^\d+$") && int.TryParse(text, out var numeric))
                    return numeric;
            }
        }

        return null;
    }

    private bool WasAskedAboutHighChairs(ChatMessage assistantMsg)
    {
        if (assistantMsg.Role != "assistant") return false;

        // Must be a QUESTION specifically asking about tronas count or need
        // NOT just any message that mentions "trona" and has a "?" somewhere
        // Valid patterns:
        // - "¿Necesitáis tronas?" / "¿Cuántas tronas?"
        // - "tronas?" at end of message (asking about tronas)
        // - "¿Y cuántas tronas serían?"
        return Regex.IsMatch(assistantMsg.Content,
            @"(¿[^?]*trona|cu[aá]ntas?\s+tronas?|necesit[aá]is?\s+tronas?|tronas?\s*\?\s*$)",
            RegexOptions.IgnoreCase);
    }

    private bool WasAskedAboutStrollers(ChatMessage assistantMsg)
    {
        if (assistantMsg.Role != "assistant") return false;

        // Must be a QUESTION specifically asking about carritos/cochecitos
        // NOT just any message that mentions "carrito" and has a "?" somewhere
        // Valid patterns:
        // - "¿Vais a traer carrito?" / "¿Traéis carrito?"
        // - "carrito?" at end of message
        // - "¿Y cochecito de bebé?"
        return Regex.IsMatch(assistantMsg.Content,
            @"(¿[^?]*carrito|tra[eé]is?\s+carritos?|vais\s+a\s+traer\s+carrito|carritos?\s*\?\s*$|¿[^?]*cochecito|cochecitos?\s*\?\s*$)",
            RegexOptions.IgnoreCase);
    }

    #endregion
}
