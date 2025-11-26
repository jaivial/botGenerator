using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace BotGenerator.Core.Agents;

/// <summary>
/// Main conversation agent that handles general restaurant inquiries and bookings.
/// </summary>
public class MainConversationAgent : IAgent
{
    private readonly IGeminiService _gemini;
    private readonly IPromptLoaderService _promptLoader;
    private readonly IContextBuilderService _contextBuilder;
    private readonly IConversationHistoryService _historyService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MainConversationAgent> _logger;

    public MainConversationAgent(
        IGeminiService gemini,
        IPromptLoaderService promptLoader,
        IContextBuilderService contextBuilder,
        IConversationHistoryService historyService,
        IConfiguration configuration,
        ILogger<MainConversationAgent> logger)
    {
        _gemini = gemini ?? throw new ArgumentNullException(nameof(gemini));
        _promptLoader = promptLoader ?? throw new ArgumentNullException(nameof(promptLoader));
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentResponse> ProcessAsync(
        WhatsAppMessage message,
        ConversationState? state,
        List<ChatMessage>? history,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Processing message from {Sender}: {Preview}",
                message.PushName,
                message.MessageText.Length > 50
                    ? message.MessageText[..50] + "..."
                    : message.MessageText);

            // 1. Get restaurant ID for this conversation
            var restaurantId = GetRestaurantId(message.SenderNumber);

            // 2. Get conversation history if not provided
            history ??= await _historyService.GetHistoryAsync(
                message.SenderNumber, cancellationToken);

            // 3. Extract conversation state if not provided
            state ??= _historyService.ExtractState(history);

            // 4. Build context with all dynamic values
            var context = _contextBuilder.BuildContext(message, state, history);

            // 5. Assemble the system prompt from external files
            var systemPrompt = await _promptLoader.AssembleSystemPromptAsync(
                restaurantId, context);

            _logger.LogDebug(
                "Assembled system prompt: {Length} chars",
                systemPrompt.Length);

            // 6. Call Gemini API
            var aiResponse = await _gemini.GenerateAsync(
                systemPrompt,
                message.MessageText,
                history,
                cancellationToken);

            _logger.LogDebug(
                "Received AI response: {Preview}",
                aiResponse.Length > 100 ? aiResponse[..100] + "..." : aiResponse);

            // 7. Parse the response for commands and clean it
            var parsedResponse = ParseAiResponse(aiResponse, message, state);

            // 8. Store the new messages in history
            await _historyService.AddMessageAsync(
                message.SenderNumber,
                ChatMessage.FromUser(message.MessageText, message.PushName),
                cancellationToken);

            await _historyService.AddMessageAsync(
                message.SenderNumber,
                ChatMessage.FromAssistant(parsedResponse.AiResponse),
                cancellationToken);

            return parsedResponse;
        }
        catch (GeminiApiException ex)
        {
            _logger.LogError(ex, "Gemini API error processing message");
            return AgentResponse.Error($"AI service error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing message");
            return AgentResponse.Error("Unexpected error occurred");
        }
    }

    #region Private Methods

    /// <summary>
    /// Gets the restaurant ID based on the sender's phone number.
    /// In production, this would look up from a database or configuration.
    /// </summary>
    private string GetRestaurantId(string senderNumber)
    {
        // Try to find mapping in configuration
        var mapping = _configuration
            .GetSection("Restaurants:Mapping")
            .GetChildren()
            .ToDictionary(x => x.Key, x => x.Value);

        if (mapping.TryGetValue(senderNumber, out var restaurantId))
        {
            return restaurantId ?? "villacarmen";
        }

        // Fall back to default
        return _configuration["Restaurants:Default"] ?? "villacarmen";
    }

    /// <summary>
    /// Parses the AI response to extract commands and clean the response text.
    /// </summary>
    private AgentResponse ParseAiResponse(
        string aiResponse,
        WhatsAppMessage message,
        ConversationState? state)
    {
        var intent = IntentType.Normal;
        BookingData? extractedData = null;
        var cleanResponse = aiResponse;
        var metadata = new Dictionary<string, object>();

        // Remove markdown escaping
        var unescapedResponse = aiResponse
            .Replace(@"\_", "_")
            .Replace(@"\|", "|")
            .Replace(@"\*", "*");

        // ========== CHECK FOR BOOKING_REQUEST ==========
        if (unescapedResponse.Contains("BOOKING_REQUEST|"))
        {
            intent = IntentType.Booking;

            var match = Regex.Match(
                unescapedResponse,
                @"BOOKING_REQUEST\|([^|]+)\|([^|]+)\|([^|]+)\|([^|]+)\|([^\n|]+)");

            if (match.Success)
            {
                extractedData = new BookingData
                {
                    Name = match.Groups[1].Value.Trim(),
                    Phone = match.Groups[2].Value.Trim(),
                    Date = match.Groups[3].Value.Trim(),
                    People = int.TryParse(match.Groups[4].Value.Trim(), out var p) ? p : 0,
                    Time = match.Groups[5].Value.Trim()
                };

                // Extract additional fields if present
                extractedData = ExtractAdditionalBookingFields(unescapedResponse, extractedData);

                _logger.LogInformation(
                    "Extracted booking: {Name}, {Date}, {Time}, {People} people",
                    extractedData.Name, extractedData.Date,
                    extractedData.Time, extractedData.People);
            }

            // Remove command from response
            cleanResponse = Regex.Replace(
                unescapedResponse,
                @"BOOKING_REQUEST\|[^\n]+",
                "").Trim();
        }

        // ========== CHECK FOR CANCELLATION_REQUEST ==========
        else if (unescapedResponse.Contains("CANCELLATION_REQUEST|"))
        {
            intent = IntentType.Cancellation;

            var match = Regex.Match(
                unescapedResponse,
                @"CANCELLATION_REQUEST\|([^|]+)\|([^|]+)\|([^|]+)\|([^|]+)\|([^\n|]+)");

            if (match.Success)
            {
                extractedData = new BookingData
                {
                    Name = match.Groups[1].Value.Trim(),
                    Phone = match.Groups[2].Value.Trim(),
                    Date = match.Groups[3].Value.Trim(),
                    People = int.TryParse(match.Groups[4].Value.Trim(), out var p) ? p : 0,
                    Time = match.Groups[5].Value.Trim()
                };
            }

            cleanResponse = Regex.Replace(
                unescapedResponse,
                @"CANCELLATION_REQUEST\|[^\n]+",
                "").Trim();
        }

        // ========== CHECK FOR MODIFICATION_INTENT ==========
        else if (unescapedResponse.Contains("MODIFICATION_INTENT"))
        {
            intent = IntentType.Modification;
            cleanResponse = unescapedResponse
                .Replace("MODIFICATION_INTENT", "")
                .Trim();
        }

        // ========== CHECK FOR SAME_DAY_BOOKING ==========
        else if (unescapedResponse.Contains("SAME_DAY_BOOKING"))
        {
            intent = IntentType.SameDay;
            cleanResponse = unescapedResponse
                .Replace("SAME_DAY_BOOKING", "")
                .Trim();

            // If response is empty, provide default message
            if (string.IsNullOrWhiteSpace(cleanResponse))
            {
                cleanResponse = "Lo sentimos, no aceptamos reservas para el mismo día. " +
                               "Por favor, llámanos al +34 638 857 294 para ver disponibilidad.";
            }
        }

        // ========== CHECK FOR URLs (INTERACTIVE) ==========
        else if (Regex.IsMatch(unescapedResponse, @"https?://[^\s]+"))
        {
            var urls = Regex.Matches(unescapedResponse, @"https?://[^\s]+")
                .Select(m => m.Value)
                .ToList();

            if (urls.Count > 0)
            {
                metadata["hasUrls"] = true;
                metadata["urls"] = urls;
            }
        }

        // Clean the response for WhatsApp
        cleanResponse = CleanForWhatsApp(cleanResponse);

        // Ensure we have some response
        if (string.IsNullOrWhiteSpace(cleanResponse))
        {
            cleanResponse = "Disculpa, no he entendido bien. ¿Puedes repetirlo?";
        }

        return new AgentResponse
        {
            Intent = intent,
            AiResponse = cleanResponse,
            ExtractedData = extractedData,
            Metadata = metadata.Count > 0 ? metadata : null,
            RawResponse = aiResponse
        };
    }

    /// <summary>
    /// Extracts additional booking fields like rice type, servings, etc.
    /// </summary>
    private BookingData ExtractAdditionalBookingFields(string response, BookingData booking)
    {
        // Extract rice type
        var riceMatch = Regex.Match(response,
            @"arroz\s+(del?\s+)?([a-záéíóúñ\s]+?)(?:\s*[,.]|\s*\d+|$)",
            RegexOptions.IgnoreCase);

        string? arrozType = null;
        if (riceMatch.Success)
        {
            var prep = riceMatch.Groups[1].Value.Trim();
            var name = riceMatch.Groups[2].Value.Trim();
            arrozType = string.IsNullOrEmpty(prep) ? name : $"{prep} {name}".Trim();
        }

        // Extract rice servings
        int? arrozServings = null;
        var servingsMatch = Regex.Match(response, @"(\d+)\s*raciones?", RegexOptions.IgnoreCase);
        if (servingsMatch.Success)
        {
            arrozServings = int.Parse(servingsMatch.Groups[1].Value);
        }

        // Extract high chairs
        var chairsMatch = Regex.Match(response, @"(\d+)\s*tronas?", RegexOptions.IgnoreCase);
        var highChairs = chairsMatch.Success ? int.Parse(chairsMatch.Groups[1].Value) : 0;

        // Extract strollers
        var strollersMatch = Regex.Match(response, @"(\d+)\s*carrit[oa]s?", RegexOptions.IgnoreCase);
        var babyStrollers = strollersMatch.Success ? int.Parse(strollersMatch.Groups[1].Value) : 0;

        return booking with
        {
            ArrozType = arrozType,
            ArrozServings = arrozServings,
            HighChairs = highChairs,
            BabyStrollers = babyStrollers
        };
    }

    /// <summary>
    /// Cleans the response text for WhatsApp formatting.
    /// </summary>
    private string CleanForWhatsApp(string text)
    {
        // Convert **text** to *text* for WhatsApp bold
        text = Regex.Replace(text, @"\*\*([^*]+)\*\*", "*$1*");

        // Remove escape backslashes
        text = text.Replace(@"\_", "_");
        text = text.Replace(@"\|", "|");

        // Remove multiple consecutive newlines
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        // Trim
        text = text.Trim();

        // Remove lines that are just whitespace
        var lines = text.Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        return string.Join("\n", lines);
    }

    #endregion
}
