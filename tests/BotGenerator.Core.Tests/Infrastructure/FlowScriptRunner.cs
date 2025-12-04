using System.Text.Json;
using FluentAssertions;

namespace BotGenerator.Core.Tests.Infrastructure;

/// <summary>
/// Runs conversation flow tests from JSON script files.
/// </summary>
public class FlowScriptRunner
{
    private readonly ConversationSimulator _simulator;

    public FlowScriptRunner(ConversationSimulator simulator)
    {
        _simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
    }

    /// <summary>
    /// Runs a complete conversation flow from a script.
    /// </summary>
    public async Task RunScript(ConversationScript script)
    {
        foreach (var step in script.Messages)
        {
            await _simulator.UserSays(step.User);

            // Check expected content
            if (step.Expect != null && step.Expect.Count > 0)
            {
                _simulator.ShouldRespond(step.Expect.ToArray());
            }

            // Check content that should NOT appear
            if (step.NotExpect != null && step.NotExpect.Count > 0)
            {
                _simulator.ShouldNotMention(step.NotExpect.ToArray());
            }

            // Check max length
            if (step.MaxLength.HasValue)
            {
                _simulator.ResponseLengthShouldBe(step.MaxLength.Value);
            }

            // Check max questions
            if (step.MaxQuestions.HasValue)
            {
                _simulator.ShouldHaveMaxQuestions(step.MaxQuestions.Value);
            }

            // Check max emojis
            if (step.MaxEmojis.HasValue)
            {
                _simulator.ShouldHaveMaxEmojis(step.MaxEmojis.Value);
            }
        }
    }

    /// <summary>
    /// Loads and runs a script from a JSON file.
    /// </summary>
    public async Task RunScriptFile(string scriptPath)
    {
        var json = await File.ReadAllTextAsync(scriptPath);
        var script = JsonSerializer.Deserialize<ConversationScript>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (script == null)
            throw new InvalidOperationException($"Failed to deserialize script: {scriptPath}");

        await RunScript(script);
    }

    /// <summary>
    /// Loads all flows from a script file and returns them.
    /// </summary>
    public static async Task<ScriptFile> LoadScriptFile(string scriptPath)
    {
        var json = await File.ReadAllTextAsync(scriptPath);
        var scriptFile = JsonSerializer.Deserialize<ScriptFile>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return scriptFile ?? throw new InvalidOperationException(
            $"Failed to deserialize script file: {scriptPath}");
    }

    /// <summary>
    /// Verifies that the conversation completed with expected message count.
    /// </summary>
    public void AssertConversationCompleted(int expectedUserMessages)
    {
        // Each user message should have a corresponding assistant response
        _simulator.MessageCount.Should().BeGreaterThanOrEqualTo(expectedUserMessages * 2,
            "Conversation should have at least {0} messages (user + assistant)",
            expectedUserMessages * 2);
    }
}

/// <summary>
/// Represents a conversation script file containing multiple flows.
/// </summary>
public class ScriptFile
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<ConversationScript> Flows { get; set; } = new();
    public List<JourneyScript> Journeys { get; set; } = new();
}

/// <summary>
/// Represents a single conversation flow script.
/// </summary>
public class ConversationScript
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ScriptSetup? Setup { get; set; }
    public List<ScriptMessage> Messages { get; set; } = new();
}

/// <summary>
/// Setup configuration for a script.
/// </summary>
public class ScriptSetup
{
    public string? PushName { get; set; }
    public string? CurrentDate { get; set; }
    public List<ExistingBooking>? ExistingBookings { get; set; }
}

/// <summary>
/// Represents an existing booking for setup.
/// </summary>
public class ExistingBooking
{
    public string Date { get; set; } = "";
    public string Time { get; set; } = "";
    public int People { get; set; }
    public string? Rice { get; set; }
    public int? RiceServings { get; set; }
}

/// <summary>
/// Represents a single message exchange in a script.
/// </summary>
public class ScriptMessage
{
    public int Turn { get; set; }
    public string User { get; set; } = "";
    public List<string>? Expect { get; set; }
    public List<string>? NotExpect { get; set; }
    public List<string>? MayContain { get; set; }
    public int? MaxLength { get; set; }
    public int? MaxQuestions { get; set; }
    public int? MaxEmojis { get; set; }
    public string? ResponseType { get; set; }
    public bool? ContextPreserved { get; set; }
}

/// <summary>
/// Represents a full journey test script.
/// </summary>
public class JourneyScript
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int MessageCount { get; set; }
    public ScriptSetup? Setup { get; set; }
    public List<JourneyPhase>? Phases { get; set; }
    public List<ScriptMessage> Messages { get; set; } = new();
}

/// <summary>
/// Represents a phase in a journey.
/// </summary>
public class JourneyPhase
{
    public string Name { get; set; } = "";
    public int Messages { get; set; }
}
