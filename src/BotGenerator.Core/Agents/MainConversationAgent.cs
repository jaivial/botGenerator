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
    private readonly IMenuRepository _menuRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MainConversationAgent> _logger;

    public MainConversationAgent(
        IGeminiService gemini,
        IPromptLoaderService promptLoader,
        IContextBuilderService contextBuilder,
        IConversationHistoryService historyService,
        IMenuRepository menuRepository,
        IConfiguration configuration,
        ILogger<MainConversationAgent> logger)
    {
        _gemini = gemini ?? throw new ArgumentNullException(nameof(gemini));
        _promptLoader = promptLoader ?? throw new ArgumentNullException(nameof(promptLoader));
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
        _menuRepository = menuRepository ?? throw new ArgumentNullException(nameof(menuRepository));
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

            // 4. Pre-validate rice if user mentions it
            var riceValidation = await PreValidateRiceAsync(message.MessageText, restaurantId, cancellationToken);

            if (riceValidation.HasRiceRequest && !riceValidation.IsValid)
            {
                // Invalid rice - return rejection message directly
                _logger.LogInformation(
                    "Rice pre-validation failed: {RiceRequest} not found in menu",
                    riceValidation.RequestedRice);

                return new AgentResponse
                {
                    Intent = IntentType.Normal,
                    AiResponse = $"Lo siento, no tenemos \"{riceValidation.RequestedRice}\" en nuestro menú. " +
                                $"Nuestros arroces disponibles son: {string.Join(", ", riceValidation.AvailableTypes)}. " +
                                "¿Te gustaría alguno de estos?"
                };
            }

            // 4. Build context with all dynamic values
            var context = _contextBuilder.BuildContext(message, state, history);

            // Add rice validation result to context if valid
            if (riceValidation.HasRiceRequest && riceValidation.IsValid)
            {
                context["validatedRiceName"] = riceValidation.ValidatedRiceName ?? "";
                context["riceValidated"] = "true";
            }

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
        // Try to find mapping in configuration (be resilient to missing sections / mocks)
        var mappingSection = _configuration.GetSection("Restaurants:Mapping");
        var children = mappingSection?.GetChildren() ?? Enumerable.Empty<IConfigurationSection>();
        var mapping = children.ToDictionary(x => x.Key, x => x.Value);

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
        // First check if this is a rice rejection - if so, don't extract rice type
        var isRiceRejection = Regex.IsMatch(response,
            @"(no\s+queremos\s+arroz|sin\s+arroz|no\s+arroz|nada\s+de\s+arroz|no,?\s+gracias|no\s+gracias)",
            RegexOptions.IgnoreCase);

        string? arrozType = null;

        if (!isRiceRejection)
        {
            // Extract rice type only if not a rejection
            var riceMatch = Regex.Match(response,
                @"arroz\s+(del?\s+)?([a-záéíóúñ\s]+?)(?:\s*[,.]|\s*\d+|$)",
                RegexOptions.IgnoreCase);

            if (riceMatch.Success)
            {
                var prep = riceMatch.Groups[1].Value.Trim();
                var name = riceMatch.Groups[2].Value.Trim();

                // Additional filter: reject common non-rice words that might be captured
                var invalidNames = new[] { "gracias", "por favor", "porfa", "porfavor", "vale", "ok", "si", "no" };
                if (!invalidNames.Contains(name.ToLower()))
                {
                    arrozType = string.IsNullOrEmpty(prep) ? name : $"{prep} {name}".Trim();
                }
            }
        }

        // Extract rice servings
        int? arrozServings = null;
        var servingsMatch = Regex.Match(response, @"(\d+)\s*raci(ón|ones?)", RegexOptions.IgnoreCase);
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
        text = text.Replace(@"\*", "*");

        // Remove lines that contain only whitespace (spaces, tabs, etc.)
        // This preserves truly empty lines but removes lines with just spaces/tabs
        var lines = text.Split('\n')
            .Select(line =>
            {
                // If line has content (after trimming), keep original with trailing whitespace removed
                // If line is truly empty (length 0), keep it as is
                // If line has only whitespace (length > 0 but all whitespace), mark for removal
                if (line.Length == 0)
                    return line; // Keep empty lines
                else if (string.IsNullOrWhiteSpace(line))
                    return null; // Mark whitespace-only lines for removal
                else
                    return line.TrimEnd(); // Keep content lines, remove trailing whitespace
            })
            .Where(line => line != null) // Remove whitespace-only lines
            .Select(line => line!) // Non-null assertion
            .ToList();

        text = string.Join("\n", lines);

        // Remove multiple consecutive newlines (3+ becomes 2)
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        // Trim
        text = text.Trim();

        return text;
    }

    /// <summary>
    /// Pre-validates rice type before calling the main AI.
    /// This catches invalid rice types early in the conversation.
    /// </summary>
    private async Task<RicePreValidationResult> PreValidateRiceAsync(
        string userMessage,
        string restaurantId,
        CancellationToken cancellationToken)
    {
        // First check if this is a rice rejection - if so, don't validate rice
        if (Regex.IsMatch(userMessage,
            @"(no\s+queremos\s+arroz|sin\s+arroz|no\s+arroz|nada\s+de\s+arroz|no\s+quiero\s+arroz)",
            RegexOptions.IgnoreCase))
        {
            _logger.LogInformation("Detected rice rejection, skipping validation");
            return new RicePreValidationResult { HasRiceRequest = false };
        }

        // Check if user mentions rice
        var ricePattern = @"arroz\s+(?:del?\s+)?([a-záéíóúñ\s]+?)(?:\s+para|\s+\d+|\s*$|[,.])";
        var match = Regex.Match(userMessage, ricePattern, RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            // No rice mentioned
            return new RicePreValidationResult { HasRiceRequest = false };
        }

        var requestedRice = match.Groups[1].Value.Trim();

        // Filter out common non-rice words that might be captured
        var invalidNames = new[] { "gracias", "por favor", "porfa", "porfavor", "vale", "ok", "si", "no" };
        if (invalidNames.Contains(requestedRice.ToLower()))
        {
            _logger.LogInformation("Detected non-rice word '{Word}', skipping validation", requestedRice);
            return new RicePreValidationResult { HasRiceRequest = false };
        }
        _logger.LogInformation("Detected rice request: {Rice}", requestedRice);

        // Get available rice types from database
        var availableTypes = await _menuRepository.GetActiveRiceTypesAsync(cancellationToken);

        if (availableTypes.Count == 0)
        {
            _logger.LogWarning("No rice types found in database");
            return new RicePreValidationResult { HasRiceRequest = false };
        }

        // Try to match the requested rice with available types
        var normalizedRequest = NormalizeForComparison(requestedRice);

        foreach (var available in availableTypes)
        {
            var normalizedAvailable = NormalizeForComparison(available);

            // Check for exact or partial match
            if (normalizedAvailable.Contains(normalizedRequest) ||
                normalizedRequest.Contains(normalizedAvailable) ||
                normalizedAvailable == normalizedRequest)
            {
                _logger.LogInformation(
                    "Rice validated: {Request} -> {Available}",
                    requestedRice, available);

                return new RicePreValidationResult
                {
                    HasRiceRequest = true,
                    IsValid = true,
                    RequestedRice = requestedRice,
                    ValidatedRiceName = available,
                    AvailableTypes = availableTypes
                };
            }
        }

        // No match found
        _logger.LogInformation(
            "Rice not found: {Request}. Available: {Available}",
            requestedRice, string.Join(", ", availableTypes));

        return new RicePreValidationResult
        {
            HasRiceRequest = true,
            IsValid = false,
            RequestedRice = requestedRice,
            AvailableTypes = availableTypes
        };
    }

    /// <summary>
    /// Normalizes a string for comparison by removing accents, lowercase, etc.
    /// </summary>
    private static string NormalizeForComparison(string text)
    {
        // Remove "arroz" prefix if present
        text = Regex.Replace(text, @"^arroz\s+(del?\s+)?", "", RegexOptions.IgnoreCase);

        // Lowercase
        text = text.ToLowerInvariant();

        // Remove accents
        text = text.Replace("á", "a").Replace("é", "e").Replace("í", "i")
                   .Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n");

        // Remove extra whitespace
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return text;
    }

    #endregion
}

/// <summary>
/// Result of rice pre-validation.
/// </summary>
public record RicePreValidationResult
{
    public bool HasRiceRequest { get; init; }
    public bool IsValid { get; init; }
    public string? RequestedRice { get; init; }
    public string? ValidatedRiceName { get; init; }
    public List<string> AvailableTypes { get; init; } = new();
}
