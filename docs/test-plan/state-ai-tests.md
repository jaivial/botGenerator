# State Management & AI Integration Tests (Steps 51-75)

## Overview
These tests verify conversation history management, state extraction, context building, prompt loading, and AI service integration.

---

## Conversation History Tests (Steps 51-56)

### Step 51: HistoryService_GetHistory_ReturnsEmpty_NewUser
**Type:** Unit Test
**Target:** `ConversationHistoryService.GetHistoryAsync()`

```csharp
[Fact]
public async Task GetHistoryAsync_NewUser_ReturnsEmptyList()
{
    // Arrange
    var service = CreateHistoryService();
    var newUserPhone = "34666000000";

    // Act
    var history = await service.GetHistoryAsync(newUserPhone);

    // Assert
    history.Should().NotBeNull();
    history.Should().BeEmpty();
}
```

**Expected:** New phone number returns empty list

---

### Step 52: HistoryService_GetHistory_ReturnsCached
**Type:** Unit Test
**Target:** `ConversationHistoryService.GetHistoryAsync()`

```csharp
[Fact]
public async Task GetHistoryAsync_ExistingUser_ReturnsCachedHistory()
{
    // Arrange
    var service = CreateHistoryService();
    var phone = "34666123456";

    await service.AddMessageAsync(phone, ChatMessage.FromUser("Hola"));
    await service.AddMessageAsync(phone, ChatMessage.FromAssistant("¡Hola! ¿En qué puedo ayudarte?"));

    // Act
    var history = await service.GetHistoryAsync(phone);

    // Assert
    history.Should().HaveCount(2);
    history[0].Role.Should().Be("user");
    history[0].Content.Should().Be("Hola");
    history[1].Role.Should().Be("assistant");
}
```

**Expected:** Previously added messages returned correctly

---

### Step 53: HistoryService_AddMessage_StoresMessage
**Type:** Unit Test
**Target:** `ConversationHistoryService.AddMessageAsync()`

```csharp
[Fact]
public async Task AddMessageAsync_StoresMessage_InHistory()
{
    // Arrange
    var service = CreateHistoryService();
    var phone = "34666123456";
    var message = ChatMessage.FromUser("Quiero reservar");

    // Act
    await service.AddMessageAsync(phone, message);
    var history = await service.GetHistoryAsync(phone);

    // Assert
    history.Should().ContainSingle();
    history[0].Content.Should().Be("Quiero reservar");
}
```

**Expected:** Message stored and retrievable

---

### Step 54: HistoryService_AddMessage_TrimsToMaxMessages
**Type:** Unit Test
**Target:** `ConversationHistoryService.AddMessageAsync()`

```csharp
[Fact]
public async Task AddMessageAsync_ExceedsMax_TrimsOldMessages()
{
    // Arrange
    var config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string>
        {
            ["History:MaxMessages"] = "5"
        })
        .Build();
    var service = CreateHistoryService(config);
    var phone = "34666123456";

    // Add 7 messages
    for (int i = 1; i <= 7; i++)
    {
        await service.AddMessageAsync(phone, ChatMessage.FromUser($"Message {i}"));
    }

    // Act
    var history = await service.GetHistoryAsync(phone);

    // Assert
    history.Should().HaveCount(5);
    history[0].Content.Should().Be("Message 3"); // Oldest kept
    history[4].Content.Should().Be("Message 7"); // Newest
}
```

**Expected:** History trimmed to max 30 messages (or configured max), keeps newest

---

### Step 55: HistoryService_SessionTimeout_ClearsHistory
**Type:** Unit Test
**Target:** `ConversationHistoryService.GetHistoryAsync()`

```csharp
[Fact]
public async Task GetHistoryAsync_SessionExpired_ClearsHistory()
{
    // Arrange
    var config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string>
        {
            ["History:SessionTimeoutMinutes"] = "0" // Immediate timeout for test
        })
        .Build();
    var service = CreateHistoryService(config);
    var phone = "34666123456";

    await service.AddMessageAsync(phone, ChatMessage.FromUser("Hola"));
    await Task.Delay(100); // Ensure timeout

    // Act
    var history = await service.GetHistoryAsync(phone);

    // Assert
    history.Should().BeEmpty();
}
```

**Expected:** Expired session returns empty history

---

### Step 56: HistoryService_ClearHistory_RemovesAll
**Type:** Unit Test
**Target:** `ConversationHistoryService.ClearHistoryAsync()`

```csharp
[Fact]
public async Task ClearHistoryAsync_RemovesAllMessages()
{
    // Arrange
    var service = CreateHistoryService();
    var phone = "34666123456";

    await service.AddMessageAsync(phone, ChatMessage.FromUser("Hola"));
    await service.AddMessageAsync(phone, ChatMessage.FromAssistant("¡Hola!"));

    // Act
    await service.ClearHistoryAsync(phone);
    var history = await service.GetHistoryAsync(phone);

    // Assert
    history.Should().BeEmpty();
}
```

**Expected:** All messages removed after clear

---

## State Extraction Tests (Steps 57-63)

### Step 57: HistoryService_ExtractState_ExtractsDate
**Type:** Unit Test
**Target:** `ConversationHistoryService.ExtractState()`

```csharp
[Theory]
[InlineData("para el sábado", "30/11/2025")] // Assuming next Saturday
[InlineData("el domingo", "01/12/2025")]
[InlineData("para el 30/11/2025", "30/11/2025")]
[InlineData("el 5/12", "5/12")]
public void ExtractState_ExtractsDate_FromHistory(string userMessage, string expectedDate)
{
    // Arrange
    var history = new List<ChatMessage>
    {
        ChatMessage.FromUser(userMessage)
    };
    var service = CreateHistoryService();

    // Act
    var state = service.ExtractState(history);

    // Assert
    state.Fecha.Should().Contain(expectedDate.Split('/')[0]); // At least day matches
}
```

**Expected:** Date extracted from various Spanish date formats

---

### Step 58: HistoryService_ExtractState_ExtractsTime
**Type:** Unit Test
**Target:** `ConversationHistoryService.ExtractState()`

```csharp
[Theory]
[InlineData("a las 14:00", "14:00")]
[InlineData("para las 15:30", "15:30")]
[InlineData("a la 13", "13:00")]
[InlineData("a las 2", "2:00")]
public void ExtractState_ExtractsTime_FromHistory(string userMessage, string expectedTime)
{
    // Arrange
    var history = new List<ChatMessage>
    {
        ChatMessage.FromUser(userMessage)
    };
    var service = CreateHistoryService();

    // Act
    var state = service.ExtractState(history);

    // Assert
    state.Hora.Should().Be(expectedTime);
}
```

**Expected:** Time extracted from "a las X" patterns

---

### Step 59: HistoryService_ExtractState_ExtractsPeople
**Type:** Unit Test
**Target:** `ConversationHistoryService.ExtractState()`

```csharp
[Theory]
[InlineData("para 4 personas", 4)]
[InlineData("somos 6", 6)]
[InlineData("6 personas", 6)]
[InlineData("para 2", 2)]
public void ExtractState_ExtractsPeople_FromHistory(string userMessage, int expectedPeople)
{
    // Arrange
    var history = new List<ChatMessage>
    {
        ChatMessage.FromUser(userMessage)
    };
    var service = CreateHistoryService();

    // Act
    var state = service.ExtractState(history);

    // Assert
    state.Personas.Should().Be(expectedPeople);
}
```

**Expected:** People count extracted from various patterns

---

### Step 60: HistoryService_ExtractState_ExtractsRiceType
**Type:** Unit Test
**Target:** `ConversationHistoryService.ExtractState()`

```csharp
[Fact]
public void ExtractState_ExtractsRiceType_FromValidation()
{
    // Arrange
    var history = new List<ChatMessage>
    {
        ChatMessage.FromUser("Arroz del señoret"),
        ChatMessage.FromAssistant("✅ Arroz del señoret disponible. ¿Cuántas raciones?")
    };
    var service = CreateHistoryService();

    // Act
    var state = service.ExtractState(history);

    // Assert
    state.ArrozType.Should().Be("Arroz del señoret");
}
```

**Expected:** Rice type extracted from validation confirmation message

---

### Step 61: HistoryService_ExtractState_DetectsNoRice
**Type:** Unit Test
**Target:** `ConversationHistoryService.ExtractState()`

```csharp
[Theory]
[InlineData("no")]
[InlineData("nada")]
[InlineData("sin arroz")]
public void ExtractState_DetectsNoRice_Response(string userResponse)
{
    // Arrange
    var history = new List<ChatMessage>
    {
        ChatMessage.FromAssistant("¿Queréis arroz?"),
        ChatMessage.FromUser(userResponse)
    };
    var service = CreateHistoryService();

    // Act
    var state = service.ExtractState(history);

    // Assert
    state.ArrozType.Should().Be(""); // Empty string = explicitly no rice
}
```

**Expected:** "no" response to rice question sets ArrozType to empty string

---

### Step 62: HistoryService_ExtractState_IdentifiesMissingData
**Type:** Unit Test
**Target:** `ConversationHistoryService.ExtractState()`

```csharp
[Fact]
public void ExtractState_IdentifiesMissingData_WhenIncomplete()
{
    // Arrange
    var history = new List<ChatMessage>
    {
        ChatMessage.FromUser("Quiero reservar para el sábado")
        // Only date provided
    };
    var service = CreateHistoryService();

    // Act
    var state = service.ExtractState(history);

    // Assert
    state.MissingData.Should().Contain("hora");
    state.MissingData.Should().Contain("personas");
    state.MissingData.Should().Contain("arroz_decision");
    state.IsComplete.Should().BeFalse();
}
```

**Expected:** Missing fields listed in MissingData

---

### Step 63: HistoryService_ExtractState_SetsStage
**Type:** Unit Test
**Target:** `ConversationHistoryService.ExtractState()`

```csharp
[Fact]
public void ExtractState_SetsStage_BasedOnCompleteness()
{
    // Arrange - Complete data
    var completeHistory = new List<ChatMessage>
    {
        ChatMessage.FromUser("Para el sábado a las 14:00 para 4 personas"),
        ChatMessage.FromAssistant("¿Queréis arroz?"),
        ChatMessage.FromUser("no")
    };

    // Arrange - Incomplete data
    var incompleteHistory = new List<ChatMessage>
    {
        ChatMessage.FromUser("Quiero reservar")
    };

    var service = CreateHistoryService();

    // Act
    var completeState = service.ExtractState(completeHistory);
    var incompleteState = service.ExtractState(incompleteHistory);

    // Assert
    completeState.Stage.Should().Be("awaiting_confirmation");
    incompleteState.Stage.Should().Be("collecting_info");
}
```

**Expected:** Stage is "collecting_info" or "awaiting_confirmation" based on data

---

## Context Building Tests (Steps 64-66)

### Step 64: ContextBuilder_BuildsContext_IncludesToday
**Type:** Unit Test
**Target:** `ContextBuilderService.BuildContext()`

```csharp
[Fact]
public void BuildContext_IncludesTodayInSpanish()
{
    // Arrange
    var service = CreateContextBuilder();
    var message = new WhatsAppMessage { MessageText = "Hola", PushName = "Juan" };

    // Act
    var context = service.BuildContext(message, null, null);

    // Assert
    context.Should().ContainKey("todayES");
    context.Should().ContainKey("todayFormatted");
    context.Should().ContainKey("currentYear");

    var todayES = context["todayES"].ToString();
    todayES.Should().MatchRegex(@"(lunes|martes|miércoles|jueves|viernes|sábado|domingo)");
}
```

**Expected:** Today's date in Spanish format included

---

### Step 65: ContextBuilder_BuildsContext_IncludesWeekends
**Type:** Unit Test
**Target:** `ContextBuilderService.GetUpcomingWeekends()`

```csharp
[Fact]
public void GetUpcomingWeekends_ReturnsNextFourWeekends()
{
    // Arrange
    var service = CreateContextBuilder();

    // Act
    var weekends = service.GetUpcomingWeekends();

    // Assert
    weekends.Should().HaveCount(8); // 4 Saturdays + 4 Sundays
    weekends.Should().OnlyContain(w =>
        w.DayName == "sábado" || w.DayName == "domingo");
    weekends.All(w => w.Date >= DateTime.Today).Should().BeTrue();
}
```

**Expected:** Next 4 weekends (Saturday/Sunday) calculated

---

### Step 66: ContextBuilder_BuildsContext_IncludesState
**Type:** Unit Test
**Target:** `ContextBuilderService.BuildContext()`

```csharp
[Fact]
public void BuildContext_IncludesStateFields()
{
    // Arrange
    var service = CreateContextBuilder();
    var state = new ConversationState
    {
        Fecha = "30/11/2025",
        Hora = "14:00",
        Personas = 4
    };

    // Act
    var context = service.BuildContext(
        new WhatsAppMessage { MessageText = "Test" },
        state,
        null);

    // Assert
    context.Should().ContainKey("state_fecha");
    context["state_fecha"].Should().Be("30/11/2025");
    context.Should().ContainKey("state_hora");
    context["state_hora"].Should().Be("14:00");
    context.Should().ContainKey("state_personas");
    context["state_personas"].Should().Be(4);
}
```

**Expected:** Conversation state fields prefixed with "state_" in context

---

## Prompt Loading Tests (Steps 67-72)

### Step 67: PromptLoader_LoadsRestaurantPrompt
**Type:** Unit Test
**Target:** `PromptLoaderService.LoadPromptAsync()`

```csharp
[Fact]
public async Task LoadPromptAsync_LoadsRestaurantSpecificPrompt()
{
    // Arrange
    var service = CreatePromptLoader();

    // Act
    var prompt = await service.LoadPromptAsync("villacarmen", "system-main");

    // Assert
    prompt.Should().NotBeNullOrEmpty();
    prompt.Should().Contain("Alquería Villa Carmen");
    prompt.Should().Contain("{{pushName}}"); // Token placeholder
}
```

**Expected:** Restaurant-specific prompt loaded from file

---

### Step 68: PromptLoader_LoadsSharedPrompt
**Type:** Unit Test
**Target:** `PromptLoaderService.LoadSharedPromptAsync()`

```csharp
[Fact]
public async Task LoadSharedPromptAsync_LoadsSharedModule()
{
    // Arrange
    var service = CreatePromptLoader();

    // Act
    var prompt = await service.LoadSharedPromptAsync("date-parsing");

    // Assert
    prompt.Should().NotBeNullOrEmpty();
}
```

**Expected:** Shared prompt loaded from shared directory

---

### Step 69: PromptLoader_AssemblesSystemPrompt
**Type:** Unit Test
**Target:** `PromptLoaderService.AssembleSystemPromptAsync()`

```csharp
[Fact]
public async Task AssembleSystemPromptAsync_CombinesAllModules()
{
    // Arrange
    var service = CreatePromptLoader();
    var context = new Dictionary<string, object>
    {
        ["pushName"] = "Juan",
        ["senderNumber"] = "34666123456"
    };

    // Act
    var prompt = await service.AssembleSystemPromptAsync("villacarmen", context);

    // Assert
    prompt.Should().Contain("Juan"); // Token replaced
    prompt.Should().Contain("34666123456");
    prompt.Should().Contain("---"); // Section separators
}
```

**Expected:** All modules assembled with tokens replaced

---

### Step 70: PromptLoader_ReplacesTokens_BracketSyntax
**Type:** Unit Test
**Target:** `PromptLoaderService.ReplaceTokens()`

```csharp
[Fact]
public void ReplaceTokens_ReplacesBracketSyntax()
{
    // Arrange
    var template = "Hola {{pushName}}, tu número es {{senderNumber}}.";
    var context = new Dictionary<string, object>
    {
        ["pushName"] = "María",
        ["senderNumber"] = "34666999888"
    };
    var service = CreatePromptLoader();

    // Act
    var result = InvokeReplaceTokens(service, template, context);

    // Assert
    result.Should().Be("Hola María, tu número es 34666999888.");
    result.Should().NotContain("{{");
}
```

**Expected:** `{{token}}` replaced with values

---

### Step 71: PromptLoader_ProcessesConditionals_IfTrue
**Type:** Unit Test
**Target:** `PromptLoaderService.ProcessConditionals()`

```csharp
[Fact]
public void ProcessConditionals_ShowsContent_WhenTrue()
{
    // Arrange
    var template = "{{#if state_fecha}}Fecha: {{state_fecha}}{{/if}}";
    var context = new Dictionary<string, object>
    {
        ["state_fecha"] = "30/11/2025"
    };
    var service = CreatePromptLoader();

    // Act
    var result = InvokeReplaceTokens(service, template, context);

    // Assert
    result.Should().Be("Fecha: 30/11/2025");
}
```

**Expected:** Content shown when condition is true

---

### Step 72: PromptLoader_ProcessesConditionals_IfFalse
**Type:** Unit Test
**Target:** `PromptLoaderService.ProcessConditionals()`

```csharp
[Fact]
public void ProcessConditionals_ShowsElse_WhenFalse()
{
    // Arrange
    var template = "{{#if state_fecha}}✅ Fecha: {{state_fecha}}{{else}}❌ Fecha: FALTA{{/if}}";
    var context = new Dictionary<string, object>(); // No state_fecha

    var service = CreatePromptLoader();

    // Act
    var result = InvokeReplaceTokens(service, template, context);

    // Assert
    result.Should().Be("❌ Fecha: FALTA");
}
```

**Expected:** Else content shown when condition is false

---

## AI Integration Tests (Steps 73-75)

### Step 73: GeminiService_SendsRequest_CorrectFormat
**Type:** Unit Test
**Target:** `GeminiService.GenerateAsync()`

```csharp
[Fact]
public async Task GenerateAsync_SendsCorrectRequestFormat()
{
    // Arrange
    var handlerMock = new Mock<HttpMessageHandler>();
    handlerMock
        .Protected()
        .Setup<Task<HttpResponseMessage>>(
            "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>())
        .ReturnsAsync(CreateGeminiResponse("Test response"))
        .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) =>
        {
            var body = await req.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(body);

            // Verify structure
            json.RootElement.TryGetProperty("system_instruction", out _).Should().BeTrue();
            json.RootElement.TryGetProperty("contents", out _).Should().BeTrue();
            json.RootElement.TryGetProperty("generationConfig", out _).Should().BeTrue();
        });

    var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://api.test.com") };
    var service = CreateGeminiService(httpClient);

    // Act
    await service.GenerateAsync("System prompt", "User message");

    // Assert - callback verifies request format
}
```

**Expected:** Request includes system_instruction, contents, and generationConfig

---

### Step 74: GeminiService_ParsesResponse_ExtractsText
**Type:** Unit Test
**Target:** `GeminiService.GenerateAsync()`

```csharp
[Fact]
public async Task GenerateAsync_ParsesResponse_ExtractsText()
{
    // Arrange
    var expectedText = "¡Hola! ¿En qué puedo ayudarte?";
    var httpClient = CreateMockHttpClient(CreateGeminiResponse(expectedText));
    var service = CreateGeminiService(httpClient);

    // Act
    var result = await service.GenerateAsync("System", "Hola");

    // Assert
    result.Should().Be(expectedText);
}

private HttpResponseMessage CreateGeminiResponse(string text)
{
    var json = $@"{{
        ""candidates"": [{{
            ""content"": {{
                ""parts"": [{{ ""text"": ""{text}"" }}],
                ""role"": ""model""
            }},
            ""finishReason"": ""STOP""
        }}]
    }}";

    return new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
}
```

**Expected:** Text extracted from candidates[0].content.parts[0].text

---

### Step 75: GeminiService_HandlesErrors_ThrowsException
**Type:** Unit Test
**Target:** `GeminiService.GenerateAsync()`

```csharp
[Theory]
[InlineData(HttpStatusCode.BadRequest, "400")]
[InlineData(HttpStatusCode.Unauthorized, "401")]
[InlineData(HttpStatusCode.TooManyRequests, "429")]
[InlineData(HttpStatusCode.InternalServerError, "500")]
public async Task GenerateAsync_ApiError_ThrowsGeminiApiException(
    HttpStatusCode statusCode, string expectedCode)
{
    // Arrange
    var httpClient = CreateMockHttpClient(new HttpResponseMessage(statusCode)
    {
        Content = new StringContent($"{{\"error\": \"Test error {expectedCode}\"}}")
    });
    var service = CreateGeminiService(httpClient);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<GeminiApiException>(
        () => service.GenerateAsync("System", "Test"));

    exception.Message.Should().Contain(statusCode.ToString());
}

[Fact]
public async Task GenerateAsync_NetworkError_ThrowsGeminiApiException()
{
    // Arrange
    var handlerMock = new Mock<HttpMessageHandler>();
    handlerMock
        .Protected()
        .Setup<Task<HttpResponseMessage>>(
            "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>())
        .ThrowsAsync(new HttpRequestException("Network error"));

    var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://api.test.com") };
    var service = CreateGeminiService(httpClient);

    // Act & Assert
    await Assert.ThrowsAsync<GeminiApiException>(
        () => service.GenerateAsync("System", "Test"));
}
```

**Expected:** API errors throw GeminiApiException with details

---

## Test Helpers

```csharp
public class StateAiTestBase
{
    protected ConversationHistoryService CreateHistoryService(IConfiguration? config = null)
    {
        config ??= new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["History:MaxMessages"] = "30",
                ["History:SessionTimeoutMinutes"] = "30"
            })
            .Build();

        var contextBuilder = CreateContextBuilder();
        return new ConversationHistoryService(
            contextBuilder,
            config,
            Mock.Of<ILogger<ConversationHistoryService>>());
    }

    protected ContextBuilderService CreateContextBuilder()
    {
        var config = new ConfigurationBuilder().Build();
        return new ContextBuilderService(config, Mock.Of<ILogger<ContextBuilderService>>());
    }

    protected PromptLoaderService CreatePromptLoader()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Prompts:BasePath"] = GetTestPromptsPath(),
                ["Prompts:CacheEnabled"] = "false"
            })
            .Build();

        return new PromptLoaderService(config, Mock.Of<ILogger<PromptLoaderService>>());
    }

    protected GeminiService CreateGeminiService(HttpClient httpClient)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["GoogleAI:ApiKey"] = "test-api-key",
                ["GoogleAI:Model"] = "gemini-2.5-flash-preview-05-20"
            })
            .Build();

        return new GeminiService(
            httpClient,
            config,
            Mock.Of<ILogger<GeminiService>>());
    }

    protected HttpClient CreateMockHttpClient(HttpResponseMessage response)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        return new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com")
        };
    }

    protected string InvokeReplaceTokens(
        PromptLoaderService service,
        string template,
        Dictionary<string, object> context)
    {
        var method = typeof(PromptLoaderService).GetMethod(
            "ReplaceTokens", BindingFlags.NonPublic | BindingFlags.Instance);
        return (string)method.Invoke(service, new object[] { template, context });
    }

    private string GetTestPromptsPath()
    {
        // Return path to test fixtures or actual prompts
        return Path.Combine(AppContext.BaseDirectory, "prompts");
    }
}
```
