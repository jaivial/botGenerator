# 🤖 Bot API Documentation

## Simplified Agent Architecture v3.0.0

This document describes the simplified WhatsApp bot architecture using a single AI agent with tool calls.

---

## Overview

### Architecture Flow

```
WhatsApp → POST /api/webhook/webhook → AgentOrchestrator → ToolExecutor → WhatsApp Response
                                        ↓
                                   AI Agent Loop
                                        ↓
                    ┌─────────────────┼─────────────────┐
                    ↓                 ↓                 ↓
              send_message    fetch_history    check_availability
                    ↓                 ↓                 ↓
              UAZAPI         UAZAPI           MySQL Database
```

### Key Differences from Legacy

| Aspect | Legacy (v2) | Simplified (v3) |
|--------|-------------|----------------|
| Architecture | Multi-node pipeline | Single AI agent |
| Endpoints | 2 endpoints | 1 endpoint |
| History | Local MySQL storage | UAZAPI direct |
| Tools | Hardcoded flow | AI-driven tool calls |
| State | Pipeline nodes | Agent memory |

---

## Endpoints

### `GET /api/webhook/health`

Health check endpoint.

**Response:**
```json
{
  "status": "healthy",
  "timestamp": "2026-05-01T12:00:00Z",
  "version": "3.0.0-simplified-agent"
}
```

---

### `POST /api/webhook/webhook`

**Main webhook endpoint** for processing WhatsApp messages.

**Request Body (WhatsApp Payload):**
```json
{
  "EventType": "messages",
  "message": {
    "chatid": "34612345678@s.whatsapp.net",
    "pushname": "John",
    "text": "Quiero reservar para mañana",
    "messageTimestamp": "1714567890"
  }
}
```

**Response:**
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

---

## Available Tools

The AI agent has access to **17 tools** for handling reservations:

### Messaging Tools

| Tool | Description | Parameters |
|------|-------------|------------|
| `send_message` | Send WhatsApp message | `message` (string) |
| `fetch_whatsapp_history` | Get conversation history | `limit` (int, default 30) |

### Restaurant Info Tools

| Tool | Description | Parameters |
|------|-------------|------------|
| `get_restaurant_info` | Restaurant contact info | None |
| `get_rice_menu` | Available rice types | None |

### Availability Tools (NEW - v3)

| Tool | Description | Parameters |
|------|-------------|------------|
| `check_future_booking` | Check if user has future bookings | None |
| `check_day_capacity` | Quick day fullness check | `date` |
| `check_availability_for_party` | Check if party fits on date | `date`, `party_size` |
| `get_opening_hours_with_capacity` | Opening hours + capacity | `date`, `party_size` (optional) |
| `check_hour_capacity` | Hour configuration check | `date` |

### Legacy Availability Tools

| Tool | Description | Parameters |
|------|-------------|------------|
| `check_availability` | Full availability check | `date`, `time`, `people` |
| `get_opening_hours` | Opening hours for date | `date` |
| `get_hour_data` | Detailed hour capacity | `date` |
| `get_day_status` | Day open/closed status | `date` |

### Booking Management Tools

| Tool | Description | Parameters |
|------|-------------|------------|
| `get_bookings` | User's existing bookings | `phone` (optional) |
| `create_booking` | Create new reservation | `date`, `time`, `people`, `confirmed: true` |
| `cancel_booking` | Cancel reservation | `booking_id`, `confirmed: true` |
| `modify_booking` | Modify reservation | `booking_id`, fields, `confirmed: true` |

---

## Tool Details

### check_future_booking

Check if user already has an upcoming reservation.

**Parameters:** None (uses phone from webhook)

**Response:**
```json
{
  "hasFutureBooking": true,
  "bookingCount": 2,
  "nextBooking": {
    "id": 1234,
    "date": "15/05/2026",
    "time": "14:00",
    "people": 4,
    "riceType": "Paella Valenciana"
  }
}
```

---

### check_day_capacity

Quickly check if a day is open, full, or closed.

**Parameters:**
- `date` (required): Format `dd/MM/yyyy`

**Response:**
```json
{
  "date": "15/05/2026",
  "status": "open",
  "dailyLimit": 45,
  "totalBooked": 38,
  "freeSeats": 7,
  "isFull": false
}
```

**Status values:**
- `open`: Day is available for bookings
- `full`: Daily limit reached
- `closed`: Restaurant closed (e.g., Monday)

---

### check_availability_for_party

Check if a specific number of people can fit on a date.

**Parameters:**
- `date` (required): Format `dd/MM/yyyy`
- `party_size` (required): Number of people

**Response:**
```json
{
  "date": "15/05/2026",
  "partySize": 6,
  "fits": true,
  "dailyLimit": 45,
  "totalBooked": 38,
  "freeSeats": 7,
  "message": "Hay sitio para 6 personas. Plazas libres: 7"
}
```

---

### get_opening_hours_with_capacity

Get available hours with capacity information per hour.

**Parameters:**
- `date` (required): Format `dd/MM/yyyy`
- `party_size` (optional): Check if party fits in each hour

**Response:**
```json
{
  "date": "15/05/2026",
  "source": "database",
  "defaultHours": ["13:30", "14:00", "14:30", "15:00", "15:30"],
  "hours": [
    {
      "hour": "13:30",
      "available": true,
      "capacity": 15,
      "booked": 8,
      "free": 7,
      "isClosed": false
    },
    {
      "hour": "14:00",
      "available": true,
      "capacity": 12,
      "booked": 12,
      "free": 0,
      "isClosed": false
    }
  ],
  "totalCapacity": 45,
  "totalBooked": 20,
  "totalFree": 25
}
```

**Logic:**
1. Check `openinghours` table for date
2. If no row, use defaults: `[13:30, 14:00, 14:30, 15:00, 15:30]`
3. Check `hour_configuration` for per-hour capacity
4. If `party_size` provided, mark hours as unavailable if not enough capacity

---

### check_hour_capacity

Check only `hour_configuration` for a date (independent of opening hours).

**Parameters:**
- `date` (required): Format `dd/MM/yyyy`

**Response:**
```json
{
  "date": "15/05/2026",
  "hasCustomConfig": true,
  "hourData": {
    "13:30": {
      "capacity": 15,
      "bookings": 8,
      "percentage": 22.2,
      "isClosed": false,
      "status": "available"
    },
    "14:00": {
      "capacity": 12,
      "bookings": 12,
      "percentage": 100,
      "isClosed": false,
      "status": "full"
    }
  }
}
```

---

### create_booking

Create a new reservation and **automatically send WhatsApp confirmation** to the user.

**⚠️ IMPORTANT:** This tool includes safety validations to prevent overbooking:

1. **Date validation**: Must be today or in the future
2. **Day status**: Fails if the restaurant is closed (Mon/Tue/Wed default, or explicitly closed)
3. **Daily capacity**: Fails if `party_size` exceeds remaining daily capacity
4. **Time slot**: Fails if the requested time is not in the available hours
5. **Hour capacity**: Fails if `party_size` exceeds remaining hourly capacity (if `hour_configuration` exists)

**After successful creation:**
- Automatically sends WhatsApp confirmation message with formatted booking details
- Includes buttons for "CONDICIONES" and "Cancelar Reserva"

**Parameters:**
- `date` (required): Format `YYYY-MM-DD` or `dd/MM/yyyy`
- `time` (required): Format `HH:MM` (e.g., 14:00)
- `people` (optional, default 2): Number of people
- `rice_type` (optional): Type of rice
- `rice_servings` (optional): Number of rice servings
- `name` (optional, default "Cliente WhatsApp")
- `high_chairs` (optional): Number of high chairs needed
- `baby_strollers` (optional): Number of baby stroller spaces needed
- `confirmed` (required): Must be `true`

**Validation Errors:**

```json
// Day is closed
{"isError": true, "content": "El restaurante está cerrado el lunes. No se puede crear la reserva."}

// Not enough daily capacity
{"isError": true, "content": "No hay suficiente capacidad para 8 personas en 15/05/2026. Plazas libres: 5."}

// Time slot not available
{"isError": true, "content": "La hora 16:00 no está disponible. Horas disponibles: 13:30, 14:00, 14:30, 15:00, 15:30."}

// Not enough hourly capacity
{"isError": true, "content": "No hay suficiente capacidad a las 14:00 para 6 personas. Plazas libres a esa hora: 3."}
```

**Success Response:**
```json
{
  "success": true,
  "bookingId": 1234,
  "date": "15/05/2026",
  "time": "14:00",
  "people": 4,
  "whatsappSent": true,
  "message": "Reserva confirmada para 15/05/2026 a las 14:00, 4 personas. Se ha enviado confirmación por WhatsApp."
}
```

**WhatsApp Confirmation Message Format:**
```
*Confirmación de Reserva - Alquería Villa Carmen*

Hola {name},

Gracias por elegir Alquería Villa Carmen. Su reserva ha sido confirmada:

📅 *Fecha:* 15/05/2026
🕒 *Hora:* 14:00
👥 *Personas:* 4
🍚 *Arroz:* Paella Valenciana (4 raciones)
👶 *Tronas:* 0
🍼 *Carros de bebé:* 0

Al hacer esta reserva, usted ha confirmado y aceptado las condiciones...

[Buttons: CONDICIONES | Cancelar Reserva]
```

**Important:** The AI agent should check availability first using `check_availability_for_party` or `get_opening_hours_with_capacity` before calling this tool.

---

### cancel_booking

Cancel an existing reservation.

**Parameters:**
- `booking_id` (required)
- `confirmed` (required): Must be `true`

**Response:**
```json
{
  "success": true,
  "bookingId": 1234,
  "message": "Booking for 15/05/2026 at 14:00 has been cancelled."
}
```

---

### modify_booking

Modifies an existing reservation with comprehensive validation and WhatsApp notification.

**⚠️ IMPORTANT:** This tool includes safety validations:

1. **Ownership validation**: Booking must belong to the phone number
2. **Status validation**: Cannot modify cancelled bookings
3. **Date validation**: Cannot modify past, today, or tomorrow bookings
4. **Advance time**: Must have 24+ hours until reservation
5. **Modification limit**: Max 3 modifications per booking
6. **Capacity validation**: Validates capacity for new date/time if changed

**After successful modification:**
- Automatically sends WhatsApp confirmation to customer
- Logs modification to `modification_history` table

**Parameters:**
- `booking_id` (required): Booking ID to modify
- `date` (optional): New date (YYYY-MM-DD or dd/MM/yyyy)
- `time` (optional): New time (HH:mm)
- `people` (optional): New party size
- `rice_type` (optional): New rice type
- `rice_servings` (optional): Number of rice servings
- `high_chairs` (optional): Number of high chairs
- `baby_strollers` (optional): Number of baby stroller spaces
- `clear_rice` (optional): Set to `true` to remove rice
- `confirmed` (required): Must be `true`

**Validation Errors:**
```json
// Not owner
{"isError": true, "content": "No tienes permiso para modificar esta reserva."}

// Cancelled booking
{"isError": true, "content": "No se puede modificar una reserva cancelada."}

// Past booking
{"isError": true, "content": "No se pueden modificar reservas que ya han pasado."}

// Today booking
{"isError": true, "content": "No se pueden modificar reservas para hoy."}

// Tomorrow booking
{"isError": true, "content": "No se pueden modificar reservas para mañana."}

// Less than 24 hours
{"isError": true, "content": "Se requiere al menos 24 horas de antelación."}

// Max modifications reached
{"isError": true, "content": "Has alcanzado el límite máximo de 3 modificaciones."}

// No capacity for new date
{"isError": true, "content": "No hay suficiente capacidad para 8 personas. Plazas libres: 5."}

// Restaurant closed on new date
{"isError": true, "content": "El restaurante está cerrado el lunes."}
```

**Success Response:**
```json
{
  "success": true,
  "bookingId": 1234,
  "message": "Reserva modificada correctamente.",
  "changes": ["personas: 4 → 6", "hora: 14:00 → 15:00"],
  "modificationsRemaining": 1,
  "updatedBooking": {
    "date": "15/05/2026",
    "time": "15:00",
    "people": 6,
    "rice": "Paella Valenciana"
  }
}
```

---

## Database Schema

### Tables Used

| Table | Purpose |
|-------|---------|
| `bookings` | Reservation records |
| `openinghours` | Custom opening hours per date |
| `hour_configuration` | Per-hour capacity configuration |
| `daily_limits` | Daily booking limits |
| `reservation_manager` | Alternative daily limits table |

### Key Queries

**Future bookings for phone:**
```sql
SELECT * FROM bookings 
WHERE contact_phone = @Phone 
  AND reservation_date >= CURDATE() 
  AND status IN ('pending', 'confirmed')
ORDER BY reservation_date ASC
```

**Sum party size for date:**
```sql
SELECT SUM(party_size) FROM bookings 
WHERE reservation_date = @Date 
  AND status IN ('pending', 'confirmed')
```

**Opening hours for date:**
```sql
SELECT hoursarray FROM openinghours 
WHERE dateselected = @Date
```

**Hour configuration:**
```sql
SELECT hourData FROM hour_configuration 
WHERE date = @Date
```

---

## Example Conversations

### Example 1: Simple Inquiry

**User:** "Qué horario tienen?"

**AI Agent Flow:**
1. No tool needed (general hours in system prompt)
2. `send_message` → "Estamos abiertos de 13:30 a 18:00 🍚"

---

### Example 2: Check Availability

**User:** "Hay disponibilidad para mañana a las 14:00 para 4 personas?"

**AI Agent Flow:**
1. `check_day_capacity`({date: "02/05/2026"})
   - Response: `{status: "open", freeSeats: 20}`
2. `check_hour_capacity`({date: "02/05/2026"})
   - Response: `{"14:00": {capacity: 10, booked: 6}}`
3. `send_message` → "Sí, hay disponibilidad para 4 personas a las 14:00. ¿Quieres reservar?"

---

### Example 3: Create Booking

**User:** "Sí, reserva por favor"

**AI Agent Flow:**
1. `create_booking`({
   date: "2026-05-02",
   time: "14:00",
   people: 4,
   confirmed: true
})
2. Response: `{success: true, bookingId: 1234}`
3. `send_message` → "✅ ¡Reserva confirmada!\n📅 2 de mayo\n🕑 14:00\n👥 4 personas"

---

## Error Handling

### Tool Errors

If a tool fails, the AI agent receives an error message and can:
1. Try a different approach
2. Inform the user of the issue
3. Suggest an alternative

**Example error response:**
```json
{
  "isError": true,
  "content": "Error: Cannot create booking - date is in the past"
}
```

### Validation Errors

Tools return validation errors if parameters are missing or invalid:
```json
{
  "isError": true,
  "content": "Missing 'date' parameter (format: dd/MM/yyyy)"
}
```

---

## Webhook Configuration

### UAZAPI Setup

Configure your UAZAPI webhook to point to:
```
https://your-domain.com/api/webhook/webhook
```

### Testing Locally

```bash
curl -X POST http://localhost:5000/api/webhook/webhook \
  -H "Content-Type: application/json" \
  -d '{
    "EventType": "messages",
    "message": {
      "chatid": "34612345678@s.whatsapp.net",
      "pushname": "Test User",
      "text": "Hola"
    }
  }'
```

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 3.0.0 | 2026-05-01 | Simplified agent architecture, removed legacy pipeline |
| 2.0.0 | 2025-XX-XX | Multi-node pipeline architecture |
| 1.0.0 | 2024-XX-XX | Initial WhatsApp bot |
