using BotGenerator.Core.Agents;
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using FluentAssertions;

namespace BotGenerator.Core.Tests.Infrastructure;

/// <summary>
/// Simulates a WhatsApp conversation for testing bot behavior.
/// Tracks message history and provides fluent assertions for responses.
/// </summary>
public class ConversationSimulator
{
    private readonly MainConversationAgent _mainAgent;
    private readonly IIntentRouterService _intentRouter;
    private readonly IConversationHistoryService _historyService;
    private readonly List<ChatMessage> _history = new();
    private string _currentResponse = "";
    private string _userId;
    private string _pushName;
    private DateTime _currentDate = DateTime.Now;

    public ConversationSimulator(
        MainConversationAgent mainAgent,
        IIntentRouterService intentRouter,
        IConversationHistoryService historyService,
        string userId = "test-user",
        string pushName = "TestUser")
    {
        _mainAgent = mainAgent ?? throw new ArgumentNullException(nameof(mainAgent));
        _intentRouter = intentRouter ?? throw new ArgumentNullException(nameof(intentRouter));
        _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
        _userId = userId;
        _pushName = pushName;
    }

    /// <summary>
    /// Simulates a user sending a message and captures the bot response.
    /// </summary>
    public async Task UserSays(string message)
    {
        var incoming = new WhatsAppMessage
        {
            SenderNumber = _userId,
            MessageText = message,
            PushName = _pushName,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            MessageType = "text",
            FromMe = false
        };

        _history.Add(ChatMessage.FromUser(message, _pushName));

        // Get conversation state
        var state = _historyService.ExtractState(_history);

        // Process with main agent
        var agentResponse = await _mainAgent.ProcessAsync(
            incoming, state, _history, CancellationToken.None);

        // Route based on intent
        var finalResponse = await _intentRouter.RouteAsync(
            agentResponse, incoming, state, CancellationToken.None);

        _currentResponse = finalResponse.AiResponse;
        _history.Add(ChatMessage.FromAssistant(_currentResponse));
    }

    /// <summary>
    /// Asserts that the current response contains all specified terms.
    /// </summary>
    public ConversationSimulator ShouldRespond(params string[] containingTerms)
    {
        foreach (var term in containingTerms)
        {
            _currentResponse.ToLower().Should().Contain(term.ToLower(),
                $"Response should contain '{term}'.\nActual response: {_currentResponse}");
        }
        return this;
    }

    /// <summary>
    /// Asserts that the current response does NOT contain any of the specified terms.
    /// </summary>
    public ConversationSimulator ShouldNotMention(params string[] terms)
    {
        foreach (var term in terms)
        {
            _currentResponse.ToLower().Should().NotContain(term.ToLower(),
                $"Response should NOT contain '{term}'.\nActual response: {_currentResponse}");
        }
        return this;
    }

    /// <summary>
    /// Asserts that the response is at most the specified length.
    /// </summary>
    public ConversationSimulator ResponseLengthShouldBe(int maxChars)
    {
        _currentResponse.Length.Should().BeLessThanOrEqualTo(maxChars,
            $"Response too long ({_currentResponse.Length} chars). " +
            $"Max allowed: {maxChars}.\nResponse: {_currentResponse}");
        return this;
    }

    /// <summary>
    /// Asserts that the response has at most the specified number of question marks.
    /// </summary>
    public ConversationSimulator ShouldHaveMaxQuestions(int max)
    {
        var questionCount = _currentResponse.Count(c => c == '?');
        questionCount.Should().BeLessThanOrEqualTo(max,
            $"Too many questions ({questionCount}) in response: {_currentResponse}");
        return this;
    }

    /// <summary>
    /// Asserts that the response has at most the specified number of emojis.
    /// </summary>
    public ConversationSimulator ShouldHaveMaxEmojis(int max)
    {
        var emojiCount = CountEmojis(_currentResponse);
        emojiCount.Should().BeLessThanOrEqualTo(max,
            $"Too many emojis ({emojiCount}) in response: {_currentResponse}");
        return this;
    }

    /// <summary>
    /// Asserts the response does not sound robotic.
    /// </summary>
    public ConversationSimulator ShouldRespondNaturally()
    {
        var roboticPhrases = new[]
        {
            "sistema de reservas",
            "procesar su solicitud",
            "campo obligatorio",
            "dato requerido",
            "introduzca",
            "seleccione una opción",
            "datos registrados",
            "procesando"
        };

        foreach (var phrase in roboticPhrases)
        {
            _currentResponse.ToLower().Should().NotContain(phrase,
                $"Response sounds robotic: contains '{phrase}'");
        }
        return this;
    }

    /// <summary>
    /// Sets the current date for time-sensitive tests.
    /// </summary>
    public void SetCurrentDate(DateTime date)
    {
        _currentDate = date;
    }

    /// <summary>
    /// Enables persistent mode for long journeys.
    /// </summary>
    public void EnablePersistence()
    {
        // Implementation for persistent state
    }

    /// <summary>
    /// Gets the current conversation state.
    /// </summary>
    public async Task<ConversationState> GetCurrentStateAsync()
    {
        return await Task.FromResult(_historyService.ExtractState(_history));
    }

    /// <summary>
    /// Total message count (user + assistant).
    /// </summary>
    public int MessageCount => _history.Count;

    /// <summary>
    /// The last response from the bot.
    /// </summary>
    public string LastResponse => _currentResponse;

    /// <summary>
    /// The full conversation history.
    /// </summary>
    public IReadOnlyList<ChatMessage> History => _history;

    private static int CountEmojis(string text)
    {
        // Simple emoji counting using Unicode ranges
        int count = 0;
        foreach (var c in text)
        {
            // Check common emoji ranges
            if (c >= 0x1F300 && c <= 0x1F9FF) count++;
            else if (c >= 0x2600 && c <= 0x26FF) count++;
            else if (c >= 0x2700 && c <= 0x27BF) count++;
            else if ("✅❌📅🕐👥👤🍚🪑".Contains(c)) count++;
        }
        return count;
    }
}
