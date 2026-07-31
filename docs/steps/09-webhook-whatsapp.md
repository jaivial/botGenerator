# WhatsApp Webhook Integration

## Simplified Architecture (v3.0.0)

The bot uses a single AI agent with tool calls for processing WhatsApp messages.

---

## Endpoint

**POST** `/api/bot/whatsapp-webhook`

Single endpoint for all WhatsApp message processing.

---

## Request Format

### WhatsApp Payload Structure

```json
{
  "EventType": "messages",
  "message": {
    "chatid": "34612345678@s.whatsapp.net",
    "pushname": "John",
    "text": "Quiero reservar para mañana",
    "messageTimestamp": "1714567890",
    "fromMe": false
  }
}
```

### Supported Message Types

| Type | Description | Handling |
|------|-------------|----------|
| `text` | Text messages | ✅ Fully supported |
| `ButtonsResponseMessage` | Button responses | ✅ Supported |
| `ListResponseMessage` | List selections | ✅ Supported |
| `image`, `audio`, `video`, `document` | Media | ⚠️ Not supported (user notified) |
| `sticker`, `location` | Other | ⚠️ Not supported |

---

## Response Format

### Success Response

```json
{
  "processed": true,
  "agent": true,
  "success": true,
  "messagesSent": 1,
  "toolCalls": ["send_message"],
  "iterations": 2,
  "error": null
}
```

### Error Response

```json
{
  "processed": false,
  "error": "Error message here"
}
```

---

## Processing Flow

```
1. Receive payload
       ↓
2. Check EventType (ignore non-messages)
       ↓
3. Extract message details
       ↓
4. Dedup check (memory cache)
       ↓
5. Send "👀" reaction
       ↓
6. Run AgentOrchestrator
       ↓
7. Return result
```

---

## Agent Flow

The AI agent processes messages using tool calls:

```
User Message
     ↓
┌─────────────────────────┐
│     AI Agent Loop       │
│  (max 15 iterations)    │
└─────────────────────────┘
     ↓
AI decides: use tool or end
     ↓
┌────────────────────────────────────┐
│ Tools available:                   │
│ - send_message                    │
│ - fetch_whatsapp_history         │
│ - check_availability             │
│ - check_day_capacity             │
│ - get_bookings                  │
│ - create_booking                 │
│ - etc.                          │
└────────────────────────────────────┘
     ↓
Tool executed → Result → AI → ...
     ↓
Final message sent to user
```

---

## Call Handling

Incoming WhatsApp calls are automatically handled:

1. Call is rejected
2. Check cooldown (prevent spam)
3. Send auto-reply message
4. Send contact card for manual assistance

### Auto-Reply Message

Default:
```
Hola. Soy el asistente automático de reservas por WhatsApp. 
Para hablar con el restaurante, por favor llama al +34 638 857 294.
```

---

## Testing

### Health Check

```bash
curl http://localhost:5000/api/bot/health
```

Response:
```json
{
  "status": "healthy",
  "timestamp": "2026-05-01T12:00:00Z",
  "version": "3.0.0-simplified-agent"
}
```

### Send Test Message

```bash
curl -X POST http://localhost:5000/api/bot/whatsapp-webhook \
  -H "Content-Type: application/json" \
  -d '{
    "EventType": "messages",
    "message": {
      "chatid": "34612345678@s.whatsapp.net",
      "pushname": "Test User",
      "text": "Hola, quiero hacer una reserva"
    }
  }'
```

### Clear Test State (Development Only)

```bash
curl -X POST "http://localhost:5000/api/bot/test/clear-state?phone=34612345678"
```

---

## UAZAPI Configuration

### Webhook URL

Set your UAZAPI webhook to:
```
https://your-domain.com/api/bot/whatsapp-webhook
```

### Token Verification

The webhook expects messages from UAZAPI with the following format.

---

## Message Extraction

The controller extracts these fields from the WhatsApp payload:

| Field | Source | Description |
|-------|--------|-------------|
| `SenderNumber` | `message.chatid` | Phone number |
| `MessageText` | `message.text` | Message content |
| `PushName` | `message.pushname` | User's display name |
| `MessageId` | `message.messageid` | For reactions/threading |
| `Timestamp` | `message.messageTimestamp` | Unix timestamp |

---

## Dedup Protection

Messages are deduplicated using memory cache with a 1-hour TTL.

**Cache Key Format:**
```
agent:wa:{phone}:{messageId}
```

If a duplicate is detected, the message is ignored but returns 200 OK.

---

## Error Handling

| Scenario | Response |
|----------|----------|
| No message property | 200 OK (ignored) |
| Own message | 200 OK (ignored) |
| Unsupported media | Send text response |
| Tool execution error | Log error, continue |
| General exception | Log error, return 200 OK |

---

## Previous Architecture (v2)

For reference, the previous architecture used two endpoints:

- `POST /api/bot/whatsapp-webhook` - Single AI agent endpoint (v3.0.0)

These have been consolidated into the single `/api/bot/whatsapp-webhook` endpoint.
