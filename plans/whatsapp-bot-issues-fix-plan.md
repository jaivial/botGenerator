# WhatsApp Bot - Comprehensive Fix Plan

## Issues to Fix

### 1. ALWAYS Verify Reservations Before Claiming (CRITICAL)
**Problem:** Bot claimed Juan had a reservation for "domingo 3 de mayo" when he never made one. WhatsApp history can be outdated (admins may have deleted manually).

**Solution:** Before answering ANYTHING about reservations, always call `get_bookings` to verify current status.

**Changes to system prompt:**
```
REGLA CRITICA: ANTES de decir que el usuario TIENE o NO tiene una reserva,
SIEMPRE usa la herramienta get_bookings para verificar el estado actual.
No confies en el historial de WhatsApp para saber si hay reservas activas.
Solo el historial de WhatsApp dice QUE SE HABLÓ, no si la reserva existe.
```

### 2. Handle "Avísame si alguien cancela" (Cancellation Notifications)
**Problem:** User asked to be notified of cancellations, but bot doesn't have this feature.

**Solution:** 
- Recognize this intent
- Explain the feature doesn't exist
- Suggest calling the restaurant directly
- Consider adding to a notification list (future feature)

**New flow:**
```
Cuando usuario dice "avísame si alguien cancela", "notify me", "avisame":
1. Verificar reservas con get_bookings
2. Explicar que no tenemos sistema de notificación
3. Sugerir llamar al restaurante: +34 638 857 294
```

### 3. Grammar/Unicode Issues
**Problem:** Bot wrote "Wouldas" instead of "¿Quieres" - escaped Unicode not decoded.

**Status:** ✅ ALREADY FIXED - DecodeUnicodeEscapes() added to ToolExecutor

### 4. Date Calculation for Relative Dates
**Problem:** Bot confused "domingo 17 de mayo" vs "el anterior domingo" (should be 10 de mayo)

**Status:** ✅ ALREADY FIXED - System prompt updated with clearer date calculation rules

### 5. Automatic Context Fetching
**Problem:** Bot didn't have conversation context when processing messages

**Status:** ✅ ALREADY FIXED - WhatsApp history fetched automatically before processing

### 6. Date Format for Booking Creation
**Problem:** `BookingData.DateForDatabase` expected dd/MM/yyyy but AI sends yyyy-MM-dd

**Status:** ✅ ALREADY FIXED - DateForDatabase now handles both formats

---

## Implementation Tasks

### Task 1: Critical - Always Verify Reservations
**File:** `bot/src/BotGenerator.Core/Services/AgentOrchestrator.cs`

**Changes:**
1. Update system prompt to emphasize always checking `get_bookings` first
2. Add explicit rule: "WHATSAPP HISTORY IS NOT SOURCE OF TRUTH FOR RESERVATIONS"

### Task 2: Handle Cancellation Notifications
**File:** `bot/src/BotGenerator.Core/Services/AgentOrchestrator.cs`

**Changes:**
1. Add new intent handling for cancellation notification requests
2. Update system prompt with this flow

### Task 3: Update System Prompt with All Fixes
**File:** `bot/src/BotGenerator.Core/Services/AgentOrchestrator.cs`

**Changes:**
- Add all the rules mentioned above
- Make it very clear: ALWAYS verify with get_bookings

---

## Testing Requirements
- ALL tests MUST be done with: +34692747052 (Jaime Villanueva)
- NEVER test with other numbers (638770169 is Juan Vea - customer)

---

## Commit Strategy
1. Make changes to AgentOrchestrator.cs (system prompt + logic)
2. Build and test with +34692747052 only
3. Verify fixes work
4. Commit and push
