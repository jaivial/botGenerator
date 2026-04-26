# Chat Analysis & AI Node Improvement Plan

## Chat Issues Identified (Raúl conversation, 4/26/26)

### BUG 1: Intent Misclassification — Modification confirmation treated as new booking
**Lines 83-91**: User confirms rice change ("Si") for existing 13:30 booking. The ContextAnalyzer (Node 1) classifies this as `ConfirmBooking` (new booking pipeline) instead of `Modification`. This sends it through `HandleBookingConfirmationAsync` instead of the modification handler's `HandleConfirmationAsync`.

**Root cause**: The pipeline checks `analysis.Intent == PipelineIntent.Modification` BEFORE running validation. But the AI classifier doesn't know there's an active modification state — it only sees the pending booking state. When it sees "Si" + a pending booking with SummaryShown=false (because the pending booking is for a NEW booking, not the modification), it classifies as `ConfirmBooking`.

**Impact**: 
- Availability check runs for a NEW booking without excluding the existing booking ID
- The existing booking occupies the time slot → "hubo un cambio en la disponibilidad"
- Creates false negatives on availability

### BUG 2: False availability rejection for 14:00 and 14:30
**Lines 92-97**: User suggests 14:00, bot says no space. User suggests 14:30, bot says no space.

**Root cause**: Because the modification flow was hijacked by the new booking pipeline, the availability checks for 14:00/14:30 are run as NEW booking checks. The `excludeBookingId` is `null` instead of the existing booking's ID. Since the existing 13:30 booking (4 people) already occupies capacity, the hourly slots show as full.

If `excludeBookingId` had been the existing booking's ID, the 4 people would have been excluded from the count and there would have been space.

Additionally, the `GetHourDataAsync` default capacity allocation uses equal percentage split. With a daily limit of 45 and 4 active hours, each hour gets ~11 seats. An existing booking of 4 people at 13:30 leaves 7 free at 13:30 but if capacity was moved/allocated differently, other hours could also appear full.

### BUG 3: Cancellation selection regex doesn't handle semicolons
**Line 132**: User says "La de las 14;30" (semicolon instead of colon).

**Root cause**: `TryParseBookingSelection` in both `CancellationHandler` and `ModificationHandler` uses `Regex.Match(normalized, @"(?:a\s+las?\s+)?(\d{1,2}):(\d{2})\b")` which only matches `:` as separator. The `;` in "14;30" is a common mobile keyboard typo.

### BUG 4: Cancellation cannot understand natural language disambiguation
**Lines 134-137**: User says "Es la misma diferente hora", "No hay número". The regex parser can't handle these.

**Root cause**: `TryParseBookingSelection` is purely regex-based. When there are multiple bookings on the same date, the user tries to explain "it's the same booking but different hour" — the parser doesn't understand this.

### BUG 5: Wrong booking cancelled
**Lines 139-156**: Confirmation shows 14:30 booking, but cancellation result shows 13:30 booking.

**Root cause hypothesis**: The cancellation flow at line 139 correctly identifies the 14:30 booking via `TryParseBookingSelection` time match. `BuildConfirmationResponse` uses `booking.TimeFormatted` which shows 14:30. But then `HandleConfirmationAsync` uses `state.SelectedBooking` to cancel. 

The most likely cause is that between the confirmation message and the user's "Si" response, the pipeline re-routes through the ContextAnalyzer which classifies "Si" as `Cancellation` again, but the cancellation state store might have been corrupted or the state was reloaded from a stale cache.

Alternatively, the booking IDs in the `FoundBookings` list may not correspond to the displayed order, or the state was overwritten between messages.

### BUG 6: Double booking created
**Lines 98-121**: Two bookings exist for the same person on the same day (13:30 and 14:30).

**Root cause**: The rice modification at 13:30 was misclassified as a new booking confirmation. The pipeline tried to create a new booking (which failed due to availability). Meanwhile, the original 13:30 booking still existed. Then at line 112-121, a new booking at 14:30 was successfully created through a separate process (likely the web form auto-confirmation or a delayed bot action).

---

## Current Architecture

```
Message arrives
    ↓
WebhookController.HandleWhatsAppWebhook
    ↓
PipelineOrchestrator.ProcessAsync
    ├── Node 1: ContextAnalyzerNode (Gemini AI) → ContextAnalysisResult
    │     Classifies intent, extracts booking data
    │
    ├── Early exits (Acknowledgment, BroadcastReply, SameDay, EventInquiry)
    ├── Delegates (Cancellation → CancellationHandler, Modification → ModificationHandler)
    │
    ├── Node 2: ValidationEnrichmentNode (deterministic)
    │     Checks availability, validates rice, checks duplicates
    │
    ├── Booking confirmation/delegation
    │
    └── Node 3: ResponseGeneratorNode (Gemini AI) → WhatsApp message
```

**State stores (in-memory)**:
- `IPendingBookingStore` — new booking in progress
- `ICancellationStateStore` — multi-turn cancellation flow
- `IModificationStateStore` — multi-turn modification flow
- `IPendingRiceStore` — rice options presented to user

---

## Improvement Plan

### 1. STATE-AWARE PRE-ROUTING NODE (NEW — Node 0)

**Problem**: The ContextAnalyzer doesn't know about active modification/cancellation states, so it can't correctly classify responses within those flows.

**Solution**: Add a new pipeline node BEFORE ContextAnalyzer that checks active state stores and injects routing hints:

```csharp
public class StateAwarePreRouter : IPipelineNode<PipelineContext, PreRouteResult>
{
    // Checks: cancellation state, modification state, pending booking state
    // Returns: routing hint for ContextAnalyzer + state context
}
```

**Logic**:
1. If `ICancellationStateStore` has state for this phone → inject hint "ACTIVE_CANCELLATION_FLOW: stage=X, selectedBooking=..."
2. If `IModificationStateStore` has state for this phone → inject hint "ACTIVE_MODIFICATION_FLOW: stage=X, field=Y, pendingChanges=..."
3. If both are null and `IPendingBookingStore` has state → inject "ACTIVE_BOOKING_FLOW: summaryShown=X"

The ContextAnalyzer prompt gets an additional section:
```
## ACTIVE CONVERSATION STATE
{activeStateDescription}
```

With a new rule:
```
### STATE-AWARE CLASSIFICATION (HIGHEST PRIORITY):
1. If ACTIVE_MODIFICATION_FLOW exists and message is "si/no/confirm/cancel" → MUST classify as Modification
2. If ACTIVE_CANCELLATION_FLOW exists and message is "si/no/confirm/cancel" → MUST classify as Cancellation  
3. If ACTIVE_BOOKING_FLOW with SummaryShown=true and message is "si" → ConfirmBooking
```

### 2. FIX: Availability Check Exclusion During Modifications

**Problem**: When modification is misclassified as new booking, the existing booking's capacity isn't excluded.

**Solution**: In `ValidationEnrichmentNode`, when checking availability, also check `IModificationStateStore` for the active booking ID:

```csharp
// Get the booking ID to exclude (from modification state or null)
var excludeBookingId = context.ModificationState?.SelectedBooking?.Id 
    ?? context.CancellationState?.SelectedBooking?.Id;
```

Pass this to `EvaluateAsync` so that even if misclassified, the availability check is correct.

### 3. AI BOOKING SELECTION NODE (NEW)

**Problem**: Regex-based `TryParseBookingSelection` can't handle "14;30", "Es la misma diferente hora", "La de las 14:30 4 raciones de arroz del señoret".

**Solution**: Add a dedicated AI node for booking selection that understands natural language:

```csharp
public class BookingSelectionAgent
{
    public Task<BookingRecord?> SelectBookingAsync(
        string userMessage, 
        List<BookingRecord> bookings,
        CancellationToken ct);
}
```

Prompt:
```
Given the user's message and the list of bookings, determine which booking they're referring to.

Bookings:
1. viernes 01/05/2026 a las 13:30 para 4 personas - Arroz de señoret (4 raciones)
2. viernes 01/05/2026 a las 14:30 para 4 personas - Arroz de señoret (4 raciones)

User message: "La de las 14;30 4 raciones de arroz del señoret"

Respond with ONLY the booking number (1 or 2) or "UNCLEAR".
```

**When to use**: Only when `TryParseBookingSelection` returns null (fallback to AI).

**Also fix the regex** to handle semicolons and periods:
```csharp
// Current: @"(?:a\s+las?\s+)?(\d{1,2}):(\d{2})\b"
// Fixed:   @"(?:a\s+las?\s+)?(\d{1,2})[:;.,h](\d{2})\b"
```

### 4. DUPLICATE BOOKING PREVENTION NODE (NEW)

**Problem**: Multiple bookings can be created for the same phone on the same date.

**Solution**: Add a pre-creation check in `PipelineOrchestrator.HandleBookingConfirmationAsync`:

```csharp
// Before creating booking, check for same-date duplicates
var phone9 = context.Message.SenderNumber;
if (phone9.StartsWith("34")) phone9 = phone9[2..];
var sameDayBookings = context.ExistingBookings
    .Where(b => b.DateFormatted == pending.Date)
    .ToList();

if (sameDayBookings.Count > 0)
{
    // Ask: modify existing or create new?
    return new PipelineResult
    {
        ResponseText = $"Ya tienes una reserva para el {pending.Date} " +
                       $"a las {sameDayBookings[0].TimeFormatted}. " +
                       "¿Quieres modificarla o crear una nueva?",
        // ...
    };
}
```

### 5. ENHANCED CONTEXT FOR CONTEXT ANALYZER

**Problem**: The AI classifier doesn't have enough context about the current flow state.

**Solution**: Add active state information to the prompt in `ContextAnalyzerNode`:

```
## ACTIVE CONVERSATION FLOWS
{activeStateInfo}
```

Where `{activeStateInfo}` is populated from:
- `IModificationStateStore`: "MODIFICATION FLOW ACTIVE: stage=AwaitingConfirmation, field=rice, pendingChanges=Arroz de señoret (4 raciones), booking=01/05/2026 13:30 4pax"
- `ICancellationStateStore`: "CANCELLATION FLOW ACTIVE: stage=AwaitingConfirmation, booking=01/05/2026 14:30 4pax"
- `IPendingBookingStore`: "BOOKING FLOW ACTIVE: date=01/05/2026, time=13:30, people=4, summaryShown=false"

### 6. POST-CANCELLATION VERIFICATION

**Problem**: Wrong booking was cancelled (confirmed 14:30 but 13:30 was cancelled).

**Solution**: In `CancellationHandler.HandleConfirmationAsync`, add a verification step:

```csharp
if (userIntent == "CONFIRM")
{
    var booking = state.SelectedBooking!;
    
    // VERIFY: Re-fetch booking from DB to ensure it matches our state
    var freshBooking = await _bookingRepository.GetBookingByIdAsync(booking.Id, ct);
    if (freshBooking == null)
    {
        return new AgentResponse { AiResponse = "Esta reserva ya no existe." };
    }
    
    // Double-check the booking details match what was shown in confirmation
    // Log the exact booking being cancelled for audit trail
    _logger.LogWarning(
        "CANCELLING booking {BookingId}: {Date} {Time} {People}pax",
        booking.Id, booking.DateFormatted, booking.TimeFormatted, booking.PartySize);
    
    // ... proceed with cancellation
}
```

Also include the booking details in the cancellation success message (currently it uses `ResponseVariations.CancellationSuccess()` which is generic).

### 7. TIME PARSING ROBUSTNESS

**Fix the regex** in both `ModificationHandler` and `CancellationHandler`:

```csharp
// Handle: 14:30, 14;30, 14.30, 14h30, 14 30
var timeMatch = Regex.Match(normalized, @"(?:a\s+las?\s+)?(\d{1,2})[:;.,h\s](\d{2})\b");
```

Also handle "14h30", "2 y media" → 14:30, "3 menos cuarto" → 14:45, etc.

---

## New Pipeline Architecture

```
Message arrives
    ↓
WebhookController
    ↓
PipelineOrchestrator.ProcessAsync
    ├── Node 0: StateAwarePreRouter (NEW — no AI, just state checks)
    │     Checks cancellation/modification/pending stores
    │     Injects routing hints + state context
    │
    ├── Node 1: ContextAnalyzerNode (Gemini AI) — ENHANCED
    │     Receives state hints from Node 0
    │     Classifies intent with full state awareness
    │
    ├── Early exits (unchanged)
    │
    ├── Delegates (Cancellation → CancellationHandler, Modification → ModificationHandler)
    │     Both handlers now use:
    │     - AI BookingSelectionAgent (fallback when regex fails) 
    │     - Robust time parsing (semicolons, etc.)
    │
    ├── Node 2: ValidationEnrichmentNode — ENHANCED
    │     Now receives excludeBookingId from active state
    │     Checks for duplicate bookings
    │
    ├── Booking confirmation — ENHANCED
    │     Duplicate booking prevention
    │     Post-creation verification
    │
    └── Node 3: ResponseGeneratorNode (unchanged)
```

## Summary of New/Modified Files

| File | Change |
|------|--------|
| `Pipeline/StateAwarePreRouter.cs` | **NEW** — Node 0: state-aware routing |
| `Pipeline/PipelineOrchestrator.cs` | Modified — add Node 0, pass state context, duplicate prevention |
| `Pipeline/ContextAnalyzerNode.cs` | Modified — receive and use state hints in prompt |
| `Pipeline/ValidationEnrichmentNode.cs` | Modified — accept excludeBookingId from state |
| `Agents/BookingSelectionAgent.cs` | **NEW** — AI booking selection |
| `Handlers/ModificationHandler.cs` | Modified — use BookingSelectionAgent, fix time regex |
| `Handlers/CancellationHandler.cs` | Modified — use BookingSelectionAgent, fix time regex, add verification |
| `Models/PipelineModels.cs` | Modified — add PreRouteResult, state fields |
| `Services/BookingAvailabilityService.cs` | No changes (already correct) |
