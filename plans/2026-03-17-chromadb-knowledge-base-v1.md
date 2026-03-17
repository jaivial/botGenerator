# Plan: ChromaDB Knowledge Base for Restaurant Data

## Objective

Move static restaurant data (policies, rice types, common responses) from hardcoded prompts to ChromaDB, reducing prompt size by 30-40% and enabling dynamic updates without code deployment.

## Current State

- `system-main.txt`: 192 lines with hardcoded policies, rice types, rules
- Rice types fetched from MySQL but passed as context variable
- No centralized knowledge base for restaurant data

## Target State

- ChromaDB collection `restaurant-knowledge` with structured documents
- LLM can query relevant knowledge dynamically
- Easy updates via API without code deployment

---

## Implementation Plan

### Phase 1: Define Knowledge Base Schema (1 hour)

- [ ] **1.1** Design document structure for each knowledge type
- [ ] **1.2** Define collection schema in `IConversationVectorStore.cs`
- [ ] **1.3** Add methods to vector store interface

**Document Types:**
```json
{
  "type": "policy",
  "key": "no_infant_menu",
  "content": "No tenemos menú infantil. Todos los comensales deben consumir un menú regular.",
  "keywords": ["menú infantil", "niños", "menu infantil"],
  "es_required": true
}
```

```json
{
  "type": "rice",
  "key": "arroz_a_banda", 
  "content": "Arroz a banda - tradicional arroz Valenciano con caldo de pescado. Precio base.",
  "keywords": ["a banda", "tradicional", "pescado"],
  "price_modifier": 0,
  "available": true,
  "min_servings": 2
}
```

```json
{
  "type": "response",
  "key": "greeting",
  "content": "¡Hola {name}! ¿En qué puedo ayudarte?",
  "keywords": ["hola", "buenas", "saludo"]
}
```

```json
{
  "type": "flow_step",
  "key": "hora_validation",
  "content": "El restaurante abre a las 13:30 y cierra entre 17:00-18:00. Rechaza horas antes de 13:30 o después del cierre.",
  "keywords": ["hora", "horario", "abrir", "cerrar", "13:30"]
}
```

---

### Phase 2: Implement Knowledge Base Service (2 hours)

- [ ] **2.1** Create `RestaurantKnowledgeService.cs` in `BotGenerator.Core/Services/`
- [ ] **2.2** Implement `QueryKnowledgeAsync(type, query)` method
- [ ] **2.3** Implement `GetRiceTypesAsync()` method (move from MenuRepository)
- [ ] **2.4** Implement `GetPoliciesAsync()` method
- [ ] **2.5** Register service in DI container (Program.cs)

**Service Interface:**
```csharp
public interface IRestaurantKnowledgeService
{
    Task<List<KnowledgeDocument>> QueryAsync(string query, string? type = null, int topK = 3);
    Task<List<RiceType>> GetRiceTypesAsync();
    Task<List<Policy>> GetPoliciesAsync();
    Task SeedInitialDataAsync();
}
```

---

### Phase 3: Create Seed Script (1 hour)

- [ ] **3.1** Create `scripts/seed-knowledge-base.php` for PHP
- [ ] **3.2** Seed initial policies (no infantil, no terraza)
- [ ] **3.3** Seed rice types from MySQL FINDE table
- [ ] **3.4** Seed common responses

**OR create C# seed command:**
- [ ] **3.5** Add `dotnet run --project BotGenerator.Api -- seed-knowledge` command

---

### Phase 4: Update Context Builder (2 hours)

- [ ] **4.1** Modify `ContextBuilderService.cs` to query knowledge base
- [ ] **4.2** Replace hardcoded rice types with ChromaDB query
- [ ] **4.3** Add `relevantPolicies` to context
- [ ] **4.4** Add `commonResponses` to context

**Context Variables After:**
```csharp
context["availableRiceTypes"] = await _knowledge.GetRiceTypesAsync();
// Returns: "Arroz a banda, Arroz de señoret, Paella Valenciana..."

context["relevantPolicies"] = await _knowledge.QueryAsync("menú infantil");
// Returns: "No tenemos menú infantil..."

context["relevantFlowSteps"] = await _knowledge.QueryAsync("hora validación", "flow_step");
// Returns: validation rules for time
```

---

### Phase 5: Simplify Prompts (2 hours)

- [ ] **5.1** Remove hardcoded policies from `system-main.txt`
- [ ] **5.2** Replace with: `{{relevantPolicies}}`
- [ ] **5.3** Remove hardcoded rice list
- [ ] **5.4** Replace with: `{{availableRiceTypes}}` (already done)
- [ ] **5.5** Update `restaurant-info.txt` to use dynamic data

**Before (system-main.txt):**
```markdown
- **NO tenemos menú infantil** - Todos los comensales deben consumir un menú regular
- **NO tenemos terraza** - Solo disponemos de interior
```

**After:**
```markdown
## POLÍTICAS DEL RESTAURANTE
{{relevantPolicies}}

## TIPOS DE ARROZ DISPONIBLES
{{availableRiceTypes}}
```

---

### Phase 6: Testing (1 hour)

- [ ] **6.1** Test rice type query returns correct data
- [ ] **6.2** Test policy query for "menú infantil"
- [ ] **6.3** Test booking flow end-to-end
- [ ] **6.4** Verify ChromaDB is populated with seed data
- [ ] **6.5** Test dynamic updates (add new rice type via API)

---

## Verification Criteria

- [ ] ChromaDB collection `restaurant-knowledge` exists
- [ ] Rice types query returns all available rices from MySQL
- [ ] Policy query returns relevant policy for user question
- [ ] System-main.txt reduced by at least 30%
- [ ] Booking flow works identically to before
- [ ] No regression in LLM responses

---

## Files to Modify

| File | Change |
|------|--------|
| `IConversationVectorStore.cs` | Add `QueryKnowledgeAsync` |
| `ChromaConversationVectorStore.cs` | Implement knowledge queries |
| `RestaurantKnowledgeService.cs` | NEW - Knowledge base service |
| `ContextBuilderService.cs` | Query knowledge for context |
| `system-main.txt` | Replace hardcoded with variables |
| `restaurant-info.txt` | Simplify with dynamic data |
| `Program.cs` | Register new service |

---

## Alternative Approaches

### Option 1: Full ChromaDB Query (More Complex)
LLM sends separate query to ChromaDB for each user question:
```
User: "¿Tienen menú infantil?"
LLM → ChromaDB: query("menú infantil", type="policy")
LLM → User: "No tenemos menú infantil..."
```
**Pros:** Most flexible, smaller prompts
**Cons:** Requires LLM to make multiple API calls

### Option 2: Hybrid (Recommended)
Pre-fetch relevant knowledge into context:
```
Context includes: { relevantPolicies, availableRiceTypes, commonResponses }
```
**Pros:** Simpler implementation, same prompt structure
**Cons:** Still passes data in context

---

## Risk Assessment

| Risk | Mitigation |
|------|------------|
| ChromaDB down | Fall back to MySQL/MenuRepository |
| Empty knowledge base | Auto-seed on startup if empty |
| Query too slow | Cache knowledge for 1 hour |
| LLM not using context | Add explicit instructions in prompt |

---

## Timeline

- **Phase 1-2**: 3 hours
- **Phase 3-4**: 3 hours  
- **Phase 5-6**: 3 hours
- **Total**: ~9 hours

Can be done in 2-3 sessions.
