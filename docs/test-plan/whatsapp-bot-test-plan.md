# WhatsApp Bot Test Plan - Alquería Villa Carmen

Generated: 2025-11-26
Total Tests: 300

## Project Overview

This test plan covers the WhatsApp bot for **Alquería Villa Carmen**, a restaurant booking system that:
- Receives webhook messages from WhatsApp via UAZAPI
- Uses Google Gemini AI for natural conversation
- Handles restaurant reservations, cancellations, and modifications
- Manages multi-turn conversations with state tracking
- Validates rice types against the menu
- Checks availability based on schedule and capacity

## Architecture Summary

| Component | Description |
|-----------|-------------|
| `WebhookController` | Entry point for WhatsApp webhooks |
| `MainConversationAgent` | Primary AI conversation handler |
| `IntentRouterService` | Routes by intent (booking, cancellation, etc.) |
| `ConversationHistoryService` | Manages chat history and state extraction |
| `GeminiService` | Google AI API integration |
| `WhatsAppService` | UAZAPI integration for sending messages |
| `RiceValidatorAgent` | Validates rice requests against menu |
| `AvailabilityCheckerAgent` | Checks booking availability |
| `BookingHandler` | Creates confirmed bookings |
| `CancellationHandler` | Processes cancellations |
| `ModificationHandler` | Handles booking modifications |

## Test Distribution

| Category | Count | Percentage | File |
|----------|-------|------------|------|
| Webhook & Infrastructure | 25 | 8% | `webhook-tests.md` |
| Message Parsing & Extraction | 15 | 5% | `message-handling-tests.md` |
| Response Building | 10 | 3% | `message-handling-tests.md` |
| State Management | 15 | 5% | `state-ai-tests.md` |
| AI Integration | 10 | 4% | `state-ai-tests.md` |
| **Code Subtotal** | **75** | **25%** | |
| Single Message Logic | 35 | 12% | `single-message-tests.md` |
| Multi-Turn Flows | 70 | 23% | `conversation-flow-tests.md` |
| Context Retention | 30 | 10% | `context-tests.md` |
| Edge Cases & Error Handling | 40 | 13% | `edge-case-tests.md` |
| Response Quality | 30 | 10% | `quality-tests.md` |
| Full Journeys (up to 40 msgs) | 20 | 7% | `journey-tests.md` |
| **Conversation Subtotal** | **225** | **75%** | |
| **TOTAL** | **300** | **100%** | |

---

## Code Correctness Tests (Steps 1-75)

### Webhook & Infrastructure (Steps 1-25)
See: [webhook-tests.md](./webhook-tests.md)

| Step | Test Name | Category | Description |
|------|-----------|----------|-------------|
| 1 | `WebhookController_Health_ReturnsOk` | Health | Health endpoint returns 200 OK with status |
| 2 | `WebhookController_Health_IncludesTimestamp` | Health | Health response includes UTC timestamp |
| 3 | `WebhookController_Health_IncludesVersion` | Health | Health response includes version 1.0.0 |
| 4 | `WebhookController_ValidPayload_Returns200` | Webhook | Valid webhook payload returns 200 |
| 5 | `WebhookController_InvalidJson_Returns400` | Webhook | Malformed JSON returns 400 BadRequest |
| 6 | `WebhookController_MissingMessage_Returns400` | Webhook | Payload missing "message" property returns 400 |
| 7 | `WebhookController_FromMe_ReturnsOkNoProcess` | Webhook | Own messages (fromMe=true) are ignored |
| 8 | `WebhookController_EmptyText_ReturnsOkNoProcess` | Webhook | Empty text messages are ignored |
| 9 | `WebhookController_MediaMessage_ReturnsOkNoProcess` | Webhook | Media-only messages are ignored |
| 10 | `WebhookController_ExtractsPhoneNumber_FromChatId` | Extraction | Phone extracted from chatid correctly |
| 11 | `WebhookController_ExtractsText_FromTextProperty` | Extraction | Regular text message extracted |
| 12 | `WebhookController_ExtractsText_FromVoteProperty` | Extraction | Button response (vote) extracted |
| 13 | `WebhookController_ExtractsText_FromListResponse` | Extraction | List selection response extracted |
| 14 | `WebhookController_ExtractsPushName_FromChat` | Extraction | Customer name extracted from chat.name |
| 15 | `WebhookController_ExtractsTimestamp_FromPayload` | Extraction | Message timestamp correctly parsed |
| 16 | `WebhookController_IdentifiesButtonResponse` | Extraction | ButtonsResponseMessage type identified |
| 17 | `WebhookController_IdentifiesListResponse` | Extraction | ListResponseMessage type identified |
| 18 | `WebhookController_ExtractsButtonId` | Extraction | Button ID extracted from buttonOrListid |
| 19 | `WebhookController_ProcessesMessage_CallsMainAgent` | Flow | Message processing calls MainConversationAgent |
| 20 | `WebhookController_ProcessesMessage_CallsIntentRouter` | Flow | Response routed through IntentRouterService |
| 21 | `WebhookController_ProcessesMessage_SendsResponse` | Flow | Response sent via WhatsAppService |
| 22 | `WebhookController_SendFails_LogsWarning` | Error | Failed send logs warning, returns OK |
| 23 | `WebhookController_Exception_Returns500` | Error | Unhandled exception returns 500 |
| 24 | `WebhookController_Exception_SendsErrorToUser` | Error | Error message sent to user on exception |
| 25 | `WebhookController_CancellationToken_Respected` | Async | Cancellation token cancels processing |

### Message Parsing & Response Building (Steps 26-50)
See: [message-handling-tests.md](./message-handling-tests.md)

| Step | Test Name | Category | Description |
|------|-----------|----------|-------------|
| 26 | `MainAgent_ParsesBookingRequest_ExtractsData` | Parsing | BOOKING_REQUEST command parsed correctly |
| 27 | `MainAgent_ParsesBookingRequest_ExtractsName` | Parsing | Customer name extracted from command |
| 28 | `MainAgent_ParsesBookingRequest_ExtractsPhone` | Parsing | Phone number extracted from command |
| 29 | `MainAgent_ParsesBookingRequest_ExtractsDate` | Parsing | Date in dd/MM/yyyy format extracted |
| 30 | `MainAgent_ParsesBookingRequest_ExtractsPeople` | Parsing | Number of people parsed as integer |
| 31 | `MainAgent_ParsesBookingRequest_ExtractsTime` | Parsing | Time in HH:mm format extracted |
| 32 | `MainAgent_ParsesCancellationRequest_ExtractsData` | Parsing | CANCELLATION_REQUEST command parsed |
| 33 | `MainAgent_ParsesModificationIntent` | Parsing | MODIFICATION_INTENT detected |
| 34 | `MainAgent_ParsesSameDayBooking` | Parsing | SAME_DAY_BOOKING intent detected |
| 35 | `MainAgent_ExtractsRiceType_FromResponse` | Parsing | Rice type extracted from AI response |
| 36 | `MainAgent_ExtractsRiceServings_FromResponse` | Parsing | Rice servings count extracted |
| 37 | `MainAgent_ExtractsHighChairs_FromResponse` | Parsing | High chair count extracted |
| 38 | `MainAgent_ExtractsBabyStrollers_FromResponse` | Parsing | Baby stroller count extracted |
| 39 | `MainAgent_DetectsUrls_SetsInteractiveIntent` | Parsing | URLs in response set hasUrls metadata |
| 40 | `MainAgent_CleansMarkdown_EscapedChars` | Cleaning | Escaped \_ and \| cleaned |
| 41 | `MainAgent_CleansMarkdown_DoubleBoldToSingle` | Cleaning | **text** converted to *text* |
| 42 | `MainAgent_CleansMarkdown_MultipleNewlines` | Cleaning | 3+ newlines reduced to 2 |
| 43 | `MainAgent_CleansMarkdown_WhitespaceLines` | Cleaning | Whitespace-only lines removed |
| 44 | `MainAgent_EmptyCleanedResponse_DefaultMessage` | Cleaning | Empty response gets fallback message |
| 45 | `BookingHandler_BuildsConfirmation_IncludesEmojis` | Response | Confirmation uses correct emojis |
| 46 | `BookingHandler_BuildsConfirmation_IncludesAllData` | Response | All booking fields in confirmation |
| 47 | `BookingHandler_BuildsConfirmation_OptionalRice` | Response | Rice shown only when present |
| 48 | `BookingHandler_BuildsConfirmation_OptionalHighChairs` | Response | High chairs shown only when >0 |
| 49 | `ModificationHandler_BuildsOptions_SingleBooking` | Response | Single booking shows modification options |
| 50 | `ModificationHandler_BuildsOptions_MultipleBookings` | Response | Multiple bookings listed with numbers |

### State Management & AI Integration (Steps 51-75)
See: [state-ai-tests.md](./state-ai-tests.md)

| Step | Test Name | Category | Description |
|------|-----------|----------|-------------|
| 51 | `HistoryService_GetHistory_ReturnsEmpty_NewUser` | History | New user returns empty history |
| 52 | `HistoryService_GetHistory_ReturnsCached` | History | Cached history returned correctly |
| 53 | `HistoryService_AddMessage_StoresMessage` | History | Messages stored in history |
| 54 | `HistoryService_AddMessage_TrimsToMaxMessages` | History | History trimmed to 30 messages |
| 55 | `HistoryService_SessionTimeout_ClearsHistory` | History | Expired session clears history |
| 56 | `HistoryService_ClearHistory_RemovesAll` | History | Clear removes all messages |
| 57 | `HistoryService_ExtractState_ExtractsDate` | State | Date extracted from history |
| 58 | `HistoryService_ExtractState_ExtractsTime` | State | Time extracted from history |
| 59 | `HistoryService_ExtractState_ExtractsPeople` | State | People count extracted |
| 60 | `HistoryService_ExtractState_ExtractsRiceType` | State | Rice type from validation result |
| 61 | `HistoryService_ExtractState_DetectsNoRice` | State | "no rice" response sets empty string |
| 62 | `HistoryService_ExtractState_IdentifiesMissingData` | State | Missing fields correctly listed |
| 63 | `HistoryService_ExtractState_SetsStage` | State | Stage set based on completeness |
| 64 | `ContextBuilder_BuildsContext_IncludesToday` | Context | Today's date in context |
| 65 | `ContextBuilder_BuildsContext_IncludesWeekends` | Context | Upcoming weekends calculated |
| 66 | `ContextBuilder_BuildsContext_IncludesState` | Context | Conversation state in context |
| 67 | `PromptLoader_LoadsRestaurantPrompt` | Prompts | Restaurant-specific prompt loaded |
| 68 | `PromptLoader_LoadsSharedPrompt` | Prompts | Shared prompts loaded |
| 69 | `PromptLoader_AssemblesSystemPrompt` | Prompts | All modules assembled correctly |
| 70 | `PromptLoader_ReplacesTokens_BracketSyntax` | Prompts | {{token}} replaced with values |
| 71 | `PromptLoader_ProcessesConditionals_IfTrue` | Prompts | {{#if var}}...{{/if}} when true |
| 72 | `PromptLoader_ProcessesConditionals_IfFalse` | Prompts | {{#if var}}...{{else}}...{{/if}} when false |
| 73 | `GeminiService_SendsRequest_CorrectFormat` | AI | Request body formatted correctly |
| 74 | `GeminiService_ParsesResponse_ExtractsText` | AI | Response text extracted from JSON |
| 75 | `GeminiService_HandlesErrors_ThrowsException` | AI | API errors throw GeminiApiException |

---

## Conversation Logic Tests (Steps 76-300)

### Single Message Logic (Steps 76-110)
See: [single-message-tests.md](./single-message-tests.md)

| Step | Test Name | Type | Messages |
|------|-----------|------|----------|
| 76 | `Greeting_Hola_RespondsWelcome` | Greeting | 1 |
| 77 | `Greeting_BuenosDias_RespondsAppropriately` | Greeting | 1 |
| 78 | `Greeting_BuenasTardes_RespondsAppropriately` | Greeting | 1 |
| 79 | `Greeting_Ey_RespondsNaturally` | Greeting | 1 |
| 80 | `Intent_QuieroReservar_StartsBookingFlow` | Intent | 1 |
| 81 | `Intent_HacerReserva_StartsBookingFlow` | Intent | 1 |
| 82 | `Intent_TeneisMesa_StartsBookingFlow` | Intent | 1 |
| 83 | `Intent_CancelarReserva_StartsCancellationFlow` | Intent | 1 |
| 84 | `Intent_ModificarReserva_StartsModificationFlow` | Intent | 1 |
| 85 | `Intent_CambiarReserva_StartsModificationFlow` | Intent | 1 |
| 86 | `Question_Horarios_RespondsWithSchedule` | Info | 1 |
| 87 | `Question_Direccion_RespondsWithLocation` | Info | 1 |
| 88 | `Question_Telefono_RespondsWithPhone` | Info | 1 |
| 89 | `Question_MenuArroces_ListsRiceTypes` | Info | 1 |
| 90 | `Question_Precios_RespondsWithPricing` | Info | 1 |
| 91 | `Question_Parking_RespondsWithInfo` | Info | 1 |
| 92 | `Question_GruposGrandes_RespondsWithPolicy` | Info | 1 |
| 93 | `Question_Tronas_RespondsWithAvailability` | Info | 1 |
| 94 | `Date_ElSabado_ParsesNextSaturday` | Date | 1 |
| 95 | `Date_ElDomingo_ParsesNextSunday` | Date | 1 |
| 96 | `Date_ProximoFinDeSemana_ParsesCorrectly` | Date | 1 |
| 97 | `Date_30DeNoviembre_ParsesExplicitDate` | Date | 1 |
| 98 | `Date_30/11_ParsesShortFormat` | Date | 1 |
| 99 | `Date_Manana_ParsesTomorrow` | Date | 1 |
| 100 | `Time_ALas14_ParsesTime` | Time | 1 |
| 101 | `Time_ALasDos_ParsesSpanishNumber` | Time | 1 |
| 102 | `Time_1430_ParsesWithoutColon` | Time | 1 |
| 103 | `People_4Personas_ParsesCount` | People | 1 |
| 104 | `People_SomosCuatro_ParsesSpanish` | People | 1 |
| 105 | `People_ParaSeis_ParsesCount` | People | 1 |
| 106 | `Rice_ArrozDelSenoret_Validates` | Rice | 1 |
| 107 | `Rice_PaellaValenciana_Validates` | Rice | 1 |
| 108 | `Rice_ArrozNegro_Validates` | Rice | 1 |
| 109 | `Rice_NoQuierenArroz_AcceptsNo` | Rice | 1 |
| 110 | `Rice_UnknownType_AsksForClarification` | Rice | 1 |

### Multi-Turn Conversation Flows (Steps 111-180)
See: [conversation-flow-tests.md](./conversation-flow-tests.md)

| Step | Test Name | Type | Messages |
|------|-----------|------|----------|
| 111-115 | `BookingFlow_SimpleComplete_5Messages` | Booking | 5 |
| 116-120 | `BookingFlow_WithRice_6Messages` | Booking | 6 |
| 121-126 | `BookingFlow_RiceValidation_7Messages` | Booking | 7 |
| 127-132 | `BookingFlow_InvalidRice_Clarification_8Messages` | Booking | 8 |
| 133-138 | `BookingFlow_MultipleRiceOptions_8Messages` | Booking | 8 |
| 139-143 | `BookingFlow_WithHighChairs_7Messages` | Booking | 7 |
| 144-148 | `BookingFlow_FullOptions_8Messages` | Booking | 8 |
| 149-153 | `BookingFlow_ChangesMindOnDate_8Messages` | Booking | 8 |
| 154-158 | `BookingFlow_ChangesMindOnTime_7Messages` | Booking | 7 |
| 159-163 | `BookingFlow_InterruptsWithQuestion_8Messages` | Booking | 8 |
| 164-168 | `CancellationFlow_WithBookingFound_5Messages` | Cancel | 5 |
| 169-173 | `CancellationFlow_NoBookingFound_4Messages` | Cancel | 4 |
| 174-178 | `ModificationFlow_SingleBooking_6Messages` | Modify | 6 |
| 179-180 | `ModificationFlow_MultipleBookings_8Messages` | Modify | 8 |

### Context Retention Tests (Steps 181-210)
See: [context-tests.md](./context-tests.md)

| Step | Test Name | Type | Messages |
|------|-----------|------|----------|
| 181-185 | `Context_RemembersPreviousDate_5Messages` | Memory | 5 |
| 186-190 | `Context_RemembersPreviousTime_5Messages` | Memory | 5 |
| 191-195 | `Context_RemembersPreviousPeople_5Messages` | Memory | 5 |
| 196-200 | `Context_NeverAsksForKnownData_6Messages` | NoRepeat | 6 |
| 201-205 | `Context_HandlesCorrections_5Messages` | Correct | 5 |
| 206-210 | `Context_MaintainsAcrossTopicChange_6Messages` | Persist | 6 |

### Edge Cases & Error Handling (Steps 211-250)
See: [edge-case-tests.md](./edge-case-tests.md)

| Step | Test Name | Type | Messages |
|------|-----------|------|----------|
| 211-214 | `Edge_SameDayBooking_Rejected_4Messages` | SameDay | 4 |
| 215-218 | `Edge_ClosedDay_Monday_4Messages` | Closed | 4 |
| 219-222 | `Edge_ClosedDay_Tuesday_4Messages` | Closed | 4 |
| 223-226 | `Edge_ClosedDay_Wednesday_4Messages` | Closed | 4 |
| 227-230 | `Edge_OutsideHours_TooEarly_4Messages` | Hours | 4 |
| 231-234 | `Edge_OutsideHours_TooLate_4Messages` | Hours | 4 |
| 235-238 | `Edge_LargeGroup_Over20_4Messages` | Capacity | 4 |
| 239-242 | `Edge_PastDate_Rejected_4Messages` | Past | 4 |
| 243-246 | `Edge_AmbiguousInput_Clarifies_4Messages` | Ambiguous | 4 |
| 247-250 | `Edge_MixedLanguage_Handles_4Messages` | Language | 4 |

### Response Quality Tests (Steps 251-280)
See: [quality-tests.md](./quality-tests.md)

| Step | Test Name | Type | Messages |
|------|-----------|------|----------|
| 251-255 | `Quality_ResponseLength_Under300Chars` | Length | 5 |
| 256-260 | `Quality_SingleQuestionPerMessage` | Focus | 5 |
| 261-265 | `Quality_NoBulletLists_ForQuestions` | Format | 5 |
| 266-270 | `Quality_UsesClientName_Occasionally` | Personal | 5 |
| 271-275 | `Quality_NaturalTone_NotRobotic` | Tone | 5 |
| 276-280 | `Quality_EmojisUsedSparingly` | Emoji | 5 |

### Full Customer Journeys (Steps 281-300)
See: [journey-tests.md](./journey-tests.md)

| Step | Test Name | Type | Messages |
|------|-----------|------|----------|
| 281-285 | `Journey_NewCustomer_FirstBooking_15Messages` | New | 15 |
| 286-290 | `Journey_ReturningCustomer_QuickBooking_10Messages` | Return | 10 |
| 291-294 | `Journey_ComplexBooking_WithAllOptions_20Messages` | Complex | 20 |
| 295-297 | `Journey_BookingThenCancel_15Messages` | MultiIntent | 15 |
| 298-300 | `Journey_FullExperience_40Messages` | Complete | 40 |

---

## Test Infrastructure

### Required Files

```
tests/
├── BotGenerator.Core.Tests/
│   └── Infrastructure/
│       ├── ConversationSimulator.cs
│       ├── ConversationFlowTestBase.cs
│       └── FlowScriptRunner.cs
└── BotGenerator.Integration.Tests/
    └── Conversations/
        ├── BookingFlowTests.cs
        ├── CancellationFlowTests.cs
        ├── ModificationFlowTests.cs
        ├── EdgeCaseTests.cs
        └── JourneyTests.cs

docs/test-plan/
├── whatsapp-bot-test-plan.md (this file)
├── webhook-tests.md
├── message-handling-tests.md
├── state-ai-tests.md
├── single-message-tests.md
├── conversation-flow-tests.md
├── context-tests.md
├── edge-case-tests.md
├── quality-tests.md
├── journey-tests.md
└── conversation-scripts/
    ├── greeting-flows.json
    ├── booking-simple.json
    ├── booking-with-rice.json
    ├── booking-complex.json
    ├── cancellation-flows.json
    ├── modification-flows.json
    ├── edge-cases.json
    └── full-journeys.json
```

### Test Execution Order

1. Run unit tests first (Steps 1-75)
2. Run single message tests (Steps 76-110)
3. Run multi-turn flow tests (Steps 111-180)
4. Run context tests (Steps 181-210)
5. Run edge case tests (Steps 211-250)
6. Run quality tests (Steps 251-280)
7. Run full journey tests (Steps 281-300)

### Implementation Command

```bash
# Implement all tests with auto-repair
/implement-all-whatsapp-tests
```

---

## Notes

- **Language**: All conversation tests are in Spanish (as the bot serves a Spanish restaurant)
- **AI Mocking**: For unit tests, mock the GeminiService to return predictable responses
- **Integration Tests**: May use actual Gemini API with test prompts
- **Time Sensitivity**: Tests involving dates should use fixed "current date" in setup
- **State Isolation**: Each test should start with fresh conversation state
