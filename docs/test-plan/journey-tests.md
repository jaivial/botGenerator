# Full Customer Journey Tests (Steps 281-300)

## Overview
These tests simulate complete customer journeys with realistic multi-turn conversations, including topic changes, corrections, questions, and complex scenarios. These are the most comprehensive tests, with up to 40 messages per journey.

**Test Type:** Conversation Logic (End-to-End)
**Messages per test:** 10-40

---

## New Customer First Booking (Steps 281-285)

### Step 281-285: Journey_NewCustomer_FirstBooking_15Messages
**Type:** Full Journey Test
**Messages:** 15

```json
{
  "name": "journey-new-customer",
  "description": "Complete journey for a new customer making their first booking",
  "scenario": "New customer discovers restaurant, asks questions, makes booking",
  "messages": [
    { "turn": 1, "user": "Hola!", "expect": ["hola", "ayudar"] },
    { "turn": 2, "user": "¿Qué tipo de restaurante sois?", "expect": ["paella", "arroz", "valenciana"] },
    { "turn": 3, "user": "Qué bien! Quiero hacer una reserva", "expect": ["día", "cuándo"] },
    { "turn": 4, "user": "¿Qué días abrís?", "expect": ["jueves", "viernes", "sábado", "domingo"] },
    { "turn": 5, "user": "Pues el sábado", "expect": ["personas"] },
    { "turn": 6, "user": "Somos 6 adultos y 2 niños", "expect": ["8", "hora"] },
    { "turn": 7, "user": "A las 14:00", "expect": ["arroz"] },
    { "turn": 8, "user": "¿Qué arroces tenéis?", "expect": ["paella", "señoret", "negro", "banda"] },
    { "turn": 9, "user": "La paella valenciana, 4 raciones", "expect": ["paella", "4", "raciones"] },
    { "turn": 10, "user": "Ah, y necesitamos 2 tronas por favor", "expect": ["tronas", "2"] },
    { "turn": 11, "user": "Perfecto!", "expect": ["sábado", "8 personas", "14:00", "paella", "tronas", "confirmo"] },
    { "turn": 12, "user": "Sí, confirma", "expect": ["✅", "confirmada"] },
    { "turn": 13, "user": "Gracias!", "expect": ["gracias", "esperamos", "pronto"] },
    { "turn": 14, "user": "Por cierto, ¿tenéis parking?", "expect": ["parking", "aparcar"] },
    { "turn": 15, "user": "Genial, hasta el sábado!", "expect": ["hasta pronto", "sábado"] }
  ]
}
```

```csharp
[Fact]
public async Task Journey_NewCustomer_FirstBooking_15Messages()
{
    var simulator = CreateSimulator(pushName: "Ana");

    // Initial greeting
    await simulator.UserSays("Hola!");
    simulator.ShouldRespond("hola", "ayudar");

    // Discovery question
    await simulator.UserSays("¿Qué tipo de restaurante sois?");
    simulator.ShouldRespond("paella", "arroz");

    // Start booking
    await simulator.UserSays("Qué bien! Quiero hacer una reserva");
    simulator.ShouldRespond("día");

    // Ask about schedule
    await simulator.UserSays("¿Qué días abrís?");
    simulator.ShouldRespond("sábado", "domingo");

    // Provide date
    await simulator.UserSays("Pues el sábado");
    simulator.ShouldRespond("personas");

    // Complex people count (adults + children)
    await simulator.UserSays("Somos 6 adultos y 2 niños");
    simulator.ShouldRespond("8"); // Should calculate total

    // Time
    await simulator.UserSays("A las 14:00");
    simulator.ShouldRespond("arroz");

    // Ask about menu
    await simulator.UserSays("¿Qué arroces tenéis?");
    simulator.ShouldRespond("paella", "señoret");

    // Rice selection with servings
    await simulator.UserSays("La paella valenciana, 4 raciones");
    simulator.ShouldRespond("paella");

    // Add high chairs
    await simulator.UserSays("Ah, y necesitamos 2 tronas por favor");
    simulator.ShouldRespond("tronas");

    // Ready to confirm
    await simulator.UserSays("Perfecto!");
    simulator.ShouldRespond("confirmo", "8 personas", "14:00", "paella");

    // Confirm
    await simulator.UserSays("Sí, confirma");
    simulator.ShouldRespond("✅", "confirmada");

    // Post-booking thanks
    await simulator.UserSays("Gracias!");
    simulator.ShouldRespond("esperamos");

    // Post-booking question
    await simulator.UserSays("Por cierto, ¿tenéis parking?");
    simulator.ShouldRespond("parking");

    // Farewell
    await simulator.UserSays("Genial, hasta el sábado!");
    simulator.ShouldRespond("pronto");

    simulator.MessageCount.Should().Be(30); // 15 user + 15 assistant
}
```

---

## Returning Customer Quick Booking (Steps 286-290)

### Step 286-290: Journey_ReturningCustomer_QuickBooking_10Messages
**Type:** Full Journey Test
**Messages:** 10

```json
{
  "name": "journey-returning-customer",
  "description": "Experienced customer makes a quick booking",
  "scenario": "Customer knows the restaurant, provides all info efficiently",
  "messages": [
    { "turn": 1, "user": "Hola! Quiero reservar para el domingo a las 14:30 para 4 personas", "expect": ["arroz"] },
    { "turn": 2, "user": "Esta vez sin arroz", "expect": ["confirmo", "domingo", "14:30", "4 personas"] },
    { "turn": 3, "user": "Sí, todo correcto", "expect": ["✅", "confirmada"] },
    { "turn": 4, "user": "Gracias, hasta el domingo", "expect": ["hasta pronto"] }
  ]
}
```

```csharp
[Fact]
public async Task Journey_ReturningCustomer_QuickBooking_10Messages()
{
    var simulator = CreateSimulator(pushName: "Pedro");

    // All info in one message
    await simulator.UserSays(
        "Hola! Quiero reservar para el domingo a las 14:30 para 4 personas");
    simulator.ShouldRespond("arroz"); // Only missing piece
    simulator.ShouldNotMention("día", "hora", "personas"); // No redundant questions

    // Quick rice answer
    await simulator.UserSays("Esta vez sin arroz");
    simulator.ShouldRespond("confirmo");

    // Confirm
    await simulator.UserSays("Sí, todo correcto");
    simulator.ShouldRespond("✅", "confirmada");

    // Quick farewell
    await simulator.UserSays("Gracias, hasta el domingo");
    simulator.ShouldRespond("hasta pronto");

    // Efficient conversation - only 4 turns needed
    simulator.MessageCount.Should().Be(8);
}
```

---

## Complex Booking with All Options (Steps 291-294)

### Step 291-294: Journey_ComplexBooking_WithAllOptions_20Messages
**Type:** Full Journey Test
**Messages:** 20

```json
{
  "name": "journey-complex-booking",
  "description": "Booking with rice validation, high chairs, strollers, and mid-course corrections",
  "scenario": "Large family celebration with special requirements",
  "messages": [
    { "turn": 1, "user": "Buenas! Vamos a celebrar un cumpleaños", "expect": ["ayudar", "reserva"] },
    { "turn": 2, "user": "Sí, para el sábado que viene", "expect": ["personas"] },
    { "turn": 3, "user": "Seremos unas 12 personas", "expect": ["hora"] },
    { "turn": 4, "user": "A las 14:00", "expect": ["arroz"] },
    { "turn": 5, "user": "Sí, queremos probar varios arroces", "expect": ["cuál", "qué"] },
    { "turn": 6, "user": "Arroz del señoret y paella valenciana", "expect": ["dos", "raciones"] },
    { "turn": 7, "user": "3 raciones de cada uno", "expect": ["6 raciones"] },
    { "turn": 8, "user": "Perfecto! Llevamos bebés, necesitamos tronas", "expect": ["cuántas"] },
    { "turn": 9, "user": "2 tronas y espacio para 1 carrito", "expect": ["tronas", "carrito"] },
    { "turn": 10, "user": "Uy espera, en verdad somos 14, se me había olvidado mi cuñada", "expect": ["14 personas"] },
    { "turn": 11, "user": "Sí 14, perdona", "expect": ["confirmo", "14 personas", "sábado"] },
    { "turn": 12, "user": "Sí, confirma", "expect": ["✅", "confirmada", "señoret", "paella", "tronas"] },
    { "turn": 13, "user": "¡Muchísimas gracias!", "expect": ["gracias", "cumpleaños"] }
  ]
}
```

```csharp
[Fact]
public async Task Journey_ComplexBooking_WithAllOptions_20Messages()
{
    var simulator = CreateSimulator(pushName: "Carmen");

    await simulator.UserSays("Buenas! Vamos a celebrar un cumpleaños");
    simulator.ShouldRespond("reserva");

    await simulator.UserSays("Sí, para el sábado que viene");
    simulator.ShouldRespond("personas");

    await simulator.UserSays("Seremos unas 12 personas");
    simulator.ShouldRespond("hora");

    await simulator.UserSays("A las 14:00");
    simulator.ShouldRespond("arroz");

    // Wants multiple rice types
    await simulator.UserSays("Sí, queremos probar varios arroces");
    simulator.ShouldRespond("cuál");

    await simulator.UserSays("Arroz del señoret y paella valenciana");
    simulator.ShouldRespond("raciones");

    await simulator.UserSays("3 raciones de cada uno");
    simulator.ShouldRespond("6");

    // High chairs
    await simulator.UserSays("Perfecto! Llevamos bebés, necesitamos tronas");
    simulator.ShouldRespond("cuántas");

    await simulator.UserSays("2 tronas y espacio para 1 carrito");
    simulator.ShouldRespond("tronas", "carrito");

    // Correction mid-flow
    await simulator.UserSays(
        "Uy espera, en verdad somos 14, se me había olvidado mi cuñada");
    simulator.ShouldRespond("14");
    simulator.ShouldNotMention("12"); // Old value replaced

    await simulator.UserSays("Sí 14, perdona");
    simulator.ShouldRespond("confirmo", "14 personas");

    await simulator.UserSays("Sí, confirma");
    simulator.ShouldRespond("✅", "confirmada");
    simulator.ShouldRespond("señoret", "paella", "tronas");

    await simulator.UserSays("¡Muchísimas gracias!");
    simulator.ShouldRespond("gracias");
}
```

---

## Booking Then Cancel Journey (Steps 295-297)

### Step 295-297: Journey_BookingThenCancel_15Messages
**Type:** Full Journey Test
**Messages:** 15

```json
{
  "name": "journey-booking-cancel",
  "description": "Customer makes booking, then cancels later in same session",
  "scenario": "Plans change, customer needs to cancel",
  "messages": [
    { "turn": 1, "user": "Hola, quiero reservar para domingo 4 personas 14:00", "expect": ["arroz"] },
    { "turn": 2, "user": "Sin arroz", "expect": ["confirmo"] },
    { "turn": 3, "user": "Sí", "expect": ["✅", "confirmada"] },
    { "turn": 4, "user": "Uy, espera, acaba de llamarme mi mujer", "expect": ["ayudar", "dime"] },
    { "turn": 5, "user": "Tengo que cancelar la reserva que acabo de hacer", "expect": ["cancelar", "reserva"] },
    { "turn": 6, "user": "Sí, la de ahora mismo", "expect": ["domingo", "4 personas", "cancelar"] },
    { "turn": 7, "user": "Sí, cancela", "expect": ["✅", "cancelada"] },
    { "turn": 8, "user": "Perdona las molestias", "expect": ["no pasa nada", "pronto"] }
  ]
}
```

```csharp
[Fact]
public async Task Journey_BookingThenCancel_15Messages()
{
    var simulator = CreateSimulator();

    // Make booking
    await simulator.UserSays("Hola, quiero reservar para domingo 4 personas 14:00");
    simulator.ShouldRespond("arroz");

    await simulator.UserSays("Sin arroz");
    await simulator.UserSays("Sí");
    simulator.ShouldRespond("✅", "confirmada");

    // Life happens
    await simulator.UserSays("Uy, espera, acaba de llamarme mi mujer");
    simulator.ShouldRespond("ayudar");

    // Cancel
    await simulator.UserSays("Tengo que cancelar la reserva que acabo de hacer");
    simulator.ShouldRespond("cancelar");

    await simulator.UserSays("Sí, la de ahora mismo");
    simulator.ShouldRespond("domingo", "cancelar");

    await simulator.UserSays("Sí, cancela");
    simulator.ShouldRespond("✅", "cancelada");

    await simulator.UserSays("Perdona las molestias");
    simulator.ShouldRespond("pronto");
}
```

---

## Full Experience 40 Messages (Steps 298-300)

### Step 298-300: Journey_FullExperience_40Messages
**Type:** Full Journey Test
**Messages:** 40

```json
{
  "name": "journey-full-experience",
  "description": "Complete customer experience over extended conversation",
  "scenario": "Discovery, questions, booking, modification, more questions",
  "phases": [
    { "name": "discovery", "messages": 8 },
    { "name": "initial_booking", "messages": 10 },
    { "name": "modification", "messages": 8 },
    { "name": "additional_questions", "messages": 8 },
    { "name": "farewell", "messages": 6 }
  ],
  "messages": [
    { "turn": 1, "user": "Buenas tardes", "expect": ["buenas tardes", "ayudar"] },
    { "turn": 2, "user": "He oído que tenéis muy buenos arroces", "expect": ["paella", "arroz", "gracias"] },
    { "turn": 3, "user": "¿Dónde estáis ubicados?", "expect": ["dirección", "valencia", "alquería"] },
    { "turn": 4, "user": "¿Se puede ir en transporte público?", "expect": ["metro", "bus", "coche"] },
    { "turn": 5, "user": "Vale, ¿y qué arroces tenéis?", "expect": ["paella", "señoret", "negro"] },
    { "turn": 6, "user": "¿Cuál me recomendáis?", "expect": ["paella", "valenciana", "señoret"] },
    { "turn": 7, "user": "¿Y precios?", "expect": ["€", "menú", "precio"] },
    { "turn": 8, "user": "Perfecto, quiero hacer una reserva", "expect": ["día", "cuándo"] },
    { "turn": 9, "user": "Para el próximo sábado", "expect": ["personas"] },
    { "turn": 10, "user": "Somos 5", "expect": ["hora"] },
    { "turn": 11, "user": "¿A qué hora es mejor venir?", "expect": ["13:30", "14:00"] },
    { "turn": 12, "user": "A las 14:00", "expect": ["arroz"] },
    { "turn": 13, "user": "Me habéis convencido, paella valenciana", "expect": ["paella", "raciones"] },
    { "turn": 14, "user": "3 raciones", "expect": ["sábado", "5 personas", "14:00", "paella", "3 raciones"] },
    { "turn": 15, "user": "Confirmo", "expect": ["✅", "confirmada"] },
    { "turn": 16, "user": "Genial!", "expect": ["gracias", "esperamos"] },
    { "turn": 17, "user": "Oye, se me olvidó, necesito una trona", "expect": ["trona", "añadir", "modificar"] },
    { "turn": 18, "user": "Sí, añádela", "expect": ["trona", "añadida", "modificada"] },
    { "turn": 19, "user": "Ah, y mi cuñado también viene, somos 6", "expect": ["6 personas", "actualizada"] },
    { "turn": 20, "user": "Perfecto", "expect": ["confirmo", "6 personas", "trona"] },
    { "turn": 21, "user": "Sí", "expect": ["actualizada", "6 personas"] },
    { "turn": 22, "user": "¿Aceptáis tarjeta?", "expect": ["tarjeta", "efectivo"] },
    { "turn": 23, "user": "¿Hay menú infantil?", "expect": ["niños", "infantil", "menú"] },
    { "turn": 24, "user": "¿Tenéis opciones sin gluten?", "expect": ["gluten", "alergia", "celíaco"] },
    { "turn": 25, "user": "¿Se puede aparcar cerca?", "expect": ["aparcar", "parking"] },
    { "turn": 26, "user": "¿A qué hora cerráis?", "expect": ["17:00", "18:00", "cierre"] },
    { "turn": 27, "user": "¿Se puede hacer reserva privada?", "expect": ["privada", "grupo", "evento"] },
    { "turn": 28, "user": "Para otra vez quizás", "expect": ["perfecto", "cuando quieras"] },
    { "turn": 29, "user": "Bueno pues nada más", "expect": ["algo más", "ayudar"] },
    { "turn": 30, "user": "¿Me puedes confirmar los datos de la reserva?", "expect": ["sábado", "6 personas", "14:00", "paella", "trona"] },
    { "turn": 31, "user": "Todo bien!", "expect": ["esperamos", "sábado"] },
    { "turn": 32, "user": "Muchas gracias por la ayuda", "expect": ["gracias", "placer"] },
    { "turn": 33, "user": "Hasta el sábado", "expect": ["hasta pronto", "sábado"] }
  ]
}
```

```csharp
[Fact]
public async Task Journey_FullExperience_40Messages()
{
    var simulator = CreateSimulator(pushName: "Miguel");

    // === PHASE 1: DISCOVERY (8 messages) ===
    await simulator.UserSays("Buenas tardes");
    simulator.ShouldRespond("buenas tardes");

    await simulator.UserSays("He oído que tenéis muy buenos arroces");
    simulator.ShouldRespond("arroz", "paella");

    await simulator.UserSays("¿Dónde estáis ubicados?");
    simulator.ShouldRespond("dirección");

    await simulator.UserSays("¿Se puede ir en transporte público?");
    // Answer about transport

    await simulator.UserSays("Vale, ¿y qué arroces tenéis?");
    simulator.ShouldRespond("paella", "señoret");

    await simulator.UserSays("¿Cuál me recomendáis?");
    // Recommendation

    await simulator.UserSays("¿Y precios?");
    simulator.ShouldRespond("€");

    // === PHASE 2: INITIAL BOOKING (10 messages) ===
    await simulator.UserSays("Perfecto, quiero hacer una reserva");
    simulator.ShouldRespond("día");

    await simulator.UserSays("Para el próximo sábado");
    simulator.ShouldRespond("personas");

    await simulator.UserSays("Somos 5");
    simulator.ShouldRespond("hora");

    await simulator.UserSays("¿A qué hora es mejor venir?");
    simulator.ShouldRespond("14:00");

    await simulator.UserSays("A las 14:00");
    simulator.ShouldRespond("arroz");

    await simulator.UserSays("Me habéis convencido, paella valenciana");
    simulator.ShouldRespond("raciones");

    await simulator.UserSays("3 raciones");
    simulator.ShouldRespond("confirmo");

    await simulator.UserSays("Confirmo");
    simulator.ShouldRespond("✅", "confirmada");

    await simulator.UserSays("Genial!");
    simulator.ShouldRespond("esperamos");

    // === PHASE 3: MODIFICATION (8 messages) ===
    await simulator.UserSays("Oye, se me olvidó, necesito una trona");
    simulator.ShouldRespond("trona");

    await simulator.UserSays("Sí, añádela");
    simulator.ShouldRespond("añadida");

    await simulator.UserSays("Ah, y mi cuñado también viene, somos 6");
    simulator.ShouldRespond("6 personas");

    await simulator.UserSays("Perfecto");
    simulator.ShouldRespond("confirmo");

    await simulator.UserSays("Sí");
    simulator.ShouldRespond("actualizada");

    // === PHASE 4: ADDITIONAL QUESTIONS (8 messages) ===
    await simulator.UserSays("¿Aceptáis tarjeta?");
    await simulator.UserSays("¿Hay menú infantil?");
    await simulator.UserSays("¿Tenéis opciones sin gluten?");
    await simulator.UserSays("¿Se puede aparcar cerca?");
    await simulator.UserSays("¿A qué hora cerráis?");

    // === PHASE 5: FAREWELL (6 messages) ===
    await simulator.UserSays("¿Me puedes confirmar los datos de la reserva?");
    simulator.ShouldRespond("sábado", "6 personas", "14:00", "paella", "trona");

    await simulator.UserSays("Todo bien!");
    await simulator.UserSays("Muchas gracias por la ayuda");
    await simulator.UserSays("Hasta el sábado");
    simulator.ShouldRespond("hasta pronto");

    // Final count
    simulator.MessageCount.Should().BeGreaterThanOrEqualTo(60);
}
```

---

## Test Infrastructure for Journey Tests

```csharp
public class JourneyTestBase : ConversationFlowTestBase
{
    /// <summary>
    /// Creates simulator with persistence across entire journey
    /// </summary>
    protected ConversationSimulator CreateJourneySimulator(string pushName)
    {
        var simulator = CreateSimulator(pushName);
        simulator.EnablePersistence();
        return simulator;
    }

    /// <summary>
    /// Runs a journey from a JSON script file
    /// </summary>
    protected async Task RunJourneyScript(string scriptPath)
    {
        var script = LoadScript(scriptPath);
        var simulator = CreateJourneySimulator(script.PushName);

        foreach (var message in script.Messages)
        {
            await simulator.UserSays(message.User);

            foreach (var expected in message.Expect)
            {
                simulator.ShouldRespond(expected);
            }

            if (message.NotExpect != null)
            {
                foreach (var notExpected in message.NotExpect)
                {
                    simulator.ShouldNotMention(notExpected);
                }
            }

            if (message.MaxLength.HasValue)
            {
                simulator.ResponseLengthShouldBe(message.MaxLength.Value);
            }
        }

        // Verify journey completed successfully
        simulator.MessageCount.Should().BeGreaterThanOrEqualTo(
            script.Messages.Count * 2);
    }

    private JourneyScript LoadScript(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<JourneyScript>(json);
    }
}

public record JourneyScript
{
    public string Name { get; init; }
    public string Description { get; init; }
    public string PushName { get; init; } = "TestUser";
    public List<JourneyMessage> Messages { get; init; } = new();
}

public record JourneyMessage
{
    public int Turn { get; init; }
    public string User { get; init; }
    public List<string> Expect { get; init; } = new();
    public List<string>? NotExpect { get; init; }
    public int? MaxLength { get; init; }
}
```
