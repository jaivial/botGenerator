# Response Quality Tests (Steps 251-280)

## Overview
These tests verify that responses meet quality standards: appropriate length, natural tone, proper formatting, and conversational flow.

**Test Type:** Conversation Quality (Integration)
**Messages per test:** 5

---

## Response Length Tests (Steps 251-255)

### Step 251-255: Quality_ResponseLength_Under300Chars
**Type:** Quality Test
**Messages:** 5

```json
{
  "name": "quality-response-length",
  "description": "Responses should be concise (under 300 chars for questions)",
  "messages": [
    {
      "turn": 1,
      "user": "Quiero reservar",
      "maxLength": 200,
      "expect": ["día", "cuándo"]
    },
    {
      "turn": 2,
      "user": "Sábado",
      "maxLength": 150,
      "expect": ["personas"]
    },
    {
      "turn": 3,
      "user": "4",
      "maxLength": 150,
      "expect": ["hora"]
    },
    {
      "turn": 4,
      "user": "14:00",
      "maxLength": 150,
      "expect": ["arroz"]
    },
    {
      "turn": 5,
      "user": "No",
      "maxLength": 350,
      "expect": ["confirmo"]
    }
  ]
}
```

```csharp
[Fact]
public async Task Quality_ResponseLength_Under300Chars()
{
    var simulator = CreateSimulator();

    // Question responses should be short
    await simulator.UserSays("Quiero reservar");
    simulator.ResponseLengthShouldBe(200);

    await simulator.UserSays("Sábado");
    simulator.ResponseLengthShouldBe(150);

    await simulator.UserSays("4");
    simulator.ResponseLengthShouldBe(150);

    await simulator.UserSays("14:00");
    simulator.ResponseLengthShouldBe(150);

    // Summary can be longer
    await simulator.UserSays("No");
    simulator.ResponseLengthShouldBe(350);
}
```

**Quality Rule:** Questions should be concise. Only summaries/confirmations can be longer.

---

## Single Question Focus Tests (Steps 256-260)

### Step 256-260: Quality_SingleQuestionPerMessage
**Type:** Quality Test
**Messages:** 5

```json
{
  "name": "quality-single-question",
  "description": "Each message should ask only one question",
  "messages": [
    {
      "turn": 1,
      "user": "Quiero reservar",
      "maxQuestions": 1,
      "notExpect": ["y también", "además"]
    },
    {
      "turn": 2,
      "user": "Sábado",
      "maxQuestions": 1,
      "notExpect": ["y a qué hora", "cuántas personas y"]
    },
    {
      "turn": 3,
      "user": "4 personas",
      "maxQuestions": 1
    },
    {
      "turn": 4,
      "user": "14:00",
      "maxQuestions": 1
    }
  ]
}
```

```csharp
[Fact]
public async Task Quality_SingleQuestionPerMessage()
{
    var simulator = CreateSimulator();

    await simulator.UserSays("Quiero reservar");
    simulator.ShouldHaveMaxQuestions(1);
    simulator.ShouldNotMention("y también", "además");

    await simulator.UserSays("Sábado");
    simulator.ShouldHaveMaxQuestions(1);
    // Should NOT ask "¿Cuántas personas y a qué hora?"
    simulator.ShouldNotMention("y a qué hora", "cuántas personas y");

    await simulator.UserSays("4 personas");
    simulator.ShouldHaveMaxQuestions(1);

    await simulator.UserSays("14:00");
    simulator.ShouldHaveMaxQuestions(1);
}
```

**Quality Rule:** One question per message. Never combine questions.

---

## No Bullet Lists for Questions (Steps 261-265)

### Step 261-265: Quality_NoBulletLists_ForQuestions
**Type:** Quality Test
**Messages:** 5

```json
{
  "name": "quality-no-bullet-lists",
  "description": "Questions should not use bullet point or numbered lists",
  "messages": [
    {
      "turn": 1,
      "user": "Quiero reservar",
      "notExpect": ["1.", "2.", "•", "-", "a)", "b)"]
    },
    {
      "turn": 2,
      "user": "Sábado",
      "notExpect": ["1.", "2.", "•", "-"]
    },
    {
      "turn": 3,
      "user": "4 personas",
      "notExpect": ["1.", "2.", "•", "-"]
    }
  ]
}
```

```csharp
[Fact]
public async Task Quality_NoBulletLists_ForQuestions()
{
    var simulator = CreateSimulator();

    await simulator.UserSays("Quiero reservar");
    simulator.ShouldNotMention("1.", "2.", "•", "-", "a)", "b)");
    // Should NOT be: "Para hacer la reserva necesito:
    //                 1. Fecha
    //                 2. Hora
    //                 3. Personas"

    await simulator.UserSays("Sábado");
    simulator.ShouldNotMention("1.", "2.");

    await simulator.UserSays("4 personas");
    simulator.ShouldNotMention("1.", "2.");
}
```

**Quality Rule:** Natural conversation, not form-filling. Lists allowed only for menus/options.

---

## Personalization Tests (Steps 266-270)

### Step 266-270: Quality_UsesClientName_Occasionally
**Type:** Quality Test
**Messages:** 5

```json
{
  "name": "quality-uses-name",
  "description": "Uses customer name occasionally but not excessively",
  "setup": {
    "pushName": "María"
  },
  "messages": [
    {
      "turn": 1,
      "user": "Hola",
      "mayContain": ["María"],
      "nameUsageOptional": true
    },
    {
      "turn": 2,
      "user": "Quiero reservar para el sábado",
      "expect": ["personas"],
      "nameUsageOptional": true
    },
    {
      "turn": 3,
      "user": "4 a las 14:00",
      "expect": ["arroz"]
    },
    {
      "turn": 4,
      "user": "No",
      "mayContain": ["María"]
    },
    {
      "turn": 5,
      "user": "Sí",
      "mayContain": ["María"]
    }
  ],
  "constraints": {
    "maxNameMentions": 3,
    "minNameMentions": 0
  }
}
```

```csharp
[Fact]
public async Task Quality_UsesClientName_Occasionally()
{
    var simulator = CreateSimulator(pushName: "María");
    var nameMentions = 0;

    await simulator.UserSays("Hola");
    if (simulator.LastResponse.Contains("María")) nameMentions++;

    await simulator.UserSays("Quiero reservar para el sábado");
    if (simulator.LastResponse.Contains("María")) nameMentions++;

    await simulator.UserSays("4 a las 14:00");
    await simulator.UserSays("No");
    await simulator.UserSays("Sí");

    // Name should be used occasionally, not every message
    nameMentions.Should().BeLessThan(4);
}
```

**Quality Rule:** Personalize occasionally, don't overuse name.

---

## Natural Tone Tests (Steps 271-275)

### Step 271-275: Quality_NaturalTone_NotRobotic
**Type:** Quality Test
**Messages:** 5

```json
{
  "name": "quality-natural-tone",
  "description": "Responses should sound natural, not robotic",
  "messages": [
    {
      "turn": 1,
      "user": "Hola",
      "notExpect": [
        "Bienvenido al sistema de reservas",
        "Para procesar su solicitud",
        "Indique el dato requerido"
      ],
      "expectTone": "friendly"
    },
    {
      "turn": 2,
      "user": "Quiero reservar",
      "notExpect": [
        "Por favor, introduzca",
        "El campo fecha es obligatorio",
        "Seleccione una opción"
      ]
    },
    {
      "turn": 3,
      "user": "Sábado",
      "notExpect": [
        "Dato registrado correctamente",
        "Procesando información"
      ]
    }
  ]
}
```

```csharp
[Fact]
public async Task Quality_NaturalTone_NotRobotic()
{
    var simulator = CreateSimulator();

    await simulator.UserSays("Hola");

    // Should NOT sound like a system message
    simulator.ShouldNotMention(
        "Bienvenido al sistema",
        "Para procesar su solicitud",
        "Indique el dato",
        "campo obligatorio");

    // Should sound like a human
    simulator.ShouldRespondNaturally();

    await simulator.UserSays("Quiero reservar");
    simulator.ShouldNotMention(
        "Por favor, introduzca",
        "campo requerido",
        "Seleccione");

    await simulator.UserSays("Sábado");
    simulator.ShouldNotMention(
        "Dato registrado",
        "Procesando");
}
```

**Quality Rule:** Sound like a friendly human, not a form system.

---

## Emoji Usage Tests (Steps 276-280)

### Step 276-280: Quality_EmojisUsedSparingly
**Type:** Quality Test
**Messages:** 5

```json
{
  "name": "quality-emoji-sparingly",
  "description": "Emojis used moderately - max 2-3 per message, none in questions",
  "messages": [
    {
      "turn": 1,
      "user": "Hola",
      "maxEmojis": 2
    },
    {
      "turn": 2,
      "user": "Quiero reservar",
      "maxEmojis": 1
    },
    {
      "turn": 3,
      "user": "Sábado 4 personas 14:00",
      "maxEmojis": 1
    },
    {
      "turn": 4,
      "user": "Sin arroz, confirma",
      "expect": ["✅"],
      "maxEmojis": 6
    }
  ]
}
```

```csharp
[Fact]
public async Task Quality_EmojisUsedSparingly()
{
    var simulator = CreateSimulator();

    await simulator.UserSays("Hola");
    simulator.ShouldHaveMaxEmojis(2);

    // Questions should have minimal emojis
    await simulator.UserSays("Quiero reservar");
    simulator.ShouldHaveMaxEmojis(1);

    await simulator.UserSays("Sábado 4 personas 14:00");
    simulator.ShouldHaveMaxEmojis(1);

    // Confirmation can have more emojis
    await simulator.UserSays("Sin arroz, confirma");
    simulator.ShouldRespond("✅");
    simulator.ShouldHaveMaxEmojis(6); // Confirmation uses icons for each field
}
```

**Quality Rule:** Emojis for emphasis, not decoration. More allowed in confirmations.

---

## Additional Quality Tests

### Quality_NoRepetition_InResponses
**Type:** Quality Test

```json
{
  "name": "quality-no-repetition",
  "description": "Bot doesn't repeat itself unnecessarily",
  "messages": [
    {
      "turn": 1,
      "user": "Reserva sábado",
      "expect": ["personas"]
    },
    {
      "turn": 2,
      "user": "4",
      "expect": ["hora"],
      "notExpect": ["sábado", "4 personas"]
    }
  ]
}
```

---

### Quality_ConfirmationReadable
**Type:** Quality Test

```json
{
  "name": "quality-confirmation-readable",
  "description": "Final confirmation is well-formatted and readable",
  "messages": [
    {
      "turn": 1,
      "user": "Reserva sábado 14:00 4 personas sin arroz, confirma",
      "expect": ["sábado", "14:00", "4 personas", "confirmo"]
    },
    {
      "turn": 2,
      "user": "Sí",
      "expectFormat": {
        "hasLineBreaks": true,
        "usesEmojis": true,
        "organizedByField": true
      }
    }
  ]
}
```

---

### Quality_GracefulErrorMessages
**Type:** Quality Test

```json
{
  "name": "quality-graceful-errors",
  "description": "Error messages are friendly and helpful",
  "messages": [
    {
      "turn": 1,
      "user": "xkjhdkjh",
      "expect": ["entendido", "repetir", "ayudar"],
      "notExpect": ["error", "inválido", "incorrecto"]
    }
  ]
}
```

---

## Test Infrastructure for Quality Tests

```csharp
public class QualityTestBase : ConversationFlowTestBase
{
    // Quality assertion methods
}

public static class QualityAssertions
{
    public static ConversationSimulator ResponseLengthShouldBe(
        this ConversationSimulator simulator,
        int maxLength)
    {
        simulator.LastResponse.Length.Should().BeLessOrEqualTo(maxLength,
            $"Response too long ({simulator.LastResponse.Length} chars). " +
            $"Expected max {maxLength}. Response: {simulator.LastResponse}");
        return simulator;
    }

    public static ConversationSimulator ShouldHaveMaxQuestions(
        this ConversationSimulator simulator,
        int max)
    {
        var response = simulator.LastResponse;
        var questionMarks = response.Count(c => c == '?');

        questionMarks.Should().BeLessOrEqualTo(max,
            $"Too many questions ({questionMarks}) in response: {response}");

        return simulator;
    }

    public static ConversationSimulator ShouldHaveMaxEmojis(
        this ConversationSimulator simulator,
        int max)
    {
        var response = simulator.LastResponse;
        var emojiCount = CountEmojis(response);

        emojiCount.Should().BeLessOrEqualTo(max,
            $"Too many emojis ({emojiCount}) in response: {response}");

        return simulator;
    }

    public static ConversationSimulator ShouldRespondNaturally(
        this ConversationSimulator simulator)
    {
        var roboticPhrases = new[]
        {
            "sistema de reservas",
            "procesar su solicitud",
            "campo obligatorio",
            "dato requerido",
            "introduzca",
            "seleccione una opción",
            "datos registrados",
            "procesando"
        };

        foreach (var phrase in roboticPhrases)
        {
            simulator.LastResponse.ToLower().Should().NotContain(phrase,
                $"Response sounds robotic: contains '{phrase}'");
        }

        return simulator;
    }

    private static int CountEmojis(string text)
    {
        // Count emoji characters (simplified - uses Unicode ranges)
        var emojiPatterns = new[]
        {
            @"[\u2600-\u26FF]",  // Misc symbols
            @"[\u2700-\u27BF]",  // Dingbats
            @"[\U0001F300-\U0001F9FF]", // Misc symbols and pictographs
        };

        int count = 0;
        foreach (var pattern in emojiPatterns)
        {
            count += Regex.Matches(text, pattern).Count;
        }
        return count;
    }
}
```
