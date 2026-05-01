using System.Text.Json;
using BotGenerator.Core.Models;
using Microsoft.Extensions.Logging;

namespace BotGenerator.Core.Services;

/// <summary>
/// AI Agent that processes messages using Anthropic-style tool calls.
/// This replaces the hardcoded response logic with a pure AI-driven approach.
/// </summary>
public class AgentOrchestrator : IAgentOrchestrator
{
    private readonly IGeminiService _ai;
    private readonly IToolExecutor _toolExecutor;
    private readonly ILogger<AgentOrchestrator> _logger;
    private readonly int _maxIterations;
    private readonly Dictionary<string, ToolResult> _toolResultsById = new();

    public AgentOrchestrator(
        IGeminiService ai,
        IToolExecutor toolExecutor,
        ILogger<AgentOrchestrator> logger,
        int maxIterations = 15)
    {
        _ai = ai;
        _toolExecutor = toolExecutor;
        _logger = logger;
        _maxIterations = maxIterations;
    }

    /// <summary>
    /// Run the agent to handle a user message.
    /// This is the main entry point for the single-agent architecture.
    /// </summary>
    public async Task<AgentResult> RunAsync(
        string userMessage,
        string phoneNumber,
        PipelineIntent intent,
        Dictionary<string, object>? extractedInfo,
        string conversationHistory,
        CancellationToken ct = default)
    {
        _logger.LogInformation("[AGENT] Starting agent for {Phone}", phoneNumber);

        try
        {
            // Build restaurant info
            var restaurantInfo = BuildRestaurantInfo();

            // Use the existing ProcessAsync method
            var result = await ProcessAsync(
                phoneNumber,
                userMessage,
                "Cliente", // TODO: Get from conversation history
                DateTime.Now.ToString("dddd dd 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-ES")),
                restaurantInfo,
                ct);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AGENT] Error running agent for {Phone}", phoneNumber);
            return new AgentResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private static string BuildRestaurantInfo()
    {
        return @"RESTAURANTE: Alquería Villa Carmen
TIPO: Arroces tradicionales valencianos
DIRECCIÓN: Valencia, España
TELÉFONO: +34 600 000 000
HORARIO: 13:30 a 18:00 (cocina hasta las 15:30)
MESA MÍNIMA: 4 personas para paella (2 para otros arroces)
RESERVAS: Obligatorias
NOTAS: Cocktails, eventos, menus corporativos";
    }

    /// <summary>
    /// Process a user message using the AI Agent with tool calls.
    /// All user-facing messages are sent via the send_message tool.
    /// </summary>
    public async Task<AgentResult> ProcessAsync(
        string phoneNumber,
        string userMessage,
        string pushName,
        string todayES,
        string restaurantInfo,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[AGENT] Starting processing for {Phone}: '{Message}'",
            phoneNumber, userMessage);

        // Clear tool results from previous calls
        _toolResultsById.Clear();

        // Build the agent system prompt
        var systemPrompt = BuildSystemPrompt(pushName, todayES, restaurantInfo);

        // AUTOMATICALLY FETCH WHATSAPP HISTORY for context
        // This is critical for understanding references like "el anterior domingo" or "como te dije antes"
        string historyContext = "";
        try
        {
            var historyInput = JsonDocument.Parse(@"{""limit"": 30}").RootElement;
            var historyResult = await _toolExecutor.ExecuteAsync("fetch_whatsapp_history", historyInput, phoneNumber, ct);
            if (historyResult.Success && !string.IsNullOrEmpty(historyResult.Content))
            {
                historyContext = $"\n\n## CONTEXTO DE LA CONVERSACION (ULTIMOS 30 MENSAJES):\n{historyResult.Content}\n\n";
                _logger.LogInformation("[AGENT] Fetched WhatsApp history for context, length: {Len}", historyResult.Content.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AGENT] Failed to fetch WhatsApp history, continuing without context");
        }

        // Build initial messages for the conversation (Anthropic format)
        // Each message has role and content (which can be string or array of blocks)
        // Include history context with the user message
        var messages = new List<object>
        {
            // Initial user message - with conversation context
            new Dictionary<string, object>
            {
                ["role"] = "user",
                ["content"] = historyContext + $"## NUEVO MENSAJE:\n{userMessage}"
            }
        };

        var tools = AgentToolDefinitions.GetAllTools();
        var iteration = 0;
        var retryCount = 0;
        var sentMessages = new List<string>();
        var toolCalls = new List<string>();

        _logger.LogInformation("[AGENT] Initializing agent loop for {Phone}", phoneNumber);

        // Agent loop: AI → tool calls → execute → AI → ... → final
        while (iteration < _maxIterations)
        {
            iteration++;
            _logger.LogDebug("[AGENT] Iteration {Iteration}/{Max} for {Phone}", iteration, _maxIterations, phoneNumber);

            try
            {
                // Call the AI with current messages and tools
                _logger.LogDebug("[AGENT] Calling AI API with {ToolCount} tools, {MsgCount} messages",
                    tools.Count, messages.Count);

                var response = await _ai.ContinueWithToolResultAsync(
                    systemPrompt,
                    messages,
                    tools,
                    config: new GeminiGenerationConfig
                    {
                        Temperature = 0.7,
                        MaxOutputTokens = 2048
                    },
                    cancellationToken: ct);

                var hasContent = false;
                var hasToolCalls = false;

                foreach (var block in response.Content)
                {
                    if (block is TextBlock textBlock)
                    {
                        hasContent = true;
                        // AI returned text - this might happen at the end or if it forgets to use tools
                        if (!string.IsNullOrWhiteSpace(textBlock.Text))
                        {
                            _logger.LogWarning(
                                "[AGENT] WARNING: AI returned plain text instead of using send_message: {Text}",
                                textBlock.Text.Length > 300 ? textBlock.Text[..300] + "..." : textBlock.Text);
                        }
                    }
                    else if (block is ToolUseBlock toolBlock)
                    {
                        hasContent = true;
                        hasToolCalls = true;
                        var toolName = toolBlock.Name;
                        var toolId = toolBlock.Id;
                        var toolInput = toolBlock.Input;

                        _logger.LogInformation(
                            "[AGENT] >>> TOOL CALL: {Tool} (id={Id}) for {Phone} in iteration {Iteration}",
                            toolName, toolId, phoneNumber, iteration);

                        toolCalls.Add(toolName);

                        // Execute the tool ONCE and store the result
                        var toolResult = await _toolExecutor.ExecuteAsync(toolName, toolInput, phoneNumber, ct);

                        // Store result keyed by tool ID for later use in the second loop
                        _toolResultsById[toolId] = toolResult;

                        // Log tool execution result
                        if (toolResult.IsError)
                        {
                            _logger.LogError("[AGENT] Tool {Tool} failed: {Error}", toolName, toolResult.Content);
                        }
                        else
                        {
                            _logger.LogInformation("[AGENT] Tool {Tool} executed successfully", toolName);
                        }

                        // Track sent messages for the result
                        if (toolName == "send_message")
                        {
                            var msgText = toolInput.TryGetProperty("message", out var m)
                                ? m.GetString() ?? ""
                                : "";
                            if (!string.IsNullOrEmpty(msgText))
                            {
                                sentMessages.Add(msgText);
                                _logger.LogInformation(
                                    "[AGENT] >>> MESSAGE SENT to {Phone}: {Preview}...",
                                    phoneNumber,
                                    msgText.Length > 100 ? msgText[..100] : msgText);
                            }
                        }
                    }
                }

                // If we got tool calls, we need to add them to messages and continue
                if (hasToolCalls)
                {
                    // The response content should be added as an assistant message
                    // But first, we need to collect all tool_use blocks from the response
                    var assistantContent = new List<object>();
                    foreach (var block in response.Content)
                    {
                        if (block is ToolUseBlock toolBlock)
                        {
                            var toolInputDict = JsonSerializer.Deserialize<Dictionary<string, object>>(
                                toolBlock.Input.GetRawText()) ?? new Dictionary<string, object>();
                            
                            assistantContent.Add(new Dictionary<string, object>
                            {
                                ["type"] = "tool_use",
                                ["id"] = toolBlock.Id,
                                ["name"] = toolBlock.Name,
                                ["input"] = toolInputDict
                            });
                        }
                        else if (block is TextBlock textBlock)
                        {
                            assistantContent.Add(new Dictionary<string, object>
                            {
                                ["type"] = "text",
                                ["text"] = textBlock.Text
                            });
                        }
                    }

                    // Add assistant message with tool calls
                    messages.Add(new Dictionary<string, object>
                    {
                        ["role"] = "assistant",
                        ["content"] = assistantContent
                    });

                    // Now add user message(s) with tool results
                    // Each tool result should be in a separate content block
                    // IMPORTANT: Use stored results from _toolResultsById to avoid duplicate tool calls
                    foreach (var block in response.Content)
                    {
                        if (block is ToolUseBlock toolBlock)
                        {
                            // Get the stored result from the first execution
                            var storedResult = _toolResultsById.TryGetValue(toolBlock.Id, out var result)
                                ? result
                                : new ToolResult { IsError = true, Content = "Tool result not found" };

                            var resultContent = storedResult.IsError
                                ? $"Error: {storedResult.Content}"
                                : storedResult.Content ?? "Tool executed successfully";

                            // Add user message with tool result
                            messages.Add(new Dictionary<string, object>
                            {
                                ["role"] = "user",
                                ["content"] = new List<object>
                                {
                                    new Dictionary<string, object>
                                    {
                                        ["type"] = "tool_result",
                                        ["tool_use_id"] = toolBlock.Id,
                                        ["content"] = resultContent
                                    }
                                }
                            });
                        }
                    }
                }

                // If we got content but no tool calls, the AI has finished (end_turn)
                // But we still need to send the message! Use fallback if text was returned.
                if (hasContent && !hasToolCalls)
                {
                    // Find the text block
                    string? plainText = null;
                    foreach (var block in response.Content)
                    {
                        if (block is TextBlock textBlock && !string.IsNullOrWhiteSpace(textBlock.Text))
                        {
                            plainText = textBlock.Text.Trim();
                            break;
                        }
                    }
                    
                    // If we have text but no message was sent, use fallback send_message
                    if (!string.IsNullOrWhiteSpace(plainText) && sentMessages.Count == 0)
                    {
                        _logger.LogWarning(
                            "[AGENT] WARNING: AI returned plain text without using send_message. Using fallback.");
                        
                        // Send via tool as fallback
                        var fallbackInput = JsonDocument.Parse($@"{{""message"": ""{EscapeJson(plainText)}""}}").RootElement;
                        var fallbackResult = await _toolExecutor.ExecuteAsync("send_message", fallbackInput, phoneNumber, ct);
                        
                        if (fallbackResult.Success)
                        {
                            sentMessages.Add(plainText);
                            _logger.LogInformation(
                                "[AGENT] >>> MESSAGE SENT (fallback) to {Phone}: {Preview}...",
                                phoneNumber,
                                plainText.Length > 100 ? plainText[..100] : plainText);
                        }
                    }
                    
                    if (response.StopReason == "end_turn")
                    {
                        _logger.LogInformation(
                            "[AGENT] Agent completed after {Iterations} iterations for {Phone}. " +
                            "Sent {MsgCount} messages, used tools: {Tools}",
                            iteration, phoneNumber, sentMessages.Count,
                            toolCalls.Count > 0 ? string.Join(", ", toolCalls) : "none");
                        break;
                    }
                }

                // If no content at all, something went wrong
                if (!hasContent)
                {
                    _logger.LogWarning("[AGENT] No content from AI in iteration {Iteration}", iteration);
                    break;
                }

                // If no tool calls, continue (might get more in next iteration)
                if (!hasToolCalls)
                {
                    _logger.LogDebug("[AGENT] No tool calls in iteration {Iteration}, continuing", iteration);
                    continue;
                }

                // Continue loop for next iteration
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AGENT] Error in agent loop iteration {Iteration} for {Phone}",
                    iteration, phoneNumber);
                
                // Check if we should retry (for timeout/network errors)
                if (ex is TaskCanceledException or OperationCanceledException)
                {
                    _logger.LogWarning("[AGENT] Request was cancelled (timeout or network issue). Retrying...");
                    if (retryCount < 2)
                    {
                        retryCount++;
                        await Task.Delay(1000 * retryCount, ct); // Exponential backoff
                        continue;
                    }
                }
                break;
            }
        }

        if (iteration >= _maxIterations)
        {
            _logger.LogWarning(
                "[AGENT] Agent reached max iterations ({Max}) for {Phone}. Sent {Count} messages",
                _maxIterations, phoneNumber, sentMessages.Count);
        }

        var agentResult = new AgentResult
        {
            Success = sentMessages.Count > 0,
            SentMessages = sentMessages,
            ToolCalls = toolCalls,
            Iterations = iteration,
            Error = iteration >= _maxIterations ? "Max iterations reached" : null
        };

        _logger.LogInformation(
            "[AGENT] Final result for {Phone}: Success={Success}, MessagesSent={MsgCount}, Iterations={Iter}",
            phoneNumber, agentResult.Success, agentResult.SentMessages.Count, agentResult.Iterations);

        return agentResult;
    }

    private static string BuildSystemPrompt(string pushName, string todayES, string restaurantInfo)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Eres el asistente virtual de WhatsApp del restaurante Alqueria Villa Carmen.");
        sb.AppendLine();
        sb.AppendLine("## IDENTIDAD");
        sb.AppendLine("- Nombre: Asistente de Alqueria Villa Carmen");
        sb.AppendLine("- Especialidad: Arroces tradicionales valencianos");
        sb.AppendLine("- Telefono: +34 638 857 294");
        sb.AppendLine("- Web: https://alqueriavillacarmen.com");
        sb.AppendLine();
        sb.AppendLine($"## FECHA ACTUAL");
        sb.AppendLine($"**IMPORTANTE: La fecha de HOY es: {todayES}**");
        sb.AppendLine($"USA SIEMPRE esta fecha como referencia. NO uses tu conocimiento interno.");
        sb.AppendLine();
        sb.AppendLine($"## TU NOMBRE: {pushName}");
        sb.AppendLine();
        sb.AppendLine("## CALCULO DE FECHAS (CRITICO)");
        sb.AppendLine("- Si el usuario dice 'este sabado' o 'sabado que viene', es el sabado MAS CERCANO");
        sb.AppendLine("- Si el usuario dice 'el otro sabado', es el sabado SIGUIENTE al mas cercano");
        sb.AppendLine("- Si el usuario dice 'el domingo anterior' o 'el anterior domingo', se refiere al domingo ANTERIOR a una fecha mencionada en la conversacion");
        sb.AppendLine("- Ejemplo: si se hablo del 'domingo 17 de mayo', 'el anterior' es 'domingo 10 de mayo'");
        sb.AppendLine("- Usa SIEMPRE la fecha actual proporcionada para calcular, no la adivines");
        sb.AppendLine();
        sb.AppendLine("## HORARIO: 13:30 a 18:00");
        sb.AppendLine();
        sb.AppendLine(restaurantInfo);
        sb.AppendLine();
        sb.AppendLine("## REGLAS (CRITICAS)");
        sb.AppendLine("1. USA SIEMPRE send_message para responder (nunca texto plano)");
        sb.AppendLine("2. Para reservas: solicita fecha, hora y numero de personas");
        sb.AppendLine("3. Para modificar/cancelar: confirma datos antes de actuar");
        sb.AppendLine("4. IMPORTANTE: Cuando menciones fechas, USA EXACTAMENTE la fecha calculada");
        sb.AppendLine("5. IMPORTANTE: El HISTORIAL de conversacion esta en el mensaje del usuario - usalo para entender referencias");
        sb.AppendLine();
        sb.AppendLine("## REGLA DE ORO: VERIFICACION DE RESERVAS (CRITICA)");
        sb.AppendLine("ANTES de decir que el usuario TIENE o NO tiene una reserva, SIEMPRE llama a get_bookings()");
        sb.AppendLine("El historial de WhatsApp NO es fuente de verdad para reservas - puede estar desactualizado.");
        sb.AppendLine("Los administradores pueden haber borrado reservas manualmente.");
        sb.AppendLine("Solo get_bookings() te dice el estado ACTUAL de las reservas en la base de datos.");
        sb.AppendLine("NUNCA afirmes que el usuario tiene una reserva si no la has verificado con get_bookings()");
        sb.AppendLine();
        sb.AppendLine("## FLUJO: AVISAME SI ALGUIEN CANCELA");
        sb.AppendLine("Cuando el usuario dice 'avisame si alguien cancela', 'notify me', 'aviso de cancelacion':");
        sb.AppendLine("1. NO afirmes que tiene una reserva");
        sb.AppendLine("2. SIEMPRE usa get_bookings() para verificar si tiene reservas actuales");
        sb.AppendLine("3. Si NO tiene reserva: explica que no tenemos sistema de notificacion por cancelacion");
        sb.AppendLine("4. Sugiere llamar al restaurante: +34 638 857 294");
        sb.AppendLine("5. Si QUIERE hacer una reserva: ayudarte con el proceso normal");
        sb.AppendLine();
        sb.AppendLine("## FLUJO CREAR RESERVA");
        sb.AppendLine("1. check_day_capacity o get_opening_hours_with_capacity");
        sb.AppendLine("2. Confirmar con usuario: fecha, hora, personas, arroz (opcional)");
        sb.AppendLine("3. create_booking (date, time, people, rice_type, name, confirmed: true)");
        sb.AppendLine("VALIDA: dia cerrado, capacidad, hora disponible");
        sb.AppendLine();
        sb.AppendLine("## FLUJO MODIFICAR RESERVA");
        sb.AppendLine("1. get_bookings para ver reservas");
        sb.AppendLine("2. Mostrar reservas y preguntar cual modificar");
        sb.AppendLine("3. modify_booking (booking_id, campos, confirmed: true)");
        sb.AppendLine("VALIDA: no hoy/manana, max 3 modificaciones");
        sb.AppendLine();
        sb.AppendLine("## FLUJO CANCELAR RESERVA");
        sb.AppendLine("1. get_bookings");
        sb.AppendLine("2. Confirmar cual cancelar");
        sb.AppendLine("3. cancel_booking (booking_id, confirmed: true)");
        sb.AppendLine();
        sb.AppendLine("## HERRAMIENTAS");
        sb.AppendLine("- send_message: Enviar mensaje (OBLIGATORIO)");
        sb.AppendLine("- fetch_whatsapp_history: Historial conversacion");
        sb.AppendLine("- get_bookings: Reservas del usuario");
        sb.AppendLine("- get_restaurant_info: Info restaurante");
        sb.AppendLine("- get_rice_menu: Tipos de arroz");
        sb.AppendLine("- check_future_booking: Tiene reservas futuras?");
        sb.AppendLine("- check_day_capacity: Estado del dia (abierto/lleno/cerrado)");
        sb.AppendLine("- check_availability_for_party: Cabe X personas?");
        sb.AppendLine("- get_opening_hours_with_capacity: Horas con capacidad");
        sb.AppendLine("- check_hour_capacity: Configuracion por hora");
        sb.AppendLine("- create_booking: Crear reserva (con validaciones)");
        sb.AppendLine("- modify_booking: Modificar reserva (con validaciones)");
        sb.AppendLine("- cancel_booking: Cancelar reserva");
        sb.AppendLine();
        sb.AppendLine("## EJEMPLO CREAR");
        sb.AppendLine("Usuario: Reservar manana 14:00 4 personas");
        sb.AppendLine("1. check_day_capacity(date=02/05/2026)");
        sb.AppendLine("2. send_message: Perfecto, sitio para 4 a las 14:00. Arroz?");
        sb.AppendLine("Usuario: Si, paella, soy Maria");
        sb.AppendLine("3. create_booking(date=2026-05-02, time=14:00, people=4, rice_type=Paella Valenciana, name=Maria, confirmed=true)");
        sb.AppendLine("4. send_message: Reserva confirmada!");
        sb.AppendLine();
        sb.AppendLine("## EJEMPLO MODIFICAR");
        sb.AppendLine("Usuario: Cambiar reserva del viernes");
        sb.AppendLine("1. get_bookings");
        sb.AppendLine("2. send_message: Reserva: Viernes 3/05 14:00 4 personas");
        sb.AppendLine("Usuario: Cambiar a 6 personas");
        sb.AppendLine("3. modify_booking(booking_id=1234, people=6, confirmed=true)");
        sb.AppendLine("4. send_message: Modificada a 6 personas!");
        sb.AppendLine();
        sb.AppendLine("## EJEMPLO CANCELAR");
        sb.AppendLine("Usuario: Cancelar reserva");
        sb.AppendLine("1. get_bookings");
        sb.AppendLine("2. send_message: Cual reservas?");
        sb.AppendLine("Usuario: La del viernes");
        sb.AppendLine("3. cancel_booking(booking_id=1234, confirmed=true)");
        sb.AppendLine("4. send_message: Reserva cancelada!");
        sb.AppendLine();
        sb.AppendLine("## EJEMPLO: AVISAME SI CANCELAN");
        sb.AppendLine("Usuario: Avisame si alguien cancela");
        sb.AppendLine("1. get_bookings (SIEMPRE verificar antes de afirmar nada)");
        sb.AppendLine("2. Si NO tiene reservas: 'No tenemos sistema de notificacion por cancelacion. Para estar seguro, llama al restaurante +34 638 857 294'");
        sb.AppendLine("3. Si quiere hacer reserva: ayudarte con el proceso normal");
        sb.AppendLine();
        sb.AppendLine("ADELANTE!");
        return sb.ToString();
    }
    private static string EscapeJson(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    /// <summary>
    /// Decode Unicode escape sequences like \u00BF to actual UTF-8 characters.
    /// This is needed because the AI may return escaped Unicode that needs decoding.
    /// </summary>
    private static string DecodeUnicodeEscapes(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        try
        {
            // Use regex to find and replace Unicode escapes
            return System.Text.RegularExpressions.Regex.Replace(
                text,
                @"\\u([0-9A-Fa-f]{4})",
                match =>
                {
                    var codePoint = int.Parse(match.Groups[1].Value, System.Globalization.NumberStyles.HexNumber);
                    return char.ConvertFromUtf32(codePoint);
                });
        }
        catch
        {
            return text;
        }
    }
}

/// <summary>
/// Result of the agent processing.
/// </summary>
public class AgentResult
{
    public bool Success { get; init; }
    public List<string> SentMessages { get; init; } = new();
    public List<string> ToolCalls { get; init; } = new();
    public int Iterations { get; init; }
    public string? Error { get; init; }
}
