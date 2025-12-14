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
    /// Validates a rice request using Gemini AI for natural language matching.
    /// Returns structured result with true/false and matched rice name.
    /// </summary>
    public async Task<RiceValidationResult> ValidateAsync(
        string userRiceRequest,
        string restaurantId,
        CancellationToken cancellationToken = default)
    {
        // Fetch available rice types from database
        var availableTypes = await GetAvailableRiceTypesAsync(cancellationToken);
        if (availableTypes.Count == 0)
        {
            _logger.LogWarning("No rice types found in database");
            return RiceValidationResult.NotFound(userRiceRequest);
        }

        _logger.LogInformation(
            "Validating rice request '{Request}' against {Count} available types using AI",
            userRiceRequest,
            availableTypes.Count);

        // Build the prompt for Gemini AI
        var riceListFormatted = string.Join("\n", availableTypes.Select((r, i) => $"{i + 1}. {r}"));

        var systemPrompt = @"Eres un asistente especializado en validar pedidos de arroz para un restaurante español.

Tu tarea es determinar si la petición del cliente coincide con alguno de los arroces disponibles en el menú.
Debes entender el lenguaje natural español, incluyendo:
- Variaciones ortográficas (señoret/señorito, fideua/fideuá)
- Abreviaciones (arroz negro = arroz negro, paella valenciana = paella valenciana de la albufera)
- Ingredientes mencionados (chorizo → arroz de chorizo, marisco → fideuá de marisco)
- Sinónimos y formas coloquiales

REGLAS:
1. Si la petición coincide claramente con UN arroz del menú, responde: TRUE|[nombre exacto del arroz del menú]
2. Si la petición podría coincidir con VARIOS arroces, responde: MULTIPLE|[arroz1]|[arroz2]|...
3. Si la petición NO coincide con ningún arroz del menú, responde: FALSE

Solo responde con el formato indicado, sin explicaciones adicionales.";

        var userMessage = $@"ARROCES DISPONIBLES EN EL MENÚ:
{riceListFormatted}

PETICIÓN DEL CLIENTE:
""{userRiceRequest}""

¿La petición coincide con algún arroz del menú?";

        try
        {
            // Use focused configuration for more deterministic results
            var config = new GeminiGenerationConfig
            {
                Temperature = 0.1,
                TopP = 0.8,
                TopK = 10,
                MaxOutputTokens = 256
            };

            var aiResponse = await _gemini.GenerateAsync(
                systemPrompt,
                userMessage,
                null,
                config,
                cancellationToken);

            _logger.LogDebug("AI rice validation response: {Response}", aiResponse);

            return ParseAiValidationResponse(aiResponse, userRiceRequest, availableTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Gemini for rice validation, falling back to pattern matching");
            // Fallback to simple pattern matching if AI fails
            return FallbackPatternMatch(userRiceRequest, availableTypes);
        }
    }

    /// <summary>
    /// Parses the AI response and returns the appropriate validation result.
    /// </summary>
    private RiceValidationResult ParseAiValidationResponse(
        string aiResponse,
        string originalRequest,
        List<string> availableTypes)
    {
        var response = aiResponse.Trim().ToUpperInvariant();

        // Check for TRUE|rice_name format
        if (response.StartsWith("TRUE|"))
        {
            var riceName = aiResponse.Trim().Substring(5).Trim();
            // Verify the rice name exists in our list (case-insensitive)
            var matchedRice = availableTypes.FirstOrDefault(r =>
                r.Equals(riceName, StringComparison.OrdinalIgnoreCase));

            if (matchedRice != null)
            {
                _logger.LogInformation("AI validated rice: {Rice}", matchedRice);
                return RiceValidationResult.Valid(matchedRice, originalRequest);
            }

            // If AI returned a name not in list, try to find closest match
            var closest = availableTypes.FirstOrDefault(r =>
                r.Contains(riceName, StringComparison.OrdinalIgnoreCase) ||
                riceName.Contains(r, StringComparison.OrdinalIgnoreCase));

            if (closest != null)
            {
                _logger.LogInformation("AI validated rice (closest match): {Rice}", closest);
                return RiceValidationResult.Valid(closest, originalRequest);
            }

            _logger.LogWarning("AI returned TRUE but rice '{Rice}' not found in menu", riceName);
            return RiceValidationResult.NotFound(originalRequest);
        }

        // Check for MULTIPLE|rice1|rice2|... format
        if (response.StartsWith("MULTIPLE|"))
        {
            var parts = aiResponse.Trim().Substring(9).Split('|')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            // Verify each rice exists in our list
            var validOptions = parts
                .Select(p => availableTypes.FirstOrDefault(r =>
                    r.Equals(p, StringComparison.OrdinalIgnoreCase) ||
                    r.Contains(p, StringComparison.OrdinalIgnoreCase)))
                .Where(r => r != null)
                .Distinct()
                .ToList()!;

            if (validOptions.Count > 0)
            {
                _logger.LogInformation("AI found multiple matches: {Options}", string.Join(", ", validOptions));
                return RiceValidationResult.Multiple(validOptions!, originalRequest);
            }

            return RiceValidationResult.NotFound(originalRequest);
        }

        // Check for FALSE
        if (response.StartsWith("FALSE"))
        {
            _logger.LogInformation("AI determined rice '{Request}' is not in menu", originalRequest);
            return RiceValidationResult.NotFound(originalRequest);
        }

        // Unexpected format - log and return not found
        _logger.LogWarning("Unexpected AI response format: {Response}", aiResponse);
        return RiceValidationResult.NotFound(originalRequest);
    }

    /// <summary>
    /// Fallback pattern matching when AI is unavailable.
    /// </summary>
    private RiceValidationResult FallbackPatternMatch(string userRiceRequest, List<string> availableTypes)
    {
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

        // Fallback: keyword match
        var requestTokens = Tokenize(normalizedRequest);
        var scored = availableTypes
            .Select(a => new { Name = a, Score = TokenOverlapScore(requestTokens, Tokenize(NormalizeForComparison(a))) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ToList();

        if (scored.Count == 0)
            return RiceValidationResult.NotFound(userRiceRequest);

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
