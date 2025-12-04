# Message Parsing & Response Building Tests (Steps 26-50)

## Overview
These tests verify AI response parsing, data extraction, and response building logic.

---

## Booking Request Parsing Tests (Steps 26-31)

### Step 26: MainAgent_ParsesBookingRequest_ExtractsData
**Type:** Unit Test
**Target:** `MainConversationAgent.ParseAiResponse()`

```csharp
[Fact]
public void ParseAiResponse_BookingRequest_ExtractsAllFields()
{
    // Arrange
    var aiResponse = @"¡Perfecto! Tu reserva está lista.
BOOKING_REQUEST|Juan García|34666123456|30/11/2025|4|14:00";

    // Act
    var result = ParseAiResponse(aiResponse);

    // Assert
    result.Intent.Should().Be(IntentType.Booking);
    result.ExtractedData.Should().NotBeNull();
    result.ExtractedData.Name.Should().Be("Juan García");
    result.ExtractedData.Phone.Should().Be("34666123456");
    result.ExtractedData.Date.Should().Be("30/11/2025");
    result.ExtractedData.People.Should().Be(4);
    result.ExtractedData.Time.Should().Be("14:00");
}
```

**Input:** AI response with BOOKING_REQUEST|name|phone|date|people|time
**Expected:** All 5 fields extracted correctly

---

### Step 27: MainAgent_ParsesBookingRequest_ExtractsName
**Type:** Unit Test
**Target:** `MainConversationAgent.ParseAiResponse()`

```csharp
[Theory]
[InlineData("BOOKING_REQUEST|María López|34666123456|30/11/2025|2|13:30", "María López")]
[InlineData("BOOKING_REQUEST|José|34666123456|30/11/2025|2|13:30", "José")]
[InlineData("BOOKING_REQUEST|Ana María García Pérez|34666123456|30/11/2025|2|13:30", "Ana María García Pérez")]
public void ParseAiResponse_BookingRequest_ExtractsName(string response, string expectedName)
{
    // Act
    var result = ParseAiResponse(response);

    // Assert
    result.ExtractedData.Name.Should().Be(expectedName);
}
```

**Expected:** Names with accents, spaces, multiple parts extracted correctly

---

### Step 28: MainAgent_ParsesBookingRequest_ExtractsPhone
**Type:** Unit Test
**Target:** `MainConversationAgent.ParseAiResponse()`

```csharp
[Theory]
[InlineData("BOOKING_REQUEST|Juan|34666123456|30/11/2025|2|14:00", "34666123456")]
[InlineData("BOOKING_REQUEST|Juan|+34666123456|30/11/2025|2|14:00", "+34666123456")]
[InlineData("BOOKING_REQUEST|Juan|666123456|30/11/2025|2|14:00", "666123456")]
public void ParseAiResponse_BookingRequest_ExtractsPhone(string response, string expectedPhone)
{
    // Act
    var result = ParseAiResponse(response);

    // Assert
    result.ExtractedData.Phone.Should().Be(expectedPhone);
}
```

**Expected:** Phone numbers with/without country code extracted

---

### Step 29: MainAgent_ParsesBookingRequest_ExtractsDate
**Type:** Unit Test
**Target:** `MainConversationAgent.ParseAiResponse()`

```csharp
[Theory]
[InlineData("BOOKING_REQUEST|Juan|34666123456|30/11/2025|2|14:00", "30/11/2025")]
[InlineData("BOOKING_REQUEST|Juan|34666123456|01/12/2025|2|14:00", "01/12/2025")]
[InlineData("BOOKING_REQUEST|Juan|34666123456|5/1/2026|2|14:00", "5/1/2026")]
public void ParseAiResponse_BookingRequest_ExtractsDate(string response, string expectedDate)
{
    // Act
    var result = ParseAiResponse(response);

    // Assert
    result.ExtractedData.Date.Should().Be(expectedDate);
}
```

**Expected:** Dates in dd/MM/yyyy format extracted

---

### Step 30: MainAgent_ParsesBookingRequest_ExtractsPeople
**Type:** Unit Test
**Target:** `MainConversationAgent.ParseAiResponse()`

```csharp
[Theory]
[InlineData("BOOKING_REQUEST|Juan|34666123456|30/11/2025|2|14:00", 2)]
[InlineData("BOOKING_REQUEST|Juan|34666123456|30/11/2025|10|14:00", 10)]
[InlineData("BOOKING_REQUEST|Juan|34666123456|30/11/2025|1|14:00", 1)]
[InlineData("BOOKING_REQUEST|Juan|34666123456|30/11/2025|20|14:00", 20)]
public void ParseAiResponse_BookingRequest_ExtractsPeople(string response, int expectedPeople)
{
    // Act
    var result = ParseAiResponse(response);

    // Assert
    result.ExtractedData.People.Should().Be(expectedPeople);
}
```

**Expected:** People count parsed as integer

---

### Step 31: MainAgent_ParsesBookingRequest_ExtractsTime
**Type:** Unit Test
**Target:** `MainConversationAgent.ParseAiResponse()`

```csharp
[Theory]
[InlineData("BOOKING_REQUEST|Juan|34666123456|30/11/2025|2|14:00", "14:00")]
[InlineData("BOOKING_REQUEST|Juan|34666123456|30/11/2025|2|13:30", "13:30")]
[InlineData("BOOKING_REQUEST|Juan|34666123456|30/11/2025|2|15:45", "15:45")]
public void ParseAiResponse_BookingRequest_ExtractsTime(string response, string expectedTime)
{
    // Act
    var result = ParseAiResponse(response);

    // Assert
    result.ExtractedData.Time.Should().Be(expectedTime);
}
```

**Expected:** Time in HH:mm format extracted

---

## Other Intent Parsing Tests (Steps 32-39)

### Step 32: MainAgent_ParsesCancellationRequest_ExtractsData
**Type:** Unit Test
**Target:** `MainConversationAgent.ParseAiResponse()`

```csharp
[Fact]
public void ParseAiResponse_CancellationRequest_ExtractsData()
{
    // Arrange
    var aiResponse = @"Entendido, vamos a cancelar tu reserva.
CANCELLATION_REQUEST|Juan García|34666123456|30/11/2025|4|14:00";

    // Act
    var result = ParseAiResponse(aiResponse);

    // Assert
    result.Intent.Should().Be(IntentType.Cancellation);
    result.ExtractedData.Should().NotBeNull();
    result.ExtractedData.Name.Should().Be("Juan García");
    result.ExtractedData.Date.Should().Be("30/11/2025");
}
```

**Expected:** CANCELLATION_REQUEST parsed with same format as booking

---

### Step 33: MainAgent_ParsesModificationIntent
**Type:** Unit Test
**Target:** `MainConversationAgent.ParseAiResponse()`

```csharp
[Fact]
public void ParseAiResponse_ModificationIntent_SetsIntent()
{
    // Arrange
    var aiResponse = "MODIFICATION_INTENT\nVoy a buscar tu reserva para modificarla.";

    // Act
    var result = ParseAiResponse(aiResponse);

    // Assert
    result.Intent.Should().Be(IntentType.Modification);
    result.AiResponse.Should().Contain("buscar tu reserva");
    result.AiResponse.Should().NotContain("MODIFICATION_INTENT");
}
```

**Expected:** MODIFICATION_INTENT detected and removed from response

---

### Step 34: MainAgent_ParsesSameDayBooking
**Type:** Unit Test
**Target:** `MainConversationAgent.ParseAiResponse()`

```csharp
[Fact]
public void ParseAiResponse_SameDayBooking_SetsIntent()
{
    // Arrange
    var aiResponse = "SAME_DAY_BOOKING\nLo siento, no aceptamos reservas para hoy.";

    // Act
    var result = ParseAiResponse(aiResponse);

    // Assert
    result.Intent.Should().Be(IntentType.SameDay);
    result.AiResponse.Should().NotContain("SAME_DAY_BOOKING");
}
```

**Expected:** SAME_DAY_BOOKING detected and provides default message if empty

---

### Step 35: MainAgent_ExtractsRiceType_FromResponse
**Type:** Unit Test
**Target:** `MainConversationAgent.ExtractAdditionalBookingFields()`

```csharp
[Theory]
[InlineData("arroz del señoret", "del señoret")]
[InlineData("arroz negro", "negro")]
[InlineData("arroz a banda", "a banda")]
[InlineData("paella valenciana", null)] // Not matching "arroz X" pattern
public void ExtractAdditionalBookingFields_ExtractsRiceType(string responseText, string? expectedRice)
{
    // Arrange
    var response = $"BOOKING_REQUEST|Juan|34666123456|30/11/2025|4|14:00\nQuieren {responseText}.";
    var booking = new BookingData { Name = "Juan", Phone = "34666123456", Date = "30/11/2025", People = 4, Time = "14:00" };

    // Act
    var result = ExtractAdditionalBookingFields(response, booking);

    // Assert
    result.ArrozType.Should().Be(expectedRice);
}
```

**Expected:** Rice type extracted from "arroz X" pattern

---

### Step 36: MainAgent_ExtractsRiceServings_FromResponse
**Type:** Unit Test
**Target:** `MainConversationAgent.ExtractAdditionalBookingFields()`

```csharp
[Theory]
[InlineData("3 raciones de arroz", 3)]
[InlineData("1 ración de arroz", 1)]
[InlineData("5 raciones", 5)]
[InlineData("arroz sin especificar raciones", null)]
public void ExtractAdditionalBookingFields_ExtractsRiceServings(string responseText, int? expectedServings)
{
    // Arrange
    var response = $"Perfecto, {responseText}.\nBOOKING_REQUEST|Juan|34666123456|30/11/2025|4|14:00";
    var booking = new BookingData { Name = "Juan" };

    // Act
    var result = ExtractAdditionalBookingFields(response, booking);

    // Assert
    result.ArrozServings.Should().Be(expectedServings);
}
```

**Expected:** Servings count extracted from "X raciones" pattern

---

### Step 37: MainAgent_ExtractsHighChairs_FromResponse
**Type:** Unit Test
**Target:** `MainConversationAgent.ExtractAdditionalBookingFields()`

```csharp
[Theory]
[InlineData("2 tronas", 2)]
[InlineData("1 trona", 1)]
[InlineData("sin tronas", 0)]
public void ExtractAdditionalBookingFields_ExtractsHighChairs(string responseText, int expectedChairs)
{
    // Arrange
    var response = $"Con {responseText}.\nBOOKING_REQUEST|Juan|34666123456|30/11/2025|4|14:00";
    var booking = new BookingData { Name = "Juan" };

    // Act
    var result = ExtractAdditionalBookingFields(response, booking);

    // Assert
    result.HighChairs.Should().Be(expectedChairs);
}
```

**Expected:** High chair count extracted from "X tronas" pattern

---

### Step 38: MainAgent_ExtractsBabyStrollers_FromResponse
**Type:** Unit Test
**Target:** `MainConversationAgent.ExtractAdditionalBookingFields()`

```csharp
[Theory]
[InlineData("2 carritos", 2)]
[InlineData("1 carrito", 1)]
[InlineData("sin carrito", 0)]
public void ExtractAdditionalBookingFields_ExtractsBabyStrollers(string responseText, int expectedStrollers)
{
    // Arrange
    var response = $"Con {responseText}.\nBOOKING_REQUEST|Juan|34666123456|30/11/2025|4|14:00";
    var booking = new BookingData { Name = "Juan" };

    // Act
    var result = ExtractAdditionalBookingFields(response, booking);

    // Assert
    result.BabyStrollers.Should().Be(expectedStrollers);
}
```

**Expected:** Baby stroller count extracted from "X carritos" pattern

---

### Step 39: MainAgent_DetectsUrls_SetsInteractiveIntent
**Type:** Unit Test
**Target:** `MainConversationAgent.ParseAiResponse()`

```csharp
[Theory]
[InlineData("Puedes ver nuestro menú en https://example.com/menu", true, 1)]
[InlineData("Visita http://maps.google.com y https://example.com", true, 2)]
[InlineData("No hay ninguna URL aquí", false, 0)]
public void ParseAiResponse_WithUrls_SetsMetadata(string response, bool hasUrls, int urlCount)
{
    // Act
    var result = ParseAiResponse(response);

    // Assert
    if (hasUrls)
    {
        result.Metadata.Should().ContainKey("hasUrls");
        ((bool)result.Metadata["hasUrls"]).Should().BeTrue();
        ((List<string>)result.Metadata["urls"]).Should().HaveCount(urlCount);
    }
    else
    {
        result.Metadata?.ContainsKey("hasUrls").Should().NotBe(true);
    }
}
```

**Expected:** URLs detected and stored in metadata

---

## Response Cleaning Tests (Steps 40-44)

### Step 40: MainAgent_CleansMarkdown_EscapedChars
**Type:** Unit Test
**Target:** `MainConversationAgent.CleanForWhatsApp()`

```csharp
[Theory]
[InlineData(@"Reserva\_confirmada", "Reserva_confirmada")]
[InlineData(@"Opción\|A", "Opción|A")]
[InlineData(@"Texto\*bold\*", "Texto*bold*")]
public void CleanForWhatsApp_RemovesEscapeChars(string input, string expected)
{
    // Act
    var result = CleanForWhatsApp(input);

    // Assert
    result.Should().Be(expected);
}
```

**Expected:** Backslash escapes removed: `\_` → `_`, `\|` → `|`

---

### Step 41: MainAgent_CleansMarkdown_DoubleBoldToSingle
**Type:** Unit Test
**Target:** `MainConversationAgent.CleanForWhatsApp()`

```csharp
[Theory]
[InlineData("**Fecha:** 30/11", "*Fecha:* 30/11")]
[InlineData("**Importante**", "*Importante*")]
[InlineData("Normal **bold** text", "Normal *bold* text")]
public void CleanForWhatsApp_ConvertsDoubleBoldToSingle(string input, string expected)
{
    // Act
    var result = CleanForWhatsApp(input);

    // Assert
    result.Should().Be(expected);
}
```

**Expected:** Markdown `**text**` converted to WhatsApp `*text*`

---

### Step 42: MainAgent_CleansMarkdown_MultipleNewlines
**Type:** Unit Test
**Target:** `MainConversationAgent.CleanForWhatsApp()`

```csharp
[Fact]
public void CleanForWhatsApp_ReducesMultipleNewlines()
{
    // Arrange
    var input = "Line 1\n\n\n\nLine 2\n\n\nLine 3";

    // Act
    var result = CleanForWhatsApp(input);

    // Assert
    result.Should().NotContain("\n\n\n");
    result.Split("\n\n").Length.Should().BeLessOrEqualTo(3);
}
```

**Expected:** 3+ consecutive newlines reduced to 2

---

### Step 43: MainAgent_CleansMarkdown_WhitespaceLines
**Type:** Unit Test
**Target:** `MainConversationAgent.CleanForWhatsApp()`

```csharp
[Fact]
public void CleanForWhatsApp_RemovesWhitespaceOnlyLines()
{
    // Arrange
    var input = "Line 1\n   \nLine 2\n\t\nLine 3";

    // Act
    var result = CleanForWhatsApp(input);

    // Assert
    result.Should().Be("Line 1\nLine 2\nLine 3");
}
```

**Expected:** Lines with only whitespace removed

---

### Step 44: MainAgent_EmptyCleanedResponse_DefaultMessage
**Type:** Unit Test
**Target:** `MainConversationAgent.ParseAiResponse()`

```csharp
[Theory]
[InlineData("")]
[InlineData("   ")]
[InlineData("\n\n")]
[InlineData("BOOKING_REQUEST|Juan|34666123456|30/11/2025|4|14:00")] // Only command
public void ParseAiResponse_EmptyAfterCleaning_ReturnsDefaultMessage(string aiResponse)
{
    // Act
    var result = ParseAiResponse(aiResponse);

    // Assert
    result.AiResponse.Should().NotBeNullOrWhiteSpace();
    result.AiResponse.Should().Contain("Disculpa");
}
```

**Expected:** Default message returned when response is empty after cleaning

---

## Response Building Tests (Steps 45-50)

### Step 45: BookingHandler_BuildsConfirmation_IncludesEmojis
**Type:** Unit Test
**Target:** `BookingHandler.BuildConfirmationMessage()`

```csharp
[Fact]
public void BuildConfirmationMessage_IncludesEmojis()
{
    // Arrange
    var booking = CreateValidBooking();

    // Act
    var result = BuildConfirmationMessage(booking);

    // Assert
    result.Should().Contain("✅");
    result.Should().Contain("📅");
    result.Should().Contain("🕐");
    result.Should().Contain("👥");
    result.Should().Contain("👤");
}
```

**Expected:** Confirmation includes emoji prefixes for each field

---

### Step 46: BookingHandler_BuildsConfirmation_IncludesAllData
**Type:** Unit Test
**Target:** `BookingHandler.BuildConfirmationMessage()`

```csharp
[Fact]
public void BuildConfirmationMessage_IncludesAllBookingData()
{
    // Arrange
    var booking = new BookingData
    {
        Name = "Juan García",
        Date = "30/11/2025",
        Time = "14:00",
        People = 4
    };

    // Act
    var result = BuildConfirmationMessage(booking);

    // Assert
    result.Should().Contain("Juan García");
    result.Should().Contain("30/11/2025");
    result.Should().Contain("14:00");
    result.Should().Contain("4");
    result.Should().Contain("*Fecha:*");
    result.Should().Contain("*Hora:*");
    result.Should().Contain("*Personas:*");
    result.Should().Contain("*Nombre:*");
}
```

**Expected:** All booking fields included in confirmation message

---

### Step 47: BookingHandler_BuildsConfirmation_OptionalRice
**Type:** Unit Test
**Target:** `BookingHandler.BuildConfirmationMessage()`

```csharp
[Fact]
public void BuildConfirmationMessage_WithRice_IncludesRiceInfo()
{
    // Arrange
    var booking = new BookingData
    {
        Name = "Juan",
        Date = "30/11/2025",
        Time = "14:00",
        People = 4,
        ArrozType = "del señoret",
        ArrozServings = 3
    };

    // Act
    var result = BuildConfirmationMessage(booking);

    // Assert
    result.Should().Contain("🍚");
    result.Should().Contain("*Arroz:*");
    result.Should().Contain("del señoret");
    result.Should().Contain("*Raciones:*");
    result.Should().Contain("3");
}

[Fact]
public void BuildConfirmationMessage_WithoutRice_OmitsRiceInfo()
{
    // Arrange
    var booking = new BookingData
    {
        Name = "Juan",
        Date = "30/11/2025",
        Time = "14:00",
        People = 4,
        ArrozType = null
    };

    // Act
    var result = BuildConfirmationMessage(booking);

    // Assert
    result.Should().NotContain("🍚");
    result.Should().NotContain("*Arroz:*");
}
```

**Expected:** Rice info only shown when ArrozType is not null

---

### Step 48: BookingHandler_BuildsConfirmation_OptionalHighChairs
**Type:** Unit Test
**Target:** `BookingHandler.BuildConfirmationMessage()`

```csharp
[Theory]
[InlineData(2, true)]
[InlineData(1, true)]
[InlineData(0, false)]
public void BuildConfirmationMessage_HighChairs_ShownWhenPositive(int chairs, bool shouldShow)
{
    // Arrange
    var booking = CreateValidBooking() with { HighChairs = chairs };

    // Act
    var result = BuildConfirmationMessage(booking);

    // Assert
    if (shouldShow)
    {
        result.Should().Contain("🪑");
        result.Should().Contain("*Tronas:*");
        result.Should().Contain(chairs.ToString());
    }
    else
    {
        result.Should().NotContain("🪑");
        result.Should().NotContain("*Tronas:*");
    }
}
```

**Expected:** High chairs shown only when count > 0

---

### Step 49: ModificationHandler_BuildsOptions_SingleBooking
**Type:** Unit Test
**Target:** `ModificationHandler.StartModificationFlowAsync()`

```csharp
[Fact]
public async Task StartModificationFlow_SingleBooking_ShowsModificationOptions()
{
    // Arrange
    var handler = CreateHandlerWithBookings(new[]
    {
        new BookingInfo { Id = "1", Date = "30/11/2025", Time = "14:00", People = 4 }
    });

    // Act
    var result = await handler.StartModificationFlowAsync("34666123456", CancellationToken.None);

    // Assert
    result.AiResponse.Should().Contain("30/11/2025");
    result.AiResponse.Should().Contain("14:00");
    result.AiResponse.Should().Contain("4 personas");
    result.AiResponse.Should().Contain("1️⃣ Fecha");
    result.AiResponse.Should().Contain("2️⃣ Hora");
    result.AiResponse.Should().Contain("3️⃣ Número de personas");
    result.AiResponse.Should().Contain("4️⃣ Tipo de arroz");
    result.Metadata["modificationState"].Should().Be("selecting_field");
}
```

**Expected:** Single booking shows modification options with numbered list

---

### Step 50: ModificationHandler_BuildsOptions_MultipleBookings
**Type:** Unit Test
**Target:** `ModificationHandler.StartModificationFlowAsync()`

```csharp
[Fact]
public async Task StartModificationFlow_MultipleBookings_ListsAllBookings()
{
    // Arrange
    var handler = CreateHandlerWithBookings(new[]
    {
        new BookingInfo { Id = "1", Date = "30/11/2025", Time = "14:00", People = 4 },
        new BookingInfo { Id = "2", Date = "07/12/2025", Time = "15:00", People = 6 }
    });

    // Act
    var result = await handler.StartModificationFlowAsync("34666123456", CancellationToken.None);

    // Assert
    result.AiResponse.Should().Contain("varias reservas");
    result.AiResponse.Should().Contain("*1.* 30/11/2025");
    result.AiResponse.Should().Contain("*2.* 07/12/2025");
    result.AiResponse.Should().Contain("¿Cuál quieres modificar?");
    result.Metadata["modificationState"].Should().Be("selecting_booking");
}
```

**Expected:** Multiple bookings listed with numbers for selection

---

## Test Helpers

```csharp
public class MessageHandlingTestBase
{
    protected AgentResponse ParseAiResponse(string aiResponse)
    {
        // Create MainConversationAgent and call ParseAiResponse via reflection
        // or extract to testable method
        var agent = CreateTestAgent();
        var method = typeof(MainConversationAgent).GetMethod(
            "ParseAiResponse", BindingFlags.NonPublic | BindingFlags.Instance);

        return (AgentResponse)method.Invoke(agent, new object[]
        {
            aiResponse,
            new WhatsAppMessage { PushName = "Test", SenderNumber = "34666123456" },
            null
        });
    }

    protected BookingData ExtractAdditionalBookingFields(string response, BookingData booking)
    {
        var agent = CreateTestAgent();
        var method = typeof(MainConversationAgent).GetMethod(
            "ExtractAdditionalBookingFields", BindingFlags.NonPublic | BindingFlags.Instance);

        return (BookingData)method.Invoke(agent, new object[] { response, booking });
    }

    protected string CleanForWhatsApp(string text)
    {
        var agent = CreateTestAgent();
        var method = typeof(MainConversationAgent).GetMethod(
            "CleanForWhatsApp", BindingFlags.NonPublic | BindingFlags.Instance);

        return (string)method.Invoke(agent, new object[] { text });
    }

    protected string BuildConfirmationMessage(BookingData booking)
    {
        var handler = new BookingHandler(
            Mock.Of<ILogger<BookingHandler>>(),
            Mock.Of<IConfiguration>());

        var method = typeof(BookingHandler).GetMethod(
            "BuildConfirmationMessage", BindingFlags.NonPublic | BindingFlags.Instance);

        return (string)method.Invoke(handler, new object[] { booking });
    }

    protected BookingData CreateValidBooking() => new()
    {
        Name = "Juan García",
        Phone = "34666123456",
        Date = "30/11/2025",
        Time = "14:00",
        People = 4
    };
}
```
