using System.Text.Json;
using BotGenerator.Api.Controllers;
using BotGenerator.Core.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BotGenerator.Core.Tests.Controllers;

public class EvolutionWebhookControllerTests
{
    [Fact]
    public async Task HandleEvolutionWebhook_WhenProviderIsNotEvolution_ReturnsNotFound()
    {
        using var document = JsonDocument.Parse(ValidWebhookJson);
        var controller = CreateController(new Dictionary<string, string?>
        {
            ["WhatsApp:Provider"] = "uazapi"
        });

        var result = await controller.HandleEvolutionWebhook("expected-secret", document.RootElement, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task HandleEvolutionWebhook_WithInvalidSecret_ReturnsUnauthorizedBeforeDedupe()
    {
        using var document = JsonDocument.Parse(ValidWebhookJson);
        var dedupe = new Mock<IEvolutionWebhookDedupe>();
        var controller = CreateController(new Dictionary<string, string?>
        {
            ["WhatsApp:Provider"] = "evolution",
            ["WhatsApp:Evolution:WebhookSecret"] = "expected-secret",
            ["WhatsApp:Evolution:InstanceName"] = "villa-carmen"
        }, dedupe.Object);

        var result = await controller.HandleEvolutionWebhook("wrong-secret", document.RootElement, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
        dedupe.Verify(
            service => service.TryClaimAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleEvolutionWebhook_DuplicateMessage_DoesNotRunAgentTwice()
    {
        using var document = JsonDocument.Parse(ValidWebhookJson);
        var dedupe = new Mock<IEvolutionWebhookDedupe>();
        dedupe.Setup(service => service.TryClaimAsync("villa-carmen", "message-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvolutionWebhookDedupeClaim(EvolutionWebhookDedupeState.Completed));
        var controller = CreateController(new Dictionary<string, string?>
        {
            ["WhatsApp:Provider"] = "evolution",
            ["WhatsApp:Evolution:WebhookSecret"] = "expected-secret",
            ["WhatsApp:Evolution:InstanceName"] = "villa-carmen"
        }, dedupe.Object);

        var result = await controller.HandleEvolutionWebhook("expected-secret", document.RootElement, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value!.GetType().GetProperty("duplicate")!.GetValue(ok.Value).Should().Be(true);
        dedupe.Verify(
            service => service.TryClaimAsync("villa-carmen", "message-123", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleEvolutionWebhook_DottedMessageEvent_ReachesDurableDedupe()
    {
        using var document = JsonDocument.Parse(ValidWebhookJson.Replace("MESSAGES_UPSERT", "messages.upsert"));
        var dedupe = new Mock<IEvolutionWebhookDedupe>();
        dedupe.Setup(service => service.TryClaimAsync("villa-carmen", "message-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvolutionWebhookDedupeClaim(EvolutionWebhookDedupeState.Completed));
        var controller = CreateController(EvolutionConfiguration(), dedupe.Object);

        var result = await controller.HandleEvolutionWebhook("expected-secret", document.RootElement, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        dedupe.Verify(
            service => service.TryClaimAsync("villa-carmen", "message-123", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("buttonsResponseMessage", "{\"selectedButtonId\":\"confirm\",\"selectedDisplayText\":\"Confirmar\"}", "reply-1")]
    [InlineData("listResponseMessage", "{\"title\":\"Paella\",\"singleSelectReply\":{\"selectedRowId\":\"paella\"}}", "list-1")]
    [InlineData("interactiveResponseMessage", "{\"nativeFlowResponseMessage\":{\"paramsJson\":\"{\\\"id\\\":\\\"cancel\\\",\\\"title\\\":\\\"Cancelar\\\"}\"}}", "flow-1")]
    public async Task HandleEvolutionWebhook_InteractiveForms_ReachDurableDedupe(
        string messageType,
        string messageContent,
        string messageId)
    {
        using var document = JsonDocument.Parse($$"""
            {
              "instance":"villa-carmen",
              "event":"MESSAGES_UPSERT",
              "data":{
                "key":{"remoteJid":"34638857294@s.whatsapp.net","id":"{{messageId}}","fromMe":false},
                "message":{"{{messageType}}":{{messageContent}}}
              }
            }
            """);
        var dedupe = new Mock<IEvolutionWebhookDedupe>();
        dedupe.Setup(service => service.TryClaimAsync("villa-carmen", messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvolutionWebhookDedupeClaim(EvolutionWebhookDedupeState.Completed));
        var controller = CreateController(EvolutionConfiguration(), dedupe.Object);

        var result = await controller.HandleEvolutionWebhook("expected-secret", document.RootElement, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        dedupe.Verify(
            service => service.TryClaimAsync("villa-carmen", messageId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("{\"from\":\"34692747052@s.whatsapp.net\",\"id\":\"call-1\",\"status\":\"offer\"}")]
    [InlineData("{\"call\":{\"remoteJid\":\"34692747052@s.whatsapp.net\",\"callId\":\"call-2\"}}")]
    [InlineData("[{\"chatId\":\"34692747052@c.us\",\"id\":\"call-3\"}]")]
    public async Task HandleEvolutionWebhook_CallWithValidCaller_SendsCooldownReplyWithoutRejecting(string dataJson)
    {
        using var document = JsonDocument.Parse($$"""
            {"instance":"villa-carmen","event":"CALL","data":{{dataJson}}}
            """);
        var whatsApp = new Mock<IWhatsAppService>();
        whatsApp.Setup(service => service.SendTextAsync(
                "34692747052", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        whatsApp.Setup(service => service.SendContactCardAsync(
                "34692747052", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var callStore = new Mock<ICallAutoReplyStore>();
        callStore.Setup(store => store.TryMarkReplied(
                "34692747052", It.IsAny<TimeSpan>(), It.IsAny<DateTime>()))
            .Returns(true);
        var controller = CreateController(EvolutionConfiguration(), whatsApp: whatsApp.Object, callStore: callStore.Object);

        var result = await controller.HandleEvolutionWebhook("expected-secret", document.RootElement, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value!.GetType().GetProperty("replied")!.GetValue(ok.Value).Should().Be(true);
        whatsApp.Verify(service => service.RejectCallAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        whatsApp.Verify(service => service.SendTextAsync(
            "34692747052", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        whatsApp.Verify(service => service.SendContactCardAsync(
            "34692747052", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleEvolutionWebhook_CallWithoutPhone_DoesNotSendOrClaimRejection()
    {
        using var document = JsonDocument.Parse("""
            {"instance":"villa-carmen","event":"CALL","data":{"from":"caller@lid","id":"call-1"}}
            """);
        var whatsApp = new Mock<IWhatsAppService>();
        var controller = CreateController(EvolutionConfiguration(), whatsApp: whatsApp.Object);

        var result = await controller.HandleEvolutionWebhook("expected-secret", document.RootElement, CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        whatsApp.VerifyNoOtherCalls();
    }

    private static WebhookController CreateController(
        Dictionary<string, string?> values,
        IEvolutionWebhookDedupe? dedupe = null,
        IWhatsAppService? whatsApp = null,
        ICallAutoReplyStore? callStore = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new WebhookController(
            null!,
            Mock.Of<IBookingRepository>(),
            Mock.Of<IPendingBookingStore>(),
            callStore ?? Mock.Of<ICallAutoReplyStore>(),
            whatsApp ?? Mock.Of<IWhatsAppService>(),
            configuration,
            Mock.Of<IHostEnvironment>(),
            NullLogger<WebhookController>.Instance,
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IRestaurantConfigRepository>(),
            dedupe ?? Mock.Of<IEvolutionWebhookDedupe>());
    }

    private static Dictionary<string, string?> EvolutionConfiguration() => new()
    {
        ["WhatsApp:Provider"] = "evolution",
        ["WhatsApp:Evolution:WebhookSecret"] = "expected-secret",
        ["WhatsApp:Evolution:InstanceName"] = "villa-carmen",
        ["WhatsApp:CallAutoReplyText"] = "No atendemos llamadas"
    };

    private const string ValidWebhookJson = """
        {
          "instance":"villa-carmen",
          "event":"MESSAGES_UPSERT",
          "data":{
            "key":{"remoteJid":"34638857294@s.whatsapp.net","id":"message-123","fromMe":false},
            "pushName":"María",
            "message":{"conversation":"Hola"}
          }
        }
        """;
}
