using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace BotGenerator.Core.Tests.Services;

/// <summary>
/// Tests for ConversationHistoryService (Steps 51-55).
/// Covers history retrieval, caching, storing, trimming, and session timeout.
/// </summary>
public class ConversationHistoryServiceTests
{
    private readonly Mock<IContextBuilderService> _contextBuilderMock;
    private readonly Mock<ILogger<ConversationHistoryService>> _loggerMock;
    private readonly IConfiguration _configuration;
    private readonly ConversationHistoryService _service;

    public ConversationHistoryServiceTests()
    {
        _contextBuilderMock = new Mock<IContextBuilderService>();
        _loggerMock = new Mock<ILogger<ConversationHistoryService>>();

        // Setup configuration with test values
        var configDict = new Dictionary<string, string>
        {
            { "History:MaxMessages", "30" },
            { "History:SessionTimeoutMinutes", "30" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict!)
            .Build();

        _service = new ConversationHistoryService(
            _contextBuilderMock.Object,
            _configuration,
            _loggerMock.Object);
    }

    /// <summary>
    /// Step 51: HistoryService_GetHistory_ReturnsEmpty_NewUser
    /// New user with no history should return empty list.
    /// </summary>
    [Fact]
    public async Task GetHistory_ReturnsEmpty_NewUser()
    {
        // Arrange
        var phoneNumber = "+1234567890";

        // Act
        var result = await _service.GetHistoryAsync(phoneNumber);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    /// <summary>
    /// Step 52: HistoryService_GetHistory_ReturnsCached
    /// Previously stored messages should be returned from cache.
    /// </summary>
    [Fact]
    public async Task GetHistory_ReturnsCached()
    {
        // Arrange
        var phoneNumber = "+1234567890";
        var message1 = ChatMessage.FromUser("Hola");
        var message2 = ChatMessage.FromAssistant("Bienvenido");
        var message3 = ChatMessage.FromUser("Quiero reservar");

        // Add messages to history
        await _service.AddMessageAsync(phoneNumber, message1);
        await _service.AddMessageAsync(phoneNumber, message2);
        await _service.AddMessageAsync(phoneNumber, message3);

        // Act
        var result = await _service.GetHistoryAsync(phoneNumber);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result[0].Content.Should().Be("Hola");
        result[0].Role.Should().Be("user");
        result[1].Content.Should().Be("Bienvenido");
        result[1].Role.Should().Be("assistant");
        result[2].Content.Should().Be("Quiero reservar");
        result[2].Role.Should().Be("user");
    }

    /// <summary>
    /// Step 53: HistoryService_AddMessage_StoresMessage
    /// Messages should be stored and retrievable.
    /// </summary>
    [Fact]
    public async Task AddMessage_StoresMessage()
    {
        // Arrange
        var phoneNumber = "+1234567890";
        var userMessage = ChatMessage.FromUser("Buenos días", "Carlos");
        var assistantMessage = ChatMessage.FromAssistant("Hola Carlos, ¿en qué puedo ayudarte?");

        // Act
        await _service.AddMessageAsync(phoneNumber, userMessage);
        await _service.AddMessageAsync(phoneNumber, assistantMessage);

        // Assert
        var history = await _service.GetHistoryAsync(phoneNumber);
        history.Should().HaveCount(2);

        var firstMessage = history[0];
        firstMessage.Content.Should().Be("Buenos días");
        firstMessage.Role.Should().Be("user");
        firstMessage.FromName.Should().Be("Carlos");

        var secondMessage = history[1];
        secondMessage.Content.Should().Be("Hola Carlos, ¿en qué puedo ayudarte?");
        secondMessage.Role.Should().Be("assistant");
    }

    /// <summary>
    /// Step 54: HistoryService_AddMessage_TrimsToMaxMessages
    /// History should be trimmed to 30 messages (keeping most recent).
    /// </summary>
    [Fact]
    public async Task AddMessage_TrimsToMaxMessages()
    {
        // Arrange
        var phoneNumber = "+1234567890";
        var maxMessages = 30;

        // Add 35 messages (exceeds max of 30)
        for (int i = 1; i <= 35; i++)
        {
            var message = i % 2 == 1
                ? ChatMessage.FromUser($"User message {i}")
                : ChatMessage.FromAssistant($"Assistant message {i}");

            await _service.AddMessageAsync(phoneNumber, message);
        }

        // Act
        var history = await _service.GetHistoryAsync(phoneNumber);

        // Assert
        history.Should().HaveCount(maxMessages, "history should be trimmed to max messages");

        // Should keep the LAST 30 messages (messages 6-35)
        // Message 6 is even, so it's an Assistant message (i % 2 == 0)
        history.First().Content.Should().Be("Assistant message 6", "oldest message should be #6");
        history.Last().Content.Should().Be("User message 35", "newest message should be #35");

        // Verify the first 5 messages were removed (using exact matching)
        history.Should().NotContain(m => m.Content == "User message 1");
        history.Should().NotContain(m => m.Content == "Assistant message 2");
        history.Should().NotContain(m => m.Content == "User message 3");
        history.Should().NotContain(m => m.Content == "Assistant message 4");
        history.Should().NotContain(m => m.Content == "User message 5");
    }

    /// <summary>
    /// Step 55: HistoryService_SessionTimeout_ClearsHistory
    /// Session should expire after timeout period and history should be cleared.
    /// </summary>
    [Fact]
    public async Task SessionTimeout_ClearsHistory()
    {
        // Arrange
        var phoneNumber = "+1234567890";

        // Create a service with a 2 minute timeout for testing
        var shortTimeoutConfig = new Dictionary<string, string>
        {
            { "History:MaxMessages", "30" },
            { "History:SessionTimeoutMinutes", "2" } // 2 minutes
        };
        var testConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(shortTimeoutConfig!)
            .Build();

        var testService = new TestableConversationHistoryService(
            _contextBuilderMock.Object,
            testConfig,
            _loggerMock.Object);

        // Add some messages
        await testService.AddMessageAsync(phoneNumber, ChatMessage.FromUser("Hola"));
        await testService.AddMessageAsync(phoneNumber, ChatMessage.FromAssistant("Bienvenido"));

        // Verify messages exist
        var historyBefore = await testService.GetHistoryAsync(phoneNumber);
        historyBefore.Should().HaveCount(2, "messages should be stored");

        // Act - Simulate time passing by directly manipulating last activity
        // Move the last activity timestamp back by 3 minutes (more than the 2 minute timeout)
        testService.SetLastActivityTimestamp(phoneNumber, DateTime.UtcNow.AddMinutes(-3));

        // Try to get history after simulated timeout
        var historyAfter = await testService.GetHistoryAsync(phoneNumber);

        // Assert
        historyAfter.Should().BeEmpty("session should have expired and history cleared");

        // Verify that debug log was called for session expiration
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Session expired")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log session expiration");
    }

    /// <summary>
    /// Testable subclass that exposes internal state for testing session timeout.
    /// </summary>
    private class TestableConversationHistoryService : ConversationHistoryService
    {
        private readonly Dictionary<string, DateTime> _lastActivity = new();

        public TestableConversationHistoryService(
            IContextBuilderService contextBuilder,
            IConfiguration configuration,
            ILogger<ConversationHistoryService> logger)
            : base(contextBuilder, configuration, logger)
        {
        }

        public void SetLastActivityTimestamp(string phoneNumber, DateTime timestamp)
        {
            // Access the base class's private field using reflection
            var baseType = typeof(ConversationHistoryService);
            var lastActivityField = baseType.GetField("_lastActivity",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (lastActivityField != null)
            {
                var lastActivityDict = lastActivityField.GetValue(this) as Dictionary<string, DateTime>;
                if (lastActivityDict != null)
                {
                    lastActivityDict[phoneNumber] = timestamp;
                }
            }
        }
    }

    /// <summary>
    /// Additional test: Verify multiple users have separate histories.
    /// </summary>
    [Fact]
    public async Task GetHistory_MultipleUsers_SeparateHistories()
    {
        // Arrange
        var user1 = "+1111111111";
        var user2 = "+2222222222";

        await _service.AddMessageAsync(user1, ChatMessage.FromUser("Message from user 1"));
        await _service.AddMessageAsync(user2, ChatMessage.FromUser("Message from user 2"));

        // Act
        var history1 = await _service.GetHistoryAsync(user1);
        var history2 = await _service.GetHistoryAsync(user2);

        // Assert
        history1.Should().HaveCount(1);
        history1[0].Content.Should().Be("Message from user 1");

        history2.Should().HaveCount(1);
        history2[0].Content.Should().Be("Message from user 2");
    }

    /// <summary>
    /// Step 56: HistoryService_ClearHistory_RemovesAll
    /// Verify ClearHistory removes all messages.
    /// </summary>
    [Fact]
    public async Task ClearHistory_RemovesAllMessages()
    {
        // Arrange
        var phoneNumber = "+1234567890";
        await _service.AddMessageAsync(phoneNumber, ChatMessage.FromUser("Message 1"));
        await _service.AddMessageAsync(phoneNumber, ChatMessage.FromAssistant("Response 1"));
        await _service.AddMessageAsync(phoneNumber, ChatMessage.FromUser("Message 2"));

        // Verify messages exist
        var historyBefore = await _service.GetHistoryAsync(phoneNumber);
        historyBefore.Should().HaveCount(3);

        // Act
        await _service.ClearHistoryAsync(phoneNumber);
        var historyAfter = await _service.GetHistoryAsync(phoneNumber);

        // Assert
        historyAfter.Should().BeEmpty("history should be cleared");
    }

    /// <summary>
    /// Step 57: HistoryService_ExtractState_ExtractsDate
    /// Date should be extracted from conversation history.
    /// </summary>
    [Fact]
    public void ExtractState_ExtractsDate_FromSaturdayWithDate()
    {
        // Arrange
        var history = new List<ChatMessage>
        {
            ChatMessage.FromUser("Hola"),
            ChatMessage.FromAssistant("Hola, ¿en qué puedo ayudarte?"),
            ChatMessage.FromUser("Quiero reservar para el sábado 30/11/2025"),
            ChatMessage.FromAssistant("Perfecto, ¿a qué hora?")
        };

        // Setup mock for GetUpcomingWeekends (not needed for explicit date)
        _contextBuilderMock
            .Setup(x => x.GetUpcomingWeekends(It.IsAny<int>()))
            .Returns(new List<WeekendDate>());

        // Act
        var state = _service.ExtractState(history);

        // Assert
        state.Should().NotBeNull();
        state.Fecha.Should().NotBeNullOrEmpty("date should be extracted");
        state.Fecha.Should().Contain("30");
        state.Fecha.Should().Contain("11");
        state.Fecha.Should().Contain("2025");
    }

    /// <summary>
    /// Step 57b: ExtractState_ExtractsDate - Test with day name only
    /// </summary>
    [Fact]
    public void ExtractState_ExtractsDate_FromDayName()
    {
        // Arrange
        var history = new List<ChatMessage>
        {
            ChatMessage.FromUser("Hola"),
            ChatMessage.FromAssistant("Hola, ¿en qué puedo ayudarte?"),
            ChatMessage.FromUser("Quiero reservar para el domingo"),
            ChatMessage.FromAssistant("Perfecto, ¿a qué hora?")
        };

        // Setup mock for GetUpcomingWeekends with a test Sunday
        var nextSunday = new WeekendDate
        {
            DayName = "domingo",
            Formatted = "01/12/2025",
            FullText = "Domingo, 1 de diciembre de 2025",
            Date = new DateTime(2025, 12, 1)
        };
        _contextBuilderMock
            .Setup(x => x.GetUpcomingWeekends(It.IsAny<int>()))
            .Returns(new List<WeekendDate> { nextSunday });

        // Act
        var state = _service.ExtractState(history);

        // Assert
        state.Should().NotBeNull();
        state.Fecha.Should().NotBeNullOrEmpty("date should be extracted from day name");
        state.Fecha.Should().Be("01/12/2025");
    }

    /// <summary>
    /// Step 58: HistoryService_ExtractState_ExtractsTime
    /// Time should be extracted from conversation history.
    /// </summary>
    [Fact]
    public void ExtractState_ExtractsTime_FromHourMinute()
    {
        // Arrange
        var history = new List<ChatMessage>
        {
            ChatMessage.FromUser("Hola"),
            ChatMessage.FromAssistant("¿A qué hora queréis venir?"),
            ChatMessage.FromUser("A las 14:00"),
            ChatMessage.FromAssistant("Perfecto")
        };

        // Act
        var state = _service.ExtractState(history);

        // Assert
        state.Should().NotBeNull();
        state.Hora.Should().NotBeNullOrEmpty("time should be extracted");
        state.Hora.Should().Contain("14");
        state.Hora.Should().Contain("00");
    }

    /// <summary>
    /// Step 58b: ExtractState_ExtractsTime - Test with hour only
    /// </summary>
    [Fact]
    public void ExtractState_ExtractsTime_FromHourOnly()
    {
        // Arrange
        var history = new List<ChatMessage>
        {
            ChatMessage.FromUser("Hola"),
            ChatMessage.FromAssistant("¿A qué hora queréis venir?"),
            ChatMessage.FromUser("Para las 2"),
            ChatMessage.FromAssistant("¿2 de la tarde o de la noche?")
        };

        // Act
        var state = _service.ExtractState(history);

        // Assert
        state.Should().NotBeNull();
        state.Hora.Should().NotBeNullOrEmpty("time should be extracted");
        state.Hora.Should().Contain("2");
    }

    /// <summary>
    /// Step 59: HistoryService_ExtractState_ExtractsPeople
    /// People count should be extracted from conversation history.
    /// </summary>
    [Fact]
    public void ExtractState_ExtractsPeople_FromPersonasPattern()
    {
        // Arrange
        var history = new List<ChatMessage>
        {
            ChatMessage.FromUser("Hola"),
            ChatMessage.FromAssistant("¿Para cuántas personas?"),
            ChatMessage.FromUser("Para 4 personas"),
            ChatMessage.FromAssistant("Perfecto, 4 personas")
        };

        // Act
        var state = _service.ExtractState(history);

        // Assert
        state.Should().NotBeNull();
        state.Personas.Should().NotBeNull("people count should be extracted");
        state.Personas.Should().Be(4);
    }

    /// <summary>
    /// Step 59b: ExtractState_ExtractsPeople - Test with "somos" pattern
    /// </summary>
    [Fact]
    public void ExtractState_ExtractsPeople_FromSomosPattern()
    {
        // Arrange
        var history = new List<ChatMessage>
        {
            ChatMessage.FromUser("Hola"),
            ChatMessage.FromAssistant("¿Para cuántas personas?"),
            ChatMessage.FromUser("Somos 6"),
            ChatMessage.FromAssistant("Perfecto, 6 personas")
        };

        // Act
        var state = _service.ExtractState(history);

        // Assert
        state.Should().NotBeNull();
        state.Personas.Should().NotBeNull("people count should be extracted");
        state.Personas.Should().Be(6);
    }

    /// <summary>
    /// Step 60: HistoryService_ExtractState_ExtractsRiceType
    /// Rice type should be extracted from validation result in history.
    /// </summary>
    [Fact]
    public void ExtractState_ExtractsRiceType_FromValidationResult()
    {
        // Arrange
        var history = new List<ChatMessage>
        {
            ChatMessage.FromUser("Hola"),
            ChatMessage.FromAssistant("¿Queréis arroz?"),
            ChatMessage.FromUser("Sí, una paella valenciana"),
            ChatMessage.FromAssistant("✅ Paella valenciana disponible. ¿Cuántas raciones?")
        };

        // Act
        var state = _service.ExtractState(history);

        // Assert
        state.Should().NotBeNull();
        state.ArrozType.Should().NotBeNullOrEmpty("rice type should be extracted");
        state.ArrozType.Should().Be("Paella valenciana");
    }

    /// <summary>
    /// Step 60b: ExtractState_ExtractsRiceType - Test with "no rice" response
    /// </summary>
    [Fact]
    public void ExtractState_ExtractsRiceType_NoRiceResponse()
    {
        // Arrange
        var history = new List<ChatMessage>
        {
            ChatMessage.FromUser("Hola"),
            ChatMessage.FromAssistant("¿Queréis arroz?"),
            ChatMessage.FromUser("No, gracias"),
            ChatMessage.FromAssistant("Perfecto, sin arroz entonces")
        };

        // Act
        var state = _service.ExtractState(history);

        // Assert
        state.Should().NotBeNull();
        state.ArrozType.Should().NotBeNull("rice decision should be captured");
        state.ArrozType.Should().Be("", "empty string means no rice");
    }

    /// <summary>
    /// Step 60c: ExtractState_ExtractsRiceType - Test with servings
    /// </summary>
    [Fact]
    public void ExtractState_ExtractsRiceServings_FromHistory()
    {
        // Arrange
        var history = new List<ChatMessage>
        {
            ChatMessage.FromUser("Hola"),
            ChatMessage.FromAssistant("¿Queréis arroz?"),
            ChatMessage.FromUser("Sí, arroz negro"),
            ChatMessage.FromAssistant("✅ Arroz negro disponible. ¿Cuántas raciones?"),
            ChatMessage.FromUser("3 raciones"),
            ChatMessage.FromAssistant("Perfecto, 3 raciones de arroz negro")
        };

        // Act
        var state = _service.ExtractState(history);

        // Assert
        state.Should().NotBeNull();
        state.ArrozType.Should().Be("Arroz negro");
        state.ArrozServings.Should().NotBeNull("rice servings should be extracted");
        state.ArrozServings.Should().Be(3);
    }

    /// <summary>
    /// Step 61: HistoryService_ExtractState_DetectsNoRice
    /// "no rice" response sets empty string for ArrozType.
    /// </summary>
    [Fact]
    public void ExtractState_DetectsNoRice_SetsEmptyString()
    {
        // Arrange
        var history = new List<ChatMessage>
        {
            ChatMessage.FromUser("Hola"),
            ChatMessage.FromAssistant("¿Queréis arroz?"),
            ChatMessage.FromUser("No, sin arroz por favor"),
            ChatMessage.FromAssistant("Perfecto, sin arroz entonces")
        };

        // Act
        var state = _service.ExtractState(history);

        // Assert
        state.Should().NotBeNull();
        state.ArrozType.Should().NotBeNull("rice decision should be captured");
        state.ArrozType.Should().Be("", "empty string means no rice was selected");
    }

    /// <summary>
    /// Step 62: HistoryService_ExtractState_IdentifiesMissingData
    /// Missing fields should be correctly listed in MissingData property.
    /// </summary>
    [Fact]
    public void ExtractState_IdentifiesMissingData_ListsFields()
    {
        // Arrange - Only date is provided
        var history = new List<ChatMessage>
        {
            ChatMessage.FromUser("Hola"),
            ChatMessage.FromAssistant("¿Para cuándo queréis reservar?"),
            ChatMessage.FromUser("Para el sábado 30/11/2025"),
            ChatMessage.FromAssistant("Perfecto, ¿a qué hora?")
        };

        // Setup mock for GetUpcomingWeekends
        _contextBuilderMock
            .Setup(x => x.GetUpcomingWeekends(It.IsAny<int>()))
            .Returns(new List<WeekendDate>());

        // Act
        var state = _service.ExtractState(history);

        // Assert
        state.Should().NotBeNull();
        state.MissingData.Should().NotBeNull();
        state.MissingData.Should().Contain("hora", "time is missing");
        state.MissingData.Should().Contain("personas", "people count is missing");
        state.MissingData.Should().Contain("arroz_decision", "rice decision is missing");
        state.MissingData.Should().NotContain("fecha", "date should not be missing");
    }

    /// <summary>
    /// Step 63: HistoryService_ExtractState_SetsStage
    /// Stage should be set based on data completeness.
    /// </summary>
    [Fact]
    public void ExtractState_SetsStage_BasedOnCompleteness()
    {
        // Arrange - Complete data
        var completeHistory = new List<ChatMessage>
        {
            ChatMessage.FromUser("Hola"),
            ChatMessage.FromAssistant("¿Para cuándo?"),
            ChatMessage.FromUser("Para el sábado 30/11/2025"),
            ChatMessage.FromAssistant("¿A qué hora?"),
            ChatMessage.FromUser("A las 14:00"),
            ChatMessage.FromAssistant("¿Para cuántas personas?"),
            ChatMessage.FromUser("Para 4 personas"),
            ChatMessage.FromAssistant("¿Queréis arroz?"),
            ChatMessage.FromUser("Sí, paella valenciana"),
            ChatMessage.FromAssistant("✅ Paella valenciana disponible. ¿Cuántas raciones?"),
            ChatMessage.FromUser("4 raciones"),
            ChatMessage.FromAssistant("Perfecto")
        };

        // Setup mock for GetUpcomingWeekends
        _contextBuilderMock
            .Setup(x => x.GetUpcomingWeekends(It.IsAny<int>()))
            .Returns(new List<WeekendDate>());

        // Act - Complete data
        var completeState = _service.ExtractState(completeHistory);

        // Assert - Stage should be "awaiting_confirmation" when complete
        completeState.Should().NotBeNull();
        completeState.IsComplete.Should().BeTrue("all required data is present");
        completeState.Stage.Should().Be("awaiting_confirmation", "complete data should set stage to awaiting_confirmation");

        // Arrange - Incomplete data
        var incompleteHistory = new List<ChatMessage>
        {
            ChatMessage.FromUser("Hola"),
            ChatMessage.FromAssistant("¿Para cuándo?"),
            ChatMessage.FromUser("Para el sábado"),
            ChatMessage.FromAssistant("¿A qué hora?")
        };

        // Act - Incomplete data
        var incompleteState = _service.ExtractState(incompleteHistory);

        // Assert - Stage should be "collecting_info" when incomplete
        incompleteState.Should().NotBeNull();
        incompleteState.IsComplete.Should().BeFalse("data is incomplete");
        incompleteState.Stage.Should().Be("collecting_info", "incomplete data should set stage to collecting_info");
    }
}
