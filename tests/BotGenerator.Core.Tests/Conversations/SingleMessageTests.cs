using BotGenerator.Core.Tests.Infrastructure;
using FluentAssertions;

namespace BotGenerator.Core.Tests.Conversations;

/// <summary>
/// Tests single-message conversation logic (Steps 76-110).
/// Each test sends one message and validates the response.
/// </summary>
public class SingleMessageTests : ConversationFlowTestBase
{
    /// <summary>
    /// Step 76: User says "Hola", bot responds with welcome message.
    /// </summary>
    [Fact]
    public async Task Greeting_Hola_RespondsWelcome()
    {
        // Act
        await Simulator.UserSays("Hola");

        // Assert
        Simulator.ShouldRespond("hola", "ayudar");
        Simulator.ShouldNotMention("reserva", "fecha", "hora");
    }

    /// <summary>
    /// Step 77: User says "Buenos días", bot responds appropriately.
    /// </summary>
    [Fact]
    public async Task Greeting_BuenosDias_RespondsAppropriately()
    {
        // Act
        await Simulator.UserSays("Buenos días");

        // Assert
        Simulator.ShouldRespond("buenos");
        Simulator.ShouldNotMention("error");
    }

    /// <summary>
    /// Step 78: User says "Buenas tardes", bot responds appropriately.
    /// </summary>
    [Fact]
    public async Task Greeting_BuenasTardes_RespondsAppropriately()
    {
        // Act
        await Simulator.UserSays("Buenas tardes");

        // Assert
        Simulator.ShouldRespond("buenas", "ayudar");
        Simulator.ShouldNotMention("error");
    }

    /// <summary>
    /// Step 79: User says "Ey" (casual greeting), bot responds naturally.
    /// </summary>
    [Fact]
    public async Task Greeting_Ey_RespondsNaturally()
    {
        // Act
        await Simulator.UserSays("Ey");

        // Assert
        Simulator.ShouldRespond("hola", "ayudar");
        Simulator.ShouldRespondNaturally();
    }

    /// <summary>
    /// Step 80: User says "Quiero reservar", bot starts booking flow by asking for date.
    /// </summary>
    [Fact]
    public async Task Intent_QuieroReservar_StartsBookingFlow()
    {
        // Act
        await Simulator.UserSays("Quiero reservar");

        // Assert
        // Should ask for date (first missing piece of info)
        Simulator.ShouldRespond("día");
        Simulator.ResponseLengthShouldBe(200); // Short response
    }

    /// <summary>
    /// Step 81: User says "Quiero hacer una reserva", bot starts booking flow.
    /// </summary>
    [Fact]
    public async Task Intent_HacerReserva_StartsBookingFlow()
    {
        // Act
        await Simulator.UserSays("Quiero hacer una reserva");

        // Assert
        // Should ask for booking date
        Simulator.ShouldRespond("día");
        Simulator.ShouldNotMention("error");
    }

    /// <summary>
    /// Step 82: User says "¿Tenéis mesa para el sábado?", bot understands date and asks for next detail.
    /// </summary>
    [Fact]
    public async Task Intent_TeneisMesa_StartsBookingFlow()
    {
        // Act
        await Simulator.UserSays("¿Tenéis mesa para el sábado?");

        // Assert
        // Date understood (sábado), should ask for people or time
        Simulator.ShouldRespond("personas");
        Simulator.ShouldNotMention("error");
    }

    /// <summary>
    /// Step 83: User says "Quiero cancelar mi reserva", bot starts cancellation flow.
    /// </summary>
    [Fact]
    public async Task Intent_CancelarReserva_StartsCancellationFlow()
    {
        // Act
        await Simulator.UserSays("Quiero cancelar mi reserva");

        // Assert
        // Should ask for reservation details
        Simulator.ShouldRespond("reserva");
        Simulator.ShouldNotMention("error");
    }

    /// <summary>
    /// Step 84: User says "Quiero modificar mi reserva", bot starts modification flow.
    /// </summary>
    [Fact]
    public async Task Intent_ModificarReserva_StartsModificationFlow()
    {
        // Act
        await Simulator.UserSays("Quiero modificar mi reserva");

        // Assert
        // Should indicate it will search for/modify the booking
        Simulator.ShouldRespond("reserva");
        Simulator.ShouldNotMention("error");
    }

    /// <summary>
    /// Step 85: User says "Necesito cambiar mi reserva", bot starts modification flow.
    /// </summary>
    [Fact]
    public async Task Intent_CambiarReserva_StartsModificationFlow()
    {
        // Act
        await Simulator.UserSays("Necesito cambiar mi reserva");

        // Assert
        // Alternative phrasing should trigger modification flow
        Simulator.ShouldRespond("reserva");
        Simulator.ShouldNotMention("error");
    }

    /// <summary>
    /// Step 86: User asks about opening hours, bot responds with schedule.
    /// </summary>
    [Fact]
    public async Task Question_Horarios_RespondsWithSchedule()
    {
        // Act
        await Simulator.UserSays("¿Cuál es vuestro horario?");

        // Assert
        // Should list open days and times
        Simulator.ShouldRespond("jueves", "viernes", "sábado", "domingo");
        Simulator.ShouldNotMention("lunes", "martes", "miércoles");
    }

    /// <summary>
    /// Step 87: User asks for location/address, bot responds with location info.
    /// </summary>
    [Fact]
    public async Task Question_Direccion_RespondsWithLocation()
    {
        // Act
        await Simulator.UserSays("¿Dónde estáis?");

        // Assert
        // Should provide location information
        Simulator.ShouldRespond("alquería", "villa carmen");
        Simulator.ShouldNotMention("error");
    }

    /// <summary>
    /// Step 88: User asks for phone number, bot provides contact number.
    /// </summary>
    [Fact]
    public async Task Question_Telefono_RespondsWithPhone()
    {
        // Act
        await Simulator.UserSays("¿Cuál es vuestro teléfono?");

        // Assert
        // Should provide phone number +34 638 857 294
        Simulator.ShouldRespond("638", "857", "294");
        Simulator.ShouldNotMention("error");
    }

    /// <summary>
    /// Step 89: User asks about rice menu, bot lists rice types.
    /// </summary>
    [Fact]
    public async Task Question_MenuArroces_ListsRiceTypes()
    {
        // Act
        await Simulator.UserSays("¿Qué arroces tenéis?");

        // Assert
        // Should list available rice types
        Simulator.ShouldRespond("paella", "arroz");
        Simulator.ShouldRespond("señoret");
        Simulator.ShouldNotMention("error");
    }

    /// <summary>
    /// Step 90: User asks about pricing, bot responds with price information.
    /// </summary>
    [Fact]
    public async Task Question_Precios_RespondsWithPricing()
    {
        // Act
        await Simulator.UserSays("¿Cuánto cuesta el menú?");

        // Assert
        // Should provide pricing info
        Simulator.ShouldRespond("€");
        Simulator.ShouldNotMention("error", "gratis");
    }

    /// <summary>
    /// Step 91: User asks about parking, bot responds with parking availability.
    /// </summary>
    [Fact]
    public async Task Question_Parking_RespondsWithInfo()
    {
        // Act
        await Simulator.UserSays("¿Tenéis parking?");

        // Assert
        // Should mention parking availability (free parking available)
        Simulator.ShouldRespond("parking");
        Simulator.ShouldNotMention("error");
    }

    /// <summary>
    /// Step 92: User asks about large groups, bot responds with policy.
    /// </summary>
    [Fact]
    public async Task Question_GruposGrandes_RespondsWithPolicy()
    {
        // Act
        await Simulator.UserSays("¿Podéis atender grupos grandes?");

        // Assert
        // Should mention policy about large groups (15-20 people requiring advance notice)
        Simulator.ShouldRespond("grupo");
        Simulator.ShouldNotMention("error");
    }

    /// <summary>
    /// Step 93: User asks about high chairs, bot responds with availability.
    /// </summary>
    [Fact]
    public async Task Question_Tronas_RespondsWithAvailability()
    {
        // Act
        await Simulator.UserSays("¿Tenéis tronas para niños?");

        // Assert
        // Should confirm high chairs are available on request
        Simulator.ShouldRespond("trona");
        Simulator.ShouldNotMention("error", "no tenemos");
    }

    /// <summary>
    /// Step 94: User says "el sábado", bot understands as next Saturday.
    /// </summary>
    [Fact]
    public async Task Date_ElSabado_ParsesNextSaturday()
    {
        // Act
        await Simulator.UserSays("Quiero reservar para el sábado");

        // Assert
        // Should understand date and ask for next detail (people or time)
        Simulator.ShouldRespond("persona");
        Simulator.ShouldNotMention("qué día", "fecha", "cuándo");
    }

    /// <summary>
    /// Step 95: User says "el domingo", bot understands as next Sunday.
    /// </summary>
    [Fact]
    public async Task Date_ElDomingo_ParsesNextSunday()
    {
        // Act
        await Simulator.UserSays("Quiero reservar para el domingo");

        // Assert
        // Should understand date and move to next question
        Simulator.ShouldRespond("persona");
        Simulator.ShouldNotMention("qué día", "fecha", "cuándo");
    }

    /// <summary>
    /// Step 96: User says "próximo fin de semana", bot asks to clarify Saturday or Sunday.
    /// </summary>
    [Fact]
    public async Task Date_ProximoFinDeSemana_ParsesCorrectly()
    {
        // Act
        await Simulator.UserSays("Quiero reservar para el próximo fin de semana");

        // Assert
        // Should ask to clarify which day (Saturday or Sunday)
        Simulator.ShouldRespond("sábado", "domingo");
        Simulator.ShouldNotMention("error");
    }

    /// <summary>
    /// Step 97: User says "30 de noviembre", bot parses explicit date.
    /// </summary>
    [Fact]
    public async Task Date_30DeNoviembre_ParsesExplicitDate()
    {
        // Act
        await Simulator.UserSays("Para el 30 de noviembre");

        // Assert
        // Should understand explicit date and ask for next detail (people count)
        Simulator.ShouldRespond("persona");
        Simulator.ShouldNotMention("qué día", "fecha", "cuándo");
    }

    /// <summary>
    /// Step 98: User says "30/11", bot parses short date format.
    /// </summary>
    [Fact]
    public async Task Date_30_11_ParsesShortFormat()
    {
        // Act
        await Simulator.UserSays("Reserva para el 30/11");

        // Assert
        // Should understand dd/MM format and ask for next detail
        Simulator.ShouldRespond("persona");
        Simulator.ShouldNotMention("qué día", "fecha", "cuándo");
    }

    /// <summary>
    /// Step 99: User says "mañana", bot parses as tomorrow.
    /// </summary>
    [Fact]
    public async Task Date_Manana_ParsesTomorrow()
    {
        // Act
        await Simulator.UserSays("Quiero reservar para mañana");

        // Assert
        // Should understand "mañana" and ask for next detail
        // Response should ask for people count (date is understood)
        Simulator.ShouldRespond("persona");
        Simulator.ShouldNotMention("error", "qué día", "fecha", "cuándo");
    }

    /// <summary>
    /// Step 100: User says "a las 14:00", bot parses time correctly.
    /// </summary>
    [Fact]
    public async Task Time_ALas14_ParsesTime()
    {
        // Act
        await Simulator.UserSays("Reserva para el sábado a las 14:00");

        // Assert
        // Should understand both date and time, ask for people count
        Simulator.ShouldRespond("persona");
        Simulator.ShouldNotMention("qué hora", "hora", "cuándo");
    }

    /// <summary>
    /// Step 101: User says "a las dos", bot parses Spanish number word for time (14:00).
    /// </summary>
    [Fact]
    public async Task Time_ALasDos_ParsesSpanishNumber()
    {
        // Act
        await Simulator.UserSays("Reserva para el sábado a las dos");

        // Assert
        // Should understand both date and time (Spanish "dos" = 2pm = 14:00)
        // Should NOT be asking for time again
        Simulator.ShouldNotMention("qué hora", "hora");
        // Should be asking for people count or moving forward in booking flow
        var response = Simulator.LastResponse.ToLower();
        var isAskingForPeopleOrRice = response.Contains("persona") || response.Contains("arroz") || response.Contains("cuántos");
        isAskingForPeopleOrRice.Should().BeTrue($"Bot should ask for people or move to next step, but responded: {Simulator.LastResponse}");
    }

    /// <summary>
    /// Step 102: User says "1430", bot parses time without colon (14:30).
    /// </summary>
    [Fact]
    public async Task Time_1430_ParsesWithoutColon()
    {
        // Act
        await Simulator.UserSays("Reserva para el sábado a las 1430");

        // Assert
        // Should understand 4-digit time format (1430 = 14:30), ask for people count
        Simulator.ShouldRespond("persona");
        Simulator.ShouldNotMention("qué hora", "hora", "cuándo");
    }

    /// <summary>
    /// Step 103: User says "4 personas", bot parses people count correctly.
    /// </summary>
    [Fact]
    public async Task People_4Personas_ParsesCount()
    {
        // Act
        await Simulator.UserSays("Reserva para el sábado para 4 personas");

        // Assert
        // Should understand date and people count, ask for time
        Simulator.ShouldRespond("hora");
        Simulator.ShouldNotMention("cuántas personas", "personas");
    }

    /// <summary>
    /// Step 104: User says "Somos cuatro", bot parses Spanish people count.
    /// </summary>
    [Fact]
    public async Task People_SomosCuatro_ParsesSpanish()
    {
        // Act
        await Simulator.UserSays("Reserva para el sábado, somos cuatro");

        // Assert
        // Should understand date and people count in Spanish (cuatro = 4), ask for time
        Simulator.ShouldRespond("hora");
        Simulator.ShouldNotMention("cuántas personas", "personas");
    }

    /// <summary>
    /// Step 105: User says "Para seis", bot parses people count with "para" prefix.
    /// </summary>
    [Fact]
    public async Task People_ParaSeis_ParsesCount()
    {
        // Act
        await Simulator.UserSays("Reserva para el sábado, para seis");

        // Assert
        // Should understand date and people count (seis = 6), ask for time
        Simulator.ShouldRespond("hora");
        Simulator.ShouldNotMention("cuántas personas", "personas");
    }

    /// <summary>
    /// Step 106: User says "Arroz del señoret", bot validates and asks for servings.
    /// </summary>
    [Fact]
    public async Task Rice_ArrozDelSenoret_Validates()
    {
        // Act
        await Simulator.UserSays("Queremos arroz del señoret");

        // Assert
        // Should validate rice type and ask for number of servings
        Simulator.ShouldRespond("señoret");
        Simulator.ShouldRespond("raciones");
        Simulator.ShouldNotMention("error", "no tenemos");
    }

    /// <summary>
    /// Step 107: User says "Paella valenciana", bot validates the rice type.
    /// </summary>
    [Fact]
    public async Task Rice_PaellaValenciana_Validates()
    {
        // Act
        await Simulator.UserSays("Paella valenciana");

        // Assert
        // Should recognize and validate paella valenciana
        Simulator.ShouldRespond("paella", "valenciana");
        Simulator.ShouldRespond("raciones");
        Simulator.ShouldNotMention("error", "no tenemos");
    }

    /// <summary>
    /// Step 108: User says "Arroz negro", bot validates the rice type.
    /// </summary>
    [Fact]
    public async Task Rice_ArrozNegro_Validates()
    {
        // Act
        await Simulator.UserSays("Arroz negro");

        // Assert
        // Should recognize and validate arroz negro
        Simulator.ShouldRespond("negro");
        Simulator.ShouldRespond("raciones");
        Simulator.ShouldNotMention("error", "no tenemos");
    }

    /// <summary>
    /// Step 109: User says "No queremos arroz", bot accepts no rice option.
    /// </summary>
    [Fact]
    public async Task Rice_NoQuierenArroz_AcceptsNo()
    {
        // Act
        await Simulator.UserSays("No queremos arroz");

        // Assert
        // Should accept "no rice" and proceed without asking for servings
        Simulator.ShouldRespond("perfecto", "sin arroz");
        Simulator.ShouldNotMention("raciones", "error");
    }

    /// <summary>
    /// Step 110: User says unknown rice type, bot asks for clarification.
    /// </summary>
    [Fact]
    public async Task Rice_UnknownType_AsksForClarification()
    {
        // Act
        await Simulator.UserSays("Arroz con pollo");

        // Assert
        // Should indicate this rice type is not available and list valid options
        Simulator.ShouldRespond("no tenemos");
        Simulator.ShouldRespond("disponible");
        // Should list valid rice types
        var response = Simulator.LastResponse.ToLower();
        var hasValidOptions = response.Contains("paella") || response.Contains("señoret") ||
                              response.Contains("negro") || response.Contains("banda") ||
                              response.Contains("fideuá");
        hasValidOptions.Should().BeTrue($"Bot should list valid rice types, but responded: {Simulator.LastResponse}");
    }
}
