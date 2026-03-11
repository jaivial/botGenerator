# Switch to Pure AI-Based Natural Language Understanding

## Objective

Replace the current regex-based natural language parser with a pure AI-powered approach using Gemini to extract booking modification fields from user messages. This will enable:
- Understanding of ANY natural language expression (not just predefined patterns)
- Better handling of typos, slang, and edge cases
- Context-aware understanding from conversation history
- Future-proof solution that improves without code changes

## Current State Analysis

**Existing Implementation:**
- `NaturalLanguageModificationParser.cs` - Hardcoded regex patterns
- Limited to predefined Spanish expressions
- Fast but rigid (no flexibility)
- Zero cost but high maintenance

**Problem:**
- Won't understand novel expressions like "para el finde que viene"
- Can't handle typos like "domngo 15"
- Requires manual pattern addition for new expressions
- No semantic understanding

## Implementation Plan

### Phase 1: AI Parser Design & Prompt Engineering (2-3 days)

- [ ] **Task 1.1: Create AiNaturalLanguageModificationParser.cs**
  - Implement `INaturalLanguageModificationParser` interface
  - Add `IGeminiService` dependency injection
  - Add `IPromptLoaderService` for prompt templates
  - Add structured logging for AI calls
  - **Rationale**: Clean architecture with proper DI and testability

- [ ] **Task 1.2: Design extraction prompt template**
  - Create `src/BotGenerator.Prompts/components/nl-modification-extraction.txt`
  - Define JSON output schema with all booking fields
  - Add examples for common patterns (date, time, party size, rice, etc.)
  - Include context variables (current booking, conversation history)
  - Add confidence scoring requirements
  - **Rationale**: Well-designed prompts are critical for consistent AI output

- [ ] **Task 1.3: Design correction detection prompt**
  - Create `src/BotGenerator.Prompts/components/correction-detection.txt`
  - Train AI to detect corrections: "no, es para...", "mejor el..."
  - Return boolean + reasoning
  - **Rationale**: Separate concern for better accuracy

- [ ] **Task 1.4: Design user goal inference prompt**
  - Create `src/BotGenerator.Prompts/components/goal-inference.txt`
  - Detect intent: change_date, change_time, change_both, add_rice, etc.
  - Use conversation context for better inference
  - **Rationale**: Understanding user intent improves flow decisions

### Phase 2: Response Parsing & Validation (1-2 days)

- [ ] **Task 2.1: Create AI response parser**
  - Implement JSON deserialization with error handling
  - Add schema validation for AI responses
  - Handle malformed AI responses gracefully
  - Add retry logic for invalid responses (max 2 retries)
  - **Rationale**: AI responses can be unpredictable, need robust parsing

- [ ] **Task 2.2: Create field validators**
  - Date validator: Ensure extracted dates are valid and in booking window
  - Time validator: Check restaurant opening hours
  - Party size validator: 1-10 people, >10 needs special handling
  - Rice validator: Cross-check with RiceValidatorAgent
  - **Rationale**: AI can hallucinate, need validation layer

- [ ] **Task 2.3: Create confidence scorer**
  - Parse AI confidence score from response
  - Set threshold: <0.7 confidence → ask clarification
  - Log low-confidence extractions for prompt improvement
  - **Rationale**: Prevent bad user experience from uncertain AI

### Phase 3: Caching & Performance Optimization (1-2 days)

- [ ] **Task 3.1: Implement response caching**
  - Add `IMemoryCache` for repeated similar messages
  - Cache key: hash(userMessage + bookingContext)
  - TTL: 5 minutes (short-lived for conversation context)
  - Add cache hit/miss logging
  - **Rationale**: Reduce API costs and latency for common patterns

- [ ] **Task 3.2: Add request batching (optional)**
  - Batch multiple field extractions in single AI call
  - Combine: field extraction + correction detection + goal inference
  - Reduce from 3 API calls to 1 per message
  - **Rationale**: Further cost and latency reduction

- [ ] **Task 3.3: Add timeout handling**
  - Set 3-second timeout for AI calls
  - Fall back to regex parser on timeout
  - Log timeout incidents for monitoring
  - **Rationale**: Prevent conversation stalls from slow AI

### Phase 4: Fallback & Safety Mechanisms (1 day)

- [ ] **Task 4.1: Create hybrid fallback parser**
  - Keep `NaturalLanguageModificationParser` as fallback
  - Use when AI fails, times out, or returns low confidence
  - Log fallback events for analysis
  - **Rationale**: Ensure system never breaks, always has backup

- [ ] **Task 4.2: Add circuit breaker pattern**
  - Track AI failure rate in last 5 minutes
  - If >30% failures → automatically switch to regex parser
  - Auto-recover after 5 minutes of no failures
  - **Rationale**: Prevent cascading failures from AI service issues

- [ ] **Task 4.3: Add graceful degradation**
  - If AI returns partial data (only date, no time) → use it
  - Don't reject entire response for one missing field
  - Ask user for missing required fields
  - **Rationale**: Maximize value from AI responses

### Phase 5: Integration & Testing (2-3 days)

- [ ] **Task 5.1: Update dependency injection**
  - Replace `NaturalLanguageModificationParser` with `AiNaturalLanguageModificationParser` in `Program.cs`
  - Keep regex parser registered as fallback
  - Add configuration flag: `USE_AI_NLU=true/false`
  - **Rationale**: Easy rollback if AI approach has issues

- [ ] **Task 5.2: Create comprehensive unit tests**
  - Test AI parser with various message patterns
  - Mock `IGeminiService` for deterministic tests
  - Test JSON parsing edge cases
  - Test validation logic
  - Test fallback scenarios
  - **Rationale**: Ensure reliability before production

- [ ] **Task 5.3: Create integration tests**
  - Test with real Gemini API (staging environment)
  - Measure latency: target <500ms per extraction
  - Test with real conversation logs (anonymized)
  - Verify accuracy: target >90% correct extraction
  - **Rationale**: Validate real-world performance

- [ ] **Task 5.4: Create A/B testing framework**
  - Route 50% traffic to AI parser, 50% to regex
  - Track: accuracy, latency, user satisfaction, cost
  - Compare metrics after 1 week
  - **Rationale**: Data-driven decision on which approach is better

### Phase 6: Monitoring & Cost Management (1 day)

- [ ] **Task 6.1: Add comprehensive logging**
  - Log every AI call with: input, output, latency, cost
  - Track extraction accuracy (user confirmations vs corrections)
  - Monitor fallback rate
  - Alert on high failure rate (>20% in 5 minutes)
  - **Rationale**: Visibility into AI performance and costs

- [ ] **Task 6.2: Add cost tracking**
  - Calculate cost per extraction (Gemini pricing)
  - Track daily/monthly costs
  - Set budget alerts at €10/day, €200/month
  - Add cost dashboard in logs
  - **Rationale**: Prevent unexpected API cost spikes

- [ ] **Task 6.3: Add performance metrics**
  - Track P50, P95, P99 latency
  - Monitor timeout rate
  - Track cache hit rate
  - Compare with regex parser baseline
  - **Rationale**: Ensure AI doesn't degrade user experience

### Phase 7: Prompt Optimization (ongoing)

- [ ] **Task 7.1: Collect edge cases**
  - Log messages where AI fails or returns low confidence
  - Categorize failure patterns
  - Add to prompt examples
  - **Rationale**: Continuous improvement from real usage

- [ ] **Task 7.2: A/B test prompt variations**
  - Test different prompt structures
  - Compare accuracy and latency
  - Iterate on best-performing prompts
  - **Rationale**: Prompt engineering is iterative process

- [ ] **Task 7.3: Fine-tune for Spanish restaurant domain**
  - Add restaurant-specific vocabulary to prompts
  - Include common Spanish expressions for bookings
  - Add examples from real conversations (anonymized)
  - **Rationale**: Domain-specific prompts improve accuracy

## Verification Criteria

### Functional Requirements
- [ ] AI parser extracts correct fields from natural language messages
- [ ] Handles combined expressions: "domingo 15 a las 14:30 para 8 personas"
- [ ] Detects corrections: "no, es para el domingo" → is_correction=true
- [ ] Infers user goals: "más tarde" → change_time
- [ ] Validates extracted data against business rules
- [ ] Falls back to regex parser on AI failure
- [ ] All existing tests pass

### Performance Requirements
- [ ] P95 latency < 500ms per extraction
- [ ] P99 latency < 1000ms per extraction
- [ ] Timeout rate < 5%
- [ ] Cache hit rate > 30% (after warm-up)
- [ ] Accuracy > 90% (measured by user confirmations)

### Cost Requirements
- [ ] Cost per extraction < €0.001 (with caching)
- [ ] Daily cost < €10 (with normal traffic)
- [ ] Monthly cost < €200

### Reliability Requirements
- [ ] Fallback rate < 10%
- [ ] Circuit breaker activates on >30% failures
- [ ] Zero unhandled exceptions from AI parser
- [ ] Graceful degradation on partial AI responses

## Potential Risks and Mitigations

### 1. **High API Costs**
**Risk**: Gemini API costs exceed budget with high traffic
**Mitigation**:
- Implement aggressive caching (5-minute TTL)
- Batch multiple extractions in single API call
- Set hard cost limits with alerts
- Keep regex parser as free fallback
- Monitor daily costs and adjust cache TTL if needed

### 2. **Increased Latency**
**Risk**: AI calls add 200-500ms latency, degrading UX
**Mitigation**:
- Set 3-second timeout with fallback
- Use caching to avoid repeated calls
- Consider async processing for non-critical paths
- Monitor P95/P99 latency and optimize prompts
- A/B test to ensure user experience doesn't degrade

### 3. **AI Hallucinations**
**Risk**: AI extracts incorrect or nonsensical data
**Mitigation**:
- Add validation layer for all extracted fields
- Use confidence scoring with threshold (0.7)
- Log low-confidence extractions for review
- Fall back to regex parser on low confidence
- Add examples in prompt to guide AI

### 4. **Non-deterministic Behavior**
**Risk**: Same input produces different outputs, confusing users
**Mitigation**:
- Set temperature=0 in Gemini API calls
- Use structured JSON output mode
- Add response validation and retry logic
- Log inconsistencies for prompt improvement
- A/B test to measure consistency

### 5. **Service Dependencies**
**Risk**: Gemini API downtime breaks modification flow
**Mitigation**:
- Keep regex parser as always-available fallback
- Implement circuit breaker pattern
- Add health checks for Gemini API
- Monitor Gemini service status
- Auto-recover when service returns

### 6. **Prompt Engineering Complexity**
**Risk**: Prompts become too complex, hard to maintain
**Mitigation**:
- Keep prompts modular (separate for extraction, correction, goal)
- Version control prompts in separate files
- Document prompt design decisions
- Regular prompt review and simplification
- A/B test prompt changes

## Alternative Approaches

### 1. **Hybrid Approach (Recommended Backup)**
**Description**: Use regex for common patterns (80% of cases), AI for edge cases (20%)
**Trade-offs**:
- ✅ Lower cost (AI only for difficult cases)
- ✅ Lower latency (regex is instant for common patterns)
- ✅ Best of both worlds
- ❌ More complex codebase (two parsers to maintain)
- ❌ Need to define "difficult case" threshold

**When to use**: If pure AI approach has cost/latency issues

### 2. **Local NLU Model (Long-term)**
**Description**: Train/fine-tune local model (BERT, spaCy) for Spanish restaurant domain
**Trade-offs**:
- ✅ Zero API costs after training
- ✅ Lower latency (no network calls)
- ✅ Full control over model
- ❌ High upfront training cost
- ❌ Requires ML expertise
- ❌ Infrastructure complexity

**When to use**: If Gemini costs become unsustainable (>€500/month)

### 3. **Rule-based + AI Ensemble**
**Description**: Run both regex and AI parsers, compare results, use most confident
**Trade-offs**:
- ✅ Higher accuracy (ensemble approach)
- ✅ Redundancy
- ❌ Higher latency (two parsers)
- ❌ Higher cost (two API calls if AI used)
- ❌ Complex decision logic

**When to use**: If accuracy is critical and cost/latency acceptable

## Cost Analysis

### Gemini API Pricing (as of 2026)
- **Gemini 1.5 Flash**: €0.00001875 per 1K characters (input), €0.000075 per 1K characters (output)
- **Estimated per extraction**: ~500 chars input + ~200 chars output = **€0.00002 per call**
- **With caching (30% hit rate)**: **€0.000014 per call average**

### Monthly Cost Projection
- **Traffic**: ~1000 modifications/day = 30,000/month
- **Without caching**: 30,000 × €0.00002 = **€0.60/month** ✅
- **With caching**: 30,000 × €0.000014 = **€0.42/month** ✅
- **Worst case (10x traffic)**: **€6/month** ✅

**Conclusion**: Costs are negligible, well under €200/month budget

## Implementation Timeline

**Total Effort**: 9-14 days

- **Week 1**: Foundation (Phases 1-2) - Prompt design, parser implementation, validation
- **Week 2**: Integration (Phases 3-5) - Caching, fallbacks, testing, DI updates
- **Week 3**: Optimization (Phases 6-7) - Monitoring, cost tracking, prompt iteration

## Success Metrics

### Quantitative
- ✅ **Accuracy**: >90% correct field extraction (measured by user confirmations)
- ✅ **Latency**: P95 < 500ms, P99 < 1000ms
- ✅ **Cost**: <€10/day, <€200/month
- ✅ **Reliability**: <10% fallback rate, <5% timeout rate
- ✅ **User satisfaction**: Reduction in "no entendí" responses by 50%+

### Qualitative
- ✅ Users can express modifications naturally without adapting to bot
- ✅ Bot understands novel expressions not in original patterns
- ✅ Conversation feels more fluid and human-like
- ✅ System is resilient to AI service issues
- ✅ Costs remain manageable and predictable

## Rollout Strategy

### Stage 1: Development & Testing (Week 1-2)
- Implement AI parser with all features
- Comprehensive unit and integration tests
- Test with staging environment
- Validate performance and cost projections

### Stage 2: Canary Release (Week 3)
- Deploy to production with 10% traffic routing
- Monitor metrics closely for 3 days
- Compare with regex parser baseline
- Adjust prompts based on edge cases

### Stage 3: Gradual Rollout (Week 4)
- Increase to 25% → 50% → 75% → 100% over 1 week
- Monitor metrics at each stage
- Rollback if any metric degrades significantly
- Document learnings and optimize

### Stage 4: Full Deployment (Week 5)
- Route 100% traffic to AI parser
- Keep regex parser as fallback
- Continue monitoring and optimization
- Celebrate improved user experience! 🎉

## Next Steps

1. **Review this plan** with team/stakeholders
2. **Prioritize phases** based on timeline and resources
3. **Assign implementation** to development team
4. **Start with Phase 1** (prompt engineering and parser design)
5. **Set up monitoring** early (Phase 6) to track progress

---

**Recommendation**: Proceed with pure AI approach. The costs are negligible (<€1/month), latency is acceptable (<500ms), and the user experience improvement is significant. The fallback mechanisms ensure reliability even if AI fails.
