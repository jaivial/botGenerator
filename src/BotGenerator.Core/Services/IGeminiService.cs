using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Service interface for communicating with AI APIs (Gemini or Anthropic-compatible).
/// </summary>
public interface IGeminiService
{
    /// <summary>
    /// Generates a text response using the AI model.
    /// </summary>
    Task<string> GenerateAsync(
        string systemPrompt,
        string userMessage,
        List<ChatMessage>? history = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a text response with custom generation configuration.
    /// </summary>
    Task<string> GenerateAsync(
        string systemPrompt,
        string userMessage,
        List<ChatMessage>? history,
        GeminiGenerationConfig config,
        CancellationToken cancellationToken);

    /// <summary>
    /// Generates a response with tool calling support.
    /// The AI may return text, tool calls, or both.
    /// </summary>
    Task<AnthropicResponse> GenerateWithToolsAsync(
        string systemPrompt,
        string userMessage,
        List<ChatMessage>? history,
        List<ToolDefinition> tools,
        ToolChoiceConfig? toolChoice = null,
        GeminiGenerationConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Continues a conversation after a tool call, sending the tool results back.
    /// </summary>
    Task<AnthropicResponse> ContinueWithToolResultAsync(
        string systemPrompt,
        List<object> messages,
        List<ToolDefinition> tools,
        ToolChoiceConfig? toolChoice = null,
        GeminiGenerationConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts tokens in the given text.
    /// </summary>
    Task<int> CountTokensAsync(string text, CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration options for text generation.
/// </summary>
public record GeminiGenerationConfig
{
    public double Temperature { get; init; } = 0.7;
    public double TopP { get; init; } = 0.95;
    public int TopK { get; init; } = 40;
    public int MaxOutputTokens { get; init; } = 2048;
    public List<string>? StopSequences { get; init; }

    public static GeminiGenerationConfig Default => new();
    public static GeminiGenerationConfig Focused => new() { Temperature = 0.3, TopP = 0.8, TopK = 20 };
    public static GeminiGenerationConfig Creative => new() { Temperature = 1.0, TopP = 0.95, TopK = 60 };
}
