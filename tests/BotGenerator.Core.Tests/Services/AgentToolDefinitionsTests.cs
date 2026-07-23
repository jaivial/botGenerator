using System.Text.Json;
using BotGenerator.Core.Services;
using FluentAssertions;

namespace BotGenerator.Core.Tests.Services;

public class AgentToolDefinitionsTests
{
    // Regression guard for the 2026-07-10 incident where a stroller ("carro de bebe")
    // modification was never persisted. The agent can only apply the change if
    // modify_booking is wired into the tool list and exposes baby_strollers.

    [Fact]
    public void GetAllTools_IncludesModifyBooking()
    {
        var tools = AgentToolDefinitions.GetAllTools();

        tools.Should().Contain(t => t.Name == "modify_booking");
    }

    [Fact]
    public void ModifyBookingTool_ExposesBabyStrollersAndHighChairs()
    {
        var tool = AgentToolDefinitions.GetModifyBookingTool();

        var props = tool.InputSchema.GetProperty("properties");

        props.TryGetProperty("baby_strollers", out _).Should().BeTrue(
            "the agent needs baby_strollers to add a 'carro/carrito de bebe'");
        props.TryGetProperty("high_chairs", out _).Should().BeTrue();

        var required = tool.InputSchema.GetProperty("required")
            .EnumerateArray()
            .Select(e => e.GetString())
            .ToList();

        required.Should().Contain("booking_id");
        required.Should().Contain("confirmed");
    }
}
