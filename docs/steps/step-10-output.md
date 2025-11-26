# Step 10: Prompt Templates - COMPLETED

## Summary
Successfully created all 9 prompt template files for the BotGenerator WhatsApp bot project.

## Files Created/Updated

### Villa Carmen Restaurant Prompts (6 files)
1. **system-main.txt** - Main system prompt with identity, state tracking, and communication style
2. **restaurant-info.txt** - Restaurant details, hours, contact info, and date interpretation
3. **booking-flow.txt** - Complete booking process flow with steps and examples
4. **cancellation-flow.txt** - Cancellation process and command format (NEW)
5. **modification-flow.txt** - Modification intent detection and handling (NEW)
6. **rice-validation.txt** - Rice menu validation logic and response formats (NEW)

### Shared Prompts (3 files)
1. **whatsapp-history-rules.txt** - Rules for using conversation history
2. **date-parsing.txt** - Date interpretation guidelines (NEW)
3. **common-responses.txt** - Standard response templates (NEW)

## File Locations
```
/home/jaime/Documents/projects/botGenerator/src/BotGenerator.Prompts/
├── restaurants/
│   └── villacarmen/
│       ├── booking-flow.txt
│       ├── cancellation-flow.txt
│       ├── modification-flow.txt
│       ├── restaurant-info.txt
│       ├── rice-validation.txt
│       └── system-main.txt
└── shared/
    ├── common-responses.txt
    ├── date-parsing.txt
    └── whatsapp-history-rules.txt
```

## Build Verification
- **Command:** dotnet build
- **Status:** SUCCESS
- **Warnings:** 0
- **Errors:** 0
- **Time:** 3.86 seconds

## Key Features Implemented

### 1. System Main Prompt
- Identity and context setup
- State tracking with visual indicators (✅/❌)
- Critical rules for natural conversation
- Communication style guidelines
- Examples of good vs. bad responses

### 2. Restaurant Info
- Complete business information
- Operating hours in table format
- Contact details and menu links
- Weekend availability templates
- Date interpretation rules

### 3. Booking Flow
- Step-by-step reservation process
- Required data collection (date, time, people, rice)
- Rice handling (yes/no cases)
- Confirmation workflow
- Command generation format: BOOKING_REQUEST|name|phone|date|people|time

### 4. Cancellation Flow
- Data collection for cancellations
- Natural questioning approach
- Command format: CANCELLATION_REQUEST|name|phone|date|people|time
- Important notes about name matching

### 5. Modification Flow
- Intent detection pattern
- Simple response format with MODIFICATION_INTENT marker
- Delegates to system for reservation lookup

### 6. Rice Validation
- Fuzzy matching logic for rice types
- Handles partial names and accents
- Multiple format responses: RICE_VALID, RICE_NOT_FOUND, RICE_MULTIPLE
- Price removal from returned names

### 7. WhatsApp History Rules
- Guidelines for context awareness
- Examples of correct/incorrect history usage
- Prevents redundant questions

### 8. Date Parsing
- Natural language date interpretation
- Weekend calculations
- Common phrase mappings
- Examples of correct handling

### 9. Common Responses
- Pre-defined greetings
- Confirmations
- Error messages
- Farewells

## Template Variables Used
- {{pushName}} - User's WhatsApp name
- {{senderNumber}} - User's phone number
- {{messageText}} - Current message
- {{todayES}} - Today's day name in Spanish
- {{todayFormatted}} - Today's date formatted
- {{currentYear}} - Current year
- {{nextSaturday}} - Next Saturday's date
- {{nextSunday}} - Next Sunday's date
- {{upcomingWeekends}} - List of upcoming weekends
- {{state_fecha}}, {{state_hora}}, {{state_personas}}, {{state_arroz}} - Booking state
- {{formattedHistory}} - Conversation history
- {{availableRiceTypes}} - Menu rice types
- {{userRiceRequest}} - User's rice request

## Dependencies for Next Steps
These prompt templates will be used by:
- Prompt loading service (to be implemented)
- Template rendering engine (Handlebars integration)
- AI conversation handler
- Command parser for booking/cancellation/modification flows

## Issues Encountered
None - all files created successfully and build completed without errors.

## Next Steps Recommendations
1. Implement prompt loader service to read and cache these templates
2. Set up Handlebars.NET for template variable replacement
3. Create prompt composition logic to combine templates
4. Build command parser to handle BOOKING_REQUEST, CANCELLATION_REQUEST, MODIFICATION_INTENT
5. Integrate rice validation flow with menu data
