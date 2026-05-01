# Plan: Migración a Agente IA con Tool Calls para Envío de Mensajes

## Objetivo
Transformar el bot de WhatsApp de un sistema con mensajes hardcodeados en C# a un **Agente IA** donde todos los mensajes se envían mediante tool calls de Anthropic API.

## Estado Actual

### Arquitectura Actual
```
WebhookController
    └── PipelineOrchestrator
            ├── ContextAnalyzerNode (Node 1) - Análisis de intent
            ├── ValidationEnrichmentNode (Node 2) - Validación de disponibilidad
            ├── ResponseGeneratorNode (Node 3) - Generación de respuesta
            └── Handlers (Cancellation, Modification, Booking)
                    └── WebhookController.SendTextAsync() ← MENSAJES HARDCODEADOS
```

### Problema Identificado
1. **Mensajes hardcodeados en C#**: PipelineOrchestrator tiene ~8 respuestas fijas en código
2. **BookingHandler.BuildConfirmationMessage()**: Mensaje de confirmación hardcodeado
3. **Handlers** (Cancellation, Modification): Generan respuestas en C#
4. **WebhookController** envía todos los mensajes directamente via `_whatsApp.SendTextAsync()`

### Herramientas Actuales (ToolExecutor)
- `fetch_whatsapp_history` ✓
- `get_restaurant_info` ✓
- `get_rice_menu` ✓
- `check_availability` ✓
- `get_opening_hours` ✓
- `get_hour_data` ✓
- `get_day_status` ✓
- `get_bookings` ✓
- `query_database` ✓
- **`send_message` ✗ FALTANTE**

---

## Plan de Migración

### Fase 1: Fundamentos del Agente
- [ ] **1.1** Agregar tool `send_message` al ToolExecutor
- [ ] **1.2** Definir herramientas disponibles en formato Anthropic Tools
- [ ] **1.3** Crear `AgentToolDefinitions` con todas las tools

### Fase 2: Refactorizar ResponseGeneratorNode
- [ ] **2.1** Convertir de texto plano a Anthropic Tools nativo
- [ ] **2.2** Implementar loop de tool calls (AI → tool → AI → tool → response)
- [ ] **2.3** Agregar `send_message` como tool disponible para AI

### Fase 3: Migrar Mensajes Hardcodeados
- [ ] **3.1** Eliminar `BuildSimpleAck()` del PipelineOrchestrator
- [ ] **3.2** Eliminar `BuildBroadcastReply()` del PipelineOrchestrator
- [ ] **3.3** Eliminar `BuildSameDayReply()` del PipelineOrchestrator
- [ ] **3.4** Eliminar `BuildEventInquiryReply()` del PipelineOrchestrator
- [ ] **3.5** Eliminar `BuildInfoReply()` del PipelineOrchestrator
- [ ] **3.6** Migrar `BuildConfirmationMessage()` a prompt de AI

### Fase 4: Refactorizar Handlers
- [ ] **4.1** Agregar tools a CancellationHandler
- [ ] **4.2** Agregar tools a ModificationHandler
- [ ] **4.3** Los handlers usan tools para enviar mensajes en lugar de strings

### Fase 5: Pipeline Orchestrator Simplificado
- [ ] **5.1** Simplificar PipelineOrchestrator para usar Agent
- [ ] **5.2** Eliminar lógica de respuestas predefinidas
- [ ] **5.3** WebhookController solo recibe y delega al Agent

### Fase 6: Testing y Validación
- [ ] **6.1** Tests unitarios para nuevo sistema de Agent
- [ ] **6.2** Tests de integración con +34692747052
- [ ] **6.3** Verificar todos los flujos: booking, cancelación, modificación

---

## Detalle Técnico

### 1.1 Tool `send_message`
```csharp
// En ToolExecutor.cs
"send_message" => await ExecuteSendMessage(input, phoneNumber, ct),

private async Task<ToolResult> ExecuteSendMessage(JsonElement input, string phoneNumber, CancellationToken ct)
{
    var message = input.TryGetProperty("message", out var m) ? m.GetString() : null;
    if (string.IsNullOrEmpty(message))
        return new ToolResult { IsError = true, Content = "Missing 'message' parameter" };

    await _whatsApp.SendTextAsync(phoneNumber, message, ct);
    return new ToolResult { Content = $"Message sent to {phoneNumber}" };
}
```

### 1.2 Formato Anthropic Tools
```csharp
public static List<ToolDefinition> GetAgentTools() => new()
{
    new ToolDefinition
    {
        Name = "send_message",
        Description = "Envía un mensaje de WhatsApp al usuario. Usar para responder.",
        InputSchema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["message"] = new JsonObject { ["type"] = "string", ["description"] = "Mensaje a enviar" }
            },
            ["required"] = new JsonArray { "message" }
        }
    },
    // ... otras tools
};
```

### 2.2 Loop de Tool Calls
```csharp
public async Task<string> AgentLoopAsync(string systemPrompt, string userMessage, List<ChatMessage> history, CancellationToken ct)
{
    var messages = BuildMessages(systemPrompt, userMessage, history);
    var tools = GetAgentTools();

    while (true)
    {
        var response = await _ai.GenerateWithToolsAsync(systemPrompt, userMessage, messages, tools, ct);

        foreach (var block in response.Content)
        {
            if (block is TextBlock text)
            {
                if (!string.IsNullOrEmpty(text.Text))
                    return text.Text; // Respuesta final
            }
            else if (block is ToolUseBlock tool)
            {
                var result = await _toolExecutor.ExecuteAsync(tool.Name, tool.Input, phoneNumber, ct);
                messages.Add(tool.ToMessage());
                messages.Add(new ToolResultMessage(tool.Id, result.Content));
            }
        }

        if (response.StopReason == "end_turn")
            break;
    }

    return "Lo siento, no pude generar una respuesta.";
}
```

---

## Flujo Propuesto

```
1. WebhookController recibe mensaje
2. AgentLoop inicia con:
   - System prompt con instrucciones de agente
   - Mensaje del usuario
   - Tools disponibles (incluyendo send_message)

3. Agent:
   a) Usa fetch_whatsapp_history → obtiene contexto
   b) Analiza intent
   c) Si necesita disponibilidad → check_availability
   d) Si necesita reservas → get_bookings
   e) Para RESPONDER → llama send_message

4. ToolExecutor.send_message() envía via WhatsAppService

5. Agent confirma envío → termina loop
```

---

## Beneficios

1. **100% AI-driven**: Todos los mensajes generados por IA
2. **Flexible**: Cambios en prompts, no en código
3. **Consistente**: Mismo estilo de mensaje siempre
4. **Debuggable**: Logs claros de tool calls
5. **Extensible**: Fácil agregar nuevas funcionalidades

---

## Riesgos y Mitigaciones

| Riesgo | Mitigación |
|--------|------------|
| AI olvida llamar send_message | Prompt incluye regla de SIEMPRE llamar tool |
| Mensajes duplicados | Deduplicación en WebhookController |
| Loop infinito de tools | Max 10 iteraciones |
| Latencia alta | Parallel tool calls cuando sea posible |

---

## Referencias

- `src/BotGenerator.Core/Services/ToolExecutor.cs` - Implementación actual de tools
- `src/BotGenerator.Core/Pipeline/ResponseGeneratorNode.cs` - Generación actual de respuestas
- `src/BotGenerator.Core/Services/MinimaxService.cs` - API de Anthropic
- `src/BotGenerator.Api/Controllers/WebhookController.cs` - Punto de entrada
