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
