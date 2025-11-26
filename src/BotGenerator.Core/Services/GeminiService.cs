using System.Net.Http.Json;
using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Implementation of IGeminiService for Google AI Studio API.
/// </summary>
public class GeminiService : IGeminiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly GeminiGenerationConfig _defaultConfig;
    private readonly ILogger<GeminiService> _logger;

    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public GeminiService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Load configuration
        _apiKey = configuration["GoogleAI:ApiKey"]
            ?? throw new InvalidOperationException("GoogleAI:ApiKey is not configured");

        _model = configuration["GoogleAI:Model"] ?? "gemini-2.5-flash-preview-05-20";

        // Load default generation config
        _defaultConfig = new GeminiGenerationConfig
        {
            Temperature = configuration.GetValue("GoogleAI:Temperature", 0.7),
            TopP = configuration.GetValue("GoogleAI:TopP", 0.95),
            TopK = configuration.GetValue("GoogleAI:TopK", 40),
            MaxOutputTokens = configuration.GetValue("GoogleAI:MaxOutputTokens", 2048)
        };

        _logger.LogInformation(
            "GeminiService initialized with model: {Model}",
            _model);
    }

    public Task<string> GenerateAsync(
        string systemPrompt,
        string userMessage,
        List<ChatMessage>? history = null,
        CancellationToken cancellationToken = default)
    {
        return GenerateAsync(systemPrompt, userMessage, history, _defaultConfig, cancellationToken);
    }

    public async Task<string> GenerateAsync(
        string systemPrompt,
        string userMessage,
        List<ChatMessage>? history,
        GeminiGenerationConfig config,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/{_model}:generateContent?key={_apiKey}";

        // Build the contents array
        var contents = BuildContents(history, userMessage);

        // Build the request body
        var requestBody = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = contents,
            generationConfig = new
            {
                temperature = config.Temperature,
                topP = config.TopP,
                topK = config.TopK,
                maxOutputTokens = config.MaxOutputTokens,
                stopSequences = config.StopSequences
            }
        };

        _logger.LogDebug(
            "Sending request to Gemini. System prompt length: {SystemLength}, " +
            "User message length: {UserLength}, History count: {HistoryCount}",
            systemPrompt.Length,
            userMessage.Length,
            history?.Count ?? 0);

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                url,
                requestBody,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Gemini API error. Status: {Status}, Response: {Response}",
                    response.StatusCode,
                    errorContent);

                throw new GeminiApiException(
                    $"Gemini API returned {response.StatusCode}",
                    errorContent);
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);

            var text = ExtractResponseText(result);

            _logger.LogDebug(
                "Received Gemini response. Length: {Length}",
                text.Length);

            return text;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling Gemini API");
            throw new GeminiApiException("Failed to connect to Gemini API", ex);
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            _logger.LogWarning("Gemini API request was cancelled");
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Gemini API response");
            throw new GeminiApiException("Invalid response from Gemini API", ex);
        }
    }

    public async Task<int> CountTokensAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/{_model}:countTokens?key={_apiKey}";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = text } }
                }
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                url,
                requestBody,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);

            return result.GetProperty("totalTokens").GetInt32();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to count tokens, returning estimate");
            // Rough estimate: ~4 characters per token
            return text.Length / 4;
        }
    }

    /// <summary>
    /// Builds the contents array from history and current message.
    /// </summary>
    private List<object> BuildContents(List<ChatMessage>? history, string userMessage)
    {
        var contents = new List<object>();

        // Add history messages
        if (history != null && history.Count > 0)
        {
            foreach (var msg in history)
            {
                // Map our roles to Gemini roles
                // "user" -> "user", "assistant" -> "model"
                var role = msg.Role == "assistant" ? "model" : "user";

                contents.Add(new
                {
                    role = role,
                    parts = new[] { new { text = msg.Content } }
                });
            }
        }

        // Add current user message
        contents.Add(new
        {
            role = "user",
            parts = new[] { new { text = userMessage } }
        });

        return contents;
    }

    /// <summary>
    /// Extracts the text response from the Gemini API response.
    /// </summary>
    private string ExtractResponseText(JsonElement result)
    {
        try
        {
            // Navigate: candidates[0].content.parts[0].text
            var candidates = result.GetProperty("candidates");

            if (candidates.GetArrayLength() == 0)
            {
                _logger.LogWarning("Gemini returned no candidates");
                return "";
            }

            var firstCandidate = candidates[0];

            // Check for blocked content
            if (firstCandidate.TryGetProperty("finishReason", out var finishReason))
            {
                var reason = finishReason.GetString();
                if (reason != "STOP" && reason != "MAX_TOKENS")
                {
                    _logger.LogWarning(
                        "Gemini response finished with reason: {Reason}",
                        reason);
                }
            }

            var content = firstCandidate.GetProperty("content");
            var parts = content.GetProperty("parts");

            if (parts.GetArrayLength() == 0)
            {
                _logger.LogWarning("Gemini response has no parts");
                return "";
            }

            return parts[0].GetProperty("text").GetString() ?? "";
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogError(ex, "Unexpected Gemini response structure");
            throw new GeminiApiException("Unexpected response structure from Gemini API");
        }
    }
}

/// <summary>
/// Exception thrown when the Gemini API returns an error.
/// </summary>
public class GeminiApiException : Exception
{
    public string? ResponseContent { get; }

    public GeminiApiException(string message) : base(message) { }

    public GeminiApiException(string message, string responseContent)
        : base(message)
    {
        ResponseContent = responseContent;
    }

    public GeminiApiException(string message, Exception innerException)
        : base(message, innerException) { }
}
