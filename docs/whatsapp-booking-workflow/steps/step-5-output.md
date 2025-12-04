# Step 5 Output: Analyze send_whatsapp_uazapi.php integration

## Status: COMPLETED

## Execution Summary
Successfully analyzed the UAZAPI WhatsApp integration file at `/home/jaime/Documents/projects/alqueriavillacarmen/includes/send_whatsapp_uazapi.php` (484 lines). This file provides complete WhatsApp messaging capabilities via the UAZAPI service, including text messages, button/menu messages, booking confirmations, reminders, and cancellation notifications. The integration demonstrates robust error handling with fallback mechanisms, credential management via .env file, Spanish phone number formatting (adding country code 34), and comprehensive logging for all operations.

## Files Read
- `/home/jaime/Documents/projects/alqueriavillacarmen/includes/send_whatsapp_uazapi.php` - UAZAPI WhatsApp integration (484 lines)
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/whatsapp-booking-workflow-steps.md` - Step requirements
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/progress.json` - Current progress
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/steps/step-4-output.md` - Prior context

## UAZAPI Integration Architecture

### Configuration (Lines 8-10)
```php
if (basename(__FILE__) == basename($_SERVER['SCRIPT_FILENAME'])) {
    header('Content-Type: application/json');
}
```
**Purpose**: Sets JSON content type only when file is called directly (not when included)
**Benefit**: Allows file to function both as standalone API and as library

### Error Configuration (Lines 2-5)
```php
error_reporting(E_ALL);
ini_set('display_errors', 0);
ini_set('log_errors', 1);
```
**Configuration**:
- Report all errors: `E_ALL`
- Hide from output: `display_errors = 0` (production-safe)
- Log to file: `log_errors = 1`

## Core Functions Documented

### 1. getUazApiCredentials() (Lines 15-37)

**Function Signature**:
```php
function getUazApiCredentials()
```

**Purpose**: Load UAZAPI credentials from .env file or environment variables

**Return Value**:
```php
[
    'api_url' => 'https://uazapi.example.com',
    'api_token' => 'abc123def456'
]
```

**Credential Loading Logic**:

1. **Determine Base Path** (Line 18):
   ```php
   $base_path = defined('BASE_PATH') ? BASE_PATH : dirname(dirname(__FILE__));
   ```
   - Uses `BASE_PATH` constant if defined
   - Otherwise: Parent directory of `includes/` folder

2. **Check for .env File** (Lines 19-22):
   ```php
   $env_file = $base_path . '/.env';
   if (!file_exists($env_file)) {
       error_log("WARNING: .env file not found at: " . $env_file);
       // Fall back to environment variables
   }
   ```

3. **Primary Source: .env File** (Lines 31-36):
   ```php
   $env_vars = parse_ini_file($env_file);
   return [
       'api_url' => $env_vars['UAZAPI_URL'] ?? '',
       'api_token' => $env_vars['UAZAPI_TOKEN'] ?? ''
   ];
   ```
   - Uses PHP's `parse_ini_file()` function
   - Parses `.env` as INI format
   - Uses null coalescing operator for missing keys

4. **Fallback Source: Environment Variables** (Lines 25-28):
   ```php
   return [
       'api_url' => getenv('UAZAPI_URL') ?: '',
       'api_token' => getenv('UAZAPI_TOKEN') ?: ''
   ];
   ```
   - Uses `getenv()` to read system environment variables
   - Returns empty string if not set

**Required Environment Variables**:
- `UAZAPI_URL`: Base URL of UAZAPI service (e.g., `https://uazapi.example.com`)
- `UAZAPI_TOKEN`: Authentication token for API access

**Error Handling**:
- Logs warning if .env file not found
- Returns empty strings if credentials missing (will cause API failures downstream)
- No exception thrown (graceful degradation)

---

### 2. sendUazApiWhatsApp() (Lines 46-119)

**Function Signature**:
```php
function sendUazApiWhatsApp($recipientNumber, $message)
```

**Purpose**: Core function to send simple text messages via UAZAPI

**Parameters**:
| Parameter | Type | Example | Description |
|-----------|------|---------|-------------|
| `$recipientNumber` | string | `"612345678"` | Recipient phone (9 digits, Spanish) |
| `$message` | string | `"Hello World"` | Text message to send |

**Return Value**:
```php
// Success
[
    'success' => true,
    'messageSid' => 'msg_abc123',
    'response' => [...] // Full UAZAPI response
]

// Failure
[
    'success' => false,
    'error' => 'Error description'
]
```

**Phone Number Processing** (Lines 53-55):
```php
// Step 1: Strip all non-numeric characters
$recipientNumber = preg_replace('/[^0-9]/', '', $recipientNumber);

// Step 2: Prepend Spanish country code
$recipientNumber = '34' . $recipientNumber;
```

**Input/Output Examples**:
| Input | After Regex | After Prefix | Final |
|-------|-------------|--------------|-------|
| `"612 345 678"` | `"612345678"` | `"34612345678"` | `34612345678` |
| `"612-345-678"` | `"612345678"` | `"34612345678"` | `34612345678` |
| `"(612) 345678"` | `"612345678"` | `"34612345678"` | `34612345678` |
| `"+34612345678"` | `"34612345678"` | `"3434612345678"` | `3434612345678` (DUPLICATE!) |

**WARNING**: If input already contains country code, it will be duplicated!
- Input: `"34612345678"` → Output: `"3434612345678"` (invalid)
- **Recommendation**: Always pass 9-digit Spanish numbers without country code

**API Endpoint** (Line 58):
```php
$endpoint = $apiUrl . '/send/text?token=' . urlencode($apiToken);
```

**Example**: `https://uazapi.example.com/send/text?token=abc123def456`

**Authentication Method**: Token passed as query parameter (not in headers)

**Request Payload** (Lines 61-64):
```php
$payload = json_encode([
    'number' => $recipientNumber,
    'text' => $message
]);
```

**Payload Structure**:
```json
{
  "number": "34612345678",
  "text": "Hello World"
}
```

**cURL Configuration** (Lines 70-85):
```php
$ch = curl_init($endpoint);
curl_setopt($ch, CURLOPT_POST, true);
curl_setopt($ch, CURLOPT_POSTFIELDS, $payload);
curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
curl_setopt($ch, CURLOPT_HTTPHEADER, [
    'Content-Type: application/json'
]);
curl_setopt($ch, CURLOPT_TIMEOUT, 30);

$response = curl_exec($ch);
$httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
$curlError = curl_error($ch);
curl_close($ch);
```

**cURL Settings**:
| Option | Value | Purpose |
|--------|-------|---------|
| `CURLOPT_POST` | `true` | Use POST method |
| `CURLOPT_POSTFIELDS` | JSON payload | Request body |
| `CURLOPT_RETURNTRANSFER` | `true` | Return response as string |
| `CURLOPT_HTTPHEADER` | `Content-Type: application/json` | JSON content type |
| `CURLOPT_TIMEOUT` | `30` seconds | Maximum execution time |

**Logging** (Lines 66-68, 87-88):
```php
error_log("UAZAPI Request - Endpoint: " . $endpoint);
error_log("UAZAPI Request - Payload: " . $payload);
// ... after execution
error_log("UAZAPI Response - HTTP Code: " . $httpCode);
error_log("UAZAPI Response - Body: " . $response);
```

**Error Handling**:

1. **cURL Error** (Lines 90-96):
   ```php
   if ($curlError) {
       error_log("UAZAPI cURL Error: " . $curlError);
       return [
           'success' => false,
           'error' => 'cURL Error: ' . $curlError
       ];
   }
   ```
   **Examples**: Connection timeout, DNS resolution failure, SSL errors

2. **HTTP Error** (Lines 98-103):
   ```php
   if ($httpCode != 200 && $httpCode != 201) {
       return [
           'success' => false,
           'error' => 'HTTP ' . $httpCode . ': ' . $response
       ];
   }
   ```
   **Accepted Codes**: 200 OK, 201 Created
   **Examples**: 400 Bad Request, 401 Unauthorized, 500 Server Error

3. **Exception Handling** (Lines 112-118):
   ```php
   catch (Exception $e) {
       error_log("UAZAPI Error: " . $e->getMessage());
       return [
           'success' => false,
           'error' => $e->getMessage()
       ];
   }
   ```

**Response Parsing** (Lines 105-111):
```php
$responseData = json_decode($response, true);

return [
    'success' => true,
    'messageSid' => $responseData['id'] ?? $responseData['data']['id'] ?? 'unknown',
    'response' => $responseData
];
```

**Message ID Extraction**:
- Primary: `$responseData['id']`
- Fallback: `$responseData['data']['id']`
- Default: `'unknown'`

**Note**: Supports multiple UAZAPI response formats

---

### 3. sendWhatsAppConfirmationUazApi() (Lines 125-154)

**Function Signature**:
```php
function sendWhatsAppConfirmationUazApi(
    $customerName,
    $bookingDate,
    $bookingTime,
    $guestCount,
    $preOrderDetails,
    $childrenMenuCount,
    $vegetarianOptionCount,
    $recipientNumber,
    $bookingId = ''
)
```

**Purpose**: Send simple text confirmation message (no buttons)

**Use Case**: Fallback when button messages fail

**Message Template** (Lines 137-150):
```
*Confirmación de Reserva - Alquería Villa Carmen*

Hola {customerName},

Gracias por elegir Alquería Villa Carmen. Su reserva ha sido confirmada con los siguientes detalles:

📅 *Fecha:* {bookingDate}
🕒 *Hora:* {bookingTime}
👥 *Personas:* {guestCount}
🍚 *Arroz:* {preOrderDetails}
👶 *Tronas:* {childrenMenuCount}
🛒 *Carritos:* {vegetarianOptionCount}

Si necesita modificar o cancelar su reserva, contáctenos:
☎️ Teléfono: 638 85 72 94
✉️ Email: reservas@alqueriavillacarmen.com

¡Esperamos darle la bienvenida pronto!

Alquería Villa Carmen
```

**Example Rendered Message**:
```
*Confirmación de Reserva - Alquería Villa Carmen*

Hola Juan García,

Gracias por elegir Alquería Villa Carmen. Su reserva ha sido confirmada con los siguientes detalles:

📅 *Fecha:* 25/12/2025
🕒 *Hora:* 14:00
👥 *Personas:* 4
🍚 *Arroz:* Paella Valenciana (2 raciones)
👶 *Tronas:* 2
🛒 *Carritos:* 1

Si necesita modificar o cancelar su reserva, contáctenos:
☎️ Teléfono: 638 85 72 94
✉️ Email: reservas@alqueriavillacarmen.com

¡Esperamos darle la bienvenida pronto!

Alquería Villa Carmen
```

**WhatsApp Formatting**:
- `*text*` = Bold text
- Emojis used for visual clarity
- No interactive buttons (plain text only)

**Implementation** (Line 153):
```php
return sendUazApiWhatsApp($recipientNumber, $message);
```
Delegates to core text sending function

---

### 4. sendWhatsAppConfirmationWithButtonsUazApi() (Lines 160-290)

**Function Signature**:
```php
function sendWhatsAppConfirmationWithButtonsUazApi(
    $customerName,
    $bookingDate,
    $bookingTime,
    $guestCount,
    $preOrderDetails,
    $childrenMenuCount,
    $vegetarianOptionCount,
    $recipientNumber,
    $bookingId = ''
)
```

**Purpose**: Send booking confirmation with interactive button/menu

**Enhanced Feature**: Clickable cancellation button

**Phone Number Processing** (Lines 176-178):
```php
$recipientNumber = preg_replace('/[^0-9]/', '', $recipientNumber);
$recipientNumber = '34' . $recipientNumber;
```
Same as `sendUazApiWhatsApp()` (adds country code 34)

**API Endpoint** (Line 181):
```php
$endpoint = $apiUrl . '/send/menu?token=' . urlencode($apiToken);
```

**Endpoint Type**: `/send/menu` (different from `/send/text`)
**Purpose**: Supports button/menu interactions

**Message Text** (Lines 184-193):
```php
$confirmationText = "*Confirmación de Reserva - Alquería Villa Carmen*\n\n";
$confirmationText .= "Hola {$customerName},\n\n";
$confirmationText .= "Gracias por elegir Alquería Villa Carmen. Su reserva ha sido confirmada:\n\n";
$confirmationText .= "📅 *Fecha:* {$bookingDate}\n";
$confirmationText .= "🕒 *Hora:* {$bookingTime}\n";
$confirmationText .= "👥 *Personas:* {$guestCount}\n";
$confirmationText .= "🍚 *Arroz:* {$preOrderDetails}\n";
$confirmationText .= "👶 *Tronas:* {$childrenMenuCount}\n";
$confirmationText .= "🛒 *Carritos:* {$vegetarianOptionCount}\n\n";
$confirmationText .= "Si desea cancelar su reserva puede hacerlo clickando en el botón de abajo:";
```

**Difference from Text Version**:
- Shorter intro text ("confirmada" vs "confirmada con los siguientes detalles")
- Ends with button instruction instead of contact info
- No phone/email in message (button provides action)

**Button Configuration** (Lines 195-201):
```php
$choices = [];

// Add cancellation link if booking ID is provided
if (!empty($bookingId)) {
    $choices[] = "Cancelar Reserva|https://alqueriavillacarmen.com/cancel_reservation.php?id={$bookingId}";
}
```

**UAZAPI Button Format**: `"Button Label|URL"`

**Components**:
- **Button Label**: `"Cancelar Reserva"` (displayed on button)
- **Separator**: `|` (pipe character)
- **URL**: `https://alqueriavillacarmen.com/cancel_reservation.php?id={bookingId}`

**Example**:
```php
$choices = ["Cancelar Reserva|https://alqueriavillacarmen.com/cancel_reservation.php?id=1984"];
```

**User Experience**:
1. Customer receives message with confirmation details
2. WhatsApp displays clickable button labeled "Cancelar Reserva"
3. Clicking button opens cancellation URL in browser
4. URL includes booking ID in query string

**Request Payload** (Lines 204-209):
```php
$payload = [
    'number' => $recipientNumber,
    'type' => 'button',
    'text' => $confirmationText,
    'choices' => $choices,
];
```

**Payload Structure**:
```json
{
  "number": "34612345678",
  "type": "button",
  "text": "*Confirmación de Reserva*...",
  "choices": [
    "Cancelar Reserva|https://alqueriavillacarmen.com/cancel_reservation.php?id=1984"
  ]
}
```

**cURL Request** (Lines 215-230):
```php
$ch = curl_init($endpoint);
curl_setopt($ch, CURLOPT_POST, true);
curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($payload));
curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
curl_setopt($ch, CURLOPT_HTTPHEADER, [
    'Content-Type: application/json'
]);
curl_setopt($ch, CURLOPT_TIMEOUT, 30);

$response = curl_exec($ch);
$httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
$curlError = curl_error($ch);
curl_close($ch);
```

**Configuration**: Same as `sendUazApiWhatsApp()` (30s timeout, JSON content type)

**Fallback Strategy**:

**1. cURL Error Fallback** (Lines 235-249):
```php
if ($curlError) {
    error_log("UAZAPI Button cURL Error: " . $curlError);
    // Fall back to simple text message
    return sendWhatsAppConfirmationUazApi(
        $customerName,
        $bookingDate,
        $bookingTime,
        $guestCount,
        $preOrderDetails,
        $childrenMenuCount,
        $vegetarianOptionCount,
        $recipientNumber,
        $bookingId
    );
}
```
**Trigger**: Network errors, connection failures
**Action**: Send text-only confirmation (no button)

**2. HTTP Error Fallback** (Lines 251-265):
```php
if ($httpCode != 200 && $httpCode != 201) {
    error_log("UAZAPI Button failed with HTTP " . $httpCode . ", falling back to text message");
    // Fall back to simple text message
    return sendWhatsAppConfirmationUazApi(...);
}
```
**Trigger**: API errors (400, 401, 500, etc.)
**Action**: Send text-only confirmation

**3. Exception Fallback** (Lines 275-289):
```php
catch (Exception $e) {
    error_log("UAZAPI Button Error: " . $e->getMessage());
    // Fall back to simple text message
    return sendWhatsAppConfirmationUazApi(...);
}
```
**Trigger**: Any unexpected error
**Action**: Send text-only confirmation

**Fallback Guarantee**: Customer ALWAYS receives confirmation message
- Best effort: Interactive button message
- Fallback: Text message with contact information

**Success Response** (Lines 267-274):
```php
$responseData = json_decode($response, true);

return [
    'success' => true,
    'messageSid' => $responseData['id'] ?? $responseData['data']['id'] ?? 'unknown',
    'response' => $responseData,
    'type' => 'button_confirmation'
];
```

**Extra Field**: `'type' => 'button_confirmation'` distinguishes from text-only responses

---

### 5. sendBookingWhatsAppUazApi() (Lines 295-344)

**Function Signature**:
```php
function sendBookingWhatsAppUazApi($bookingData)
```

**Purpose**: Orchestrator function for sending booking confirmations

**Called By**: `insert_booking.php` after successful database insertion (Step 4)

**Input Parameter**: `$bookingData` array

**Expected Structure**:
```php
[
    'customer_name' => 'Juan García',
    'reservation_date' => '2025-12-25',  // YYYY-MM-DD
    'reservation_time' => '14:00:00',
    'party_size' => 4,
    'arroz_type' => 'Paella Valenciana',
    'arroz_servings' => 2,
    'toggleArroz' => 'true',
    'high_chairs' => 2,
    'baby_strollers' => 1,
    'booking_id' => 1984,
    'contact_phone' => '612345678'
]
```

**Data Extraction** (Lines 297-322):

**1. Customer Name** (Line 297):
```php
$customerName = isset($bookingData['customer_name']) ? $bookingData['customer_name'] : '';
```

**2. Date Formatting** (Lines 299-304):
```php
$bookingDate = isset($bookingData['reservation_date']) ? $bookingData['reservation_date'] : '';
if (!empty($bookingDate)) {
    $date = new DateTime($bookingDate);
    $bookingDate = $date->format('d/m/Y');
}
```

**Date Transformation**:
- **Input**: `2025-12-25` (YYYY-MM-DD from database)
- **Output**: `25/12/2025` (DD/MM/YYYY for customer)

**3. Time and Party Size** (Lines 306-307):
```php
$bookingTime = isset($bookingData['reservation_time']) ? $bookingData['reservation_time'] : '';
$guestCount = isset($bookingData['party_size']) ? $bookingData['party_size'] : '';
```

**4. Rice Details Processing** (Lines 309-318):
```php
$arrozType = isset($bookingData['arroz_type']) ? $bookingData['arroz_type'] : '';
$arrozServings = isset($bookingData['arroz_servings']) ? $bookingData['arroz_servings'] : '0';
$toggleArroz = isset($bookingData['toggleArroz']) ? $bookingData['toggleArroz'] : 'false';

if ($toggleArroz === 'true' && !empty($arrozType) && !empty($arrozServings)) {
    $preOrderDetails = "$arrozType ($arrozServings raciones)";
} else {
    $preOrderDetails = "No Arroz";
}
```

**Rice Display Logic**:
| Condition | Display |
|-----------|---------|
| `toggleArroz === 'true'` AND `arrozType` not empty AND `arrozServings` not empty | `"Paella Valenciana (2 raciones)"` |
| Any other case | `"No Arroz"` |

**Examples**:
- Input: `toggleArroz='true'`, `arrozType='Paella'`, `arrozServings=3` → Output: `"Paella (3 raciones)"`
- Input: `toggleArroz='false'` → Output: `"No Arroz"`
- Input: `toggleArroz='true'`, `arrozType=''` → Output: `"No Arroz"`

**5. Other Details** (Lines 320-322):
```php
$highChairs = isset($bookingData['high_chairs']) ? $bookingData['high_chairs'] : '0';
$babyStrollers = isset($bookingData['baby_strollers']) ? $bookingData['baby_strollers'] : '0';
$bookingId = isset($bookingData['booking_id']) ? $bookingData['booking_id'] : '';
```

**6. Phone Number** (Line 325):
```php
$customerPhone = isset($bookingData['contact_phone']) ? $bookingData['contact_phone'] : '';
```

**Send Message** (Lines 327-341):
```php
if (!empty($customerPhone)) {
    $result = sendWhatsAppConfirmationWithButtonsUazApi(
        $customerName,
        $bookingDate,
        $bookingTime,
        $guestCount,
        $preOrderDetails,
        $highChairs,
        $babyStrollers,
        $customerPhone,
        $bookingId
    );
    error_log("WhatsApp to customer result: " . json_encode($result));
    return $result;
}

return ['success' => false, 'error' => 'No customer phone number provided'];
```

**Logic**:
1. Check if phone number provided
2. If yes: Send confirmation with buttons
3. Log result
4. Return result to caller
5. If no phone: Return error

**Note**: Empty phone returns error but doesn't throw exception (allows booking to complete)

---

### 6. sendWhatsAppReminderConfirmationUazApi() (Lines 349-369)

**Function Signature**:
```php
function sendWhatsAppReminderConfirmationUazApi(
    $customerName,
    $bookingDate,
    $bookingTime,
    $bookingId,
    $recipientNumber
)
```

**Purpose**: Send reservation reminder (day before booking)

**Use Case**: Automated reminder system (likely cron job)

**Message Template** (Lines 356-366):
```
🔔 *Recordatorio de Reserva* 🔔

Hola *{customerName}*,

Te recordamos tu reserva para mañana:
📅 *Fecha:* {bookingDate}
⏰ *Hora:* {bookingTime}

¡Nos vemos pronto! 😊

Si necesitas cancelar:
https://alqueriavillacarmen.com/cancel?id={bookingId}
```

**Example**:
```
🔔 *Recordatorio de Reserva* 🔔

Hola *Juan García*,

Te recordamos tu reserva para mañana:
📅 *Fecha:* 25/12/2025
⏰ *Hora:* 14:00

¡Nos vemos pronto! 😊

Si necesitas cancelar:
https://alqueriavillacarmen.com/cancel?id=1984
```

**Cancellation Link** (Lines 363-366):
```php
if (!empty($bookingId)) {
    $message .= "Si necesitas cancelar:\n";
    $message .= "https://alqueriavillacarmen.com/cancel?id={$bookingId}";
}
```

**Note**: Plain URL (not button), works in any WhatsApp client

**Implementation** (Line 368):
```php
return sendUazApiWhatsApp($recipientNumber, $message);
```

**Simplified Design**: Text-only (no buttons/menus)

---

### 7. sendWhatsAppCancellationNotificationUazApi() (Lines 374-441)

**Function Signature**:
```php
function sendWhatsAppCancellationNotificationUazApi($bookingData)
```

**Purpose**: Notify restaurant staff when customer cancels booking

**Target Audience**: Restaurant staff (not customer)

**Hardcoded Phone Numbers** (Line 379):
```php
$phoneNumbers = ["34692747052", "34638857294", "34686969914"];
```

**Recipients**:
1. `34692747052` (Spanish format: 692 74 70 52)
2. `34638857294` (Spanish format: 638 85 72 94)
3. `34686969914` (Spanish format: 686 96 99 14)

**Note**: These numbers are the same as mentioned in Step 4 for staff notifications

**Data Extraction** (Lines 383-410):

**1. Customer Name** (Line 383):
```php
$customerName = isset($bookingData['customer_name']) ? trim($bookingData['customer_name']) : 'Cliente';
```
**Default**: `'Cliente'` if name missing

**2. Date Formatting with Error Handling** (Lines 385-393):
```php
$bookingDate = isset($bookingData['reservation_date']) ? trim($bookingData['reservation_date']) : '';
if (!empty($bookingDate)) {
    try {
        $date = new DateTime($bookingDate);
        $bookingDate = $date->format('d/m/Y');
    } catch (Exception $e) {
        error_log("Date formatting error: " . $e->getMessage());
    }
}
```
**Robust**: Catches date parsing errors, logs them

**3. Time Formatting** (Lines 395-399):
```php
$bookingTime = isset($bookingData['reservation_time']) ? trim($bookingData['reservation_time']) : '';
if (!empty($bookingTime)) {
    $timeArr = explode(':', $bookingTime);
    $bookingTime = $timeArr[0] . ':' . $timeArr[1];
}
```

**Time Transformation**:
- **Input**: `14:00:00` (HH:MM:SS from database)
- **Output**: `14:00` (HH:MM for readability)

**4. Party Size** (Line 401):
```php
$partySize = isset($bookingData['party_size']) ? trim((string)$bookingData['party_size']) : '0';
```
**Casting**: Integer to string for display

**5. Contact Phone** (Line 402):
```php
$contactPhone = isset($bookingData['contact_phone']) ? trim($bookingData['contact_phone']) : 'No disponible';
```
**Default**: `'No disponible'` if missing

**6. Rice Type** (Lines 404-406):
```php
$arrozType = isset($bookingData['arroz_type']) && !empty(trim($bookingData['arroz_type']))
    ? trim($bookingData['arroz_type'])
    : 'Sin arroz';
```
**Default**: `'Sin arroz'` if missing or empty

**7. Rice Servings** (Lines 408-410):
```php
$arrozServings = isset($bookingData['arroz_servings']) && !empty(trim($bookingData['arroz_servings'])) && $bookingData['arroz_servings'] !== '0'
    ? trim((string)$bookingData['arroz_servings']) . ' raciones'
    : 'Sin raciones';
```
**Default**: `'Sin raciones'` if missing, empty, or zero

**Message Template** (Lines 412-420):
```php
$message = "❌ *Reserva Cancelada* ❌\n\n";
$message .= "Cliente: *{$customerName}*\n";
$message .= "📅 Fecha: {$bookingDate}\n";
$message .= "⏰ Hora: {$bookingTime}\n";
$message .= "👥 Personas: {$partySize}\n";
$message .= "📞 Teléfono: {$contactPhone}\n";
$message .= "🍚 Arroz: {$arrozType}\n";
$message .= "📦 Raciones: {$arrozServings}";
```

**Example**:
```
❌ *Reserva Cancelada* ❌

Cliente: *Juan García*
📅 Fecha: 25/12/2025
⏰ Hora: 14:00
👥 Personas: 4
📞 Teléfono: 612345678
🍚 Arroz: Paella Valenciana
📦 Raciones: 2 raciones
```

**Multi-Recipient Sending** (Lines 422-426):
```php
foreach ($phoneNumbers as $phoneNumber) {
    error_log("Sending cancellation notification to: {$phoneNumber}");
    $result = sendUazApiWhatsApp($phoneNumber, $message);
    $results[$phoneNumber] = $result;
}
```

**Behavior**:
- Sends to all 3 staff numbers
- Logs each attempt
- Stores individual results
- **Continues even if some fail** (no break on error)

**Success Determination** (Lines 428-435):
```php
$overallSuccess = false;
foreach ($results as $result) {
    if ($result['success']) {
        $overallSuccess = true;
        break;
    }
}
```

**Logic**: Returns success if **at least ONE** message succeeds

**Return Value** (Lines 437-440):
```php
return [
    'success' => $overallSuccess,
    'results' => $results
];
```

**Structure**:
```php
[
    'success' => true,  // At least one succeeded
    'results' => [
        '34692747052' => ['success' => true, 'messageSid' => 'msg_123'],
        '34638857294' => ['success' => false, 'error' => 'HTTP 400'],
        '34686969914' => ['success' => true, 'messageSid' => 'msg_456']
    ]
]
```

**Reliability**: Even if 2 out of 3 fail, overall success is `true`

---

## Direct API Call Support (Lines 444-484)

**Purpose**: Allow file to be called directly as API endpoint

**Detection** (Line 444):
```php
if (basename(__FILE__) == basename($_SERVER['SCRIPT_FILENAME'])) {
```

**Condition**: File is accessed directly (not included via `require()`)

**HTTP Methods Supported**: POST and GET (Lines 448-468)

**POST Parameters** (Lines 449-457):
```php
if ($requestMethod === 'POST') {
    $customerName = isset($_POST['customerName']) ? $_POST['customerName'] : '';
    $bookingDate = isset($_POST['bookingDate']) ? $_POST['bookingDate'] : '';
    $bookingTime = isset($_POST['bookingTime']) ? $_POST['bookingTime'] : '';
    $guestCount = isset($_POST['guestCount']) ? $_POST['guestCount'] : '';
    $preOrderDetails = isset($_POST['preOrderDetails']) ? $_POST['preOrderDetails'] : '';
    $childrenMenuCount = isset($_POST['childrenMenuCount']) ? $_POST['childrenMenuCount'] : '';
    $vegetarianOptionCount = isset($_POST['vegetarianOptionCount']) ? $_POST['vegetarianOptionCount'] : '';
    $recipientNumber = isset($_POST['recipientNumber']) ? $_POST['recipientNumber'] : '';
}
```

**GET Parameters** (Lines 459-467):
```php
else {
    $customerName = isset($_GET['customerName']) ? $_GET['customerName'] : '';
    // ... same parameters from query string
}
```

**Function Call** (Lines 470-480):
```php
error_log("Calling sendWhatsAppConfirmationUazApi function");
$result = sendWhatsAppConfirmationUazApi(
    $customerName,
    $bookingDate,
    $bookingTime,
    $guestCount,
    $preOrderDetails,
    $childrenMenuCount,
    $vegetarianOptionCount,
    $recipientNumber
);

error_log("Result: " . json_encode($result));
echo json_encode($result);
```

**Usage Example**:
```bash
# POST request
curl -X POST https://alqueriavillacarmen.com/includes/send_whatsapp_uazapi.php \
  -d "customerName=Juan García" \
  -d "bookingDate=25/12/2025" \
  -d "bookingTime=14:00" \
  -d "guestCount=4" \
  -d "preOrderDetails=Paella (2 raciones)" \
  -d "childrenMenuCount=2" \
  -d "vegetarianOptionCount=1" \
  -d "recipientNumber=612345678"

# GET request
curl "https://alqueriavillacarmen.com/includes/send_whatsapp_uazapi.php?customerName=Juan&bookingDate=25/12/2025&..."
```

**Response**: JSON encoded result from `sendWhatsAppConfirmationUazApi()`

---

## UAZAPI Endpoint Formats

### 1. Text Message Endpoint
```
POST {UAZAPI_URL}/send/text?token={UAZAPI_TOKEN}
```

**Payload**:
```json
{
  "number": "34612345678",
  "text": "Message text"
}
```

**Used By**:
- `sendUazApiWhatsApp()` - Core text function
- `sendWhatsAppConfirmationUazApi()` - Simple confirmation
- `sendWhatsAppReminderConfirmationUazApi()` - Reminders
- `sendWhatsAppCancellationNotificationUazApi()` - Staff notifications

---

### 2. Button/Menu Message Endpoint
```
POST {UAZAPI_URL}/send/menu?token={UAZAPI_TOKEN}
```

**Payload**:
```json
{
  "number": "34612345678",
  "type": "button",
  "text": "Message text",
  "choices": [
    "Button Label|https://url.com/action?param=value"
  ]
}
```

**Used By**:
- `sendWhatsAppConfirmationWithButtonsUazApi()` - Booking confirmation with cancellation button

**Button Format**: `"Label|URL"` (pipe-separated)

---

## Phone Number Formatting

### Processing Pattern (Used in 2 Functions)

**Functions**:
1. `sendUazApiWhatsApp()` (Lines 54-55)
2. `sendWhatsAppConfirmationWithButtonsUazApi()` (Lines 177-178)

**Code**:
```php
$recipientNumber = preg_replace('/[^0-9]/', '', $recipientNumber);
$recipientNumber = '34' . $recipientNumber;
```

### Step-by-Step Transformation

**Step 1: Strip Non-Numeric Characters**
```php
preg_replace('/[^0-9]/', '', $recipientNumber)
```

| Input | Output |
|-------|--------|
| `"612 345 678"` | `"612345678"` |
| `"612-345-678"` | `"612345678"` |
| `"(612) 345 678"` | `"612345678"` |
| `"+34 612 345 678"` | `"34612345678"` |
| `"612.345.678"` | `"612345678"` |

**Removes**: Spaces, hyphens, parentheses, plus signs, dots, etc.

**Step 2: Prepend Spanish Country Code**
```php
$recipientNumber = '34' . $recipientNumber;
```

| Input (after Step 1) | Output |
|----------------------|--------|
| `"612345678"` | `"34612345678"` ✓ Correct |
| `"34612345678"` | `"3434612345678"` ✗ DUPLICATE! |
| `"612 345 678"` | Not possible (Step 1 removes spaces first) |

### Important Notes

**Best Practice**: Always provide 9-digit Spanish numbers without country code
- ✓ Correct input: `"612345678"`, `"612 345 678"`, `"612-345-678"`
- ✗ Problematic: `"34612345678"`, `"+34612345678"` (will duplicate country code)

**Expected Input Format**:
- 9 digits (Spanish mobile/landline format)
- May include formatting characters (spaces, hyphens)
- Should NOT include country code

**Output Format**:
- Always: `34` + 9 digits = 11 digits total
- Example: `34612345678`

**Phone Number Types**:
- Spanish mobile: `6XX XXX XXX` → `346XXXXXXXX`
- Spanish landline: `9XX XXX XXX` → `349XXXXXXXX`

---

## Error Handling Patterns

### 1. Credential Loading (getUazApiCredentials)

**Pattern**: Graceful degradation with logging

```php
if (!file_exists($env_file)) {
    error_log("WARNING: .env file not found at: " . $env_file);
    // Fall back to environment variables
}
```

**Behavior**:
- Logs warning but doesn't fail
- Returns empty strings if credentials missing
- API calls will fail downstream (but won't crash PHP)

---

### 2. API Request Errors (sendUazApiWhatsApp)

**Pattern**: Try-catch with error return

```php
try {
    // API request logic
} catch (Exception $e) {
    error_log("UAZAPI Error: " . $e->getMessage());
    return [
        'success' => false,
        'error' => $e->getMessage()
    ];
}
```

**Error Types Handled**:
1. **cURL Errors**: Connection timeout, DNS failure, SSL errors
2. **HTTP Errors**: Non-200/201 status codes
3. **Exceptions**: Any unexpected errors

**Return Format**:
```php
['success' => false, 'error' => 'Error description']
```

---

### 3. Fallback Strategy (sendWhatsAppConfirmationWithButtonsUazApi)

**Pattern**: Graceful degradation from advanced to basic

```php
if ($curlError || $httpCode != 200/201) {
    error_log("UAZAPI Button failed, falling back to text message");
    return sendWhatsAppConfirmationUazApi(...);
}
```

**Fallback Chain**:
1. **Try**: Send button/menu message
2. **If fails**: Send simple text message
3. **Guarantee**: Customer receives confirmation

**Reliability**: 100% message delivery (assuming text endpoint works)

---

### 4. Multi-Recipient Resilience (sendWhatsAppCancellationNotificationUazApi)

**Pattern**: Continue-on-error with partial success

```php
foreach ($phoneNumbers as $phoneNumber) {
    $result = sendUazApiWhatsApp($phoneNumber, $message);
    $results[$phoneNumber] = $result;
    // No break - continues even if this fails
}

// Success if at least one succeeded
$overallSuccess = false;
foreach ($results as $result) {
    if ($result['success']) {
        $overallSuccess = true;
        break;
    }
}
```

**Behavior**:
- Attempts all recipients
- Doesn't stop on first failure
- Returns success if any succeed
- Provides detailed results per recipient

---

### 5. Logging Strategy

**Pattern**: Comprehensive logging at all stages

**Request Logging**:
```php
error_log("UAZAPI Request - Endpoint: " . $endpoint);
error_log("UAZAPI Request - Payload: " . $payload);
```

**Response Logging**:
```php
error_log("UAZAPI Response - HTTP Code: " . $httpCode);
error_log("UAZAPI Response - Body: " . $response);
```

**Error Logging**:
```php
error_log("UAZAPI cURL Error: " . $curlError);
error_log("UAZAPI Button Error: " . $e->getMessage());
```

**Result Logging**:
```php
error_log("WhatsApp to customer result: " . json_encode($result));
error_log("Sending cancellation notification to: {$phoneNumber}");
```

**Benefit**: Complete audit trail for debugging

---

## Integration Points Summary

### 1. Called by insert_booking.php (Step 4)

**Function Used**: `sendBookingWhatsAppUazApi($bookingData)`

**Flow**:
```
insert_booking.php
  ├─ Database INSERT
  ├─ COMMIT transaction
  └─ sendBookingWhatsAppUazApi($bookingData)
       └─ sendWhatsAppConfirmationWithButtonsUazApi()
            ├─ Try: Button message
            └─ Fallback: Text message
```

**Data Flow**:
```php
// insert_booking.php prepares:
$bookingData = [
    'booking_id' => $bookingId,
    'reservation_date' => $reservation_date,
    'party_size' => $party_size,
    'reservation_time' => $reservation_time,
    'customer_name' => $customer_name,
    'contact_phone' => $contact_phone,
    'commentary' => $commentary,
    'arroz_type' => $arroz_type,
    'arroz_servings' => $arroz_servings,
    'baby_strollers' => $baby_strollers,
    'high_chairs' => $high_chairs,
    'contact_email' => $contact_email,
    'toggleArroz' => $toggleArroz
];

// Calls:
$whatsAppResult = sendBookingWhatsAppUazApi($bookingData);
```

---

### 2. Standalone API Access

**Direct URL**: `https://alqueriavillacarmen.com/includes/send_whatsapp_uazapi.php`

**Methods**: POST or GET

**Parameters**:
- `customerName`
- `bookingDate`
- `bookingTime`
- `guestCount`
- `preOrderDetails`
- `childrenMenuCount`
- `vegetarianOptionCount`
- `recipientNumber`

**Response**: JSON result

---

### 3. Reminder System (Future/Cron)

**Function**: `sendWhatsAppReminderConfirmationUazApi()`

**Likely Usage**: Automated cron job that:
1. Queries bookings for tomorrow
2. Sends reminder to each customer
3. Logs results

**Not Yet Analyzed**: Reminder scheduling script

---

### 4. Cancellation System

**Function**: `sendWhatsAppCancellationNotificationUazApi()`

**Called By**: `cancel_reservation.php` (referenced but not analyzed)

**Recipients**: Restaurant staff (3 hardcoded numbers)

---

## Code Quality Assessment

### Strengths

1. **Comprehensive Error Handling**: Try-catch blocks, fallbacks, logging
2. **Graceful Degradation**: Button → Text fallback ensures delivery
3. **Flexible Configuration**: .env file with environment variable fallback
4. **Detailed Logging**: Complete audit trail for debugging
5. **Dual-Use Design**: Works as library and standalone API
6. **Phone Formatting**: Automatic Spanish country code handling
7. **Multi-Recipient Resilience**: Continues sending even if some fail
8. **Separation of Concerns**: Distinct functions for each message type
9. **WhatsApp Formatting**: Uses bold, emojis for clarity
10. **Date/Time Formatting**: Converts database format to user-friendly display

---

### Weaknesses

1. **Country Code Duplication Risk**: Prepends `34` without checking if already present
2. **Hardcoded Phone Numbers**: Staff numbers embedded in code (lines 379)
3. **Hardcoded Contact Info**: Phone/email in message template (lines 147-148)
4. **No Input Validation**: Doesn't validate phone number format before sending
5. **No Rate Limiting**: Could send unlimited messages
6. **Empty Credential Handling**: Returns empty strings instead of throwing error
7. **Message SID Fallback**: Uses `'unknown'` if ID not in expected format
8. **No Retry Logic**: Single attempt per message (no exponential backoff)
9. **Mixed Responsibilities**: Direct call handling mixed with library functions
10. **No Message Queuing**: Synchronous sending (blocking)

---

### Potential Improvements

1. **Smart Country Code Handling**:
   ```php
   if (!preg_match('/^34/', $recipientNumber)) {
       $recipientNumber = '34' . $recipientNumber;
   }
   ```

2. **Configuration Externalization**:
   - Move staff phone numbers to .env
   - Move contact info to .env
   - Load from database configuration table

3. **Input Validation**:
   ```php
   if (!preg_match('/^[0-9]{9}$/', $recipientNumber)) {
       throw new Exception('Invalid Spanish phone format');
   }
   ```

4. **Retry Logic**:
   ```php
   $maxRetries = 3;
   for ($i = 0; $i < $maxRetries; $i++) {
       $result = sendUazApiWhatsApp(...);
       if ($result['success']) break;
       sleep(pow(2, $i)); // Exponential backoff
   }
   ```

5. **Message Queue**: Use background job system (Redis Queue, RabbitMQ)

6. **Credential Validation**:
   ```php
   if (empty($apiUrl) || empty($apiToken)) {
       throw new Exception('UAZAPI credentials not configured');
   }
   ```

7. **Separate Direct Call Handler**: Move lines 444-484 to separate API file

---

## Business Logic Insights

### Customer Communication Strategy

1. **Immediate Confirmation**: Sent right after booking creation
2. **Interactive Elements**: Cancellation button for self-service
3. **Reminder System**: Day-before reminder (automated)
4. **Multi-Channel Fallback**: Button → Text ensures delivery

---

### Staff Notification Strategy

1. **Cancellation Alerts**: Immediate notification to 3 staff members
2. **Redundancy**: Message succeeds if any 1 of 3 succeeds
3. **Complete Info**: All booking details in cancellation message
4. **No New Booking Alerts**: Only cancellations notified via WhatsApp

**Question**: How are staff notified of NEW bookings?
- Possibly: Email, SMS, different WhatsApp function, or admin dashboard

---

### WhatsApp Message Types

| Type | Function | Recipient | Interactive | Fallback |
|------|----------|-----------|-------------|----------|
| Booking Confirmation | `sendWhatsAppConfirmationWithButtonsUazApi()` | Customer | Yes (button) | Text version |
| Simple Confirmation | `sendWhatsAppConfirmationUazApi()` | Customer | No | N/A |
| Reminder | `sendWhatsAppReminderConfirmationUazApi()` | Customer | No | N/A |
| Cancellation Alert | `sendWhatsAppCancellationNotificationUazApi()` | Staff (3) | No | N/A |

---

## Context for Next Step

### What We've Learned (Steps 1-5)

1. **Step 1**: Located all API files in project
2. **Step 2**: Analyzed date availability with 7 validation layers
3. **Step 3**: Analyzed rice type retrieval from FINDE table
4. **Step 4**: Analyzed booking insertion with WhatsApp integration
5. **Step 5**: Analyzed complete UAZAPI WhatsApp integration (THIS STEP)

---

### Complete WhatsApp Notification Flow

```
Customer Makes Booking
        ↓
insert_booking.php
        ↓
Database INSERT + COMMIT
        ↓
sendBookingWhatsAppUazApi($bookingData)
        ↓
sendWhatsAppConfirmationWithButtonsUazApi()
        ↓
    ┌───────────────────┐
    │ Try: Button Msg   │
    │ /send/menu        │
    └───────┬───────────┘
            │
        Success? ───Yes──→ Return success + messageSid
            │
           No
            ↓
    ┌───────────────────┐
    │ Fallback: Text    │
    │ /send/text        │
    └───────┬───────────┘
            │
            ↓
    Return result to insert_booking.php
            ↓
    Customer receives WhatsApp confirmation
    (with cancellation button if successful)
```

---

### Restaurant Staff Notification Flow

```
Customer Cancels Booking
        ↓
cancel_reservation.php (not yet analyzed)
        ↓
sendWhatsAppCancellationNotificationUazApi($bookingData)
        ↓
    ┌──────────────────────────┐
    │ Send to 34692747052      │ ──→ Log result
    ├──────────────────────────┤
    │ Send to 34638857294      │ ──→ Log result
    ├──────────────────────────┤
    │ Send to 34686969914      │ ──→ Log result
    └──────────────────────────┘
            ↓
    Return success if ANY succeeded
```

---

### Key Integration Requirements for WhatsApp Bot

**C# BotGenerator Integration Needs**:

1. **UAZAPI Credentials**:
   - Add to appsettings.json: `UAZAPI_URL` and `UAZAPI_TOKEN`
   - Create C# HTTP client for UAZAPI

2. **Phone Number Formatting**:
   ```csharp
   public static string FormatSpanishPhone(string input)
   {
       // Remove non-digits
       string digits = Regex.Replace(input, @"[^0-9]", "");

       // Check if already has country code
       if (!digits.StartsWith("34"))
       {
           digits = "34" + digits;
       }

       return digits;
   }
   ```

3. **Message Sending Functions**:
   ```csharp
   Task<UazApiResponse> SendTextMessage(string recipientNumber, string message);
   Task<UazApiResponse> SendButtonMessage(string recipientNumber, string text, List<ButtonChoice> choices);
   Task<UazApiResponse> SendBookingConfirmation(BookingData booking);
   ```

4. **Endpoint Integration**:
   - Text: `POST {UAZAPI_URL}/send/text?token={token}`
   - Button: `POST {UAZAPI_URL}/send/menu?token={token}`

5. **Error Handling**:
   - Implement fallback from button to text
   - Log all requests/responses
   - Handle HTTP errors gracefully

6. **Message Templates**:
   - Create C# string interpolation templates matching PHP versions
   - Use same emojis and formatting
   - Include cancellation links when booking_id available

---

### Outstanding Questions

1. **New Booking Staff Notifications**: How are staff notified of new bookings? (Email? Different WhatsApp function?)
2. **Reminder Automation**: Where is the cron job/scheduler that sends reminders?
3. **Cancellation API**: What is `cancel_reservation.php` structure?
4. **Message Status**: Does UAZAPI provide delivery/read receipts?
5. **Phone Validation**: Should bot validate Spanish phone format before accepting?
6. **Message Rate Limits**: Does UAZAPI have rate limiting we need to respect?

---

### Next Steps Likely To Analyze

Based on progress, next steps likely involve:
- Analyzing cancellation endpoint (`cancel_reservation.php`)
- Understanding time slot configuration (`gethourdata.php`)
- Exploring closed days management (`fetch_closed_days.php`)
- Investigating reminder scheduling system
- Building C# integration layer

---

## Issues Encountered

None. File successfully read and fully analyzed.

---

## Blockers

None.

---

## Verification

- [x] File successfully read: `/home/jaime/Documents/projects/alqueriavillacarmen/includes/send_whatsapp_uazapi.php` (484 lines)
- [x] `getUazApiCredentials()` function documented (lines 15-37)
- [x] `sendUazApiWhatsApp()` function documented (lines 46-119) - Core text sending
- [x] `sendWhatsAppConfirmationUazApi()` function documented (lines 125-154) - Simple text confirmation
- [x] `sendWhatsAppConfirmationWithButtonsUazApi()` function documented (lines 160-290) - Button message confirmation
- [x] `sendBookingWhatsAppUazApi()` function documented (lines 295-344) - Main orchestrator
- [x] `sendWhatsAppReminderConfirmationUazApi()` function documented (lines 349-369) - Reminder messages
- [x] `sendWhatsAppCancellationNotificationUazApi()` function documented (lines 374-441) - Staff notifications
- [x] Direct call handling documented (lines 444-484)
- [x] UAZAPI endpoint formats documented (`/send/text` and `/send/menu`)
- [x] Phone number formatting logic documented (adds country code 34)
- [x] Phone number transformation examples provided
- [x] Error handling patterns documented (5 distinct patterns)
- [x] Fallback strategies documented (button → text)
- [x] Logging strategy documented (request/response/error logging)
- [x] Message templates documented with examples
- [x] Button/menu format documented (`Label|URL`)
- [x] Multi-recipient logic documented (staff notifications)
- [x] Date/time formatting transformations documented
- [x] Rice display logic documented
- [x] All 7 functions with complete signatures
- [x] Integration points with insert_booking.php documented
- [x] Code quality assessment completed
- [x] Business logic insights extracted
- [x] WhatsApp bot integration requirements identified
- [x] No issues or blockers encountered
