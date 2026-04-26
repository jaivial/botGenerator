using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace BotGenerator.Core.Services;

/// <summary>
/// AI-powered rice understanding. Analyzes user messages about rice in the context
/// of booking modifications, handling typos, partial names, and natural language.
/// </summary>
public class AiRiceUnderstandingService : IAiRiceUnderstandingService
{
    private readonly IGeminiService _gemini;
    private readonly ILogger<AiRiceUnderstandingService> _logger;

    public AiRiceUnderstandingService(
        IGeminiService gemini,
        ILogger<AiRiceUnderstandingService> logger)
    {
        _gemini = gemini;
        _logger = logger;
    }

    public async Task<RiceUnderstandingResult> AnalyzeAsync(
        string userMessage,
        string bookingSummary,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = @"Eres un analizador de mensajes sobre arroz en reservas de restaurante.
Analiza el mensaje del usuario en el contexto de su reserva y determina su intención sobre el arroz.

Responde SOLO con un JSON:
{
  ""isGenericReference"": false,
  ""wantsCancel"": false,
  ""riceTypeMentioned"": null,
  ""servingsMentioned"": null,
  ""isServingsOnly"": false
}

DEFINICIONES:
- isGenericReference: true si dice ""arroz"", ""paella"", ""fideuá"" SIN indicar un tipo específico (ej: ""quiero arroz"", ""poned arroz"")
  - Es FALSE si menciona un tipo específico como ""señoret"", ""a banda"", ""meloso de pulpo"", etc.
  - Es FALSE si dice ""arroz"" como parte de un nombre específico como ""arroz de señoret""
- wantsCancel: true si quiere QUITAR el arroz (""quitar"", ""sin arroz"", ""no quiero"", ""cancelar"", ""nada"", ""eliminar"")
- riceTypeMentioned: el nombre del arroz mencionado (ej: ""señoret"", ""a banda"", ""fideuá"", ""meloso de pulpo"") o null
  - Solo si menciona un tipo ESPECÍFICO, no si dice genéricamente ""arroz""
- servingsMentioned: número de raciones mencionado (ej: ""4"" de ""4 raciones"") o null
- isServingsOnly: true si el mensaje es SOLO un número o número + ""raciones"" (ej: ""4"", ""4 raciones"", ""dos raciones"")
  - Esto indica que ya eligió el arroz antes y está respondiendo cuántas raciones quiere

EJEMPLOS:
- ""quiero arroz"" → {""isGenericReference"": true, ""wantsCancel"": false, ""riceTypeMentioned"": null, ""servingsMentioned"": null, ""isServingsOnly"": false}
- ""arroz del señoret"" → {""isGenericReference"": false, ""wantsCancel"": false, ""riceTypeMentioned"": ""señoret"", ""servingsMentioned"": null, ""isServingsOnly"": false}
- ""2 de fideuá y dos de arroz del señoret"" → {""isGenericReference"": false, ""wantsCancel"": false, ""riceTypeMentioned"": ""señoret"", ""servingsMentioned"": null, ""isServingsOnly"": false}
- ""quitar el arroz"" → {""isGenericReference"": false, ""wantsCancel"": true, ""riceTypeMentioned"": null, ""servingsMentioned"": null, ""isServingsOnly"": false}
- ""4 raciones"" → {""isGenericReference"": false, ""wantsCancel"": false, ""riceTypeMentioned"": null, ""servingsMentioned"": 4, ""isServingsOnly"": true}
- ""4"" → {""isGenericReference"": false, ""wantsCancel"": false, ""riceTypeMentioned"": null, ""servingsMentioned"": 4, ""isServingsOnly"": true}
- ""fideuá"" → {""isGenericReference"": false, ""wantsCancel"": false, ""riceTypeMentioned"": ""fideuá"", ""servingsMentioned"": null, ""isServingsOnly"": false}
- ""paella para 4"" → {""isGenericReference"": false, ""wantsCancel"": false, ""riceTypeMentioned"": ""paella"", ""servingsMentioned"": 4, ""isServingsOnly"": false}
- ""no quiero arroz"" → {""isGenericReference"": false, ""wantsCancel"": true, ""riceTypeMentioned"": null, ""servingsMentioned"": null, ""isServingsOnly"": false}";

        var userPrompt = $@"Reserva: {bookingSummary}
Mensaje: ""{userMessage}""

Analiza:";

        try
        {
            var config = new GeminiGenerationConfig
            {
                Temperature = 0.0,
                MaxOutputTokens = 200
            };

            var response = await _gemini.GenerateAsync(systemPrompt, userPrompt, null, config, cancellationToken);

            _logger.LogInformation(
                "AiRiceUnderstanding for '{Message}': AI returned '{Response}'",
                userMessage, response);

            return ParseResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AiRiceUnderstanding failed for message: '{Message}'", userMessage);
            return new RiceUnderstandingResult();
        }
    }

    private static RiceUnderstandingResult ParseResponse(string response)
    {
        try
        {
            var json = response.Trim();
            var start = json.IndexOf('{');
            var end = json.LastIndexOf('}');
            if (start >= 0 && end > start)
                json = json[start..(end + 1)];

            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new RiceUnderstandingResult
            {
                IsGenericReference = GetBool(root, "isGenericReference"),
                WantsCancel = GetBool(root, "wantsCancel"),
                RiceTypeMentioned = GetString(root, "riceTypeMentioned"),
                ServingsMentioned = GetInt(root, "servingsMentioned"),
                IsServingsOnly = GetBool(root, "isServingsOnly")
            };
        }
        catch
        {
            return new RiceUnderstandingResult();
        }
    }

    private static bool GetBool(JsonElement root, string prop)
    {
        return root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.True;
    }

    private static string? GetString(JsonElement root, string prop)
    {
        if (root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String)
        {
            var val = el.GetString();
            return string.IsNullOrEmpty(val) ? null : val;
        }
        return null;
    }

    private static int? GetInt(JsonElement root, string prop)
    {
        if (root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.Number)
            return el.GetInt32();
        return null;
    }
}
