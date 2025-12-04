# Edge Cases & Error Handling Tests (Steps 211-250)

## Overview
These tests verify proper handling of edge cases, policy violations, invalid inputs, and error scenarios.

**Test Type:** Conversation Logic (Integration)
**Messages per test:** 4

---

## Same-Day Booking Rejection (Steps 211-214)

### Step 211-214: Edge_SameDayBooking_Rejected_4Messages
**Type:** Edge Case Test
**Messages:** 4

```json
{
  "name": "edge-same-day-rejected",
  "description": "Same-day booking is rejected with phone number",
  "messages": [
    {
      "turn": 1,
      "user": "Quiero reservar para hoy",
      "expect": ["no aceptamos", "mismo día", "llámanos", "638", "857", "294"]
    },
    {
      "turn": 2,
      "user": "¿Y para mañana?",
      "expect": ["personas", "hora"],
      "notExpect": ["no aceptamos"]
    }
  ]
}
```

```csharp
[Fact]
public async Task Edge_SameDayBooking_Rejected_4Messages()
{
    var simulator = CreateSimulator(currentDate: DateTime.Today);

    await simulator.UserSays("Quiero reservar para hoy");

    simulator.ShouldRespond("no aceptamos", "mismo día");
    simulator.ShouldRespond("llámanos", "638 857 294");

    // Should allow booking for tomorrow
    await simulator.UserSays("¿Y para mañana?");
    simulator.ShouldNotMention("no aceptamos");
}
```

**Policy:** No same-day bookings allowed. User must call.

---

## Closed Days Tests (Steps 215-226)

### Step 215-218: Edge_ClosedDay_Monday_4Messages
**Type:** Edge Case Test
**Messages:** 4

```json
{
  "name": "edge-closed-monday",
  "description": "Booking for Monday (closed day) rejected",
  "messages": [
    {
      "turn": 1,
      "user": "Quiero reservar para el lunes",
      "expect": ["cerrados", "lunes", "abrimos"]
    },
    {
      "turn": 2,
      "user": "¿Qué días abrís?",
      "expect": ["jueves", "viernes", "sábado", "domingo"]
    }
  ]
}
```

```csharp
[Fact]
public async Task Edge_ClosedDay_Monday_4Messages()
{
    var simulator = CreateSimulator();

    await simulator.UserSays("Quiero reservar para el lunes");

    simulator.ShouldRespond("cerrados");
    simulator.ShouldNotMention("personas", "hora"); // Don't continue booking flow
}
```

**Schedule:** Open Thu-Sun only

---

### Step 219-222: Edge_ClosedDay_Tuesday_4Messages
**Type:** Edge Case Test
**Messages:** 4

```json
{
  "name": "edge-closed-tuesday",
  "description": "Booking for Tuesday (closed day) rejected",
  "messages": [
    {
      "turn": 1,
      "user": "Mesa para el martes por favor",
      "expect": ["cerrados", "martes"]
    }
  ]
}
```

---

### Step 223-226: Edge_ClosedDay_Wednesday_4Messages
**Type:** Edge Case Test
**Messages:** 4

```json
{
  "name": "edge-closed-wednesday",
  "description": "Booking for Wednesday (closed day) rejected",
  "messages": [
    {
      "turn": 1,
      "user": "Reserva para miércoles 4 personas",
      "expect": ["cerrados", "miércoles"]
    }
  ]
}
```

---

## Outside Hours Tests (Steps 227-234)

### Step 227-230: Edge_OutsideHours_TooEarly_4Messages
**Type:** Edge Case Test
**Messages:** 4

```json
{
  "name": "edge-too-early",
  "description": "Booking for before opening time rejected",
  "messages": [
    {
      "turn": 1,
      "user": "Reserva sábado a las 11:00 para 4",
      "expect": ["abrimos", "13:30", "hora"]
    },
    {
      "turn": 2,
      "user": "A las 13:30 entonces",
      "expect": ["arroz"],
      "notExpect": ["abrimos"]
    }
  ]
}
```

```csharp
[Fact]
public async Task Edge_OutsideHours_TooEarly_4Messages()
{
    var simulator = CreateSimulator();

    await simulator.UserSays("Reserva sábado a las 11:00 para 4");

    simulator.ShouldRespond("abrimos", "13:30");
    simulator.ShouldNotMention("confirmada"); // Can't proceed

    await simulator.UserSays("A las 13:30 entonces");
    simulator.ShouldRespond("arroz"); // Now continues
}
```

**Schedule:** Opens at 13:30

---

### Step 231-234: Edge_OutsideHours_TooLate_4Messages
**Type:** Edge Case Test
**Messages:** 4

```json
{
  "name": "edge-too-late",
  "description": "Booking for after closing time rejected",
  "messages": [
    {
      "turn": 1,
      "user": "Mesa sábado 19:00 para 4",
      "expect": ["cerramos", "18:00", "otra hora"]
    },
    {
      "turn": 2,
      "user": "17:00",
      "expect": ["arroz"]
    }
  ]
}
```

**Schedule:** Closes 17:00-18:00 depending on day

---

## Large Group Tests (Steps 235-238)

### Step 235-238: Edge_LargeGroup_Over20_4Messages
**Type:** Edge Case Test
**Messages:** 4

```json
{
  "name": "edge-large-group",
  "description": "Groups over 20 require phone call",
  "messages": [
    {
      "turn": 1,
      "user": "Reserva para 25 personas el sábado",
      "expect": ["grupos", "20", "llamar", "638", "857", "294"]
    },
    {
      "turn": 2,
      "user": "En realidad somos solo 15",
      "expect": ["hora", "arroz"],
      "notExpect": ["llamar"]
    }
  ]
}
```

```csharp
[Fact]
public async Task Edge_LargeGroup_Over20_4Messages()
{
    var simulator = CreateSimulator();

    await simulator.UserSays("Reserva para 25 personas el sábado");

    simulator.ShouldRespond("20", "llamar");
    simulator.ShouldNotMention("confirmo"); // Can't proceed online

    // Smaller group continues normally
    await simulator.UserSays("En realidad somos solo 15");
    simulator.ShouldNotMention("llamar");
}
```

**Policy:** Groups > 20 must call directly

---

## Past Date Tests (Steps 239-242)

### Step 239-242: Edge_PastDate_Rejected_4Messages
**Type:** Edge Case Test
**Messages:** 4

```json
{
  "name": "edge-past-date",
  "description": "Booking for past date rejected",
  "messages": [
    {
      "turn": 1,
      "user": "Reserva para el 15 de octubre",
      "expect": ["pasada", "fecha", "futura"]
    },
    {
      "turn": 2,
      "user": "Perdón, el 15 de diciembre",
      "expect": ["personas", "hora"]
    }
  ]
}
```

```csharp
[Fact]
public async Task Edge_PastDate_Rejected_4Messages()
{
    // Set current date to November 2025
    var simulator = CreateSimulator(currentDate: new DateTime(2025, 11, 26));

    await simulator.UserSays("Reserva para el 15 de octubre");

    simulator.ShouldRespond("pasada", "fecha");
    simulator.ShouldNotMention("personas"); // Don't continue

    await simulator.UserSays("Perdón, el 15 de diciembre");
    simulator.ShouldRespond("personas"); // Now valid
}
```

---

## Ambiguous Input Tests (Steps 243-246)

### Step 243-246: Edge_AmbiguousInput_Clarifies_4Messages
**Type:** Edge Case Test
**Messages:** 4

```json
{
  "name": "edge-ambiguous-input",
  "description": "Ambiguous input prompts clarification",
  "messages": [
    {
      "turn": 1,
      "user": "Reserva fin de semana",
      "expect": ["sábado", "domingo", "qué día"]
    },
    {
      "turn": 2,
      "user": "El sábado",
      "expect": ["personas", "hora"]
    }
  ]
}
```

```csharp
[Fact]
public async Task Edge_AmbiguousInput_Clarifies_4Messages()
{
    var simulator = CreateSimulator();

    await simulator.UserSays("Reserva fin de semana");

    // Should ask for clarification
    simulator.ShouldRespond("sábado", "domingo");
    simulator.ShouldNotMention("confirmada");

    await simulator.UserSays("El sábado");
    simulator.ShouldRespond("personas");
}
```

---

## Mixed Language Tests (Steps 247-250)

### Step 247-250: Edge_MixedLanguage_Handles_4Messages
**Type:** Edge Case Test
**Messages:** 4

```json
{
  "name": "edge-mixed-language",
  "description": "Handles Spanish with occasional English/Valenciano",
  "messages": [
    {
      "turn": 1,
      "user": "Booking for Saturday please",
      "expect": ["sábado", "personas"]
    },
    {
      "turn": 2,
      "user": "Vull reservar per a 4 persones",
      "expect": ["personas", "4", "hora"]
    }
  ]
}
```

```csharp
[Fact]
public async Task Edge_MixedLanguage_Handles_4Messages()
{
    var simulator = CreateSimulator();

    // English
    await simulator.UserSays("Booking for Saturday please");
    simulator.ShouldRespond("personas"); // Understood and continues

    // Valenciano
    await simulator.UserSays("Vull reservar per a 4 persones");
    simulator.ShouldRespond("hora"); // Understood 4 people
}
```

---

## Additional Edge Cases

### Edge_SpecialCharacters_Handles
**Type:** Edge Case Test

```json
{
  "name": "edge-special-chars",
  "messages": [
    {
      "turn": 1,
      "user": "Reserva para García-López 4 personas",
      "expect": ["día", "cuándo"]
    }
  ]
}
```

---

### Edge_VeryLongMessage_Handles
**Type:** Edge Case Test

```json
{
  "name": "edge-long-message",
  "messages": [
    {
      "turn": 1,
      "user": "Hola buenas tardes, mira que quería hacer una reserva para mi familia, que somos muchos, como unas 8 personas o así, para el próximo sábado que viene, sobre las dos de la tarde más o menos, y queríamos saber si tenéis disponibilidad y también si podemos pedir paella porque es el cumpleaños de mi padre y le encanta la paella valenciana de toda la vida",
      "expect": ["sábado", "8 personas", "14:00", "paella"]
    }
  ]
}
```

---

### Edge_Typos_Understands
**Type:** Edge Case Test

```json
{
  "name": "edge-typos",
  "messages": [
    {
      "turn": 1,
      "user": "Quero reservr para el sabdo",
      "expect": ["sábado", "personas"]
    }
  ]
}
```

---

### Edge_EmptyAfterGreeting_Handles
**Type:** Edge Case Test

```json
{
  "name": "edge-empty-intent",
  "messages": [
    {
      "turn": 1,
      "user": ".",
      "expect": ["ayudar", "qué"]
    }
  ]
}
```

---

### Edge_NumbersAsWords_Understands
**Type:** Edge Case Test

```json
{
  "name": "edge-numbers-words",
  "messages": [
    {
      "turn": 1,
      "user": "Reserva para seis personas a las dos de la tarde",
      "expect": ["6", "14:00", "día"]
    }
  ]
}
```

---

### Edge_ImplicitConfirmation_Handles
**Type:** Edge Case Test

```json
{
  "name": "edge-implicit-confirm",
  "messages": [
    {
      "turn": 1,
      "user": "Reserva sábado 14h 4 personas",
      "expect": ["arroz"]
    },
    {
      "turn": 2,
      "user": "Sin arroz, todo correcto",
      "expect": ["confirmo", "reserva"],
      "notExpect": ["confirmada"]
    },
    {
      "turn": 3,
      "user": "Dale, perfecto",
      "expect": ["confirmada"]
    }
  ]
}
```

---

## Test Infrastructure for Edge Cases

```csharp
public class EdgeCaseTestBase : ConversationFlowTestBase
{
    protected ConversationSimulator CreateSimulator(DateTime? currentDate = null)
    {
        // Configure date for time-sensitive tests
        var simulator = base.CreateSimulator();

        if (currentDate.HasValue)
        {
            // Mock current date for testing
            simulator.SetCurrentDate(currentDate.Value);
        }

        return simulator;
    }

    protected override void ConfigureDefaultAiBehavior()
    {
        base.ConfigureDefaultAiBehavior();

        // Additional edge case handling
        AiServiceMock
            .Setup(x => x.GenerateAsync(
                It.IsAny<string>(),
                It.Is<string>(u => ContainsPastDate(u)),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Lo siento, esa fecha ya ha pasado. Por favor, elige una fecha futura.");
    }

    private bool ContainsPastDate(string userMessage)
    {
        // Logic to detect past dates in message
        return false;
    }
}

/// <summary>
/// Extension methods for edge case assertions
/// </summary>
public static class EdgeCaseAssertions
{
    public static ConversationSimulator ShouldRejectWithPhone(
        this ConversationSimulator simulator)
    {
        simulator.ShouldRespond("llámanos", "638", "857", "294");
        return simulator;
    }

    public static ConversationSimulator ShouldNotProceedWithBooking(
        this ConversationSimulator simulator)
    {
        simulator.ShouldNotMention("confirmada", "confirmo", "reserva creada");
        return simulator;
    }

    public static ConversationSimulator ShouldSuggestAlternative(
        this ConversationSimulator simulator,
        string alternativeType)
    {
        switch (alternativeType)
        {
            case "date":
                simulator.ShouldRespond("otro día", "otra fecha", "qué día");
                break;
            case "time":
                simulator.ShouldRespond("otra hora", "qué hora", "abrimos");
                break;
            case "phone":
                simulator.ShouldRespond("llamar", "638", "857", "294");
                break;
        }
        return simulator;
    }
}
```
