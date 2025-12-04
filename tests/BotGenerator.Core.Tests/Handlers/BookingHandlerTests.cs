using BotGenerator.Core.Handlers;
using BotGenerator.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace BotGenerator.Core.Tests.Handlers;

/// <summary>
/// Tests for BookingHandler - Step 45: Response Building Tests
/// Tests that confirmation messages are built correctly with proper emojis.
/// </summary>
public class BookingHandlerTests
{
    private readonly Mock<ILogger<BookingHandler>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly BookingHandler _handler;

    public BookingHandlerTests()
    {
        _loggerMock = new Mock<ILogger<BookingHandler>>();
        _configurationMock = new Mock<IConfiguration>();

        _handler = new BookingHandler(
            _loggerMock.Object,
            _configurationMock.Object);
    }

    /// <summary>
    /// Step 45: BookingHandler_BuildsConfirmation_IncludesEmojis
    /// Tests that the confirmation message includes the correct emojis:
    /// - ✅ for confirmation
    /// - 📅 for date
    /// - 🕐 for time
    /// - 👥 for people
    /// - 👤 for name
    /// - 🍚 for rice (arroz)
    /// - 🪑 for high chairs (tronas)
    /// - 🛒 for baby strollers (carritos)
    /// </summary>
    [Fact]
    public async Task BookingHandler_BuildsConfirmation_IncludesEmojis()
    {
        // Arrange
        var booking = new BookingData
        {
            Name = "Juan Pérez",
            Phone = "34612345678",
            Date = "30/11/2024",
            Time = "14:00",
            People = 4,
            ArrozType = "del señoret",
            ArrozServings = 4,
            HighChairs = 2,
            BabyStrollers = 1
        };

        var message = CreateTestMessage("Confirmar reserva");

        // Act
        var result = await _handler.CreateBookingAsync(booking, message);

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(IntentType.Booking);
        result.AiResponse.Should().NotBeNullOrWhiteSpace();

        // Check for all expected emojis
        result.AiResponse.Should().Contain("✅"); // Confirmation check mark
        result.AiResponse.Should().Contain("📅"); // Calendar for date
        result.AiResponse.Should().Contain("🕐"); // Clock for time
        result.AiResponse.Should().Contain("👥"); // People/Group
        result.AiResponse.Should().Contain("👤"); // Person for name
        result.AiResponse.Should().Contain("🍚"); // Rice bowl for arroz
        result.AiResponse.Should().Contain("🪑"); // Chair for tronas
        result.AiResponse.Should().Contain("🛒"); // Shopping cart for strollers
    }

    /// <summary>
    /// Tests that confirmation includes all booking details.
    /// </summary>
    [Fact]
    public async Task BookingHandler_BuildsConfirmation_IncludesAllDetails()
    {
        // Arrange
        var booking = new BookingData
        {
            Name = "María García",
            Phone = "34666123456",
            Date = "15/12/2024",
            Time = "20:30",
            People = 6
        };

        var message = CreateTestMessage("Reservar");

        // Act
        var result = await _handler.CreateBookingAsync(booking, message);

        // Assert
        result.AiResponse.Should().Contain("María García");
        result.AiResponse.Should().Contain("15/12/2024");
        result.AiResponse.Should().Contain("20:30");
        result.AiResponse.Should().Contain("6");
    }

    /// <summary>
    /// Tests that confirmation includes optional fields when present.
    /// </summary>
    [Fact]
    public async Task BookingHandler_BuildsConfirmation_IncludesOptionalFields()
    {
        // Arrange
        var booking = new BookingData
        {
            Name = "Carlos López",
            Phone = "34655666777",
            Date = "20/12/2024",
            Time = "13:30",
            People = 4,
            ArrozType = "banda",
            ArrozServings = 3
        };

        var message = CreateTestMessage("Confirmar");

        // Act
        var result = await _handler.CreateBookingAsync(booking, message);

        // Assert
        result.AiResponse.Should().Contain("banda");
        result.AiResponse.Should().Contain("3");
    }

    /// <summary>
    /// Tests that confirmation omits optional fields when not present.
    /// </summary>
    [Fact]
    public async Task BookingHandler_BuildsConfirmation_OmitsEmptyOptionalFields()
    {
        // Arrange
        var booking = new BookingData
        {
            Name = "Ana Martín",
            Phone = "34644555666",
            Date = "25/12/2024",
            Time = "21:00",
            People = 2
            // No rice, chairs, or strollers
        };

        var message = CreateTestMessage("Reservar");

        // Act
        var result = await _handler.CreateBookingAsync(booking, message);

        // Assert
        result.AiResponse.Should().NotContain("🍚");
        result.AiResponse.Should().NotContain("🪑");
        result.AiResponse.Should().NotContain("🛒");
        result.AiResponse.Should().NotContain("Arroz");
        result.AiResponse.Should().NotContain("Tronas");
        result.AiResponse.Should().NotContain("Carritos");
    }

    /// <summary>
    /// Tests that confirmation uses proper formatting (bold with asterisks).
    /// </summary>
    [Fact]
    public async Task BookingHandler_BuildsConfirmation_UsesBoldFormatting()
    {
        // Arrange
        var booking = new BookingData
        {
            Name = "Pedro Ruiz",
            Phone = "34633444555",
            Date = "31/12/2024",
            Time = "22:00",
            People = 8
        };

        var message = CreateTestMessage("Reserva");

        // Act
        var result = await _handler.CreateBookingAsync(booking, message);

        // Assert
        // Should use *text* for bold in WhatsApp
        result.AiResponse.Should().MatchRegex(@"\*[^*]+\*");
        // Should contain bold labels
        result.AiResponse.Should().Contain("*Fecha:*");
        result.AiResponse.Should().Contain("*Hora:*");
        result.AiResponse.Should().Contain("*Personas:*");
        result.AiResponse.Should().Contain("*Nombre:*");
    }

    /// <summary>
    /// Tests that metadata includes booking creation flag.
    /// </summary>
    [Fact]
    public async Task BookingHandler_BuildsConfirmation_SetsMetadata()
    {
        // Arrange
        var booking = new BookingData
        {
            Name = "Elena Torres",
            Phone = "34622333444",
            Date = "10/01/2025",
            Time = "19:00",
            People = 5
        };

        var message = CreateTestMessage("Confirmar");

        // Act
        var result = await _handler.CreateBookingAsync(booking, message);

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().ContainKey("bookingCreated");
        result.Metadata!["bookingCreated"].Should().Be(true);
        result.Metadata.Should().ContainKey("bookingId");
    }

    /// <summary>
    /// Step 46: BookingHandler_BuildsConfirmation_IncludesAllData
    /// Tests that confirmation message includes all required booking fields.
    /// Must include: name, date, time, people count.
    /// </summary>
    [Fact]
    public async Task BookingHandler_BuildsConfirmation_IncludesAllData()
    {
        // Arrange
        var booking = new BookingData
        {
            Name = "Laura Fernández",
            Phone = "34611222333",
            Date = "15/12/2024",
            Time = "19:30",
            People = 5,
            ArrozType = "paella valenciana",
            ArrozServings = 5,
            HighChairs = 1,
            BabyStrollers = 0
        };

        var message = CreateTestMessage("Confirmar reserva");

        // Act
        var result = await _handler.CreateBookingAsync(booking, message);

        // Assert
        result.Should().NotBeNull();
        result.AiResponse.Should().NotBeNullOrWhiteSpace();

        // All required fields must be present
        result.AiResponse.Should().Contain("Laura Fernández"); // Name
        result.AiResponse.Should().Contain("15/12/2024"); // Date
        result.AiResponse.Should().Contain("19:30"); // Time
        result.AiResponse.Should().Contain("5"); // People count

        // Optional fields that are present
        result.AiResponse.Should().Contain("paella valenciana"); // Rice type
        result.AiResponse.Should().Contain("1"); // High chairs

        // Confirmation indicators
        result.AiResponse.Should().Contain("✅");
        result.AiResponse.Should().Contain("confirmada");
    }

    /// <summary>
    /// Step 47: BookingHandler_BuildsConfirmation_OptionalRice
    /// Tests that rice information is shown ONLY when present.
    /// If ArrozType is null or empty, rice section should not appear.
    /// </summary>
    [Fact]
    public async Task BookingHandler_BuildsConfirmation_OptionalRice()
    {
        // Arrange - booking WITH rice
        var bookingWithRice = new BookingData
        {
            Name = "Miguel Sánchez",
            Phone = "34622333444",
            Date = "20/12/2024",
            Time = "14:00",
            People = 4,
            ArrozType = "del señoret",
            ArrozServings = 4
        };

        var message1 = CreateTestMessage("Confirmar");

        // Act
        var resultWithRice = await _handler.CreateBookingAsync(bookingWithRice, message1);

        // Assert - rice should be present
        resultWithRice.AiResponse.Should().Contain("🍚"); // Rice emoji
        resultWithRice.AiResponse.Should().Contain("del señoret");
        resultWithRice.AiResponse.Should().Contain("Raciones");
        resultWithRice.AiResponse.Should().Contain("4");

        // Arrange - booking WITHOUT rice
        var bookingWithoutRice = new BookingData
        {
            Name = "Isabel Moreno",
            Phone = "34633444555",
            Date = "22/12/2024",
            Time = "20:00",
            People = 3,
            ArrozType = null, // No rice
            ArrozServings = null
        };

        var message2 = CreateTestMessage("Confirmar");

        // Act
        var resultWithoutRice = await _handler.CreateBookingAsync(bookingWithoutRice, message2);

        // Assert - rice should NOT appear
        resultWithoutRice.AiResponse.Should().NotContain("🍚"); // No rice emoji
        resultWithoutRice.AiResponse.Should().NotContain("Arroz");
        resultWithoutRice.AiResponse.Should().NotContain("Raciones");
    }

    /// <summary>
    /// Step 48: BookingHandler_BuildsConfirmation_OptionalHighChairs
    /// Tests that high chairs and baby strollers are shown ONLY when > 0.
    /// If count is 0, they should not appear in the message.
    /// </summary>
    [Fact]
    public async Task BookingHandler_BuildsConfirmation_OptionalHighChairs()
    {
        // Arrange - booking WITH high chairs and strollers
        var bookingWithChairs = new BookingData
        {
            Name = "Carmen Ruiz",
            Phone = "34644555666",
            Date = "25/12/2024",
            Time = "13:30",
            People = 6,
            HighChairs = 2,
            BabyStrollers = 1
        };

        var message1 = CreateTestMessage("Confirmar");

        // Act
        var resultWithChairs = await _handler.CreateBookingAsync(bookingWithChairs, message1);

        // Assert - chairs and strollers should be present
        resultWithChairs.AiResponse.Should().Contain("🪑"); // Chair emoji
        resultWithChairs.AiResponse.Should().Contain("Tronas");
        resultWithChairs.AiResponse.Should().Contain("2");
        resultWithChairs.AiResponse.Should().Contain("🛒"); // Stroller emoji
        resultWithChairs.AiResponse.Should().Contain("Carritos");
        resultWithChairs.AiResponse.Should().Contain("1");

        // Arrange - booking WITHOUT chairs or strollers
        var bookingWithoutChairs = new BookingData
        {
            Name = "David Torres",
            Phone = "34655666777",
            Date = "28/12/2024",
            Time = "21:00",
            People = 2,
            HighChairs = 0, // No chairs
            BabyStrollers = 0 // No strollers
        };

        var message2 = CreateTestMessage("Reservar");

        // Act
        var resultWithoutChairs = await _handler.CreateBookingAsync(bookingWithoutChairs, message2);

        // Assert - chairs and strollers should NOT appear
        resultWithoutChairs.AiResponse.Should().NotContain("🪑"); // No chair emoji
        resultWithoutChairs.AiResponse.Should().NotContain("Tronas");
        resultWithoutChairs.AiResponse.Should().NotContain("🛒"); // No stroller emoji
        resultWithoutChairs.AiResponse.Should().NotContain("Carritos");

        // But other required fields should still be present
        resultWithoutChairs.AiResponse.Should().Contain("David Torres");
        resultWithoutChairs.AiResponse.Should().Contain("28/12/2024");
        resultWithoutChairs.AiResponse.Should().Contain("21:00");
        resultWithoutChairs.AiResponse.Should().Contain("2");
    }

    #region Helper Methods

    /// <summary>
    /// Creates a test WhatsApp message with default values.
    /// </summary>
    private WhatsAppMessage CreateTestMessage(string text)
    {
        return new WhatsAppMessage
        {
            InstanceName = "test-instance",
            SenderNumber = "34612345678",
            MessageText = text,
            MessageType = "text",
            PushName = "Test User",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            MessageId = Guid.NewGuid().ToString()
        };
    }

    #endregion
}
