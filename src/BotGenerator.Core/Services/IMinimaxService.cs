using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Service interface for communicating with Minimax M2.1 API.
/// Replaces Gemini for better performance with long prompts and system messages.
/// </summary>
public interface IMinimaxService
{
    /// <summary>
    /// Generates a response using the Minimax model.
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
        MinimaxGenerationConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts tokens in the given text.
    /// Useful for managing context window limits.
    /// </summary>
    Task<int> CountTokensAsync(string text, CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration options for Minimax text generation.
/// </summary>
public record MinimaxGenerationConfig
{
    /// <summary>
    /// Controls randomness. Lower = more focused, Higher = more creative.
    /// Range: 0.0 to 2.0, Default: 0.7
    /// </summary>
    public double Temperature { get; init; } = 0.7;

    /// <summary>
    /// Maximum number of tokens to generate.
    /// Default: 2048
    /// </summary>
    public int MaxOutputTokens { get; init; } = 2048;

    /// <summary>
    /// Top-p sampling (nucleus sampling).
    /// Range: 0.0 to 1.0, Default: 0.95
    /// </summary>
    public double TopP { get; init; } = 0.95;

    /// <summary>
    /// Default configuration for conversational responses.
    /// </summary>
    public static MinimaxGenerationConfig Default => new();

    /// <summary>
    /// More focused/deterministic configuration.
    /// </summary>
    public static MinimaxGenerationConfig Focused => new()
    {
        Temperature = 0.3,
        TopP = 0.8
    };

    /// <summary>
    /// More creative configuration.
    /// </summary>
    public static MinimaxGenerationConfig Creative => new()
    {
        Temperature = 1.0,
        TopP = 0.95
    };
}
