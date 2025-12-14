using BotGenerator.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BotGenerator.Core.Services;

/// <summary>
/// Uses Gemini AI to extract booking state from conversation history.
/// Much more robust than regex - understands natural language variations.
/// </summary>
public class AiStateExtractorService : IAiStateExtractorService
{
    private readonly IGeminiService _gemini;
    private readonly IContextBuilderService _contextBuilder;
    private readonly ILogger<AiStateExtractorService> _logger;

    public AiStateExtractorService(
        IGeminiService gemini,
        IContextBuilderService contextBuilder,
        ILogger<AiStateExtractorService> logger)
    {
        _gemini = gemini;
        _contextBuilder = contextBuilder;
        _logger = logger;
    }

    public async Task<ConversationState> ExtractStateAsync(
        List<ChatMessage> history,
        CancellationToken cancellationToken = default)
    {
        if (history.Count == 0)
        {
            return new ConversationState
            {
                MissingData = new List<string> { "fecha", "hora", "personas", "arroz_decision", "tronas", "carritos" },
                IsComplete = false,
                Stage = "collecting_info"
            };
        }

        // Build conversation text for AI
        var conversationText = string.Join("\n", history.Select(m =>
            m.Role == "user" ? $"CLIENTE: {m.Content}" : $"ASISTENTE: {m.Content}"));

        // Get upcoming weekends for context
        var upcomingWeekends = _contextBuilder.GetUpcomingWeekends();

        var systemPrompt = $@"Eres un extractor de datos de reservas de restaurante. Tu ÚNICA tarea es analizar la conversación y devolver JSON.

FECHA HOY: {DateTime.Now:dd/MM/yyyy} ({DateTime.Now:dddd})
FINES DE SEMANA: {upcomingWeekends}

REGLAS DE EXTRACCIÓN:
1. fecha: formato DD/MM/YYYY o null si no mencionada
2. hora: formato HH:MM o null si no mencionada
3. personas: número entero o null
4. arrozType: nombre EXACTO del arroz elegido, """" si dijo que no quiere arroz, null si no decidido
5. arrozServings: número de raciones o null
6. tronas: 0 si dijo no, -1 si dijo sí sin cantidad, número si dio cantidad, null si no preguntado
7. carritos: igual que tronas
8. isConfirming: true SOLO si el último mensaje del cliente es una confirmación clara (""sí"", ""confirmo"", ""perfecto"")

IMPORTANTE:
- Para tronas/carritos: ""no"", ""ninguna"", ""nada"", ""sin tronas"", ""no hace falta"" = 0
- El arroz debe ser el nombre COMPLETO Y EXACTO como aparece en el menú
- NO uses markdown, NO uses ```json```, responde SOLO el objeto JSON
- Si el asistente confirmó ""0 tronas"" o ""0 carritos"", el valor es 0, NO null";

        var userMessage = $@"CONVERSACIÓN:
{conversationText}

Responde SOLO con este JSON (sin markdown):
{{""fecha"":""DD/MM/YYYY o null"",""hora"":""HH:MM o null"",""personas"":null,""arrozType"":null,""arrozServings"":null,""tronas"":null,""carritos"":null,""isConfirming"":false}}";

        try
        {
            var config = new GeminiGenerationConfig
            {
                Temperature = 0.0,  // Deterministic for extraction
                TopP = 0.95,
                TopK = 40,
                MaxOutputTokens = 1024  // Increased for complete JSON
            };

            var response = await _gemini.GenerateAsync(
                systemPrompt,
                userMessage,
                null,
                config,
                cancellationToken);

            _logger.LogDebug("AI state extraction response: {Response}", response);

            return ParseAiResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting state with AI, returning empty state");
            return new ConversationState
            {
                MissingData = new List<string> { "fecha", "hora", "personas", "arroz_decision", "tronas", "carritos" },
                IsComplete = false,
                Stage = "collecting_info"
            };
        }
    }

    private ConversationState ParseAiResponse(string response)
    {
        try
        {
            // Strip markdown code block wrappers if present
            var cleanResponse = response.Trim();
            if (cleanResponse.StartsWith("```json"))
                cleanResponse = cleanResponse.Substring(7);
            else if (cleanResponse.StartsWith("```"))
                cleanResponse = cleanResponse.Substring(3);
            if (cleanResponse.EndsWith("```"))
                cleanResponse = cleanResponse.Substring(0, cleanResponse.Length - 3);
            cleanResponse = cleanResponse.Trim();

            // Extract JSON from response (in case AI adds extra text)
            var jsonMatch = Regex.Match(cleanResponse, @"\{[\s\S]*\}");
            if (!jsonMatch.Success)
            {
                _logger.LogWarning("No JSON found in AI response: {Response}", response);
                return CreateEmptyState();
            }

            var json = jsonMatch.Value;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var state = new ConversationState();
            var missingData = new List<string>();

            // Extract fecha
            if (root.TryGetProperty("fecha", out var fechaEl) && fechaEl.ValueKind == JsonValueKind.String)
            {
                var fecha = fechaEl.GetString();
                if (!string.IsNullOrWhiteSpace(fecha) && fecha != "null")
                    state = state with { Fecha = fecha };
                else
                    missingData.Add("fecha");
            }
            else
            {
                missingData.Add("fecha");
            }

            // Extract hora
            if (root.TryGetProperty("hora", out var horaEl) && horaEl.ValueKind == JsonValueKind.String)
            {
                var hora = horaEl.GetString();
                if (!string.IsNullOrWhiteSpace(hora) && hora != "null")
                    state = state with { Hora = hora };
                else
                    missingData.Add("hora");
            }
            else
            {
                missingData.Add("hora");
            }

            // Extract personas
            if (root.TryGetProperty("personas", out var personasEl))
            {
                int? personas = personasEl.ValueKind switch
                {
                    JsonValueKind.Number => personasEl.GetInt32(),
                    JsonValueKind.String when int.TryParse(personasEl.GetString(), out var p) => p,
                    _ => null
                };
                if (personas.HasValue)
                    state = state with { Personas = personas.Value };
                else
                    missingData.Add("personas");
            }
            else
            {
                missingData.Add("personas");
            }

            // Extract arrozType
            if (root.TryGetProperty("arrozType", out var arrozEl))
            {
                if (arrozEl.ValueKind == JsonValueKind.String)
                {
                    var arroz = arrozEl.GetString();
                    if (arroz == null)
                        missingData.Add("arroz_decision");
                    else
                        state = state with { ArrozType = arroz }; // Empty string means "no quiere"
                }
                else if (arrozEl.ValueKind == JsonValueKind.Null)
                {
                    missingData.Add("arroz_decision");
                }
            }
            else
            {
                missingData.Add("arroz_decision");
            }

            // Extract arrozServings
            if (root.TryGetProperty("arrozServings", out var servingsEl))
            {
                int? servings = servingsEl.ValueKind switch
                {
                    JsonValueKind.Number => servingsEl.GetInt32(),
                    JsonValueKind.String when int.TryParse(servingsEl.GetString(), out var s) => s,
                    _ => null
                };
                if (servings.HasValue)
                    state = state with { ArrozServings = servings.Value };
                else if (!string.IsNullOrEmpty(state.ArrozType))
                    missingData.Add("arroz_servings");
            }
            else if (!string.IsNullOrEmpty(state.ArrozType))
            {
                missingData.Add("arroz_servings");
            }

            // Extract tronas
            if (root.TryGetProperty("tronas", out var tronasEl))
            {
                int? tronas = tronasEl.ValueKind switch
                {
                    JsonValueKind.Number => tronasEl.GetInt32(),
                    JsonValueKind.String when int.TryParse(tronasEl.GetString(), out var t) => t,
                    _ => null
                };
                if (tronas.HasValue)
                {
                    state = state with { HighChairs = tronas.Value };
                    if (tronas.Value < 0)
                        missingData.Add("tronas_count");
                }
                else
                {
                    missingData.Add("tronas");
                }
            }
            else
            {
                missingData.Add("tronas");
            }

            // Extract carritos
            if (root.TryGetProperty("carritos", out var carritosEl))
            {
                int? carritos = carritosEl.ValueKind switch
                {
                    JsonValueKind.Number => carritosEl.GetInt32(),
                    JsonValueKind.String when int.TryParse(carritosEl.GetString(), out var c) => c,
                    _ => null
                };
                if (carritos.HasValue)
                {
                    state = state with { BabyStrollers = carritos.Value };
                    if (carritos.Value < 0)
                        missingData.Add("carritos_count");
                }
                else
                {
                    missingData.Add("carritos");
                }
            }
            else
            {
                missingData.Add("carritos");
            }

            // Check minimum rice servings
            if (state.ArrozServings.HasValue && state.ArrozServings.Value < 2 && !string.IsNullOrEmpty(state.ArrozType))
            {
                missingData.Add("arroz_servings_min2");
            }

            state = state with
            {
                MissingData = missingData,
                IsComplete = missingData.Count == 0,
                Stage = missingData.Count == 0 ? "awaiting_confirmation" : "collecting_info"
            };

            _logger.LogInformation(
                "AI extracted state: fecha={Fecha}, hora={Hora}, personas={Personas}, arroz={Arroz}, " +
                "servings={Servings}, tronas={Tronas}, carritos={Carritos}, missing={Missing}",
                state.Fecha, state.Hora, state.Personas, state.ArrozType,
                state.ArrozServings, state.HighChairs, state.BabyStrollers,
                string.Join(",", missingData));

            return state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing AI response: {Response}", response);
            return CreateEmptyState();
        }
    }

    private ConversationState CreateEmptyState()
    {
        return new ConversationState
        {
            MissingData = new List<string> { "fecha", "hora", "personas", "arroz_decision", "tronas", "carritos" },
            IsComplete = false,
            Stage = "collecting_info"
        };
    }
}
