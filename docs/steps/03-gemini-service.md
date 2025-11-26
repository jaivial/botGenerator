# Step 03: Gemini Service

In this step, we'll implement the Google AI Studio API client to communicate with Gemini 2.5 Flash.

## 3.1 Understanding the Gemini API

### API Endpoint
```
POST https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}
```

### Request Format
```json
{
  "system_instruction": {
    "parts": [{ "text": "Your system prompt here" }]
  },
  "contents": [
    { "role": "user", "parts": [{ "text": "First user message" }] },
    { "role": "model", "parts": [{ "text": "First AI response" }] },
    { "role": "user", "parts": [{ "text": "Current user message" }] }
  ],
  "generationConfig": {
    "temperature": 0.7,
    "topP": 0.95,
    "topK": 40,
    "maxOutputTokens": 2048
  }
}
```

### Response Format
```json
{
  "candidates": [
    {
      "content": {
        "parts": [{ "text": "The AI response" }],
        "role": "model"
      },
      "finishReason": "STOP"
    }
  ],
  "usageMetadata": {
    "promptTokenCount": 100,
    "candidatesTokenCount": 50,
    "totalTokenCount": 150
  }
}
```

## 3.2 Create the Interface

### src/BotGenerator.Core/Services/IGeminiService.cs

```csharp
using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Service interface for communicating with Google Gemini API.
/// </summary>
public interface IGeminiService
{
    /// <summary>
    /// Generates a response using the Gemini model.
    /// </summary>
    /// <param name="systemPrompt">The system instruction that sets the AI's behavior.</param>
    /// <param name="userMessage">The current user message to respond to.</param>
    /// <param name="history">Optional conversation history for context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated text response.</returns>
    Task<string> GenerateAsync(
        string systemPrompt,
        string userMessage,
        List<ChatMessage>? history = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a response with custom generation configuration.
    /// </summary>
    Task<string> GenerateAsync(
        string systemPrompt,
        string userMessage,
        List<ChatMessage>? history,
        GeminiGenerationConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts tokens in the given text.
    /// Useful for managing context window limits.
    /// </summary>
    Task<int> CountTokensAsync(string text, CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration options for text generation.
/// </summary>
public record GeminiGenerationConfig
{
    /// <summary>
    /// Controls randomness. Lower = more focused, Higher = more creative.
    /// Range: 0.0 to 2.0, Default: 0.7
    /// </summary>
    public double Temperature { get; init; } = 0.7;

    /// <summary>
    /// Nucleus sampling. Consider tokens with top_p probability mass.
    /// Range: 0.0 to 1.0, Default: 0.95
    /// </summary>
    public double TopP { get; init; } = 0.95;

    /// <summary>
    /// Consider only the top K tokens.
    /// Default: 40
    /// </summary>
    public int TopK { get; init; } = 40;

    /// <summary>
    /// Maximum number of tokens to generate.
    /// Default: 2048
    /// </summary>
    public int MaxOutputTokens { get; init; } = 2048;

    /// <summary>
    /// Stop sequences that will halt generation.
    /// </summary>
    public List<string>? StopSequences { get; init; }

    /// <summary>
    /// Default configuration for conversational responses.
    /// </summary>
    public static GeminiGenerationConfig Default => new();

    /// <summary>
    /// More focused/deterministic configuration.
    /// </summary>
    public static GeminiGenerationConfig Focused => new()
    {
        Temperature = 0.3,
        TopP = 0.8,
        TopK = 20
    };

    /// <summary>
    /// More creative configuration.
    /// </summary>
    public static GeminiGenerationConfig Creative => new()
    {
        Temperature = 1.0,
        TopP = 0.95,
        TopK = 60
    };
}
```

## 3.3 Implement the Service

### src/BotGenerator.Core/Services/GeminiService.cs

```csharp
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
```

## 3.4 Register the Service

Update the DI configuration in Program.cs:

### src/BotGenerator.Api/Program.cs (partial)

```csharp
using BotGenerator.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// ... other services ...

// Register Gemini service with HttpClient
builder.Services.AddHttpClient<IGeminiService, GeminiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// ... rest of configuration ...
```

## 3.5 Usage Example

Here's how to use the GeminiService:

```csharp
public class SomeService
{
    private readonly IGeminiService _gemini;

    public SomeService(IGeminiService gemini)
    {
        _gemini = gemini;
    }

    public async Task<string> GetResponseAsync()
    {
        var systemPrompt = @"
            You are a helpful assistant for a restaurant.
            Always respond in Spanish.
            Be friendly and concise.
        ";

        var userMessage = "Quiero reservar una mesa para 4 personas";

        // Without history
        var response = await _gemini.GenerateAsync(
            systemPrompt,
            userMessage);

        // With history
        var history = new List<ChatMessage>
        {
            ChatMessage.FromUser("Hola"),
            ChatMessage.FromAssistant("¡Hola! ¿En qué puedo ayudarte?")
        };

        var responseWithHistory = await _gemini.GenerateAsync(
            systemPrompt,
            userMessage,
            history);

        // With custom config (more focused)
        var focusedResponse = await _gemini.GenerateAsync(
            systemPrompt,
            userMessage,
            history,
            GeminiGenerationConfig.Focused);

        return response;
    }
}
```

## 3.6 Testing the Service

### tests/BotGenerator.Core.Tests/Services/GeminiServiceTests.cs

```csharp
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

namespace BotGenerator.Core.Tests.Services;

public class GeminiServiceTests
{
    private readonly Mock<ILogger<GeminiService>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly IConfiguration _configuration;

    public GeminiServiceTests()
    {
        _loggerMock = new Mock<ILogger<GeminiService>>();
        _httpHandlerMock = new Mock<HttpMessageHandler>();

        var configValues = new Dictionary<string, string?>
        {
            { "GoogleAI:ApiKey", "test-api-key" },
            { "GoogleAI:Model", "gemini-2.5-flash-preview-05-20" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
    }

    [Fact]
    public async Task GenerateAsync_ReturnsResponse_WhenApiSucceeds()
    {
        // Arrange
        var expectedResponse = "Hola, ¿en qué puedo ayudarte?";

        var apiResponse = new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[] { new { text = expectedResponse } },
                        role = "model"
                    },
                    finishReason = "STOP"
                }
            }
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(apiResponse));

        var httpClient = new HttpClient(_httpHandlerMock.Object);
        var service = new GeminiService(httpClient, _configuration, _loggerMock.Object);

        // Act
        var result = await service.GenerateAsync(
            "You are a helpful assistant",
            "Hola");

        // Assert
        Assert.Equal(expectedResponse, result);
    }

    [Fact]
    public async Task GenerateAsync_ThrowsException_WhenApiReturnsError()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.BadRequest, "Invalid request");

        var httpClient = new HttpClient(_httpHandlerMock.Object);
        var service = new GeminiService(httpClient, _configuration, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<GeminiApiException>(() =>
            service.GenerateAsync("System prompt", "User message"));
    }

    [Fact]
    public async Task GenerateAsync_IncludesHistory_WhenProvided()
    {
        // Arrange
        var expectedResponse = "Response with context";
        var apiResponse = new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[] { new { text = expectedResponse } },
                        role = "model"
                    }
                }
            }
        };

        string? capturedBody = null;
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedBody = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(apiResponse))
            });

        var httpClient = new HttpClient(_httpHandlerMock.Object);
        var service = new GeminiService(httpClient, _configuration, _loggerMock.Object);

        var history = new List<ChatMessage>
        {
            ChatMessage.FromUser("Previous message"),
            ChatMessage.FromAssistant("Previous response")
        };

        // Act
        await service.GenerateAsync("System", "Current message", history);

        // Assert
        Assert.NotNull(capturedBody);
        Assert.Contains("Previous message", capturedBody);
        Assert.Contains("Previous response", capturedBody);
        Assert.Contains("Current message", capturedBody);
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
    }
}
```

## 3.7 Get Your API Key

1. Go to [Google AI Studio](https://aistudio.google.com/)
2. Sign in with your Google account
3. Click on "Get API key" in the left sidebar
4. Create a new API key or use an existing one
5. Copy the key and add it to your configuration

**Important**: Never commit your API key to source control. Use:
- User secrets for development
- Environment variables for production
- Azure Key Vault or similar for cloud deployments

```bash
# Add to user secrets
dotnet user-secrets set "GoogleAI:ApiKey" "your-actual-api-key"
```

## Summary

In this step, we:

1. Created the `IGeminiService` interface
2. Implemented `GeminiService` with full error handling
3. Added configuration options for generation parameters
4. Created unit tests for the service
5. Learned how to get and configure the API key

Key features of our implementation:
- Supports system instructions (system prompts)
- Handles conversation history
- Configurable generation parameters
- Proper error handling and logging
- Token counting capability

## Next Step

Continue to [Step 04: Prompt System](./04-prompt-system.md) where we'll build the prompt loader that reads from external files.
