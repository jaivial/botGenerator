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
