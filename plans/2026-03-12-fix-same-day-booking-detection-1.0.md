# Fix Same-Day Booking Detection for Contextual Responses

## Objective

Fix the bug where the bot incorrectly allows same-day bookings when users respond with simple date expressions like "Para hoy" in conversation context. The system should detect these as same-day booking requests and block them, sending the restaurant's contact card instead.

## Problem Summary

**Current Behavior:** When user says "Para hoy" (for today) in response to the bot asking "dime para qué día y cuántas personas", the bot responds with availability times instead of blocking the same-day request.

**Expected Behavior:** The bot should detect "Para hoy" as a same-day booking request, block it, and send the restaurant management contact card.

**Root Cause:** In `SameDayDetector.IsSameDayBookingRequest()` at `src/BotGenerator.Core/Services/TurnAnalysis/SameDayDetector.cs:22-23`, the function checks for booking intent keywords **before** checking for same-day keywords. Since "para hoy" doesn't contain explicit booking intent words like "reservar" or "mesa para", the function returns `false` before ever checking the same-day keywords list.

## Implementation Plan

### Phase 1: Fix SameDayDetector Logic

- [ ] **Task 1.1:** Reorder the logic in `SameDayDetector.IsSameDayBookingRequest()` to check same-day keywords first
  - **File:** `src/BotGenerator.Core/Services/TurnAnalysis/SameDayDetector.cs`
  - **Rationale:** Same-day keywords like "para hoy" should trigger detection regardless of explicit booking intent words. The intent is implied by the conversation context (user responding to bot's question about date).
  - **Changes:** Move the same-day keyword check (lines 36-51) before the `HasBookingOrModificationIntent` check (lines 20-23), or modify the logic to be more lenient when same-day keywords are present.

- [ ] **Task 1.2:** Add contextual response patterns to same-day keywords
  - **File:** `src/BotGenerator.Core/Services/TurnAnalysis/SameDayDetector.cs`
  - **Rationale:** Users often respond with just the date when asked, without repeating booking intent words.
  - **Changes:** Add patterns like "hoy", "el día de hoy", "para el día de hoy" to the same-day keywords array. Consider standalone "hoy" when it's a short response.

### Phase 2: Add Fallback Check in IntentRouterService

- [ ] **Task 2.1:** Add same-day validation in `HandleBookingAsync` before creating booking
  - **File:** `src/BotGenerator.Core/Services/IntentRouterService.cs`
  - **Lines:** Around 613-656 (where `BookingAvailabilityService.EvaluateAsync` is called)
  - **Rationale:** Defense in depth. Even if the pre-check in WebhookController misses a same-day request, the IntentRouterService should catch it when processing the booking intent.
  - **Note:** This check already exists at lines 617-655 via `BookingAvailabilityService.EvaluateAsync` which returns `same_day` decision. Verify this path is working correctly.

### Phase 3: Verify Contact Card Sending

- [ ] **Task 3.1:** Verify contact card is sent correctly for same-day rejections
  - **Files:** 
    - `src/BotGenerator.Core/Services/IntentRouterService.cs:632-648`
    - `src/BotGenerator.Api/Controllers/WebhookController.cs:986-999`
  - **Rationale:** Ensure the restaurant management contact card (phone: 34638857294) is sent when same-day is detected.
  - **Verification:** Confirm both intro message (`ResponseVariations.SameDayBookingIntro()`) and contact card are sent before the rejection message.

### Phase 4: Update AI Prompt (Optional Enhancement)

- [ ] **Task 4.1:** Add explicit same-day rejection rule to AI system prompt
  - **File:** `src/BotGenerator.Prompts/restaurants/villacarmen/system-main.txt`
  - **Rationale:** As a safety net, instruct the AI to detect and reject same-day bookings even if code checks fail.
  - **Changes:** Add a rule in the "REGLAS CRÍTICAS" section stating that bookings for today must be rejected with a message directing users to call the restaurant.

### Phase 5: Testing

- [ ] **Task 5.1:** Create/update unit tests for `SameDayDetector`
  - **File:** `tests/BotGenerator.Core.Tests/Services/SameDayDetectorTests.cs`
  - **Test Cases:**
    - "Para hoy" → should return `true`
    - "hoy" (standalone short response) → should return `true`
    - "el día de hoy" → should return `true`
    - "hoy te confirmo" → should return `false` (deferral pattern)
    - "mi reserva es hoy" → should return `false` (not a new booking request)

- [ ] **Task 5.2:** Create integration test for the full conversation flow
  - **File:** `tests/BotGenerator.Core.Tests/Conversations/` or `testing/` scripts
  - **Test Case:** Simulate the exact conversation from the bug report:
    1. "Hola" → greeting response
    2. "Tengo reservas?" → "No he encontrado ninguna reserva..."
    3. "Para hoy" → Should receive contact card, NOT availability times

## Verification Criteria

1. **Primary Success Criterion:** When user says "Para hoy" in response to booking date question, the bot:
   - Sends intro message: "Las reservas para el mismo día las gestionamos por teléfono..."
   - Sends contact card for "Gestión Reservas Villa Carmen" with phone 34638857294
   - Sends rejection message: "Te he enviado la tarjeta de contacto. Llámanos..."
   - Does NOT show availability times

2. **Secondary Criteria:**
   - Same-day detection works for: "para hoy", "hoy", "el día de hoy", "esta tarde", "esta noche"
   - Same-day detection does NOT trigger for: "hoy te confirmo", "mi reserva es hoy", forwarded confirmations
   - Same-day modifications and cancellations are also blocked (existing behavior preserved)

3. **Regression Prevention:**
   - All existing `SameDayDetectorTests` pass
   - Existing modification/cancellation same-day checks continue to work
   - BookingAvailabilityService same-day check (line 246) remains as final enforcement

## Potential Risks and Mitigations

1. **Risk:** Over-blocking legitimate messages that mention "hoy" but aren't booking requests
   - **Mitigation:** Keep the intent check but reorder it. Only block if:
     - Contains same-day keyword AND
     - Is NOT a forwarded confirmation AND
     - Either has booking intent OR is a short contextual response

2. **Risk:** Breaking existing same-day detection for modifications/cancellations
   - **Mitigation:** Run full test suite after changes. The `CancellationHandler` and `ModificationHandler` have their own same-day checks that don't use `SameDayDetector`.

3. **Risk:** False positives for users checking their existing same-day reservation
   - **Mitigation:** The `HasBookingContext` function and forwarded confirmation check should prevent this. Test with "mi reserva de hoy" and similar phrases.

## Alternative Approaches

1. **Alternative A:** Add conversation history context to `SameDayDetector`
   - **Description:** Pass the last bot message to detect if user is responding to a booking question
   - **Trade-offs:** More accurate but requires changing the function signature and all call sites
   - **Recommendation:** Consider for future enhancement if simple fix isn't sufficient

2. **Alternative B:** Rely solely on `BookingAvailabilityService` check
   - **Description:** Remove pre-check and let the availability service handle same-day rejection
   - **Trade-offs:** Simpler code but user sees AI-generated response before rejection
   - **Recommendation:** Keep pre-check for better UX (immediate rejection without AI processing)

3. **Alternative C:** Add "para" to booking intent keywords
   - **Description:** Add "para" to the `HasBookingOrModificationIntent` keywords
   - **Trade-offs:** Too broad, would trigger on many non-booking messages
   - **Recommendation:** Do not implement

## Recommended Implementation Order

1. **Task 1.1** (Critical) - Fix the logic order in SameDayDetector
2. **Task 5.1** (Critical) - Add unit tests to prevent regression
3. **Task 3.1** (High) - Verify contact card flow
4. **Task 5.2** (High) - Integration test for the bug scenario
5. **Task 1.2** (Medium) - Enhance keyword coverage
6. **Task 4.1** (Low) - Optional AI prompt enhancement

## Code Changes Summary

### File: `src/BotGenerator.Core/Services/TurnAnalysis/SameDayDetector.cs`

**Current Logic (buggy):**
```csharp
// Line 20-23: Returns false for "para hoy" because no booking intent
if (!HasBookingOrModificationIntent(t))
    return false;

// Line 36-51: Never reached for "para hoy"
var sameDayKeywords = new[] { "para hoy", ... };
if (sameDayKeywords.Any(keyword => t.Contains(keyword)))
    return true;
```

**Proposed Fix:**
```csharp
// Check same-day keywords FIRST (before intent check)
var sameDayKeywords = new[]
{
    "para hoy",
    "reservar hoy",
    "reserva hoy",
    "mesa hoy",
    "hoy para",
    "el día de hoy",
    "dia de hoy",
    "esta tarde",
    "esta noche",
    "ahora mismo"
};

if (sameDayKeywords.Any(keyword => t.Contains(keyword)))
{
    // Verify it's not a forwarded confirmation
    if (IsForwardedConfirmation(t))
        return false;
    
    // Verify it's not a deferral pattern
    if (Regex.IsMatch(t, @"\b(hoy|hoy\s+mismo)\b.*\b(confirm|confirmo|confirmar|aviso|digo|dir[eé]|consult|pregunt|comento|te\s+cuento)\b",
            RegexOptions.IgnoreCase))
    {
        return false;
    }
    
    return true;
}

// Then check for standalone "hoy" with booking context
if (Regex.IsMatch(t, @"\bhoy\b"))
{
    if (HasBookingContext(t))
        return true;
    
    var trimmed = t.Trim();
    if (trimmed == "hoy" || Regex.IsMatch(trimmed, @"^hoy\s*(a\s*las)?\s*\d"))
        return true;
}

// Check for today's date patterns
var today = nowLocal.Date;
var todayPatterns = new[]
{
    $"{today.Day}/{today.Month}",
    $"{today.Day:D2}/{today.Month:D2}",
    $"{today.Day}/{today.Month}/{today.Year}",
    $"{today.Day:D2}/{today.Month:D2}/{today.Year}"
};

if (todayPatterns.Any(pattern => t.Contains(pattern)))
{
    // Only trigger if there's booking intent for explicit date mentions
    return HasBookingOrModificationIntent(t);
}

return false;
```

This reordering ensures that explicit same-day phrases like "para hoy" are detected immediately, while still maintaining safeguards against false positives.
