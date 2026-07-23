using System.Text.Json;
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using FluentAssertions;
using Xunit;

namespace BotGenerator.Core.Tests.Services;

/// <summary>
/// Regression tests for the change-detection logic used by modify_booking.
/// Reproduces chat-16166 where client 34627344782 asked to change rice
/// servings from 2 to 4 and the tool wrongly reported "no change specified".
/// Uses the test phone 34692747052.
/// </summary>
public class ModifyBookingChangesTests
{
    private const string TestPhone = "34 692747052";

    private static BookingRecord SampleBooking() => new()
    {
        Id = 2720,
        CustomerName = "Test Cliente",
        ReservationDate = new DateTime(2026, 7, 30),
        ReservationTime = new TimeSpan(14, 0, 0),
        PartySize = 5,
        ArrozType = "Arroz meloso de pulpo y gambones (+5\u20ac)",
        ArrozServings = 2,
        HighChairs = 0,
        BabyStrollers = 0,
        ContactPhone = "692747052"
    };

    private static JsonElement Input(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void RiceServingsOnly_ChangedValue_RegistersChange()
    {
        var updateData = new BookingUpdateData();

        var changes = ToolExecutor.CollectRiceAndExtrasChanges(
            Input("{\"rice_servings\": 4}"), SampleBooking(), updateData);

        changes.Should().NotBeEmpty("changing rice servings is a real modification");
        updateData.ArrozServings.Should().Be(4);
    }

    [Fact]
    public void RiceServings_SameValue_RegistersNoChange()
    {
        var updateData = new BookingUpdateData();

        var changes = ToolExecutor.CollectRiceAndExtrasChanges(
            Input("{\"rice_servings\": 2}"), SampleBooking(), updateData);

        changes.Should().BeEmpty("servings equal to the current value is not a change");
        updateData.ArrozServings.Should().BeNull();
    }

    [Fact]
    public void RiceType_Changed_RegistersChange()
    {
        var updateData = new BookingUpdateData();

        var changes = ToolExecutor.CollectRiceAndExtrasChanges(
            Input("{\"rice_type\": \"Paella valenciana\"}"), SampleBooking(), updateData);

        changes.Should().ContainSingle();
        updateData.ArrozType.Should().Be("Paella valenciana");
    }

    [Fact]
    public void HighChairs_Changed_RegistersChange()
    {
        var updateData = new BookingUpdateData();

        var changes = ToolExecutor.CollectRiceAndExtrasChanges(
            Input("{\"high_chairs\": 2}"), SampleBooking(), updateData);

        changes.Should().ContainSingle();
        updateData.HighChairs.Should().Be(2);
    }

    [Fact]
    public void ClearRice_RegistersChange()
    {
        var updateData = new BookingUpdateData();

        var changes = ToolExecutor.CollectRiceAndExtrasChanges(
            Input("{\"clear_rice\": true}"), SampleBooking(), updateData);

        changes.Should().ContainSingle();
        updateData.ClearRice.Should().BeTrue();
    }

    [Fact]
    public void AddRiceWithoutServings_IsRejected()
    {
        var booking = SampleBooking() with { ArrozType = null, ArrozServings = null };

        var error = ToolExecutor.ValidateRiceChange(
            Input("{\"rice_type\":\"Arroz de Verduras\"}"), booking, booking.PartySize);

        error.Should().Contain("rice_servings");
    }

    [Fact]
    public void AddRiceWithFiveServings_IsValidForTestPhoneBooking()
    {
        var booking = SampleBooking() with
        {
            ContactPhone = TestPhone[^9..],
            ArrozType = null,
            ArrozServings = null
        };

        var error = ToolExecutor.ValidateRiceChange(
            Input("{\"rice_type\":\"Arroz seco de verduras de la huerta\",\"rice_servings\":5}"),
            booking,
            booking.PartySize);

        error.Should().BeNull();
    }

    [Fact]
    public void RiceMatch_ResolvesVegetableRiceInsteadOfFirstRice()
    {
        var match = ToolExecutor.FindRiceMatch("Arroz de verduras", new[]
        {
            "Arroz meloso de pulpo y gambones (+5€)",
            "Arroz seco de verduras de la huerta"
        });

        match.Should().Be("Arroz seco de verduras de la huerta");
    }
}
