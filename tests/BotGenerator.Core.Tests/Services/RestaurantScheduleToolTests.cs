using System.Text.Json;
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BotGenerator.Core.Tests.Services;

public class RestaurantScheduleToolTests
{
    [Fact]
    public async Task GetRestaurantInfo_WithoutConfig_ReturnsDefaultOpenAndClosedDaysWithoutHours()
    {
        var result = await Executor(null).ExecuteAsync(
            "get_restaurant_info",
            JsonDocument.Parse("{}").RootElement,
            "+34 600000000");

        using var payload = JsonDocument.Parse(result.Content);
        var root = payload.RootElement;
        var closedDays = root.GetProperty("defaultClosedDays")
            .EnumerateArray().Select(day => day.GetString()).ToList();
        var openDays = root.GetProperty("defaultOpenDays")
            .EnumerateArray().Select(day => day.GetString()).ToList();

        closedDays.Should().Equal("Monday", "Tuesday", "Wednesday");
        openDays.Should().Equal("Thursday", "Friday", "Saturday", "Sunday");
        root.TryGetProperty("weeklySchedule", out _).Should().BeFalse();
        root.GetProperty("schedulePolicy").GetString().Should().Contain("check_day_capacity");
    }

    [Fact]
    public async Task GetRestaurantInfo_WithUnhydratedSchedule_IgnoresHypotheticalModelOverrides()
    {
        var config = new RestaurantConfig
        {
            Name = "Configured restaurant",
            ClosedDays = [DayOfWeek.Sunday],
            Schedule = new Dictionary<DayOfWeek, ScheduleEntry>
            {
                [DayOfWeek.Friday] = new()
                {
                    OpenTime = new TimeOnly(13, 30),
                    CloseTime = new TimeOnly(17, 30)
                },
                [DayOfWeek.Saturday] = new() { IsClosed = true },
                [DayOfWeek.Sunday] = new()
                {
                    OpenTime = new TimeOnly(13, 30),
                    CloseTime = new TimeOnly(18, 0)
                }
            }
        };

        var result = await Executor(config).ExecuteAsync(
            "get_restaurant_info",
            JsonDocument.Parse("{}").RootElement,
            "+34 600000000");

        using var payload = JsonDocument.Parse(result.Content);
        var root = payload.RootElement;
        var closedDays = root.GetProperty("defaultClosedDays")
            .EnumerateArray().Select(day => day.GetString()).ToList();

        closedDays.Should().Equal("Monday", "Tuesday", "Wednesday");
        root.GetProperty("name").GetString().Should().Be("Configured restaurant");
        root.TryGetProperty("weeklySchedule", out _).Should().BeFalse();
    }

    [Fact]
    public void GetRestaurantInfoTool_DirectsDatedQuestionsToAvailabilityTools()
    {
        var description = AgentToolDefinitions.GetRestaurantInfoTool().Description;

        description.Should().Contain("días abiertos/cerrados por defecto");
        description.Should().Contain("No devuelve horas de apertura");
        description.Should().Contain("check_day_capacity");
        description.Should().Contain("restaurant_days");
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, true)]
    [InlineData(DayOfWeek.Tuesday, true)]
    [InlineData(DayOfWeek.Wednesday, true)]
    [InlineData(DayOfWeek.Thursday, false)]
    [InlineData(DayOfWeek.Friday, false)]
    [InlineData(DayOfWeek.Saturday, false)]
    [InlineData(DayOfWeek.Sunday, false)]
    public void DefaultPolicy_MatchesBookingFallback(DayOfWeek day, bool expectedClosed)
    {
        RestaurantSchedulePolicy.IsDefaultClosed(day).Should().Be(expectedClosed);
    }

    private static ToolExecutor Executor(RestaurantConfig? config)
    {
        var configRepository = new Mock<IRestaurantConfigRepository>();
        configRepository.Setup(repository => repository.GetBySlugAsync("villacarmen", default))
            .ReturnsAsync(config);
        var outboxRepository = Mock.Of<IBookingConfirmationOutboxRepository>();
        var whatsApp = Mock.Of<IWhatsAppService>();

        return new ToolExecutor(
            whatsApp,
            Mock.Of<IMenuRepository>(),
            Mock.Of<IBookingRepository>(),
            outboxRepository,
            new BookingConfirmationOutboxProcessor(
                outboxRepository,
                whatsApp,
                new BookingConfirmationOutboxOptions(),
                NullLogger<BookingConfirmationOutboxProcessor>.Instance),
            configRepository.Object,
            Mock.Of<IOpeningHoursService>(),
            new ServiceCollection().BuildServiceProvider(),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["MySQL:ConnectionString"] = "unused" }).Build(),
            NullLogger<ToolExecutor>.Instance);
    }
}
