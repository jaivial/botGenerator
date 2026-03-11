# Improve Natural Language Understanding in Modification Flow

## Objective

Transform the modification flow from a rigid, menu-driven state machine into a fluid, natural language conversation system that can:
- Understand combined date+time expressions ("domingo 15 a las 14:30")
- Accumulate partial information across multiple turns
- Reduce repetitive questioning and improve conversation fluency
- Handle natural human expressions instead of requiring menu selections

## Problem Analysis

### Current Issues Identified

**From Conversation Transcript:**
```
User: "No es para el 13, es para el domingo 15"
Bot: Shows menu with 6 numbered options
User: "Domingo 15"
Bot: "La reserva actual es para el 13/03/2026. ¿Para cuándo lo movemos?"
User: "A las 14.30"
Bot: "No entendí bien el día. ¿Me lo repites?"
User: "Domingo, 15/03"
Bot: Shows availability at 14:00 (wrong time)
User: "14.30h"
Bot: "Perdona, no pillé la fecha..."
[Loop continues for 10+ messages]
```

### Root Causes

1. **Sequential State Machine** (`src/BotGenerator.Core/Handlers/ModificationHandler.cs:89-97`)
   - Rigid stages: SelectingBooking → SelectingField → CollectingNewValue → AwaitingConfirmation
   - Each stage expects specific input format
   - No support for multi-field updates in single message

2. **Single-Field Focus** (`ModificationHandler.cs:305-318`)
   - `HandleNewValueAsync` routes to field-specific handlers
   - Date handler only parses date, ignores time
   - Time handler only parses time, ignores date
   - No mechanism to handle both simultaneously

3. **Limited Context Accumulation** (`ModificationState.cs`)
   - State only stores: `FieldToModify`, `PendingChanges`, `SelectedBooking`
   - No accumulation of partial data across turns
   - No "working memory" for multi-turn information gathering

4. **Menu-Driven Prompts** (`src/BotGenerator.Prompts/restaurants/villacarmen/modification-flow.txt`)
   - Trained to show numbered menus
   - Not optimized for natural language understanding
   - Expects structured responses

## Implementation Plan

### Phase 1: Enhanced State Model with Accumulator Pattern

**Objective**: Add working memory to accumulate partial information across turns

- [ ] **Task 1.1**: Extend `ModificationState.cs` with accumulator fields
  - Add `AccumulatedChanges` dictionary to store partial field values
  - Add `LastAskedField` to track what information was requested
  - Add `ExtractedFields` list to track which fields were successfully parsed
  - Rationale: Enables tracking of partial information provided across multiple turns

- [ ] **Task 1.2**: Create `FieldAccumulatorService.cs` in `Services/` directory
  - Implement `AccumulateFieldAsync(state, fieldName, value)` method
  - Implement `GetAccumulatedValue(state, fieldName)` method
  - Implement `HasAllRequiredFields(state, requiredFields)` method
  - Implement `ClearAccumulatedField(state, fieldName)` method
  - Rationale: Centralized logic for managing partial information state

- [ ] **Task 1.3**: Update `ModificationHandler.cs` constructor to inject `FieldAccumulatorService`
  - Add dependency injection for the new service
  - Initialize service in handler
  - Rationale: Makes accumulator available throughout modification flow

### Phase 2: Multi-Field Natural Language Parser

**Objective**: Extract multiple fields from single user message

- [ ] **Task 2.1**: Create `NaturalLanguageModificationParser.cs` in `Services/` directory
  - Implement `ParseModificationRequest(message, currentState)` method
  - Return `Dictionary<string, object>` with all extracted fields
  - Support combined expressions: "domingo 15 a las 14:30" → {date: "15/03/2026", time: "14:30"}
  - Support relative expressions: "más tarde" → {time: "later"}
  - Support partial expressions: "para 10 personas" → {party_size: 10}
  - Rationale: Enables extraction of multiple fields from natural language

- [ ] **Task 2.2**: Implement date+time combined parser
  - Detect patterns: "domingo 15 a las 14:30", "el 15 a las 14:30", "domingo a las 2 y media"
  - Use existing `ParseDate()` and `ParseTime()` methods from `ModificationHandler.cs:1074-1191`
  - Combine results into single extraction
  - Rationale: Handles the most common user expression pattern

- [ ] **Task 2.3**: Implement context-aware field inference
  - If user says "14:30" when asked for date → infer they're providing time, keep previous date context
  - If user says "domingo 15" when asked for time → infer they're correcting date, ask for time again
  - Use `LastAskedField` to understand user intent
  - Rationale: Reduces "no entendí" responses when user provides related but unexpected information

- [ ] **Task 2.4**: Add Spanish natural language patterns
  - Support: "más tarde", "más temprano", "el mismo día", "otro día"
  - Support: "más personas", "menos personas", "somos X"
  - Support: "quitar el arroz", "cambiar el arroz", "sin arroz"
  - Rationale: Improves understanding of natural Spanish expressions

### Phase 3: Flexible Field Collection Flow

**Objective**: Replace rigid state machine with flexible accumulation flow

- [ ] **Task 3.1**: Refactor `HandleFieldSelectionAsync()` in `ModificationHandler.cs:222-292`
  - Remove menu display logic
  - Use `NaturalLanguageModificationParser` to extract fields from user message
  - Accumulate extracted fields using `FieldAccumulatorService`
  - Determine which fields still need collection
  - Ask naturally for missing fields (not numbered menu)
  - Rationale: Eliminates menu-driven interaction in favor of natural conversation

- [ ] **Task 3.2**: Refactor `HandleNewValueAsync()` in `ModificationHandler.cs:297-319`
  - Extract ALL possible fields from message (not just `FieldToModify`)
  - Accumulate all extracted fields
  - Check if enough information to proceed with modification
  - If multiple fields extracted (date+time), validate both before confirmation
  - Rationale: Enables multi-field updates in single interaction

- [ ] **Task 3.3**: Create intelligent confirmation builder
  - Build confirmation message from accumulated fields
  - Show ALL changes being made (not just single field)
  - Format: "Vas a cambiar tu reserva del [fecha] a las [hora] para [personas] personas al [nueva fecha] a las [nueva hora] para [nuevas personas] personas"
  - Rationale: Provides clear summary of all modifications

- [ ] **Task 3.4**: Implement smart follow-up questions
  - If date extracted but no time → ask "¿A qué hora?"
  - If time extracted but no date → ask "¿Para qué día?"
  - If both extracted → skip to confirmation
  - Use conversation context to phrase questions naturally
  - Rationale: Reduces unnecessary back-and-forth

### Phase 4: Contextual Memory and Intent Tracking

**Objective**: Maintain conversation context to understand user intent

- [ ] **Task 4.1**: Add conversation context to `ModificationState`
  - Track `UserGoal`: what user is trying to achieve (change_date, change_time, change_both, add_rice, etc.)
  - Track `ConversationTurn`: number of interactions in current modification
  - Track `PreviousBotQuestion`: what bot asked last (for context)
  - Rationale: Enables bot to understand responses in context

- [ ] **Task 4.2**: Implement intent detection in `NaturalLanguageModificationParser`
  - Detect user goal from first message: "quiero cambiar la fecha" → goal: change_date
  - Detect corrections: "no, es para el domingo" → goal: correct_date
  - Detect cancellations: "déjalo" → goal: cancel_modification
  - Rationale: Understanding intent improves response relevance

- [ ] **Task 4.3**: Add fallback logic for unclear messages
  - If message unclear but has extractable fields → extract what's possible, ask for rest
  - If message unclear and no fields → ask natural clarification question
  - Track repeated failures → offer human handoff
  - Rationale: Prevents infinite loops of "no entendí"

### Phase 5: Prompt Engineering for Natural Interaction

**Objective**: Update AI prompts to support natural language modification

- [ ] **Task 5.1**: Update `modification-flow.txt` prompt
  - Remove instruction to show numbered menus
  - Add instruction to parse natural language expressions
  - Add examples of combined date+time expressions
  - Add instruction to accumulate partial information
  - Rationale: Aligns AI behavior with new flexible flow

- [ ] **Task 5.2**: Add natural language examples to prompt
  ```
  User: "No es para el 13, es para el domingo 15 a las 14:30"
  Bot: "Perfecto, cambio tu reserva al domingo 15 de marzo a las 14:30. ¿Confirmas?"
  
  User: "Quiero cambiar a más tarde"
  Bot: "Vale, ¿a qué hora te viene mejor? Ahora tienes las 14:00"
  
  User: "Para 10 personas y a las 14:30"
  Bot: "Entonces cambio a 10 personas y a las 14:30. ¿Confirmas?"
  ```
  - Rationale: Provides AI with examples of natural conversation patterns

- [ ] **Task 5.3**: Update `system-main.txt` with modification context awareness
  - Add instruction to detect multi-field expressions
  - Add instruction to maintain context across modification turns
  - Add instruction to avoid repetitive questions
  - Rationale: Improves AI's understanding of modification context

### Phase 6: Availability Integration with Accumulated Fields

**Objective**: Validate availability for multi-field changes atomically

- [ ] **Task 6.1**: Create `MultiFieldAvailabilityValidator.cs` in `Services/` directory
  - Implement `ValidateAccumulatedChangesAsync(booking, accumulatedChanges)` method
  - Check availability for date+time combinations together
  - Return consolidated availability result with suggestions
  - Rationale: Prevents showing availability for wrong time/date combinations

- [ ] **Task 6.2**: Update `HandleDateChangeAsync()` and `HandleTimeChangeAsync()`
  - Check for accumulated time/date in state before validating
  - Use accumulated time when checking date availability
  - Use accumulated date when checking time availability
  - Rationale: Ensures availability checks use complete information

- [ ] **Task 6.3**: Implement smart suggestion builder
  - If date+time not available → suggest closest available combinations
  - Format: "El domingo 15 a las 14:30 no está disponible, pero tengo a las 13:30 o a las 15:00"
  - Allow user to choose or provide alternative
  - Rationale: Provides helpful alternatives instead of just rejection

### Phase 7: Testing and Validation

**Objective**: Ensure natural language understanding works correctly

- [ ] **Task 7.1**: Create unit tests for `NaturalLanguageModificationParser`
  - Test combined date+time extraction: "domingo 15 a las 14:30"
  - Test partial field extraction: "para 10 personas"
  - Test relative expressions: "más tarde"
  - Test context-aware inference: "14:30" when asked for date
  - Rationale: Validates parser handles various natural language patterns

- [ ] **Task 7.2**: Create integration tests for modification flow
  - Test multi-field modification in single message
  - Test accumulation across multiple turns
  - Test correction handling: "no, es para el domingo"
  - Test availability validation with accumulated fields
  - Rationale: Validates end-to-end flow works naturally

- [ ] **Task 7.3**: Create conversation test scenarios in `testing/` directory
  - Scenario: User provides date+time together
  - Scenario: User provides partial information across turns
  - Scenario: User corrects previous input
  - Scenario: User provides natural expressions ("más tarde", "somos 10")
  - Rationale: Validates real-world conversation patterns work

- [ ] **Task 7.4**: Add logging for natural language understanding metrics
  - Log extraction success rate
  - Log average turns to complete modification
  - Log clarification request frequency
  - Rationale: Enables monitoring of improvement effectiveness

## Verification Criteria

### Functional Requirements

- [ ] User can provide date and time in single message: "domingo 15 a las 14:30" → both fields extracted
- [ ] System accumulates partial information: "domingo 15" → "14:30" → both fields recognized
- [ ] No repetitive "no entendí" loops for valid natural expressions
- [ ] Confirmation shows ALL fields being modified, not just one
- [ ] Availability checks use complete date+time information
- [ ] System handles corrections gracefully: "no, es para el domingo"

### Performance Requirements

- [ ] Average modification completion time reduced by 40% (fewer turns)
- [ ] Clarification request rate reduced by 50%
- [ ] User satisfaction score improved (measured via conversation analysis)

### User Experience Requirements

- [ ] No numbered menus shown during modification (unless user explicitly asks for options)
- [ ] Natural language responses feel conversational, not robotic
- [ ] System understands Spanish expressions naturally ("más tarde", "somos X personas")
- [ ] Maximum 3 turns to complete simple modification (date OR time OR party size)
- [ ] Maximum 5 turns to complete complex modification (date AND time AND party size)

## Potential Risks and Mitigations

### Risk 1: Over-Extraction of Fields
**Description**: Parser extracts fields that user didn't intend to modify
**Mitigation**: 
- Implement confidence scoring for extracted fields
- Only extract fields explicitly mentioned or strongly implied
- Use conversation context to validate extraction relevance
- When in doubt, ask confirmation: "¿Quieres cambiar también la hora?"

### Risk 2: Ambiguous Natural Language
**Description**: User expressions could be interpreted multiple ways
**Mitigation**: 
- Maintain conversation context to disambiguate
- Use `LastAskedField` to understand response context
- When truly ambiguous, ask natural clarification: "¿Te refieres a la fecha o a la hora?"
- Track clarification patterns to improve parser over time

### Risk 3: Breaking Existing Functionality
**Description**: Changes to state machine could break existing modification flows
**Mitigation**: 
- Implement changes incrementally with feature flags
- Maintain backward compatibility with menu-driven flow
- Extensive testing of edge cases
- Gradual rollout with monitoring

### Risk 4: AI Prompt Confusion
**Description**: AI may generate conflicting instructions with new prompts
**Mitigation**: 
- Clear separation of concerns: AI detects intent, code handles extraction
- Explicit instructions in prompts about when to use MODIFICATION_INTENT
- Test prompts with various conversation scenarios
- Version control prompts for easy rollback

### Risk 5: Performance Impact
**Description**: Natural language parsing may add latency
**Mitigation**: 
- Optimize parser with compiled regex patterns
- Cache frequently used patterns
- Async processing where possible
- Performance testing with load simulation

## Alternative Approaches

### Alternative 1: AI-Only Extraction (No Code Parser)
**Description**: Let AI extract all fields from natural language without dedicated parser
**Trade-offs**:
- ✅ More flexible, handles edge cases better
- ✅ Less code to maintain
- ❌ Slower (requires AI call for every message)
- ❌ Less predictable, harder to debug
- ❌ Higher cost (more AI API calls)
**Recommendation**: Use hybrid approach - AI for intent detection, code parser for field extraction

### Alternative 2: Slot-Filling Dialogue System
**Description**: Implement formal slot-filling system with defined slots and validation rules
**Trade-offs**:
- ✅ More structured and predictable
- ✅ Easier to add new fields
- ❌ More complex to implement
- ❌ May feel less natural than direct extraction
- ❌ Overkill for current scope
**Recommendation**: Consider for future if adding many more modification fields

### Alternative 3: Voice-First NLU Library
**Description**: Integrate specialized NLU library (Rasa, Dialogflow) for natural language understanding
**Trade-offs**:
- ✅ Purpose-built for this problem
- ✅ Better NLU capabilities out of box
- ❌ Significant integration effort
- ❌ External dependency and cost
- ❌ Overkill for WhatsApp text-based bot
**Recommendation**: Not necessary - custom parser sufficient for current scope

## Implementation Timeline Estimate

**Phase 1-2 (Foundation)**: 2-3 days
- State model extensions
- Parser service creation
- Basic multi-field extraction

**Phase 3-4 (Flow Refactoring)**: 3-4 days
- Handler refactoring
- Context tracking
- Smart follow-up questions

**Phase 5-6 (Integration)**: 2-3 days
- Prompt updates
- Availability integration
- End-to-end testing

**Phase 7 (Testing & Validation)**: 2 days
- Unit tests
- Integration tests
- Conversation scenarios

**Total Estimated Effort**: 9-12 days

## Success Metrics

### Quantitative Metrics
- **Turn Reduction**: Average turns per modification from 8+ to 3-5
- **Extraction Accuracy**: 90%+ correct field extraction from natural language
- **Clarification Rate**: <15% of messages require clarification
- **Completion Rate**: 95%+ modifications completed successfully

### Qualitative Metrics
- Conversations feel natural and fluid
- Users don't need to adapt language to bot's expectations
- No frustration evident in conversation patterns
- Bot understands context and intent, not just keywords

## Next Steps After Implementation

1. **Monitor and Iterate**: Track metrics, gather user feedback, refine parser patterns
2. **Expand Patterns**: Add more Spanish natural language expressions based on real usage
3. **Apply to Other Flows**: Extend natural language approach to booking and cancellation flows
4. **Machine Learning**: Consider ML-based NLU if rule-based parser reaches limits
5. **Multilingual Support**: Extend to other languages if restaurant expands internationally