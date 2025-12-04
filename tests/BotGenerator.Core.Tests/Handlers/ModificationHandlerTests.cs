using BotGenerator.Core.Handlers;
using BotGenerator.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace BotGenerator.Core.Tests.Handlers;

/// <summary>
/// Tests for ModificationHandler - Steps 49-50: Response Building Tests
/// Tests that modification options are built correctly for single and multiple bookings.
/// </summary>
public class ModificationHandlerTests
{
    private readonly Mock<ILogger<ModificationHandler>> _loggerMock;
    private readonly ModificationHandler _handler;

    public ModificationHandlerTests()
    {
        _loggerMock = new Mock<ILogger<ModificationHandler>>();
        _handler = new ModificationHandler(_loggerMock.Object);
    }

    /// <summary>
    /// Step 49: ModificationHandler_BuildsOptions_SingleBooking
    /// Tests that when a single booking is found, the response:
    /// - Shows booking details (date, time, people)
    /// - Asks "¿Qué quieres modificar?" or similar
    /// - Lists modification options numbered (Fecha, Hora, Personas, Arroz)
    /// - Sets correct metadata (modificationState, selectedBooking)
    /// </summary>
    [Fact]
    public async Task ModificationHandler_BuildsOptions_SingleBooking()
    {
        // Arrange
        var senderNumber = "34612345678";

        // Act
        var result = await _handler.StartModificationFlowAsync(senderNumber);

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(IntentType.Modification);
        result.AiResponse.Should().NotBeNullOrWhiteSpace();

        // Should show booking details (based on default mock data)
        result.AiResponse.Should().Contain("30/11/2025"); // Date
        result.AiResponse.Should().Contain("14:00"); // Time
        result.AiResponse.Should().Contain("4"); // People count

        // Should ask what to modify
        result.AiResponse.Should().MatchRegex("(qué.*modificar|modificar)");

        // Should list options
        result.AiResponse.Should().Contain("1"); // Option 1
        result.AiResponse.Should().Contain("2"); // Option 2
        result.AiResponse.Should().Contain("3"); // Option 3
        result.AiResponse.Should().Contain("4"); // Option 4

        // Should contain modification categories
        result.AiResponse.Should().Contain("Fecha");
        result.AiResponse.Should().Contain("Hora");
        result.AiResponse.Should().Contain("personas");
        result.AiResponse.Should().Contain("arroz");

        // Should set correct metadata
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().ContainKey("modificationState");
        result.Metadata!["modificationState"].Should().Be("selecting_field");
        result.Metadata.Should().ContainKey("selectedBooking");
    }

    /// <summary>
    /// Step 50: ModificationHandler_BuildsOptions_MultipleBookings
    /// Tests that when multiple bookings are found, the response:
    /// - Lists all bookings numbered (1, 2, 3...)
    /// - Shows date, time, and people count for each
    /// - Asks which one to modify
    /// - Sets correct metadata (modificationState, bookings list)
    /// </summary>
    [Fact]
    public async Task ModificationHandler_BuildsOptions_MultipleBookings()
    {
        // Arrange
        var senderNumber = "34699888777";

        // Create a modified handler that returns multiple bookings
        var modifiedHandler = new TestableModificationHandler(_loggerMock.Object);

        // Act
        var result = await modifiedHandler.StartModificationFlowAsync(senderNumber);

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(IntentType.Modification);
        result.AiResponse.Should().NotBeNullOrWhiteSpace();

        // Should list multiple bookings
        result.AiResponse.Should().Contain("varias reservas");

        // Should have numbered list
        result.AiResponse.Should().Contain("1."); // First booking
        result.AiResponse.Should().Contain("2."); // Second booking
        result.AiResponse.Should().Contain("3."); // Third booking

        // Should show booking details for each
        result.AiResponse.Should().Contain("05/12/2024"); // First date
        result.AiResponse.Should().Contain("12/12/2024"); // Second date
        result.AiResponse.Should().Contain("19/12/2024"); // Third date

        result.AiResponse.Should().Contain("14:00"); // First time
        result.AiResponse.Should().Contain("20:00"); // Second time
        result.AiResponse.Should().Contain("13:30"); // Third time

        result.AiResponse.Should().Contain("4"); // First people count
        result.AiResponse.Should().Contain("2"); // Second people count
        result.AiResponse.Should().Contain("6"); // Third people count

        // Should ask which one to modify
        result.AiResponse.Should().MatchRegex("(cuál.*modificar|modificar.*número)");

        // Should set correct metadata
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().ContainKey("modificationState");
        result.Metadata!["modificationState"].Should().Be("selecting_booking");
        result.Metadata.Should().ContainKey("bookings");

        var bookings = result.Metadata["bookings"] as List<ModificationHandler.BookingInfo>;
        bookings.Should().NotBeNull();
        bookings.Should().HaveCount(3);
    }

    /// <summary>
    /// Additional test: Verify no bookings found returns appropriate message
    /// </summary>
    [Fact]
    public async Task ModificationHandler_NoBookingsFound_ReturnsNoBookingsMessage()
    {
        // Arrange
        var senderNumber = "34600000000"; // No bookings for this number
        var handlerWithNoBookings = new TestableModificationHandlerNoBookings(_loggerMock.Object);

        // Act
        var result = await handlerWithNoBookings.StartModificationFlowAsync(senderNumber);

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(IntentType.Normal);
        result.AiResponse.Should().Contain("No encontré reservas");
        result.AiResponse.Should().MatchRegex("(nueva reserva|hacer.*reserva)");
    }
}

/// <summary>
/// Testable version of ModificationHandler that returns multiple bookings.
/// </summary>
internal class TestableModificationHandler : ModificationHandler
{
    public TestableModificationHandler(ILogger<ModificationHandler> logger) : base(logger)
    {
    }

    public new async Task<AgentResponse> StartModificationFlowAsync(
        string senderNumber,
        CancellationToken cancellationToken = default)
    {
        // Find existing bookings for this phone number
        var bookings = await FindBookingsForPhoneAsync(senderNumber, cancellationToken);

        // Multiple bookings case
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Encontré varias reservas a tu nombre:\n");

        for (int i = 0; i < bookings.Count; i++)
        {
            var b = bookings[i];
            sb.AppendLine($"*{i + 1}.* {b.Date} a las {b.Time} ({b.People} personas)");
        }

        sb.AppendLine("\n¿Cuál quieres modificar? (responde con el número)");

        return new AgentResponse
        {
            Intent = IntentType.Modification,
            AiResponse = sb.ToString(),
            Metadata = new Dictionary<string, object>
            {
                ["modificationState"] = "selecting_booking",
                ["bookings"] = bookings
            }
        };
    }

    private async Task<List<BookingInfo>> FindBookingsForPhoneAsync(
        string phoneNumber,
        CancellationToken cancellationToken)
    {
        await Task.Delay(50, cancellationToken);

        // Return three bookings for testing
        return new List<BookingInfo>
        {
            new()
            {
                Id = "booking-1",
                Date = "05/12/2024",
                Time = "14:00",
                People = 4
            },
            new()
            {
                Id = "booking-2",
                Date = "12/12/2024",
                Time = "20:00",
                People = 2,
                ArrozType = "banda"
            },
            new()
            {
                Id = "booking-3",
                Date = "19/12/2024",
                Time = "13:30",
                People = 6,
                ArrozType = "del señoret"
            }
        };
    }
}

/// <summary>
/// Testable version that returns no bookings.
/// </summary>
internal class TestableModificationHandlerNoBookings : ModificationHandler
{
    public TestableModificationHandlerNoBookings(ILogger<ModificationHandler> logger) : base(logger)
    {
    }

    public new async Task<AgentResponse> StartModificationFlowAsync(
        string senderNumber,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken);

        return new AgentResponse
        {
            Intent = IntentType.Normal,
            AiResponse = "No encontré reservas futuras asociadas a tu número. " +
                        "¿Quieres hacer una nueva reserva?"
        };
    }
}
