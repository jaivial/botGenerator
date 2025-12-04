using BotGenerator.Core.Agents;
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.RegularExpressions;

namespace BotGenerator.Core.Tests.Agents;

/// <summary>
/// Tests for MainConversationAgent - Steps 26-30: Message Parsing Tests
/// Tests that BOOKING_REQUEST commands from AI responses are parsed correctly.
/// </summary>
public class MainConversationAgentTests
{
    private readonly Mock<IGeminiService> _geminiMock;
    private readonly Mock<IPromptLoaderService> _promptLoaderMock;
    private readonly Mock<IContextBuilderService> _contextBuilderMock;
    private readonly Mock<IConversationHistoryService> _historyServiceMock;
    private readonly Mock<IMenuRepository> _menuRepositoryMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<MainConversationAgent>> _loggerMock;
    private readonly MainConversationAgent _agent;

    public MainConversationAgentTests()
    {
        _geminiMock = new Mock<IGeminiService>();
        _promptLoaderMock = new Mock<IPromptLoaderService>();
        _contextBuilderMock = new Mock<IContextBuilderService>();
        _historyServiceMock = new Mock<IConversationHistoryService>();
        _menuRepositoryMock = new Mock<IMenuRepository>();
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<MainConversationAgent>>();

        // Setup default menu repository
        _menuRepositoryMock.Setup(x => x.GetActiveRiceTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Arroz de chorizo", "Arroz Negro", "Paella valenciana" });

        // Setup default configuration
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(x => x.Value).Returns("villacarmen");
        _configurationMock.Setup(x => x["Restaurants:Default"]).Returns("villacarmen");
        _configurationMock.Setup(x => x.GetSection("Restaurants:Mapping"))
            .Returns(Mock.Of<IConfigurationSection>(s => s.GetChildren() == new List<IConfigurationSection>()));

        _agent = new MainConversationAgent(
            _geminiMock.Object,
            _promptLoaderMock.Object,
            _contextBuilderMock.Object,
            _historyServiceMock.Object,
            _menuRepositoryMock.Object,
            _configurationMock.Object,
            _loggerMock.Object);
    }

    /// <summary>
    /// Step 26: MainAgent_ParsesBookingRequest_ExtractsData
    /// Tests that BOOKING_REQUEST command is parsed correctly and all data is extracted.
    /// </summary>
    [Fact]
    public async Task MainAgent_ParsesBookingRequest_ExtractsData()
    {
        // Arrange
        var message = CreateTestMessage("Quiero reservar para 4 personas el 30/11/2024 a las 14:00");
        var aiResponse = "Perfecto! Voy a proceder con tu reserva.\n\n" +
                        "BOOKING_REQUEST|Juan Pérez|34612345678|30/11/2024|4|14:00\n\n" +
                        "Tu reserva ha sido confirmada.";

        SetupMocks(aiResponse);

        // Act
        var result = await _agent.ProcessAsync(message, null, null);

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(IntentType.Booking);
        result.ExtractedData.Should().NotBeNull();
        result.ExtractedData!.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// Step 27: MainAgent_ParsesBookingRequest_ExtractsName
    /// Tests that customer name is extracted correctly from BOOKING_REQUEST command.
    /// </summary>
    [Fact]
    public async Task MainAgent_ParsesBookingRequest_ExtractsName()
    {
        // Arrange
        var message = CreateTestMessage("Me llamo Carlos García, quiero reservar");
        var aiResponse = "BOOKING_REQUEST|Carlos García|34678901234|01/12/2024|2|13:30\n\n" +
                        "Reserva confirmada a nombre de Carlos García.";

        SetupMocks(aiResponse);

        // Act
        var result = await _agent.ProcessAsync(message, null, null);

        // Assert
        result.ExtractedData.Should().NotBeNull();
        result.ExtractedData!.Name.Should().Be("Carlos García");
    }

    /// <summary>
    /// Step 28: MainAgent_ParsesBookingRequest_ExtractsPhone
    /// Tests that phone number is extracted correctly from BOOKING_REQUEST command.
    /// </summary>
    [Fact]
    public async Task MainAgent_ParsesBookingRequest_ExtractsPhone()
    {
        // Arrange
        var message = CreateTestMessage("Mi teléfono es 34612345678");
        var aiResponse = "BOOKING_REQUEST|Ana López|34612345678|02/12/2024|3|20:00\n\n" +
                        "Te confirmo la reserva.";

        SetupMocks(aiResponse);

        // Act
        var result = await _agent.ProcessAsync(message, null, null);

        // Assert
        result.ExtractedData.Should().NotBeNull();
        result.ExtractedData!.Phone.Should().Be("34612345678");
    }

    /// <summary>
    /// Step 29: MainAgent_ParsesBookingRequest_ExtractsDate
    /// Tests that date in dd/MM/yyyy format is extracted correctly.
    /// </summary>
    [Fact]
    public async Task MainAgent_ParsesBookingRequest_ExtractsDate()
    {
        // Arrange
        var message = CreateTestMessage("Para el 15/12/2024");
        var aiResponse = "BOOKING_REQUEST|Pedro Martínez|34611222333|15/12/2024|5|19:30\n\n" +
                        "Reserva confirmada para el 15/12/2024.";

        SetupMocks(aiResponse);

        // Act
        var result = await _agent.ProcessAsync(message, null, null);

        // Assert
        result.ExtractedData.Should().NotBeNull();
        result.ExtractedData!.Date.Should().Be("15/12/2024");
    }

    /// <summary>
    /// Step 30: MainAgent_ParsesBookingRequest_ExtractsPeople
    /// Tests that number of people is parsed as an integer correctly.
    /// </summary>
    [Fact]
    public async Task MainAgent_ParsesBookingRequest_ExtractsPeople()
    {
        // Arrange
        var message = CreateTestMessage("Somos 8 personas");
        var aiResponse = "BOOKING_REQUEST|María Rodríguez|34655666777|20/12/2024|8|21:00\n\n" +
                        "Reserva para 8 personas confirmada.";

        SetupMocks(aiResponse);

        // Act
        var result = await _agent.ProcessAsync(message, null, null);

        // Assert
        result.ExtractedData.Should().NotBeNull();
        result.ExtractedData!.People.Should().Be(8);
        result.ExtractedData!.People.Should().BeOfType(typeof(int));
    }

    /// <summary>
    /// Additional test: Verify that the command is removed from the clean response.
    /// </summary>
    [Fact]
    public async Task MainAgent_ParsesBookingRequest_RemovesCommandFromResponse()
    {
        // Arrange
        var message = CreateTestMessage("Quiero reservar");
        var aiResponse = "Perfecto! Voy a confirmar tu reserva.\n\n" +
                        "BOOKING_REQUEST|Luis Fernández|34622333444|25/12/2024|6|13:00\n\n" +
                        "Tu reserva está confirmada. Nos vemos pronto!";

        SetupMocks(aiResponse);

        // Act
        var result = await _agent.ProcessAsync(message, null, null);

        // Assert
        result.AiResponse.Should().NotContain("BOOKING_REQUEST");
        result.AiResponse.Should().NotContain("34622333444");
        result.AiResponse.Should().Contain("Perfecto");
        result.AiResponse.Should().Contain("confirmada");
    }

    /// <summary>
    /// Additional test: Verify that all fields are extracted together correctly.
    /// </summary>
    [Fact]
    public async Task MainAgent_ParsesBookingRequest_ExtractsAllFieldsCorrectly()
    {
        // Arrange
        var message = CreateTestMessage("Reserva completa");
        var aiResponse = "BOOKING_REQUEST|Roberto Santos|34633444555|31/12/2024|10|22:00\n\n" +
                        "Reserva de fin de año confirmada!";

        SetupMocks(aiResponse);

        // Act
        var result = await _agent.ProcessAsync(message, null, null);

        // Assert
        result.ExtractedData.Should().NotBeNull();
        var data = result.ExtractedData!;
        data.Name.Should().Be("Roberto Santos");
        data.Phone.Should().Be("34633444555");
        data.Date.Should().Be("31/12/2024");
        data.People.Should().Be(10);
        data.Time.Should().Be("22:00");
        data.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// Additional test: Verify handling of escaped characters in AI response.
    /// The AI might escape pipe characters as \| which needs to be unescaped.
    /// </summary>
    [Fact]
    public async Task MainAgent_ParsesBookingRequest_HandlesEscapedCharacters()
    {
        // Arrange
        var message = CreateTestMessage("Reserva con caracteres escapados");
        var aiResponse = "BOOKING_REQUEST\\|Elena Ruiz\\|34644555666\\|05/01/2025\\|4\\|12:30\n\n" +
                        "Reserva confirmada.";

        SetupMocks(aiResponse);

        // Act
        var result = await _agent.ProcessAsync(message, null, null);

        // Assert
        result.ExtractedData.Should().NotBeNull();
        var data = result.ExtractedData!;
        data.Name.Should().Be("Elena Ruiz");
        data.Phone.Should().Be("34644555666");
        data.Date.Should().Be("05/01/2025");
        data.People.Should().Be(4);
        data.Time.Should().Be("12:30");
    }

    /// <summary>
    /// Step 31: MainAgent_ParsesBookingRequest_ExtractsTime
    /// Tests that time in HH:mm format is extracted correctly from BOOKING_REQUEST command.
    /// </summary>
    [Fact]
    public async Task MainAgent_ParsesBookingRequest_ExtractsTime()
    {
        // Arrange
        var message = CreateTestMessage("Reserva para las 14:30");
        var aiResponse = "BOOKING_REQUEST|Miguel Torres|34612345678|01/12/2024|4|14:30\n\n" +
                        "Reserva confirmada para las 14:30.";

        SetupMocks(aiResponse);

        // Act
        var result = await _agent.ProcessAsync(message, null, null);

        // Assert
        result.ExtractedData.Should().NotBeNull();
        result.ExtractedData!.Time.Should().Be("14:30");
        result.ExtractedData!.Time.Should().MatchRegex(@"^\d{2}:\d{2}$");
    }

    /// <summary>
    /// Step 32: MainAgent_ParsesCancellationRequest_ExtractsData
    /// Tests that CANCELLATION_REQUEST command is parsed correctly and all data is extracted.
    /// </summary>
    [Fact]
    public async Task MainAgent_ParsesCancellationRequest_ExtractsData()
    {
        // Arrange
        var message = CreateTestMessage("Quiero cancelar mi reserva del 30/11/2024");
        var aiResponse = "Entendido, procedo a cancelar tu reserva.\n\n" +
                        "CANCELLATION_REQUEST|Juan Pérez|34612345678|30/11/2024|4|14:00\n\n" +
                        "Tu reserva ha sido cancelada.";

        SetupMocks(aiResponse);

        // Act
        var result = await _agent.ProcessAsync(message, null, null);

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(IntentType.Cancellation);
        result.ExtractedData.Should().NotBeNull();
        result.ExtractedData!.Name.Should().Be("Juan Pérez");
        result.ExtractedData!.Phone.Should().Be("34612345678");
        result.ExtractedData!.Date.Should().Be("30/11/2024");
        result.ExtractedData!.People.Should().Be(4);
        result.ExtractedData!.Time.Should().Be("14:00");
        result.AiResponse.Should().NotContain("CANCELLATION_REQUEST");
    }

    /// <summary>
    /// Step 33: MainAgent_ParsesModificationIntent
    /// Tests that MODIFICATION_INTENT flag is detected and intent is set correctly.
    /// </summary>
    [Fact]
    public async Task MainAgent_ParsesModificationIntent()
    {
        // Arrange
        var message = CreateTestMessage("Quiero cambiar mi reserva");
        var aiResponse = "Claro, te ayudo a modificar tu reserva.\n\n" +
                        "MODIFICATION_INTENT\n\n" +
                        "¿Qué deseas cambiar?";

        SetupMocks(aiResponse);

        // Act
        var result = await _agent.ProcessAsync(message, null, null);

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(IntentType.Modification);
        result.AiResponse.Should().NotContain("MODIFICATION_INTENT");
        result.AiResponse.Should().Contain("modificar");
        result.AiResponse.Should().Contain("cambiar");
    }

    /// <summary>
    /// Step 34: MainAgent_ParsesSameDayBooking
    /// Tests that SAME_DAY_BOOKING intent is detected for same-day booking attempts.
    /// </summary>
    [Fact]
    public async Task MainAgent_ParsesSameDayBooking()
    {
        // Arrange
        var message = CreateTestMessage("Quiero reservar para hoy");
        var aiResponse = "SAME_DAY_BOOKING\n\n" +
                        "Lo sentimos, no aceptamos reservas para el mismo día. " +
                        "Por favor, llámanos al +34 638 857 294 para consultar disponibilidad.";

        SetupMocks(aiResponse);

        // Act
        var result = await _agent.ProcessAsync(message, null, null);

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(IntentType.SameDay);
        result.AiResponse.Should().NotContain("SAME_DAY_BOOKING");
        result.AiResponse.Should().Contain("mismo día");
        result.AiResponse.Should().Contain("llámanos");
    }

    /// <summary>
    /// Step 35: MainAgent_ExtractsRiceType_FromResponse
    /// Tests that rice type is extracted from BOOKING_REQUEST or additional text.
    /// </summary>
    [Fact]
    public async Task MainAgent_ExtractsRiceType_FromResponse()
    {
        // Arrange
        var message = CreateTestMessage("Quiero reservar y pedir Arroz del señoret");
        var aiResponse = "BOOKING_REQUEST|Carlos Martín|34655666777|05/12/2024|4|13:30\n\n" +
                        "Perfecto! He anotado tu pedido de Arroz del señoret para 4 personas. " +
                        "Tu reserva está confirmada.";

        SetupMocks(aiResponse);

        // Act
        var result = await _agent.ProcessAsync(message, null, null);

        // Assert
        result.ExtractedData.Should().NotBeNull();
        result.ExtractedData!.ArrozType.Should().NotBeNullOrEmpty();
        result.ExtractedData!.ArrozType.Should().ContainAny("señoret", "del señoret");
    }

    /// <summary>
    /// Step 36: MainAgent_ExtractsRiceServings_FromResponse
    /// Tests that rice servings count is extracted from patterns like "3 raciones".
    /// </summary>
    [Theory]
    [InlineData("3 raciones de arroz", 3)]
    [InlineData("1 ración de arroz", 1)]
    [InlineData("5 raciones", 5)]
    [InlineData("2 raciones de arroz del señoret", 2)]
    public async Task MainAgent_ExtractsRiceServings_FromResponse(string servingsText, int expectedServings)
    {
        // Arrange
        var message = CreateTestMessage($"Quiero reservar y pedir {servingsText}");
        var aiResponse = $"BOOKING_REQUEST|Juan Pérez|34666123456|30/11/2024|4|14:00\n\n" +
                        $"Perfecto, {servingsText}. Tu reserva está confirmada.";

        SetupMocks(aiResponse);

        // Act
        var result = await _agent.ProcessAsync(message, null, null);

        // Assert
        result.ExtractedData.Should().NotBeNull();
        result.ExtractedData!.ArrozServings.Should().Be(expectedServings);
    }

    /// <summary>
    /// Step 37: MainAgent_ExtractsHighChairs_FromResponse
    /// Tests that high chair count is extracted from patterns like "2 tronas".
    /// </summary>
    [Theory]
    [InlineData("2 tronas", 2)]
    [InlineData("1 trona", 1)]
    [InlineData("3 tronas para los niños", 3)]
    public async Task MainAgent_ExtractsHighChairs_FromResponse(string chairText, int expectedChairs)
    {
        // Arrange
        var message = CreateTestMessage($"Necesito {chairText}");
        var aiResponse = $"BOOKING_REQUEST|Ana López|34666123456|30/11/2024|4|14:00\n\n" +
                        $"Con {chairText}. Reserva confirmada.";

        SetupMocks(aiResponse);

        // Act
        var result = await _agent.ProcessAsync(message, null, null);

        // Assert
        result.ExtractedData.Should().NotBeNull();
        result.ExtractedData!.HighChairs.Should().Be(expectedChairs);
    }

    /// <summary>
    /// Step 38: MainAgent_ExtractsBabyStrollers_FromResponse
    /// Tests that baby stroller count is extracted from patterns like "2 carritos".
    /// </summary>
    [Theory]
    [InlineData("2 carritos", 2)]
    [InlineData("1 carrito", 1)]
    [InlineData("3 carritos de bebé", 3)]
    public async Task MainAgent_ExtractsBabyStrollers_FromResponse(string strollerText, int expectedStrollers)
    {
        // Arrange
        var message = CreateTestMessage($"Traemos {strollerText}");
        var aiResponse = $"BOOKING_REQUEST|Pedro Martín|34666123456|30/11/2024|4|14:00\n\n" +
                        $"Con {strollerText}. Reserva confirmada.";

        SetupMocks(aiResponse);

        // Act
        var result = await _agent.ProcessAsync(message, null, null);

        // Assert
        result.ExtractedData.Should().NotBeNull();
        result.ExtractedData!.BabyStrollers.Should().Be(expectedStrollers);
    }

    /// <summary>
    /// Step 39: MainAgent_DetectsUrls_SetsInteractiveIntent
    /// Tests that URLs in the response are detected and metadata is set.
    /// </summary>
    [Theory]
    [InlineData("Puedes ver nuestro menú en https://example.com/menu", true, 1)]
    [InlineData("Visita http://maps.google.com y https://example.com", true, 2)]
    [InlineData("No hay ninguna URL aquí", false, 0)]
    public async Task MainAgent_DetectsUrls_SetsInteractiveIntent(string responseText, bool hasUrls, int urlCount)
    {
        // Arrange
        var message = CreateTestMessage("Dónde están ubicados?");
        var aiResponse = responseText;

        SetupMocks(aiResponse);

        // Act
        var result = await _agent.ProcessAsync(message, null, null);

        // Assert
        result.Should().NotBeNull();
        if (hasUrls)
        {
            result.Metadata.Should().NotBeNull();
            result.Metadata.Should().ContainKey("hasUrls");
            ((bool)result.Metadata!["hasUrls"]).Should().BeTrue();
            result.Metadata.Should().ContainKey("urls");
            ((List<string>)result.Metadata["urls"]).Should().HaveCount(urlCount);
        }
        else
        {
            if (result.Metadata != null)
            {
                result.Metadata.Should().NotContainKey("hasUrls");
            }
        }
    }

    /// <summary>
    /// Step 40: MainAgent_CleansMarkdown_EscapedChars
    /// Tests that escaped markdown characters are cleaned (\_text\_ → _text_).
    /// </summary>
    [Theory]
    [InlineData(@"Reserva\_confirmada", "Reserva_confirmada")]
    [InlineData(@"Opción\|A", "Opción|A")]
    [InlineData(@"Texto\*bold\*", "Texto*bold*")]
    [InlineData(@"Nombre: Juan\_Pérez", "Nombre: Juan_Pérez")]
    public async Task MainAgent_CleansMarkdown_EscapedChars(string input, string expected)
    {
        // Arrange
        var message = CreateTestMessage("Confirma mi reserva");
        var aiResponse = input;

        SetupMocks(aiResponse);

        // Act
        var result = await _agent.ProcessAsync(message, null, null);

        // Assert
        result.Should().NotBeNull();
        result.AiResponse.Should().Be(expected);
    }

    /// <summary>
    /// Step 41: MainAgent_CleansMarkdown_DoubleBoldToSingle
    /// Tests that **text** is converted to *text* for WhatsApp (which doesn't support double asterisks).
    /// </summary>
    [Theory]
    [InlineData("**Reserva confirmada**", "*Reserva confirmada*")]
    [InlineData("Tu **mesa** está lista", "Tu *mesa* está lista")]
    [InlineData("**Importante:** Llega **puntual**", "*Importante:* Llega *puntual*")]
    [InlineData("Texto sin formato", "Texto sin formato")]
    public async Task MainAgent_CleansMarkdown_DoubleBoldToSingle(string input, string expected)
    {
        // Arrange
        var message = CreateTestMessage("Confirma mi reserva");
        var aiResponse = input;

        SetupMocks(aiResponse);

        // Act
        var result = await _agent.ProcessAsync(message, null, null);

        // Assert
        result.Should().NotBeNull();
        result.AiResponse.Should().Be(expected);
    }

    /// <summary>
    /// Step 42: MainAgent_CleansMarkdown_MultipleNewlines
    /// Tests that 3+ consecutive newlines are reduced to 2 (double newline for paragraph separation).
    /// </summary>
    [Theory]
    [InlineData("Línea 1\n\n\nLínea 2", "Línea 1\n\nLínea 2")]
    [InlineData("Línea 1\n\n\n\n\nLínea 2", "Línea 1\n\nLínea 2")]
    [InlineData("Línea 1\n\nLínea 2", "Línea 1\n\nLínea 2")]
    [InlineData("A\n\n\nB\n\n\nC", "A\n\nB\n\nC")]
    public async Task MainAgent_CleansMarkdown_MultipleNewlines(string input, string expected)
    {
        // Arrange
        var message = CreateTestMessage("Dime información");
        var aiResponse = input;

        SetupMocks(aiResponse);

        // Act
        var result = await _agent.ProcessAsync(message, null, null);

        // Assert
        result.Should().NotBeNull();
        result.AiResponse.Should().Be(expected);
    }

    /// <summary>
    /// Step 43: MainAgent_CleansMarkdown_WhitespaceLines
    /// Tests that lines containing only whitespace (spaces, tabs) are removed.
    /// </summary>
    [Theory]
    [InlineData("Línea 1\n   \nLínea 2", "Línea 1\nLínea 2")]
    [InlineData("Línea 1\n\t\t\nLínea 2", "Línea 1\nLínea 2")]
    [InlineData("Línea 1\n \t \nLínea 2", "Línea 1\nLínea 2")]
    [InlineData("Inicio\n   \n\t\nFin", "Inicio\nFin")]
    public async Task MainAgent_CleansMarkdown_WhitespaceLines(string input, string expected)
    {
        // Arrange
        var message = CreateTestMessage("Información del restaurante");
        var aiResponse = input;

        SetupMocks(aiResponse);

        // Act
        var result = await _agent.ProcessAsync(message, null, null);

        // Assert
        result.Should().NotBeNull();
        result.AiResponse.Should().Be(expected);
    }

    /// <summary>
    /// Step 44: MainAgent_EmptyCleanedResponse_DefaultMessage
    /// Tests that when the cleaned response is empty or whitespace-only, a fallback message is provided.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\n\n")]
    [InlineData("\t\t")]
    [InlineData("   \n   \n   ")]
    public async Task MainAgent_EmptyCleanedResponse_DefaultMessage(string input)
    {
        // Arrange
        var message = CreateTestMessage("Pregunta confusa");
        var aiResponse = input;

        SetupMocks(aiResponse);

        // Act
        var result = await _agent.ProcessAsync(message, null, null);

        // Assert
        result.Should().NotBeNull();
        result.AiResponse.Should().NotBeNullOrWhiteSpace();
        result.AiResponse.Should().Contain("Disculpa");
        result.AiResponse.Should().MatchRegex(@"(?i)(no he entendido|repetir)");
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

    /// <summary>
    /// Sets up mocks for a successful AI interaction.
    /// </summary>
    private void SetupMocks(string aiResponse)
    {
        // Setup history service
        _historyServiceMock
            .Setup(x => x.GetHistoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessage>());

        _historyServiceMock
            .Setup(x => x.ExtractState(It.IsAny<List<ChatMessage>>()))
            .Returns(ConversationState.Empty());

        _historyServiceMock
            .Setup(x => x.AddMessageAsync(
                It.IsAny<string>(),
                It.IsAny<ChatMessage>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Setup context builder
        _contextBuilderMock
            .Setup(x => x.BuildContext(
                It.IsAny<WhatsAppMessage>(),
                It.IsAny<ConversationState?>(),
                It.IsAny<List<ChatMessage>?>(),
                null))
            .Returns(new Dictionary<string, object>
            {
                ["currentDate"] = DateTime.UtcNow.ToString("dd/MM/yyyy"),
                ["currentTime"] = DateTime.UtcNow.ToString("HH:mm")
            });

        // Setup prompt loader
        _promptLoaderMock
            .Setup(x => x.AssembleSystemPromptAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>()))
            .ReturnsAsync("System prompt for restaurant booking");

        // Setup Gemini service to return the AI response
        _geminiMock
            .Setup(x => x.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(aiResponse);
    }

    #endregion
}
