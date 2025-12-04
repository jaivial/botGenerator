using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace BotGenerator.Core.Tests.Services;

public class ContextBuilderServiceTests
{
    private readonly ContextBuilderService _service;

    public ContextBuilderServiceTests()
    {
        _service = new ContextBuilderService(Mock.Of<ILogger<ContextBuilderService>>());
    }

    [Fact]
    public void BuildContext_IncludesCustomerInfo()
    {
        var message = new WhatsAppMessage
        {
            PushName = "Juan García",
            SenderNumber = "34612345678",
            MessageText = "Hola"
        };

        var context = _service.BuildContext(message, null, null);

        Assert.Equal("Juan García", context["pushName"]);
        Assert.Equal("34612345678", context["senderNumber"]);
        Assert.Equal("Hola", context["messageText"]);
    }

    [Fact]
    public void BuildContext_IncludesDateInfo()
    {
        var message = new WhatsAppMessage { PushName = "Test" };
        var context = _service.BuildContext(message, null, null);

        Assert.Equal(DateTime.Now.Year, context["currentYear"]);
        Assert.NotNull(context["todayES"]);
        Assert.NotNull(context["todayFormatted"]);
    }

    [Fact]
    public void BuildContext_IncludesBookingState()
    {
        var message = new WhatsAppMessage { PushName = "Test" };
        var state = new ConversationState
        {
            Fecha = "29/11/2025",
            Hora = "14:00",
            Personas = 4
        };

        var context = _service.BuildContext(message, state, null);

        Assert.Equal("29/11/2025", context["state_fecha"]);
        Assert.Equal("14:00", context["state_hora"]);
        Assert.Equal(4, context["state_personas"]);
    }

    [Fact]
    public void FormatHistory_ReturnsFirstContact_WhenEmpty()
    {
        var result = _service.FormatHistory(null);
        Assert.Contains("Primer contacto", result);
    }

    [Fact]
    public void FormatHistory_FormatsMessages()
    {
        var history = new List<ChatMessage>
        {
            ChatMessage.FromUser("Hola", "Juan"),
            ChatMessage.FromAssistant("¡Hola!")
        };

        var result = _service.FormatHistory(history);

        Assert.Contains("👤", result);
        Assert.Contains("🤖", result);
        Assert.Contains("Hola", result);
    }

    [Fact]
    public void GetUpcomingWeekends_ReturnsFourWeekends()
    {
        var weekends = _service.GetUpcomingWeekends();

        Assert.Equal(4, weekends.Count);
        Assert.All(weekends, w =>
            Assert.True(w.IsSaturday || w.IsSunday));
    }

    [Fact]
    public void ExtendContext_MergesValues()
    {
        var baseContext = new Dictionary<string, object>
        {
            ["key1"] = "value1"
        };

        var extended = _service.ExtendContext(baseContext,
            new Dictionary<string, object>
            {
                ["key2"] = "value2"
            });

        Assert.Equal("value1", extended["key1"]);
        Assert.Equal("value2", extended["key2"]);
    }

    /// <summary>
    /// Step 64: ContextBuilder_BuildsContext_IncludesToday
    /// Today's date should be included in the context.
    /// </summary>
    [Fact]
    public void BuildContext_IncludesToday_DateInformation()
    {
        // Arrange
        var message = new WhatsAppMessage
        {
            PushName = "Test User",
            SenderNumber = "123456789",
            MessageText = "Hola"
        };

        // Act
        var context = _service.BuildContext(message, null, null);

        // Assert
        Assert.NotNull(context);

        // Check today's date is included in multiple formats
        Assert.True(context.ContainsKey("todayES"), "todayES should be present");
        Assert.True(context.ContainsKey("todayFormatted"), "todayFormatted should be present");
        Assert.True(context.ContainsKey("todayISO"), "todayISO should be present");
        Assert.True(context.ContainsKey("todayDayName"), "todayDayName should be present");
        Assert.True(context.ContainsKey("todayMonthName"), "todayMonthName should be present");

        // Verify the values are not empty
        Assert.NotNull(context["todayES"]);
        Assert.NotNull(context["todayFormatted"]);
        Assert.NotNull(context["todayISO"]);
        Assert.NotEmpty(context["todayES"].ToString()!);
        Assert.NotEmpty(context["todayFormatted"].ToString()!);
        Assert.NotEmpty(context["todayISO"].ToString()!);

        // Verify current year, month, day
        Assert.Equal(DateTime.Now.Year, context["currentYear"]);
        Assert.Equal(DateTime.Now.Month, context["currentMonth"]);
        Assert.Equal(DateTime.Now.Day, context["currentDay"]);

        // Verify todayFormatted matches expected format (dd/MM/yyyy)
        var todayFormatted = context["todayFormatted"].ToString();
        Assert.Matches(@"\d{2}/\d{2}/\d{4}", todayFormatted!);
    }

    /// <summary>
    /// Step 65: ContextBuilder_BuildsContext_IncludesWeekends
    /// Upcoming weekends should be calculated and included in context.
    /// </summary>
    [Fact]
    public void BuildContext_IncludesWeekends_Calculated()
    {
        // Arrange
        var message = new WhatsAppMessage
        {
            PushName = "Test User",
            SenderNumber = "123456789",
            MessageText = "Hola"
        };

        // Act
        var context = _service.BuildContext(message, null, null);

        // Assert
        Assert.NotNull(context);

        // Check upcoming weekends are included
        Assert.True(context.ContainsKey("upcomingWeekends"), "upcomingWeekends should be present");
        Assert.True(context.ContainsKey("nextSaturday"), "nextSaturday should be present");
        Assert.True(context.ContainsKey("nextSunday"), "nextSunday should be present");
        Assert.True(context.ContainsKey("nextSaturdayFull"), "nextSaturdayFull should be present");
        Assert.True(context.ContainsKey("nextSundayFull"), "nextSundayFull should be present");

        // Verify upcomingWeekends is a non-empty string
        var upcomingWeekends = context["upcomingWeekends"].ToString();
        Assert.NotNull(upcomingWeekends);
        Assert.NotEmpty(upcomingWeekends);

        // Verify nextSaturday and nextSunday are in correct format (dd/MM/yyyy)
        var nextSaturday = context["nextSaturday"].ToString();
        var nextSunday = context["nextSunday"].ToString();
        Assert.Matches(@"\d{2}/\d{2}/\d{4}", nextSaturday!);
        Assert.Matches(@"\d{2}/\d{2}/\d{4}", nextSunday!);

        // Verify GetUpcomingWeekends returns 4 weekend dates
        var weekendList = _service.GetUpcomingWeekends();
        Assert.Equal(4, weekendList.Count);

        // Verify all returned dates are Saturdays or Sundays
        Assert.All(weekendList, w =>
        {
            Assert.True(w.Date.DayOfWeek == DayOfWeek.Saturday ||
                       w.Date.DayOfWeek == DayOfWeek.Sunday,
                       $"Date {w.Formatted} should be a Saturday or Sunday");
        });

        // Verify dates are in the future
        var today = DateTime.Now.Date;
        Assert.All(weekendList, w =>
        {
            Assert.True(w.Date > today,
                       $"Date {w.Formatted} should be in the future");
        });

        // Verify dates contain proper Spanish formatting
        Assert.All(weekendList, w =>
        {
            Assert.NotEmpty(w.DayName);
            Assert.NotEmpty(w.Formatted);
            Assert.NotEmpty(w.FullText);
            Assert.True(w.DayName == "sábado" || w.DayName == "domingo",
                       $"Day name should be 'sábado' or 'domingo', got '{w.DayName}'");
        });
    }

    /// <summary>
    /// Step 66: ContextBuilder_BuildsContext_IncludesState
    /// Conversation state should be properly included in the context dictionary.
    /// </summary>
    [Fact]
    public void ContextBuilder_BuildsContext_IncludesState()
    {
        // Arrange
        var message = new WhatsAppMessage
        {
            PushName = "Test User",
            SenderNumber = "123456789",
            MessageText = "Confirmo la reserva"
        };

        var state = new ConversationState
        {
            Fecha = "30/11/2025",
            FechaFullText = "Sábado, 30 de noviembre de 2025",
            Hora = "15:00",
            Personas = 6,
            ArrozType = "Paella Valenciana",
            ArrozServings = 2,
            Stage = "confirming",
            MissingData = new List<string>()
        };

        // Act
        var context = _service.BuildContext(message, state, null);

        // Assert
        Assert.NotNull(context);

        // Verify all state fields are included in context
        Assert.True(context.ContainsKey("state_fecha"), "state_fecha should be present");
        Assert.True(context.ContainsKey("state_fecha_fullText"), "state_fecha_fullText should be present");
        Assert.True(context.ContainsKey("state_hora"), "state_hora should be present");
        Assert.True(context.ContainsKey("state_personas"), "state_personas should be present");
        Assert.True(context.ContainsKey("state_arroz"), "state_arroz should be present");
        Assert.True(context.ContainsKey("state_raciones"), "state_raciones should be present");
        Assert.True(context.ContainsKey("state_isComplete"), "state_isComplete should be present");
        Assert.True(context.ContainsKey("state_stage"), "state_stage should be present");
        Assert.True(context.ContainsKey("state_missingData"), "state_missingData should be present");

        // Verify the values match the state
        Assert.Equal("30/11/2025", context["state_fecha"]);
        Assert.Equal("Sábado, 30 de noviembre de 2025", context["state_fecha_fullText"]);
        Assert.Equal("15:00", context["state_hora"]);
        Assert.Equal(6, context["state_personas"]);
        Assert.Equal("Paella Valenciana", context["state_arroz"]);
        Assert.Equal(2, context["state_raciones"]);
        Assert.Equal("confirming", context["state_stage"]);
        Assert.Equal("", context["state_missingData"]); // Empty list = empty string

        // Test with null state - should include empty defaults
        var contextWithoutState = _service.BuildContext(message, null, null);
        Assert.Equal("", contextWithoutState["state_fecha"]);
        Assert.Equal("", contextWithoutState["state_hora"]);
        Assert.Equal(0, contextWithoutState["state_personas"]);
        Assert.False((bool)contextWithoutState["state_isComplete"]);
        Assert.Equal("collecting_info", contextWithoutState["state_stage"]);

        // Test with missing data
        var stateWithMissing = new ConversationState
        {
            Fecha = "01/12/2025",
            Hora = "",
            Personas = 4,
            Stage = "collecting_info",
            MissingData = new List<string> { "hora", "nombre", "telefono" }
        };

        var contextWithMissing = _service.BuildContext(message, stateWithMissing, null);
        Assert.Equal("hora, nombre, telefono", contextWithMissing["state_missingData"]);
    }
}
