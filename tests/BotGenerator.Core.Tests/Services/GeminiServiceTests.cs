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

    /// <summary>
    /// Step 73: GeminiService_SendsRequest_CorrectFormat
    /// The request body should be formatted correctly with contents, systemInstruction, and generationConfig.
    /// </summary>
    [Fact]
    public async Task GeminiService_SendsRequest_CorrectFormat()
    {
        // Arrange
        var expectedResponse = "Test response";
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

        string? capturedRequestBody = null;
        string? capturedUrl = null;

        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedUrl = req.RequestUri?.ToString();
                capturedRequestBody = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(apiResponse))
            });

        var httpClient = new HttpClient(_httpHandlerMock.Object);
        var service = new GeminiService(httpClient, _configuration, _loggerMock.Object);

        var systemPrompt = "You are a helpful restaurant assistant";
        var userMessage = "Quiero reservar una mesa";
        var history = new List<ChatMessage>
        {
            ChatMessage.FromUser("Hola"),
            ChatMessage.FromAssistant("¡Hola! ¿En qué puedo ayudarte?")
        };

        // Act
        await service.GenerateAsync(systemPrompt, userMessage, history);

        // Assert
        Assert.NotNull(capturedRequestBody);
        Assert.NotNull(capturedUrl);

        // Verify URL contains API key
        Assert.Contains("key=test-api-key", capturedUrl);
        Assert.Contains("gemini-2.5-flash-preview-05-20:generateContent", capturedUrl);

        // Parse the captured request body
        var requestJson = JsonDocument.Parse(capturedRequestBody);
        var root = requestJson.RootElement;

        // Verify system_instruction structure
        Assert.True(root.TryGetProperty("system_instruction", out var sysInstruction));
        Assert.True(sysInstruction.TryGetProperty("parts", out var sysParts));
        Assert.Equal(1, sysParts.GetArrayLength());
        Assert.Equal(systemPrompt, sysParts[0].GetProperty("text").GetString());

        // Verify contents structure (should have history + current message)
        Assert.True(root.TryGetProperty("contents", out var contents));
        Assert.Equal(3, contents.GetArrayLength()); // 2 history + 1 current

        // First content (user history)
        var firstContent = contents[0];
        Assert.Equal("user", firstContent.GetProperty("role").GetString());
        Assert.Equal("Hola", firstContent.GetProperty("parts")[0].GetProperty("text").GetString());

        // Second content (assistant history, mapped to "model")
        var secondContent = contents[1];
        Assert.Equal("model", secondContent.GetProperty("role").GetString());
        Assert.Equal("¡Hola! ¿En qué puedo ayudarte?",
            secondContent.GetProperty("parts")[0].GetProperty("text").GetString());

        // Third content (current user message)
        var thirdContent = contents[2];
        Assert.Equal("user", thirdContent.GetProperty("role").GetString());
        Assert.Equal(userMessage, thirdContent.GetProperty("parts")[0].GetProperty("text").GetString());

        // Verify generationConfig structure
        Assert.True(root.TryGetProperty("generationConfig", out var genConfig));
        Assert.True(genConfig.TryGetProperty("temperature", out _));
        Assert.True(genConfig.TryGetProperty("topP", out _));
        Assert.True(genConfig.TryGetProperty("topK", out _));
        Assert.True(genConfig.TryGetProperty("maxOutputTokens", out _));
    }

    /// <summary>
    /// Step 74: GeminiService_ParsesResponse_ExtractsText
    /// The service should correctly extract text from candidates[0].content.parts[0].text.
    /// </summary>
    [Fact]
    public async Task GeminiService_ParsesResponse_ExtractsText()
    {
        // Arrange
        var expectedText = "Por supuesto! Puedo ayudarte a reservar una mesa. ¿Para cuántas personas?";

        var apiResponse = new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[]
                        {
                            new { text = expectedText }
                        },
                        role = "model"
                    },
                    finishReason = "STOP",
                    index = 0
                }
            },
            usageMetadata = new
            {
                promptTokenCount = 50,
                candidatesTokenCount = 30,
                totalTokenCount = 80
            }
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(apiResponse));

        var httpClient = new HttpClient(_httpHandlerMock.Object);
        var service = new GeminiService(httpClient, _configuration, _loggerMock.Object);

        // Act
        var result = await service.GenerateAsync(
            "System prompt",
            "User message");

        // Assert
        Assert.Equal(expectedText, result);

        // Test with multiple parts (should return first part)
        var multiPartResponse = new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[]
                        {
                            new { text = "First part" },
                            new { text = "Second part" }
                        },
                        role = "model"
                    }
                }
            }
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(multiPartResponse));
        var multiPartService = new GeminiService(
            new HttpClient(_httpHandlerMock.Object),
            _configuration,
            _loggerMock.Object);

        var multiPartResult = await multiPartService.GenerateAsync("System", "Message");
        Assert.Equal("First part", multiPartResult);

        // Test with empty candidates
        var emptyResponse = new
        {
            candidates = Array.Empty<object>()
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(emptyResponse));
        var emptyService = new GeminiService(
            new HttpClient(_httpHandlerMock.Object),
            _configuration,
            _loggerMock.Object);

        var emptyResult = await emptyService.GenerateAsync("System", "Message");
        Assert.Equal("", emptyResult);
    }

    /// <summary>
    /// Step 75: GeminiService_HandlesErrors_ThrowsException
    /// API errors should throw GeminiApiException with appropriate details.
    /// </summary>
    [Fact]
    public async Task GeminiService_HandlesErrors_ThrowsException()
    {
        // Test 400 Bad Request
        var errorResponse400 = JsonSerializer.Serialize(new
        {
            error = new
            {
                code = 400,
                message = "Invalid request: missing required field",
                status = "INVALID_ARGUMENT"
            }
        });

        SetupHttpResponse(HttpStatusCode.BadRequest, errorResponse400);
        var service400 = new GeminiService(
            new HttpClient(_httpHandlerMock.Object),
            _configuration,
            _loggerMock.Object);

        var exception400 = await Assert.ThrowsAsync<GeminiApiException>(() =>
            service400.GenerateAsync("System prompt", "User message"));

        Assert.Contains("BadRequest", exception400.Message);
        Assert.NotNull(exception400.ResponseContent);
        Assert.Contains("Invalid request", exception400.ResponseContent);

        // Test 401 Unauthorized
        var errorResponse401 = JsonSerializer.Serialize(new
        {
            error = new
            {
                code = 401,
                message = "API key not valid",
                status = "UNAUTHENTICATED"
            }
        });

        SetupHttpResponse(HttpStatusCode.Unauthorized, errorResponse401);
        var service401 = new GeminiService(
            new HttpClient(_httpHandlerMock.Object),
            _configuration,
            _loggerMock.Object);

        var exception401 = await Assert.ThrowsAsync<GeminiApiException>(() =>
            service401.GenerateAsync("System prompt", "User message"));

        Assert.Contains("Unauthorized", exception401.Message);
        Assert.Contains("API key not valid", exception401.ResponseContent ?? "");

        // Test 429 Too Many Requests
        SetupHttpResponse(HttpStatusCode.TooManyRequests, "Rate limit exceeded");
        var service429 = new GeminiService(
            new HttpClient(_httpHandlerMock.Object),
            _configuration,
            _loggerMock.Object);

        var exception429 = await Assert.ThrowsAsync<GeminiApiException>(() =>
            service429.GenerateAsync("System prompt", "User message"));

        Assert.Contains("TooManyRequests", exception429.Message);

        // Test 500 Internal Server Error
        SetupHttpResponse(HttpStatusCode.InternalServerError, "Internal server error");
        var service500 = new GeminiService(
            new HttpClient(_httpHandlerMock.Object),
            _configuration,
            _loggerMock.Object);

        var exception500 = await Assert.ThrowsAsync<GeminiApiException>(() =>
            service500.GenerateAsync("System prompt", "User message"));

        Assert.Contains("InternalServerError", exception500.Message);

        // Test network error (HttpRequestException wrapped as GeminiApiException)
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var serviceNetwork = new GeminiService(
            new HttpClient(_httpHandlerMock.Object),
            _configuration,
            _loggerMock.Object);

        var exceptionNetwork = await Assert.ThrowsAsync<GeminiApiException>(() =>
            serviceNetwork.GenerateAsync("System prompt", "User message"));

        Assert.Contains("connect", exceptionNetwork.Message.ToLower());
        Assert.NotNull(exceptionNetwork.InnerException);
        Assert.IsType<HttpRequestException>(exceptionNetwork.InnerException);
    }
}
