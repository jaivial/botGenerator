using BotGenerator.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BotGenerator.Core.Services;

/// <summary>
/// Implementation of IGeminiService using MiniMax M2.7 via Anthropic-compatible API.
/// Drop-in replacement for GeminiService with better handling of long prompts.
/// </summary>
public class MinimaxService : IGeminiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;
    private readonly double _temperature;
    private readonly double _topP;
    private readonly int _maxOutputTokens;
    private readonly ILogger<MinimaxService> _logger;

    public MinimaxService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<MinimaxService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _apiKey = configuration["Minimax:ApiKey"]
            ?? throw new InvalidOperationException("Minimax:ApiKey must be configured");

        _model = configuration["Minimax:Model"] ?? "MiniMax-M2.7-highspeed";
        _baseUrl = configuration["Minimax:BaseUrl"] ?? "https://api.minimax.io/anthropic/v1";

        _temperature = configuration.GetValue("Minimax:Temperature", 0.7);
        _topP = configuration.GetValue("Minimax:TopP", 0.95);
        _maxOutputTokens = configuration.GetValue("Minimax:MaxOutputTokens", 2048);

        _logger.LogInformation(
            "MinimaxService initialized with model: {Model}, baseUrl: {BaseUrl}",
            _model, _baseUrl);
    }

    public Task<string> GenerateAsync(
        string systemPrompt,
        string userMessage,
        List<ChatMessage>? history = null,
        CancellationToken cancellationToken = default)
    {
        return CallAnthropicApiAsync(systemPrompt, userMessage, history, _maxOutputTokens, _temperature, cancellationToken);
    }

    public Task<string> GenerateAsync(
        string systemPrompt,
        string userMessage,
        List<ChatMessage>? history,
        GeminiGenerationConfig config,
        CancellationToken cancellationToken)
    {
        return CallAnthropicApiAsync(systemPrompt, userMessage, history, config.MaxOutputTokens, config.Temperature, cancellationToken);
    }

    public Task<int> CountTokensAsync(string text, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(text.Length / 4);
    }

    private async Task<string> CallAnthropicApiAsync(
        string systemPrompt,
        string userMessage,
        List<ChatMessage>? history,
        int maxTokens,
        double temperature,
        CancellationToken cancellationToken)
    {
        var url = $"{_baseUrl}/messages";

        var messages = new List<object>();

        if (history != null)
        {
            foreach (var msg in history)
            {
                var role = msg.Role == "assistant" ? "assistant" : "user";
                messages.Add(new { role, content = msg.Content });
            }
        }

        messages.Add(new { role = "user", content = userMessage });

        var requestBody = new Dictionary<string, object>
        {
            ["model"] = _model,
            ["max_tokens"] = maxTokens,
            ["temperature"] = temperature,
            ["system"] = systemPrompt,
            ["messages"] = messages
        };

        _logger.LogDebug(
            "Sending Anthropic-compatible request. System prompt length: {SystemLength}, " +
            "User message length: {UserLength}, History count: {HistoryCount}",
            systemPrompt.Length,
            userMessage.Length,
            history?.Count ?? 0);

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(requestBody)
            };
            request.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
            request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "MiniMax API error. Status: {Status}, Response: {Response}",
                    response.StatusCode,
                    errorContent);

                throw new MinimaxApiException(
                    $"MiniMax API returned {response.StatusCode}",
                    errorContent);
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);

            _logger.LogDebug("MiniMax raw response: {Response}", result.ToString());

            return ExtractResponseText(result);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling MiniMax API");
            throw new MinimaxApiException("Failed to connect to MiniMax API", ex);
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            _logger.LogWarning("MiniMax API request was cancelled");
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse MiniMax API response");
            throw new MinimaxApiException("Invalid response from MiniMax API", ex);
        }
    }

    private string ExtractResponseText(JsonElement result)
    {
        try
        {
            if (!result.TryGetProperty("content", out var content))
            {
                _logger.LogWarning("MiniMax response missing 'content' property");
                return "";
            }

            if (content.ValueKind != JsonValueKind.Array || content.GetArrayLength() == 0)
            {
                _logger.LogWarning("MiniMax response 'content' is empty or not an array");
                return "";
            }

            // The model may return multiple content blocks: thinking blocks + text blocks.
            // We need to find the first block with type="text" that has a "text" field.
            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var typeProp) &&
                    typeProp.GetString() == "text" &&
                    block.TryGetProperty("text", out var textProp))
                {
                    return textProp.GetString() ?? "";
                }
            }

            _logger.LogWarning("MiniMax response has no text content block");
            return "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected MiniMax response structure");
            throw new MinimaxApiException("Unexpected response structure from MiniMax API");
        }
    }
}

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
