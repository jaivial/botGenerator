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
