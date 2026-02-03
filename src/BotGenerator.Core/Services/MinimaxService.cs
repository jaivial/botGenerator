using BotGenerator.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BotGenerator.Core.Services;

/// <summary>
/// Implementation of IMinimaxService and IGeminiService for Minimax M2.1 API.
/// Provides better handling of long prompts and system messages compared to Gemini.
/// This service can be used as a drop-in replacement for GeminiService.
/// </summary>
public class MinimaxService : IMinimaxService, IGeminiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;
    private readonly MinimaxGenerationConfig _defaultConfig;
    private readonly ILogger<MinimaxService> _logger;

    public MinimaxService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<MinimaxService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Load configuration - check both Minimax and GoogleAI sections for compatibility
        _apiKey = configuration["Minimax:ApiKey"]
            ?? configuration["GoogleAI:ApiKey"]
            ?? throw new InvalidOperationException("Minimax:ApiKey or GoogleAI:ApiKey must be configured");

        _model = configuration["Minimax:Model"] ?? "MiniMax-M2-1";
        _baseUrl = configuration["Minimax:BaseUrl"] ?? "https://api.minimaxi.chat/v1";

        // Load default generation config
        _defaultConfig = new MinimaxGenerationConfig
        {
            Temperature = configuration.GetValue("Minimax:Temperature", 0.7),
            TopP = configuration.GetValue("Minimax:TopP", 0.95),
            MaxOutputTokens = configuration.GetValue("Minimax:MaxOutputTokens", 2048)
        };

        _logger.LogInformation(
            "MinimaxService initialized with model: {Model}, baseUrl: {BaseUrl}",
            _model, _baseUrl);
    }

    #region IMinimaxService Implementation

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
        MinimaxGenerationConfig config,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/text/chatcompletion_v2";

        // Build messages array
        var messages = BuildMessages(systemPrompt, history, userMessage);

        // Build the request body
        var requestBody = new
        {
            model = _model,
            messages = messages,
            temperature = config.Temperature,
            top_p = config.TopP,
            max_tokens = config.MaxOutputTokens
        };

        _logger.LogDebug(
            "Sending request to Minimax. System prompt length: {SystemLength}, " +
            "User message length: {UserLength}, History count: {HistoryCount}",
            systemPrompt.Length,
            userMessage.Length,
            history?.Count ?? 0);

        try
        {
            // Create request with authorization header
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(requestBody)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Minimax API error. Status: {Status}, Response: {Response}",
                    response.StatusCode,
                    errorContent);

                throw new MinimaxApiException(
                    $"Minimax API returned {response.StatusCode}",
                    errorContent);
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);

            var text = ExtractResponseText(result);

            _logger.LogDebug(
                "Received Minimax response. Length: {Length}",
                text.Length);

            return text;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling Minimax API");
            throw new MinimaxApiException("Failed to connect to Minimax API", ex);
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            _logger.LogWarning("Minimax API request was cancelled");
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Minimax API response");
            throw new MinimaxApiException("Invalid response from Minimax API", ex);
        }
    }

    #endregion

    #region IGeminiService Implementation (Adapter)

    /// <summary>
    /// Adapter method to implement IGeminiService using Minimax backend.
    /// This allows MinimaxService to be used as a drop-in replacement for GeminiService.
    /// </summary>
    Task<string> IGeminiService.GenerateAsync(
        string systemPrompt,
        string userMessage,
        List<ChatMessage>? history,
        CancellationToken cancellationToken)
    {
        // Map Gemini config to Minimax config
        var config = new MinimaxGenerationConfig
        {
            Temperature = _defaultConfig.Temperature,
            TopP = _defaultConfig.TopP,
            MaxOutputTokens = _defaultConfig.MaxOutputTokens
        };

        return GenerateAsync(systemPrompt, userMessage, history, config, cancellationToken);
    }

    /// <summary>
    /// Adapter method with GeminiGenerationConfig.
    /// Maps Gemini config to Minimax config.
    /// </summary>
    Task<string> IGeminiService.GenerateAsync(
        string systemPrompt,
        string userMessage,
        List<ChatMessage>? history,
        GeminiGenerationConfig config,
        CancellationToken cancellationToken)
    {
        // Map Gemini config to Minimax config
        var minimaxConfig = new MinimaxGenerationConfig
        {
            Temperature = config.Temperature,
            TopP = config.TopP,
            MaxOutputTokens = config.MaxOutputTokens
        };

        return GenerateAsync(systemPrompt, userMessage, history, minimaxConfig, cancellationToken);
    }

    /// <summary>
    /// Token counting - uses the same estimation method.
    /// </summary>
    async Task<int> IGeminiService.CountTokensAsync(string text, CancellationToken cancellationToken)
    {
        return await CountTokensAsync(text, cancellationToken);
    }

    #endregion

    #region Common Methods

    public async Task<int> CountTokensAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        // Minimax doesn't have a token counting endpoint like Gemini
        // We'll use a rough estimate: ~4 characters per token for Spanish text
        // This is a reasonable approximation for planning purposes
        await Task.CompletedTask;
        return text.Length / 4;
    }

    /// <summary>
    /// Builds the messages array for Minimax API.
    /// Minimax uses OpenAI-compatible format with system, user, and assistant roles.
    /// </summary>
    private List<Dictionary<string, string>> BuildMessages(
        string systemPrompt,
        List<ChatMessage>? history,
        string currentUserMessage)
    {
        var messages = new List<Dictionary<string, string>>();

        // Add system message
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new Dictionary<string, string>
            {
                ["role"] = "system",
                ["content"] = systemPrompt
            });
        }

        // Add history messages
        if (history != null && history.Count > 0)
        {
            foreach (var msg in history)
            {
                // Map our roles to Minimax roles
                // "user" -> "user", "assistant" -> "assistant"
                var role = msg.Role == "assistant" ? "assistant" : "user";

                messages.Add(new Dictionary<string, string>
                {
                    ["role"] = role,
                    ["content"] = msg.Content
                });
            }
        }

        // Add current user message
        messages.Add(new Dictionary<string, string>
        {
            ["role"] = "user",
            ["content"] = currentUserMessage
        });

        return messages;
    }

    /// <summary>
    /// Extracts the text response from the Minimax API response.
    /// </summary>
    private string ExtractResponseText(JsonElement result)
    {
        try
        {
            // Minimax response format:
            // {
            //   "choices": [
            //     {
            //       "message": {
            //         "role": "assistant",
            //         "content": "..."
            //       }
            //     }
            //   ]
            // }

            if (!result.TryGetProperty("choices", out var choices))
            {
                _logger.LogWarning("Minimax response missing 'choices' property");
                return "";
            }

            if (choices.GetArrayLength() == 0)
            {
                _logger.LogWarning("Minimax returned no choices");
                return "";
            }

            var firstChoice = choices[0];

            if (!firstChoice.TryGetProperty("message", out var message))
            {
                _logger.LogWarning("Minimax response missing 'message' property");
                return "";
            }

            if (!message.TryGetProperty("content", out var content))
            {
                _logger.LogWarning("Minimax response missing 'content' property");
                return "";
            }

            return content.GetString() ?? "";
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogError(ex, "Unexpected Minimax response structure");
            throw new MinimaxApiException("Unexpected response structure from Minimax API");
        }
    }

    #endregion
}

/// <summary>
/// Exception thrown when the Minimax API returns an error.
/// </summary>
public class MinimaxApiException : Exception
{
    public string? ResponseContent { get; }

    public MinimaxApiException(string message) : base(message) { }

    public MinimaxApiException(string message, string responseContent)
        : base(message)
    {
        ResponseContent = responseContent;
    }

    public MinimaxApiException(string message, Exception innerException)
        : base(message, innerException) { }
}
