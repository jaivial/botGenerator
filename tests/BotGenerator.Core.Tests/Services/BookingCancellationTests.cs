using System.Text.Json;
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BotGenerator.Core.Tests.Services;

public class BookingCancellationTests
{
    private const string TestPhone = "+34 692747052";
    private readonly Mock<IBookingRepository> _bookings = new();

    private BookingRecord Booking(string status = "pending") => new()
    {
        Id = 900001,
        CustomerName = "Test Cliente",
        ReservationDate = DateTime.Today.AddDays(10),
        ReservationTime = new TimeSpan(14, 30, 0),
        PartySize = 5,
        ContactPhone = "692747052",
        ContactEmail = "test@example.com",
        Status = status
    };

    private ToolExecutor Executor() => new(
        Mock.Of<IWhatsAppService>(),
        Mock.Of<IMenuRepository>(),
        _bookings.Object,
        Mock.Of<IRestaurantConfigRepository>(),
        Mock.Of<IOpeningHoursService>(),
        new ServiceCollection().BuildServiceProvider(),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["MySQL:ConnectionString"] = "unused" }).Build(),
        NullLogger<ToolExecutor>.Instance);

    private static JsonElement Input(bool confirmed = true) =>
        JsonDocument.Parse($"{{\"booking_id\":\"900001\",\"confirmed\":{confirmed.ToString().ToLowerInvariant()}}}").RootElement;

    [Fact]
    public async Task Cancel_WrongPhone_IsRejectedWithoutWrites()
    {
        _bookings.Setup(x => x.GetBookingByIdAsync(900001, default)).ReturnsAsync(Booking());

        var result = await Executor().ExecuteAsync("cancel_booking", Input(), "+34 600000000");

        result.IsError.Should().BeTrue();
        _bookings.Verify(x => x.ArchiveAndCancelBookingAsync(It.IsAny<BookingRecord>(), It.IsAny<string>(), default), Times.Never);
    }

    [Theory]
    [InlineData("cancelled")]
    [InlineData("expired")]
    public async Task Cancel_InactiveStatus_IsRejected(string status)
    {
        _bookings.Setup(x => x.GetBookingByIdAsync(900001, default)).ReturnsAsync(Booking(status));

        var result = await Executor().ExecuteAsync("cancel_booking", Input(), TestPhone);

        result.IsError.Should().BeTrue();
        _bookings.Verify(x => x.ArchiveAndCancelBookingAsync(It.IsAny<BookingRecord>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task Cancel_ArchiveFailure_DoesNotDeleteBooking()
    {
        var booking = Booking();
        _bookings.Setup(x => x.GetBookingByIdAsync(900001, default)).ReturnsAsync(booking);
        _bookings.Setup(x => x.ArchiveAndCancelBookingAsync(booking, "AI_AGENT", default)).ReturnsAsync(false);

        var result = await Executor().ExecuteAsync("cancel_booking", Input(), TestPhone);

        result.IsError.Should().BeTrue();
        _bookings.Verify(x => x.ArchiveAndCancelBookingAsync(booking, "AI_AGENT", default), Times.Once);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("confirmed")]
    public async Task Cancel_OwnedActiveBooking_ArchivesThenDeletes(string status)
    {
        var booking = Booking(status);
        _bookings.Setup(x => x.GetBookingByIdAsync(900001, default)).ReturnsAsync(booking);
        _bookings.Setup(x => x.ArchiveAndCancelBookingAsync(booking, "AI_AGENT", default)).ReturnsAsync(true);

        var result = await Executor().ExecuteAsync("cancel_booking", Input(), TestPhone);

        result.IsError.Should().BeFalse();
        _bookings.Verify(x => x.ArchiveAndCancelBookingAsync(booking, "AI_AGENT", default), Times.Once);
    }

    [Fact]
    public async Task Cancel_WithoutConfirmation_DoesNothing()
    {
        var result = await Executor().ExecuteAsync("cancel_booking", Input(false), TestPhone);

        result.IsError.Should().BeTrue();
        _bookings.VerifyNoOtherCalls();
    }
}
