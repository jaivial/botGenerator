using System.Text.Json;
using System.Text.Json.Serialization;

namespace BotGenerator.Core.Models;

/// <summary>
/// Definition of a tool the AI can call (Anthropic Messages API format).
/// </summary>
public record ToolDefinition
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public JsonElement InputSchema { get; init; }
}

/// <summary>
/// Response from the Anthropic Messages API, which may contain text and/or tool_use blocks.
/// </summary>
public class AnthropicResponse
{
    public List<ContentBlock> Content { get; init; } = new();
    public string StopReason { get; init; } = "end_turn";

    public bool HasToolCalls => Content.Any(c => c.Type == "tool_use");

    public string? GetText()
    {
        var textBlock = Content.OfType<TextBlock>().FirstOrDefault();
        return textBlock?.Text;
    }

    public List<ToolUseBlock> GetToolCalls()
        => Content.OfType<ToolUseBlock>().ToList();
}

/// <summary>
/// Base class for content blocks in Anthropic API responses.
/// </summary>
[JsonDerivedType(typeof(TextBlock), "text")]
[JsonDerivedType(typeof(ToolUseBlock), "tool_use")]
public class ContentBlock
{
    public string Type { get; init; } = "";
}

/// <summary>
/// A text content block from the AI.
/// </summary>
public class TextBlock : ContentBlock
{
    public string Text { get; init; } = "";
    public TextBlock() => Type = "text";
    public TextBlock(string text) : this() => Text = text;
}

/// <summary>
/// A tool_use content block — the AI is requesting to call a tool.
/// </summary>
public class ToolUseBlock : ContentBlock
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public JsonElement Input { get; init; }
    public ToolUseBlock() => Type = "tool_use";
    public ToolUseBlock(string id, string name, JsonElement input) : this()
    {
        Id = id;
        Name = name;
        Input = input;
    }
}

/// <summary>
/// Configuration for tool_choice in the Anthropic API.
/// </summary>
public record ToolChoiceConfig
{
    /// <summary>"auto", "any", or "tool"</summary>
    public string Type { get; init; } = "auto";

    /// <summary>Required when Type is "tool" — the specific tool to force.</summary>
    public string? ToolName { get; init; }

    public static ToolChoiceConfig Auto => new() { Type = "auto" };
    public static ToolChoiceConfig Any => new() { Type = "any" };
    public static ToolChoiceConfig ForceTool(string toolName) => new() { Type = "tool", ToolName = toolName };
}
