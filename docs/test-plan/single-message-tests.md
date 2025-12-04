# Single Message Logic Tests (Steps 76-110)

## Overview
These tests verify that single user messages are understood and responded to appropriately. Each test sends one message and validates the response.

**Test Type:** Conversation Logic (Integration)
**Messages per test:** 1

---

## Greeting Tests (Steps 76-79)

### Step 76: Greeting_Hola_RespondsWelcome
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "greeting-hola",
  "messages": [
    {
      "user": "Hola",
      "expect": ["hola", "ayudar"],
      "notExpect": ["reserva", "fecha"]
    }
  ]
}
```

```csharp
[Fact]
public async Task Greeting_Hola_RespondsWithWelcome()
{
    // Arrange
    var simulator = CreateSimulator();

    // Act
    await simulator.UserSays("Hola");

    // Assert
    simulator.ShouldRespond("hola", "ayudar");
    simulator.ShouldNotMention("reserva", "fecha", "hora");
}
```

**Expected:** Friendly greeting, offers help, doesn't immediately ask booking questions

---

### Step 77: Greeting_BuenosDias_RespondsAppropriately
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "greeting-buenos-dias",
  "messages": [
    {
      "user": "Buenos días",
      "expect": ["buenos días", "ayudar"],
      "notExpect": ["reserva"]
    }
  ]
}
```

```csharp
[Fact]
public async Task Greeting_BuenosDias_RespondsAppropriately()
{
    await simulator.UserSays("Buenos días");
    simulator.ShouldRespond("buenos días");
}
```

**Expected:** Mirrors time-appropriate greeting

---

### Step 78: Greeting_BuenasTardes_RespondsAppropriately
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "greeting-buenas-tardes",
  "messages": [
    {
      "user": "Buenas tardes!",
      "expect": ["buenas", "ayudar"]
    }
  ]
}
```

**Expected:** Time-appropriate response

---

### Step 79: Greeting_Ey_RespondsNaturally
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "greeting-informal",
  "messages": [
    {
      "user": "Ey",
      "expect": ["hola", "ayudar"]
    }
  ]
}
```

**Expected:** Natural response to informal greeting

---

## Booking Intent Tests (Steps 80-85)

### Step 80: Intent_QuieroReservar_StartsBookingFlow
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "intent-quiero-reservar",
  "messages": [
    {
      "user": "Quiero reservar",
      "expect": ["cuándo", "fecha", "día"],
      "responseType": "question"
    }
  ]
}
```

```csharp
[Fact]
public async Task Intent_QuieroReservar_StartsBookingFlow()
{
    await simulator.UserSays("Quiero reservar");

    simulator.ShouldRespond("cuándo", "día", "fecha");
    simulator.ResponseLengthShouldBe(200); // Short response
}
```

**Expected:** Asks for date (first missing piece of info)

---

### Step 81: Intent_HacerReserva_StartsBookingFlow
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "intent-hacer-reserva",
  "messages": [
    {
      "user": "Quiero hacer una reserva",
      "expect": ["día", "cuándo", "fecha"]
    }
  ]
}
```

**Expected:** Asks for booking date

---

### Step 82: Intent_TeneisMesa_StartsBookingFlow
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "intent-teneis-mesa",
  "messages": [
    {
      "user": "¿Tenéis mesa para el sábado?",
      "expect": ["personas", "cuántas", "hora"]
    }
  ]
}
```

**Expected:** Date understood, asks for next piece (people or time)

---

### Step 83: Intent_CancelarReserva_StartsCancellationFlow
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "intent-cancelar",
  "messages": [
    {
      "user": "Quiero cancelar mi reserva",
      "expect": ["datos", "reserva", "nombre", "fecha"]
    }
  ]
}
```

```csharp
[Fact]
public async Task Intent_CancelarReserva_StartsCancellationFlow()
{
    await simulator.UserSays("Quiero cancelar mi reserva");

    simulator.ShouldRespond("reserva", "datos");
    // Should ask for booking details
}
```

**Expected:** Asks for reservation details to find booking

---

### Step 84: Intent_ModificarReserva_StartsModificationFlow
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "intent-modificar",
  "messages": [
    {
      "user": "Quiero modificar mi reserva",
      "expect": ["buscar", "reserva", "modificar"]
    }
  ]
}
```

**Expected:** Indicates it will search for the booking

---

### Step 85: Intent_CambiarReserva_StartsModificationFlow
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "intent-cambiar",
  "messages": [
    {
      "user": "Necesito cambiar mi reserva",
      "expect": ["buscar", "reserva"]
    }
  ]
}
```

**Expected:** Alternative phrasing triggers modification flow

---

## Information Questions (Steps 86-93)

### Step 86: Question_Horarios_RespondsWithSchedule
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "question-horarios",
  "messages": [
    {
      "user": "¿Cuál es vuestro horario?",
      "expect": ["jueves", "viernes", "sábado", "domingo"],
      "notExpect": ["lunes", "martes", "miércoles"]
    }
  ]
}
```

```csharp
[Fact]
public async Task Question_Horarios_RespondsWithSchedule()
{
    await simulator.UserSays("¿Cuál es vuestro horario?");

    simulator.ShouldRespond("sábado", "domingo");
    simulator.ShouldNotMention("lunes", "martes", "miércoles");
}
```

**Expected:** Lists open days (Thu-Sun), mentions closed days

---

### Step 87: Question_Direccion_RespondsWithLocation
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "question-direccion",
  "messages": [
    {
      "user": "¿Dónde estáis?",
      "expect": ["alquería", "villa carmen", "valencia", "dirección"]
    }
  ]
}
```

**Expected:** Provides address/location info

---

### Step 88: Question_Telefono_RespondsWithPhone
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "question-telefono",
  "messages": [
    {
      "user": "¿Cuál es vuestro teléfono?",
      "expect": ["638", "857", "294"]
    }
  ]
}
```

**Expected:** Provides phone number +34 638 857 294

---

### Step 89: Question_MenuArroces_ListsRiceTypes
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "question-arroces",
  "messages": [
    {
      "user": "¿Qué arroces tenéis?",
      "expect": ["paella", "arroz", "señoret", "negro"]
    }
  ]
}
```

**Expected:** Lists available rice types

---

### Step 90: Question_Precios_RespondsWithPricing
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "question-precios",
  "messages": [
    {
      "user": "¿Cuánto cuesta el menú?",
      "expect": ["€", "euro", "precio"]
    }
  ]
}
```

**Expected:** Provides pricing info or explains pricing structure

---

### Step 91: Question_Parking_RespondsWithInfo
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "question-parking",
  "messages": [
    {
      "user": "¿Tenéis parking?",
      "expect": ["aparcar", "parking", "coche"]
    }
  ]
}
```

**Expected:** Info about parking availability

---

### Step 92: Question_GruposGrandes_RespondsWithPolicy
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "question-grupos",
  "messages": [
    {
      "user": "¿Aceptáis grupos grandes?",
      "expect": ["personas", "grupo", "llamar", "20"]
    }
  ]
}
```

**Expected:** Explains group policy (>20 requires phone call)

---

### Step 93: Question_Tronas_RespondsWithAvailability
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "question-tronas",
  "messages": [
    {
      "user": "¿Tenéis tronas para bebés?",
      "expect": ["sí", "trona", "disponible"]
    }
  ]
}
```

**Expected:** Confirms high chair availability

---

## Date Parsing Tests (Steps 94-99)

### Step 94: Date_ElSabado_ParsesNextSaturday
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "date-sabado",
  "messages": [
    {
      "user": "Quiero reservar para el sábado",
      "expect": ["sábado", "personas", "cuántas"]
    }
  ]
}
```

```csharp
[Fact]
public async Task Date_ElSabado_ParsesNextSaturday()
{
    await simulator.UserSays("Quiero reservar para el sábado");

    simulator.ShouldRespond("personas");
    // Date should be captured, now asking for people
}
```

**Expected:** "el sábado" parsed as next Saturday, asks for next info

---

### Step 95: Date_ElDomingo_ParsesNextSunday
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "date-domingo",
  "messages": [
    {
      "user": "Para el domingo",
      "expect": ["domingo", "personas", "hora"]
    }
  ]
}
```

**Expected:** "el domingo" parsed as next Sunday

---

### Step 96: Date_ProximoFinDeSemana_ParsesCorrectly
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "date-fin-de-semana",
  "messages": [
    {
      "user": "Quiero reservar para el próximo fin de semana",
      "expect": ["sábado", "domingo", "qué día"]
    }
  ]
}
```

**Expected:** Asks to clarify Saturday or Sunday

---

### Step 97: Date_30DeNoviembre_ParsesExplicitDate
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "date-explicit-full",
  "messages": [
    {
      "user": "Para el 30 de noviembre",
      "expect": ["30", "noviembre", "personas"]
    }
  ]
}
```

**Expected:** Explicit date parsed, asks for next info

---

### Step 98: Date_30/11_ParsesShortFormat
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "date-short-format",
  "messages": [
    {
      "user": "Reserva para el 30/11",
      "expect": ["personas", "hora"]
    }
  ]
}
```

**Expected:** dd/MM format parsed correctly

---

### Step 99: Date_Manana_ParsesTomorrow
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "date-manana",
  "messages": [
    {
      "user": "Quiero reservar para mañana",
      "expect": ["mañana", "personas"]
    }
  ]
}
```

**Expected:** "mañana" understood (may trigger same-day rejection depending on restaurant hours)

---

## Time Parsing Tests (Steps 100-102)

### Step 100: Time_ALas14_ParsesTime
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "time-14",
  "messages": [
    {
      "user": "Reserva para el sábado a las 14:00",
      "expect": ["14:00", "personas"]
    }
  ]
}
```

```csharp
[Fact]
public async Task Time_ALas14_ParsesTime()
{
    await simulator.UserSays("Reserva para el sábado a las 14:00");

    // Both date and time captured, asks for people
    simulator.ShouldRespond("personas");
}
```

**Expected:** Date and time parsed together, asks for people

---

### Step 101: Time_ALasDos_ParsesSpanishNumber
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "time-spanish-number",
  "messages": [
    {
      "user": "Para las dos de la tarde",
      "expect": ["14", "personas"]
    }
  ]
}
```

**Expected:** Spanish number "dos de la tarde" = 14:00

---

### Step 102: Time_1430_ParsesWithoutColon
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "time-no-colon",
  "messages": [
    {
      "user": "A las 1430",
      "expect": ["14:30", "hora"]
    }
  ]
}
```

**Expected:** Time without colon parsed (1430 = 14:30)

---

## People Count Tests (Steps 103-105)

### Step 103: People_4Personas_ParsesCount
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "people-4",
  "messages": [
    {
      "user": "Para 4 personas",
      "expect": ["4", "cuándo", "fecha"]
    }
  ]
}
```

```csharp
[Fact]
public async Task People_4Personas_ParsesCount()
{
    await simulator.UserSays("Para 4 personas");

    simulator.ShouldRespond("fecha", "día", "cuándo");
    // People captured, asks for date
}
```

**Expected:** People count captured, asks for next info

---

### Step 104: People_SomosCuatro_ParsesSpanish
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "people-spanish",
  "messages": [
    {
      "user": "Somos cuatro",
      "expect": ["4", "cuándo"]
    }
  ]
}
```

**Expected:** Spanish number "cuatro" parsed as 4

---

### Step 105: People_ParaSeis_ParsesCount
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "people-para-seis",
  "messages": [
    {
      "user": "Para seis por favor",
      "expect": ["6", "día"]
    }
  ]
}
```

**Expected:** "para seis" parsed as 6 people

---

## Rice Validation Tests (Steps 106-110)

### Step 106: Rice_ArrozDelSenoret_Validates
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "rice-senoret",
  "messages": [
    {
      "user": "Queremos arroz del señoret",
      "expect": ["señoret", "disponible", "raciones"]
    }
  ]
}
```

```csharp
[Fact]
public async Task Rice_ArrozDelSenoret_Validates()
{
    // Context: booking flow already started
    await simulator.UserSays("Queremos arroz del señoret");

    simulator.ShouldRespond("señoret", "disponible", "raciones");
}
```

**Expected:** Rice type validated, asks for servings count

---

### Step 107: Rice_PaellaValenciana_Validates
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "rice-paella",
  "messages": [
    {
      "user": "Paella valenciana",
      "expect": ["paella", "valenciana", "raciones"]
    }
  ]
}
```

**Expected:** Paella recognized and validated

---

### Step 108: Rice_ArrozNegro_Validates
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "rice-negro",
  "messages": [
    {
      "user": "Arroz negro",
      "expect": ["negro", "disponible"]
    }
  ]
}
```

**Expected:** Arroz negro validated

---

### Step 109: Rice_NoQuierenArroz_AcceptsNo
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "rice-no",
  "messages": [
    {
      "user": "No queremos arroz",
      "expect": ["perfecto", "sin arroz", "vale"]
    }
  ]
}
```

```csharp
[Fact]
public async Task Rice_NoQuierenArroz_AcceptsNo()
{
    // Context: after being asked about rice
    await simulator.UserSays("No queremos arroz");

    simulator.ShouldRespond("perfecto", "sin arroz");
    // Should proceed without asking for servings
}
```

**Expected:** "no" accepted, moves to confirmation

---

### Step 110: Rice_UnknownType_AsksForClarification
**Type:** Conversation Test
**Messages:** 1

```json
{
  "name": "rice-unknown",
  "messages": [
    {
      "user": "Arroz con pollo",
      "expect": ["no tenemos", "disponible", "carta", "menú"]
    }
  ]
}
```

**Expected:** Unknown rice type prompts clarification/menu

---

## Test Infrastructure

```csharp
public class SingleMessageTestBase
{
    protected ConversationSimulator CreateSimulator()
    {
        var services = new ServiceCollection();
        ConfigureTestServices(services);
        var provider = services.BuildServiceProvider();

        return new ConversationSimulator(
            provider.GetRequiredService<IBotService>(),
            "test-user-" + Guid.NewGuid().ToString("N")[..8]);
    }

    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
        // Mock AI service with predictable responses
        var aiMock = new Mock<IGeminiService>();
        aiMock
            .Setup(x => x.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns((string system, string user, List<ChatMessage> history, CancellationToken ct) =>
                Task.FromResult(GenerateTestResponse(system, user)));

        services.AddSingleton(aiMock.Object);
        // ... other service registrations
    }

    private string GenerateTestResponse(string system, string user)
    {
        // Simple rule-based response generator for testing
        // In production tests, use actual AI or more sophisticated mocks
        var lower = user.ToLower();

        if (lower.Contains("hola") || lower.Contains("buenos"))
            return "¡Hola! ¿En qué puedo ayudarte?";

        if (lower.Contains("reservar"))
            return "¡Perfecto! ¿Para qué día quieres la reserva?";

        // ... more patterns

        return "Entendido. ¿Hay algo más en lo que pueda ayudarte?";
    }
}
```
