using System.Globalization;
using System.Text.RegularExpressions;
using BotGenerator.Core.Agents;
using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Parses natural language messages to extract booking modification fields.
/// Supports Spanish expressions and combined date+time patterns.
/// </summary>
public interface INaturalLanguageModificationParser
{
    /// <summary>
    /// Extracts all possible booking fields from a natural language message.
    /// Returns a dictionary of field name → extracted value.
    /// </summary>
    Dictionary<string, object> ExtractFields(string userMessage, ModificationState state);

    /// <summary>
    /// Detects if the user is making a correction (e.g., "no, es para el domingo").
    /// </summary>
    bool IsCorrection(string userMessage);

    /// <summary>
    /// Infers the user's goal from the message (change_date, change_time, change_both, etc.).
    /// </summary>
    string? InferUserGoal(string userMessage, ModificationState state);
}

public class NaturalLanguageModificationParser : INaturalLanguageModificationParser
{
    private readonly ILogger<NaturalLanguageModificationParser> _logger;
    private readonly DateParserAgent _dateParserAgent;

    public NaturalLanguageModificationParser(
        ILogger<NaturalLanguageModificationParser> logger,
        DateParserAgent dateParserAgent)
    {
        _logger = logger;
        _dateParserAgent = dateParserAgent;
    }

    /// <summary>
    /// Extracts all possible booking fields from a natural language message.
    /// </summary>
    public Dictionary<string, object> ExtractFields(string userMessage, ModificationState state)
    {
        var extracted = new Dictionary<string, object>();
        var normalizedMessage = userMessage.ToLowerInvariant().Trim();

        _logger.LogDebug("Extracting fields from message: {Message}", userMessage);

        // Extract date
        var dateValue = ExtractDate(normalizedMessage, state);
        if (dateValue != null)
        {
            extracted["date"] = dateValue;
            _logger.LogDebug("Extracted date: {Date}", dateValue);
        }

        // Extract time
        var timeValue = ExtractTime(normalizedMessage, state);
        if (timeValue != null)
        {
            extracted["time"] = timeValue;
            _logger.LogDebug("Extracted time: {Time}", timeValue);
        }

        // Extract party size
        var partySizeValue = ExtractPartySize(normalizedMessage);
        if (partySizeValue != null)
        {
            extracted["party_size"] = partySizeValue.Value;
            _logger.LogDebug("Extracted party size: {PartySize}", partySizeValue);
        }

        // Extract rice preference
        var riceValue = ExtractRicePreference(normalizedMessage);
        if (riceValue != null)
        {
            extracted["rice"] = riceValue;
            _logger.LogDebug("Extracted rice preference: {Rice}", riceValue);
        }

        // Extract tronas (high chairs)
        var tronasValue = ExtractTronas(normalizedMessage);
        if (tronasValue != null)
        {
            extracted["tronas"] = tronasValue.Value;
            _logger.LogDebug("Extracted tronas: {Tronas}", tronasValue);
        }

        // Extract carritos (baby carriages)
        var carritosValue = ExtractCarritos(normalizedMessage);
        if (carritosValue != null)
        {
            extracted["carritos"] = carritosValue.Value;
            _logger.LogDebug("Extracted carritos: {Carritos}", carritosValue);
        }

        _logger.LogInformation("Extracted {Count} fields from message: {Fields}",
            extracted.Count, string.Join(", ", extracted.Keys));

        return extracted;
    }

    /// <summary>
    /// Detects if the user is making a correction.
    /// </summary>
    public bool IsCorrection(string userMessage)
    {
        var normalized = userMessage.ToLowerInvariant().Trim();

        // Spanish correction patterns
        var correctionPatterns = new[]
        {
            @"^no\s",
            @"^no,\s",
            @"no es\s",
            @"no es para\s",
            @"mejor\s",
            @"cambialo\s",
            @"cámbialo\s",
            @"en realidad\s",
            @"mejor el\s",
            @"quiero el\s"
        };

        return correctionPatterns.Any(pattern => Regex.IsMatch(normalized, pattern));
    }

    /// <summary>
    /// Infers the user's goal from the message and context.
    /// </summary>
    public string? InferUserGoal(string userMessage, ModificationState state)
    {
        var extracted = ExtractFields(userMessage, state);
        var normalized = userMessage.ToLowerInvariant().Trim();

        // Check for combined date+time
        if (extracted.ContainsKey("date") && extracted.ContainsKey("time"))
            return "change_both";

        // Check for explicit patterns
        if (Regex.IsMatch(normalized, @"cambiar\s+(la\s+)?fecha|otro\s+d[ií]a"))
            return "change_date";

        if (Regex.IsMatch(normalized, @"cambiar\s+(la\s+)?hora|otra\s+hora|m[aá]s\s+tarde|m[aá]s\s+temprano"))
            return "change_time";

        // Use context from last asked field
        if (!string.IsNullOrEmpty(state.LastAskedField))
        {
            if (extracted.ContainsKey("date") && state.LastAskedField == "date")
                return "change_date";
            if (extracted.ContainsKey("time") && state.LastAskedField == "time")
                return "change_time";
        }

        // Infer from extracted fields
        if (extracted.ContainsKey("date"))
            return "change_date";
        if (extracted.ContainsKey("time"))
            return "change_time";
        if (extracted.ContainsKey("party_size"))
            return "change_party_size";
        if (extracted.ContainsKey("rice"))
            return "change_rice";

        return null;
    }

    // ========== PRIVATE EXTRACTION METHODS ==========

    private DateTime? ExtractDate(string message, ModificationState state)
    {
        // Try combined date+time patterns first
        // Pattern: "domingo 15 a las 14:30" or "domingo 15/03 a las 14:30"
        var combinedPattern = @"(?<day>\w+)\s+(?<date>\d{1,2}(?:/\d{1,2}(?:/\d{2,4})?)?)\s+(?:a\s+las\s+|a\s+las\s+)?(?<time>\d{1,2}[:\.]\d{2})";
        var combinedMatch = Regex.Match(message, combinedPattern);
        if (combinedMatch.Success)
        {
            // Extract just the date part
            var dayName = combinedMatch.Groups["day"].Value;
            var datePart = combinedMatch.Groups["date"].Value;
            return ParseDateExpression($"{dayName} {datePart}", state);
        }

        // Pattern: "domingo 15" or "domingo 15/03"
        var dayDatePattern = @"(?<day>\w+)\s+(?<date>\d{1,2}(?:/\d{1,2}(?:/\d{2,4})?)?)";
        var dayDateMatch = Regex.Match(message, dayDatePattern);
        if (dayDateMatch.Success)
        {
            return ParseDateExpression($"{dayDateMatch.Groups["day"].Value} {dayDateMatch.Groups["date"].Value}", state);
        }

        // Pattern: "15/03" or "15/03/2026" or "15 de marzo"
        var datePattern = @"\b(\d{1,2}/\d{1,2}(?:/\d{2,4})?|\d{1,2}\s+de\s+\w+)";
        var dateMatch = Regex.Match(message, datePattern);
        if (dateMatch.Success)
        {
            return ParseDateExpression(dateMatch.Value, state);
        }

        // Pattern: "domingo" or "lunes" or "mañana" etc.
        var relativePattern = @"\b(mañana|pasado mañana|el\s+lunes|el\s+martes|el\s+miércoles|el\s+jueves|el\s+viernes|el\s+sábado|el\s+domingo|lunes|martes|miércoles|jueves|viernes|sábado|domingo)\b";
        var relativeMatch = Regex.Match(message, relativePattern);
        if (relativeMatch.Success)
        {
            return ParseDateExpression(relativeMatch.Value, state);
        }

        return null;
    }

    private TimeSpan? ExtractTime(string message, ModificationState state)
    {
        // Try combined date+time patterns first
        var combinedPattern = @"(?<day>\w+)\s+(?<date>\d{1,2}(?:/\d{1,2}(?:/\d{2,4})?)?)\s+(?:a\s+las\s+|a\s+las)?\s*(?<time>\d{1,2}[:\.]\d{2})";
        var combinedMatch = Regex.Match(message, combinedPattern);
        if (combinedMatch.Success)
        {
            return ParseTimeExpression(combinedMatch.Groups["time"].Value);
        }

        // Pattern: "a las 14:30" or "a las 14.30" or "14:30h"
        var timePattern = @"(?:a\s+las\s+)?(\d{1,2}[:\.]\d{2})(?:h)?";
        var timeMatch = Regex.Match(message, timePattern);
        if (timeMatch.Success)
        {
            return ParseTimeExpression(timeMatch.Groups[1].Value);
        }

        // Pattern: "más tarde" or "más temprano" - relative time expressions
        if (Regex.IsMatch(message, @"m[aá]s\s+tarde"))
        {
            // If we have a current booking time, suggest 30-60 mins later
            if (state.SelectedBooking?.ReservationTime != null)
            {
                return state.SelectedBooking.ReservationTime.Add(TimeSpan.FromMinutes(30));
            }
        }

        if (Regex.IsMatch(message, @"m[aá]s\s+temprano"))
        {
            if (state.SelectedBooking?.ReservationTime != null)
            {
                return state.SelectedBooking.ReservationTime.Subtract(TimeSpan.FromMinutes(30));
            }
        }

        return null;
    }

    private int? ExtractPartySize(string message)
    {
        // Pattern: "para 10 personas" or "para 10" or "10 personas" or "seremos 10"
        var patterns = new[]
        {
            @"para\s+(\d+)\s+(?:personas?|pax)?",
            @"(\d+)\s+personas?",
            @"seremos\s+(\d+)",
            @"somos\s+(\d+)",
            @"(\d+)\s+pax"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(message, pattern);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var partySize))
            {
                return partySize;
            }
        }

        return null;
    }

    private string? ExtractRicePreference(string message)
    {
        // Pattern: "con arroz" or "sin arroz" or "arroz para 5"
        if (Regex.IsMatch(message, @"sin\s+arroz|no\s+quiero\s+arroz"))
            return "sin arroz";

        var riceMatch = Regex.Match(message, @"(?:con\s+)?arroz\s+(?:para\s+)?(\d+)");
        if (riceMatch.Success)
        {
            return $"arroz para {riceMatch.Groups[1].Value}";
        }

        if (Regex.IsMatch(message, @"\bcon\s+arroz\b"))
            return "con arroz";

        return null;
    }

    private int? ExtractTronas(string message)
    {
        // Pattern: "2 tronas" or "tronas 2" or "necesito 2 tronas"
        var patterns = new[]
        {
            @"(\d+)\s+tronas?",
            @"tronas?\s+(\d+)",
            @"necesito\s+(\d+)\s+tronas?"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(message, pattern);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var tronas))
            {
                return tronas;
            }
        }

        return null;
    }

    private int? ExtractCarritos(string message)
    {
        // Pattern: "2 carritos" or "carritos 2" or "necesito 2 carritos"
        var patterns = new[]
        {
            @"(\d+)\s+carritos?",
            @"carritos?\s+(\d+)",
            @"necesito\s+(\d+)\s+carritos?"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(message, pattern);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var carritos))
            {
                return carritos;
            }
        }

        return null;
    }

    // ========== PARSING HELPERS ==========

    private DateTime? ParseDateExpression(string dateExpression, ModificationState state)
    {
        try
        {
            // Use the existing DateParserAgent for complex date parsing
            // This is a simplified version - in production, we'd call the agent
            var normalized = dateExpression.ToLowerInvariant().Trim();

            // Relative dates
            if (normalized.Contains("mañana") && !normalized.Contains("pasado"))
            {
                return DateTime.Today.AddDays(1);
            }

            if (normalized.Contains("pasado mañana"))
            {
                return DateTime.Today.AddDays(2);
            }

            // Day names
            var dayNames = new Dictionary<string, DayOfWeek>
            {
                {"domingo", DayOfWeek.Sunday},
                {"lunes", DayOfWeek.Monday},
                {"martes", DayOfWeek.Tuesday},
                {"miércoles", DayOfWeek.Wednesday},
                {"jueves", DayOfWeek.Thursday},
                {"viernes", DayOfWeek.Friday},
                {"sábado", DayOfWeek.Saturday}
            };

            foreach (var kvp in dayNames)
            {
                if (normalized.Contains(kvp.Key))
                {
                    // Find next occurrence of this day
                    var today = DateTime.Today;
                    var daysUntil = (kvp.Value - today.DayOfWeek + 7) % 7;
                    if (daysUntil == 0) daysUntil = 7; // If today is that day, assume next week
                    var targetDate = today.AddDays(daysUntil);

                    // Check if there's a specific date number
                    var dateMatch = Regex.Match(normalized, @"(\d{1,2})(?:/(\d{1,2})(?:/(\d{2,4}))?)?");
                    if (dateMatch.Success)
                    {
                        var day = int.Parse(dateMatch.Groups[1].Value);
                        var month = dateMatch.Groups[2].Success
                            ? int.Parse(dateMatch.Groups[2].Value)
                            : targetDate.Month;
                        var year = dateMatch.Groups[3].Success
                            ? int.Parse(dateMatch.Groups[3].Value)
                            : targetDate.Year;

                        // Adjust year for 2-digit formats
                        if (year < 100) year += 2000;

                        try
                        {
                            return new DateTime(year, month, day);
                        }
                        catch
                        {
                            // Invalid date, fall back to day-of-week calculation
                            return targetDate;
                        }
                    }

                    return targetDate;
                }
            }

            // Pure date formats: "15/03" or "15/03/2026" or "15 de marzo"
            var pureDateMatch = Regex.Match(normalized, @"(\d{1,2})/(\d{1,2})(?:/(\d{2,4}))?");
            if (pureDateMatch.Success)
            {
                var day = int.Parse(pureDateMatch.Groups[1].Value);
                var month = int.Parse(pureDateMatch.Groups[2].Value);
                var year = pureDateMatch.Groups[3].Success
                    ? int.Parse(pureDateMatch.Groups[3].Value)
                    : DateTime.Today.Year;

                if (year < 100) year += 2000;

                try
                {
                    return new DateTime(year, month, day);
                }
                catch
                {
                    _logger.LogWarning("Invalid date parsed: {Day}/{Month}/{Year}", day, month, year);
                    return null;
                }
            }

            // "15 de marzo" format
            var textDateMatch = Regex.Match(normalized, @"(\d{1,2})\s+de\s+(\w+)");
            if (textDateMatch.Success)
            {
                var day = int.Parse(textDateMatch.Groups[1].Value);
                var monthName = textDateMatch.Groups[2].Value.ToLowerInvariant();

                var months = new Dictionary<string, int>
                {
                    {"enero", 1}, {"febrero", 2}, {"marzo", 3}, {"abril", 4},
                    {"mayo", 5}, {"junio", 6}, {"julio", 7}, {"agosto", 8},
                    {"septiembre", 9}, {"octubre", 10}, {"noviembre", 11}, {"diciembre", 12}
                };

                if (months.TryGetValue(monthName, out var month))
                {
                    try
                    {
                        return new DateTime(DateTime.Today.Year, month, day);
                    }
                    catch
                    {
                        _logger.LogWarning("Invalid text date parsed: {Day} de {Month}", day, monthName);
                        return null;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing date expression: {Expression}", dateExpression);
            return null;
        }
    }

    private TimeSpan? ParseTimeExpression(string timeExpression)
    {
        try
        {
            // Normalize: replace '.' with ':'
            var normalized = timeExpression.Replace('.', ':');

            // Parse HH:MM format
            if (TimeSpan.TryParseExact(normalized, @"hh\:mm", CultureInfo.InvariantCulture, out var time))
            {
                return time;
            }

            // Try parsing with TimeSpan.TryParse (more flexible)
            if (TimeSpan.TryParse(normalized, out time))
            {
                return time;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing time expression: {Expression}", timeExpression);
            return null;
        }
    }
}
