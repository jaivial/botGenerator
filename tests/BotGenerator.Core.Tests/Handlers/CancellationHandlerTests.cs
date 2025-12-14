using BotGenerator.Core.Handlers;
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace BotGenerator.Core.Tests.Handlers;

/// <summary>
/// Tests for the CancellationHandler.
/// Tests the multi-turn cancellation conversation flow.
/// </summary>
public class CancellationHandlerTests
{
    private readonly Mock<ILogger<CancellationHandler>> _loggerMock;
    private readonly Mock<IBookingRepository> _bookingRepoMock;
    private readonly Mock<ICancellationStateStore> _stateStoreMock;
    private readonly Mock<IWhatsAppService> _whatsAppMock;
    private readonly CancellationHandler _handler;

    public CancellationHandlerTests()
    {
        _loggerMock = new Mock<ILogger<CancellationHandler>>();
        _bookingRepoMock = new Mock<IBookingRepository>();
        _stateStoreMock = new Mock<ICancellationStateStore>();
        _whatsAppMock = new Mock<IWhatsAppService>();

        _handler = new CancellationHandler(
            _loggerMock.Object,
            _bookingRepoMock.Object,
            _stateStoreMock.Object,
            _whatsAppMock.Object);
    }

    #region No Bookings Tests

    /// <summary>
    /// Tests that when no bookings are found, the handler returns appropriate message.
    /// </summary>
    [Fact]
    public async Task ProcessCancellation_NoBookings_ReturnsNoBookingsMessage()
    {
        // Arrange
        var phone = "34600000000";
        var message = CreateTextMessage(phone, "quiero cancelar mi reserva");

        _bookingRepoMock
            .Setup(x => x.FindBookingsByPhoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BookingRecord>());

        // Act
        var result = await _handler.ProcessCancellationAsync(message, null);

        // Assert
        result.Should().NotBeNull();
        result.AiResponse.Should().NotBeNullOrWhiteSpace();

        // Should indicate no bookings found
        result.AiResponse.Should().MatchRegex("([Nn]o.*encuentr|[Nn]o.*reserva|[Nn]o.*tiene)");

        // Should clear state
        _stateStoreMock.Verify(x => x.Clear(It.IsAny<string>()), Times.Once);

        // Should NOT set new state
        _stateStoreMock.Verify(
            x => x.Set(It.IsAny<string>(), It.IsAny<CancellationState>()),
            Times.Never);
    }

    #endregion

    #region Single Booking Tests

    /// <summary>
    /// Tests that when a single booking is found, the handler:
    /// - Auto-selects the booking
    /// - Shows booking details
    /// - Asks for confirmation
    /// - Sets state to AwaitingConfirmation
    /// </summary>
    [Fact]
    public async Task ProcessCancellation_SingleBooking_AsksForConfirmation()
    {
        // Arrange
        var phone = "34612345678";
        var message = CreateTextMessage(phone, "quiero cancelar mi reserva");

        var booking = new BookingRecord
        {
            Id = 1,
            CustomerName = "Test User",
            ReservationDate = DateTime.Today.AddDays(7),
            ReservationTime = TimeSpan.FromHours(14),
            PartySize = 4,
            ContactPhone = "612345678"
        };

        _bookingRepoMock
            .Setup(x => x.FindBookingsByPhoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BookingRecord> { booking });

        // Act
        var result = await _handler.ProcessCancellationAsync(message, null);

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(IntentType.Cancellation);
        result.AiResponse.Should().NotBeNullOrWhiteSpace();

        // Should show booking details
        result.AiResponse.Should().Contain("14:00");
        result.AiResponse.Should().Contain("4 personas");

        // Should ask for confirmation (response variations include "Seguro", "Confirmas", "Cancelamos...sí o no", etc.)
        result.AiResponse.Should().MatchRegex("([Ss]eguro|[Cc]onfirm|[Cc]ancel.*sí|sí o no)");

        // Should set state to AwaitingConfirmation with the booking selected
        _stateStoreMock.Verify(
            x => x.Set(It.IsAny<string>(), It.Is<CancellationState>(s =>
                s.Stage == CancellationStage.AwaitingConfirmation &&
                s.SelectedBooking != null &&
                s.SelectedBooking.Id == 1)),
            Times.Once);
    }

    #endregion

    #region Multiple Bookings Tests

    /// <summary>
    /// Tests that when multiple bookings are found, the handler:
    /// - Lists all bookings
    /// - Asks which one to cancel
    /// - Sets state to SelectingBooking
    /// </summary>
    [Fact]
    public async Task ProcessCancellation_MultipleBookings_AsksWhichToCancel()
    {
        // Arrange
        var phone = "34699888777";
        var message = CreateTextMessage(phone, "quiero cancelar mi reserva");

        var bookings = new List<BookingRecord>
        {
            new()
            {
                Id = 1,
                CustomerName = "Test User",
                ReservationDate = DateTime.Today.AddDays(5),
                ReservationTime = TimeSpan.FromHours(14),
                PartySize = 4,
                ContactPhone = "699888777"
            },
            new()
            {
                Id = 2,
                CustomerName = "Test User",
                ReservationDate = DateTime.Today.AddDays(12),
                ReservationTime = TimeSpan.FromHours(20),
                PartySize = 2,
                ArrozType = "banda",
                ContactPhone = "699888777"
            },
            new()
            {
                Id = 3,
                CustomerName = "Test User",
                ReservationDate = DateTime.Today.AddDays(19),
                ReservationTime = TimeSpan.FromHours(13) + TimeSpan.FromMinutes(30),
                PartySize = 6,
                ContactPhone = "699888777"
            }
        };

        _bookingRepoMock
            .Setup(x => x.FindBookingsByPhoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookings);

        // Act
        var result = await _handler.ProcessCancellationAsync(message, null);

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(IntentType.Cancellation);
        result.AiResponse.Should().NotBeNullOrWhiteSpace();

        // Should list multiple bookings
        result.AiResponse.Should().Contain("1.");
        result.AiResponse.Should().Contain("2.");
        result.AiResponse.Should().Contain("3.");

        // Should show booking times
        result.AiResponse.Should().Contain("14:00");
        result.AiResponse.Should().Contain("20:00");
        result.AiResponse.Should().Contain("13:30");

        // Should ask which one to cancel
        result.AiResponse.Should().MatchRegex("[Cc]uál");

        // Should set state to SelectingBooking
        _stateStoreMock.Verify(
            x => x.Set(It.IsAny<string>(), It.Is<CancellationState>(s =>
                s.Stage == CancellationStage.SelectingBooking &&
                s.FoundBookings != null &&
                s.FoundBookings.Count == 3)),
            Times.Once);
    }

    #endregion

    #region Booking Selection Tests

    /// <summary>
    /// Tests that lazy response "la primera" selects the first booking.
    /// </summary>
    [Fact]
    public async Task HandleBookingSelection_LaPrimera_SelectsFirstBooking()
    {
        // Arrange
        var phone = "34612345678";
        var message = CreateTextMessage(phone, "la primera");

        var bookings = new List<BookingRecord>
        {
            new()
            {
                Id = 1,
                ReservationDate = DateTime.Today.AddDays(5),
                ReservationTime = TimeSpan.FromHours(14),
                PartySize = 4,
                ContactPhone = "612345678"
            },
            new()
            {
                Id = 2,
                ReservationDate = DateTime.Today.AddDays(12),
                ReservationTime = TimeSpan.FromHours(20),
                PartySize = 2,
                ContactPhone = "612345678"
            }
        };

        var currentState = new CancellationState
        {
            PhoneNumber = phone,
            Stage = CancellationStage.SelectingBooking,
            FoundBookings = bookings
        };

        // Act
        var result = await _handler.ProcessCancellationAsync(message, currentState);

        // Assert
        result.Should().NotBeNull();

        // Should transition to AwaitingConfirmation and select the first booking
        _stateStoreMock.Verify(
            x => x.Set(It.IsAny<string>(), It.Is<CancellationState>(s =>
                s.Stage == CancellationStage.AwaitingConfirmation &&
                s.SelectedBooking != null &&
                s.SelectedBooking.Id == 1)),
            Times.Once);

        // Should show confirmation prompt with booking details
        result.AiResponse.Should().Contain("14:00");
        result.AiResponse.Should().Contain("4 personas");
    }

    /// <summary>
    /// Tests that selecting by day "la del martes" matches correctly.
    /// </summary>
    [Fact]
    public async Task HandleBookingSelection_LaDelMartes_MatchesByDay()
    {
        // Arrange
        var phone = "34612345678";
        var message = CreateTextMessage(phone, "la del martes");

        // Find a Tuesday that is in the future
        var today = DateTime.Today;
        var daysUntilTuesday = ((int)DayOfWeek.Tuesday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilTuesday == 0) daysUntilTuesday = 7; // Next Tuesday if today is Tuesday
        var tuesdayDate = today.AddDays(daysUntilTuesday);

        // Thursday
        var daysUntilThursday = ((int)DayOfWeek.Thursday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilThursday == 0) daysUntilThursday = 7;
        var thursdayDate = today.AddDays(daysUntilThursday);

        var bookings = new List<BookingRecord>
        {
            new()
            {
                Id = 1,
                ReservationDate = thursdayDate,  // Thursday
                ReservationTime = TimeSpan.FromHours(14),
                PartySize = 4,
                ContactPhone = "612345678"
            },
            new()
            {
                Id = 2,
                ReservationDate = tuesdayDate,  // Tuesday
                ReservationTime = TimeSpan.FromHours(20),
                PartySize = 6,
                ContactPhone = "612345678"
            }
        };

        var currentState = new CancellationState
        {
            PhoneNumber = phone,
            Stage = CancellationStage.SelectingBooking,
            FoundBookings = bookings
        };

        // Act
        var result = await _handler.ProcessCancellationAsync(message, currentState);

        // Assert
        result.Should().NotBeNull();

        // Should select the Tuesday booking (Id = 2)
        _stateStoreMock.Verify(
            x => x.Set(It.IsAny<string>(), It.Is<CancellationState>(s =>
                s.Stage == CancellationStage.AwaitingConfirmation &&
                s.SelectedBooking != null &&
                s.SelectedBooking.Id == 2)),
            Times.Once);

        // Should show confirmation with Tuesday's booking details
        result.AiResponse.Should().Contain("20:00");
        result.AiResponse.Should().Contain("6 personas");
    }

    /// <summary>
    /// Tests that an unrecognized selection asks for clarification.
    /// </summary>
    [Fact]
    public async Task HandleBookingSelection_UnrecognizedInput_AsksForClarification()
    {
        // Arrange
        var phone = "34612345678";
        var message = CreateTextMessage(phone, "cualquiera");

        var bookings = new List<BookingRecord>
        {
            new()
            {
                Id = 1,
                ReservationDate = DateTime.Today.AddDays(5),
                ReservationTime = TimeSpan.FromHours(14),
                PartySize = 4,
                ContactPhone = "612345678"
            },
            new()
            {
                Id = 2,
                ReservationDate = DateTime.Today.AddDays(12),
                ReservationTime = TimeSpan.FromHours(20),
                PartySize = 2,
                ContactPhone = "612345678"
            }
        };

        var currentState = new CancellationState
        {
            PhoneNumber = phone,
            Stage = CancellationStage.SelectingBooking,
            FoundBookings = bookings
        };

        // Act
        var result = await _handler.ProcessCancellationAsync(message, currentState);

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(IntentType.Cancellation);

        // Should ask for clarification
        result.AiResponse.Should().MatchRegex("([Nn]o entendí|[Cc]uál|indica)");

        // Should NOT transition state (stays in SelectingBooking)
        _stateStoreMock.Verify(
            x => x.Set(It.IsAny<string>(), It.Is<CancellationState>(s =>
                s.Stage == CancellationStage.AwaitingConfirmation)),
            Times.Never);
    }

    #endregion

    #region Confirmation Tests

    /// <summary>
    /// Tests that confirmation with "sí" cancels the booking and sends notification.
    /// </summary>
    [Fact]
    public async Task HandleConfirmation_Yes_CancelsAndNotifies()
    {
        // Arrange
        var phone = "34612345678";
        var message = CreateTextMessage(phone, "sí, cancela");

        var selectedBooking = new BookingRecord
        {
            Id = 42,
            CustomerName = "Test User",
            ReservationDate = DateTime.Today.AddDays(7),
            ReservationTime = TimeSpan.FromHours(14),
            PartySize = 4,
            ContactPhone = "612345678"
        };

        var currentState = new CancellationState
        {
            PhoneNumber = phone,
            Stage = CancellationStage.AwaitingConfirmation,
            SelectedBooking = selectedBooking
        };

        _bookingRepoMock
            .Setup(x => x.InsertCancelledBookingAsync(
                It.IsAny<BookingRecord>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _bookingRepoMock
            .Setup(x => x.CancelBookingAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _whatsAppMock
            .Setup(x => x.SendTextAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.ProcessCancellationAsync(message, currentState);

        // Assert
        result.Should().NotBeNull();
        result.AiResponse.Should().NotBeNullOrWhiteSpace();

        // Should indicate success
        result.AiResponse.Should().MatchRegex("([Ll]isto|cancelad|[Hh]echo)");

        // Should clear state
        _stateStoreMock.Verify(x => x.Clear(It.IsAny<string>()), Times.Once);

        // Should archive to cancelled_bookings table
        _bookingRepoMock.Verify(
            x => x.InsertCancelledBookingAsync(
                It.Is<BookingRecord>(b => b.Id == 42),
                "AI_ASSISTANT",
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Should mark booking as cancelled
        _bookingRepoMock.Verify(
            x => x.CancelBookingAsync(42, It.IsAny<CancellationToken>()),
            Times.Once);

        // Should send WhatsApp notification to restaurant
        _whatsAppMock.Verify(
            x => x.SendTextAsync(
                "34692747052",  // Restaurant notification phone
                It.Is<string>(msg =>
                    msg.Contains("Reserva cancelada") &&
                    msg.Contains("Test User")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Should include metadata about cancellation
        result.Metadata.Should().ContainKey("cancelled");
        result.Metadata!["cancelled"].Should().Be(true);
        result.Metadata.Should().ContainKey("bookingId");
        result.Metadata["bookingId"].Should().Be(42);
    }

    /// <summary>
    /// Tests that rejection with "no" keeps the booking active.
    /// </summary>
    [Fact]
    public async Task HandleConfirmation_No_KeepsBookingActive()
    {
        // Arrange
        var phone = "34612345678";
        var message = CreateTextMessage(phone, "no, déjala");

        var selectedBooking = new BookingRecord
        {
            Id = 1,
            CustomerName = "Test User",
            ReservationDate = DateTime.Today.AddDays(7),
            ReservationTime = TimeSpan.FromHours(14),
            PartySize = 4,
            ContactPhone = "612345678"
        };

        var currentState = new CancellationState
        {
            PhoneNumber = phone,
            Stage = CancellationStage.AwaitingConfirmation,
            SelectedBooking = selectedBooking
        };

        // Act
        var result = await _handler.ProcessCancellationAsync(message, currentState);

        // Assert
        result.Should().NotBeNull();
        result.AiResponse.Should().NotBeNullOrWhiteSpace();

        // Should indicate booking is kept (any variation from CancellationAborted)
        result.AiResponse.Should().MatchRegex("([Pp]erfecto|[Ss]igue|manten|[Vv]ale|[Ee]ntendido|[Oo]k|dejamos)");

        // Should clear state
        _stateStoreMock.Verify(x => x.Clear(It.IsAny<string>()), Times.Once);

        // Should NOT cancel booking
        _bookingRepoMock.Verify(
            x => x.CancelBookingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Should NOT archive booking
        _bookingRepoMock.Verify(
            x => x.InsertCancelledBookingAsync(
                It.IsAny<BookingRecord>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        // Should NOT send notification
        _whatsAppMock.Verify(
            x => x.SendTextAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Tests that unrecognized confirmation response asks for clarification.
    /// </summary>
    [Fact]
    public async Task HandleConfirmation_UnrecognizedInput_AsksForClarification()
    {
        // Arrange
        var phone = "34612345678";
        var message = CreateTextMessage(phone, "quizás");

        var selectedBooking = new BookingRecord
        {
            Id = 1,
            CustomerName = "Test User",
            ReservationDate = DateTime.Today.AddDays(7),
            ReservationTime = TimeSpan.FromHours(14),
            PartySize = 4,
            ContactPhone = "612345678"
        };

        var currentState = new CancellationState
        {
            PhoneNumber = phone,
            Stage = CancellationStage.AwaitingConfirmation,
            SelectedBooking = selectedBooking
        };

        // Act
        var result = await _handler.ProcessCancellationAsync(message, currentState);

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(IntentType.Cancellation);

        // Should ask for yes/no clarification
        result.AiResponse.Should().MatchRegex("([Ss]í.*[Nn]o|[Cc]onfirm)");

        // Should NOT clear state
        _stateStoreMock.Verify(x => x.Clear(It.IsAny<string>()), Times.Never);

        // Should NOT cancel
        _bookingRepoMock.Verify(
            x => x.CancelBookingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tests that cancellation failure shows error message.
    /// </summary>
    [Fact]
    public async Task HandleConfirmation_CancellationFails_ShowsErrorMessage()
    {
        // Arrange
        var phone = "34612345678";
        var message = CreateTextMessage(phone, "sí");

        var selectedBooking = new BookingRecord
        {
            Id = 1,
            CustomerName = "Test User",
            ReservationDate = DateTime.Today.AddDays(7),
            ReservationTime = TimeSpan.FromHours(14),
            PartySize = 4,
            ContactPhone = "612345678"
        };

        var currentState = new CancellationState
        {
            PhoneNumber = phone,
            Stage = CancellationStage.AwaitingConfirmation,
            SelectedBooking = selectedBooking
        };

        _bookingRepoMock
            .Setup(x => x.InsertCancelledBookingAsync(
                It.IsAny<BookingRecord>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _bookingRepoMock
            .Setup(x => x.CancelBookingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);  // Cancellation fails

        // Act
        var result = await _handler.ProcessCancellationAsync(message, currentState);

        // Assert
        result.Should().NotBeNull();
        result.AiResponse.Should().NotBeNullOrWhiteSpace();

        // Should indicate error
        result.AiResponse.Should().MatchRegex("([Ee]rror|[Ll]o siento|inténtalo)");

        // Should still clear state
        _stateStoreMock.Verify(x => x.Clear(It.IsAny<string>()), Times.Once);
    }

    /// <summary>
    /// Tests selection by party size "la de 6 personas".
    /// </summary>
    [Fact]
    public async Task HandleBookingSelection_LaDeSeisPersonas_MatchesByPartySize()
    {
        // Arrange
        var phone = "34612345678";
        var message = CreateTextMessage(phone, "la de 6 personas");

        var bookings = new List<BookingRecord>
        {
            new()
            {
                Id = 1,
                ReservationDate = DateTime.Today.AddDays(5),
                ReservationTime = TimeSpan.FromHours(14),
                PartySize = 4,
                ContactPhone = "612345678"
            },
            new()
            {
                Id = 2,
                ReservationDate = DateTime.Today.AddDays(12),
                ReservationTime = TimeSpan.FromHours(20),
                PartySize = 6,
                ContactPhone = "612345678"
            }
        };

        var currentState = new CancellationState
        {
            PhoneNumber = phone,
            Stage = CancellationStage.SelectingBooking,
            FoundBookings = bookings
        };

        // Act
        var result = await _handler.ProcessCancellationAsync(message, currentState);

        // Assert
        result.Should().NotBeNull();

        // Should select the 6-person booking (Id = 2)
        _stateStoreMock.Verify(
            x => x.Set(It.IsAny<string>(), It.Is<CancellationState>(s =>
                s.Stage == CancellationStage.AwaitingConfirmation &&
                s.SelectedBooking != null &&
                s.SelectedBooking.Id == 2)),
            Times.Once);
    }

    /// <summary>
    /// Tests selection by numeric input "2".
    /// </summary>
    [Fact]
    public async Task HandleBookingSelection_NumericInput_SelectsCorrectBooking()
    {
        // Arrange
        var phone = "34612345678";
        var message = CreateTextMessage(phone, "2");

        var bookings = new List<BookingRecord>
        {
            new()
            {
                Id = 10,
                ReservationDate = DateTime.Today.AddDays(5),
                ReservationTime = TimeSpan.FromHours(14),
                PartySize = 4,
                ContactPhone = "612345678"
            },
            new()
            {
                Id = 20,
                ReservationDate = DateTime.Today.AddDays(12),
                ReservationTime = TimeSpan.FromHours(20),
                PartySize = 2,
                ContactPhone = "612345678"
            },
            new()
            {
                Id = 30,
                ReservationDate = DateTime.Today.AddDays(19),
                ReservationTime = TimeSpan.FromHours(13),
                PartySize = 8,
                ContactPhone = "612345678"
            }
        };

        var currentState = new CancellationState
        {
            PhoneNumber = phone,
            Stage = CancellationStage.SelectingBooking,
            FoundBookings = bookings
        };

        // Act
        var result = await _handler.ProcessCancellationAsync(message, currentState);

        // Assert
        result.Should().NotBeNull();

        // Should select the second booking (Id = 20)
        _stateStoreMock.Verify(
            x => x.Set(It.IsAny<string>(), It.Is<CancellationState>(s =>
                s.Stage == CancellationStage.AwaitingConfirmation &&
                s.SelectedBooking != null &&
                s.SelectedBooking.Id == 20)),
            Times.Once);
    }

    #endregion

    #region Helpers

    private static WhatsAppMessage CreateTextMessage(string phone, string text)
    {
        return new WhatsAppMessage
        {
            SenderNumber = phone,
            MessageText = text,
            MessageType = "text",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }

    #endregion
}
