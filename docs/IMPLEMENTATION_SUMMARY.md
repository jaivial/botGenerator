# Implementation Summary: External Booking Integration & Minimax Migration

## Overview
This document summarizes the changes made to fix the two issues:
1. **External Booking Awareness**: The bot now fetches booking information from an external API when a user first contacts the bot, and stores conversation history in the database.
2. **AI Model Migration**: Switched from Google Gemini to Minimax M2.1 for better handling of long prompts and system messages.

---

## Changes Made

### 1. New Services Created

#### IMessageRepository / MessageRepository
- **Location**: `src/BotGenerator.Core/Services/IMessageRepository.cs`, `MessageRepository.cs`
- **Purpose**: Persist conversation messages to MySQL database
- **Methods**:
  - `GetMessagesAsync()` - Retrieve conversation history
  - `SaveMessageAsync()` - Save a message
  - `HasMessagesAsync()` - Check if user has history
  - `ClearMessagesAsync()` - Clear user's history

#### IExternalBookingService / ExternalBookingService
- **Location**: `src/BotGenerator.Core/Services/IExternalBookingService.cs`, `ExternalBookingService.cs`
- **Purpose**: Fetch booking information from external API
- **Methods**:
  - `GetBookingByPhoneAsync()` - Fetch booking from API
  - `ParseBookingFromConfirmationMessage()` - Parse booking from confirmation text
- **Configuration**:
  ```env
  EXTERNAL_BOOKING_API_URL=https://your-booking-system.com/api
  EXTERNAL_BOOKING_API_KEY=your_external_api_key_here
  ```

#### IMinimaxService / MinimaxService
- **Location**: `src/BotGenerator.Core/Services/IMinimaxService.cs`, `MinimaxService.cs`
- **Purpose**: AI service using Minimax M2.1 model
- **Features**:
  - Implements both `IMinimaxService` and `IGeminiService` for drop-in replacement
  - Better handling of long prompts and system messages
  - OpenAI-compatible API format
- **Configuration**:
  ```env
  MINIMAX_API_KEY=your_minimax_api_key_here
  ```

### 2. Updated Services

#### ConversationHistoryService
- **Location**: `src/BotGenerator.Core/Services/ConversationHistoryService.cs`
- **Changes**:
  - Now uses `IMessageRepository` for database persistence
  - Added `IExternalBookingService` dependency
  - **Key behavior**: When `GetHistoryAsync()` is called for a new user (no messages in DB):
    1. Checks database for existing messages
    2. If no messages found, calls external booking API
    3. If booking found, creates initial assistant message with confirmation
    4. Saves confirmation message to database
    5. Returns history including the booking confirmation
  - Added caching layer for performance
  - Added `InitializeFromConfirmationMessageAsync()` for direct message parsing

### 3. Configuration Updates

#### Program.cs
- **Location**: `src/BotGenerator.Api/Program.cs`
- **Changes**:
  - Added HTTP client for `IExternalBookingService`
  - Registered `IMessageRepository` and `IExternalBookingService` as singletons
  - Replaced `GeminiService` with `MinimaxService` for `IGeminiService`
  - Added environment variable handling for new configuration

#### .env.example
- **Location**: `.env.example`
- **Added variables**:
  ```env
  MINIMAX_API_KEY=your_minimax_api_key_here
  MYSQL_CONNECTION_STRING=Server=localhost;Database=...;User=...;Password=...;
  EXTERNAL_BOOKING_API_URL=https://your-booking-system.com/api
  EXTERNAL_BOOKING_API_KEY=your_external_api_key_here
  ```

### 4. Database Migration

#### conversation_messages Table
- **Location**: `docs/database_migration.sql`
- **Schema**:
  ```sql
  CREATE TABLE conversation_messages (
      id BIGINT AUTO_INCREMENT PRIMARY KEY,
      phone_number VARCHAR(20) NOT NULL,
      role VARCHAR(20) NOT NULL,  -- user, assistant, system
      content TEXT NOT NULL,
      timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
      message_id VARCHAR(100) NULL,
      from_name VARCHAR(100) NULL,
      INDEX idx_phone_number (phone_number),
      INDEX idx_timestamp (timestamp),
      INDEX idx_phone_timestamp (phone_number, timestamp)
  );
  ```

---

## How It Works

### User First Contact Flow

```
1. User sends first message to bot
   ↓
2. WebhookController receives message
   ↓
3. Calls _historyService.GetHistoryAsync(phone)
   ↓
4. ConversationHistoryService checks database
   ↓
5. No messages found → calls _externalBookingService.GetBookingByPhoneAsync(phone)
   ↓
6. If booking found:
   - Creates assistant message with confirmation
   - Saves to database via _messageRepository
   - Returns history including confirmation
   ↓
7. MainConversationAgent receives history with confirmation
   ↓
8. ContextBuilderService formats history for prompt
   ↓
9. MinimaxService generates response with full context
```

### Example Conversation Flow

**External System Sends (before bot):**
```
*Confirmación de Reserva - Alquería Villa Carmen*

Hola Eneida Balaguer Garcia,

Gracias por elegir Alquería Villa Carmen. Su reserva ha sido confirmada:

📅 Fecha: 14/02/2026
🕒 Hora: 15:00
👥 Personas: 2
🍚 Arroz: Arroz meloso de pulpo y gambones (+5€) x 2
👶 Tronas: 0
🍼 Carros de bebé: 0
...
```

**User Replies:**
```
"Gracias, nos vemos el sábado"
```

**Bot Response (knowing about the booking):**
```
"¡Perfecto! Quedamos confirmados para el sábado 14 de febrero a las 15:00. 
Recuerda que tienes una reserva para 2 personas con Arroz meloso de pulpo y gambones.
¡Nos vemos pronto!"
```

---

## Setup Instructions

### 1. Run Database Migration

```bash
mysql -u your_user -p your_database < docs/database_migration.sql
```

### 2. Update Environment Variables

```bash
cp .env.example .env
# Edit .env and add:
# - MINIMAX_API_KEY
# - MYSQL_CONNECTION_STRING (or DB_* variables)
# - EXTERNAL_BOOKING_API_URL
# - EXTERNAL_BOOKING_API_KEY (if required)
```

### 3. Configure External Booking API

The external booking API should respond to:
```
GET {EXTERNAL_BOOKING_API_URL}/api/bookings/by-phone?phone={phone_number}
```

Expected response format:
```json
{
  "customerName": "Eneida Balaguer Garcia",
  "date": "14/02/2026",
  "time": "15:00",
  "people": 2,
  "arrozType": "Arroz meloso de pulpo y gambones (+5€)",
  "arrozServings": 2,
  "highChairs": 0,
  "babyStrollers": 0
}
```

### 4. Build and Run

```bash
dotnet build
dotnet run --project src/BotGenerator.Api
```

---

## Backwards Compatibility

- The `MinimaxService` implements `IGeminiService`, so existing code that depends on `IGeminiService` continues to work
- If `EXTERNAL_BOOKING_API_URL` is not configured, the bot works normally (just won't fetch external bookings)
- If MySQL is not configured, the service will fail gracefully with error logs

---

## Testing

### Test External Booking Integration

1. Clear conversation history for a test phone:
   ```bash
   curl -X POST "http://localhost:5000/api/webhook/test/clear-state?phone=34612345678"
   ```

2. Send a message from that phone

3. Check logs for:
   - "No history found for {phone}, attempting to fetch from external booking API"
   - "Found external booking for {phone}, initializing conversation history"

### Test Minimax Integration

1. Send any message to the bot
2. Check logs for:
   - "MinimaxService initialized with model: MiniMax-M2-1"
   - "Sending request to Minimax..."
   - "Received Minimax response..."

---

## Troubleshooting

### Bot doesn't fetch external booking
- Check `EXTERNAL_BOOKING_API_URL` is configured
- Verify API returns 200 with correct JSON format
- Check logs for "No external booking found for {phone}"

### Minimax API errors
- Verify `MINIMAX_API_KEY` is set correctly
- Check API key has sufficient quota
- Check logs for specific error messages

### Database errors
- Verify MySQL connection string
- Ensure `conversation_messages` table exists
- Check logs for connection errors
