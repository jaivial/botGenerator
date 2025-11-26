# Step 00: Overview - WhatsApp Bot with Google Gemini 2.5 Flash

## What We're Building

This step-by-step guide walks you through building a complete WhatsApp reservation bot using:

- **C# / .NET 8** - Backend language
- **Google AI Studio API** - Gemini 2.5 Flash model
- **External prompt files** - For easy customization
- **Multi-agent architecture** - Specialized AI agents for different tasks

The bot handles:
- Restaurant reservations (booking)
- Cancellations
- Modifications
- Menu inquiries
- Rice type validation (specialized agent)

## Why This Architecture?

### Problem with Hardcoded Prompts
```csharp
// BAD: Hardcoded prompts
var systemPrompt = "Eres un asistente de reservas para Alquería Villa Carmen...";
```

This approach has several problems:
1. Changing prompts requires recompiling
2. Non-developers can't edit AI behavior
3. Hard to manage multiple restaurants
4. No separation of concerns

### Solution: External Prompt Files
```
prompts/
├── restaurants/
│   ├── villacarmen/
│   │   ├── system-main.txt        <-- Easy to edit!
│   │   ├── booking-flow.txt
│   │   └── restaurant-info.txt
│   └── other-restaurant/
│       └── ... (same structure)
└── shared/
    └── common-rules.txt
```

Benefits:
1. Edit prompts without recompiling
2. Non-developers can customize AI behavior
3. Easy to add new restaurants (just copy folder)
4. Clean separation of concerns

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              WEBHOOK                                     │
│                     POST /api/webhook/whatsapp                          │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         MESSAGE EXTRACTOR                                │
│                                                                          │
│  Input: Raw WhatsApp webhook JSON                                       │
│  Output: WhatsAppMessage { SenderNumber, MessageText, PushName, ... }   │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                      CONVERSATION HISTORY                                │
│                                                                          │
│  - Fetch last 20-30 messages from WhatsApp API                          │
│  - Format for AI context                                                 │
│  - Extract booking state (date, time, people collected so far)          │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                        CONTEXT BUILDER                                   │
│                                                                          │
│  Builds dynamic context dictionary:                                      │
│  {                                                                       │
│    "pushName": "Juan García",                                           │
│    "todayES": "Martes, 25 de noviembre de 2025",                        │
│    "nextSaturday": "29/11/2025",                                        │
│    "state_fecha": "29/11/2025",                                         │
│    "state_hora": "FALTA",                                               │
│    ...                                                                   │
│  }                                                                       │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                     PROMPT LOADER & ASSEMBLER                            │
│                                                                          │
│  1. Load prompt files from disk:                                         │
│     - prompts/restaurants/villacarmen/system-main.txt                   │
│     - prompts/restaurants/villacarmen/booking-flow.txt                  │
│     - prompts/shared/whatsapp-rules.txt                                 │
│                                                                          │
│  2. Replace tokens with context values:                                  │
│     "Hola {{pushName}}" → "Hola Juan García"                            │
│                                                                          │
│  3. Assemble into final system prompt                                    │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                        MAIN AI AGENT                                     │
│                                                                          │
│  Google Gemini 2.5 Flash API Call:                                       │
│  {                                                                       │
│    "system_instruction": "<assembled prompt>",                           │
│    "contents": [                                                         │
│      { "role": "user", "parts": [{ "text": "..." }] },                  │
│      { "role": "model", "parts": [{ "text": "..." }] },                 │
│      { "role": "user", "parts": [{ "text": "current message" }] }       │
│    ]                                                                     │
│  }                                                                       │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                       RESPONSE PARSER                                    │
│                                                                          │
│  Parse AI response for commands:                                         │
│  - "BOOKING_REQUEST|Juan|34612...|29/11/2025|4|14:00"                   │
│  - "CANCELLATION_REQUEST|..."                                            │
│  - "MODIFICATION_INTENT"                                                 │
│  - "SAME_DAY_BOOKING"                                                    │
│                                                                          │
│  Extract: Intent + Data + Clean response text                            │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                        INTENT ROUTER                                     │
│                                                                          │
│  Switch on intent:                                                       │
│  - Booking → BookingHandler (may call RiceValidatorAgent)               │
│  - Cancellation → CancellationHandler                                   │
│  - Modification → ModificationHandler                                    │
│  - Normal → Return AI response directly                                  │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
              ┌─────────────────────┼─────────────────────┐
              ▼                     ▼                     ▼
┌──────────────────────┐ ┌──────────────────┐ ┌──────────────────────┐
│   BOOKING HANDLER    │ │  CANCELLATION    │ │   MODIFICATION       │
│                      │ │     HANDLER      │ │      HANDLER         │
│  - Validate data     │ │                  │ │                      │
│  - Check rice type   │ │  - Find booking  │ │  - Find bookings     │
│  - Call specialized  │ │  - Cancel in DB  │ │  - Show options      │
│    agents if needed  │ │  - Send confirm  │ │  - Apply changes     │
└──────────────────────┘ └──────────────────┘ └──────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    SPECIALIZED AGENTS (Mid-path)                         │
│                                                                          │
│  RiceValidatorAgent:                                                     │
│  - Loads rice-validation.txt prompt                                      │
│  - Calls Gemini to validate rice type                                    │
│  - Returns: RICE_VALID|name or RICE_NOT_FOUND|name                      │
│                                                                          │
│  DateParserAgent:                                                        │
│  - Parses natural language dates                                         │
│  - "el próximo sábado" → "29/11/2025"                                   │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                       WHATSAPP SENDER                                    │
│                                                                          │
│  POST to WhatsApp API:                                                   │
│  - Text messages                                                         │
│  - Button messages (for confirmations)                                   │
│  - Menu messages (for options)                                           │
└─────────────────────────────────────────────────────────────────────────┘
```

## Project Structure

```
BotGenerator/
│
├── src/
│   │
│   ├── BotGenerator.Api/                 # ASP.NET Core Web API
│   │   ├── Controllers/
│   │   │   └── WebhookController.cs      # WhatsApp webhook endpoint
│   │   ├── Program.cs                    # DI configuration
│   │   ├── appsettings.json              # Configuration
│   │   └── BotGenerator.Api.csproj
│   │
│   ├── BotGenerator.Core/                # Business logic
│   │   │
│   │   ├── Agents/                       # AI Agents
│   │   │   ├── IAgent.cs                 # Agent interface
│   │   │   ├── MainConversationAgent.cs  # Main conversation handler
│   │   │   ├── RiceValidatorAgent.cs     # Rice validation specialist
│   │   │   └── DateParserAgent.cs        # Date parsing specialist
│   │   │
│   │   ├── Services/                     # Core services
│   │   │   ├── IGeminiService.cs         # Gemini API interface
│   │   │   ├── GeminiService.cs          # Gemini API implementation
│   │   │   ├── IPromptLoaderService.cs   # Prompt loading interface
│   │   │   ├── PromptLoaderService.cs    # Loads prompts from files
│   │   │   ├── IContextBuilderService.cs # Context building interface
│   │   │   ├── ContextBuilderService.cs  # Builds dynamic context
│   │   │   ├── IIntentRouterService.cs   # Intent routing interface
│   │   │   ├── IntentRouterService.cs    # Routes to handlers
│   │   │   ├── IWhatsAppService.cs       # WhatsApp API interface
│   │   │   ├── WhatsAppService.cs        # WhatsApp API implementation
│   │   │   ├── IConversationHistoryService.cs
│   │   │   └── ConversationHistoryService.cs
│   │   │
│   │   ├── Handlers/                     # Intent handlers
│   │   │   ├── BookingHandler.cs         # Creates bookings
│   │   │   ├── CancellationHandler.cs    # Cancels bookings
│   │   │   └── ModificationHandler.cs    # Modifies bookings
│   │   │
│   │   ├── Models/                       # Data models
│   │   │   ├── WhatsAppMessage.cs        # Incoming message
│   │   │   ├── ConversationState.cs      # Conversation state
│   │   │   ├── BookingData.cs            # Booking information
│   │   │   ├── AgentResponse.cs          # Agent response
│   │   │   └── IntentType.cs             # Intent enumeration
│   │   │
│   │   └── BotGenerator.Core.csproj
│   │
│   └── BotGenerator.Prompts/             # External prompt files
│       │
│       ├── restaurants/                  # Per-restaurant prompts
│       │   │
│       │   ├── villacarmen/              # Alquería Villa Carmen
│       │   │   ├── system-main.txt       # Main system identity
│       │   │   ├── restaurant-info.txt   # Hours, menus, location
│       │   │   ├── booking-flow.txt      # Booking process rules
│       │   │   ├── cancellation-flow.txt # Cancellation rules
│       │   │   ├── modification-flow.txt # Modification rules
│       │   │   └── rice-validation.txt   # Rice validation prompt
│       │   │
│       │   └── example-restaurant/       # Template for new restaurants
│       │       └── ...
│       │
│       └── shared/                       # Shared across restaurants
│           ├── date-parsing.txt          # Date interpretation rules
│           ├── whatsapp-history-rules.txt # History usage rules
│           └── common-responses.txt      # Common response templates
│
├── tests/
│   ├── BotGenerator.Core.Tests/
│   │   ├── Agents/
│   │   ├── Services/
│   │   └── Handlers/
│   └── BotGenerator.Integration.Tests/
│
└── BotGenerator.sln
```

## Steps Overview

| Step | Title | Description |
|------|-------|-------------|
| 01 | Project Setup | Create solution, projects, install packages |
| 02 | Core Models | Define data models and enums |
| 03 | Gemini Service | Implement Google AI Studio API client |
| 04 | Prompt System | Build prompt loader and assembler |
| 05 | Context Builder | Create dynamic context for prompts |
| 06 | Main Agent | Implement main conversation agent |
| 07 | Specialized Agents | Build rice validator and other agents |
| 08 | Intent Router | Route responses to appropriate handlers |
| 09 | Webhook & WhatsApp | Set up webhook and WhatsApp integration |
| 10 | Prompt Templates | Create all prompt files |
| 11 | Adding New Restaurants | Guide for replicating to new restaurants |

## Prerequisites

- .NET 8 SDK
- Google AI Studio API key
- WhatsApp Business API access (e.g., uazapi.com)
- Redis (optional, for chat memory)
- Visual Studio Code or Visual Studio 2022

## Let's Begin!

Start with [Step 01: Project Setup](./01-project-setup.md)
