namespace BotGenerator.Core.Services;

/// <summary>
/// Result of AI rice understanding analysis.
/// </summary>
public class RiceUnderstandingResult
{
    /// <summary>True if user says "arroz" or "paella" without specifying a particular type.</summary>
    public bool IsGenericReference { get; init; }

    /// <summary>True if user wants to remove/cancel rice from their booking.</summary>
    public bool WantsCancel { get; init; }

    /// <summary>The rice type name mentioned, if any (e.g., "señoret", "fideuá").</summary>
    public string? RiceTypeMentioned { get; init; }

    /// <summary>Number of servings mentioned, if any.</summary>
    public int? ServingsMentioned { get; init; }

    /// <summary>True if the message is just a servings count (e.g., "4" or "4 raciones").</summary>
    public bool IsServingsOnly { get; init; }
}

/// <summary>
/// AI-powered service for understanding rice-related messages.
/// Replaces regex-based IsGenericRiceReference, ContainsSpecificRiceDescriptor, and rice parsing patterns.
/// </summary>
public interface IAiRiceUnderstandingService
{
    /// <summary>
    /// Analyzes a user message in the context of a rice change flow.
    /// </summary>
    Task<RiceUnderstandingResult> AnalyzeAsync(
        string userMessage,
        string bookingSummary,
        CancellationToken cancellationToken = default);
}
