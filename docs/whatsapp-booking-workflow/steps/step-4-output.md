# Step 4 Output: Analyze insert_booking.php API structure

## Status: COMPLETED

## Execution Summary
Successfully analyzed the `insert_booking.php` API located at `/home/jaime/Documents/projects/alqueriavillacarmen/insert_booking.php` and its associated WhatsApp notification system in `/home/jaime/Documents/projects/alqueriavillacarmen/includes/send_whatsapp_uazapi.php`. This is the core booking insertion endpoint that accepts all reservation details via POST, inserts them into the database within a transaction, and triggers WhatsApp confirmations to both the customer and restaurant staff. The API demonstrates robust error handling with transaction rollback capabilities and integrates with the UAZAPI WhatsApp service for notifications.

## Files Read
- `/home/jaime/Documents/projects/alqueriavillacarmen/insert_booking.php` - Booking insertion API (110 lines)
- `/home/jaime/Documents/projects/alqueriavillacarmen/includes/send_whatsapp_uazapi.php` - WhatsApp notification integration (484 lines)
- `/home/jaime/Documents/projects/alqueriavillacarmen/villacarmen_schema.sql` - Database schema (bookings table structure)

## API Overview

### Endpoint Details
- **File Path**: `/home/jaime/Documents/projects/alqueriavillacarmen/insert_booking.php`
- **URL**: `https://alqueriavillacarmen.com/insert_booking.php`
- **Method**: POST (required)
- **Content-Type**: application/x-www-form-urlencoded (standard form POST)
- **Response Type**: application/json
- **Authentication**: None (public endpoint)

### Purpose
Inserts a new restaurant booking into the `bookings` database table with complete transaction support and automatic WhatsApp notifications to both customer and restaurant staff.

## POST Parameters

### Required Parameters

| Parameter | Type | Example | Description | Validation |
|-----------|------|---------|-------------|------------|
| `date` | string (YYYY-MM-DD) | `2025-12-25` | Reservation date | Must be set via `isset()` check |
| `party_size` | integer | `4` | Number of guests | Must be set via `isset()` check |
| `time` | string (HH:MM:SS) | `14:00:00` | Reservation time | Must be set via `isset()` check |
| `nombre` | string | `Juan García` | Customer name | Must be set via `isset()` check |
| `phone` | string | `612345678` | Customer phone (9 digits) | Must be set via `isset()` check |

**Line 11 Validation**:
```php
if (isset($_POST['date']) && isset($_POST['party_size']) && isset($_POST['time'])
    && isset($_POST['nombre']) && isset($_POST['phone'])) {
```

If any required field is missing, the API returns plain text: `"Invalid input"` (line 106).

### Optional Parameters

| Parameter | Type | Default | Example | Description |
|-----------|------|---------|---------|-------------|
| `arroz_type` | string / null | null | `"Paella Valenciana"` | Type of rice dish |
| `arroz_servings` | integer / null | null | `2` | Number of rice servings |
| `commentary` | string | `''` (empty) | `"Alergia al marisco"` | Customer comments/notes |
| `baby_strollers` | integer | required | `1` | Number of baby strollers |
| `high_chairs` | integer | required | `2` | Number of high chairs |

**Note**: `baby_strollers` and `high_chairs` are accessed without `isset()` check (lines 28-29), suggesting they're always provided by the frontend, likely with default value `0`.

### Hardcoded Parameters

| Parameter | Value | Source | Description |
|-----------|-------|--------|-------------|
| `contact_email` | `'reservas@alqueriavillacarmen.com'` | Line 30 | Always set to restaurant email |

## Cookie-Based Rice Logic (reservaArroz)

### Cookie: `reservaArroz`

The API uses a special cookie to determine whether the customer wants rice with their reservation.

#### Cookie Value: `'false'` (Lines 12-16)
```php
if (isset($_COOKIE['reservaArroz']) && $_COOKIE['reservaArroz'] === 'false') {
    $arroz_type = null;
    $arroz_servings = null;
    $toggleArroz = 'false';
}
```

**Behavior**: Customer does NOT want rice
- Rice fields are explicitly set to `null` in database
- `toggleArroz` flag set to `'false'` for notification formatting
- POST parameters `arroz_type` and `arroz_servings` are ignored even if present

#### Cookie Value: `'true'` (Lines 17-21)
```php
else if ((isset($_COOKIE['reservaArroz']) && $_COOKIE['reservaArroz'] === 'true')) {
    $arroz_type = isset($_POST['arroz_type']) ? $_POST['arroz_type'] : '';
    $arroz_servings = isset($_POST['arroz_servings']) ? $_POST['arroz_servings'] : '';
    $toggleArroz = 'true';
}
```

**Behavior**: Customer wants rice
- Rice fields retrieved from POST parameters (with empty string fallback)
- `toggleArroz` flag set to `'true'` for notification formatting
- If POST parameters missing, empty strings are used (not null)

#### Cookie Not Set or Other Value
**Behavior**: Variables `$arroz_type`, `$arroz_servings`, and `$toggleArroz` are NOT defined
- This will cause PHP warnings when used in INSERT statement
- **Potential Bug**: The code doesn't handle this case explicitly

### Business Logic Implication
The cookie approach suggests:
1. Frontend sets `reservaArroz` cookie based on user's rice preference
2. Cookie persists across page loads during booking flow
3. Rice type/servings only matter if cookie is `'true'`
4. This prevents accidental rice orders when customer declines

## Database Structure

### Table: `bookings`

From `/home/jaime/Documents/projects/alqueriavillacarmen/villacarmen_schema.sql` (lines 100-122):

```sql
CREATE TABLE `bookings` (
  `id` int NOT NULL AUTO_INCREMENT,
  `customer_name` varchar(255) NOT NULL,
  `contact_email` varchar(255) NOT NULL,
  `reservation_date` date NOT NULL,
  `reservation_time` time NOT NULL,
  `party_size` int NOT NULL,
  `contact_phone` varchar(9) DEFAULT NULL,
  `re_confirmation_token` varchar(255) DEFAULT NULL,
  `re_confirmation` tinyint(1) DEFAULT '0',
  `commentary` text,
  `babyStrollers` int DEFAULT NULL,
  `highChairs` int DEFAULT NULL,
  `arroz_type` varchar(200) DEFAULT NULL,
  `arroz_servings` int DEFAULT NULL,
  `added_date` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `status` varchar(20) DEFAULT 'pending',
  `reminder_sent` tinyint(1) DEFAULT '0',
  `rice_reminder_sent` tinyint(1) DEFAULT '0',
  `table_number` varchar(10) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `idx_bookings_date_status` (`reservation_date`,`status`)
) ENGINE=InnoDB AUTO_INCREMENT=1984 DEFAULT CHARSET=utf8mb4;
```

### Fields Inserted by API

| Database Column | POST Parameter | Type | Nullable | Notes |
|-----------------|----------------|------|----------|-------|
| `reservation_date` | `$_POST['date']` | date | NO | Required field |
| `party_size` | `$_POST['party_size']` | int | NO | Required field |
| `reservation_time` | `$_POST['time']` | time | NO | Required field |
| `customer_name` | `$_POST['nombre']` | varchar(255) | NO | Required field |
| `contact_phone` | `$_POST['phone']` | varchar(9) | YES | Required by API but nullable in DB |
| `commentary` | `$_POST['commentary']` | text | YES | Optional field |
| `arroz_type` | `$_POST['arroz_type']` / `null` | varchar(200) | YES | Cookie-dependent |
| `arroz_servings` | `$_POST['arroz_servings']` / `null` | int | YES | Cookie-dependent |
| `babyStrollers` | `$_POST['baby_strollers']` | int | YES | Required by API |
| `highChairs` | `$_POST['high_chairs']` | int | YES | Required by API |
| `contact_email` | Hardcoded | varchar(255) | NO | Always `'reservas@alqueriavillacarmen.com'` |

### Fields Auto-Generated by Database

| Column | Default Value | Description |
|--------|---------------|-------------|
| `id` | AUTO_INCREMENT | Primary key, returned as `booking_id` in response |
| `added_date` | CURRENT_TIMESTAMP | Automatic timestamp of insertion |
| `status` | `'pending'` | Reservation status (pending/confirmed/cancelled) |
| `re_confirmation` | `0` | Re-confirmation flag (boolean) |
| `reminder_sent` | `0` | Whether reminder was sent |
| `rice_reminder_sent` | `0` | Whether rice reminder was sent |
| `re_confirmation_token` | NULL | Token for re-confirmation |
| `table_number` | NULL | Assigned table number |

### Database Index
- **Primary Index**: `id` (auto-increment primary key)
- **Composite Index**: `idx_bookings_date_status` on (`reservation_date`, `status`)
  - Optimizes queries filtering by date and status
  - Used by availability checking (Step 2 analysis)

## Transaction Handling

### Transaction Flow (Lines 33-104)

#### 1. Begin Transaction (Line 33)
```php
$conn->begin_transaction();
```
**Purpose**: Ensures atomicity - either all operations succeed or none do

#### 2. Try Block: Insert Booking (Lines 35-88)

**2.1 Prepare SQL Statement (Lines 36-38)**
```php
$sql = "INSERT INTO bookings (reservation_date, party_size, reservation_time,
        customer_name, contact_phone, commentary, arroz_type, arroz_servings,
        babyStrollers, highChairs, contact_email)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";
```

**2.2 Bind Parameters (Line 41)**
```php
$stmt->bind_param("sisssssiiis", $reservation_date, $party_size, $reservation_time,
                  $customer_name, $contact_phone, $commentary, $arroz_type,
                  $arroz_servings, $baby_strollers, $high_chairs, $contact_email);
```

**Parameter Types**:
- `s` = string: reservation_date, reservation_time, customer_name, contact_phone, commentary, arroz_type, contact_email (7)
- `i` = integer: party_size, arroz_servings, baby_strollers, high_chairs (4)

**2.3 Execute Insert (Lines 43-45)**
```php
if (!$stmt->execute()) {
    throw new Exception("Database insert failed: " . $stmt->error);
}
```
Throws exception if INSERT fails (constraints, duplicate keys, etc.)

**2.4 Get Booking ID (Lines 47-53)**
```php
$bookingId = $conn->insert_id;

if (!$bookingId) {
    throw new Exception("Failed to get booking ID after insert");
}
```
- Retrieves auto-increment ID of inserted row
- Validates that ID was generated (should never fail if INSERT succeeded)

**2.5 Commit Transaction (Lines 55-57)**
```php
$conn->commit();
$stmt->close();
```
**CRITICAL**: Transaction is committed BEFORE WhatsApp notifications are sent
- Ensures booking is saved even if notifications fail
- Prevents losing reservations due to external API issues

**2.6 Prepare Booking Data Array (Lines 59-75)**
```php
$bookingData = array(
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
    'toggleArroz' => isset($toggleArroz) ? $toggleArroz : 'false'
);
```
Packages all booking details for WhatsApp notification

**2.7 Send WhatsApp Notification (Lines 77-81)**
```php
$whatsAppResult = sendBookingWhatsAppUazApi($bookingData);
error_log("UAZAPI WhatsApp result from booking: " . json_encode($whatsAppResult));
```
- Calls WhatsApp service (analyzed below)
- Logs result but doesn't fail if notification fails
- **Non-blocking**: Booking success doesn't depend on notification success

**2.8 Return Success Response (Lines 83-88)**
```php
header('Content-Type: application/json');
echo json_encode([
    'success' => true,
    'booking_id' => $bookingId
]);
exit();
```

#### 3. Catch Block: Handle Errors (Lines 90-104)

```php
catch (Exception $e) {
    $conn->rollback();  // Undo all database changes
    if (isset($stmt)) {
        $stmt->close();
    }

    error_log("Booking insert error: " . $e->getMessage());
    header('Content-Type: application/json');
    echo json_encode([
        'success' => false,
        'message' => "Error: " . $e->getMessage()
    ]);
    exit();
}
```

**Rollback Conditions**:
- Database insert fails
- Booking ID retrieval fails
- Any exception thrown in try block

**Error Response Format**:
```json
{
  "success": false,
  "message": "Error: Database insert failed: Duplicate entry..."
}
```

### Transaction Best Practices Observed
1. **Commit Before External APIs**: Transaction committed before WhatsApp (lines 55-56)
2. **Proper Resource Cleanup**: Statement closed in both success and error paths
3. **Detailed Error Logging**: Errors logged to server logs (line 97)
4. **Atomic Operations**: All-or-nothing database changes
5. **No Nested Transactions**: Simple single-level transaction

## WhatsApp Notification Integration

### File: `send_whatsapp_uazapi.php`

Located at: `/home/jaime/Documents/projects/alqueriavillacarmen/includes/send_whatsapp_uazapi.php`

### Main Function Called: `sendBookingWhatsAppUazApi()`

**Signature** (Lines 295-344):
```php
function sendBookingWhatsAppUazApi($bookingData)
```

**Input**: Array with booking details (see section 2.6 above)

**Output**:
```php
[
  'success' => true/false,
  'messageSid' => 'message_id',
  'response' => [...],
  'type' => 'button_confirmation'
]
```

### WhatsApp Notification Flow

#### Step 1: Extract and Format Data (Lines 297-322)

```php
$customerName = $bookingData['customer_name'];

// Format date from YYYY-MM-DD to DD/MM/YYYY
$bookingDate = $bookingData['reservation_date'];
$date = new DateTime($bookingDate);
$bookingDate = $date->format('d/m/Y');

$bookingTime = $bookingData['reservation_time'];
$guestCount = $bookingData['party_size'];

// Format rice details
$arrozType = $bookingData['arroz_type'];
$arrozServings = $bookingData['arroz_servings'];
$toggleArroz = $bookingData['toggleArroz'];

if ($toggleArroz === 'true' && !empty($arrozType) && !empty($arrozServings)) {
    $preOrderDetails = "$arrozType ($arrozServings raciones)";
} else {
    $preOrderDetails = "No Arroz";
}

$highChairs = $bookingData['high_chairs'];
$babyStrollers = $bookingData['baby_strollers'];
$bookingId = $bookingData['booking_id'];
$customerPhone = $bookingData['contact_phone'];
```

**Key Transformations**:
- **Date Format**: `2025-12-25` → `25/12/2025`
- **Rice Display**:
  - If `toggleArroz === 'true'`: `"Paella Valenciana (2 raciones)"`
  - Otherwise: `"No Arroz"`

#### Step 2: Send Confirmation with Buttons (Lines 328-338)

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
    return $result;
}
```

### WhatsApp Confirmation Message Format

#### Function: `sendWhatsAppConfirmationWithButtonsUazApi()` (Lines 160-290)

**Message Template** (Lines 184-193):
```
*Confirmación de Reserva - Alquería Villa Carmen*

Hola {customerName},

Gracias por elegir Alquería Villa Carmen. Su reserva ha sido confirmada:

📅 *Fecha:* {bookingDate}
🕒 *Hora:* {bookingTime}
👥 *Personas:* {guestCount}
🍚 *Arroz:* {preOrderDetails}
👶 *Tronas:* {highChairs}
🛒 *Carritos:* {babyStrollers}

Si desea cancelar su reserva puede hacerlo clickando en el botón de abajo:
```

**Example Rendered**:
```
*Confirmación de Reserva - Alquería Villa Carmen*

Hola Juan García,

Gracias por elegir Alquería Villa Carmen. Su reserva ha sido confirmada:

📅 *Fecha:* 25/12/2025
🕒 *Hora:* 14:00
👥 *Personas:* 4
🍚 *Arroz:* Paella Valenciana (2 raciones)
👶 *Tronas:* 2
🛒 *Carritos:* 1

Si desea cancelar su reserva puede hacerlo clickando en el botón de abajo:
```

#### Button/Menu Configuration (Lines 196-209)

```php
$choices = [];

if (!empty($bookingId)) {
    $choices[] = "Cancelar Reserva|https://alqueriavillacarmen.com/cancel_reservation.php?id={$bookingId}";
}

$payload = [
    'number' => $recipientNumber,
    'type' => 'button',
    'text' => $confirmationText,
    'choices' => $choices,
];
```

**UAZAPI Button Format**: `"Button Label|URL"`
- Creates clickable button in WhatsApp message
- Opens cancellation page with booking ID in query string

### UAZAPI Integration Details

#### API Credentials (Lines 15-37)

```php
function getUazApiCredentials() {
    $base_path = defined('BASE_PATH') ? BASE_PATH : dirname(dirname(__FILE__));
    $env_file = $base_path . '/.env';

    if (!file_exists($env_file)) {
        // Fall back to environment variables
        return [
            'api_url' => getenv('UAZAPI_URL'),
            'api_token' => getenv('UAZAPI_TOKEN')
        ];
    }

    $env_vars = parse_ini_file($env_file);
    return [
        'api_url' => $env_vars['UAZAPI_URL'],
        'api_token' => $env_vars['UAZAPI_TOKEN']
    ];
}
```

**Credential Sources**:
1. **Primary**: `.env` file in base directory (parsed with `parse_ini_file()`)
2. **Fallback**: System environment variables (`getenv()`)

**Required Environment Variables**:
- `UAZAPI_URL`: Base URL of UAZAPI service
- `UAZAPI_TOKEN`: Authentication token

#### UAZAPI Endpoint (Line 181)

```php
$endpoint = $apiUrl . '/send/menu?token=' . urlencode($apiToken);
```

**Example**: `https://uazapi.example.com/send/menu?token=abc123def456`

**Authentication**: Token passed as query parameter (not header)

#### Phone Number Processing (Lines 176-178)

```php
$recipientNumber = preg_replace('/[^0-9]/', '', $recipientNumber);
$recipientNumber = '34' . $recipientNumber;
```

**Processing Steps**:
1. Strip all non-numeric characters (spaces, dashes, parentheses)
2. Prepend Spanish country code `34`

**Examples**:
- Input: `612 345 678` → Output: `34612345678`
- Input: `612-345-678` → Output: `34612345678`
- Input: `(612) 345678` → Output: `34612345678`

**Note**: Always assumes Spanish numbers (country code 34)

#### cURL Request Configuration (Lines 215-230)

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

**Configuration**:
- **Method**: POST
- **Content-Type**: application/json
- **Timeout**: 30 seconds
- **Payload**: JSON-encoded with number, type, text, choices

#### Error Handling with Fallback (Lines 235-265)

```php
if ($curlError) {
    error_log("UAZAPI Button cURL Error: " . $curlError);
    // Fall back to simple text message
    return sendWhatsAppConfirmationUazApi(...);
}

if ($httpCode != 200 && $httpCode != 201) {
    error_log("UAZAPI Button failed with HTTP " . $httpCode);
    // Fall back to simple text message
    return sendWhatsAppConfirmationUazApi(...);
}
```

**Fallback Strategy**:
1. **Try**: Send message with button/menu interface
2. **If Fails**: Fall back to plain text message (without button)
3. **Plain Text**: Includes cancellation URL in message body

**Plain Text Message Format** (Lines 125-154):
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

### Other WhatsApp Functions Available

#### 1. `sendUazApiWhatsApp()` (Lines 46-119)
**Purpose**: Core function to send simple text messages via UAZAPI
**Endpoint**: `/send/text?token={token}`
**Used By**: All other WhatsApp functions internally

#### 2. `sendWhatsAppReminderConfirmationUazApi()` (Lines 349-369)
**Purpose**: Send reminder message day before reservation
**Message Format**:
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

#### 3. `sendWhatsAppCancellationNotificationUazApi()` (Lines 374-441)
**Purpose**: Notify restaurant staff about cancellations
**Recipients**: Hardcoded phone numbers (lines 379)
```php
$phoneNumbers = ["34692747052", "34638857294", "34686969914"];
```

**Message Format** (Lines 413-420):
```
❌ *Reserva Cancelada* ❌

Cliente: *{customerName}*
📅 Fecha: {bookingDate}
⏰ Hora: {bookingTime}
👥 Personas: {partySize}
📞 Teléfono: {contactPhone}
🍚 Arroz: {arrozType}
📦 Raciones: {arrozServings}
```

**Notification Logic**:
- Sends to all 3 restaurant staff numbers
- Returns success if at least ONE message succeeds
- Continues sending to all numbers even if some fail

## Response Formats

### Success Response (Lines 84-87)

```json
{
  "success": true,
  "booking_id": 1984
}
```

**Fields**:
- `success` (boolean): Always `true` for successful insertions
- `booking_id` (integer): Auto-increment ID from database (used for cancellation links)

**HTTP Status**: 200 OK (implicit, no explicit status code set)

**Content-Type**: `application/json` (line 83)

### Error Response (Lines 99-102)

```json
{
  "success": false,
  "message": "Error: Database insert failed: Duplicate entry '123' for key 'PRIMARY'"
}
```

**Fields**:
- `success` (boolean): Always `false` for errors
- `message` (string): Error description including exception message

**Content-Type**: `application/json` (line 98)

### Invalid Input Response (Line 106)

**Response**: Plain text `"Invalid input"`
**Condition**: Missing required POST parameters
**Content-Type**: text/plain (no explicit header set)

**Note**: This is inconsistent with other responses (not JSON)

## Error Handling

### Error Reporting Configuration (Lines 2-5)

```php
error_reporting(E_ALL);
ini_set('display_errors', 0);
ini_set('log_errors', 1);
```

**Configuration**:
- **Report All Errors**: `E_ALL` captures all warnings/errors
- **Hide from Output**: `display_errors = 0` prevents errors in response
- **Log to File**: `log_errors = 1` writes to PHP error log

**Best Practice**: Errors logged but not exposed to clients

### Database Connection (Line 7)

```php
require('conectaVILLACARMEN.php');
```

**Provides**: `$conn` MySQLi connection object
- Same connection file used across all APIs
- No explicit connection error handling in this file (assumes `conectaVILLACARMEN.php` handles it)

### Potential Error Scenarios

| Scenario | Line | Handling | User Impact |
|----------|------|----------|-------------|
| Missing required parameter | 11, 106 | Return "Invalid input" | Booking fails, non-JSON response |
| Database connection failure | N/A | Likely fatal error | 500 Internal Server Error |
| INSERT constraint violation | 43-45 | Exception → Rollback → Error response | Booking fails, JSON error |
| Statement preparation failure | 40 | Likely fatal error | 500 Internal Server Error |
| Booking ID not generated | 51-53 | Exception → Rollback → Error response | Booking fails, JSON error |
| WhatsApp API failure | 78 | Logged but ignored | Booking succeeds, no notification |
| Cookie not set | N/A | Undefined variables → Warnings | Likely works but logs warnings |

### Logging Strategy

**Success Path Logging** (Line 81):
```php
error_log("UAZAPI WhatsApp result from booking: " . json_encode($whatsAppResult));
```
Logs WhatsApp notification result (success or failure)

**Error Path Logging** (Line 97):
```php
error_log("Booking insert error: " . $e->getMessage());
```
Logs database errors and exceptions

**Additional Logging in WhatsApp Module**:
- Request/response logging (lines 66-68, 87-88, 211-212, 232-233 in send_whatsapp_uazapi.php)
- cURL error logging
- Phone number processing logging

## Security Considerations

### Prepared Statements (Lines 37-41)
```php
$sql = "INSERT INTO bookings (...) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";
$stmt = $conn->prepare($sql);
$stmt->bind_param("sisssssiiis", ...);
```
**Protection**: SQL injection prevention via parameterized queries

### Input Validation Gaps

| Issue | Risk Level | Description |
|-------|------------|-------------|
| No parameter sanitization | Medium | Raw POST data used directly (relies on prepared statements) |
| No phone number format validation | Low | Accepts any string, but WhatsApp module formats it |
| No date format validation | Medium | Invalid dates could cause database errors |
| No party size range check | Medium | Could accept negative or excessively large values |
| Missing cookie validation | Low | Cookie values not validated beyond string comparison |
| No commentary length limit | Low | Could accept extremely long text (DB has `text` type limit) |

### CORS Headers
**Not Set**: Unlike other APIs analyzed (Steps 2-3), this API doesn't set CORS headers
- May cause issues if called from browser JavaScript on different domain
- Likely intended for same-origin form submissions only

### Authentication
**None**: Public endpoint with no authentication/authorization
- Any client can submit bookings
- Could be abused for spam/fake reservations
- **Risk**: Denial of service through fake booking flooding

## Database Connection & Resources

### Connection File (Line 7)
```php
require('conectaVILLACARMEN.php');
```
**Location**: `/home/jaime/Documents/projects/alqueriavillacarmen/conectaVILLACARMEN.php`
**Provides**: `$conn` MySQLi object

### Resource Cleanup (Lines 57, 94, 109)

**Success Path**:
```php
$stmt->close();  // Line 57 (after commit)
$conn->close();  // Line 109 (end of script)
```

**Error Path**:
```php
if (isset($stmt)) {
    $stmt->close();  // Line 94
}
// Note: $conn is NOT closed in error path (potential resource leak)
```

**Issue**: Connection not closed in error path before `exit()` (line 103)
- Minor issue as PHP will close connections on script termination
- Best practice would be to close explicitly

## Integration Points

### 1. Frontend Form Requirements

The frontend must provide:
- All 5 required POST parameters: `date`, `party_size`, `time`, `nombre`, `phone`
- Optional parameters: `commentary`, `arroz_type`, `arroz_servings`, `baby_strollers`, `high_chairs`
- Cookie `reservaArroz` set to `'true'` or `'false'`

### 2. Date Availability Check Integration

Before calling `insert_booking.php`, frontend should:
1. Call `check_date_availability.php` (from Step 2) to verify date/time is available
2. Call `get_available_rice_types.php` (from Step 3) to get rice options
3. Set `reservaArroz` cookie based on user's rice selection
4. Submit booking with all validated parameters

### 3. WhatsApp Bot Integration

For WhatsApp bot to create bookings:

**Required Information Collection**:
1. Date (validate with `check_date_availability.php`)
2. Party size (validate against availability)
3. Time slot (validate with `gethourdata.php`)
4. Customer name
5. Customer phone number (9 digits, Spanish format)
6. Rice preference (yes/no)
7. If yes: Rice type (from `get_available_rice_types.php`) and servings
8. Number of baby strollers
9. Number of high chairs
10. Optional comments

**Bot Workflow**:
```
1. Collect all required information through conversation
2. Set reservaArroz cookie equivalent (or include in POST logic)
3. POST to insert_booking.php with all parameters
4. Parse JSON response:
   - If success=true: Confirm booking with booking_id
   - If success=false: Show error message to user
5. Customer receives WhatsApp confirmation automatically
```

**Note**: WhatsApp bot would receive its own confirmation message if using the same phone number

## Business Logic Insights

### Booking Workflow
1. Customer provides all information
2. System validates and inserts into database
3. Transaction commits (booking guaranteed)
4. WhatsApp confirmation sent (best effort, not guaranteed)
5. Booking ID returned for future reference

### Rice Ordering Process
- Cookie-based toggle system suggests multi-page form
- User decides on rice before final submission
- Rice selection persistent across form pages
- System distinguishes "No rice" (null) from "Rice type not selected" (empty string)

### Notification Strategy
- **Customer**: Always notified (if phone provided)
- **Button vs Text**: Tries button first, falls back to text
- **Cancellation Link**: Provided only if booking_id available
- **Non-blocking**: Notification failure doesn't prevent booking

### Restaurant Staff Notifications
**Hardcoded Phone Numbers** (from `sendWhatsAppCancellationNotificationUazApi`):
- `34692747052`
- `34638857294`
- `34686969914`

**Used For**: Cancellation notifications (not new bookings)
- New bookings likely notified through different mechanism (email? separate script?)

## Code Quality Assessment

### Strengths
1. **Transaction Safety**: Proper begin/commit/rollback usage
2. **Prepared Statements**: SQL injection protection
3. **Error Logging**: Comprehensive logging without exposing details
4. **Graceful Degradation**: WhatsApp button → text fallback
5. **Separation of Concerns**: WhatsApp logic in separate file
6. **Resource Management**: Statements properly closed
7. **Booking Persistence**: Commit before external API calls

### Weaknesses
1. **Inconsistent Responses**: "Invalid input" is plain text, not JSON
2. **Missing Input Validation**: No sanitization/validation of POST data
3. **Cookie Handling**: No validation for missing/invalid cookie values
4. **No Authentication**: Public endpoint vulnerable to abuse
5. **No Rate Limiting**: Could be flooded with fake bookings
6. **Incomplete Resource Cleanup**: Connection not closed in error path
7. **Hardcoded Values**: Email address, phone numbers in code
8. **No CORS Headers**: May have cross-origin issues

### Potential Improvements
1. Add input validation and sanitization
2. Implement rate limiting or CAPTCHA
3. Make responses consistently JSON
4. Add authentication/session validation
5. Externalize configuration (email, phone numbers)
6. Add webhook/callback for WhatsApp status
7. Implement booking confirmation/verification flow
8. Add duplicate booking detection

## Context for Next Step

### What We've Learned (Steps 1-4)

1. **Step 1**: Located all API files
2. **Step 2**: Analyzed complex date availability with 7 validation layers
3. **Step 3**: Analyzed simple rice type retrieval from FINDE table
4. **Step 4**: Analyzed booking insertion with transaction support and WhatsApp integration

### Complete Booking Flow Discovered

```
Frontend/Bot                    Backend APIs                           External Services
    |                               |                                          |
    |-- Check Date Availability --->| check_date_availability.php              |
    |<- Available/Unavailable -------|                                          |
    |                               |                                          |
    |-- Get Rice Types ------------>| get_available_rice_types.php             |
    |<- Rice Type Array ------------|                                          |
    |                               |                                          |
    |-- Submit Booking ------------>| insert_booking.php                       |
    |                               |-- Begin Transaction                      |
    |                               |-- INSERT INTO bookings                   |
    |                               |-- Get booking_id                         |
    |                               |-- COMMIT                                 |
    |                               |                                          |
    |                               |-- Send WhatsApp Confirmation ----------->| UAZAPI
    |                               |<- WhatsApp Result (logged) --------------|
    |                               |                                          |
    |<- {success, booking_id} ------|                                          |
    |                               |                                          |
Customer Phone                                                                  |
    |<- Confirmation Message with Cancellation Button -----------------------|
```

### Key Integration Points for WhatsApp Bot

1. **Date Selection**: Use `check_date_availability.php` API
2. **Rice Selection**: Use `get_available_rice_types.php` API
3. **Booking Creation**: Use `insert_booking.php` API
4. **Cookie Equivalent**: Bot needs to track rice preference state
5. **Phone Formatting**: Ensure 9-digit Spanish format
6. **Required Fields**: All 5 core fields must be collected
7. **Error Handling**: Parse JSON responses, handle failures gracefully

### Database Schema Insights

**Tables Identified**:
- `bookings` - Main reservation table (Step 4)
- `daily_limits` - Daily capacity limits (Step 2)
- `FINDE` - Multi-purpose config table (Step 3)
- `hour_configuration` - Time slot configuration (referenced in Step 2)

**Relationships**:
- Bookings reference date/time validated against daily_limits
- Rice types in bookings validated against FINDE (TIPO='ARROZ')

### Outstanding Questions

1. **Modification API**: How to update existing bookings?
2. **Cancellation API**: `cancel_reservation.php` referenced but not analyzed
3. **Staff Notifications**: Are new bookings sent to staff via WhatsApp or only cancellations?
4. **Confirmation Flow**: What is `re_confirmation_token` used for?
5. **Table Assignment**: How is `table_number` assigned?
6. **Status Updates**: When/how does status change from 'pending' to 'confirmed'?

### Next Steps Likely To Analyze

- Booking modification/update endpoints
- Cancellation endpoint (`cancel_reservation.php`)
- Time slot configuration (`gethourdata.php` - referenced in Step 2)
- Closed days API (`fetch_closed_days.php` - referenced in Step 2)
- WhatsApp webhook handling (if any)

## Issues Encountered

None. All files successfully read and analyzed.

## Blockers

None.

## Verification

- [x] File successfully read: `/home/jaime/Documents/projects/alqueriavillacarmen/insert_booking.php` (110 lines)
- [x] WhatsApp integration file read: `/home/jaime/Documents/projects/alqueriavillacarmen/includes/send_whatsapp_uazapi.php` (484 lines)
- [x] Database schema read: `/home/jaime/Documents/projects/alqueriavillacarmen/villacarmen_schema.sql` (bookings table structure)
- [x] All required POST parameters documented with types and validation
- [x] All optional POST parameters documented
- [x] Cookie handling (reservaArroz) fully documented
- [x] Transaction workflow documented (begin/commit/rollback)
- [x] WhatsApp notification trigger documented with full message format
- [x] UAZAPI integration documented (credentials, endpoints, payload)
- [x] Response formats documented (success/error/invalid)
- [x] Database structure documented (11 inserted fields, 8 auto-generated)
- [x] Error handling documented with all scenarios
- [x] Security considerations analyzed
- [x] Business logic insights extracted
- [x] Integration points identified for WhatsApp bot
- [x] Code quality assessed
- [x] Fallback strategies documented (button → text message)
- [x] Restaurant staff notification system documented
- [x] Complete booking flow diagram created
- [x] No issues or blockers encountered
