# Context Retention Tests (Steps 181-210)

## Overview
These tests verify that the bot correctly remembers information across conversation turns, never asks for data it already has, and properly handles corrections.

**Test Type:** Conversation Logic (Integration)
**Messages per test:** 5-6

---

## Memory Retention Tests (Steps 181-195)

### Step 181-185: Context_RemembersPreviousDate_5Messages
**Type:** Context Retention Test
**Messages:** 5

```json
{
  "name": "context-remembers-date",
  "description": "Bot remembers date across conversation turns",
  "messages": [
    {
      "turn": 1,
      "user": "Quiero reservar para el sábado",
      "expect": ["sábado", "personas", "cuántas"]
    },
    {
      "turn": 2,
      "user": "¿Qué arroces tenéis?",
      "expect": ["paella", "señoret"],
      "notExpect": ["fecha", "día", "cuándo"]
    },
    {
      "turn": 3,
      "user": "Vale, somos 4 a las 14:00",
      "expect": ["sábado", "arroz"],
      "notExpect": ["qué día", "cuándo"]
    },
    {
      "turn": 4,
      "user": "Sin arroz, confirma",
      "expect": ["sábado", "4 personas", "14:00", "confirmada"]
    }
  ]
}
```

```csharp
[Fact]
public async Task Context_RemembersPreviousDate_5Messages()
{
    var simulator = CreateSimulator();

    // Provide date
    await simulator.UserSays("Quiero reservar para el sábado");
    simulator.ShouldRespond("personas");

    // Interrupt with question
    await simulator.UserSays("¿Qué arroces tenéis?");
    simulator.ShouldNotMention("fecha", "día", "cuándo");

    // Continue - date should still be remembered
    await simulator.UserSays("Vale, somos 4 a las 14:00");
    simulator.ShouldRespond("sábado"); // Date still known
    simulator.ShouldNotMention("qué día"); // Doesn't ask again

    await simulator.UserSays("Sin arroz, confirma");
    simulator.ShouldRespond("sábado", "confirmada");
}
```

**Critical Assert:** After interruption, bot still remembers the date and doesn't ask again.

---

### Step 186-190: Context_RemembersPreviousTime_5Messages
**Type:** Context Retention Test
**Messages:** 5

```json
{
  "name": "context-remembers-time",
  "description": "Bot remembers time across conversation turns",
  "messages": [
    {
      "turn": 1,
      "user": "Reserva a las 14:30",
      "expect": ["día", "cuándo", "fecha"]
    },
    {
      "turn": 2,
      "user": "Para el domingo",
      "expect": ["personas"],
      "notExpect": ["hora"]
    },
    {
      "turn": 3,
      "user": "Somos 6",
      "expect": ["arroz"],
      "notExpect": ["hora"]
    },
    {
      "turn": 4,
      "user": "No",
      "expect": ["14:30", "confirmo"],
      "notExpect": ["hora"]
    }
  ]
}
```

**Critical Assert:** Time provided first is remembered throughout flow.

---

### Step 191-195: Context_RemembersPreviousPeople_5Messages
**Type:** Context Retention Test
**Messages:** 5

```json
{
  "name": "context-remembers-people",
  "description": "Bot remembers people count across conversation",
  "messages": [
    {
      "turn": 1,
      "user": "Somos 5 personas y queremos reservar",
      "expect": ["día", "cuándo", "fecha"]
    },
    {
      "turn": 2,
      "user": "El sábado a las 14:00",
      "expect": ["arroz"],
      "notExpect": ["personas", "cuántas"]
    },
    {
      "turn": 3,
      "user": "Sin arroz",
      "expect": ["5 personas", "sábado", "14:00", "confirmo"],
      "notExpect": ["cuántas personas"]
    }
  ]
}
```

**Critical Assert:** People count given first is remembered and included in summary.

---

## No Repeat Questions Tests (Steps 196-200)

### Step 196-200: Context_NeverAsksForKnownData_6Messages
**Type:** Context Retention Test
**Messages:** 6

```json
{
  "name": "context-no-repeat-questions",
  "description": "Bot never asks for information it already has",
  "messages": [
    {
      "turn": 1,
      "user": "Quiero reservar para el sábado 30 de noviembre a las 14:00 para 4 personas",
      "expect": ["arroz"],
      "notExpect": ["día", "hora", "personas", "cuándo", "cuántas"]
    },
    {
      "turn": 2,
      "user": "Arroz del señoret",
      "expect": ["raciones"],
      "notExpect": ["día", "hora", "personas", "fecha"]
    },
    {
      "turn": 3,
      "user": "3 raciones",
      "expect": ["confirmo"],
      "notExpect": ["día", "hora", "personas", "arroz"]
    },
    {
      "turn": 4,
      "user": "Sí",
      "expect": ["confirmada"],
      "notExpect": ["datos", "información"]
    }
  ]
}
```

```csharp
[Fact]
public async Task Context_NeverAsksForKnownData_6Messages()
{
    var simulator = CreateSimulator();

    // Provide all basic data at once
    await simulator.UserSays("Quiero reservar para el sábado 30 de noviembre a las 14:00 para 4 personas");

    // Should ONLY ask about rice - the only missing piece
    simulator.ShouldRespond("arroz");
    simulator.ShouldNotMention("día", "fecha", "cuándo");
    simulator.ShouldNotMention("hora", "qué hora");
    simulator.ShouldNotMention("personas", "cuántas");

    await simulator.UserSays("Arroz del señoret");
    simulator.ShouldRespond("raciones");
    simulator.ShouldNotMention("fecha", "hora", "personas");

    await simulator.UserSays("3 raciones");
    simulator.ShouldRespond("confirmo");
    simulator.ShouldNotMention("qué arroz", "cuántas personas");

    await simulator.UserSays("Sí");
    simulator.ShouldRespond("confirmada");
}
```

**Critical Assert:** Bot only asks for genuinely missing information, never repeats questions.

---

## Correction Handling Tests (Steps 201-205)

### Step 201-205: Context_HandlesCorrections_5Messages
**Type:** Context Retention Test
**Messages:** 5

```json
{
  "name": "context-handles-corrections",
  "description": "Bot updates data when user corrects themselves",
  "messages": [
    {
      "turn": 1,
      "user": "Reserva para 4 personas el sábado a las 14:00",
      "expect": ["arroz"]
    },
    {
      "turn": 2,
      "user": "Perdón, en verdad somos 6",
      "expect": ["6", "arroz"],
      "notExpect": ["4 personas"]
    },
    {
      "turn": 3,
      "user": "Sin arroz",
      "expect": ["6 personas", "confirmo"],
      "notExpect": ["4 personas"]
    },
    {
      "turn": 4,
      "user": "Confirma",
      "expect": ["6 personas", "confirmada"]
    }
  ]
}
```

```csharp
[Fact]
public async Task Context_HandlesCorrections_5Messages()
{
    var simulator = CreateSimulator();

    await simulator.UserSays("Reserva para 4 personas el sábado a las 14:00");
    simulator.ShouldRespond("arroz");

    // Correction
    await simulator.UserSays("Perdón, en verdad somos 6");
    simulator.ShouldRespond("6");
    simulator.ShouldNotMention("4 personas");

    await simulator.UserSays("Sin arroz");
    simulator.ShouldRespond("6 personas"); // Uses corrected value
    simulator.ShouldNotMention("4 personas");

    await simulator.UserSays("Confirma");
    simulator.ShouldRespond("6 personas", "confirmada");
}
```

**Critical Assert:** Corrected data replaces old data in all subsequent responses.

---

## Topic Change Persistence Tests (Steps 206-210)

### Step 206-210: Context_MaintainsAcrossTopicChange_6Messages
**Type:** Context Retention Test
**Messages:** 6

```json
{
  "name": "context-survives-topic-change",
  "description": "Booking data survives unrelated questions",
  "messages": [
    {
      "turn": 1,
      "user": "Reserva domingo 14:00 para 4",
      "expect": ["arroz"]
    },
    {
      "turn": 2,
      "user": "Por cierto, ¿tenéis parking?",
      "expect": ["parking", "aparcar"],
      "contextPreserved": ["fecha", "hora", "personas"]
    },
    {
      "turn": 3,
      "user": "Genial. Pues sin arroz",
      "expect": ["domingo", "14:00", "4 personas", "confirmo"]
    },
    {
      "turn": 4,
      "user": "¿Aceptáis tarjeta?",
      "expect": ["tarjeta", "pago"],
      "contextPreserved": ["fecha", "hora", "personas"]
    },
    {
      "turn": 5,
      "user": "Perfecto, confirma la reserva",
      "expect": ["confirmada", "domingo"]
    }
  ]
}
```

```csharp
[Fact]
public async Task Context_MaintainsAcrossTopicChange_6Messages()
{
    var simulator = CreateSimulator();

    await simulator.UserSays("Reserva domingo 14:00 para 4");
    simulator.ShouldRespond("arroz");

    // Unrelated question
    await simulator.UserSays("Por cierto, ¿tenéis parking?");
    simulator.ShouldRespond("parking");
    // Context should be preserved (internal check)

    // Continue booking
    await simulator.UserSays("Genial. Pues sin arroz");
    simulator.ShouldRespond("domingo", "14:00", "4 personas");
    simulator.ShouldNotMention("qué día", "hora", "cuántas personas");

    // Another unrelated question
    await simulator.UserSays("¿Aceptáis tarjeta?");
    simulator.ShouldRespond("tarjeta");

    // Final confirmation - all data preserved
    await simulator.UserSays("Perfecto, confirma la reserva");
    simulator.ShouldRespond("confirmada", "domingo");
}
```

**Critical Assert:** Booking context survives multiple topic changes.

---

## Advanced Context Tests

### Context_PartialDataAcrossMessages
**Type:** Context Retention Test
**Messages:** 5

```json
{
  "name": "context-partial-data",
  "description": "Data collected incrementally across messages is aggregated correctly",
  "messages": [
    {
      "turn": 1,
      "user": "Quiero reservar",
      "expect": ["día"]
    },
    {
      "turn": 2,
      "user": "El sábado",
      "expect": ["personas"],
      "notExpect": ["día"]
    },
    {
      "turn": 3,
      "user": "4",
      "expect": ["hora"],
      "notExpect": ["día", "personas"]
    },
    {
      "turn": 4,
      "user": "14:00",
      "expect": ["arroz"],
      "notExpect": ["día", "personas", "hora"]
    },
    {
      "turn": 5,
      "user": "No",
      "expect": ["sábado", "4 personas", "14:00", "confirmo"]
    }
  ]
}
```

---

### Context_HandlesAmbiguousCorrections
**Type:** Context Retention Test
**Messages:** 6

```json
{
  "name": "context-ambiguous-correction",
  "description": "Handles ambiguous corrections correctly",
  "messages": [
    {
      "turn": 1,
      "user": "Reserva sábado 14:00 para 4",
      "expect": ["arroz"]
    },
    {
      "turn": 2,
      "user": "Mejor las 15:00",
      "expect": ["15:00", "arroz"],
      "notExpect": ["14:00"]
    },
    {
      "turn": 3,
      "user": "Y mejor el domingo",
      "expect": ["domingo", "arroz"],
      "notExpect": ["sábado"]
    },
    {
      "turn": 4,
      "user": "Sin arroz",
      "expect": ["domingo", "15:00", "4 personas"],
      "notExpect": ["sábado", "14:00"]
    }
  ]
}
```

---

## Test Infrastructure for Context Tests

```csharp
public class ContextTestBase : ConversationFlowTestBase
{
    /// <summary>
    /// Verifies that the internal state has preserved expected data
    /// even when not explicitly mentioned in response
    /// </summary>
    protected async Task AssertContextPreserved(
        ConversationSimulator simulator,
        params string[] expectedFields)
    {
        var state = await GetCurrentState(simulator);

        foreach (var field in expectedFields)
        {
            switch (field.ToLower())
            {
                case "fecha":
                    state.Fecha.Should().NotBeNullOrEmpty(
                        "Date should be preserved in context");
                    break;
                case "hora":
                    state.Hora.Should().NotBeNullOrEmpty(
                        "Time should be preserved in context");
                    break;
                case "personas":
                    state.Personas.Should().NotBeNull(
                        "People count should be preserved in context");
                    break;
                case "arroz":
                    state.ArrozType.Should().NotBeNull(
                        "Rice decision should be preserved in context");
                    break;
            }
        }
    }

    /// <summary>
    /// Verifies that old values have been replaced
    /// </summary>
    protected async Task AssertValueReplaced(
        ConversationSimulator simulator,
        string field,
        string oldValue,
        string newValue)
    {
        var state = await GetCurrentState(simulator);
        var lastResponse = simulator.LastResponse;

        // New value should appear in relevant places
        lastResponse.Should().Contain(newValue);

        // Old value should NOT appear
        lastResponse.Should().NotContain(oldValue);

        // Internal state should have new value
        var stateValue = field switch
        {
            "fecha" => state.Fecha,
            "hora" => state.Hora,
            "personas" => state.Personas?.ToString(),
            _ => null
        };

        stateValue.Should().Contain(newValue);
    }

    private async Task<ConversationState> GetCurrentState(ConversationSimulator simulator)
    {
        // Access internal state from simulator or history service
        return await simulator.GetCurrentStateAsync();
    }
}

public class ConversationSimulator
{
    // ... existing implementation ...

    /// <summary>
    /// Additional assertions for context testing
    /// </summary>
    public void ShouldNotAskFor(params string[] topics)
    {
        var questionPatterns = new Dictionary<string, string[]>
        {
            { "fecha", new[] { "qué día", "cuándo", "para cuándo", "fecha" } },
            { "hora", new[] { "qué hora", "a qué hora", "hora" } },
            { "personas", new[] { "cuántas personas", "para cuántos", "cuántos" } },
            { "arroz", new[] { "queréis arroz", "arroz", "qué arroz" } }
        };

        foreach (var topic in topics)
        {
            if (questionPatterns.TryGetValue(topic.ToLower(), out var patterns))
            {
                foreach (var pattern in patterns)
                {
                    // Only flag if it appears as a question
                    if (_currentResponse.ToLower().Contains(pattern) &&
                        (_currentResponse.Contains("?") ||
                         _currentResponse.ToLower().Contains("qué") ||
                         _currentResponse.ToLower().Contains("cuándo") ||
                         _currentResponse.ToLower().Contains("cuántas")))
                    {
                        throw new AssertionException(
                            $"Bot asked for '{topic}' when it should already know this. " +
                            $"Pattern matched: '{pattern}'. Response: {_currentResponse}");
                    }
                }
            }
        }
    }

    public async Task<ConversationState> GetCurrentStateAsync()
    {
        // Return current conversation state for assertions
        return await _historyService.GetStateAsync(_userId);
    }
}
```
