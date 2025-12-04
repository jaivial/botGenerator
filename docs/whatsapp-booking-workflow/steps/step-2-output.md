# Step 2 Output: Analyze check_date_availability.php API structure

## Status: COMPLETED

## Execution Summary
Successfully analyzed the check_date_availability.php API located at `/home/jaime/Documents/projects/alqueriavillacarmen/api/check_date_availability.php`. This API provides comprehensive date availability checking with two modes: simple mode (date-only validation) and full mode (hourly availability with party size). The API includes 7 distinct validation layers with specific response codes for each scenario.

## Files Read
- `/home/jaime/Documents/projects/alqueriavillacarmen/api/check_date_availability.php` - Date availability checking API (352 lines)

## API Overview

### Endpoint
- **URL**: `https://alqueriavillacarmen.com/api/check_date_availability.php`
- **Method**: POST
- **Content-Type**: application/json

### Operation Modes

#### Simple Mode (Date-Only Check)
- **Trigger**: When `partySize` parameter is NOT provided
- **Validation Layers**: 4 checks
  1. Special holidays
  2. Closed days (database + default schedule)
  3. 35-day future range
  4. Return available
- **Use Case**: Quick date validation before asking for party size

#### Full Mode (Complete Availability Check)
- **Trigger**: When `partySize` parameter IS provided
- **Validation Layers**: 7 checks (all simple mode checks + 3 more)
  5. Hourly availability from gethourdata.php API
  6. Specific time slot availability
  7. Daily capacity validation (if bookingId provided)
- **Use Case**: Complete validation during booking modification or detailed availability check

## Input Parameters

### Required Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `newDate` | string | YES | Date in YYYY-MM-DD format to check availability |

### Optional Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `partySize` | integer | NO | Number of people. Triggers full mode if provided |
| `currentTime` | string | NO | Time slot (e.g., "14:00") to check if it's available |
| `bookingId` | integer | NO | Existing booking ID for modification scenarios |

### Parameter Validation
- **Line 18-25**: Validates that `newDate` is provided, returns error if missing
- **Line 29**: Simple check mode detection: `$simpleCheck = ($partySize === null);`

## Helper Functions

### 1. isNormallyClosedDay($date)
- **Lines**: 41-45
- **Purpose**: Checks if a date falls on Monday, Tuesday, or Wednesday
- **Returns**: boolean (true if Mon/Tue/Wed)
- **Logic**: Uses PHP's `date('N')` where 1=Monday, 7=Sunday

### 2. isDateClosed($date, $closedDays, $openedDays)
- **Lines**: 62-79
- **Purpose**: Comprehensive date closure check with 3-step priority logic
- **Priority Order**:
  1. **Highest**: If in `opened_days` array → OPEN (overrides everything)
  2. **Medium**: If in `closed_days` array → CLOSED
  3. **Default**: If in neither → Check day of week (Mon/Tue/Wed = closed, Thu/Fri/Sat/Sun = open)
- **Returns**: boolean (true if closed)

### 3. isDateTooFarInFuture($date)
- **Lines**: 88-98
- **Purpose**: Validates date is within 35-day booking window
- **Returns**: array with two keys:
  - `isTooFar`: boolean (true if > 35 days)
  - `daysUntil`: integer (number of days until target date)

### 4. formatAvailableHours($hours)
- **Lines**: 225-238
- **Purpose**: Formats available hours array into conversational Spanish text
- **Examples**:
  - 1 hour: "14:00"
  - 2 hours: "14:00 o 15:00"
  - 3+ hours: "14:00, 15:00 o 16:00"

## Validation Layers (Execution Order)

### Layer 1: Closed/Opened Days Database Fetch
- **Lines**: 108-112
- **API Called**: `https://alqueriavillacarmen.com/fetch_closed_days.php`
- **Returns**: JSON with both `closed_days` and `opened_days` arrays
- **Purpose**: Single API call to get all special date overrides

### Layer 2: Special Holidays Check
- **Lines**: 117-129
- **Hard-Coded Dates**: 12-24, 12-25, 12-31, 01-01, 01-05, 01-06
- **Format**: MM-DD (year-agnostic)
- **Response Code**: `special_holiday`
- **Success**: true
- **Available**: false
- **Message**: "Uy, esos días festivos (24, 25 y 31 de diciembre, 1, 5 y 6 de enero) tienen un menú especial y no se pueden modificar reservas. ¿Prefieres otro día? 😊"

### Layer 3: Closed Days Check
- **Lines**: 138-148
- **Uses**: `isDateClosed()` function with 3-step priority logic
- **Response Code**: `closed_day`
- **Success**: true
- **Available**: false
- **Message**: "Ese día ({formattedDate}) estamos cerrados. ¿Qué tal otro día? Abrimos jueves, viernes, sábado y domingo 😊"

### Layer 4: 35-Day Range Check
- **Lines**: 153-165
- **Uses**: `isDateTooFarInFuture()` function
- **Response Code**: `too_far_future`
- **Success**: true
- **Available**: false
- **Extra Field**: `daysUntil` (integer)
- **Message**: "Uy, esa fecha está muy lejos todavía (más de 35 días). Solo aceptamos reservas con un máximo de 35 días de antelación. ¿Qué tal una fecha más cercana? 😊"

### Layer 5: Simple Mode Exit (if no partySize)
- **Lines**: 175-189
- **Trigger**: When `$simpleCheck === true`
- **Response Code**: `date_open`
- **Success**: true
- **Available**: true
- **Extra Fields**:
  - `simpleCheck`: true
  - `isExplicitlyOpened`: boolean
- **Message**: "El día {formattedDate} está disponible para reservas 😊"
- **Note**: Exits before hourly checks

### Layer 6: Hourly Availability Check (Full Mode Only)
- **Lines**: 195-252
- **API Called**: `https://alqueriavillacarmen.com/api/gethourdata.php`
- **POST Data**: `{ "date": newDate, "party_size": partySize }`
- **Expected Response**: JSON with `available_hours` array
- **Each Hour Object**:
  ```json
  {
    "time": "14:00",
    "capacity": 50,
    "remaining": 20
  }
  ```

#### No Hours Available Response
- **Lines**: 241-252
- **Response Code**: `no_hours_available`
- **Success**: true
- **Available**: false
- **Extra Field**: `availableHours`: []
- **Message**: "Ay, lo siento 😔 Ese día ({formattedDate}) no tengo ninguna mesa libre para {partySize} personas. ¿Te vendría bien otro día?"

### Layer 7: Daily Capacity Cross-Validation (if bookingId provided)
- **Lines**: 266-308
- **Purpose**: When modifying an existing booking, verify the new date can accommodate the party size
- **Database Queries**:
  1. Get `daily_limit` for new date (default: 100)
  2. Get current total party size for new date (excluding current booking)
- **Validation**: `(currentTotal + partySize) > dailyLimit`
- **Response Code**: `capacity_exceeded_new_date`
- **Success**: true
- **Available**: false
- **Extra Fields**:
  - `dailyLimit`: integer
  - `currentTotal`: integer
- **Message**: "Ay, qué pena 😔 Ese día ({formattedDate}) ya estamos completos para grupos de {partySize} personas. ¿Te viene bien otro día?"

## Success Response Formats

### Format 1: Current Time Available (Full Mode)
- **Lines**: 311-322
- **Condition**: `currentTime` provided AND available in `availableHours`
- **Success**: true
- **Available**: true
- **HasAvailability**: true
- **currentTimeAvailable**: true
- **Message**: "¡Perfecto! 😊 Hay disponibilidad para {partySize} personas el {formattedDate} a las {currentTime}"
- **Extra Field**: `availableHours` array

### Format 2: Current Time NOT Available But Other Hours Are (Full Mode)
- **Lines**: 323-342
- **Condition**: Hours exist but current time not available, OR no current time provided
- **Success**: true
- **Available**: true
- **HasAvailability**: true
- **currentTimeAvailable**: false
- **Response Code**: `current_time_not_available`
- **Message Variations**:
  - If currentTime provided: "Esa hora ({currentTime}) no está libre ese día 😔 Pero tengo disponible: {hoursFormatted}. ¿Cuál te viene mejor?"
  - If no currentTime: "Para {partySize} personas el {formattedDate}, tengo disponible: {hoursFormatted}. ¿Qué hora prefieres?"
- **Extra Field**: `availableHours` array

## All Response Codes Summary

| Response Code | Trigger | Available | Success | Mode |
|---------------|---------|-----------|---------|------|
| `special_holiday` | Date is 12-24, 12-25, 12-31, 01-01, 01-05, 01-06 | false | true | Both |
| `closed_day` | Date is in closed_days OR is Mon/Tue/Wed (unless in opened_days) | false | true | Both |
| `too_far_future` | Date is > 35 days from today | false | true | Both |
| `date_open` | Date passed all checks (simple mode) | true | true | Simple |
| `no_hours_available` | No time slots available for party size | false | true | Full |
| `capacity_exceeded_new_date` | Daily capacity exceeded for new date | false | true | Full |
| `current_time_not_available` | Requested time not available but others are | true | true | Full |
| (no code) | Current time available | true | true | Full |
| (error) | Missing newDate parameter | false | false | Both |
| (error) | gethourdata.php API failure | false | false | Full |

## Complete Response Structure Examples

### Example 1: Special Holiday
```json
{
  "success": true,
  "available": false,
  "hasAvailability": false,
  "message": "Uy, esos días festivos...",
  "reason": "special_holiday"
}
```

### Example 2: Closed Day
```json
{
  "success": true,
  "available": false,
  "hasAvailability": false,
  "message": "Ese día (25/12/2025) estamos cerrados...",
  "reason": "closed_day"
}
```

### Example 3: Too Far Future
```json
{
  "success": true,
  "available": false,
  "hasAvailability": false,
  "message": "Uy, esa fecha está muy lejos todavía...",
  "reason": "too_far_future",
  "daysUntil": 42
}
```

### Example 4: Simple Mode Success
```json
{
  "success": true,
  "available": true,
  "hasAvailability": true,
  "message": "El día 15/12/2025 está disponible para reservas 😊",
  "reason": "date_open",
  "simpleCheck": true,
  "isExplicitlyOpened": false
}
```

### Example 5: No Hours Available
```json
{
  "success": true,
  "available": false,
  "hasAvailability": false,
  "message": "Ay, lo siento 😔 Ese día...",
  "reason": "no_hours_available",
  "availableHours": []
}
```

### Example 6: Capacity Exceeded
```json
{
  "success": true,
  "available": false,
  "hasAvailability": false,
  "message": "Ay, qué pena 😔 Ese día...",
  "reason": "capacity_exceeded_new_date",
  "dailyLimit": 100,
  "currentTotal": 94
}
```

### Example 7: Current Time Available (Full Success)
```json
{
  "success": true,
  "available": true,
  "hasAvailability": true,
  "currentTimeAvailable": true,
  "message": "¡Perfecto! 😊 Hay disponibilidad para 4 personas el 15/12/2025 a las 14:00",
  "availableHours": [
    {"time": "14:00", "capacity": 50, "remaining": 20},
    {"time": "15:00", "capacity": 50, "remaining": 35}
  ]
}
```

### Example 8: Alternative Hours Suggested
```json
{
  "success": true,
  "available": true,
  "hasAvailability": true,
  "currentTimeAvailable": false,
  "message": "Esa hora (16:00) no está libre ese día 😔 Pero tengo disponible: 14:00 o 15:00. ¿Cuál te viene mejor?",
  "reason": "current_time_not_available",
  "availableHours": [
    {"time": "14:00", "capacity": 50, "remaining": 20},
    {"time": "15:00", "capacity": 50, "remaining": 35}
  ]
}
```

## External API Dependencies

### 1. fetch_closed_days.php
- **URL**: `https://alqueriavillacarmen.com/fetch_closed_days.php`
- **Method**: GET (file_get_contents)
- **Line**: 108
- **Expected Response**:
```json
{
  "closed_days": ["2025-12-15", "2025-12-16"],
  "opened_days": ["2025-12-02", "2025-12-03"]
}
```

### 2. gethourdata.php
- **URL**: `https://alqueriavillacarmen.com/api/gethourdata.php`
- **Method**: POST
- **Line**: 195
- **POST Body**:
```json
{
  "date": "2025-12-15",
  "party_size": 4
}
```
- **Expected Response**:
```json
{
  "available_hours": [
    {"time": "14:00", "capacity": 50, "remaining": 20},
    {"time": "15:00", "capacity": 50, "remaining": 35}
  ]
}
```

## Database Interactions (Full Mode with bookingId only)

### Table: daily_limits
- **Query Line**: 271
- **Purpose**: Get custom capacity limit for specific date
- **SQL**:
```sql
SELECT daily_limit FROM daily_limits WHERE date = ?
```
- **Default**: 100 (if no row exists)

### Table: bookings
- **Query Line**: 279
- **Purpose**: Calculate current bookings total for the date (excluding current booking)
- **SQL**:
```sql
SELECT SUM(party_size) as total
FROM bookings
WHERE reservation_date = ?
AND id != ?
AND status != 'cancelled'
```

## Key Business Rules Discovered

### Restaurant Operating Schedule
1. **Default Open Days**: Thursday, Friday, Saturday, Sunday (days 4, 5, 6, 7)
2. **Default Closed Days**: Monday, Tuesday, Wednesday (days 1, 2, 3)
3. **Override System**:
   - `opened_days` table = highest priority (can open on Mon/Tue/Wed)
   - `closed_days` table = medium priority (can close on Thu/Fri/Sat/Sun)
   - Day of week = lowest priority (default schedule)

### Booking Window
- **Maximum**: 35 days in advance
- **Minimum**: Not specified in this API (likely handled elsewhere)

### Special Holidays (Non-Modifiable)
- December 24, 25, 31
- January 1, 5, 6
- These dates have special menus and bookings cannot be modified

### Capacity Management
- **Default Daily Limit**: 100 people
- **Custom Limits**: Stored in `daily_limits` table per date
- **Calculation**: Excludes cancelled bookings and current booking (for modifications)

## Error Handling

### Missing newDate Parameter
- **Lines**: 19-25
- **Response**:
```json
{
  "success": false,
  "message": "Missing required parameter: newDate"
}
```

### gethourdata.php API Failure
- **Lines**: 213-220
- **Response**:
```json
{
  "success": false,
  "available": false,
  "message": "Error al obtener disponibilidad de horarios"
}
```

### General Exception Handling
- **Lines**: 344-350
- **Catches**: Any uncaught exceptions
- **Response**:
```json
{
  "success": false,
  "available": false,
  "message": "{exception message}"
}
```

## Database Connection
- **Required File**: `conectaVILLACARMEN.php` (line 8)
- **Connection Objects Used**:
  - `$mysqli` - Created inline for database queries (lines 267-292)
  - Does NOT use global `$conn` or `$pdo` from connection file
  - Creates fresh MySQLi connection for capacity validation

## Headers Set
```php
Content-Type: application/json
Access-Control-Allow-Origin: *
Access-Control-Allow-Methods: POST
Access-Control-Allow-Headers: Content-Type
```

## Issues Encountered
None. File was successfully read and comprehensively analyzed.

## Blockers
None.

## Context for Next Step
- **Next API to analyze**: `get_available_rice_types.php`
- **Pattern Observed**: This API uses external API calls (fetch_closed_days.php, gethourdata.php) rather than direct database queries for core logic
- **Response Pattern**: All responses are JSON with `success`, `available`, and `message` fields at minimum
- **Message Style**: Conversational Spanish with emojis (😊, 😔)
- **Date Format**: Internal = YYYY-MM-DD, Display = DD/MM/YYYY
- **Important for Integration**:
  - Simple mode check is useful for quick date validation in chatbot flow
  - Full mode provides complete validation with available time slots
  - `reason` codes are critical for handling different scenarios in chatbot logic
  - `availableHours` array can be used to present time options to user

## Verification
- [x] File location confirmed and read successfully (352 lines)
- [x] All input parameters documented with types and requirements
- [x] Both operation modes (Simple/Full) documented
- [x] All 7 validation layers documented in execution order
- [x] All response codes and reason codes documented
- [x] All success and error response formats documented
- [x] Helper functions analyzed and documented
- [x] External API dependencies identified
- [x] Database queries documented
- [x] Business rules extracted (operating schedule, booking window, special holidays)
- [x] Error handling patterns documented
- [x] Response message style observed (Spanish, conversational, emojis)

## Additional Technical Notes

### Code Quality Observations
1. **Well-Structured**: Clear separation of concerns with helper functions
2. **Comprehensive Validation**: 7 layers ensure data integrity
3. **User-Friendly Messages**: All responses in Spanish with helpful suggestions
4. **Flexible Design**: Two modes (simple/full) support different use cases
5. **Database Efficiency**: Only queries database when absolutely necessary (capacity check)
6. **Priority Logic**: Clear override hierarchy for date availability

### Integration Considerations for WhatsApp Bot
1. **Initial Flow**: Use simple mode to quickly validate date before asking party size
2. **Full Validation**: Use full mode once party size is known
3. **Time Selection**: Use `availableHours` array to present time options via UAZAPI menu
4. **Error Handling**: Map each `reason` code to appropriate chatbot response
5. **Modification Flow**: Pass `bookingId` when modifying existing booking to trigger capacity validation

### Potential Improvements Identified (Not Implemented)
- Could cache closed/opened days to reduce API calls
- Could validate date format before processing
- Could add minimum advance booking validation
