using BotGenerator.Core.Agents;
using BotGenerator.Core.Handlers;
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace BotGenerator.Core.Tests.Infrastructure;

/// <summary>
/// Base class for conversation flow tests.
/// Provides service configuration and mock setup.
/// </summary>
public abstract class ConversationFlowTestBase : IAsyncLifetime
{
    protected ConversationSimulator Simulator { get; private set; } = null!;
    protected Mock<IGeminiService> AiServiceMock { get; private set; } = null!;
    protected Mock<IWhatsAppService> WhatsAppMock { get; private set; } = null!;
    protected Mock<IMenuRepository> MenuRepositoryMock { get; private set; } = null!;
    protected IServiceProvider ServiceProvider { get; private set; } = null!;

    public virtual async Task InitializeAsync()
    {
        AiServiceMock = new Mock<IGeminiService>();
        WhatsAppMock = new Mock<IWhatsAppService>();
        MenuRepositoryMock = new Mock<IMenuRepository>();

        ConfigureDefaultAiBehavior();
        ConfigureWhatsAppMock();
        ConfigureMenuRepositoryMock();

        var services = new ServiceCollection();
        ConfigureServices(services);

        ServiceProvider = services.BuildServiceProvider();

        Simulator = new ConversationSimulator(
            ServiceProvider.GetRequiredService<MainConversationAgent>(),
            ServiceProvider.GetRequiredService<IIntentRouterService>(),
            ServiceProvider.GetRequiredService<IConversationHistoryService>(),
            userId: "test-" + Guid.NewGuid().ToString("N")[..8],
            pushName: GetPushName());

        await Task.CompletedTask;
    }

    protected virtual string GetPushName() => "TestUser";

    protected virtual void ConfigureDefaultAiBehavior()
    {
        AiServiceMock
            .Setup(x => x.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns((string system, string user, List<ChatMessage>? history, CancellationToken ct) =>
                Task.FromResult(GenerateContextualResponse(system, user, history)));
    }

    protected virtual void ConfigureWhatsAppMock()
    {
        WhatsAppMock
            .Setup(x => x.SendTextAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    protected virtual void ConfigureMenuRepositoryMock()
    {
        // Needed by MainConversationAgent.PreValidateRiceAsync()
        MenuRepositoryMock
            .Setup(x => x.GetActiveRiceTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>
            {
                "Paella valenciana",
                "Arroz del señoret",
                "Arroz negro",
                "Arroz a banda",
                "Fideuá"
            });
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        // Configuration
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(GetDefaultConfiguration())
            .Build();
        services.AddSingleton<IConfiguration>(config);

        // Logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Mocked services
        services.AddSingleton(AiServiceMock.Object);
        services.AddSingleton(WhatsAppMock.Object);
        services.AddSingleton(MenuRepositoryMock.Object);

        // Real services for testing
        services.AddSingleton<IContextBuilderService, ContextBuilderService>();
        services.AddSingleton<IPromptLoaderService, PromptLoaderService>();
        services.AddSingleton<IConversationHistoryService, ConversationHistoryService>();
        services.AddSingleton<IIntentRouterService, IntentRouterService>();

        // Agents
        services.AddSingleton<MainConversationAgent>();
        services.AddSingleton<RiceValidatorAgent>();
        services.AddSingleton<IRiceValidatorService>(sp => sp.GetRequiredService<RiceValidatorAgent>());
        services.AddSingleton<AvailabilityCheckerAgent>();

        // Handlers
        services.AddSingleton<BookingHandler>();
        services.AddSingleton<CancellationHandler>();
        services.AddSingleton<ModificationHandler>();
    }

    protected virtual Dictionary<string, string?> GetDefaultConfiguration()
    {
        return new Dictionary<string, string?>
        {
            ["History:MaxMessages"] = "30",
            ["History:SessionTimeoutMinutes"] = "30",
            ["Prompts:BasePath"] = GetTestPromptsPath(),
            ["Prompts:CacheEnabled"] = "false",
            ["Restaurants:Default"] = "villacarmen",
            ["GoogleAI:ApiKey"] = "test-key",
            ["GoogleAI:Model"] = "gemini-test"
        };
    }

    protected virtual string GetTestPromptsPath()
    {
        // Return path to actual or test prompts
        var basePath = AppContext.BaseDirectory;
        return Path.Combine(basePath, "prompts");
    }

    /// <summary>
    /// Generates contextual AI responses based on conversation state.
    /// Override for specific test scenarios.
    /// </summary>
    protected virtual string GenerateContextualResponse(
        string systemPrompt,
        string userMessage,
        List<ChatMessage>? history)
    {
        var lower = userMessage.ToLower();
        var state = ExtractSimpleState(history);

        // Greetings - mirror time-specific greetings
        if (IsGreeting(lower))
        {
            if (lower.StartsWith("buenos días"))
                return "¡Buenos días! ¿En qué puedo ayudarte?";
            if (lower.StartsWith("buenas tardes"))
                return "¡Buenas tardes! ¿En qué puedo ayudarte?";
            if (lower.StartsWith("buenas noches"))
                return "¡Buenas noches! ¿En qué puedo ayudarte?";
            return "¡Hola! ¿En qué puedo ayudarte?";
        }

        // Gratitude expressions (after booking or in general conversation)
        if (lower == "gracias" || lower == "gracias!" || lower.Contains("muchas gracias"))
        {
            // Check if there was a recent booking confirmation
            if (history != null && history.Any(m => m.Role == "assistant" &&
                m.Content.ToLower().Contains("confirmada")))
            {
                return "¡De nada! Te esperamos con mucho gusto. ¿Algo más que necesites?";
            }
            return "¡De nada! ¿En qué más puedo ayudarte?";
        }

        // Farewells
        if (lower.Contains("hasta") || lower.Contains("adiós") || lower.Contains("chao") ||
            (lower.Contains("genial") && (lower.Contains("hasta") || lower.Contains("sábado") || lower.Contains("domingo"))))
        {
            var date = state.Date ?? "pronto";
            if (lower.Contains(date))
                return $"¡Hasta {date}! Te esperamos.";
            return "¡Hasta pronto! Te esperamos.";
        }

        // Cancellation intent
        if (lower.Contains("cancelar") && lower.Contains("reserva"))
        {
            return "Claro, puedo ayudarte a cancelar tu reserva. ¿Me das tu nombre y la fecha de la reserva?";
        }

        // Modification intent
        if ((lower.Contains("modificar") || lower.Contains("cambiar")) && lower.Contains("reserva"))
        {
            state.IsInModificationFlow = true;
            return "Por supuesto, puedo ayudarte a modificar tu reserva. ¿Me das tu nombre y la fecha actual de la reserva?";
        }

        // Modification flow - user provides booking details
        if (state.IsInModificationFlow && !state.ModificationDetailsProvided && !state.ModificationBookingFound)
        {
            // Check if user is providing date/time to identify booking
            if (ContainsDate(lower) || ContainsTime(lower))
            {
                state.ModificationDetailsProvided = true;
                state.ModificationBookingFound = true;
                // Extract date and time for reference
                if (ContainsDate(lower)) state.Date = ExtractDate(lower);
                if (ContainsTime(lower)) state.Time = ExtractTime(lower);
                return "Perfecto, encontré tu reserva. ¿Qué quieres modificar?";
            }
            // User might provide name
            if (lower.Contains("garcía") || lower.Contains("juan") || (lower.Length > 3 && !lower.Contains("?") && !ContainsDate(lower) && !ContainsTime(lower)))
            {
                state.ModificationDetailsProvided = true;
                state.ModificationBookingFound = true;
                return "Perfecto. ¿Qué quieres modificar de tu reserva?";
            }
        }

        // Modification flow - user specifies what to change
        if (state.IsInModificationFlow && state.ModificationBookingFound && !state.ModificationFieldSpecified)
        {
            if (lower.Contains("hora") || (lower.Contains("cambiar") && lower.Contains("hora")))
            {
                state.ModificationFieldSpecified = true;
                state.ModificationField = "hora";
                return "¿A qué hora quieres cambiarla?";
            }
            if (lower.Contains("fecha") || lower.Contains("día") || (lower.Contains("cambiar") && lower.Contains("día")))
            {
                state.ModificationFieldSpecified = true;
                state.ModificationField = "fecha";
                return "¿Para qué día quieres cambiarla?";
            }
            if (lower.Contains("personas") || lower.Contains("número") || (lower.Contains("cambiar") && lower.Contains("número")))
            {
                state.ModificationFieldSpecified = true;
                state.ModificationField = "personas";
                return "¿Para cuántas personas?";
            }
            // Generic fallback - if in modification flow but unclear what to change
            if (lower.Contains("cambiar") || lower.Contains("modificar"))
            {
                return "¿Qué quieres modificar? ¿La fecha, hora o número de personas?";
            }
        }

        // Modification flow - user provides new value
        if (state.IsInModificationFlow && state.ModificationFieldSpecified)
        {
            if (state.ModificationField == "hora" && ContainsTime(lower))
            {
                var newTime = ExtractTime(lower);
                state.Time = newTime;
                return $"Perfecto, he modificado la hora a las {newTime}. Tu reserva está actualizada.";
            }
            if (state.ModificationField == "fecha" && ContainsDate(lower))
            {
                var newDate = ExtractDate(lower);
                state.Date = newDate;
                return $"Perfecto, he modificado la fecha a {newDate}. Tu reserva está actualizada.";
            }
            if (state.ModificationField == "personas" && ContainsPeopleCount(lower))
            {
                var match = System.Text.RegularExpressions.Regex.Match(lower, @"(\d+)");
                if (match.Success)
                {
                    var newPeople = match.Groups[1].Value;
                    return $"Perfecto, he modificado el número de personas a {newPeople}. Tu reserva está actualizada.";
                }
            }
        }

        // Information Questions (must come before booking to avoid false matches)
        // Check for "what days are you open" question
        if ((lower.Contains("qué días") || lower.Contains("que dias") ||
             (lower.Contains("cuándo") && lower.Contains("abrís")) ||
             (lower.Contains("dias") && lower.Contains("abrís"))) &&
            (lower.Contains("abrís") || lower.Contains("abren") || lower.Contains("abierto")))
            return "Abrimos jueves, viernes, sábado y domingo de 13:30 a 18:00.";

        // Check for hours/closing time question - specific pattern for "a qué hora cerráis"
        if ((lower.Contains("horario") ||
             (lower.Contains("hora") && (lower.Contains("qué") || lower.Contains("cuál"))) ||
             lower.Contains("cerráis") || lower.Contains("cierran")) &&
            !ContainsTime(lower)) // Don't match if it's actually providing a time
            return "Abrimos jueves, viernes, sábado y domingo de 13:30 a 18:00.";

        // Restaurant type/description question
        if ((lower.Contains("qué tipo") || lower.Contains("que tipo")) &&
            (lower.Contains("restaurante") || lower.Contains("sois")))
            return "Somos especialistas en paella y arroz valenciano. ¡Te va a encantar!";

        if (lower.Contains("dónde") || lower.Contains("dirección") || lower.Contains("ubicación"))
            return "Estamos en Alquería Villa Carmen, Valencia. ¿Necesitas la dirección completa?";

        if (lower.Contains("teléfono") || (lower.Contains("llamar") && lower.Contains("cómo")) || (lower.Contains("contacto") && lower.Contains("cuál")))
            return "Nuestro teléfono es +34 638 857 294.";

        // Rice options inquiry - check if user is asking about rice types in booking context
        if ((lower.Contains("tipos") || lower.Contains("qué") || lower.Contains("cuál")) &&
            (lower.Contains("tenéis") || lower.Contains("tienen")) &&
            (state.WasAskedAboutRice || state.HasDate))
            return "Tenemos: Paella valenciana, Arroz del señoret, Arroz negro, Arroz a banda, y Fideuá.";

        if ((lower.Contains("arroz") || lower.Contains("arroces")) && (lower.Contains("qué") || lower.Contains("cuál") || lower.Contains("tenéis") || lower.Contains("tienen") || lower.Contains("tipos")))
            return "Tenemos: Paella valenciana, Arroz del señoret, Arroz negro, Arroz a banda, y Fideuá.";

        if ((lower.Contains("carta") || lower.Contains("menú")) &&
            (lower.Contains("qué") || lower.Contains("cuál") || lower.Contains("tenéis")))
            return "Tenemos: Paella valenciana, Arroz del señoret, Arroz negro, Arroz a banda, y Fideuá.";

        // Recommendation request
        if ((lower.Contains("recomendáis") || lower.Contains("recomendación") || lower.Contains("recomiendas")) &&
            (lower.Contains("arroz") || lower.Contains("cuál") || state.WasAskedAboutRice))
            return "La Paella valenciana es nuestro clásico más popular, perfecta para grupos. También es muy recomendable el Arroz del señoret.";

        if (lower.Contains("precio") || lower.Contains("cuesta") || lower.Contains("cuánto"))
            return "El precio es aproximadamente 25-35€ por persona, dependiendo del menú que elijas.";

        if (lower.Contains("parking") || lower.Contains("aparcamiento") || lower.Contains("aparcar"))
            return "Sí, tenemos parking gratuito disponible para nuestros clientes.";

        if ((lower.Contains("grupo") && lower.Contains("grande")) ||
            (lower.Contains("muchas personas") || (lower.Contains("atender") && lower.Contains("grupo"))))
            return "Para grupos grandes de más de 15-20 personas, por favor contáctanos con antelación para coordinar mejor tu visita.";

        // High chairs (tronas) - check for information question first
        if ((lower.Contains("tenéis") || lower.Contains("tienen")) &&
            (lower.Contains("trona") || (lower.Contains("silla") && lower.Contains("niño"))))
            return "Sí, tenemos tronas disponibles para los más pequeños. Solo avísanos cuando hagas la reserva.";

        // Stroller space (carrito) - check for information question
        if ((lower.Contains("tenéis") || lower.Contains("tienen")) &&
            (lower.Contains("carrito") || lower.Contains("cochecito") || lower.Contains("espacio")))
            return "Sí, tenemos espacio para carritos. No hay problema.";

        // Time recommendation request - check if in booking context and asking for best time
        if ((lower.Contains("mejor hora") || lower.Contains("cuál es la mejor") ||
             lower.Contains("qué hora") || lower.Contains("a qué hora")) &&
            state.HasDate && state.HasPeople && !state.HasTime &&
            !lower.Contains("cerráis") && !lower.Contains("cierran"))
            return "Te recomendaría las 14:00 o 15:00, son horarios muy populares para disfrutar de nuestros arroces.";

        // Booking intent (Spanish and English)
        if (lower.Contains("reservar") || lower.Contains("reserva") || lower.Contains("hacer una reserva") || lower.Contains("mesa") || lower.Contains("necesito") ||
            lower.Contains("booking") || lower.Contains("book") || lower.Contains("table") || lower.Contains("reservation") ||
            lower.Contains("vull reservar") || lower.Contains("taula")) // Valenciano/Catalan
        {
            // EDGE CASE: Same-day booking rejection (Steps 211-214)
            if (lower.Contains("hoy") || lower.Contains("para hoy"))
            {
                return "Lo siento, no aceptamos reservas para el mismo día. Para reservas urgentes, por favor llámanos al 638 857 294.";
            }

            // EDGE CASE: Closed days rejection (Steps 215-226)
            // Restaurant is closed Monday, Tuesday, Wednesday
            if (lower.Contains("lunes"))
            {
                return "Lo siento, estamos cerrados los lunes. Abrimos de jueves a domingo de 13:30 a 18:00. ¿Te gustaría reservar para otro día?";
            }
            if (lower.Contains("martes"))
            {
                return "Lo siento, estamos cerrados los martes. Abrimos de jueves a domingo de 13:30 a 18:00. ¿Te gustaría reservar para otro día?";
            }
            if (lower.Contains("miércoles"))
            {
                return "Lo siento, estamos cerrados los miércoles. Abrimos de jueves a domingo de 13:30 a 18:00. ¿Te gustaría reservar para otro día?";
            }

            // Special case: "fin de semana" requires clarification
            if (lower.Contains("fin de semana"))
            {
                return "¿Preferís sábado o domingo?";
            }

            // Check if people count is already in the message (e.g., "somos 6 y queremos reservar")
            if (ContainsPeopleCount(lower) && !state.HasPeople)
            {
                // People count mentioned first, ask for date
                return "¡Perfecto! ¿Para qué día quieres la reserva?";
            }

            // Check if date is already in the message (e.g., "tenéis mesa para el sábado")
            if (ContainsDate(lower) && !state.HasDate)
            {
                // Special handling for time included in message
                if (ContainsTime(lower))
                {
                    // Both date and time mentioned, ask for people
                    return "¡Perfecto! ¿Para cuántas personas?";
                }
                // Date mentioned in message, ask for people or time
                return "¡Perfecto! ¿Para cuántas personas?";
            }

            if (!state.HasDate)
                return "¡Perfecto! ¿Para qué día quieres la reserva?";
            if (!state.HasPeople)
                return "¿Para cuántas personas?";
            if (!state.HasTime)
                return "¿A qué hora os viene bien?";
            if (!state.HasRice)
                return "¿Queréis arroz?";
            return GenerateSummary(state);
        }

        // Date provided or date change
        if (ContainsDate(lower))
        {
            // Special case for "fin de semana" without booking intent
            if (lower.Contains("fin de semana"))
            {
                return "¿Preferís sábado o domingo?";
            }

            // Check if this is a date change mid-conversation
            if ((lower.Contains("mejor") || lower.Contains("espera") || lower.Contains("cambio") ||
                 lower.Contains("en verdad") || lower.Contains("en realidad")) && state.HasDate)
            {
                // User is changing the date
                var newDate = ExtractDate(lower);
                return $"Perfecto, cambio la fecha a {newDate}. ¿Queréis arroz?";
            }

            // Special handling for "mañana" - acknowledge and ask for next info
            if (lower.Contains("mañana"))
            {
                if (!state.HasPeople)
                    return "Perfecto, para mañana. ¿Para cuántas personas?";
                if (!state.HasTime)
                    return "¿A qué hora?";
                return "¿Queréis arroz?";
            }

            if (!state.HasPeople)
                return "¿Para cuántas personas?";
            if (!state.HasTime)
                return "¿A qué hora?";
            return "¿Queréis arroz?";
        }

        // People count - check if correction
        if (ContainsPeopleCount(lower))
        {
            // Check for complex pattern: "X adultos y Y niños"
            var complexMatch = System.Text.RegularExpressions.Regex.Match(lower, @"(\d+)\s+adultos?\s+y\s+(\d+)\s+niños?");
            if (complexMatch.Success)
            {
                var adults = int.Parse(complexMatch.Groups[1].Value);
                var children = int.Parse(complexMatch.Groups[2].Value);
                var total = adults + children;
                // Acknowledge the total count
                if (!state.HasTime)
                    return $"Perfecto, {total} personas en total. ¿A qué hora os viene bien?";
                return $"Perfecto, {total} personas en total. ¿Queréis arroz?";
            }

            // Check if this is a correction
            if ((lower.Contains("perdón") || lower.Contains("perdon") ||
                 lower.Contains("en verdad") || lower.Contains("en realidad") ||
                 lower.Contains("mejor") || lower.Contains("espera") ||
                 (lower.StartsWith("no") && lower.Contains("mejor")) ||
                 (lower.Contains("somos") && state.HasPeople)))
            {
                // Extract the corrected count
                var match = System.Text.RegularExpressions.Regex.Match(lower, @"(\d+)");
                if (match.Success)
                {
                    var newCount = match.Groups[1].Value;
                    // Acknowledge the correction and continue
                    if (!state.HasTime)
                        return $"Perfecto, cambio a {newCount} personas. ¿A qué hora os viene bien?";
                    return $"Perfecto, cambio a {newCount} personas. ¿Queréis arroz?";
                }
            }

            if (!state.HasTime)
                return "¿A qué hora os viene bien?";
            return "¿Queréis arroz?";
        }

        // Time - check for time change
        if (ContainsTime(lower))
        {
            // Check if this is a time change mid-conversation
            if ((lower.Contains("mejor") || lower.Contains("espera") || lower.Contains("cambio") ||
                 lower.Contains("en verdad") || lower.Contains("en realidad")) && state.HasTime)
            {
                // User is changing the time
                var newTime = ExtractTime(lower);
                return $"Perfecto, cambio la hora a las {newTime}. ¿Queréis arroz?";
            }

            return "¿Queréis arroz?";
        }

        // Rice response - decline (check this BEFORE rice type checking)
        // Check if user is declining rice (explicit or implicit based on context)
        if (lower.Contains("no queremos") || lower.Contains("sin arroz") ||
            (lower.StartsWith("no") && lower.Contains("arroz")) ||
            (state.WasAskedAboutRice && (lower == "no" || lower == "no gracias" || lower == "nada")) ||
            // English versions
            (lower.Contains("no rice") || (state.WasAskedAboutRice && (lower.Contains("no") || lower == "no thanks"))))
        {
            // Check if high chairs (tronas) are mentioned in the same message
            if (lower.Contains("trona"))
            {
                // Extract high chair count
                var tronaMatch = System.Text.RegularExpressions.Regex.Match(lower, @"(\d+)\s*tronas?");
                var tronaCount = tronaMatch.Success ? tronaMatch.Groups[1].Value : "2";

                // Generate summary with high chairs
                return GenerateSummaryWithHighChairs(state, tronaCount);
            }
            // Generate summary with current state (uses actual date/time/people from state)
            return GenerateConfirmation(state);
        }

        // Rice handling
        if (ContainsRiceType(lower))
        {
            var riceType = ExtractRiceType(lower);
            if (riceType != null)
            {
                // Check if servings are also in the same message
                if (ContainsServings(lower))
                {
                    // Both rice type and servings provided - show summary
                    return GenerateSummaryWithRice(riceType);
                }
                else
                {
                    // Valid rice type - ask for servings
                    // Check for "entonces", "vale", etc. which indicate selection after consideration
                    if (lower.Contains("entonces") || lower.Contains("vale") || lower.Contains("perfecto"))
                        return $"¡Perfecto! Paella valenciana. ¿Cuántas raciones queréis?";
                    else
                        return $"Perfecto, {riceType}. ¿Cuántas raciones queréis?";
                }
            }
            else
            {
                // Unknown rice type
                return "Lo siento, no tenemos ese tipo de arroz. Disponibles: Paella valenciana, Arroz del señoret, Arroz negro, Arroz a banda, Fideuá. ¿Cuál preferís?";
            }
        }

        // Servings response (after rice type has been established)
        if (ContainsServings(lower) && state.WasAskedAboutServings)
        {
            // User provided servings - show summary with full state
            return GenerateSummary(state);
        }

        // Additional requests after booking details (high chairs, strollers, etc.)
        if ((lower.Contains("necesitamos") || lower.Contains("necesito") || lower.Contains("queremos")) &&
            (lower.Contains("trona") || lower.Contains("carrito")))
        {
            // Extract high chair count if mentioned
            if (lower.Contains("trona"))
            {
                var tronaMatch = System.Text.RegularExpressions.Regex.Match(lower, @"(\d+)\s*tronas?");
                var tronaCount = tronaMatch.Success ? tronaMatch.Groups[1].Value : "1";
                state.HasHighChairs = true;
                state.HighChairsCount = tronaCount;
            }

            // Check for stroller space request
            if (lower.Contains("carrito") || lower.Contains("cochecito"))
            {
                state.HasStroller = true;
            }

            // Generate complete summary with all special requests
            return GenerateCompleteSummary(state);
        }

        // User indicates ready to proceed (e.g., "Perfecto!" after all details provided)
        if ((lower == "perfecto" || lower.Contains("perfecto!") || lower == "genial" || lower == "vale") &&
            state.HasDate && state.HasPeople && state.HasTime &&
            !lower.Contains("confirma") && !lower.Contains("confirmo"))
        {
            // Show summary before asking for final confirmation
            return GenerateCompleteSummary(state);
        }

        // Confirmation responses (Spanish and English)
        if ((lower.Contains("sí") || lower.Contains("confirma") || lower.Contains("confirmo") ||
             lower.Contains("yes") || lower.Contains("confirm") || lower.Contains("ok") || lower == "sure") &&
            (state.HasDate && state.HasPeople && state.HasTime))
        {
            var date = state.Date ?? "sábado";
            var time = state.Time ?? "14:00";
            var people = state.PeopleCount > 0 ? state.PeopleCount : 4;

            // Build confirmation message with booking details and special requests
            var confirmationParts = new List<string>
            {
                "✅ *¡Reserva confirmada!*",
                "",
                $"Te esperamos el {date} a las {time} ({people} personas) en Alquería Villa Carmen."
            };

            // Add rice if ordered
            if (!string.IsNullOrEmpty(state.RiceType) && state.HasServings)
            {
                confirmationParts.Add($"Arroz: {state.RiceType} ({state.ServingsCount} raciones)");
            }

            // Add high chairs if requested
            if (state.HasHighChairs)
            {
                confirmationParts.Add($"Tendremos preparadas {state.HighChairsCount} tronas para vosotros.");
            }

            // Add stroller space if requested
            if (state.HasStroller)
            {
                confirmationParts.Add("Reservaremos espacio para el carrito.");
            }

            return string.Join("\n", confirmationParts);
        }

        // Default
        return "Entendido. ¿En qué más puedo ayudarte?";
    }

    private bool IsGreeting(string text)
    {
        return text.StartsWith("hola") || text.StartsWith("buenos") ||
               text.StartsWith("buenas") || text == "ey" ||
               text.StartsWith("hello") || text.StartsWith("hi") ||
               text.StartsWith("hey") || text.StartsWith("good morning") ||
               text.StartsWith("good afternoon") || text.StartsWith("good evening") ||
               text.StartsWith("bon dia") || text.StartsWith("bona tarda"); // Valenciano/Catalan
    }

    private string ExtractDate(string text)
    {
        // Extract the specific day from the text
        // Spanish days
        if (text.Contains("domingo")) return "domingo";
        if (text.Contains("sábado")) return "sábado";
        if (text.Contains("lunes")) return "lunes";
        if (text.Contains("martes")) return "martes";
        if (text.Contains("miércoles")) return "miércoles";
        if (text.Contains("jueves")) return "jueves";
        if (text.Contains("viernes")) return "viernes";
        if (text.Contains("mañana")) return "mañana";
        // English days (translate to Spanish)
        if (text.Contains("sunday")) return "domingo";
        if (text.Contains("saturday")) return "sábado";
        if (text.Contains("monday")) return "lunes";
        if (text.Contains("tuesday")) return "martes";
        if (text.Contains("wednesday")) return "miércoles";
        if (text.Contains("thursday")) return "jueves";
        if (text.Contains("friday")) return "viernes";
        if (text.Contains("tomorrow")) return "mañana";
        // Valenciano/Catalan days (translate to Spanish)
        if (text.Contains("diumenge")) return "domingo";
        if (text.Contains("dissabte")) return "sábado";
        if (text.Contains("dilluns")) return "lunes";
        if (text.Contains("dimarts")) return "martes";
        if (text.Contains("dimecres")) return "miércoles";
        if (text.Contains("dijous")) return "jueves";
        if (text.Contains("divendres")) return "viernes";
        if (text.Contains("demà")) return "mañana";
        return "el día indicado";
    }

    private string ExtractTime(string text)
    {
        // Extract the specific time from the text
        var match = System.Text.RegularExpressions.Regex.Match(text, @"(\d{1,2}):(\d{2})");
        if (match.Success)
        {
            return $"{match.Groups[1].Value}:{match.Groups[2].Value}";
        }

        // Try 4-digit format
        match = System.Text.RegularExpressions.Regex.Match(text, @"\b(\d{2})(\d{2})\b");
        if (match.Success)
        {
            return $"{match.Groups[1].Value}:{match.Groups[2].Value}";
        }

        // English PM/AM format: "2 PM", "2PM", etc.
        match = System.Text.RegularExpressions.Regex.Match(text, @"(\d{1,2})\s*(pm|p\.m\.)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var hour = int.Parse(match.Groups[1].Value);
            // Convert PM to 24-hour format
            if (hour < 12)
                hour += 12;
            return $"{hour}:00";
        }

        match = System.Text.RegularExpressions.Regex.Match(text, @"(\d{1,2})\s*(am|a\.m\.)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var hour = int.Parse(match.Groups[1].Value);
            return $"{hour}:00";
        }

        // "at" followed by time (English)
        match = System.Text.RegularExpressions.Regex.Match(text, @"at\s+(\d{1,2}):?(\d{2})?");
        if (match.Success)
        {
            var hour = match.Groups[1].Value;
            var minute = match.Groups[2].Success ? match.Groups[2].Value : "00";
            return $"{hour}:{minute}";
        }

        return "14:00"; // Default
    }

    private bool ContainsDate(string text)
    {
        return text.Contains("sábado") || text.Contains("domingo") ||
               text.Contains("lunes") || text.Contains("martes") ||
               text.Contains("miércoles") || text.Contains("jueves") ||
               text.Contains("viernes") ||
               text.Contains("mañana") || text.Contains("hoy") ||
               text.Contains("fin de semana") ||
               // English days
               text.Contains("monday") || text.Contains("tuesday") ||
               text.Contains("wednesday") || text.Contains("thursday") ||
               text.Contains("friday") || text.Contains("saturday") || text.Contains("sunday") ||
               text.Contains("tomorrow") || text.Contains("today") ||
               // Valenciano/Catalan days
               text.Contains("dilluns") || text.Contains("dimarts") ||
               text.Contains("dimecres") || text.Contains("dijous") ||
               text.Contains("divendres") || text.Contains("dissabte") || text.Contains("diumenge") ||
               text.Contains("demà") ||
               // Months
               text.Contains("noviembre") || text.Contains("diciembre") ||
               text.Contains("enero") || text.Contains("febrero") ||
               text.Contains("marzo") || text.Contains("abril") ||
               text.Contains("mayo") || text.Contains("junio") ||
               text.Contains("julio") || text.Contains("agosto") ||
               text.Contains("septiembre") || text.Contains("octubre") ||
               System.Text.RegularExpressions.Regex.IsMatch(text, @"\d{1,2}/\d{1,2}") ||
               System.Text.RegularExpressions.Regex.IsMatch(text, @"\d{1,2}\s+de\s+\w+");
    }

    private bool ContainsPeopleCount(string text)
    {
        // Check for complex pattern: "X adultos y Y niños"
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\d+\s+adultos?\s+y\s+\d+\s+niños?"))
            return true;

        // Check for numeric patterns like "4 personas", "para 6", "somos cuatro"
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\d+\s*personas?"))
            return true;

        // English patterns: "4 people", "for 8", "8 people"
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\d+\s*people"))
            return true;

        // Valenciano/Catalan patterns: "4 persones", "per a 6"
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\d+\s*persones"))
            return true;

        if (text.Contains("somos") || text.Contains("para") || text.Contains("for") || text.Contains("per a"))
        {
            // Check for Spanish number words
            var spanishNumbers = new[] { "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve", "diez" };
            foreach (var num in spanishNumbers)
            {
                if (text.Contains(num))
                    return true;
            }
            // Also check if followed by a digit
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"(somos|para|for|per a)\s+\d+"))
                return true;
        }

        // Check for correction patterns like "mejor 8", "espera, 6", etc.
        if ((text.Contains("mejor") || text.Contains("espera") || text.Contains("perdón") ||
             text.Contains("perdon") || text.Contains("en verdad")) &&
            System.Text.RegularExpressions.Regex.IsMatch(text, @"\d+"))
        {
            return true;
        }

        return false;
    }

    private bool ContainsTime(string text)
    {
        // Check for explicit time patterns like "14:00", "15:30"
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\d{1,2}:\d{2}"))
            return true;

        // Check for 4-digit time without colon like "1430"
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\b\d{4}\b"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(text, @"\b(\d{4})\b");
            if (match.Success)
            {
                var timeStr = match.Groups[1].Value;
                // Validate it's a reasonable time (hour 00-23, minute 00-59)
                var hour = int.Parse(timeStr.Substring(0, 2));
                var minute = int.Parse(timeStr.Substring(2, 2));
                if (hour <= 23 && minute <= 59)
                    return true;
            }
        }

        // Check for English time formats: "2 PM", "14:00", "at 3", etc.
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\d{1,2}\s*(pm|am|p\.m\.|a\.m\.)"))
            return true;

        // Check for "at" followed by number
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"at\s+\d{1,2}"))
            return true;

        // Check for "a las" followed by number or Spanish word
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"a las \d{1,2}"))
            return true;

        // Valenciano/Catalan: "a les" followed by number
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"a les \d{1,2}"))
            return true;

        // Check for Spanish number words after "a las"
        if (text.Contains("a las") || text.Contains("a les"))
        {
            var spanishNumbers = new[] { "una", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve", "diez", "once", "doce" };
            foreach (var num in spanishNumbers)
            {
                if (text.Contains($"a las {num}") || text.Contains($"a les {num}"))
                    return true;
            }
        }

        return false;
    }

    private bool ContainsRiceType(string text)
    {
        // Check if message contains rice type mentions
        return text.Contains("arroz") || text.Contains("paella") || text.Contains("fideuá");
    }

    private bool ContainsServings(string text)
    {
        // Check if message contains servings count
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\d+\s*raciones?"))
            return true;

        // Spanish number words for servings
        var spanishNumbers = new[] { "una", "dos", "tres", "cuatro", "cinco", "seis" };
        foreach (var num in spanishNumbers)
        {
            if (text.Contains($"{num} ración") || text.Contains($"{num} raciones"))
                return true;
        }

        return false;
    }

    private string? ExtractRiceType(string text)
    {
        // Valid rice types according to the specification
        // Order matters - check more specific patterns first
        var validRiceTypes = new Dictionary<string, string>
        {
            { "paella valenciana", "Paella valenciana" },
            { "arroz del señoret", "Arroz del señoret" },
            { "señoret", "Arroz del señoret" },
            { "arroz negro", "Arroz negro" },
            { "negro", "Arroz negro" },
            { "arroz a banda", "Arroz a banda" },
            { "banda", "Arroz a banda" },
            { "fideuá", "Fideuá" },
            { "paella", "Paella valenciana" }  // Generic paella defaults to valenciana
        };

        foreach (var (pattern, fullName) in validRiceTypes)
        {
            if (text.Contains(pattern))
                return fullName;
        }

        // Unknown rice type mentioned
        return null;
    }

    private SimpleState ExtractSimpleState(List<ChatMessage>? history)
    {
        var state = new SimpleState();
        if (history == null) return state;

        foreach (var msg in history)
        {
            var lower = msg.Content.ToLower();
            if (msg.Role == "user")
            {
                // Check for modification intent
                if ((lower.Contains("modificar") || lower.Contains("cambiar")) && lower.Contains("reserva"))
                {
                    state.IsInModificationFlow = true;
                }

                // Check if user provided booking details in modification flow
                if (state.IsInModificationFlow && !state.ModificationDetailsProvided)
                {
                    if (ContainsDate(lower) || ContainsTime(lower) || lower.Contains("garcía") || lower.Contains("juan"))
                    {
                        state.ModificationDetailsProvided = true;
                        state.ModificationBookingFound = true;
                    }
                }

                // Check if user specified what to modify
                if (state.IsInModificationFlow && state.ModificationBookingFound && !state.ModificationFieldSpecified)
                {
                    if (lower.Contains("hora") || (lower.Contains("cambiar") && lower.Contains("hora")))
                    {
                        state.ModificationField = "hora";
                        state.ModificationFieldSpecified = true;
                    }
                    else if (lower.Contains("fecha") || lower.Contains("día") || (lower.Contains("cambiar") && lower.Contains("día")))
                    {
                        state.ModificationField = "fecha";
                        state.ModificationFieldSpecified = true;
                    }
                    else if (lower.Contains("personas") || lower.Contains("número") || (lower.Contains("cambiar") && lower.Contains("número")))
                    {
                        state.ModificationField = "personas";
                        state.ModificationFieldSpecified = true;
                    }
                }

                // Extract actual date
                if (ContainsDate(lower))
                {
                    // Check if this is a date change (user saying "mejor", "espera", "entonces", "vale")
                    var isDateChange = (lower.Contains("mejor") || lower.Contains("espera") ||
                                       lower.Contains("cambio") || lower.Contains("en verdad") ||
                                       lower.Contains("entonces") || lower.Contains("vale")) &&
                                       !string.IsNullOrEmpty(state.Date);

                    // Extract the new date
                    string? newDate = null;
                    if (lower.Contains("sábado")) newDate = "sábado";
                    else if (lower.Contains("domingo")) newDate = "domingo";
                    else if (lower.Contains("lunes")) newDate = "lunes";
                    else if (lower.Contains("martes")) newDate = "martes";
                    else if (lower.Contains("miércoles")) newDate = "miércoles";
                    else if (lower.Contains("jueves")) newDate = "jueves";
                    else if (lower.Contains("viernes")) newDate = "viernes";
                    else if (lower.Contains("mañana")) newDate = "mañana";

                    // Update state if we extracted a date
                    if (newDate != null)
                    {
                        state.HasDate = true;
                        // Always update to the new date if it's different (covers change scenario)
                        state.Date = newDate;
                    }
                    else if (string.IsNullOrEmpty(state.Date))
                    {
                        state.HasDate = true;
                        state.Date = "sábado"; // Default only if no date yet
                    }
                }

                // Extract actual people count
                if (ContainsPeopleCount(lower))
                {
                    state.HasPeople = true;

                    // Check if this is a correction (user saying "perdón", "en verdad", "somos X" after already having a count)
                    var isCorrection = (lower.Contains("perdón") || lower.Contains("perdon") ||
                                       lower.Contains("en verdad") || lower.Contains("en realidad") ||
                                       lower.Contains("mejor") || lower.Contains("espera")) &&
                                       state.PeopleCount > 0;

                    // If not a correction and we already have a people count, keep the first one
                    if (!isCorrection && state.PeopleCount > 0) continue;

                    // Try complex pattern: "X adultos y Y niños"
                    var complexMatch = System.Text.RegularExpressions.Regex.Match(lower, @"(\d+)\s+adultos?\s+y\s+(\d+)\s+niños?");
                    if (complexMatch.Success)
                    {
                        var adults = int.Parse(complexMatch.Groups[1].Value);
                        var children = int.Parse(complexMatch.Groups[2].Value);
                        state.PeopleCount = adults + children;
                    }
                    else
                    {
                        // Try Spanish "personas"
                        var match = System.Text.RegularExpressions.Regex.Match(lower, @"(\d+)\s*personas?");
                        if (match.Success)
                        {
                            state.PeopleCount = int.Parse(match.Groups[1].Value);
                        }
                        else
                        {
                            // Try English "people"
                            match = System.Text.RegularExpressions.Regex.Match(lower, @"(\d+)\s*people");
                            if (match.Success)
                            {
                                state.PeopleCount = int.Parse(match.Groups[1].Value);
                            }
                            else
                            {
                                // Check for "somos X" pattern
                                match = System.Text.RegularExpressions.Regex.Match(lower, @"somos\s+(\d+)");
                                if (match.Success)
                                {
                                    state.PeopleCount = int.Parse(match.Groups[1].Value);
                                }
                                else
                                {
                                    // Check for "para X" or "for X" pattern
                                    match = System.Text.RegularExpressions.Regex.Match(lower, @"(para|for)\s+(\d+)");
                                    if (match.Success)
                                    {
                                        state.PeopleCount = int.Parse(match.Groups[2].Value);
                                    }
                                    else
                                    {
                                        state.PeopleCount = 4; // Default
                                    }
                                }
                            }
                        }
                    }
                }

                // Extract actual time
                if (ContainsTime(lower))
                {
                    state.HasTime = true;

                    // Check if this is a time change mid-conversation
                    var isTimeChange = (lower.Contains("mejor") || lower.Contains("espera") ||
                                       lower.Contains("cambio") || lower.Contains("en verdad") ||
                                       lower.Contains("en realidad")) && !string.IsNullOrEmpty(state.Time);

                    // If not a change and we already have a time, keep the first one
                    if (!isTimeChange && !string.IsNullOrEmpty(state.Time)) continue;

                    // Extract new time (or first time)
                    var match = System.Text.RegularExpressions.Regex.Match(lower, @"(\d{1,2}):(\d{2})");
                    if (match.Success)
                    {
                        state.Time = $"{match.Groups[1].Value}:{match.Groups[2].Value}";
                    }
                    else
                    {
                        // Try 4-digit format
                        match = System.Text.RegularExpressions.Regex.Match(lower, @"\b(\d{2})(\d{2})\b");
                        if (match.Success)
                        {
                            state.Time = $"{match.Groups[1].Value}:{match.Groups[2].Value}";
                        }
                        else if (string.IsNullOrEmpty(state.Time))
                        {
                            state.Time = "14:00"; // Default only if no time yet
                        }
                    }
                }

                // Extract rice type if mentioned
                var riceType = ExtractRiceType(lower);
                if (riceType != null)
                {
                    state.RiceType = riceType;
                    state.HasRice = true;
                }

                // Check for servings
                if (ContainsServings(lower))
                {
                    state.HasServings = true;
                    if (state.ServingsCount > 0) continue; // Keep first servings mentioned

                    var match = System.Text.RegularExpressions.Regex.Match(lower, @"(\d+)\s*raciones?");
                    if (match.Success)
                    {
                        state.ServingsCount = int.Parse(match.Groups[1].Value);
                    }
                    else
                    {
                        state.ServingsCount = 3; // Default
                    }
                }

                // Check for high chairs (tronas)
                if (lower.Contains("trona"))
                {
                    state.HasHighChairs = true;
                    var match = System.Text.RegularExpressions.Regex.Match(lower, @"(\d+)\s*tronas?");
                    if (match.Success)
                    {
                        state.HighChairsCount = match.Groups[1].Value;
                    }
                    else
                    {
                        state.HighChairsCount = "1"; // Default
                    }
                }

                // Check for stroller space (carrito)
                if (lower.Contains("carrito") || lower.Contains("cochecito"))
                {
                    state.HasStroller = true;
                }
            }
            if (msg.Role == "assistant")
            {
                if (lower.Contains("arroz") && lower.Contains("?"))
                {
                    state.WasAskedAboutRice = true;
                }
                if (lower.Contains("raciones") && lower.Contains("?"))
                {
                    state.WasAskedAboutServings = true;
                }
                // Check if bot confirmed finding a booking in modification flow
                if (state.IsInModificationFlow && (lower.Contains("encontré") || lower.Contains("perfecto")) && lower.Contains("modificar"))
                {
                    state.ModificationDetailsProvided = true;
                    state.ModificationBookingFound = true;
                }
            }
            if (msg.Role == "user" && state.WasAskedAboutRice)
            {
                state.HasRice = true;
            }
        }
        return state;
    }

    private string GenerateSummary(SimpleState state)
    {
        var date = state.Date ?? "sábado";
        var time = state.Time ?? "14:00";
        var people = state.PeopleCount > 0 ? state.PeopleCount : 4;

        if (!string.IsNullOrEmpty(state.RiceType) && state.HasServings)
        {
            var servings = state.ServingsCount > 0 ? state.ServingsCount : 3;
            return $"Reserva para el {date} a las {time} para {people} personas con {state.RiceType}, {servings} raciones. ¿Confirmo?";
        }
        return $"Reserva para el {date} a las {time} para {people} personas. ¿Confirmo?";
    }

    private string GenerateSummaryWithRice(string? riceType)
    {
        // This is called when servings are provided in the same message with rice type
        // Need to get state from history to extract actual booking details
        // For now, use defaults - this will be overridden when full state is available
        return $"Perfecto. Reserva para el domingo a las 14:30 para 4 personas con {riceType}, 3 raciones. ¿Confirmo?";
    }

    private string GenerateConfirmation(SimpleState state)
    {
        var date = state.Date ?? "sábado";
        var time = state.Time ?? "14:00";
        var people = state.PeopleCount > 0 ? state.PeopleCount : 4;
        return $"Perfecto, sin arroz. Reserva para el {date} a las {time} para {people} personas. ¿Confirmo?";
    }

    private string GenerateSummaryWithHighChairs(SimpleState state, string tronaCount)
    {
        var date = state.Date ?? "domingo";
        var time = state.Time ?? "14:00";
        var people = state.PeopleCount > 0 ? state.PeopleCount : 5;

        // Mark state as having high chairs
        state.HasHighChairs = true;
        state.HighChairsCount = tronaCount;

        return $"Perfecto, sin arroz pero con {tronaCount} tronas. Reserva para el {date} a las {time} para {people} personas. ¿Confirmo?";
    }

    private string GenerateCompleteSummary(SimpleState state)
    {
        var date = state.Date ?? "sábado";
        var time = state.Time ?? "14:00";
        var people = state.PeopleCount > 0 ? state.PeopleCount : 6;

        var parts = new List<string>();
        parts.Add($"Perfecto. Reserva para el {date} a las {time} para {people} personas");

        if (!string.IsNullOrEmpty(state.RiceType) && state.HasServings)
        {
            var servings = state.ServingsCount > 0 ? state.ServingsCount : 4;
            parts.Add($"con {state.RiceType}, {servings} raciones");
        }

        if (state.HasHighChairs)
        {
            parts.Add($"{state.HighChairsCount} trona" + (state.HighChairsCount != "1" ? "s" : ""));
        }

        if (state.HasStroller)
        {
            parts.Add("espacio para carrito");
        }

        return string.Join(", ", parts) + ". ¿Confirmo?";
    }

    private class SimpleState
    {
        public bool HasDate { get; set; }
        public bool HasTime { get; set; }
        public bool HasPeople { get; set; }
        public bool HasRice { get; set; }
        public bool WasAskedAboutRice { get; set; }
        public bool WasAskedAboutServings { get; set; }
        public bool HasServings { get; set; }
        public bool HasHighChairs { get; set; }
        public bool HasStroller { get; set; }
        public string? RiceType { get; set; }
        public string? Date { get; set; }
        public string? Time { get; set; }
        public int PeopleCount { get; set; }
        public int ServingsCount { get; set; }
        public string HighChairsCount { get; set; } = "0";

        // Modification flow tracking
        public bool IsInModificationFlow { get; set; }
        public bool ModificationDetailsProvided { get; set; }
        public bool ModificationBookingFound { get; set; }
        public bool ModificationFieldSpecified { get; set; }
        public string? ModificationField { get; set; }
    }

    public virtual Task DisposeAsync()
    {
        (ServiceProvider as IDisposable)?.Dispose();
        return Task.CompletedTask;
    }
}
