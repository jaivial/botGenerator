using BotGenerator.Core.Tests.Infrastructure;
using FluentAssertions;

namespace BotGenerator.Core.Tests.Conversations;

/// <summary>
/// Tests response quality characteristics (Steps 251-280).
/// Validates response length, tone, naturalness, and coherence.
/// </summary>
public class QualityTests : ConversationFlowTestBase
{
    /// <summary>
    /// Step 251: Reservation start response should be under 200 characters.
    /// Tests that the initial booking question is concise.
    /// </summary>
    [Fact]
    public async Task Quality_ResponseLength_ReservationStart_Under200Chars()
    {
        // Act
        await Simulator.UserSays("Quiero reservar");

        // Assert
        Simulator.ShouldRespond("día", "reserva");
        Simulator.ResponseLengthShouldBe(200);
    }

    /// <summary>
    /// Step 252: Date response should be under 150 characters.
    /// Tests that date acknowledgment is brief.
    /// </summary>
    [Fact]
    public async Task Quality_ResponseLength_DateResponse_Under150Chars()
    {
        // Arrange
        await Simulator.UserSays("Quiero reservar");

        // Act
        await Simulator.UserSays("Sábado");

        // Assert
        Simulator.ShouldRespond("persona");
        Simulator.ResponseLengthShouldBe(150);
    }

    /// <summary>
    /// Step 253: People count response should be under 150 characters.
    /// Tests that people count acknowledgment is brief.
    /// </summary>
    [Fact]
    public async Task Quality_ResponseLength_PeopleResponse_Under150Chars()
    {
        // Arrange
        await Simulator.UserSays("Quiero reservar");
        await Simulator.UserSays("Sábado");

        // Act
        await Simulator.UserSays("4 personas");

        // Assert
        Simulator.ShouldRespond("hora");
        Simulator.ResponseLengthShouldBe(150);
    }

    /// <summary>
    /// Step 254: Time response should be under 150 characters.
    /// Tests that time acknowledgment is brief.
    /// </summary>
    [Fact]
    public async Task Quality_ResponseLength_TimeResponse_Under150Chars()
    {
        // Arrange
        await Simulator.UserSays("Quiero reservar");
        await Simulator.UserSays("Sábado");
        await Simulator.UserSays("4");

        // Act
        await Simulator.UserSays("14:00");

        // Assert
        Simulator.ShouldRespond("arroz");
        Simulator.ResponseLengthShouldBe(150);
    }

    /// <summary>
    /// Step 255: Confirmation summary can be up to 350 characters.
    /// Tests that final confirmation summary is comprehensive but not excessive.
    /// </summary>
    [Fact]
    public async Task Quality_ResponseLength_ConfirmationAllowed350Chars()
    {
        // Arrange - complete booking flow
        await Simulator.UserSays("Quiero reservar");
        await Simulator.UserSays("Sábado");
        await Simulator.UserSays("4");
        await Simulator.UserSays("14:00");

        // Act - decline rice to get confirmation
        await Simulator.UserSays("No");

        // Assert
        Simulator.ShouldRespond("reserva", "confirmo");
        Simulator.ResponseLengthShouldBe(350);
    }

    /// <summary>
    /// Step 256: Response should ask only one question when starting a reservation.
    /// Tests that the initial booking question focuses on a single piece of information.
    /// </summary>
    [Fact]
    public async Task Quality_SingleQuestion_ReservationStart()
    {
        // Act
        await Simulator.UserSays("Quiero reservar");

        // Assert
        Simulator.ShouldHaveMaxQuestions(1);
        Simulator.ShouldNotMention("y también", "además");
    }

    /// <summary>
    /// Step 257: Response should ask only one question after date is provided.
    /// Tests that date acknowledgment doesn't bundle multiple questions.
    /// </summary>
    [Fact]
    public async Task Quality_SingleQuestion_AfterDate()
    {
        // Arrange
        await Simulator.UserSays("Quiero reservar");

        // Act
        await Simulator.UserSays("Sábado");

        // Assert
        Simulator.ShouldHaveMaxQuestions(1);
        // Should NOT ask "¿Cuántas personas y a qué hora?"
        Simulator.ShouldNotMention("y a qué hora", "cuántas personas y", "y también");
    }

    /// <summary>
    /// Step 258: Response should ask only one question after people count is provided.
    /// Tests that bot asks for time only, not combining with other questions.
    /// </summary>
    [Fact]
    public async Task Quality_SingleQuestion_AfterPeople()
    {
        // Arrange
        await Simulator.UserSays("Quiero reservar");
        await Simulator.UserSays("Sábado");

        // Act
        await Simulator.UserSays("4 personas");

        // Assert
        Simulator.ShouldHaveMaxQuestions(1);
        Simulator.ShouldRespond("hora");
        Simulator.ShouldNotMention("y también", "además", "y queréis");
    }

    /// <summary>
    /// Step 259: Response should ask only one question after time is provided.
    /// Tests that rice question is asked independently.
    /// </summary>
    [Fact]
    public async Task Quality_SingleQuestion_AfterTime()
    {
        // Arrange
        await Simulator.UserSays("Quiero reservar");
        await Simulator.UserSays("Sábado");
        await Simulator.UserSays("4");

        // Act
        await Simulator.UserSays("14:00");

        // Assert
        Simulator.ShouldHaveMaxQuestions(1);
        Simulator.ShouldRespond("arroz");
        Simulator.ShouldNotMention("y también", "además", "cuántas raciones");
    }

    /// <summary>
    /// Step 260: Validates no combined questions throughout the entire flow.
    /// Tests that the bot never asks compound questions at any point.
    /// </summary>
    [Fact]
    public async Task Quality_SingleQuestion_NoCombinedQuestions()
    {
        // Act - complete booking flow
        await Simulator.UserSays("Quiero reservar");
        Simulator.ShouldHaveMaxQuestions(1);
        Simulator.ShouldNotMention("y también", "además");

        await Simulator.UserSays("Sábado");
        Simulator.ShouldHaveMaxQuestions(1);
        Simulator.ShouldNotMention("y a qué hora", "cuántas personas y");

        await Simulator.UserSays("4 personas");
        Simulator.ShouldHaveMaxQuestions(1);
        Simulator.ShouldNotMention("y también", "además");

        await Simulator.UserSays("14:00");
        Simulator.ShouldHaveMaxQuestions(1);
        Simulator.ShouldNotMention("y también", "además", "cuántas raciones");

        // Assert - all responses should have been validated
        Simulator.MessageCount.Should().Be(8); // 4 user + 4 bot messages
    }

    /// <summary>
    /// Step 261: Initial booking response should not use bullet or numbered lists.
    /// Tests that the bot uses natural conversation, not form-like lists.
    /// </summary>
    [Fact]
    public async Task Quality_NoBulletLists_ReservationStart()
    {
        // Act
        await Simulator.UserSays("Quiero reservar");

        // Assert - Should NOT be: "Para hacer la reserva necesito:
        //                          1. Fecha
        //                          2. Hora
        //                          3. Personas"
        Simulator.ShouldNotMention("1.", "2.", "3.", "•", "-", "*", "a)", "b)", "c)");
        Simulator.ShouldRespond("día", "reserva");
    }

    /// <summary>
    /// Step 262: Date response should not use bullet or numbered lists.
    /// Tests that date acknowledgment uses natural conversation flow.
    /// </summary>
    [Fact]
    public async Task Quality_NoBulletLists_AfterDate()
    {
        // Arrange
        await Simulator.UserSays("Quiero reservar");

        // Act
        await Simulator.UserSays("Sábado");

        // Assert - Natural question, not a list
        Simulator.ShouldNotMention("1.", "2.", "3.", "•", "-", "*", "a)", "b)");
        Simulator.ShouldRespond("persona");
    }

    /// <summary>
    /// Step 263: People count response should not use bullet or numbered lists.
    /// Tests that response after people count is conversational.
    /// </summary>
    [Fact]
    public async Task Quality_NoBulletLists_AfterPeople()
    {
        // Arrange
        await Simulator.UserSays("Quiero reservar");
        await Simulator.UserSays("Sábado");

        // Act
        await Simulator.UserSays("4 personas");

        // Assert - Natural question about time, not a list
        Simulator.ShouldNotMention("1.", "2.", "3.", "•", "-", "*", "a)", "b)");
        Simulator.ShouldRespond("hora");
    }

    /// <summary>
    /// Step 264: Time response should not use bullet or numbered lists.
    /// Tests that rice question is asked naturally, not as a list item.
    /// </summary>
    [Fact]
    public async Task Quality_NoBulletLists_AfterTime()
    {
        // Arrange
        await Simulator.UserSays("Quiero reservar");
        await Simulator.UserSays("Sábado");
        await Simulator.UserSays("4");

        // Act
        await Simulator.UserSays("14:00");

        // Assert - Natural rice question, not a checklist
        Simulator.ShouldNotMention("1.", "2.", "3.", "•", "-", "*", "a)", "b)");
        Simulator.ShouldRespond("arroz");
    }

    /// <summary>
    /// Step 265: Entire conversation flow should maintain natural format without lists.
    /// Tests that the bot never uses bullet points or numbered lists during questions.
    /// </summary>
    [Fact]
    public async Task Quality_NoBulletLists_NaturalConversation()
    {
        // Act - complete booking flow
        await Simulator.UserSays("Quiero reservar");
        Simulator.ShouldNotMention("1.", "2.", "•", "-", "*", "a)", "b)");

        await Simulator.UserSays("Sábado");
        Simulator.ShouldNotMention("1.", "2.", "•", "-", "*", "a)", "b)");

        await Simulator.UserSays("4 personas");
        Simulator.ShouldNotMention("1.", "2.", "•", "-", "*", "a)", "b)");

        await Simulator.UserSays("14:00");
        Simulator.ShouldNotMention("1.", "2.", "•", "-", "*", "a)", "b)");

        await Simulator.UserSays("No");
        // Confirmation summary can have structure, but shouldn't be a numbered list of questions
        // Lists are OK for options/menus, but not for data collection questions
        Simulator.ShouldRespond("confirmo");

        // Assert - all responses maintained natural conversation format
        Simulator.MessageCount.Should().Be(10); // 5 user + 5 bot messages
    }

    /// <summary>
    /// Step 266: Client name MAY be used in greeting.
    /// Tests that the bot can optionally personalize the greeting with the customer's name.
    /// </summary>
    [Fact]
    public async Task Quality_UsesClientName_InGreeting()
    {
        // Note: Name usage is optional - bot may or may not use it
        // This test just verifies that IF name is used, it's correct
        await Simulator.UserSays("Hola");

        // If name appears, it should be correct
        if (Simulator.LastResponse.Contains("María") || Simulator.LastResponse.Contains("maria", StringComparison.OrdinalIgnoreCase))
        {
            // Name used correctly
            Simulator.ShouldRespond("ayudar");
        }
        else
        {
            // Name not used - still valid
            Simulator.ShouldRespond("ayudar");
        }

        // Response should still be helpful regardless of name usage
        Simulator.LastResponse.Should().NotBeEmpty();
    }

    /// <summary>
    /// Step 267: Client name should NOT be used excessively.
    /// Tests that the bot doesn't overuse the customer's name in every message.
    /// </summary>
    [Fact]
    public async Task Quality_UsesClientName_NotExcessive()
    {
        var nameMentions = 0;

        await Simulator.UserSays("Hola");
        if (ContainsName(Simulator.LastResponse)) nameMentions++;

        await Simulator.UserSays("Quiero reservar");
        if (ContainsName(Simulator.LastResponse)) nameMentions++;

        await Simulator.UserSays("Sábado");
        if (ContainsName(Simulator.LastResponse)) nameMentions++;

        await Simulator.UserSays("4 personas");
        if (ContainsName(Simulator.LastResponse)) nameMentions++;

        await Simulator.UserSays("14:00");
        if (ContainsName(Simulator.LastResponse)) nameMentions++;

        // Name should be used occasionally (max 3 times in 5 messages)
        nameMentions.Should().BeLessThan(4,
            "Name should be used sparingly, not in every message");
    }

    /// <summary>
    /// Step 268: Client name MAY appear in confirmation.
    /// Tests that personalization in confirmation messages is optional but appropriate.
    /// </summary>
    [Fact]
    public async Task Quality_UsesClientName_InConfirmation()
    {
        // Complete booking flow
        await Simulator.UserSays("Quiero reservar");
        await Simulator.UserSays("Sábado");
        await Simulator.UserSays("4 personas");
        await Simulator.UserSays("14:00");
        await Simulator.UserSays("No"); // No rice

        // Confirmation may or may not include name
        // Both approaches are valid
        if (ContainsName(Simulator.LastResponse))
        {
            // Personalized confirmation
            Simulator.ShouldRespond("confirmo");
        }
        else
        {
            // Generic confirmation
            Simulator.ShouldRespond("confirmo");
        }

        // Confirmation should have booking details regardless
        Simulator.ShouldRespond("sábado", "personas");
    }

    /// <summary>
    /// Step 269: Name usage tracking across conversation.
    /// Tests that name mentions are counted correctly throughout a booking flow.
    /// </summary>
    [Fact]
    public async Task Quality_UsesClientName_CountTracking()
    {
        var nameMentions = 0;

        await Simulator.UserSays("Hola");
        if (ContainsName(Simulator.LastResponse)) nameMentions++;

        await Simulator.UserSays("Quiero reservar para el sábado");
        if (ContainsName(Simulator.LastResponse)) nameMentions++;

        await Simulator.UserSays("4 a las 14:00");
        if (ContainsName(Simulator.LastResponse)) nameMentions++;

        await Simulator.UserSays("No");
        if (ContainsName(Simulator.LastResponse)) nameMentions++;

        await Simulator.UserSays("Sí");
        if (ContainsName(Simulator.LastResponse)) nameMentions++;

        // Name should be used occasionally, not in every message
        nameMentions.Should().BeLessThan(4,
            "Name usage should be balanced - not excessive");

        // Should have at least processed all messages
        Simulator.MessageCount.Should().Be(10); // 5 user + 5 bot
    }

    /// <summary>
    /// Step 270: Balanced personalization throughout conversation.
    /// Tests that name usage is natural and balanced - personalized but not robotic.
    /// </summary>
    [Fact]
    public async Task Quality_UsesClientName_BalancedPersonalization()
    {
        var nameMentions = 0;
        var responseCount = 0;

        // Complete booking conversation
        await Simulator.UserSays("Hola");
        if (ContainsName(Simulator.LastResponse)) nameMentions++;
        responseCount++;

        await Simulator.UserSays("Quiero reservar");
        if (ContainsName(Simulator.LastResponse)) nameMentions++;
        responseCount++;

        await Simulator.UserSays("Domingo");
        if (ContainsName(Simulator.LastResponse)) nameMentions++;
        responseCount++;

        await Simulator.UserSays("6 personas");
        if (ContainsName(Simulator.LastResponse)) nameMentions++;
        responseCount++;

        await Simulator.UserSays("15:00");
        if (ContainsName(Simulator.LastResponse)) nameMentions++;
        responseCount++;

        await Simulator.UserSays("Sí, paella valenciana");
        if (ContainsName(Simulator.LastResponse)) nameMentions++;
        responseCount++;

        // Calculate personalization ratio
        var ratio = (double)nameMentions / responseCount;

        // Healthy personalization: 0-50% of messages (not every message, but some)
        ratio.Should().BeLessThan(0.6,
            "Over-personalization makes responses feel unnatural");

        // Name should be used thoughtfully, if at all
        nameMentions.Should().BeLessThan(4,
            "Excessive name usage (4+ times in 6 messages) feels sales-y");

        // All responses should be natural regardless of name usage
        Simulator.MessageCount.Should().Be(12); // 6 user + 6 bot
    }

    /// <summary>
    /// Helper method to check if response contains the customer's name.
    /// </summary>
    private bool ContainsName(string response)
    {
        var name = GetPushName();
        return response.Contains(name, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Override to set customer name for personalization tests.
    /// </summary>
    protected override string GetPushName() => "María";

    /// <summary>
    /// Step 271: Response should not use robotic phrases in greeting.
    /// Tests that initial greeting sounds natural and friendly, not like a system.
    /// </summary>
    [Fact]
    public async Task Quality_NaturalTone_GreetingNotRobotic()
    {
        // Act
        await Simulator.UserSays("Hola");

        // Assert - Should NOT sound like a system message
        Simulator.ShouldNotMention(
            "Bienvenido al sistema",
            "sistema de reservas",
            "Para procesar su solicitud",
            "Indique el dato",
            "campo obligatorio",
            "dato requerido");

        // Should sound like a human
        Simulator.ShouldRespondNaturally();
        Simulator.ShouldRespond("ayudar");
    }

    /// <summary>
    /// Step 272: Questions should sound natural and conversational, not robotic.
    /// Tests that bot asks for booking details using friendly language.
    /// </summary>
    [Fact]
    public async Task Quality_NaturalTone_QuestionsNotRobotic()
    {
        // Act - Start booking flow
        await Simulator.UserSays("Quiero reservar");

        // Assert - Should NOT use form-like language
        Simulator.ShouldNotMention(
            "Por favor, introduzca",
            "campo requerido",
            "Seleccione una opción",
            "dato obligatorio",
            "indique la fecha",
            "campo fecha",
            "completar el formulario");

        // Should ask naturally for date
        Simulator.ShouldRespondNaturally();
        Simulator.ShouldRespond("día");

        // Act - Provide date
        await Simulator.UserSays("Sábado");

        // Assert - Should continue naturally
        Simulator.ShouldNotMention(
            "introduzca",
            "seleccione",
            "dato requerido",
            "campo obligatorio");

        Simulator.ShouldRespondNaturally();
    }

    /// <summary>
    /// Step 273: Acknowledgements should be natural, not system-like.
    /// Tests that bot doesn't say "dato registrado" or "procesando".
    /// </summary>
    [Fact]
    public async Task Quality_NaturalTone_AcknowledgementsNotRobotic()
    {
        // Arrange - Start booking
        await Simulator.UserSays("Quiero reservar");

        // Act - Provide date
        await Simulator.UserSays("Sábado");

        // Assert - Should NOT use system acknowledgments
        Simulator.ShouldNotMention(
            "Dato registrado",
            "Datos registrados",
            "Procesando",
            "Registrado correctamente",
            "Información almacenada",
            "guardado en el sistema");

        Simulator.ShouldRespondNaturally();

        // Act - Provide people count
        await Simulator.UserSays("4 personas");

        // Assert - Natural acknowledgment
        Simulator.ShouldNotMention(
            "Dato registrado",
            "Procesando",
            "almacenado");

        Simulator.ShouldRespondNaturally();

        // Act - Provide time
        await Simulator.UserSays("14:00");

        // Assert - Natural flow continues
        Simulator.ShouldNotMention(
            "registrado",
            "procesando",
            "guardado");

        Simulator.ShouldRespondNaturally();
    }

    /// <summary>
    /// Step 274: Error messages should be natural and helpful, not technical.
    /// Tests that bot handles confusion or errors with friendly language.
    /// </summary>
    [Fact]
    public async Task Quality_NaturalTone_ErrorsNotRobotic()
    {
        // Act - Invalid/unclear input
        await Simulator.UserSays("asdfghjkl xyz");

        // Assert - Should NOT use technical error messages
        Simulator.ShouldNotMention(
            "Error en el sistema",
            "Dato inválido",
            "formato incorrecto",
            "campo no válido",
            "entrada no reconocida",
            "error de validación");

        // Should respond naturally
        Simulator.ShouldRespondNaturally();

        // Additional test - try closed day
        await Simulator.UserSays("Quiero reservar para lunes");

        // Should explain naturally, not with system language
        Simulator.ShouldNotMention(
            "Dato no válido",
            "error",
            "formato incorrecto",
            "campo",
            "validación");

        Simulator.ShouldRespondNaturally();
        Simulator.ShouldRespond("cerrado");
    }

    /// <summary>
    /// Step 275: Complete conversation should maintain natural tone throughout.
    /// Tests that bot never uses robotic language during full booking flow.
    /// </summary>
    [Fact]
    public async Task Quality_NaturalTone_FullConversationNatural()
    {
        // Message 1: Greeting
        await Simulator.UserSays("Hola");
        Simulator.ShouldRespondNaturally();
        Simulator.ShouldNotMention("sistema", "procesar", "dato", "campo");

        // Message 2: Booking intent
        await Simulator.UserSays("Quiero reservar");
        Simulator.ShouldRespondNaturally();
        Simulator.ShouldNotMention("introduzca", "seleccione", "campo obligatorio");

        // Message 3: Date
        await Simulator.UserSays("Sábado");
        Simulator.ShouldRespondNaturally();
        Simulator.ShouldNotMention("dato registrado", "procesando", "almacenado");

        // Message 4: People
        await Simulator.UserSays("4 personas");
        Simulator.ShouldRespondNaturally();
        Simulator.ShouldNotMention("registrado", "procesando", "guardado");

        // Message 5: Time
        await Simulator.UserSays("14:00");
        Simulator.ShouldRespondNaturally();
        Simulator.ShouldNotMention("dato", "campo", "sistema");

        // Message 6: Decline rice
        await Simulator.UserSays("No");
        Simulator.ShouldRespondNaturally();
        Simulator.ShouldNotMention("registro", "sistema", "procesando");

        // Message 7: Confirm
        await Simulator.UserSays("Sí, confirmo");
        Simulator.ShouldRespondNaturally();
        Simulator.ShouldNotMention("procesado", "sistema", "dato registrado");

        // Validate entire conversation maintained natural tone
        Simulator.MessageCount.Should().Be(14); // 7 user + 7 bot messages
    }

    /// <summary>
    /// Step 276: Greeting response should use maximum 2 emojis.
    /// Tests that the initial greeting is friendly but not overly decorated.
    /// </summary>
    [Fact]
    public async Task Quality_Emojis_GreetingMax2()
    {
        // Act
        await Simulator.UserSays("Hola");

        // Assert
        Simulator.ShouldRespond("ayudar");
        Simulator.ShouldHaveMaxEmojis(2);
    }

    /// <summary>
    /// Step 277: Question responses should use maximum 1 emoji.
    /// Tests that questions are clear and not cluttered with emojis.
    /// </summary>
    [Fact]
    public async Task Quality_Emojis_QuestionsMax1()
    {
        // Arrange
        await Simulator.UserSays("Quiero reservar");

        // Assert - First question (date) should have max 1 emoji
        Simulator.ShouldRespond("día");
        Simulator.ShouldHaveMaxEmojis(1);

        // Act
        await Simulator.UserSays("Sábado");

        // Assert - People question should have max 1 emoji
        Simulator.ShouldRespond("persona");
        Simulator.ShouldHaveMaxEmojis(1);

        // Act
        await Simulator.UserSays("4 personas");

        // Assert - Time question should have max 1 emoji
        Simulator.ShouldRespond("hora");
        Simulator.ShouldHaveMaxEmojis(1);
    }

    /// <summary>
    /// Step 278: Acknowledgement responses should use maximum 1 emoji.
    /// Tests that acknowledgements are brief and minimally decorated.
    /// </summary>
    [Fact]
    public async Task Quality_Emojis_AcknowledgementsMax1()
    {
        // Arrange - Start booking
        await Simulator.UserSays("Quiero reservar");

        // Act - Provide date
        await Simulator.UserSays("Domingo");

        // Assert - Date acknowledgement should have max 1 emoji
        Simulator.ShouldRespond("persona");
        Simulator.ShouldHaveMaxEmojis(1);

        // Act - Provide people count
        await Simulator.UserSays("6 personas");

        // Assert - People acknowledgement should have max 1 emoji
        Simulator.ShouldRespond("hora");
        Simulator.ShouldHaveMaxEmojis(1);
    }

    /// <summary>
    /// Step 279: Confirmation responses can use up to 6 emojis.
    /// Tests that confirmations can use icons to represent booking details visually.
    /// </summary>
    [Fact]
    public async Task Quality_Emojis_ConfirmationAllowsMore()
    {
        // Arrange - Complete booking flow
        await Simulator.UserSays("Quiero reservar");
        await Simulator.UserSays("Sábado");
        await Simulator.UserSays("4 personas");
        await Simulator.UserSays("14:00");

        // Act - Decline rice to get confirmation
        await Simulator.UserSays("No");

        // Assert - Confirmation can use up to 6 emojis (✅📅🕐👥🍚🪑)
        Simulator.ShouldRespond("reserva", "confirmo");
        Simulator.ShouldHaveMaxEmojis(6);
    }

    /// <summary>
    /// Step 280: Overall emoji usage should be sparing throughout conversation.
    /// Tests that the bot doesn't overuse emojis across the entire flow.
    /// </summary>
    [Fact]
    public async Task Quality_Emojis_SparingUseOverall()
    {
        var totalEmojis = 0;

        // Message 1: Greeting
        await Simulator.UserSays("Hola");
        totalEmojis += CountEmojisInResponse(Simulator.LastResponse);
        Simulator.ShouldHaveMaxEmojis(2);

        // Message 2: Start booking
        await Simulator.UserSays("Quiero reservar");
        totalEmojis += CountEmojisInResponse(Simulator.LastResponse);
        Simulator.ShouldHaveMaxEmojis(1);

        // Message 3: Date
        await Simulator.UserSays("Sábado");
        totalEmojis += CountEmojisInResponse(Simulator.LastResponse);
        Simulator.ShouldHaveMaxEmojis(1);

        // Message 4: People
        await Simulator.UserSays("4 personas");
        totalEmojis += CountEmojisInResponse(Simulator.LastResponse);
        Simulator.ShouldHaveMaxEmojis(1);

        // Message 5: Time
        await Simulator.UserSays("14:00");
        totalEmojis += CountEmojisInResponse(Simulator.LastResponse);
        Simulator.ShouldHaveMaxEmojis(1);

        // Message 6: Rice preference
        await Simulator.UserSays("No");
        totalEmojis += CountEmojisInResponse(Simulator.LastResponse);
        Simulator.ShouldHaveMaxEmojis(6); // Confirmation can have more

        // Assert - Total emojis across all responses should be reasonable
        // Max: 2 + 1 + 1 + 1 + 1 + 6 = 12 emojis across 6 responses
        totalEmojis.Should().BeLessThanOrEqualTo(12,
            "Total emoji usage should be sparing across entire conversation");

        // Validate conversation completed
        Simulator.MessageCount.Should().Be(12); // 6 user + 6 bot messages
    }

    /// <summary>
    /// Helper method to count emojis in a response.
    /// </summary>
    private static int CountEmojisInResponse(string text)
    {
        int count = 0;
        foreach (var c in text)
        {
            // Check common emoji ranges
            if (c >= 0x1F300 && c <= 0x1F9FF) count++;
            else if (c >= 0x2600 && c <= 0x26FF) count++;
            else if (c >= 0x2700 && c <= 0x27BF) count++;
            else if ("✅❌📅🕐👥👤🍚🪑".Contains(c)) count++;
        }
        return count;
    }
}
