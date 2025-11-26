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
