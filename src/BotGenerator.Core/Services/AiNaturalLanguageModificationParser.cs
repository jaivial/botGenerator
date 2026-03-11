using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotGenerator.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BotGenerator.Core.Services;

/// <summary>
/// AI-powered natural language parser for booking modifications.
/// Uses Gemini to extract fields from natural language messages with high accuracy.
/// Includes caching, fallback mechanisms, and comprehensive validation.
/// </summary>
public class AiNaturalLanguageModificationParser : INaturalLanguageModificationParser
{
    private readonly ILogger<AiNaturalLanguageModificationParser> _logger;
    private readonly IGeminiService _geminiService;
    private readonly IPromptLoaderService _promptLoader;
    private readonly IMemoryCache _cache;
    private readonly IFieldValidatorService _fieldValidator;
    private readonly IConfidenceScorerService _confidenceScorer;
    private readonly NaturalLanguageModificationParser _regexFallback;
    
    // Configuration
    private const double ConfidenceThreshold = 0.65;
    private const int CacheDurationMinutes = 5;
    private const int MaxRetries = 2;
    private const int TimeoutMs = 3000;

    public AiNaturalLanguageModificationParser(
        ILogger<AiNaturalLanguageModificationParser> logger,
        IGeminiService geminiService,
        IPromptLoaderService promptLoader,
        IMemoryCache cache,
        IFieldValidatorService fieldValidator,
        IConfidenceScorerService confidenceScorer)
    {
        _logger = logger;
        _geminiService = geminiService;
        _promptLoader = promptLoader;
        _cache = cache;
        _fieldValidator = fieldValidator;
        _confidenceScorer = confidenceScorer;
        _regexFallback = new NaturalLanguageModificationParser(
            logger as ILogger<NaturalLanguageModificationParser> ?? throw new ArgumentNullException(nameof(logger)),
            null!); // DateParserAgent not needed for fallback
    }

    /// <summary>
    /// Extracts all possible booking fields from a natural language message using AI.
    /// Falls back to regex parser if AI fails or returns low confidence.
    /// </summary>
    public Dictionary<string, object> ExtractFields(string userMessage, ModificationState state)
    {
        return ExtractFieldsAsync(userMessage, state).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Async version of ExtractFields for better performance.
    /// </summary>
    public async Task<Dictionary<string, object>> ExtractFieldsAsync(
        string userMessage, 
        ModificationState state,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Check cache first
            var cacheKey = GenerateCacheKey(userMessage, state);
            if (_cache.TryGetValue(cacheKey, out Dictionary<string, object>? cachedResult))
            {
                _logger.LogDebug("Cache hit for message: {Message}", userMessage);
                return cachedResult ?? new Dictionary<string, object>();
            }

            // Try AI extraction with retries
            var result = await ExtractWithAiAsync(userMessage, state, ct);
            
            // Count extracted fields
            var fieldsCount = result.Count(k => !k.Key.StartsWith("_"));
            
            // Calculate confidence
            var confidence = result.TryGetValue("_confidence", out var confObj) ? (double)confObj : 0.0;
            
            // Check if confident enough
            if (_confidenceScorer.IsConfidentEnough(confidence, fieldsCount))
            {
                // Cache successful extraction
                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(CacheDurationMinutes));
                
                stopwatch.Stop();
                _logger.LogInformation(
                    "AI extraction successful: {FieldsCount} fields (confidence: {Confidence}, latency: {Latency}ms)",
                    fieldsCount,
                    confidence,
                    stopwatch.ElapsedMilliseconds);
                
                return result;
            }

            // Low confidence - fall back to regex
            _logger.LogWarning(
                "AI extraction low confidence ({Confidence}) or insufficient fields ({FieldsCount}), falling back to regex",
                confidence,
                fieldsCount);
            
            var fallbackResult = _regexFallback.ExtractFields(userMessage, state);
            
            stopwatch.Stop();
            _logger.LogInformation(
                "Used regex fallback: {Fields} (latency: {Latency}ms)",
                string.Join(", ", fallbackResult.Keys),
                stopwatch.ElapsedMilliseconds);
            
            return fallbackResult;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, 
                "AI extraction failed after {Latency}ms, falling back to regex",
                stopwatch.ElapsedMilliseconds);
            
            // Fall back to regex parser
            return _regexFallback.ExtractFields(userMessage, state);
        }
    }

    /// <summary>
    /// Detects if the user is making a correction using AI.
    /// </summary>
    public bool IsCorrection(string userMessage)
    {
        try
        {
            var cacheKey = $"correction_{GenerateCacheKey(userMessage, null)}";
            if (_cache.TryGetValue(cacheKey, out bool cachedResult))
            {
                return cachedResult;
            }

            // Use AI to detect correction
            var prompt = $@"
Analyze if this message is a CORRECTION to previous information.
A correction typically starts with 'no', 'mejor', 'en realidad', 'cámbialo', etc.

Message: ""{userMessage}""

Return ONLY a JSON object:
{{""is_correction"": true/false, ""confidence"": 0.0-1.0}}

Examples:
- ""no es para el 13"" → {{""is_correction"": true, ""confidence"": 0.95}}
- ""domingo 15"" → {{""is_correction"": false, ""confidence"": 0.9}}
- ""mejor el sábado"" → {{""is_correction"": true, ""confidence"": 0.95}}
";

            var response = _geminiService.GenerateAsync(
                "You are a correction detection AI. Return only valid JSON.",
                prompt,
                null,
                new GeminiGenerationConfig { Temperature = 0.0 },
                CancellationToken.None).Result;

            var result = JsonSerializer.Deserialize<CorrectionResponse>(response);
            var isCorrection = result?.IsCorrection ?? false;

            // Cache result
            _cache.Set(cacheKey, isCorrection, TimeSpan.FromMinutes(CacheDurationMinutes));
            
            _logger.LogDebug("Correction detection: {IsCorrection} (confidence: {Confidence})",
                isCorrection, result?.Confidence ?? 0);

            return isCorrection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI correction detection failed, using regex fallback");
            return _regexFallback.IsCorrection(userMessage);
        }
    }

    /// <summary>
    /// Infers the user's goal from the message using AI.
    /// </summary>
    public string? InferUserGoal(string userMessage, ModificationState state)
    {
        try
        {
            var cacheKey = $"goal_{GenerateCacheKey(userMessage, state)}";
            if (_cache.TryGetValue(cacheKey, out string? cachedGoal))
            {
                return cachedGoal;
            }

            var bookingContext = state.SelectedBooking?.Summary ?? "No booking selected";
            
            var prompt = $@"
Analyze the user's goal from this message in the context of a restaurant booking modification.

Current booking: {bookingContext}
User message: ""{userMessage}""

Return ONLY a JSON object with the user's goal:
{{""goal"": ""GOAL_TYPE"", ""confidence"": 0.0-1.0}}

Possible goals:
- change_date: User wants to change the date
- change_time: User wants to change the time
- change_both: User wants to change both date and time
- change_party_size: User wants to change number of people
- add_rice: User wants to add/modify rice order
- cancel: User wants to cancel
- unclear: Cannot determine goal

Examples:
- ""domingo 15 a las 14:30"" → {{""goal"": ""change_both"", ""confidence"": 0.95}}
- ""más tarde"" → {{""goal"": ""change_time"", ""confidence"": 0.9}}
- ""para 8 personas"" → {{""goal"": ""change_party_size"", ""confidence"": 0.95}}
";

            var response = _geminiService.GenerateAsync(
                "You are a goal inference AI. Return only valid JSON.",
                prompt,
                null,
                new GeminiGenerationConfig { Temperature = 0.0 },
                CancellationToken.None).Result;

            var result = JsonSerializer.Deserialize<GoalResponse>(response);
            var goal = result?.Goal;

            // Cache result
            if (!string.IsNullOrEmpty(goal))
            {
                _cache.Set(cacheKey, goal, TimeSpan.FromMinutes(CacheDurationMinutes));
            }

            _logger.LogDebug("Goal inference: {Goal} (confidence: {Confidence})",
                goal, result?.Confidence ?? 0);

            return goal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI goal inference failed, using regex fallback");
            return _regexFallback.InferUserGoal(userMessage, state);
        }
    }

    // ========== PRIVATE METHODS ==========

    private async Task<Dictionary<string, object>> ExtractWithAiAsync(
        string userMessage,
        ModificationState state,
        CancellationToken ct)
    {
        var bookingContext = state.SelectedBooking?.Summary ?? "No booking selected";
        var lastAskedField = state.LastAskedField ?? "none";
        var conversationTurn = state.ConversationTurn;

        // Load prompt template
        var systemPrompt = await _promptLoader.LoadPromptAsync(
            "components",
            "nl-modification-extraction.txt");

        var userPrompt = $@"
Extract booking modification fields from this message:

**Current Booking**: {bookingContext}
**Last Asked Field**: {lastAskedField}
**Conversation Turn**: {conversationTurn}
**User Message**: ""{userMessage}""

Return ONLY valid JSON with extracted fields and confidence score.
";

        // Try extraction with retries
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeoutMs);
                var linkedCt = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);

                var response = await _geminiService.GenerateAsync(
                    systemPrompt,
                    userPrompt,
                    null,
                    new GeminiGenerationConfig { Temperature = 0.0 },
                    linkedCt.Token);

                // Parse JSON response
                var aiResponse = ParseAiResponse(response);
                
                // Validate and convert fields
                var result = ValidateAndConvertFields(aiResponse, state);
                
                return result;
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("AI extraction timeout on attempt {Attempt}", attempt + 1);
                if (attempt == MaxRetries - 1)
                    throw;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "AI returned invalid JSON on attempt {Attempt}", attempt + 1);
                if (attempt == MaxRetries - 1)
                    throw;
            }
        }

        throw new Exception("AI extraction failed after all retries");
    }

    private AiExtractionResponse ParseAiResponse(string response)
    {
        // Clean response (remove markdown code blocks if present)
        var cleanedResponse = response
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();

        var aiResponse = JsonSerializer.Deserialize<AiExtractionResponse>(cleanedResponse);
        
        if (aiResponse == null)
        {
            throw new JsonException("Failed to deserialize AI response");
        }

        // Validate response structure
        if (!aiResponse.IsValid())
        {
            _logger.LogWarning("AI response validation failed: {Reasoning}", aiResponse.Reasoning);
        }

        return aiResponse;
    }

    private Dictionary<string, object> ValidateAndConvertFields(AiExtractionResponse aiResponse, ModificationState state)
    {
        var result = new Dictionary<string, object>();

        // Validate and add date
        if (!string.IsNullOrEmpty(aiResponse.Date))
        {
            var (isValid, dateValue, error) = _fieldValidator.ValidateDate(aiResponse.Date);
            if (isValid && dateValue.HasValue)
            {
                result["date"] = dateValue.Value;
            }
            else
            {
                _logger.LogWarning("Date validation failed: {Error}", error);
            }
        }

        // Validate and add time
        if (!string.IsNullOrEmpty(aiResponse.Time))
        {
            var (isValid, timeValue, error) = _fieldValidator.ValidateTime(aiResponse.Time);
            if (isValid && timeValue.HasValue)
            {
                result["time"] = timeValue.Value;
            }
            else
            {
                _logger.LogWarning("Time validation failed: {Error}", error);
            }
        }

        // Validate and add party size
        if (aiResponse.PartySize.HasValue)
        {
            var (isValid, value, error) = _fieldValidator.ValidatePartySize(aiResponse.PartySize);
            if (isValid && value.HasValue)
            {
                result["party_size"] = value.Value;
            }
            else
            {
                _logger.LogWarning("Party size validation failed: {Error}", error);
            }
        }

        // Add rice type
        if (!string.IsNullOrEmpty(aiResponse.RiceType))
        {
            result["rice_type"] = aiResponse.RiceType;
        }

        // Validate and add rice servings
        if (aiResponse.RiceServings.HasValue)
        {
            var (isValid, value, error) = _fieldValidator.ValidateRiceServings(aiResponse.RiceServings);
            if (isValid && value.HasValue)
            {
                result["rice_servings"] = value.Value;
            }
            else
            {
                _logger.LogWarning("Rice servings validation failed: {Error}", error);
            }
        }

        // Validate and add tronas
        if (aiResponse.Tronas.HasValue)
        {
            var (isValid, value, error) = _fieldValidator.ValidateTronas(aiResponse.Tronas);
            if (isValid && value.HasValue)
            {
                result["tronas"] = value.Value;
            }
            else
            {
                _logger.LogWarning("Tronas validation failed: {Error}", error);
            }
        }

        // Validate and add carritos
        if (aiResponse.Carritos.HasValue)
        {
            var (isValid, value, error) = _fieldValidator.ValidateCarritos(aiResponse.Carritos);
            if (isValid && value.HasValue)
            {
                result["carritos"] = value.Value;
            }
            else
            {
                _logger.LogWarning("Carritos validation failed: {Error}", error);
            }
        }

        // Calculate confidence using scorer
        var calculatedConfidence = _confidenceScorer.CalculateConfidence(aiResponse, state.PreviousBotQuestion ?? "");
        result["_confidence"] = calculatedConfidence;
        result["_reasoning"] = aiResponse.Reasoning ?? "";
        result["_is_correction"] = aiResponse.IsCorrection;
        result["_user_goal"] = aiResponse.UserGoal ?? "";

        _logger.LogDebug(
            "Validated fields: {Fields}, confidence: {Confidence}, level: {Level}",
            string.Join(", ", result.Keys.Where(k => !k.StartsWith("_"))),
            calculatedConfidence,
            _confidenceScorer.GetConfidenceLevel(calculatedConfidence));

        return result;
    }

    private string GenerateCacheKey(string userMessage, ModificationState? state)
    {
        var input = $"{userMessage}|{state?.SelectedBooking?.Id ?? 0}|{state?.LastAskedField ?? ""}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hash)[..16]; // Use first 16 chars as key
    }

    // ========== RESPONSE DTOs ==========

    private class CorrectionResponse
    {
        public bool IsCorrection { get; set; }
        public double Confidence { get; set; }
    }

    private class GoalResponse
    {
        public string? Goal { get; set; }
        public double Confidence { get; set; }
    }
}
