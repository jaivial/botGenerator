using System.Text.Json;
using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Executes tool calls requested by the AI during the agentic loop.
/// </summary>
public interface IToolExecutor
{
    /// <summary>
    /// Executes a tool call and returns the result as a JSON string.
    /// </summary>
    Task<ToolResult> ExecuteAsync(string toolName, JsonElement input, string phoneNumber, CancellationToken ct = default);
}

public class ToolResult
{
    public bool Success { get; init; } = true;
    public string Content { get; init; } = "";
    public bool IsError { get; init; } = false;
}
