using BotGenerator.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BotGenerator.Core.Services;

/// <summary>
/// Implementation of IGeminiService using MiniMax M2.7 via Anthropic-compatible API.
/// Supports both text generation and tool calling.
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

        _model = configuration["Minimax:Model"] ?? "MiniMax-M3";
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
        return CallAnthropicApiAsync(systemPrompt, userMessage, history, _maxOutputTokens, _temperature, tools: null, toolChoice: null, cancellationToken);
    }

    public Task<string> GenerateAsync(
        string systemPrompt,
        string userMessage,
        List<ChatMessage>? history,
        GeminiGenerationConfig config,
        CancellationToken cancellationToken)
    {
        return CallAnthropicApiAsync(systemPrompt, userMessage, history, config.MaxOutputTokens, config.Temperature, tools: null, toolChoice: null, cancellationToken);
    }

    public async Task<AnthropicResponse> GenerateWithToolsAsync(
        string systemPrompt,
        string userMessage,
        List<ChatMessage>? history,
        List<ToolDefinition> tools,
        ToolChoiceConfig? toolChoice = null,
        GeminiGenerationConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        var text = await CallAnthropicApiAsync(
            systemPrompt, userMessage, history,
            config?.MaxOutputTokens ?? 1024,
            config?.Temperature ?? 0.1,
            tools, toolChoice, cancellationToken);

        // The raw text is actually the full JSON response — we need to parse it differently
        // Actually, we need a separate code path that returns the parsed response
        // Let's use the direct API call method instead
        return await CallAnthropicApiStructuredAsync(
            systemPrompt, userMessage, history,
            config?.MaxOutputTokens ?? 1024,
            config?.Temperature ?? 0.1,
            tools, toolChoice, cancellationToken);
    }

    public async Task<AnthropicResponse> ContinueWithToolResultAsync(
        string systemPrompt,
        List<object> messages,
        List<ToolDefinition> tools,
        ToolChoiceConfig? toolChoice = null,
        GeminiGenerationConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/messages";

        var requestBody = new Dictionary<string, object>
        {
            ["model"] = _model,
            ["max_tokens"] = config?.MaxOutputTokens ?? 1024,
            ["temperature"] = config?.Temperature ?? 0.1,
            ["system"] = systemPrompt,
            ["messages"] = messages
        };

        AddToolsToRequest(requestBody, tools, toolChoice);

        var request = CreateRequest(url, requestBody);
        var response = await _httpClient.SendAsync(request, cancellationToken);
        var result = await HandleResponse(response, cancellationToken);

        return ParseStructuredResponse(result);
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
        List<ToolDefinition>? tools,
        ToolChoiceConfig? toolChoice,
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

        AddToolsToRequest(requestBody, tools, toolChoice);

        _logger.LogDebug(
            "Sending Anthropic-compatible request. System prompt length: {SystemLength}, " +
            "User message length: {UserLength}, History count: {HistoryCount}, HasTools: {HasTools}",
            systemPrompt.Length,
            userMessage.Length,
            history?.Count ?? 0,
            tools?.Count > 0);

        try
        {
            var request = CreateRequest(url, requestBody);
            var response = await _httpClient.SendAsync(request, cancellationToken);
            var result = await HandleResponse(response, cancellationToken);

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

    private async Task<AnthropicResponse> CallAnthropicApiStructuredAsync(
        string systemPrompt,
        string userMessage,
        List<ChatMessage>? history,
        int maxTokens,
        double temperature,
        List<ToolDefinition> tools,
        ToolChoiceConfig? toolChoice,
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

        AddToolsToRequest(requestBody, tools, toolChoice);

        try
        {
            var request = CreateRequest(url, requestBody);
            var response = await _httpClient.SendAsync(request, cancellationToken);
            var result = await HandleResponse(response, cancellationToken);

            return ParseStructuredResponse(result);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling MiniMax API");
            throw new MinimaxApiException("Failed to connect to MiniMax API", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse MiniMax API response");
            throw new MinimaxApiException("Invalid response from MiniMax API", ex);
        }
    }

    private void AddToolsToRequest(
        Dictionary<string, object> requestBody,
        List<ToolDefinition>? tools,
        ToolChoiceConfig? toolChoice)
    {
        if (tools == null || tools.Count == 0) return;

        var toolsArray = tools.Select(t => new Dictionary<string, object>
        {
            ["name"] = t.Name,
            ["description"] = t.Description,
            ["input_schema"] = t.InputSchema
        }).ToList();

        requestBody["tools"] = toolsArray;

        if (toolChoice != null)
        {
            if (toolChoice.Type == "tool" && !string.IsNullOrEmpty(toolChoice.ToolName))
            {
                requestBody["tool_choice"] = new { type = "tool", name = toolChoice.ToolName };
            }
            else
            {
                requestBody["tool_choice"] = new { type = toolChoice.Type };
            }
        }
    }

    private HttpRequestMessage CreateRequest(string url, Dictionary<string, object> body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
        request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        return request;
    }

    private async Task<JsonElement> HandleResponse(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "MiniMax API error. Status: {Status}, Response: {Response}",
                response.StatusCode,
                errorContent);

            throw new MinimaxApiException(
                $"MiniMax API returned {response.StatusCode}",
                errorContent);
        }

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        _logger.LogDebug("MiniMax raw response: {Response}", result.ToString());
        return result;
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

    private AnthropicResponse ParseStructuredResponse(JsonElement result)
    {
        try
        {
            var blocks = new List<ContentBlock>();
            var stopReason = "end_turn";

            if (result.TryGetProperty("stop_reason", out var sr))
                stopReason = sr.GetString() ?? "end_turn";

            if (!result.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.Array)
            {
                return new AnthropicResponse
                {
                    Content = blocks,
                    StopReason = stopReason
                };
            }

            foreach (var block in content.EnumerateArray())
            {
                var type = block.TryGetProperty("type", out var tp) ? tp.GetString() : "";
                if (type == "text")
                {
                    var text = block.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                    blocks.Add(new TextBlock(text));
                }
                else if (type == "tool_use")
                {
                    var id = block.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
                    var name = block.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var input = block.TryGetProperty("input", out var inp) ? inp : default;
                    blocks.Add(new ToolUseBlock(id, name, input));
                }
            }

            return new AnthropicResponse
            {
                Content = blocks,
                StopReason = stopReason
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse structured Anthropic response");
            return new AnthropicResponse { StopReason = "end_turn" };
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
