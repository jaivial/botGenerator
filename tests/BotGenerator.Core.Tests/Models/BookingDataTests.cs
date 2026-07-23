using BotGenerator.Core.Models;
using FluentAssertions;

namespace BotGenerator.Core.Tests.Models;

public class BookingDataTests
{
    private static BookingData ValidBooking() => new()
    {
        Name = "Test",
        Phone = "+34 692747052",
        Date = "2026-08-23",
        Time = "14:30",
        People = 5
    };

    [Theory]
    [InlineData("23/08/2026", "2026-08-23")]
    [InlineData("3/8/2026", "2026-08-03")]
    [InlineData("2026-08-23", "2026-08-23")]
    public void DateForDatabase_ValidDate_Normalizes(string input, string expected) =>
        new BookingData { Date = input }.DateForDatabase.Should().Be(expected);

    [Theory]
    [InlineData("31/02/2026")]
    [InlineData("2026-13-01")]
    [InlineData("tomorrow")]
    [InlineData("")]
    public void DateForDatabase_InvalidDate_ReturnsNull(string input) =>
        new BookingData { Date = input }.DateForDatabase.Should().BeNull();

    [Fact]
    public void IsValid_NoRiceAndValidCounts_ReturnsTrue() =>
        ValidBooking().IsValid.Should().BeTrue();

    [Theory]
    [InlineData(0, 0, 0, null, null)]
    [InlineData(5, -1, 0, null, null)]
    [InlineData(5, 0, -1, null, null)]
    [InlineData(5, 6, 0, null, null)]
    [InlineData(5, 0, 6, null, null)]
    [InlineData(5, 0, 0, "Arroz test", null)]
    [InlineData(5, 0, 0, null, 2)]
    [InlineData(5, 0, 0, "Arroz test", 1)]
    [InlineData(5, 0, 0, "Arroz test", 6)]
    public void IsValid_InvalidColumnCombination_ReturnsFalse(
        int people, int chairs, int strollers, string? rice, int? servings)
    {
        var booking = ValidBooking() with
        {
            People = people,
            HighChairs = chairs,
            BabyStrollers = strollers,
            ArrozType = rice,
            ArrozServings = servings
        };

        booking.IsValid.Should().BeFalse();
    }
}
