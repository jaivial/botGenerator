using System.Text.Json;
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BotGenerator.Core.Tests.Services;

public class SendContactCardToolTests
{
    [Fact]
    public async Task SendContactCard_SendsManagementCard_WithManagementPhone()
    {
        var whatsApp = new Mock<IWhatsAppService>();
        whatsApp.Setup(service => service.SendContactCardAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await Executor(whatsApp.Object).ExecuteAsync(
            "send_contact_card",
            JsonDocument.Parse("{}").RootElement,
            "+34600000000");

        result.IsError.Should().BeFalse();
        result.Content.Should().Contain("sent successfully");

        whatsApp.Verify(service => service.SendContactCardAsync(
            "+34600000000",
            "Gestión Reservas",
            "+34638857294",
            "Alquería Villa Carmen",
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendContactCard_WhenProviderFails_ReturnsError()
    {
        var whatsApp = new Mock<IWhatsAppService>();
        whatsApp.Setup(service => service.SendContactCardAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await Executor(whatsApp.Object).ExecuteAsync(
            "send_contact_card",
            JsonDocument.Parse("{}").RootElement,
            "+34600000000");

        result.IsError.Should().BeTrue();
        result.Content.Should().Contain("Failed to send contact card");
    }

    private static ToolExecutor Executor(IWhatsAppService whatsApp)
    {
        var outboxRepository = Mock.Of<IBookingConfirmationOutboxRepository>();

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
            Mock.Of<IRestaurantConfigRepository>(),
            Mock.Of<IOpeningHoursService>(),
            new ServiceCollection().BuildServiceProvider(),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["MySQL:ConnectionString"] = "unused" }).Build(),
            NullLogger<ToolExecutor>.Instance);
    }
}
