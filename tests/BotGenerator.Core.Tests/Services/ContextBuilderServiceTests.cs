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
}
