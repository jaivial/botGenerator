namespace BotGenerator.Core.Services;

/// <summary>
/// Generic interface for pipeline processing nodes.
/// Each node takes typed input and produces typed output.
/// </summary>
public interface IPipelineNode<TInput, TOutput>
{
    Task<TOutput> ProcessAsync(TInput input, CancellationToken ct);
}
