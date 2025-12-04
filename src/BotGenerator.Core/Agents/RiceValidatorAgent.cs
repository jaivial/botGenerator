using BotGenerator.Core.Models;
using BotGenerator.Core.Services;

namespace BotGenerator.Core.Agents;

/// <summary>
/// Specialized agent for validating rice types against the menu.
/// </summary>
public class RiceValidatorAgent : IAgent
{
    private readonly IGeminiService _gemini;
    private readonly IPromptLoaderService _promptLoader;
    private readonly IMenuRepository _menuRepository;
    private readonly ILogger<RiceValidatorAgent> _logger;

    public RiceValidatorAgent(
        IGeminiService gemini,
        IPromptLoaderService promptLoader,
        IMenuRepository menuRepository,
        ILogger<RiceValidatorAgent> logger)
    {
        _gemini = gemini;
        _promptLoader = promptLoader;
        _menuRepository = menuRepository;
        _logger = logger;
    }

    public async Task<AgentResponse> ProcessAsync(
        WhatsAppMessage message,
        ConversationState? state,
        List<ChatMessage>? history,
        CancellationToken cancellationToken = default)
    {
        // This agent is typically called with rice request in message.MessageText
        var userRiceRequest = message.MessageText;

        _logger.LogInformation(
            "Validating rice request: {Request}",
            userRiceRequest);

        // Get available rice types (in production, fetch from API)
        var availableTypes = await GetAvailableRiceTypesAsync(cancellationToken);

        // Build context for rice validation prompt
        var context = new Dictionary<string, object>
        {
            ["userRiceRequest"] = userRiceRequest,
            ["availableRiceTypes"] = string.Join(", ", availableTypes),
            ["riceCount"] = availableTypes.Count
        };

        // Load rice validation prompt
        var systemPrompt = await _promptLoader.LoadSpecializedPromptAsync(
            "villacarmen", // Get from config
            "rice-validation",
            context);

        // Call Gemini with focused prompt
        var aiResponse = await _gemini.GenerateAsync(
            systemPrompt,
            userRiceRequest,
            cancellationToken: cancellationToken);

        // Parse the validation result
        return ParseValidationResult(aiResponse, userRiceRequest);
    }

    /// <summary>
    /// Validates a rice request and returns structured result.
    /// </summary>
    public async Task<RiceValidationResult> ValidateAsync(
        string userRiceRequest,
        string restaurantId,
        CancellationToken cancellationToken = default)
    {
        var availableTypes = await GetAvailableRiceTypesAsync(cancellationToken);

        var context = new Dictionary<string, object>
        {
            ["userRiceRequest"] = userRiceRequest,
            ["availableRiceTypes"] = string.Join(", ", availableTypes)
        };

        var systemPrompt = await _promptLoader.LoadSpecializedPromptAsync(
            restaurantId,
            "rice-validation",
            context);

        var aiResponse = await _gemini.GenerateAsync(
            systemPrompt,
            userRiceRequest,
            cancellationToken: cancellationToken);

        return ParseToValidationResult(aiResponse, userRiceRequest);
    }

    private async Task<List<string>> GetAvailableRiceTypesAsync(
        CancellationToken cancellationToken)
    {
        // Fetch rice types from MySQL database
        return await _menuRepository.GetActiveRiceTypesAsync(cancellationToken);
    }

    private AgentResponse ParseValidationResult(string aiResponse, string originalRequest)
    {
        var result = ParseToValidationResult(aiResponse, originalRequest);

        return new AgentResponse
        {
            Intent = IntentType.Normal,
            AiResponse = result.Message ?? "",
            Metadata = new Dictionary<string, object>
            {
                ["riceValidation"] = result,
                ["riceStatus"] = result.Status,
                ["riceName"] = result.RiceName ?? ""
            }
        };
    }

    private RiceValidationResult ParseToValidationResult(string aiResponse, string originalRequest)
    {
        if (aiResponse.Contains("RICE_VALID|"))
        {
            var parts = aiResponse.Split("RICE_VALID|");
            var riceName = parts.Length > 1 ? parts[1].Trim().Split('\n')[0] : "";

            return RiceValidationResult.Valid(riceName, originalRequest);
        }

        if (aiResponse.Contains("RICE_NOT_FOUND|"))
        {
            return RiceValidationResult.NotFound(originalRequest);
        }

        if (aiResponse.Contains("RICE_MULTIPLE|"))
        {
            var parts = aiResponse.Split("RICE_MULTIPLE|");
            var options = parts.Length > 1
                ? parts[1].Trim().Split(" y ").Select(s => s.Trim()).ToList()
                : new List<string>();

            return RiceValidationResult.Multiple(options, originalRequest);
        }

        _logger.LogWarning(
            "Unexpected rice validation response: {Response}",
            aiResponse);

        return RiceValidationResult.NotFound(originalRequest);
    }
}
