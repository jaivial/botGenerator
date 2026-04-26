using Microsoft.Extensions.Logging;

namespace BotGenerator.Core.Services;

/// <summary>
/// AI-powered intent detection. Understands confirmations, rejections, exit intent,
/// rice cancel intent, and more from natural language.
/// </summary>
public class AiIntentDetectionService : IAiIntentDetectionService
{
    private readonly IGeminiService _gemini;
    private readonly ILogger<AiIntentDetectionService> _logger;

    public AiIntentDetectionService(
        IGeminiService gemini,
        ILogger<AiIntentDetectionService> logger)
    {
        _gemini = gemini;
        _logger = logger;
    }

    public async Task<string> DetectIntentAsync(
        string userMessage,
        string context,
        CancellationToken cancellationToken = default)
    {
        var (systemPrompt, userPrompt) = BuildPrompts(userMessage, context);

        try
        {
            var config = new GeminiGenerationConfig
            {
                Temperature = 0.0,
                MaxOutputTokens = 20
            };

            var response = await _gemini.GenerateAsync(systemPrompt, userPrompt, null, config, cancellationToken);
            var cleaned = response.Trim().ToUpperInvariant();

            _logger.LogInformation(
                "AiIntentDetection for '{Message}' in context '{Context}': AI returned '{Response}'",
                userMessage, context, cleaned);

            // Normalize response to standard intent names
            if (cleaned.Contains("CONFIRM")) return "confirm";
            if (cleaned.Contains("REJECT")) return "reject";
            if (cleaned.Contains("EXIT")) return "exit";
            if (cleaned.Contains("CANCEL_RICE")) return "cancel_rice";
            if (cleaned.Contains("CONTINUE")) return "continue";
            if (cleaned.Contains("UNCLEAR")) return "none";

            return "none";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AiIntentDetection failed for message: '{Message}'", userMessage);
            return "none";
        }
    }

    private static (string System, string User) BuildPrompts(string userMessage, string context)
    {
        return context switch
        {
            "modification_exit" => BuildExitPrompts(userMessage),
            "cancellation_confirm" => BuildCancellationConfirmPrompts(userMessage),
            "modification_confirm" => BuildModificationConfirmPrompts(userMessage),
            "rice_cancel" => BuildRiceCancelPrompts(userMessage),
            _ => BuildGenericPrompts(userMessage, context)
        };
    }

    private static (string, string) BuildExitPrompts(string userMessage)
    {
        var system = @"Eres un detector de intenciones en una conversación de reservas de restaurante.
El usuario está en un flujo de modificación de reserva y ha respondido a la pregunta ""¿qué quieres cambiar?"".

Determina si el usuario quiere SALIR del flujo sin hacer cambios o si quiere CONTINUAR con una modificación.

EXIT: Quiere salir sin cambiar nada. Ejemplos: ""no"", ""nada"", ""dejalo"", ""déjalo"", ""no gracias"", ""todo bien"", ""así está bien"", ""ninguna"", ""cancelar"", ""salir""
CONTINUE: Quiere seguir con una modificación o proporciona información de cambio. Ejemplos: ""la fecha"", ""14:30"", ""más personas"", ""arroz"", ""1"", ""2"", o cualquier cosa que no sea salir.

Responde SOLO con: EXIT o CONTINUE";
        var user = $@"Mensaje del usuario: ""{userMessage}""";
        return (system, user);
    }

    private static (string, string) BuildCancellationConfirmPrompts(string userMessage)
    {
        var system = @"Eres un detector de intenciones en una conversación de cancelación de reserva.
Se le ha preguntado al usuario si confirma la cancelación.

CONFIRM: Confirma que quiere cancelar. Ejemplos: ""sí"", ""si"", ""ok"", ""vale"", ""claro"", ""confirmo"", ""adelante"", ""por supuesto"", ""afirmativo"", ""correcto"", ""exacto"", ""hazlo"", ""procede""
REJECT: No quiere cancelar. Ejemplos: ""no"", ""nop"", ""nope"", ""nel"", ""mejor no"", ""déjalo"", ""mantener"", ""nada"", ""no quiero""
UNCLEAR: No se puede determinar la intención.

Responde SOLO con: CONFIRM, REJECT o UNCLEAR";
        var user = $@"Mensaje del usuario: ""{userMessage}""";
        return (system, user);
    }

    private static (string, string) BuildModificationConfirmPrompts(string userMessage)
    {
        var system = @"Eres un detector de intenciones en una conversación de modificación de reserva.
Se le ha preguntado al usuario si confirma el cambio propuesto.

CONFIRM: Confirma el cambio. Ejemplos: ""sí"", ""si"", ""ok"", ""vale"", ""claro"", ""confirmo"", ""adelante"", ""perfecto"", ""de acuerdo""
REJECT: Rechaza el cambio. Ejemplos: ""no"", ""nop"", ""mejor no"", ""déjalo"", ""cancelar"", ""nada""
UNCLEAR: No se puede determinar.

Responde SOLO con: CONFIRM, REJECT o UNCLEAR";
        var user = $@"Mensaje del usuario: ""{userMessage}""";
        return (system, user);
    }

    private static (string, string) BuildRiceCancelPrompts(string userMessage)
    {
        var system = @"Eres un detector de intenciones sobre arroz en una reserva de restaurante.
El usuario está en un flujo de cambio de arroz.

CANCEL_RICE: Quiere quitar/cancelar el arroz. Ejemplos: ""quitar"", ""sin arroz"", ""no quiero arroz"", ""cancelar"", ""nada"", ""eliminar"", ""lo quito""
CONTINUE: Quiere cambiar a otro tipo o especificar raciones. Ejemplos: ""paella"", ""fideuá"", ""señoret"", ""2 raciones"", ""cambiar"", o menciona un tipo de arroz
UNCLEAR: No se puede determinar.

Responde SOLO con: CANCEL_RICE, CONTINUE o UNCLEAR";
        var user = $@"Mensaje del usuario: ""{userMessage}""";
        return (system, user);
    }

    private static (string, string) BuildGenericPrompts(string userMessage, string context)
    {
        var system = $@"Eres un detector de intenciones en una conversación de reservas de restaurante.
Contexto: {context}

CONFIRM: El usuario confirma (sí, ok, vale, claro, etc.)
REJECT: El usuario rechaza (no, nop, mejor no, etc.)
EXIT: El usuario quiere salir del flujo actual
CANCEL_RICE: El usuario quiere quitar el arroz
CONTINUE: El usuario quiere continuar o proporcionar más información
UNCLEAR: No se puede determinar

Responde SOLO con: CONFIRM, REJECT, EXIT, CANCEL_RICE, CONTINUE o UNCLEAR";
        var user = $@"Mensaje del usuario: ""{userMessage}""";
        return (system, user);
    }
}
