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

        var systemPrompt = $@"Eres un extractor de datos de reservas. Analiza la conversación y extrae los datos de la reserva en formato JSON.

FECHA DE HOY: {DateTime.Now:dd/MM/yyyy} ({DateTime.Now:dddd})
PRÓXIMOS FINES DE SEMANA DISPONIBLES:
{upcomingWeekends}

INSTRUCCIONES:
1. Extrae SOLO datos que el cliente haya CONFIRMADO explícitamente
2. Si el asistente preguntó algo y el cliente NO respondió aún, ese campo es null
3. Para tronas/carritos:
   - Si cliente dijo NO (""no"", ""ninguna"", ""nada"", ""0"", ""sin tronas"", etc.) → valor 0
   - Si cliente dijo SÍ pero sin cantidad → valor -1 (necesita especificar cuántas)
   - Si cliente dijo SÍ con cantidad (""2 tronas"", ""una trona"") → ese número
   - Si NO se ha preguntado o no hay respuesta clara → null
4. Para arroz:
   - Si cliente dijo que NO quiere arroz → arrozType = """"
   - Si eligió un arroz específico → arrozType = nombre del arroz
   - Si no se ha decidido → arrozType = null
5. Detecta si el cliente está CONFIRMANDO la reserva (""sí, confirmo"", ""perfecto"", ""de acuerdo"", etc.)

RESPONDE SOLO CON JSON VÁLIDO, SIN TEXTO ADICIONAL:
{{
  ""fecha"": ""DD/MM/YYYY o null"",
  ""hora"": ""HH:MM o null"",
  ""personas"": ""número o null"",
  ""arrozType"": ""nombre del arroz, cadena vacía si no quiere, o null si no decidido"",
  ""arrozServings"": ""número de raciones o null"",
  ""tronas"": ""número (0 si no), -1 si sí pero sin cantidad, null si no preguntado"",
  ""carritos"": ""número (0 si no), -1 si sí pero sin cantidad, null si no preguntado"",
  ""isConfirming"": ""true si el cliente está confirmando la reserva en su último mensaje""
}}";

        var userMessage = $"CONVERSACIÓN:\n{conversationText}\n\nExtrae los datos de la reserva:";

        try
        {
            var config = new GeminiGenerationConfig
            {
                Temperature = 0.1,
                TopP = 0.8,
                TopK = 10,
                MaxOutputTokens = 512
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
            // Extract JSON from response (in case AI adds extra text)
            var jsonMatch = Regex.Match(response, @"\{[\s\S]*\}");
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
