using System.Net;
using System.Text;
using System.Text.Json;
using BotGenerator.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BotGenerator.Core.Tests.Services;

public class EvolutionWhatsAppServiceTests
{
    [Fact]
    public async Task SendTextAsync_AcceptedResponse_SendsRc2Request()
    {
        var handler = SuccessHandler();
        var accepted = await CreateService(handler).SendTextAsync("638 857 294", "Hola reserva");

        accepted.Should().BeTrue();
        var payload = AssertRequest(handler, "/message/sendText/villa-carmen");
        payload.GetProperty("number").GetString().Should().Be("34638857294");
        payload.GetProperty("text").GetString().Should().Be("Hola reserva");
    }

    [Theory]
    [InlineData("638857294", "34638857294")]
    [InlineData("+1 (415) 555-2671", "14155552671")]
    [InlineData("4915123456789", "4915123456789")]
    public async Task SendTextAsync_NumberNormalization_PreservesInternationalE164(
        string input,
        string expected)
    {
        var handler = SuccessHandler();

        (await CreateService(handler).SendTextAsync(input, "test")).Should().BeTrue();

        AssertRequest(handler, "/message/sendText/villa-carmen")
            .GetProperty("number").GetString().Should().Be(expected);
    }

    [Fact]
    public async Task SendButtonsAsync_AcceptedResponse_SendsReplyOnlyButtons()
    {
        var handler = SuccessHandler();
        var accepted = await CreateService(handler).SendButtonsAsync(
            "638857294",
            "Confirma",
            "Pie",
            [new("yes", "Sí"), new("no", "No")]);

        accepted.Should().BeTrue();
        var payload = AssertRequest(handler, "/message/sendButtons/villa-carmen");
        payload.GetProperty("number").GetString().Should().Be("34638857294");
        payload.GetProperty("title").GetString().Should().Be("Confirma");
        payload.GetProperty("footer").GetString().Should().Be("Pie");
        payload.GetProperty("buttons").GetArrayLength().Should().Be(2);
        var button = payload.GetProperty("buttons")[0];
        button.GetProperty("type").GetString().Should().Be("reply");
        button.GetProperty("displayText").GetString().Should().Be("Sí");
        button.GetProperty("id").GetString().Should().Be("yes");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task SendButtonsAsync_OutsideRc2ReplyLimit_DoesNotSend(int count)
    {
        var handler = SuccessHandler();
        var buttons = Enumerable.Range(1, count).Select(index => new ButtonOption($"id-{index}", $"Button {index}")).ToList();

        (await CreateService(handler).SendButtonsAsync("638857294", "Text", "Footer", buttons)).Should().BeFalse();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task SendLinkButtonsAsync_AcceptedResponse_SendsUrlOnlyButtons()
    {
        var handler = SuccessHandler();
        var accepted = await CreateService(handler).SendLinkButtonsAsync(
            "638857294",
            "Accesos",
            [new("WEB", "https://example.test"), new("MAPA", "http://example.test/map")]);

        accepted.Should().BeTrue();
        var payload = AssertRequest(handler, "/message/sendButtons/villa-carmen");
        payload.GetProperty("buttons").GetArrayLength().Should().Be(2);
        var button = payload.GetProperty("buttons")[0];
        button.GetProperty("type").GetString().Should().Be("url");
        button.GetProperty("displayText").GetString().Should().Be("WEB");
        button.GetProperty("url").GetString().Should().Be("https://example.test");
    }

    [Theory]
    [InlineData("ftp://example.test")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/relative")]
    public async Task SendLinkButtonsAsync_NonHttpUrl_DoesNotSend(string url)
    {
        var handler = SuccessHandler();

        (await CreateService(handler).SendLinkButtonsAsync("638857294", "Link", [new("OPEN", url)]))
            .Should().BeFalse();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task SendLinkButtonsAsync_AboveRc2CtaLimit_DoesNotSend()
    {
        var handler = SuccessHandler();
        var buttons = Enumerable.Range(1, 3)
            .Select(index => new LinkButtonOption($"Link {index}", $"https://example.test/{index}"))
            .ToList();

        (await CreateService(handler).SendLinkButtonsAsync("638857294", "Links", buttons)).Should().BeFalse();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task SendMenuAsync_AcceptedResponse_SendsRc2List()
    {
        var handler = SuccessHandler();
        var accepted = await CreateService(handler).SendMenuAsync(
            "638857294",
            "Arroces",
            "Elegir",
            [new("Menú", [new("paella", "Paella", "Para dos")])]);

        accepted.Should().BeTrue();
        var payload = AssertRequest(handler, "/message/sendList/villa-carmen");
        payload.GetProperty("title").GetString().Should().Be("Arroces");
        payload.GetProperty("buttonText").GetString().Should().Be("Elegir");
        var row = payload.GetProperty("sections")[0].GetProperty("rows")[0];
        row.GetProperty("rowId").GetString().Should().Be("paella");
        row.GetProperty("description").GetString().Should().Be("Para dos");
    }

    [Fact]
    public async Task GetHistoryPageAsync_Success_SendsQueryAndUsesRawPagination()
    {
        var handler = new RecordingHandler(_ => JsonResponse(HttpStatusCode.OK, HistoryPage(1, 2,
            """{"key":{"id":"text-1","fromMe":false},"pushName":"Ana","messageTimestamp":20,"message":{"conversation":"Hola"}}""",
            """{"key":{"id":"media-1","fromMe":false},"messageTimestamp":21,"message":{"imageMessage":{"url":"x"}}}""")));

        var page = await CreateService(handler).GetHistoryPageAsync("+1 415 555 2671", 2, 0);

        page.Messages.Should().ContainSingle().Which.MessageId.Should().Be("text-1");
        page.HasMore.Should().BeTrue();
        page.NextOffset.Should().Be(2);
        var payload = AssertRequest(handler, "/chat/findMessages/villa-carmen");
        payload.GetProperty("where").GetProperty("key").GetProperty("remoteJid").GetString()
            .Should().Be("14155552671@s.whatsapp.net");
        payload.GetProperty("page").GetInt32().Should().Be(1);
        payload.GetProperty("offset").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task GetHistoryAsync_Success_ReturnsFirstPageText()
    {
        var handler = new RecordingHandler(_ => JsonResponse(HttpStatusCode.OK, HistoryPage(1, 1,
            """{"key":{"id":"text-1","fromMe":true},"messageTimestamp":20,"message":{"extendedTextMessage":{"text":"Respuesta"}}}""")));

        var messages = await CreateService(handler).GetHistoryAsync("638857294", 20);

        messages.Should().ContainSingle().Which.Should().BeEquivalentTo(new WhatsAppHistoryMessage
        {
            Text = "Respuesta",
            FromMe = true,
            Timestamp = 20,
            MessageId = "text-1"
        });
        AssertRequest(handler, "/chat/findMessages/villa-carmen")
            .GetProperty("where").GetProperty("key").GetProperty("remoteJid").GetString()
            .Should().Be("34638857294@s.whatsapp.net");
    }

    [Fact]
    public async Task GetFullHistoryAsync_MediaOnlyIntermediatePage_ContinuesAndReturnsChronologicalUniqueText()
    {
        var responses = new Queue<string>(
        [
            HistoryPage(1, 3, """{"key":{"id":"new","fromMe":false},"messageTimestamp":30,"message":{"conversation":"Nuevo"}}"""),
            HistoryPage(2, 3, """{"key":{"id":"media","fromMe":false},"messageTimestamp":20,"message":{"imageMessage":{"url":"x"}}}"""),
            HistoryPage(3, 3,
                """{"key":{"id":"old","fromMe":false},"messageTimestamp":10,"message":{"conversation":"Antiguo"}}""",
                """{"key":{"id":"new","fromMe":false},"messageTimestamp":30,"message":{"conversation":"Duplicado"}}""")
        ]);
        var handler = new RecordingHandler(_ => JsonResponse(HttpStatusCode.OK, responses.Dequeue()));

        var messages = await CreateService(handler).GetFullHistoryAsync("638857294", pageSize: 1, maxPages: 10);

        handler.Requests.Should().HaveCount(3);
        handler.Requests.Should().OnlyContain(request =>
            request.Path == "/chat/findMessages/villa-carmen" &&
            request.ApiKey == "test-api-key" &&
            request.Origin == "https://evolution.test");
        handler.Requests.Select(request => JsonDocument.Parse(request.Body).RootElement.GetProperty("page").GetInt32())
            .Should().Equal(1, 2, 3);
        messages.Select(message => message.MessageId).Should().Equal("old", "new");
        messages.Select(message => message.Text).Should().Equal("Antiguo", "Duplicado");
    }

    [Fact]
    public async Task SendContactCardAsync_AcceptedResponse_SendsRc2Contact()
    {
        var handler = SuccessHandler();
        var accepted = await CreateService(handler).SendContactCardAsync(
            "638857294", "Reservas", "+1 415 555 2671", "Villa Carmen", "test@example.com");

        accepted.Should().BeTrue();
        var contact = AssertRequest(handler, "/message/sendContact/villa-carmen").GetProperty("contact")[0];
        contact.GetProperty("fullName").GetString().Should().Be("Reservas");
        contact.GetProperty("wuid").GetString().Should().Be("14155552671");
        contact.GetProperty("phoneNumber").GetString().Should().Be("14155552671");
        contact.GetProperty("organization").GetString().Should().Be("Villa Carmen");
        contact.GetProperty("email").GetString().Should().Be("test@example.com");
    }

    // Regression: Evolution v2.4.0-rc2 rejects sendContact with `"email":null`
    // ("contact[0].email is not of a type(s) string"). Optional fields must be omitted.
    [Fact]
    public async Task SendContactCardAsync_WhenOptionalFieldsAreNull_OmitsThem()
    {
        var handler = SuccessHandler();
        var accepted = await CreateService(handler).SendContactCardAsync(
            "638857294", "Gestión Reservas", "638857294", "Alquería Villa Carmen");

        accepted.Should().BeTrue();
        var payload = AssertRequest(handler, "/message/sendContact/villa-carmen");
        var contact = payload.GetProperty("contact")[0];
        contact.TryGetProperty("email", out _).Should().BeFalse();
        contact.GetProperty("organization").GetString().Should().Be("Alquería Villa Carmen");
    }

    [Theory]
    [InlineData("👀")]
    [InlineData("")]
    public async Task SendReactionAsync_AcceptedResponse_SendsAddOrRemoveReaction(string reaction)
    {
        var handler = SuccessHandler();

        (await CreateService(handler).SendReactionAsync("638857294", "inbound-1", reaction)).Should().BeTrue();

        var payload = AssertRequest(handler, "/message/sendReaction/villa-carmen");
        payload.GetProperty("key").GetProperty("id").GetString().Should().Be("inbound-1");
        payload.GetProperty("key").GetProperty("remoteJid").GetString().Should().Be("34638857294@s.whatsapp.net");
        payload.GetProperty("key").GetProperty("fromMe").GetBoolean().Should().BeFalse();
        payload.GetProperty("reaction").GetString().Should().Be(reaction);
    }

    [Fact]
    public async Task MarkAsReadAsync_Rc2Success_SendsReadReceiptWithoutReaction()
    {
        var handler = new RecordingHandler(_ => JsonResponse(HttpStatusCode.Created,
            """{"message":"Read messages","read":"success"}"""));

        (await CreateService(handler).MarkAsReadAsync("638857294", "inbound-1")).Should().BeTrue();

        var payload = AssertRequest(handler, "/chat/markMessageAsRead/villa-carmen");
        var key = payload.GetProperty("readMessages")[0];
        key.GetProperty("id").GetString().Should().Be("inbound-1");
        key.GetProperty("remoteJid").GetString().Should().Be("34638857294@s.whatsapp.net");
        key.GetProperty("fromMe").GetBoolean().Should().BeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "{\"message\":\"Read messages\",\"read\":\"success\"}")]
    [InlineData(HttpStatusCode.Created, "{\"key\":{\"id\":\"message-1\"}}")]
    [InlineData(HttpStatusCode.Created, "{\"message\":\"Read messages\",\"read\":\"failed\"}")]
    [InlineData(HttpStatusCode.InternalServerError, "{\"error\":\"failed\"}")]
    public async Task MarkAsReadAsync_NonRc2Success_ReturnsFalse(HttpStatusCode status, string body)
    {
        var handler = new RecordingHandler(_ => JsonResponse(status, body));

        (await CreateService(handler).MarkAsReadAsync("638857294", "inbound-1")).Should().BeFalse();
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task RejectCallAsync_Unsupported_DoesNotSendRequest()
    {
        var handler = SuccessHandler();

        (await CreateService(handler).RejectCallAsync("638857294", "call-1")).Should().BeFalse();
        handler.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("text", "/message/sendText/villa-carmen")]
    [InlineData("reply", "/message/sendButtons/villa-carmen")]
    [InlineData("url", "/message/sendButtons/villa-carmen")]
    [InlineData("list", "/message/sendList/villa-carmen")]
    [InlineData("contact", "/message/sendContact/villa-carmen")]
    [InlineData("reaction", "/message/sendReaction/villa-carmen")]
    public async Task MessageMethod_ProviderError_ReturnsFalse(string operation, string expectedPath)
    {
        var handler = new RecordingHandler(_ => JsonResponse(HttpStatusCode.BadRequest, "{\"error\":\"invalid\"}"));
        var service = CreateService(handler);

        var accepted = operation switch
        {
            "text" => await service.SendTextAsync("638857294", "Text"),
            "reply" => await service.SendButtonsAsync("638857294", "Text", "Footer", [new("id", "Reply")]),
            "url" => await service.SendLinkButtonsAsync("638857294", "Text", [new("URL", "https://example.test")]),
            "list" => await service.SendMenuAsync("638857294", "Text", "Open", [new("Section", [new("id", "Row")])]),
            "contact" => await service.SendContactCardAsync("638857294", "Name", "638857294"),
            "reaction" => await service.SendReactionAsync("638857294", "message-1"),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        accepted.Should().BeFalse();
        AssertRequest(handler, expectedPath);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "{\"success\":true,\"status\":\"ERROR\",\"key\":{\"id\":\"message-1\"}}")]
    [InlineData(HttpStatusCode.Created, "{\"success\":true}")]
    [InlineData(HttpStatusCode.Created, "not-json")]
    public async Task SendTextAsync_UnacceptedBody_ReturnsFalse(HttpStatusCode status, string body)
    {
        var handler = new RecordingHandler(_ => JsonResponse(status, body));

        (await CreateService(handler).SendTextAsync("638857294", "Text")).Should().BeFalse();
    }

    [Fact]
    public async Task HistoryMethods_ProviderError_ReturnEmptyResults()
    {
        var handler = new RecordingHandler(_ => JsonResponse(HttpStatusCode.InternalServerError, "{\"error\":\"failed\"}"));
        var service = CreateService(handler);

        (await service.GetHistoryAsync("638857294")).Should().BeEmpty();
        (await service.GetHistoryPageAsync("638857294")).Messages.Should().BeEmpty();
        (await service.GetFullHistoryAsync("638857294")).Should().BeEmpty();
        handler.Requests.Should().HaveCount(3);
        handler.Requests.Should().OnlyContain(request => request.Path == "/chat/findMessages/villa-carmen");
    }

    private static RecordingHandler SuccessHandler() => new(_ => JsonResponse(HttpStatusCode.Created,
        """{"success":true,"status":"PENDING","key":{"id":"message-123"}}"""));

    private static JsonElement AssertRequest(RecordingHandler handler, string expectedPath)
    {
        handler.Requests.Should().ContainSingle();
        var request = handler.Requests.Single();
        request.Method.Should().Be(HttpMethod.Post);
        request.Path.Should().Be(expectedPath);
        request.ApiKey.Should().Be("test-api-key");
        request.Origin.Should().Be("https://evolution.test");
        return JsonDocument.Parse(request.Body).RootElement.Clone();
    }

    private static string HistoryPage(int currentPage, int pages, params string[] records) =>
        $"{{\"messages\":{{\"pages\":{pages},\"currentPage\":{currentPage},\"records\":[{string.Join(',', records)}]}}}}";

    private static EvolutionWhatsAppService CreateService(RecordingHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WhatsApp:Evolution:ApiKey"] = "test-api-key",
                ["WhatsApp:Evolution:InstanceName"] = "villa-carmen"
            })
            .Build();
        return new EvolutionWhatsAppService(
            new HttpClient(handler) { BaseAddress = new Uri("https://evolution.test/") },
            configuration,
            NullLogger<EvolutionWhatsAppService>.Instance);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body) => new(statusCode)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!.AbsolutePath,
                request.Headers.GetValues("apikey").Single(),
                request.Headers.GetValues("Origin").Single(),
                await request.Content!.ReadAsStringAsync(cancellationToken)));
            return _responseFactory(request);
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, string Path, string ApiKey, string Origin, string Body);
}
