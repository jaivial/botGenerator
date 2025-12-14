using BotGenerator.Core.Agents;
using BotGenerator.Core.Handlers;
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace BotGenerator.Core.Tests.Handlers;

/// <summary>
/// Tests for the rewritten ModificationHandler.
/// Tests the multi-turn modification conversation flow.
/// </summary>
public class ModificationHandlerTests
{
    private readonly Mock<ILogger<ModificationHandler>> _loggerMock;
    private readonly Mock<IBookingRepository> _bookingRepoMock;
    private readonly Mock<IModificationStateStore> _stateStoreMock;
    private readonly Mock<IBookingAvailabilityService> _availabilityMock;
    private readonly Mock<IRiceValidatorService> _riceValidatorMock;
    private readonly Mock<IWhatsAppService> _whatsAppMock;
    private readonly Mock<IContextBuilderService> _contextBuilderMock;
    private readonly ModificationHandler _handler;

    public ModificationHandlerTests()
    {
        _loggerMock = new Mock<ILogger<ModificationHandler>>();
        _bookingRepoMock = new Mock<IBookingRepository>();
        _stateStoreMock = new Mock<IModificationStateStore>();
        _availabilityMock = new Mock<IBookingAvailabilityService>();

        // Use the interface instead of the concrete class to avoid complex mocking
        _riceValidatorMock = new Mock<IRiceValidatorService>();
        _riceValidatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RiceValidationResult.Valid("arroz del senyoret", "senyoret"));

        _whatsAppMock = new Mock<IWhatsAppService>();
        _contextBuilderMock = new Mock<IContextBuilderService>();

        // Create a real RiceValidatorAgent with mocked dependencies
        var riceValidatorAgent = CreateMockRiceValidatorAgent();

        _handler = new ModificationHandler(
            _loggerMock.Object,
            _bookingRepoMock.Object,
            _stateStoreMock.Object,
            _availabilityMock.Object,
            riceValidatorAgent,
            _whatsAppMock.Object,
            _contextBuilderMock.Object);
    }

    private RiceValidatorAgent CreateMockRiceValidatorAgent()
    {
        var geminiMock = new Mock<IGeminiService>();
        var promptLoaderMock = new Mock<IPromptLoaderService>();
        var menuRepoMock = new Mock<IMenuRepository>();
        var loggerMock = new Mock<ILogger<RiceValidatorAgent>>();

        // Setup the prompt loader to return a valid prompt
        promptLoaderMock
            .Setup(x => x.LoadPromptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("Valid rice types: senyoret, banda, mixto");

        promptLoaderMock
            .Setup(x => x.LoadSpecializedPromptAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .ReturnsAsync("Valid rice types: senyoret, banda, mixto");

        // Setup menu repository
        menuRepoMock
            .Setup(x => x.GetActiveRiceTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "arroz del senyoret", "arroz a banda", "arroz mixto" });

        // Create actual agent (validation logic will still work)
        return new RiceValidatorAgent(
            geminiMock.Object,
            promptLoaderMock.Object,
            menuRepoMock.Object,
            loggerMock.Object);
    }

    /// <summary>
    /// Tests that when a single booking is found, the handler:
    /// - Shows booking details (date, time, people)
    /// - Asks what to modify
    /// - Sets state to SelectingField
    /// </summary>
    [Fact]
    public async Task ProcessModification_SingleBooking_AsksWhatToModify()
    {
        // Arrange
        var phone = "34612345678";
        var message = CreateTextMessage(phone, "quiero modificar mi reserva");

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

        _stateStoreMock
            .Setup(x => x.Get(It.IsAny<string>()))
            .Returns((ModificationState?)null);

        // Act
        var result = await _handler.ProcessModificationAsync(message, null);

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(IntentType.Modification);
        result.AiResponse.Should().NotBeNullOrWhiteSpace();

        // Should show booking summary
        result.AiResponse.Should().Contain("14:00");
        result.AiResponse.Should().Contain("4 personas");

        // Should ask what to modify (can use various forms of "cambiar" or "modificar")
        result.AiResponse.Should().MatchRegex("(modific|cambi)");

        // Should list modification options
        result.AiResponse.Should().MatchRegex(@"\d.*[Ff]echa");
        result.AiResponse.Should().MatchRegex(@"\d.*[Hh]ora");
        result.AiResponse.Should().MatchRegex(@"\d.*personas");

        // Should have set state
        _stateStoreMock.Verify(
            x => x.Set(It.IsAny<string>(), It.Is<ModificationState>(s =>
                s.Stage == ModificationStage.SelectingField &&
                s.SelectedBooking != null)),
            Times.Once);
    }

    /// <summary>
    /// Tests that when multiple bookings are found, the handler:
    /// - Lists all bookings
    /// - Asks which one to modify
    /// - Sets state to SelectingBooking
    /// </summary>
    [Fact]
    public async Task ProcessModification_MultipleBookings_AsksWhichToModify()
    {
        // Arrange
        var phone = "34699888777";
        var message = CreateTextMessage(phone, "quiero modificar mi reserva");

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

        _stateStoreMock
            .Setup(x => x.Get(It.IsAny<string>()))
            .Returns((ModificationState?)null);

        // Act
        var result = await _handler.ProcessModificationAsync(message, null);

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(IntentType.Modification);
        result.AiResponse.Should().NotBeNullOrWhiteSpace();

        // Should list multiple bookings
        result.AiResponse.Should().Contain("1.");
        result.AiResponse.Should().Contain("2.");
        result.AiResponse.Should().Contain("3.");

        // Should show booking details
        result.AiResponse.Should().Contain("14:00");
        result.AiResponse.Should().Contain("20:00");
        result.AiResponse.Should().Contain("13:30");

        // Should ask which one to modify
        result.AiResponse.Should().MatchRegex("[Cc]uál");

        // Should have set state to SelectingBooking
        _stateStoreMock.Verify(
            x => x.Set(It.IsAny<string>(), It.Is<ModificationState>(s =>
                s.Stage == ModificationStage.SelectingBooking &&
                s.FoundBookings != null &&
                s.FoundBookings.Count == 3)),
            Times.Once);
    }

    /// <summary>
    /// Tests that when no bookings are found, the handler returns appropriate message.
    /// </summary>
    [Fact]
    public async Task ProcessModification_NoBookings_ReturnsNoBookingsMessage()
    {
        // Arrange
        var phone = "34600000000";
        var message = CreateTextMessage(phone, "quiero modificar mi reserva");

        _bookingRepoMock
            .Setup(x => x.FindBookingsByPhoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BookingRecord>());

        _stateStoreMock
            .Setup(x => x.Get(It.IsAny<string>()))
            .Returns((ModificationState?)null);

        // Act
        var result = await _handler.ProcessModificationAsync(message, null);

        // Assert
        result.Should().NotBeNull();
        result.AiResponse.Should().NotBeNullOrWhiteSpace();

        // Should indicate no bookings found
        result.AiResponse.Should().MatchRegex("([Nn]o.*encontr|[Nn]o.*reserva)");

        // Should offer to make a new reservation
        result.AiResponse.Should().MatchRegex("(nueva|hacer|reservar)");

        // Should NOT set any state
        _stateStoreMock.Verify(
            x => x.Set(It.IsAny<string>(), It.IsAny<ModificationState>()),
            Times.Never);
    }

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

        var currentState = new ModificationState
        {
            PhoneNumber = phone,
            Stage = ModificationStage.SelectingBooking,
            FoundBookings = bookings
        };

        // Act
        var result = await _handler.ProcessModificationAsync(message, currentState);

        // Assert
        result.Should().NotBeNull();

        // Should transition to SelectingField and select the first booking
        _stateStoreMock.Verify(
            x => x.Set(It.IsAny<string>(), It.Is<ModificationState>(s =>
                s.Stage == ModificationStage.SelectingField &&
                s.SelectedBooking != null &&
                s.SelectedBooking.Id == 1)),
            Times.Once);
    }

    /// <summary>
    /// Tests that confirmation with "sí" applies the changes.
    /// </summary>
    [Fact]
    public async Task HandleConfirmation_Yes_AppliesChanges()
    {
        // Arrange
        var phone = "34612345678";
        var message = CreateTextMessage(phone, "sí, confirmo");

        var selectedBooking = new BookingRecord
        {
            Id = 1,
            ReservationDate = DateTime.Today.AddDays(7),
            ReservationTime = TimeSpan.FromHours(14),
            PartySize = 4,
            ContactPhone = "612345678"
        };

        var pendingChanges = new BookingUpdateData
        {
            PartySize = 6
        };

        var currentState = new ModificationState
        {
            PhoneNumber = phone,
            Stage = ModificationStage.AwaitingConfirmation,
            SelectedBooking = selectedBooking,
            PendingChanges = pendingChanges,
            ChangeDescription = "cambiar de 4 a 6 personas"
        };

        _bookingRepoMock
            .Setup(x => x.UpdateBookingAsync(1, It.IsAny<BookingUpdateData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.ProcessModificationAsync(message, currentState);

        // Assert
        result.Should().NotBeNull();
        result.AiResponse.Should().NotBeNullOrWhiteSpace();

        // Should indicate success
        result.AiResponse.Should().MatchRegex("(modificad|actualizad|[Ll]isto|[Hh]echo|[Pp]erfecto)");

        // Should clear state
        _stateStoreMock.Verify(x => x.Clear(It.IsAny<string>()), Times.Once);

        // Should update database
        _bookingRepoMock.Verify(
            x => x.UpdateBookingAsync(1, It.IsAny<BookingUpdateData>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that cancellation with "no" cancels the modification.
    /// </summary>
    [Fact]
    public async Task HandleConfirmation_No_CancelsModification()
    {
        // Arrange
        var phone = "34612345678";
        var message = CreateTextMessage(phone, "no");

        var selectedBooking = new BookingRecord
        {
            Id = 1,
            ReservationDate = DateTime.Today.AddDays(7),
            ReservationTime = TimeSpan.FromHours(14),
            PartySize = 4,
            ContactPhone = "612345678"
        };

        var currentState = new ModificationState
        {
            PhoneNumber = phone,
            Stage = ModificationStage.AwaitingConfirmation,
            SelectedBooking = selectedBooking,
            PendingChanges = new BookingUpdateData { PartySize = 6 },
            ChangeDescription = "cambiar de 4 a 6 personas"
        };

        // Act
        var result = await _handler.ProcessModificationAsync(message, currentState);

        // Assert
        result.Should().NotBeNull();
        result.AiResponse.Should().NotBeNullOrWhiteSpace();

        // Should indicate cancellation
        result.AiResponse.Should().MatchRegex("([Nn]o.*cambio|cancel|igual)");

        // Should clear state
        _stateStoreMock.Verify(x => x.Clear(It.IsAny<string>()), Times.Once);

        // Should NOT update database
        _bookingRepoMock.Verify(
            x => x.UpdateBookingAsync(It.IsAny<int>(), It.IsAny<BookingUpdateData>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Tests that >10 people triggers contact card.
    /// </summary>
    [Fact]
    public async Task HandlePartySize_MoreThan10_SendsContactCard()
    {
        // Arrange
        var phone = "34612345678";
        var message = CreateTextMessage(phone, "seremos 15");

        var selectedBooking = new BookingRecord
        {
            Id = 1,
            ReservationDate = DateTime.Today.AddDays(7),
            ReservationTime = TimeSpan.FromHours(14),
            PartySize = 4,
            ContactPhone = "612345678"
        };

        var currentState = new ModificationState
        {
            PhoneNumber = phone,
            Stage = ModificationStage.CollectingNewValue,
            SelectedBooking = selectedBooking,
            FieldToModify = "party_size"
        };

        // Act
        var result = await _handler.ProcessModificationAsync(message, currentState);

        // Assert
        result.Should().NotBeNull();
        result.AiResponse.Should().NotBeNullOrWhiteSpace();

        // Should mention large group/contact team
        result.AiResponse.Should().MatchRegex("(grupo|10|equipo|contacto)");

        // Should send contact card
        _whatsAppMock.Verify(
            x => x.SendContactCardAsync(
                It.IsAny<string>(),  // phoneNumber
                It.IsAny<string>(),  // fullName
                It.IsAny<string>(),  // contactPhoneNumber
                It.IsAny<string?>(), // organization
                It.IsAny<string?>(), // email
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Should clear state
        _stateStoreMock.Verify(x => x.Clear(It.IsAny<string>()), Times.Once);
    }

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
}
