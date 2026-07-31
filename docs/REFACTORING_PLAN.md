# 🤖 Bot Refactoring Plan: Simplified Agent-Only Architecture

## Executive Summary

This document outlines the refactoring plan to:
1. **Delete** the legacy pipeline (multi-node architecture)
2. **Simplify** to a single AI agent with tool calls
3. **Remove** local conversation history storage
4. **Validate/Create** missing API tools for comprehensive availability checking

---

## ✅ IMPLEMENTATION STATUS

| Phase | Status | Notes |
|-------|--------|-------|
| Phase 1: Delete Legacy Pipeline | ✅ Complete | All files deleted |
| Phase 2: Simplify Agent Webhook | ✅ Complete | Single endpoint, no history |
| Phase 3: Remove History Storage | ✅ Complete | Using UAZAPI tool |
| Phase 4: Create New Tools | ✅ Complete | 5 new tools implemented |
| Phase 5: Testing | ✅ Complete | All tests pass |

---

## Phase 1 Complete: Legacy Pipeline Deleted

### Files DELETED:

**Pipeline Core (5 files):**
```
bot/src/BotGenerator.Core/Pipeline/
├── ContextAnalyzerNode.cs
├── PipelineOrchestrator.cs
├── ResponseGeneratorNode.cs
├── StateAwarePreRouter.cs
└── ValidationEnrichmentNode.cs
```

**Agents (5 files):**
```
bot/src/BotGenerator.Core/Agents/
├── MainConversationAgent.cs
├── RiceValidatorAgent.cs
├── DateParserAgent.cs
├── AvailabilityCheckerAgent.cs
└── IAgent.cs
```

**Handlers (3 files):**
```
bot/src/BotGenerator.Core/Handlers/
├── BookingHandler.cs
├── CancellationHandler.cs
└── ModificationHandler.cs
```

**Legacy Services (deleted):**
```
├── IntentRouterService.cs
├── NaturalLanguageModificationParser.cs
├── AiNaturalLanguageModificationParser.cs
├── IAiNaturalLanguageModificationParser.cs
├── ConversationHistoryService.cs
├── IConversationHistoryService.cs
├── ConversationVectorStore.cs
├── IConversationVectorStore.cs
├── ChromaConversationVectorStore.cs
└── RestaurantKnowledgeService.cs
```

**Test files (deleted):**
```
tests/BotGenerator.Core.Tests/Agents/
tests/BotGenerator.Core.Tests/Handlers/
tests/BotGenerator.Core.Tests/Conversations/
tests/BotGenerator.Core.Tests/Infrastructure/
tests/BotGenerator.Core.Tests/Controllers/WebhookControllerTests.cs
```

### Program.cs Updated:

**Removed Service Registrations:**
- Pipeline nodes (ContextAnalyzerNode, ValidationEnrichmentNode, ResponseGeneratorNode, PipelineOrchestrator, StateAwarePreRouter)
- Agents (MainConversationAgent, RiceValidatorAgent, DateParserAgent, AvailabilityCheckerAgent)
- Handlers (BookingHandler, CancellationHandler, ModificationHandler)
- ConversationHistoryService
- ConversationVectorStore
- IntentRouterService
- NaturalLanguageModificationParser

**Kept Essential Services:**
- AgentOrchestrator (single AI agent)
- ToolExecutor (tool execution)
- IBookingRepository, IMenuRepository, etc. (database access)
- IWhatsAppService (UAZAPI communication)
- IPendingBookingStore, ICallAutoReplyStore

---

## Phase 2 Complete: Agent Webhook Simplified

### New Architecture

```
WhatsApp → POST /api/bot/whatsapp-webhook → AgentOrchestrator → ToolExecutor → UAZAPI/DB
```

### Simplified Flow

1. **Parse message** from WhatsApp payload
2. **Dedup check** using memory cache
3. **Send reaction** acknowledgment (👀)
4. **Run AgentOrchestrator** - AI decides tool calls
5. **Return result** to WhatsApp

### Endpoint Changes

**Current:**
- `POST /api/bot/whatsapp-webhook` - Single AI agent endpoint

### Version

Bumped to `3.0.0-simplified-agent`

---

## Phase 3 Complete: History Storage Removed

### Changes

- Removed `ConversationHistoryService` and `IConversationHistoryService`
- Removed `ChromaConversationVectorStore` and `IConversationVectorStore`
- Agent uses `fetch_whatsapp_history` tool to get history from UAZAPI
- Removed ChromaDB knowledge base seeding

### Why This Works

The `fetch_whatsapp_history` tool already exists and fetches conversation history directly from UAZAPI via the `/message/find` endpoint. The AI agent can call this tool when it needs context.

---

## Phase 4 Complete: New Tools Created

### Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `Models/NewToolModels.cs` | Created | 5 new response models |
| `Services/ToolExecutor.cs` | Modified | Added 5 new tool implementations |
| `Services/AgentToolDefinitions.cs` | Modified | Added 5 new tool definitions |

### New Tools Available

| Tool | Purpose | Parameters |
|------|---------|------------|
| `check_future_booking` | Check if user has future bookings | None |
| `get_opening_hours_with_capacity` | Opening hours + hour_configuration capacity | `date`, `party_size` (optional) |
| `check_hour_capacity` | Independent hour_configuration check | `date` |
| `check_day_capacity` | Quick day fullness/open/closed status | `date` |
| `check_availability_for_party` | Check if party_size fits on date | `date`, `party_size` |

---

## Tool Details

### 1. check_future_booking

**Purpose:** Quickly check if user already has an upcoming reservation

**Database Query:**
```sql
SELECT COUNT(*) as count
FROM bookings
WHERE contact_phone = @Phone
  AND reservation_date >= CURDATE()
  AND status IN ('pending', 'confirmed')
```

**Response:**
```json
{
  "hasFutureBooking": true,
  "bookingCount": 2,
  "nextBooking": {
    "id": 1234,
    "date": "15/05/2026",
    "time": "14:00",
    "people": 4
  }
}
```

### 2. get_opening_hours_with_capacity

**Purpose:** Combined opening hours + hour_configuration

**Logic:**
1. Check `openinghours` table for date
2. If no row, use default [13:30, 14:00, 14:30, 15:00, 15:30]
3. Check `hour_configuration` for capacity per hour

### 3. check_hour_capacity

**Purpose:** Independent hour_configuration check (for when party_size not known)

**Logic:**
1. Check `hour_configuration` table for date
2. If no row, return "no custom configuration"

### 4. check_day_capacity

**Purpose:** Quick day fullness check

**Logic:**
```
1. SUM(bookings.party_size) WHERE reservation_date = @date
2. Get daily_limit from daily_limits (default 45)
3. Compare: if total >= daily_limit → "full"
```

### 5. check_availability_for_party

**Purpose:** Check if specific party_size fits on date

**Logic:**
```
1. Get daily_limit (default 45)
2. Sum bookings.party_size for date
3. Calculate: free_seats = daily_limit - total_booked
4. Check: if party_size <= free_seats → fits
```

---

## Testing Results

```
Passed!  - Failed: 0, Passed: 68, Skipped: 0, Total: 68
```

All unit tests pass. Integration tests also pass.

---

## Remaining Tasks

All phases are **COMPLETE**. The following documentation updates were made:

- ✅ Created `API_DOCUMENTATION.md` - Full API reference
- ✅ Updated `steps/09-webhook-whatsapp.md` - Simplified webhook docs
- ✅ Updated `steps/01-project-setup.md` - Updated endpoint references
- ✅ Updated `README.md` - New architecture overview
- ✅ Updated `REFACTORING_PLAN.md` - Complete status

---

## Architecture Comparison

### Before (Multi-Node Pipeline)

```
Message → PipelineOrchestrator → [ContextAnalyzer, Validation, ResponseGenerator] → Response
```

### After (Single AI Agent)

```
Message → AgentOrchestrator → [AI decides: use tools] → Response
         ↓
    Tool Calls:
    - send_message
    - fetch_whatsapp_history
    - check_availability
    - get_bookings
    - create_booking
    - check_day_capacity
    - etc.
```

---

## Build Status

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```
