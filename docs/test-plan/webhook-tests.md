# Webhook & Infrastructure Tests (Steps 1-25)

## Overview
These tests verify the webhook controller, payload extraction, and error handling.

---

## Health Check Tests (Steps 1-3)

### Step 1: WebhookController_Health_ReturnsOk
**Type:** Unit Test
**Target:** `WebhookController.Health()`

```csharp
[Fact]
public void Health_ReturnsOkObjectResult()
{
    // Arrange
    var controller = CreateController();

    // Act
    var result = controller.Health();

    // Assert
    var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
    var value = okResult.Value as dynamic;
    ((string)value.status).Should().Be("healthy");
}
```

**Expected:** Returns 200 OK with `{ status: "healthy", ... }`

---

### Step 2: WebhookController_Health_IncludesTimestamp
**Type:** Unit Test
**Target:** `WebhookController.Health()`

```csharp
[Fact]
public void Health_IncludesTimestamp()
{
    // Arrange
    var controller = CreateController();
    var before = DateTime.UtcNow;

    // Act
    var result = controller.Health() as OkObjectResult;
    var after = DateTime.UtcNow;

    // Assert
    var value = result.Value as dynamic;
    var timestamp = (DateTime)value.timestamp;
    timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
}
```

**Expected:** Timestamp is within test execution window

---

### Step 3: WebhookController_Health_IncludesVersion
**Type:** Unit Test
**Target:** `WebhookController.Health()`

```csharp
[Fact]
public void Health_IncludesVersion()
{
    // Arrange
    var controller = CreateController();

    // Act
    var result = controller.Health() as OkObjectResult;

    // Assert
    var value = result.Value as dynamic;
    ((string)value.version).Should().Be("1.0.0");
}
```

**Expected:** Version is "1.0.0"

---

## Webhook Processing Tests (Steps 4-9)

### Step 4: WebhookController_ValidPayload_Returns200
**Type:** Unit Test
**Target:** `WebhookController.HandleWhatsAppWebhook()`

```csharp
[Fact]
public async Task HandleWhatsAppWebhook_ValidPayload_Returns200()
{
    // Arrange
    var controller = CreateController();
    var payload = CreateValidPayload("Hola", "34666123456");

    // Act
    var result = await controller.HandleWhatsAppWebhook(payload, CancellationToken.None);

    // Assert
    result.Should().BeOfType<OkObjectResult>();
}
```

**Payload:**
```json
{
  "message": {
    "chatid": "34666123456@s.whatsapp.net",
    "text": "Hola",
    "fromMe": false,
    "messageType": "text",
    "messageTimestamp": 1700000000
  },
  "chat": {
    "name": "Juan"
  }
}
```

**Expected:** Returns 200 OK with `{ processed: true }`

---

### Step 5: WebhookController_InvalidJson_Returns400
**Type:** Unit Test
**Target:** `WebhookController.HandleWhatsAppWebhook()`

```csharp
[Fact]
public async Task HandleWhatsAppWebhook_InvalidJson_Returns400()
{
    // Arrange
    var controller = CreateController();
    // Simulate JsonElement parse failure by triggering JsonException

    // Act & Assert
    // When JsonException is thrown during extraction
    var result = await controller.HandleWhatsAppWebhook(
        invalidJsonElement, CancellationToken.None);

    result.Should().BeOfType<BadRequestObjectResult>();
}
```

**Expected:** Returns 400 BadRequest with `{ error: "Invalid JSON" }`

---

### Step 6: WebhookController_MissingMessage_Returns400
**Type:** Unit Test
**Target:** `WebhookController.HandleWhatsAppWebhook()`

```csharp
[Fact]
public async Task HandleWhatsAppWebhook_MissingMessageProperty_Returns400()
{
    // Arrange
    var controller = CreateController();
    var payload = JsonDocument.Parse("{ \"other\": \"data\" }").RootElement;

    // Act
    var result = await controller.HandleWhatsAppWebhook(payload, CancellationToken.None);

    // Assert
    result.Should().BeOfType<BadRequestObjectResult>();
}
```

**Expected:** Returns 400 when "message" property is missing

---

### Step 7: WebhookController_FromMe_ReturnsOkNoProcess
**Type:** Unit Test
**Target:** `WebhookController.HandleWhatsAppWebhook()`

```csharp
[Fact]
public async Task HandleWhatsAppWebhook_FromMe_ReturnsOkWithoutProcessing()
{
    // Arrange
    var controller = CreateControllerWithMocks(out var mainAgentMock, out _);
    var payload = CreatePayload(text: "Test", fromMe: true);

    // Act
    var result = await controller.HandleWhatsAppWebhook(payload, CancellationToken.None);

    // Assert
    result.Should().BeOfType<OkResult>();
    mainAgentMock.Verify(x => x.ProcessAsync(
        It.IsAny<WhatsAppMessage>(),
        It.IsAny<ConversationState>(),
        It.IsAny<List<ChatMessage>>(),
        It.IsAny<CancellationToken>()), Times.Never);
}
```

**Expected:** Returns 200 OK but does NOT call MainConversationAgent

---

### Step 8: WebhookController_EmptyText_ReturnsOkNoProcess
**Type:** Unit Test
**Target:** `WebhookController.HandleWhatsAppWebhook()`

```csharp
[Fact]
public async Task HandleWhatsAppWebhook_EmptyText_ReturnsOkWithoutProcessing()
{
    // Arrange
    var controller = CreateControllerWithMocks(out var mainAgentMock, out _);
    var payload = CreatePayload(text: "", fromMe: false);

    // Act
    var result = await controller.HandleWhatsAppWebhook(payload, CancellationToken.None);

    // Assert
    result.Should().BeOfType<OkResult>();
    mainAgentMock.Verify(x => x.ProcessAsync(
        It.IsAny<WhatsAppMessage>(),
        It.IsAny<ConversationState>(),
        It.IsAny<List<ChatMessage>>(),
        It.IsAny<CancellationToken>()), Times.Never);
}
```

**Expected:** Returns 200 OK but does NOT process empty messages

---

### Step 9: WebhookController_MediaMessage_ReturnsOkNoProcess
**Type:** Unit Test
**Target:** `WebhookController.HandleWhatsAppWebhook()`

```csharp
[Fact]
public async Task HandleWhatsAppWebhook_MediaMessage_ReturnsOkWithoutProcessing()
{
    // Arrange
    var controller = CreateControllerWithMocks(out var mainAgentMock, out _);
    var payload = CreatePayload(messageType: "image", text: null);

    // Act
    var result = await controller.HandleWhatsAppWebhook(payload, CancellationToken.None);

    // Assert
    result.Should().BeOfType<OkResult>();
    mainAgentMock.Verify(x => x.ProcessAsync(
        It.IsAny<WhatsAppMessage>(),
        It.IsAny<ConversationState>(),
        It.IsAny<List<ChatMessage>>(),
        It.IsAny<CancellationToken>()), Times.Never);
}
```

**Expected:** Returns 200 OK but does NOT process media-only messages

---

## Message Extraction Tests (Steps 10-18)

### Step 10: WebhookController_ExtractsPhoneNumber_FromChatId
**Type:** Unit Test
**Target:** `WebhookController.ExtractMessage()`

```csharp
[Theory]
[InlineData("34666123456@s.whatsapp.net", "34666123456")]
[InlineData("1234567890@s.whatsapp.net", "1234567890")]
public async Task ExtractMessage_ExtractsPhoneNumber_FromChatId(
    string chatId, string expectedPhone)
{
    // Arrange & Act
    var message = ExtractMessageFromPayload(chatId: chatId);

    // Assert
    message.SenderNumber.Should().Be(expectedPhone);
}
```

**Expected:** Phone number extracted by removing "@s.whatsapp.net" suffix

---

### Step 11: WebhookController_ExtractsText_FromTextProperty
**Type:** Unit Test
**Target:** `WebhookController.ExtractMessage()`

```csharp
[Fact]
public void ExtractMessage_ExtractsText_FromTextProperty()
{
    // Arrange
    var payload = CreatePayload(text: "Quiero reservar");

    // Act
    var message = ExtractMessageFromPayload(payload);

    // Assert
    message.MessageText.Should().Be("Quiero reservar");
}
```

**Expected:** Regular text message extracted from "text" property

---

### Step 12: WebhookController_ExtractsText_FromVoteProperty
**Type:** Unit Test
**Target:** `WebhookController.ExtractMessage()`

```csharp
[Fact]
public void ExtractMessage_ExtractsText_FromVoteProperty()
{
    // Arrange
    var payload = CreateButtonResponsePayload(vote: "Confirmar");

    // Act
    var message = ExtractMessageFromPayload(payload);

    // Assert
    message.MessageText.Should().Be("Confirmar");
}
```

**Payload with vote:**
```json
{
  "message": {
    "chatid": "34666123456@s.whatsapp.net",
    "vote": "Confirmar",
    "messageType": "ButtonsResponseMessage"
  }
}
```

**Expected:** Button response text extracted from "vote" property

---

### Step 13: WebhookController_ExtractsText_FromListResponse
**Type:** Unit Test
**Target:** `WebhookController.ExtractMessage()`

```csharp
[Fact]
public void ExtractMessage_ExtractsText_FromListResponse()
{
    // Arrange
    var payload = CreateListResponsePayload(selectedText: "Arroz del señoret");

    // Act
    var message = ExtractMessageFromPayload(payload);

    // Assert
    message.MessageText.Should().Be("Arroz del señoret");
}
```

**Payload with list response:**
```json
{
  "message": {
    "chatid": "34666123456@s.whatsapp.net",
    "messageType": "ListResponseMessage",
    "content": {
      "Response": {
        "SelectedDisplayText": "Arroz del señoret"
      }
    }
  }
}
```

**Expected:** List selection extracted from nested content structure

---

### Step 14: WebhookController_ExtractsPushName_FromChat
**Type:** Unit Test
**Target:** `WebhookController.ExtractMessage()`

```csharp
[Theory]
[InlineData("Juan García", "Juan García")]
[InlineData(null, "Cliente")]
[InlineData("", "Cliente")]
public void ExtractMessage_ExtractsPushName_FromChat(
    string? chatName, string expectedPushName)
{
    // Arrange
    var payload = CreatePayload(chatName: chatName);

    // Act
    var message = ExtractMessageFromPayload(payload);

    // Assert
    message.PushName.Should().Be(expectedPushName);
}
```

**Expected:** Push name from chat.name, defaults to "Cliente" if missing

---

### Step 15: WebhookController_ExtractsTimestamp_FromPayload
**Type:** Unit Test
**Target:** `WebhookController.ExtractMessage()`

```csharp
[Fact]
public void ExtractMessage_ExtractsTimestamp_FromPayload()
{
    // Arrange
    var timestamp = 1700000000L;
    var payload = CreatePayload(messageTimestamp: timestamp);

    // Act
    var message = ExtractMessageFromPayload(payload);

    // Assert
    message.Timestamp.Should().Be(timestamp);
}
```

**Expected:** Unix timestamp extracted from messageTimestamp

---

### Step 16: WebhookController_IdentifiesButtonResponse
**Type:** Unit Test
**Target:** `WebhookController.ExtractMessage()`

```csharp
[Fact]
public void ExtractMessage_IdentifiesButtonResponse()
{
    // Arrange
    var payload = CreatePayload(messageType: "ButtonsResponseMessage");

    // Act
    var message = ExtractMessageFromPayload(payload);

    // Assert
    message.IsButtonResponse.Should().BeTrue();
    message.MessageType.Should().Be("ButtonsResponseMessage");
}
```

**Expected:** `IsButtonResponse = true` for ButtonsResponseMessage

---

### Step 17: WebhookController_IdentifiesListResponse
**Type:** Unit Test
**Target:** `WebhookController.ExtractMessage()`

```csharp
[Fact]
public void ExtractMessage_IdentifiesListResponse()
{
    // Arrange
    var payload = CreatePayload(messageType: "ListResponseMessage");

    // Act
    var message = ExtractMessageFromPayload(payload);

    // Assert
    message.IsButtonResponse.Should().BeTrue();
    message.MessageType.Should().Be("ListResponseMessage");
}
```

**Expected:** `IsButtonResponse = true` for ListResponseMessage

---

### Step 18: WebhookController_ExtractsButtonId
**Type:** Unit Test
**Target:** `WebhookController.ExtractMessage()`

```csharp
[Fact]
public void ExtractMessage_ExtractsButtonId()
{
    // Arrange
    var payload = CreatePayload(buttonOrListid: "btn_confirm_1");

    // Act
    var message = ExtractMessageFromPayload(payload);

    // Assert
    message.ButtonId.Should().Be("btn_confirm_1");
}
```

**Expected:** Button ID extracted from buttonOrListid property

---

## Message Flow Tests (Steps 19-21)

### Step 19: WebhookController_ProcessesMessage_CallsMainAgent
**Type:** Unit Test
**Target:** `WebhookController.HandleWhatsAppWebhook()`

```csharp
[Fact]
public async Task HandleWhatsAppWebhook_ValidMessage_CallsMainAgent()
{
    // Arrange
    var mainAgentMock = new Mock<MainConversationAgent>();
    mainAgentMock
        .Setup(x => x.ProcessAsync(
            It.IsAny<WhatsAppMessage>(),
            It.IsAny<ConversationState>(),
            It.IsAny<List<ChatMessage>>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(new AgentResponse { Intent = IntentType.Normal, AiResponse = "Hola" });

    var controller = CreateController(mainAgent: mainAgentMock.Object);
    var payload = CreateValidPayload("Hola");

    // Act
    await controller.HandleWhatsAppWebhook(payload, CancellationToken.None);

    // Assert
    mainAgentMock.Verify(x => x.ProcessAsync(
        It.Is<WhatsAppMessage>(m => m.MessageText == "Hola"),
        It.IsAny<ConversationState>(),
        It.IsAny<List<ChatMessage>>(),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

**Expected:** MainConversationAgent.ProcessAsync called with message

---

### Step 20: WebhookController_ProcessesMessage_CallsIntentRouter
**Type:** Unit Test
**Target:** `WebhookController.HandleWhatsAppWebhook()`

```csharp
[Fact]
public async Task HandleWhatsAppWebhook_AfterMainAgent_CallsIntentRouter()
{
    // Arrange
    var intentRouterMock = new Mock<IIntentRouterService>();
    intentRouterMock
        .Setup(x => x.RouteAsync(
            It.IsAny<AgentResponse>(),
            It.IsAny<WhatsAppMessage>(),
            It.IsAny<ConversationState>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync((AgentResponse r, WhatsAppMessage m, ConversationState s, CancellationToken c) => r);

    var controller = CreateController(intentRouter: intentRouterMock.Object);
    var payload = CreateValidPayload("Quiero reservar");

    // Act
    await controller.HandleWhatsAppWebhook(payload, CancellationToken.None);

    // Assert
    intentRouterMock.Verify(x => x.RouteAsync(
        It.IsAny<AgentResponse>(),
        It.IsAny<WhatsAppMessage>(),
        It.IsAny<ConversationState>(),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

**Expected:** IntentRouterService.RouteAsync called with agent response

---

### Step 21: WebhookController_ProcessesMessage_SendsResponse
**Type:** Unit Test
**Target:** `WebhookController.HandleWhatsAppWebhook()`

```csharp
[Fact]
public async Task HandleWhatsAppWebhook_AfterRouting_SendsResponse()
{
    // Arrange
    var whatsAppMock = new Mock<IWhatsAppService>();
    whatsAppMock
        .Setup(x => x.SendTextAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(true);

    var controller = CreateController(whatsApp: whatsAppMock.Object);
    var payload = CreateValidPayload("Hola", phone: "34666123456");

    // Act
    await controller.HandleWhatsAppWebhook(payload, CancellationToken.None);

    // Assert
    whatsAppMock.Verify(x => x.SendTextAsync(
        "34666123456",
        It.IsAny<string>(),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

**Expected:** WhatsAppService.SendTextAsync called with phone and response

---

## Error Handling Tests (Steps 22-25)

### Step 22: WebhookController_SendFails_LogsWarning
**Type:** Unit Test
**Target:** `WebhookController.HandleWhatsAppWebhook()`

```csharp
[Fact]
public async Task HandleWhatsAppWebhook_SendFails_LogsWarningButReturnsOk()
{
    // Arrange
    var loggerMock = new Mock<ILogger<WebhookController>>();
    var whatsAppMock = new Mock<IWhatsAppService>();
    whatsAppMock
        .Setup(x => x.SendTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);

    var controller = CreateController(whatsApp: whatsAppMock.Object, logger: loggerMock.Object);
    var payload = CreateValidPayload("Hola");

    // Act
    var result = await controller.HandleWhatsAppWebhook(payload, CancellationToken.None);

    // Assert
    result.Should().BeOfType<OkObjectResult>();
    // Verify warning was logged
}
```

**Expected:** Returns 200 OK, logs warning about failed send

---

### Step 23: WebhookController_Exception_Returns500
**Type:** Unit Test
**Target:** `WebhookController.HandleWhatsAppWebhook()`

```csharp
[Fact]
public async Task HandleWhatsAppWebhook_UnhandledException_Returns500()
{
    // Arrange
    var mainAgentMock = new Mock<MainConversationAgent>();
    mainAgentMock
        .Setup(x => x.ProcessAsync(
            It.IsAny<WhatsAppMessage>(),
            It.IsAny<ConversationState>(),
            It.IsAny<List<ChatMessage>>(),
            It.IsAny<CancellationToken>()))
        .ThrowsAsync(new Exception("Test error"));

    var controller = CreateController(mainAgent: mainAgentMock.Object);
    var payload = CreateValidPayload("Hola");

    // Act
    var result = await controller.HandleWhatsAppWebhook(payload, CancellationToken.None);

    // Assert
    var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
    statusResult.StatusCode.Should().Be(500);
}
```

**Expected:** Returns 500 with `{ error: "Internal error" }`

---

### Step 24: WebhookController_Exception_SendsErrorToUser
**Type:** Unit Test
**Target:** `WebhookController.HandleWhatsAppWebhook()`

```csharp
[Fact]
public async Task HandleWhatsAppWebhook_Exception_SendsErrorMessageToUser()
{
    // Arrange
    var whatsAppMock = new Mock<IWhatsAppService>();
    var mainAgentMock = new Mock<MainConversationAgent>();
    mainAgentMock
        .Setup(x => x.ProcessAsync(
            It.IsAny<WhatsAppMessage>(),
            It.IsAny<ConversationState>(),
            It.IsAny<List<ChatMessage>>(),
            It.IsAny<CancellationToken>()))
        .ThrowsAsync(new Exception("Test error"));

    var controller = CreateController(mainAgent: mainAgentMock.Object, whatsApp: whatsAppMock.Object);
    var payload = CreateValidPayload("Hola", phone: "34666123456");

    // Act
    await controller.HandleWhatsAppWebhook(payload, CancellationToken.None);

    // Assert
    whatsAppMock.Verify(x => x.SendTextAsync(
        "34666123456",
        It.Is<string>(s => s.Contains("error") || s.Contains("inténtalo")),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

**Expected:** Error message sent to user: "Disculpa, hubo un error..."

---

### Step 25: WebhookController_CancellationToken_Respected
**Type:** Unit Test
**Target:** `WebhookController.HandleWhatsAppWebhook()`

```csharp
[Fact]
public async Task HandleWhatsAppWebhook_CancelledToken_ThrowsOrCancels()
{
    // Arrange
    var cts = new CancellationTokenSource();
    cts.Cancel();

    var mainAgentMock = new Mock<MainConversationAgent>();
    mainAgentMock
        .Setup(x => x.ProcessAsync(
            It.IsAny<WhatsAppMessage>(),
            It.IsAny<ConversationState>(),
            It.IsAny<List<ChatMessage>>(),
            It.IsAny<CancellationToken>()))
        .Returns((WhatsAppMessage m, ConversationState s, List<ChatMessage> h, CancellationToken ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new AgentResponse { AiResponse = "Test" });
        });

    var controller = CreateController(mainAgent: mainAgentMock.Object);
    var payload = CreateValidPayload("Hola");

    // Act & Assert
    await Assert.ThrowsAsync<OperationCanceledException>(
        () => controller.HandleWhatsAppWebhook(payload, cts.Token));
}
```

**Expected:** Cancellation token properly propagated and respected

---

## Test Helpers

```csharp
public class WebhookControllerTestBase
{
    protected WebhookController CreateController(
        MainConversationAgent? mainAgent = null,
        IIntentRouterService? intentRouter = null,
        IConversationHistoryService? historyService = null,
        IWhatsAppService? whatsApp = null,
        ILogger<WebhookController>? logger = null)
    {
        return new WebhookController(
            mainAgent ?? CreateDefaultMainAgent(),
            intentRouter ?? CreateDefaultIntentRouter(),
            historyService ?? CreateDefaultHistoryService(),
            whatsApp ?? CreateDefaultWhatsAppService(),
            logger ?? Mock.Of<ILogger<WebhookController>>());
    }

    protected JsonElement CreateValidPayload(
        string text,
        string phone = "34666123456",
        string chatName = "Juan",
        bool fromMe = false)
    {
        var json = $@"{{
            ""message"": {{
                ""chatid"": ""{phone}@s.whatsapp.net"",
                ""text"": ""{text}"",
                ""fromMe"": {fromMe.ToString().ToLower()},
                ""messageType"": ""text"",
                ""messageTimestamp"": {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}
            }},
            ""chat"": {{
                ""name"": ""{chatName}""
            }}
        }}";
        return JsonDocument.Parse(json).RootElement;
    }

    protected JsonElement CreateButtonResponsePayload(string vote)
    {
        var json = $@"{{
            ""message"": {{
                ""chatid"": ""34666123456@s.whatsapp.net"",
                ""vote"": ""{vote}"",
                ""fromMe"": false,
                ""messageType"": ""ButtonsResponseMessage""
            }}
        }}";
        return JsonDocument.Parse(json).RootElement;
    }
}
```
