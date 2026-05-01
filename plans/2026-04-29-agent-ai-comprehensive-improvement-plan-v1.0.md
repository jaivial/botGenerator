# Plan de Mejora del Sistema de Agente IA para Alquería Villa Carmen

## Resumen Ejecutivo

Este plan detalla las mejoras necesarias para migrar todos los flujos de conversación (reservas, cancelaciones, modificaciones y consultas) a un sistema basado en agentes de IA con tool calls, eliminando la lógica hardcodeada en C#.

---

## Estado Actual

### Lo que funciona:
- ✅ **NewBooking**: Agente usa tools para verificar disponibilidad
- ✅ **Greeting/Acknowledgment**: Agente responde con send_message
- ✅ **OffTopic**: Agente responde usando tools (get_rice_menu, get_restaurant_info)
- ✅ **Schedule questions**: Handler directo con horario correcto (13:30-18:00, cocina hasta 15:30)

### Lo que NO funciona completamente:
- ❌ **Cancelación**: Usa CancellationHandler con lógica hardcodeada
- ❌ **Modificación**: Usa ModificationHandler con lógica hardcodeada
- ❌ **Consultas de reservas existentes**: No está ruteado al agente
- ❌ **Flujo completo de reserva**: La IA no usa siempre send_message
- ❌ **Multi-turno**: El agente no mantiene contexto entre mensajes

---

## Arquitectura Objetivo

```
WebhookController
       ↓
ContextAnalyzer (detecta intent)
       ↓
┌──────────────────────────────────────────────┐
│           AgentOrchestrator                  │
│  ┌────────────────────────────────────────┐ │
│  │     System Prompt (contexto)            │ │
│  │  + Instrucciones específicas por intent  │ │
│  └────────────────────────────────────────┘ │
│                    ↓                         │
│  ┌────────────────────────────────────────┐ │
│  │     Tool Executor (herramientas)        │ │
│  │                                         │ │
│  │  📋 get_bookings - Consultar reservas  │ │
│  │  📅 check_availability - Disponibilidad│ │
│  │  📊 get_hour_data - Slots por hora     │ │
│  │  📞 get_day_status - Estado del día    │ │
│  │  📱 send_message - Enviar respuesta    │ │
│  │  🍚 get_rice_menu - Tipos de arroz     │ │
│  │  ❌ cancel_booking - Cancelar reserva  │ │
│  │  ✏️ modify_booking - Modificar reserva │ │
│  └────────────────────────────────────────┘ │
│                    ↓                         │
│           WhatsAppService                   │
└──────────────────────────────────────────────┘
```

---

## Herramientas Necesarias (Fase 1)

### 1.1 Herramientas Existentes
| Tool | Descripción | Estado |
|------|-------------|--------|
| `send_message` | Envía WhatsApp | ✅ Implementado |
| `fetch_whatsapp_history` | Historial de chat | ✅ Implementado |
| `get_rice_menu` | Tipos de arroz | ✅ Implementado |
| `get_restaurant_info` | Info del restaurante | ✅ Implementado |
| `check_availability` | Verifica disponibilidad | ✅ Implementado |
| `get_opening_hours` | Horarios por fecha | ✅ Implementado |
| `get_hour_data` | Slots por hora | ✅ Implementado |
| `get_day_status` | Estado del día | ✅ Implementado |
| `get_bookings` | Reservas del usuario | ✅ Implementado |

### 1.2 Herramientas Nuevas Requeridas
| Tool | Descripción | Prioridad |
|------|-------------|-----------|
| `cancel_booking` | Cancela una reserva específica | ALTA |
| `modify_booking` | Modifica campos de una reserva | ALTA |
| `create_booking` | Crea nueva reserva | MEDIA |

---

## Implementación por Flujo

### Flujo 2.1: Cancelación de Reserva

**Estado actual**: Usa `CancellationHandler` con lógica hardcodeada en C#

**Flujo objetivo**:
```
1. Usuario: "Quiero cancelar mi reserva"
   ↓
2. Agente usa get_bookings → Obtiene reservas activas
   ↓
3. Agente pregunta cuál reserva (si hay varias)
   ↓
4. Agente muestra detalles de la reserva seleccionada
   ↓
5. Usuario confirma: "Sí, cancelar"
   ↓
6. Agente usa cancel_booking → Cancela en DB
   ↓
7. Agente usa send_message → Confirma cancelación
```

**Pasos de implementación**:
- [ ] 2.1.1: Crear tool `cancel_booking` en ToolExecutor
- [ ] 2.1.2: Definir tool en AgentToolDefinitions
- [ ] 2.1.3: Crear prompt para intent Cancellation
- [ ] 2.1.4: Testear flujo completo con +34692747052

---

### Flujo 2.2: Modificación de Reserva

**Estado actual**: Usa `ModificationHandler` con lógica hardcodeada (2000+ líneas)

**Flujo objetivo**:
```
1. Usuario: "Quiero cambiar mi reserva del domingo"
   ↓
2. Agente usa get_bookings → Obtiene reservas
   ↓
3. Agente pregunta cuál reserva y qué cambiar
   ↓
4. Usuario: "Cambiar la hora a las 14:30"
   ↓
5. Agente usa check_availability → Verifica nueva hora
   ↓
6. Si disponible: Agente usa modify_booking
   ↓
7. Agente usa send_message → Confirma cambio
```

**Pasos de implementación**:
- [ ] 2.2.1: Crear tool `modify_booking` en ToolExecutor
- [ ] 2.2.2: Definir tool en AgentToolDefinitions
- [ ] 2.2.3: Crear prompt para intent Modification
- [ ] 2.2.4: Testear flujo completo con +34692747052

---

### Flujo 2.3: Consulta de Reservas Existentes

**Estado actual**: No existe, se mezcla con otros intents

**Flujo objetivo**:
```
1. Usuario: "¿Cuándo tengo reserva?"
   ↓
2. Agente usa get_bookings → Obtiene todas las reservas
   ↓
3. Agente usa send_message → Muestra lista de reservas
```

**Pasos de implementación**:
- [ ] 2.3.1: Asegurar que get_bookings funciona correctamente
- [ ] 2.3.2: Crear prompt para intent InfoRequest
- [ ] 2.3.3: Testear con +34692747052

---

### Flujo 2.4: Creación de Reserva (MEJORAR)

**Estado actual**: Parcialmente migrado, pero con problemas

**Problemas identificados**:
1. La IA no siempre usa send_message al final
2. El flujo de datos (fecha, hora, personas) no se valida completamente
3. No hay confirmación antes de crear la reserva

**Flujo objetivo**:
```
1. Usuario: "Quiero reservar para el 15 de mayo, 4 personas"
   ↓
2. Agente usa get_day_status → Verifica si está abierto
   ↓
3. Agente usa get_opening_hours → Obtiene horarios
   ↓
4. Agente pregunta: "¿A qué hora? Tenemos 13:30, 14:30, 15:30"
   ↓
5. Usuario: "A las 14:30"
   ↓
6. Agente usa check_availability → Verifica disponibilidad
   ↓
7. Agente usa get_rice_menu → Ofrece tipos de arroz
   ↓
8. Usuario selecciona arroz
   ↓
9. Agente muestra resumen y pide confirmación
   ↓
10. Usuario: "Sí, confirmar"
   ↓
11. Agente usa create_booking → Crea reserva
   ↓
12. Agente usa send_message → Mensaje de confirmación
```

**Pasos de implementación**:
- [ ] 2.4.1: Crear tool `create_booking` en ToolExecutor
- [ ] 2.4.2: Mejorar prompts para extraer datos faltantes
- [ ] 2.4.3: Agregar paso de confirmación antes de crear
- [ ] 2.4.4: Testear flujo completo

---

## Mejoras del Sistema de Prompts

### 3.1 Prompts Específicos por Intent

```csharp
// PipelineOrchestrator.cs - RouteToAgentAsync
var intentInstructions = new Dictionary<PipelineIntent, string>
{
    [PipelineIntent.NewBooking] = @"
## PARA RESERVAS (NewBooking):

Sigue estos pasos en ORDEN:
1. Primero: Usa get_bookings para ver si el usuario ya tiene reservas
2. Si quiere modificar una existente → Routing a Modification
3. Si es nueva reserva:
   a) Pregunta la fecha si no la dio
   b) Pregunta la hora (ofrece opciones usando get_opening_hours)
   c) Pregunta el número de personas
   d) Usa check_availability para verificar
   e) Usa get_rice_menu para ofrecer arroz
   f) Muestra resumen y pide confirmación
4. Para CONFIRMAR: Usa create_booking
5. Para RESPONDER: Usa SIEMPRE send_message
",

    [PipelineIntent.Cancellation] = @"
## PARA CANCELACIONES (Cancellation):

1. Usa get_bookings para ver las reservas del usuario
2. Si tiene varias, pregunta cuál quiere cancelar
3. Muestra los detalles de la reserva a cancelar
4. Pide confirmación explícita (""¿Estás seguro de que quieres cancelar?"")
5. Usa cancel_booking para cancelar
6. Confirma la cancelación con send_message
",

    [PipelineIntent.Modification] = @"
## PARA MODIFICACIONES (Modification):

1. Usa get_bookings para ver las reservas
2. Pregunta cuál reserva quiere modificar
3. Pregunta qué quiere cambiar (fecha/hora/personas/arroz)
4. Para CAMBIAR FECHA/HORA:
   a) Usa get_day_status para verificar si está abierto
   b) Usa get_opening_hours para ver horarios
   c) Usa check_availability para verificar disponibilidad
5. Para CONFIRMAR: Usa modify_booking
6. Confirma con send_message
",

    [PipelineIntent.InfoRequest] = @"
## PARA CONSULTAS (InfoRequest):

1. Usa get_bookings para ver las reservas
2. Usa get_day_status/get_opening_hours si pregunta por fechas
3. Responde con send_message
"
};
```

### 3.2 Sistema de Contexto Multi-Turno

**Problema actual**: Cada mensaje es independiente, no hay memoria

**Solución**: Implementar session context storage

```csharp
// Nuevo: AgentSessionState
public class AgentSessionState
{
    public string PhoneNumber { get; set; }
    public PipelineIntent CurrentIntent { get; set; }
    public BookingData? PendingBooking { get; set; }
    public BookingRecord? SelectedBooking { get; set; }
    public int ConversationTurn { get; set; }
    public Dictionary<string, object> ExtractedData { get; set; }
    public DateTime LastActivity { get; set; }
}

// Store en memoria o Redis
public interface IAgentSessionStore
{
    Task<AgentSessionState?> GetAsync(string phoneNumber);
    Task SetAsync(string phoneNumber, AgentSessionState state);
    Task ClearAsync(string phoneNumber);
}
```

---

## Testing (Fase 4)

### 4.1 Escenarios de Test con +34692747052

| Escenario | Descripción | Esperado |
|----------|-------------|----------|
| TC-01 | "Hola" | Saludo amigable |
| TC-02 | "Quiero reservar para mañana" | Solicita datos faltantes |
| TC-03 | "Tengo reserva para el 15" | Muestra detalles de la reserva |
| TC-04 | "Quiero cancelar mi reserva" | Cancela tras confirmación |
| TC-05 | "Cambiar hora a las 14:30" | Modifica tras verificación |
| TC-06 | "¿Cuándo tengo reserva?" | Lista todas las reservas |
| TC-07 | "Qué arroz tenéis?" | Muestra menú de arroces |
| TC-08 | "A qué hora abrís?" | Horario: 13:30-18:00 |

### 4.2 Scripts de Testing Automatizado

```bash
#!/bin/bash
# test_agent_flows.sh

PHONE="34692747052"
BOT_URL="http://localhost:5050/api/webhook/whatsapp-webhook"

test_flow() {
    local name=$1
    local message=$2
    echo "Testing: $name"
    curl -s -X POST "$BOT_URL" \
        -H "Content-Type: application/json" \
        -d "{\"eventType\":\"messages\",\"message\":{\"chatid\":\"${PHONE}@s.whatsapp.net\",\"pushname\":\"Test\",\"fromMe\":false,\"messageTimestamp\":\"$(date +%s)\",\"messageid\":\"TEST_$(date +%s)\",\"text\":\"$message\"}}"
    sleep 5
}

# Ejecutar tests
test_flow "Saludo" "Hola"
test_flow "Consulta horario" "A qué hora abrís?"
test_flow "Consulta reservas" "¿Cuándo tengo reserva?"
test_flow "Nueva reserva" "Quiero reservar para el 20 de mayo"
```

---

## Riesgos y Mitigaciones

| Riesgo | Impacto | Mitigación |
|--------|---------|------------|
| La IA no sigue el flujo | Alto | Prompts más explícitos, ejemplos en el prompt |
| Timeout en llamadas a API | Medio | Retry logic con backoff |
| Datos inconsistentes | Alto | Validación en tools + en código |
| Mensajes duplicados | Bajo | Deduplicación por messageId |

---

## Timeline Propuesto

| Fase | Descripción | Días estimados |
|------|-------------|----------------|
| 1 | Herramientas nuevas (cancel_booking, modify_booking) | 1 |
| 2 | Prompts mejorados por intent | 1 |
| 3 | Session state para multi-turno | 2 |
| 4 | Testing completo | 1 |
| 5 | Deploy y monitorización | 1 |

**Total: ~6 días laborables**

---

## Métricas de Éxito

1. **100% de mensajes procesados por el agente** (no por handlers hardcodeados)
2. **0 mensajes duplicados** en WhatsApp
3. **Tasa de éxito > 90%** en flujos de reserva/cancelación/modificación
4. **Tiempo de respuesta < 10 segundos** por mensaje
5. **0 errores de timeout** con retry logic
