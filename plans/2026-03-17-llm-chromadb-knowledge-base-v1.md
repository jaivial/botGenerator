# Plan: LLM-Driven Context Handling + ChromaDB Knowledge Base

## Objective

Make the bot's conversation flow more flexible by:
1. Allowing the LLM to handle off-topic questions during flows (instead of hardcoded handlers)
2. Offloading static restaurant knowledge to ChromaDB so the LLM can query relevant instructions on-demand

---

## Problem Statement

### Current Issues

1. **Hardcoded State Machine**: When in modification flow (e.g., asking about tronas/carritos), any response that doesn't match expected patterns gets handled by hardcoded handlers that ignore what the user actually asked

2. **Massive System Prompt**: `system-main.txt` contains 165 lines of instructions that get sent with every LLM request, even when most aren't relevant

3. **No Flexibility**: If user asks "Tener arroz al horno?" during tronas question, the hardcoded handler doesn't know how to respond

---

## Solution Architecture

### Part 1: LLM Handles Off-Topic Questions

Add instructions to the prompt telling the LLM to:
- First respond to what the user actually asks
- Then optionally resume the flow if appropriate

### Part 2: ChromaDB as Knowledge Base

Instead of hardcoding all restaurant knowledge in the prompt, store it in ChromaDB:

- **Restaurant Policies**: Opening hours, menu items, dietary options
- **Flow Instructions**: Step-by-step booking flow rules
- **Response Templates**: Variation patterns for responses
- **FAQ**: Common questions and answers

The LLM can query ChromaDB for relevant context based on the conversation state.

---

## Implementation Phases

### Phase 1: LLM Handles Off-Topic Questions (Quick Win)

- [ ] **1.1** Add "Manejo de Preguntas Fuera de Contexto" section to `system-main.txt`
- [ ] **1.2** Add `availableRiceTypes` to ContextBuilderService context
- [ ] **1.3** Test with the "arroz al horno" scenario

### Phase 2: ChromaDB Knowledge Base Setup

- [ ] **2.1** Create knowledge base collection in ChromaDB: `restaurant-knowledge`
- [ ] **2.2** Define document structure for knowledge entries
- [ ] **2.3** Create seeding script to populate initial knowledge

### Phase 3: Implement ChromaDB Query Integration

- [ ] **3.1** Add `QueryKnowledgeAsync` method to ChromaConversationVectorStore
- [ ] **3.2** Update MainConversationAgent to query knowledge base
- [ ] **3.3** Modify prompt to use `{{relevantKnowledge}}` placeholder

### Phase 4: Decompose system-main.txt

- [ ] **4.1** Extract static data (hours, menu) to ChromaDB
- [ ] **4.2** Extract flow rules to ChromaDB  
- [ ] **4.3** Keep only dynamic/flow-critical instructions in prompt
- [ ] **4.4** Update ContextBuilderService to query ChromaDB for context

### Phase 5: Testing & Optimization

- [ ] **5.1** Test booking flow end-to-end
- [ ] **5.2** Test off-topic question handling
- [ ] **5.3** Measure prompt size reduction
- [ ] **5.4** Optimize ChromaDB queries (TopK, filters)

---

## Detailed Tasks

### Task 1.1: Add Off-Topic Handling to Prompt

**File**: `src/BotGenerator.Prompts/restaurants/villacarmen/system-main.txt`

**Add at end**:
```markdown
## MANEJO DE PREGUNTAS FUERA DE CONTEXTO

**REGLA UNIVERSAL:** Si el usuario hace una pregunta que NO está relacionada con lo que acabas de preguntar, PRIMERO responde a su pregunta de forma útil, luego puedes retomar el flujo de conversación si corresponde.

**Ejemplo:**
- Bot: "¿Necesitáis alguna trona o vais a traer algún carrito de bebé?"
- Usuario: "¿Tenéis arroz al horno?"
- ✅ RESPUESTA: "Sí, tenemos arroz al horno..." (responde a la pregunta, luego retoma)

## TIPOS DE ARROZ DISPONIBLES
{{availableRiceTypes}}
```

### Task 1.2: Add Available Rice Types to Context

**File**: `src/BotGenerator.Core/Services/ContextBuilderService.cs`

**Add to BuildContext method**:
```csharp
// Fetch available rice types for prompt
try
{
    if (_menuRepository != null)
    {
        var riceTypes = await _menuRepository.GetActiveRiceTypesAsync(cancellationToken);
        context["availableRiceTypes"] = string.Join(", ", riceTypes);
    }
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to get rice types");
    context["availableRiceTypes"] = "Paella Valenciana, Arroz a banda, Arroz Negro";
}
```

### Task 2.1: Create Knowledge Base Collection

**New File**: `src/BotGenerator.Core/Services/RestaurantKnowledgeService.cs`

```csharp
public class RestaurantKnowledgeService
{
    // Knowledge categories:
    // - policies: opening hours, booking rules, payment methods
    // - menu: rice types, ingredients, allergens
    // - flows: booking steps, modification steps
    // - faq: common questions
    
    public async Task<List<KnowledgeEntry>> QueryAsync(
        string category, 
        string query, 
        int topK = 3);
    
    public async Task SeedInitialKnowledgeAsync();
}
```

### Task 2.2: Knowledge Entry Schema

```json
{
  "id": "policy-opening-hours",
  "category": "policies",
  "title": "Horario de apertura",
  "content": "Abrimos de 13:30 a 17:00 (domingo a jueves)...",
  "keywords": ["horario", "abrimos", "cerramos", "hora"],
  "active": true
}
```

### Task 2.3: Initial Knowledge Seeding

**Seed Data Categories**:

1. **Políticas** (10-15 entries)
   - Horarios de apertura
   - Política de reservas (mínimo 2 días antes)
   - Política de cancelación
   - Métodos de pago
   - Menú infantil (no disponible)
   - Terraza (no disponible)
   - Máximo de personas por reserva
   - Información de contacto

2. **Menú** (15-20 entries)
   - Tipos de arroz disponibles
   - Ingredientes de cada arroz
   - Alérgenos
   - Opciones sin gluten
   - Bebidas disponibles

3. **Flujos** (5-10 entries)
   - Pasos para nueva reserva
   - Pasos para modificar reserva
   - Pasos para cancelar
   - Preguntas obligatorias (arroz, tronas, carritos)

4. **FAQ** (10-15 entries)
   - Preguntas comunes y respuestas

### Task 3.1: Query Knowledge from ChromaDB

**File**: `src/BotGenerator.Core/Services/ChromaConversationVectorStore.cs`

```csharp
public async Task<List<KnowledgeEntry>> QueryKnowledgeAsync(
    string query,
    string? category = null,
    int topK = 3,
    CancellationToken cancellationToken = default)
{
    // Query ChromaDB for relevant knowledge entries
    // Filter by category if specified
    // Return topK results
}
```

### Task 3.3: Update Prompt Placeholder

**In system-main.txt**, replace static sections with:
```markdown
## INFORMACIÓN RELEVANTE DEL RESTAURANTE
{{relevantKnowledge}}
```

**In ContextBuilderService**:
```csharp
// Query ChromaDB for relevant knowledge based on conversation state
var relevantKnowledge = await _knowledgeService.QueryAsync(
    category: "all", 
    query: message.MessageText + " " + state.Stage,
    topK: 5);
context["relevantKnowledge"] = FormatKnowledge(relevantKnowledge);
```

### Task 4.1-4.4: Decompose Prompt

**Extract to ChromaDB**:
- ✅ Static info (opening hours, policies)
- ✅ Menu items and rice types
- ✅ Response variations (already in ResponseVariations.cs)
- ❌ Keep: Flow logic and critical rules

**Keep in Prompt**:
- Identity and role
- Flow logic (how to handle booking/modification)
- Critical rules (today booking = call, etc.)
- Style guidelines

---

## Verification Criteria

- [ ] User can ask "arroz al horno" during tronas question and get response
- [ ] ChromaDB contains all restaurant knowledge
- [ ] LLM can query ChromaDB for relevant info
- [ ] Prompt size reduced by 30-50%
- [ ] All existing flows still work correctly
- [ ] Response times within acceptable limits

---

## Alternative Approaches Considered

1. **Use function calling**: Let LLM call functions to get info
   - ✅ More precise control
   - ❌ Requires more complex implementation

2. **Keep all in prompt**: No ChromaDB
   - ✅ Simpler
   - ❌ Larger prompts, slower, less flexible

3. **Hybrid**: Some in ChromaDB, some in prompt (CHOSEN)
   - ✅ Balance of flexibility and control
   - ✅ Gradual migration possible

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| ChromaDB query adds latency | Medium | Low | Cache common queries |
| LLM doesn't use knowledge base | Medium | High | Add explicit instructions in prompt |
| Migration breaks existing flows | Low | High | Test extensively before deploying |
| Knowledge base gets out of sync | Medium | Medium | Add update mechanism |

---

## Timeline Estimate

- **Phase 1**: 1-2 hours (quick win)
- **Phase 2-3**: 2-3 hours (infrastructure)
- **Phase 4**: 2-3 hours (decomposition)
- **Phase 5**: 1-2 hours (testing)

**Total**: ~8-10 hours
