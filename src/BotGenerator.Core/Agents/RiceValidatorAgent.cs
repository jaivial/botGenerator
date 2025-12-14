using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using System.Globalization;

namespace BotGenerator.Core.Agents;

/// <summary>
/// Specialized agent for validating rice types against the menu.
/// </summary>
public class RiceValidatorAgent : IAgent, IRiceValidatorService
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
        // Deterministic DB-backed validation (no LLM) to avoid flaky behavior.
        // We keep the same output semantics as the prompt-based validator: valid / not_found / multiple.
        var availableTypes = await GetAvailableRiceTypesAsync(cancellationToken);
        if (availableTypes.Count == 0)
            return RiceValidationResult.NotFound(userRiceRequest);

        var normalizedRequest = NormalizeForComparison(userRiceRequest);

        // Try simple containment against normalized menu entries
        var matches = new List<string>();
        foreach (var available in availableTypes)
        {
            var normalizedAvailable = NormalizeForComparison(available);

            if (string.IsNullOrWhiteSpace(normalizedAvailable))
                continue;

            if (normalizedRequest.Contains(normalizedAvailable) ||
                normalizedAvailable.Contains(normalizedRequest) ||
                normalizedRequest == normalizedAvailable)
            {
                matches.Add(available);
            }
        }

        if (matches.Count == 1)
            return RiceValidationResult.Valid(matches[0], userRiceRequest);

        if (matches.Count > 1)
            return RiceValidationResult.Multiple(matches.Distinct().ToList(), userRiceRequest);

        // Fallback: keyword match (helps when the user mentions just an ingredient like "chorizo")
        var requestTokens = Tokenize(normalizedRequest);
        var scored = availableTypes
            .Select(a => new { Name = a, Score = TokenOverlapScore(requestTokens, Tokenize(NormalizeForComparison(a))) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ToList();

        if (scored.Count == 0)
            return RiceValidationResult.NotFound(userRiceRequest);

        // If top score is ambiguous (tie), return multiple.
        var top = scored[0].Score;
        var topMatches = scored.Where(x => x.Score == top).Select(x => x.Name).Distinct().ToList();

        if (topMatches.Count == 1)
            return RiceValidationResult.Valid(topMatches[0], userRiceRequest);

        return RiceValidationResult.Multiple(topMatches, userRiceRequest);
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

    private static string NormalizeForComparison(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        // Lowercase
        var t = text.ToLowerInvariant();

        // Remove common suffixes/phrases that appear in user requests
        t = t.Replace("raciones", " ").Replace("ración", " ");
        t = t.Replace("para", " ").Replace("queremos", " ").Replace("quiero", " ").Replace("sí", " ").Replace("si", " ");

        // Remove accents (diacritics)
        t = RemoveDiacritics(t);

        // Remove punctuation
        t = new string(t.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());

        // Remove the generic dish prefix if present
        t = System.Text.RegularExpressions.Regex.Replace(t, @"\b(arroz|paella|fideua|fideua)\b", " ");
        t = System.Text.RegularExpressions.Regex.Replace(t, @"\b(del|de|la|el|los|las|un|una|unos|unas)\b", " ");

        // Collapse whitespace
        t = System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ").Trim();
        return t;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static HashSet<string> Tokenize(string normalized)
    {
        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length >= 3) // ignore very small tokens
            .ToHashSet(StringComparer.Ordinal);
    }

    private static int TokenOverlapScore(HashSet<string> requestTokens, HashSet<string> candidateTokens)
    {
        if (requestTokens.Count == 0 || candidateTokens.Count == 0) return 0;
        var overlap = requestTokens.Count(t => candidateTokens.Contains(t));
        return overlap;
    }
}
