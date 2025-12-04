# Multi-Turn Conversation Flow Tests (Steps 111-180)

## Overview
These tests verify complete multi-turn conversation flows for booking, cancellation, and modification scenarios.

**Test Type:** Conversation Logic (Integration)
**Messages per test:** 5-8

---

## Simple Booking Flow (Steps 111-115)

### Step 111-115: BookingFlow_SimpleComplete_5Messages
**Type:** Conversation Flow Test
**Messages:** 5

```json
{
  "name": "booking-simple-complete",
  "description": "Complete booking without rice",
  "messages": [
    {
      "turn": 1,
      "user": "Hola, quiero hacer una reserva",
      "expect": ["día", "cuándo", "fecha"]
    },
    {
      "turn": 2,
      "user": "Para el sábado",
      "expect": ["personas", "cuántas"]
    },
    {
      "turn": 3,
      "user": "Somos 4 a las 14:00",
      "expect": ["arroz", "queréis"]
    },
    {
      "turn": 4,
      "user": "No, sin arroz",
      "expect": ["4 personas", "sábado", "14:00", "confirmo"]
    },
    {
      "turn": 5,
      "user": "Sí, confirma",
      "expect": ["✅", "confirmada", "reserva"]
    }
  ]
}
```

```csharp
[Fact]
public async Task BookingFlow_SimpleComplete_5Messages()
{
    // Arrange
    var simulator = CreateSimulator();

    // Turn 1: Greeting + intent
    await simulator.UserSays("Hola, quiero hacer una reserva");
    simulator.ShouldRespond("día", "cuándo");

    // Turn 2: Date
    await simulator.UserSays("Para el sábado");
    simulator.ShouldRespond("personas");

    // Turn 3: People + Time
    await simulator.UserSays("Somos 4 a las 14:00");
    simulator.ShouldRespond("arroz");

    // Turn 4: No rice
    await simulator.UserSays("No, sin arroz");
    simulator.ShouldRespond("4 personas", "sábado", "14:00", "confirmo");

    // Turn 5: Confirmation
    await simulator.UserSays("Sí, confirma");
    simulator.ShouldRespond("✅", "confirmada");

    // Verify booking was created
    simulator.MessageCount.Should().Be(10); // 5 user + 5 assistant
}
```

---

## Booking With Rice (Steps 116-120)

### Step 116-120: BookingFlow_WithRice_6Messages
**Type:** Conversation Flow Test
**Messages:** 6

```json
{
  "name": "booking-with-rice",
  "description": "Booking with rice selection",
  "messages": [
    {
      "turn": 1,
      "user": "Quiero reservar para el domingo 4 personas a las 14:30",
      "expect": ["arroz", "queréis"]
    },
    {
      "turn": 2,
      "user": "Sí, arroz del señoret",
      "expect": ["señoret", "disponible", "raciones"]
    },
    {
      "turn": 3,
      "user": "3 raciones",
      "expect": ["domingo", "4 personas", "14:30", "señoret", "3 raciones", "confirmo"]
    },
    {
      "turn": 4,
      "user": "Sí",
      "expect": ["✅", "confirmada"]
    }
  ]
}
```

```csharp
[Fact]
public async Task BookingFlow_WithRice_6Messages()
{
    var simulator = CreateSimulator();

    // Turn 1: All basic data at once
    await simulator.UserSays("Quiero reservar para el domingo 4 personas a las 14:30");
    simulator.ShouldRespond("arroz"); // Only missing piece

    // Turn 2: Rice type
    await simulator.UserSays("Sí, arroz del señoret");
    simulator.ShouldRespond("raciones"); // Validated, ask for servings

    // Turn 3: Servings
    await simulator.UserSays("3 raciones");
    simulator.ShouldRespond("confirmo"); // Summary + confirmation

    // Turn 4: Confirm
    await simulator.UserSays("Sí");
    simulator.ShouldRespond("✅", "confirmada");
}
```

---

## Rice Validation Flow (Steps 121-126)

### Step 121-126: BookingFlow_RiceValidation_7Messages
**Type:** Conversation Flow Test
**Messages:** 7

```json
{
  "name": "booking-rice-validation",
  "description": "Rice validation with successful match",
  "messages": [
    {
      "turn": 1,
      "user": "Reserva sábado 14h para 6",
      "expect": ["arroz"]
    },
    {
      "turn": 2,
      "user": "Paella valenciana",
      "expect": ["✅", "paella valenciana", "disponible", "raciones"]
    },
    {
      "turn": 3,
      "user": "4 raciones",
      "expect": ["sábado", "6 personas", "14:00", "paella", "4 raciones"]
    },
    {
      "turn": 4,
      "user": "Confirmo",
      "expect": ["✅", "confirmada"]
    }
  ]
}
```

---

## Invalid Rice Clarification (Steps 127-132)

### Step 127-132: BookingFlow_InvalidRice_Clarification_8Messages
**Type:** Conversation Flow Test
**Messages:** 8

```json
{
  "name": "booking-invalid-rice",
  "description": "Invalid rice type requires clarification",
  "messages": [
    {
      "turn": 1,
      "user": "Reserva para domingo 4 personas 15:00",
      "expect": ["arroz"]
    },
    {
      "turn": 2,
      "user": "Arroz con pollo",
      "expect": ["no tenemos", "carta", "disponible"]
    },
    {
      "turn": 3,
      "user": "¿Qué arroces tenéis?",
      "expect": ["señoret", "paella", "negro", "banda"]
    },
    {
      "turn": 4,
      "user": "Arroz a banda entonces",
      "expect": ["✅", "banda", "disponible", "raciones"]
    },
    {
      "turn": 5,
      "user": "2 raciones",
      "expect": ["domingo", "4 personas", "15:00", "banda", "confirmo"]
    },
    {
      "turn": 6,
      "user": "Vale",
      "expect": ["✅", "confirmada"]
    }
  ]
}
```

```csharp
[Fact]
public async Task BookingFlow_InvalidRice_Clarification_8Messages()
{
    var simulator = CreateSimulator();

    await simulator.UserSays("Reserva para domingo 4 personas 15:00");
    simulator.ShouldRespond("arroz");

    // Try invalid rice
    await simulator.UserSays("Arroz con pollo");
    simulator.ShouldRespond("no tenemos");

    // Ask for menu
    await simulator.UserSays("¿Qué arroces tenéis?");
    simulator.ShouldRespond("señoret", "paella");

    // Choose valid option
    await simulator.UserSays("Arroz a banda entonces");
    simulator.ShouldRespond("banda", "disponible");

    // Continue flow...
    await simulator.UserSays("2 raciones");
    await simulator.UserSays("Vale");
    simulator.ShouldRespond("confirmada");
}
```

---

## Multiple Rice Options (Steps 133-138)

### Step 133-138: BookingFlow_MultipleRiceOptions_8Messages
**Type:** Conversation Flow Test
**Messages:** 8

```json
{
  "name": "booking-multiple-rice",
  "description": "Ambiguous rice selection clarified",
  "messages": [
    {
      "turn": 1,
      "user": "Reserva sábado 14h 4 personas",
      "expect": ["arroz"]
    },
    {
      "turn": 2,
      "user": "Arroz de carrillada",
      "expect": ["meloso", "seco", "cuál"]
    },
    {
      "turn": 3,
      "user": "El meloso con boletus",
      "expect": ["✅", "meloso", "carrillada", "boletus"]
    },
    {
      "turn": 4,
      "user": "3 raciones",
      "expect": ["confirmo"]
    },
    {
      "turn": 5,
      "user": "Sí",
      "expect": ["✅", "confirmada"]
    }
  ]
}
```

---

## Booking With High Chairs (Steps 139-143)

### Step 139-143: BookingFlow_WithHighChairs_7Messages
**Type:** Conversation Flow Test
**Messages:** 7

```json
{
  "name": "booking-high-chairs",
  "description": "Booking requesting high chairs",
  "messages": [
    {
      "turn": 1,
      "user": "Reserva para el domingo para 5 personas a las 14:00",
      "expect": ["arroz"]
    },
    {
      "turn": 2,
      "user": "No queremos arroz, pero necesitamos 2 tronas",
      "expect": ["2 tronas", "sin arroz", "confirmo"]
    },
    {
      "turn": 3,
      "user": "Sí, confirma",
      "expect": ["✅", "confirmada", "tronas"]
    }
  ]
}
```

---

## Full Options Booking (Steps 144-148)

### Step 144-148: BookingFlow_FullOptions_8Messages
**Type:** Conversation Flow Test
**Messages:** 8

```json
{
  "name": "booking-full-options",
  "description": "Booking with rice, high chairs, and strollers",
  "messages": [
    {
      "turn": 1,
      "user": "Quiero reservar para 6 el sábado a las 14:00",
      "expect": ["arroz"]
    },
    {
      "turn": 2,
      "user": "Arroz del señoret, 4 raciones",
      "expect": ["señoret", "4 raciones"]
    },
    {
      "turn": 3,
      "user": "Ah, y necesitamos 1 trona y espacio para un carrito",
      "expect": ["trona", "carrito", "confirmo"]
    },
    {
      "turn": 4,
      "user": "Confirmo",
      "expect": ["✅", "confirmada", "señoret", "trona", "carrito"]
    }
  ]
}
```

---

## Changes Mind on Date (Steps 149-153)

### Step 149-153: BookingFlow_ChangesMindOnDate_8Messages
**Type:** Conversation Flow Test
**Messages:** 8

```json
{
  "name": "booking-change-date",
  "description": "User changes date mid-conversation",
  "messages": [
    {
      "turn": 1,
      "user": "Reserva para el sábado",
      "expect": ["sábado", "personas"]
    },
    {
      "turn": 2,
      "user": "4 personas a las 14:00",
      "expect": ["arroz"]
    },
    {
      "turn": 3,
      "user": "Espera, mejor el domingo",
      "expect": ["domingo", "arroz"]
    },
    {
      "turn": 4,
      "user": "No queremos arroz",
      "expect": ["domingo", "4 personas", "14:00", "confirmo"]
    },
    {
      "turn": 5,
      "user": "Sí",
      "expect": ["✅", "confirmada", "domingo"]
    }
  ]
}
```

```csharp
[Fact]
public async Task BookingFlow_ChangesMindOnDate_8Messages()
{
    var simulator = CreateSimulator();

    await simulator.UserSays("Reserva para el sábado");
    await simulator.UserSays("4 personas a las 14:00");

    // Change date
    await simulator.UserSays("Espera, mejor el domingo");
    simulator.ShouldRespond("domingo");
    simulator.ShouldNotMention("sábado"); // Old date should be replaced

    await simulator.UserSays("No queremos arroz");
    simulator.ShouldRespond("domingo"); // Confirm new date in summary

    await simulator.UserSays("Sí");
    simulator.ShouldRespond("confirmada", "domingo");
}
```

---

## Changes Mind on Time (Steps 154-158)

### Step 154-158: BookingFlow_ChangesMindOnTime_7Messages
**Type:** Conversation Flow Test
**Messages:** 7

```json
{
  "name": "booking-change-time",
  "description": "User changes time mid-conversation",
  "messages": [
    {
      "turn": 1,
      "user": "Reserva domingo 14:00 para 4",
      "expect": ["arroz"]
    },
    {
      "turn": 2,
      "user": "En verdad mejor a las 15:00",
      "expect": ["15:00", "arroz"]
    },
    {
      "turn": 3,
      "user": "Sin arroz",
      "expect": ["15:00", "confirmo"]
    },
    {
      "turn": 4,
      "user": "Ok",
      "expect": ["✅", "15:00"]
    }
  ]
}
```

---

## Interrupts With Question (Steps 159-163)

### Step 159-163: BookingFlow_InterruptsWithQuestion_8Messages
**Type:** Conversation Flow Test
**Messages:** 8

```json
{
  "name": "booking-interrupt-question",
  "description": "User asks question mid-booking flow",
  "messages": [
    {
      "turn": 1,
      "user": "Quiero reservar para el sábado 4 personas",
      "expect": ["hora", "qué hora"]
    },
    {
      "turn": 2,
      "user": "¿A qué hora abrís?",
      "expect": ["13:30", "abrimos"]
    },
    {
      "turn": 3,
      "user": "Vale, a las 14:00",
      "expect": ["arroz"]
    },
    {
      "turn": 4,
      "user": "No",
      "expect": ["confirmo"]
    },
    {
      "turn": 5,
      "user": "Sí",
      "expect": ["✅", "confirmada"]
    }
  ]
}
```

---

## Cancellation Flows (Steps 164-173)

### Step 164-168: CancellationFlow_WithBookingFound_5Messages
**Type:** Conversation Flow Test
**Messages:** 5

```json
{
  "name": "cancellation-found",
  "description": "Cancel existing booking",
  "messages": [
    {
      "turn": 1,
      "user": "Quiero cancelar mi reserva",
      "expect": ["reserva", "datos", "cuándo"]
    },
    {
      "turn": 2,
      "user": "Es para el sábado 30 a las 14:00",
      "expect": ["encontrado", "cancelar"]
    },
    {
      "turn": 3,
      "user": "Sí, cancela",
      "expect": ["✅", "cancelada"]
    }
  ]
}
```

```csharp
[Fact]
public async Task CancellationFlow_WithBookingFound_5Messages()
{
    var simulator = CreateSimulatorWithExistingBooking();

    await simulator.UserSays("Quiero cancelar mi reserva");
    simulator.ShouldRespond("datos");

    await simulator.UserSays("Es para el sábado 30 a las 14:00");
    simulator.ShouldRespond("encontrado");

    await simulator.UserSays("Sí, cancela");
    simulator.ShouldRespond("✅", "cancelada");
}
```

---

### Step 169-173: CancellationFlow_NoBookingFound_4Messages
**Type:** Conversation Flow Test
**Messages:** 4

```json
{
  "name": "cancellation-not-found",
  "description": "Cancellation with no matching booking",
  "messages": [
    {
      "turn": 1,
      "user": "Cancelar mi reserva",
      "expect": ["datos"]
    },
    {
      "turn": 2,
      "user": "Para el viernes a las 20:00",
      "expect": ["no encontramos", "verificar"]
    },
    {
      "turn": 3,
      "user": "A nombre de García",
      "expect": ["no hay reserva", "nueva"]
    }
  ]
}
```

---

## Modification Flows (Steps 174-180)

### Step 174-178: ModificationFlow_SingleBooking_6Messages
**Type:** Conversation Flow Test
**Messages:** 6

```json
{
  "name": "modification-single",
  "description": "Modify single existing booking",
  "messages": [
    {
      "turn": 1,
      "user": "Quiero modificar mi reserva",
      "expect": ["reserva", "30/11", "14:00", "4 personas"]
    },
    {
      "turn": 2,
      "user": "Quiero cambiar la hora",
      "expect": ["hora", "cuál"]
    },
    {
      "turn": 3,
      "user": "A las 15:00",
      "expect": ["15:00", "modificada"]
    }
  ]
}
```

```csharp
[Fact]
public async Task ModificationFlow_SingleBooking_6Messages()
{
    var simulator = CreateSimulatorWithExistingBooking();

    await simulator.UserSays("Quiero modificar mi reserva");
    simulator.ShouldRespond("reserva", "30/11"); // Shows found booking

    await simulator.UserSays("Quiero cambiar la hora");
    simulator.ShouldRespond("hora");

    await simulator.UserSays("A las 15:00");
    simulator.ShouldRespond("15:00", "modificada");
}
```

---

### Step 179-180: ModificationFlow_MultipleBookings_8Messages
**Type:** Conversation Flow Test
**Messages:** 8

```json
{
  "name": "modification-multiple",
  "description": "Select from multiple bookings to modify",
  "messages": [
    {
      "turn": 1,
      "user": "Modificar reserva",
      "expect": ["varias reservas", "1.", "2."]
    },
    {
      "turn": 2,
      "user": "La segunda",
      "expect": ["07/12", "modificar"]
    },
    {
      "turn": 3,
      "user": "Cambiar el número de personas",
      "expect": ["cuántas personas"]
    },
    {
      "turn": 4,
      "user": "Ahora somos 8",
      "expect": ["8 personas", "modificada"]
    }
  ]
}
```

---

## Test Infrastructure

```csharp
public class ConversationFlowTestBase : IAsyncLifetime
{
    protected ConversationSimulator Simulator { get; private set; } = null!;
    protected Mock<IGeminiService> AiServiceMock { get; private set; } = null!;
    protected Mock<IWhatsAppService> WhatsAppMock { get; private set; } = null!;

    public virtual async Task InitializeAsync()
    {
        AiServiceMock = new Mock<IGeminiService>();
        WhatsAppMock = new Mock<IWhatsAppService>();

        ConfigureDefaultAiBehavior();

        var services = new ServiceCollection();
        ConfigureServices(services);

        var provider = services.BuildServiceProvider();
        Simulator = new ConversationSimulator(
            provider.GetRequiredService<MainConversationAgent>(),
            provider.GetRequiredService<IIntentRouterService>(),
            provider.GetRequiredService<IConversationHistoryService>());
    }

    protected virtual void ConfigureDefaultAiBehavior()
    {
        // Configure mock to generate contextually appropriate responses
        AiServiceMock
            .Setup(x => x.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns((string system, string user, List<ChatMessage> history, CancellationToken ct) =>
                Task.FromResult(GenerateContextualResponse(system, user, history)));
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(AiServiceMock.Object);
        services.AddSingleton(WhatsAppMock.Object);
        services.AddSingleton<IConversationHistoryService, ConversationHistoryService>();
        services.AddSingleton<IPromptLoaderService, PromptLoaderService>();
        services.AddSingleton<IContextBuilderService, ContextBuilderService>();
        services.AddSingleton<IIntentRouterService, IntentRouterService>();
        services.AddSingleton<MainConversationAgent>();
        services.AddSingleton<RiceValidatorAgent>();
        services.AddSingleton<AvailabilityCheckerAgent>();
        services.AddSingleton<BookingHandler>();
        services.AddSingleton<CancellationHandler>();
        services.AddSingleton<ModificationHandler>();

        // Add configuration
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(GetDefaultConfiguration())
            .Build();
        services.AddSingleton<IConfiguration>(config);

        // Add logging
        services.AddLogging();
    }

    protected ConversationSimulator CreateSimulatorWithExistingBooking()
    {
        // Configure handler to return existing booking
        // ...
        return Simulator;
    }

    private string GenerateContextualResponse(
        string system,
        string user,
        List<ChatMessage>? history)
    {
        // Sophisticated mock that tracks conversation state
        var state = ExtractStateFromHistory(history);
        var intent = DetectIntent(user);

        return intent switch
        {
            "greeting" => "¡Hola! ¿En qué puedo ayudarte?",
            "booking_start" => GenerateBookingResponse(state, user),
            "cancellation" => GenerateCancellationResponse(user),
            "modification" => GenerateModificationResponse(user),
            _ => "Entendido. ¿Hay algo más en lo que pueda ayudarte?"
        };
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
```
