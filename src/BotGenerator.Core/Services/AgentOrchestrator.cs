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

        // AI CLASSIFICATION GATE: ask the model (from a focused system prompt) whether this
        // conversation is a special event (comunion, boda, bautizo, fecha senalada) or a group
        // of more than 10 people. If so, the booking is never processed: we redirect the
        // customer to restaurant management and send the management contact card.
        if (await IsSpecialEventAsync(userMessage, historyContext, ct))
        {
            _logger.LogWarning(
                "[AGENT] AI classified as special event / large group for {Phone}: '{Message}'. Redirecting to restaurant management (booking not processed).",
                phoneNumber, userMessage);
            return await RedirectToManagementAsync(phoneNumber, pushName, ct);
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
                            _logger.LogWarning("[AGENT] Tool {Tool} failed for {Phone}: {Error} | Input: {Input}",
                                toolName, phoneNumber, toolResult.Content, toolInput.GetRawText());
                        }
                        else
                        {
                            _logger.LogInformation("[AGENT] Tool {Tool} executed successfully for {Phone}", toolName, phoneNumber);
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

    private static readonly string SpecialEventClassifierPrompt =
        "Eres un clasificador de conversaciones de un restaurante por WhatsApp. " +
        "Responde SOLO con 'SI' o 'NO'. " +
        "Responde 'SI' si el cliente quiere reservar u organizar: " +
        "un evento especial (comunion, boda, bautizo, celebracion, banquete, evento privado, fiesta privada), " +
        "una fecha senalada o festiva en Espana (Nochevieja, Navidad, Ano Nuevo, Reyes, Fallas, San Jose, Semana Santa, San Juan, puentes, dias festivos), " +
        "un cumpleanos con mas de 10 personas, o un grupo de mas de 10 personas (ej: 'somos 40', '40 personas', '20 invitados'). " +
        "Tambien responde 'SI' si en la conversacion anterior el cliente ya menciono un evento especial o un grupo de mas de 10 personas y ahora sigue insistiendo o dando detalles de esa reserva. " +
        "NO respondas 'SI' si el ultimo mensaje del cliente es solo un saludo o agradecimiento corto (hola, gracias, ok, vale, perfecto, de nada, si, no). " +
        "Responde 'NO' para cualquier otra peticion: reservas normales de hasta 10 personas, consultas, preguntas sobre el restaurante, modificaciones o cancelaciones de reservas normales, saludos o agradecimientos.";

    /// <summary>
    /// Asks the AI (via a focused classification prompt) whether the conversation should be
    /// redirected to restaurant management because it involves a special event or a group of
    /// more than 10 people. Detection is fully AI-driven from the prompt, no regex.
    /// </summary>
    private async Task<bool> IsSpecialEventAsync(string userMessage, string historyContext, CancellationToken ct)
    {
        try
        {
            var input = historyContext +
                        $"## MENSAJE DEL CLIENTE:\n{userMessage}\n\n" +
                        "¿Este cliente quiere reservar u organizar un EVENTO ESPECIAL o un GRUPO DE MAS DE 10 PERSONAS?\n" +
                        "Responde SOLO con SI o NO.";

            var answer = await _ai.GenerateAsync(
                SpecialEventClassifierPrompt,
                input,
                null,
                new GeminiGenerationConfig { Temperature = 0.0, MaxOutputTokens = 10 },
                ct);

            var normalized = StripDiacritics(answer.Trim().ToUpperInvariant());
            var isSpecialEvent = normalized.StartsWith("SI", StringComparison.Ordinal);

            _logger.LogInformation(
                "[AGENT] Special event classification: answer='{Answer}' => IsSpecialEvent={Result}",
                answer, isSpecialEvent);
            return isSpecialEvent;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AGENT] Special event classification failed; proceeding with normal flow");
            return false;
        }
    }

    /// <summary>
    /// Redirects a special-event / large-group request to restaurant management: sends a
    /// message with the management phone and the management contact card. No booking is
    /// ever processed for these cases.
    /// </summary>
    private async Task<AgentResult> RedirectToManagementAsync(string phoneNumber, string pushName, CancellationToken ct)
    {
        var sentMessages = new List<string>();
        var toolCalls = new List<string> { "send_message", "send_contact_card" };

        var message =
            $"¡Hola, {pushName}! 😊\n\n" +
            "Gracias por tu interés en Alquería Villa Carmen. 🌿\n\n" +
            "Las reservas para eventos especiales (comuniones, bodas, bautizos, fechas señaladas) y grupos de más de 10 personas se gestionan directamente con nuestro equipo de gestión del restaurante, no por WhatsApp.\n\n" +
            "Te he enviado la tarjeta de contacto con el teléfono *+34 638 857 294*. ¡Muchas gracias! 🎉";

        var messageInput = JsonDocument.Parse($"{{\"message\": \"{EscapeJson(message)}\"}}").RootElement;
        var messageResult = await _toolExecutor.ExecuteAsync("send_message", messageInput, phoneNumber, ct);
        if (messageResult.Success)
            sentMessages.Add(message);

        await _toolExecutor.ExecuteAsync("send_contact_card", JsonDocument.Parse("{}").RootElement, phoneNumber, ct);

        _logger.LogInformation(
            "[AGENT] Redirected {Phone} to restaurant management for a special event / large group. MessagesSent={Count}",
            phoneNumber, sentMessages.Count);

        return new AgentResult
        {
            Success = sentMessages.Count > 0,
            SentMessages = sentMessages,
            ToolCalls = toolCalls,
            Iterations = 0
        };
    }

    private static string StripDiacritics(string text)
    {
        var chars = new char[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            chars[i] = text[i] switch
            {
                'Á' => 'A', 'É' => 'E', 'Í' => 'I', 'Ó' => 'O', 'Ú' => 'U',
                'á' => 'a', 'é' => 'e', 'í' => 'i', 'ó' => 'o', 'ú' => 'u',
                'ñ' => 'n', 'Ñ' => 'N', 'Ü' => 'U', 'ü' => 'u',
                _ => text[i]
            };
        }
        return new string(chars);
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
        sb.AppendLine("1. REGLA DE PRIORIDAD MAXIMA - EVENTOS ESPECIALES Y GRUPOS GRANDES: si el cliente pide reservar para una comunion, boda, bautizo, celebracion, evento, banquete, fecha senalada o festiva en Espana (Nochevieja, Navidad, Ano Nuevo, Reyes, Fallas, San Jose, Semana Santa, puentes, festivos), un cumpleanos de mas de 10 personas o cualquier grupo de mas de 10 personas, entonces: NO preguntes fecha/hora/personas/arroz, NO llames a create_booking/modify_booking ni a herramientas de disponibilidad/capacidad, NO proceses la reserva. En su lugar llama a send_contact_card (tarjeta del equipo de gestion) y responde con send_message indicando que estos eventos/grupos se gestionan directamente con el equipo de gestion del restaurante, telefono +34 638 857 294. TERMINA ahi, no sigas pidiendo datos.");
        sb.AppendLine("2. USA SIEMPRE send_message para responder (nunca texto plano)");
        sb.AppendLine("3. Para reservas: solicita fecha, hora y numero de personas");
        sb.AppendLine("4. Para modificar/cancelar: confirma datos antes de actuar");
        sb.AppendLine("5. IMPORTANTE: Cuando menciones fechas, USA EXACTAMENTE la fecha calculada");
        sb.AppendLine("6. IMPORTANTE: El HISTORIAL de conversacion esta en el mensaje del usuario - usalo para entender referencias");
        sb.AppendLine("7. ENVIA UNA SOLA RESPUESTA COMPLETA por cada mensaje del usuario con UN solo send_message. No envíes mensajes de relleno ni de saludo y luego más mensajes en el mismo turno, ni repitas información ya enviada.");
        sb.AppendLine();
        sb.AppendLine("## REGLA DE ORO: VERIFICACION DE RESERVAS (CRITICA)");
        sb.AppendLine("ANTES de decir que el usuario TIENE o NO tiene una reserva, SIEMPRE llama a get_bookings()");
        sb.AppendLine("El historial de WhatsApp NO es fuente de verdad para reservas - puede estar desactualizado.");
        sb.AppendLine("Los administradores pueden haber borrado reservas manualmente.");
        sb.AppendLine("Solo get_bookings() te dice el estado ACTUAL de las reservas en la base de datos.");
        sb.AppendLine("NUNCA afirmes que el usuario tiene una reserva si no la has verificado con get_bookings()");
        sb.AppendLine();
        sb.AppendLine("## REGLA CRITICA: EVENTOS ESPECIALES Y GRUPOS GRANDES (PRIORIDAD MAXIMA, NO GESTIONAR RESERVA)");
        sb.AppendLine("ESTA REGLA PREVALECE SOBRE CUALQUIER OTRA REGLA O FLUJO DE RESERVA.");
        sb.AppendLine("DETECTA e INTERPRETA estas senales en el mensaje del cliente (y en el historial de la conversacion):");
        sb.AppendLine("- Eventos especiales: cualquier mencion a comunion(es), boda(s), bautizo(s), celebracion(es), banquete(s), evento(s) privado(s), fiesta(s) privada(s), festejo(s).");
        sb.AppendLine("- Fechas senaladas/festivas en Espana: Nochevieja, Navidad, Ano Nuevo, Reyes, Fallas, San Jose, Semana Santa, San Juan, puentes, dias festivos.");
        sb.AppendLine("- Cumpleanos (birthday) con mas de 10 personas.");
        sb.AppendLine("- Cualquier grupo de mas de 10 personas (ej: 'somos 40', '40 personas', '20 invitados').");
        sb.AppendLine("SI DETECTAS CUALQUIERA DE ESAS SENALES:");
        sb.AppendLine("1. NO proceses la reserva. PROHIBIDO preguntar fecha, hora, personas o arroz. PROHIBIDO llamar a create_booking, modify_booking, check_day_capacity, get_opening_hours_with_capacity, check_availability o cualquier herramienta de disponibilidad/capacidad.");
        sb.AppendLine("2. Llama a send_contact_card (envia la tarjeta de contacto del equipo de gestion del restaurante).");
        sb.AppendLine("3. Envia UN mensaje con send_message: explica que estos eventos y grupos se gestionan directamente con el equipo de gestion del restaurante e indica el telefono +34 638 857 294.");
        sb.AppendLine("4. TERMINA: no sigas pidiendo mas datos de la reserva.");
        sb.AppendLine("EJEMPLO DE RESPUESTA CORRECTA:");
        sb.AppendLine("Usuario: 'Quiero reservar para una comunion'");
        sb.AppendLine("1. send_contact_card");
        sb.AppendLine("2. send_message: 'Hola! Las reservas de eventos especiales como comuniones se gestionan directamente con nuestro equipo de gestion del restaurante. Te he enviado su tarjeta de contacto. Puedes llamarles al +34 638 857 294. Gracias!'");
        sb.AppendLine("EJEMPLO PROHIBIDO: preguntar 'Para que dia seria la comunion?' o 'Cuantas personas sereis?' en una comunion.");
        sb.AppendLine();
        sb.AppendLine("## REGLA DE ORO: CONFIRMAR CAMBIOS (CRITICA)");
        sb.AppendLine("NUNCA confirmes que una reserva ha sido CREADA, MODIFICADA o CANCELADA a menos que la herramienta");
        sb.AppendLine("correspondiente (create_booking / modify_booking / cancel_booking) haya devuelto success=true en ESTE turno.");
        sb.AppendLine("Tras un modify_booking con exito, refleja SOLO los valores del objeto `updatedBooking` devuelto por la herramienta.");
        sb.AppendLine("NO inventes ni asumas campos (tronas, carritos, arroz, personas) que no aparezcan en el resultado de la herramienta.");
        sb.AppendLine("Si una herramienta devolvio un error, NO afirmes exito en este ni en turnos posteriores:");
        sb.AppendLine("reintenta la herramienta con datos corregidos, o manten el mensaje de error y sugiere llamar al restaurante.");
        sb.AppendLine("Ejemplo PROHIBIDO: decir 'Carro de bebe: 1 ✅' sin que modify_booking haya devuelto babyStrollers=1.");
        sb.AppendLine();
        sb.AppendLine("## NOMBRE DEL USUARIO (AUTOMATICO)");
        sb.AppendLine($"El nombre del usuario ES: {pushName}");
        sb.AppendLine("Este nombre viene del perfil de WhatsApp del usuario.");
        sb.AppendLine(" Cuando el usuario dice solo su nombre (ej: 'Jaime', 'Maria Garcia'), es porque se lo estas pidiendo para la reserva.");
        sb.AppendLine("USA ESTE NOMBRE directamente en create_booking - NO vuelvas a preguntarlo.");
        sb.AppendLine("Si el usuario SOLO envia su nombre, entiende que esta confirmando la reserva con ese nombre.");
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
        sb.AppendLine("3. Si añade/cambia arroz: check_rice_availability y usa el nombre matched exacto");
        sb.AppendLine("4. Si no indicó raciones: preguntar cuántas quiere. NUNCA asumirlas");
        sb.AppendLine("5. modify_booking (booking_id, campos, confirmed: true). Arroz requiere rice_type + rice_servings");
        sb.AppendLine("VALIDA: no hoy/manana, max 3 modificaciones");
        sb.AppendLine();
        sb.AppendLine("## FLUJO CANCELAR RESERVA");
        sb.AppendLine("1. get_bookings");
        sb.AppendLine("2. Confirmar cual cancelar");
        sb.AppendLine("3. cancel_booking (booking_id, confirmed: true)");
        sb.AppendLine();
        sb.AppendLine("## HERRAMIENTAS");
        sb.AppendLine("- send_message: Enviar mensaje (OBLIGATORIO)");
        sb.AppendLine("- send_contact_card: Enviar tarjeta de contacto del equipo de gestión (eventos especiales / grupos >10 personas)");
        sb.AppendLine("- fetch_whatsapp_history: Historial conversacion");
        sb.AppendLine("- get_bookings: Reservas del usuario");
        sb.AppendLine("- get_restaurant_info: Info restaurante");
        sb.AppendLine("- get_rice_menu: Lista de tipos de arroz disponibles");
        sb.AppendLine("- check_rice_availability: Verificar si un arroz especifico esta disponible");
        sb.AppendLine("- check_future_booking: Tiene reservas futuras?");
        sb.AppendLine("- check_day_capacity: Estado del dia (abierto/lleno/cerrado)");
        sb.AppendLine("- check_availability_for_party: Cabe X personas?");
        sb.AppendLine("- get_opening_hours_with_capacity: Horas con capacidad");
        sb.AppendLine("- check_hour_capacity: Configuracion por hora");
        sb.AppendLine("- create_booking: Crear reserva (con validaciones)");
        sb.AppendLine("- modify_booking: Modificar reserva (con validaciones)");
        sb.AppendLine("- cancel_booking: Cancelar reserva");
        sb.AppendLine();
        sb.AppendLine("## FLUJO: CONSULTA DE ARROCES");
        sb.AppendLine("Cuando usuario pregunta por tipos de arroz o menu:");
        sb.AppendLine("1. get_rice_menu (obtiene lista de FINDE donde TIPO='ARROZ' y active=1)");
        sb.AppendLine("2. Mostrar la lista al usuario");
        sb.AppendLine();
        sb.AppendLine("## FLUJO: ARROZ ESPECIFICO EN RESERVA");
        sb.AppendLine("Cuando usuario menciona un arroz concreto (ej: 'paella', 'arroz negro'):");
        sb.AppendLine("1. check_rice_availability(rice_type='arroz mencionado') para verificar");
        sb.AppendLine("2. Si available=true: usar el matched rice exacto en create_booking o modify_booking");
        sb.AppendLine("3. Preguntar cuántas raciones quiere si no lo indicó; mínimo 2, máximo número de personas");
        sb.AppendLine("4. Si available=false: informar y sugerir opciones de la lista");
        sb.AppendLine();
        sb.AppendLine("## EJEMPLO: EVENTO ESPECIAL (PRIORIDAD MAXIMA)");
        sb.AppendLine("Usuario: Quiero reservar para una comunion");
        sb.AppendLine("1. send_contact_card (tarjeta del equipo de gestion)");
        sb.AppendLine("2. send_message: Hola! Las reservas de eventos especiales como comuniones se gestionan directamente con nuestro equipo de gestion del restaurante. Te he enviado su tarjeta de contacto. Puedes llamarles al +34 638 857 294. Gracias!");
        sb.AppendLine("Usuario: Somos 40");
        sb.AppendLine("3. Si el grupo es de mas de 10 personas, repetir el mismo tratamiento: send_contact_card + send_message indicando +34 638 857 294. NUNCA preguntar fecha/hora/arroz.");
        sb.AppendLine();
        sb.AppendLine("## EJEMPLO CREAR");
        sb.AppendLine("Usuario: Reservar manana 14:00 4 personas");
        sb.AppendLine("1. check_day_capacity(date=02/05/2026)");
        sb.AppendLine("2. send_message: Perfecto, sitio para 4 a las 14:00. Arroz?");
        sb.AppendLine("Usuario: Si, paella, soy Maria");
        sb.AppendLine("3. check_rice_availability(rice_type='paella')");
        sb.AppendLine("4. create_booking(date=2026-05-02, time=14:00, people=4, rice_type=Paella Valenciana, name=Maria, confirmed=true)");
        sb.AppendLine("5. send_message: Reserva confirmada!");
        sb.AppendLine();
        sb.AppendLine("## EJEMPLO: VER LISTA ARROCES");
        sb.AppendLine("Usuario: Que arroces teneis?");
        sb.AppendLine("1. get_rice_menu");
        sb.AppendLine("2. Mostrar lista de arroces disponibles");
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
        sb.AppendLine("## EJEMPLO: CONFIRMACION DE NOMBRE");
        sb.AppendLine("Cuando el usuario ya dio fecha/hora/personas y solo envia su nombre");
        sb.AppendLine("Usuario: Para el domingo 10 de mayo 3 personas sin arroz");
        sb.AppendLine("1. check_day_capacity(date=10/05/2026)");
        sb.AppendLine("2. send_message: Confirmo: domingo 10 mayo 14:00 3 personas sin arroz. Nombre?");
        sb.AppendLine("Usuario: Jaime Villanueva");
        sb.AppendLine("3. create_booking(name='Jaime Villanueva', date=2026-05-10, time=14:00, people=3, confirmed=true)");
        sb.AppendLine("4. send_message: Reserva confirmada para Jaime Villanueva");
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
