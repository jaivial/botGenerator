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
    public void GetAllTools_IncludesSendContactCard()
    {
        var tools = AgentToolDefinitions.GetAllTools();

        tools.Should().Contain(t => t.Name == "send_contact_card");
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

    [Fact]
    public void CreateBookingTool_RequiresPeopleAndPublishesCountBounds()
    {
        var schema = AgentToolDefinitions.GetCreateBookingTool().InputSchema;
        var properties = schema.GetProperty("properties");
        var required = schema.GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToList();

        required.Should().Contain("people");
        properties.GetProperty("people").GetProperty("minimum").GetInt32().Should().Be(1);
        properties.GetProperty("rice_servings").GetProperty("minimum").GetInt32().Should().Be(2);
        properties.GetProperty("high_chairs").GetProperty("minimum").GetInt32().Should().Be(0);
        properties.GetProperty("baby_strollers").GetProperty("minimum").GetInt32().Should().Be(0);
    }

    [Fact]
    public void ModifyBookingTool_PublishesCountBounds()
    {
        var properties = AgentToolDefinitions.GetModifyBookingTool().InputSchema.GetProperty("properties");

        properties.GetProperty("people").GetProperty("minimum").GetInt32().Should().Be(1);
        properties.GetProperty("rice_servings").GetProperty("minimum").GetInt32().Should().Be(2);
        properties.GetProperty("high_chairs").GetProperty("minimum").GetInt32().Should().Be(0);
        properties.GetProperty("baby_strollers").GetProperty("minimum").GetInt32().Should().Be(0);
    }
}
