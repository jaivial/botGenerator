# Step 6 Output: Document UAZAPI /send/text endpoint

## Status: COMPLETED

## Execution Summary
Successfully documented the UAZAPI `/send/text` endpoint based on comprehensive analysis from Step 5. The endpoint is used for sending simple text messages via WhatsApp through the UAZAPI service. This is the core text messaging function used by all text-based notifications in the system including confirmations (fallback), reminders, and staff cancellation alerts.

## Files Read
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/steps/step-5-output.md` - UAZAPI integration analysis
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/whatsapp-booking-workflow-steps.md` - Step requirements
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/progress.json` - Current progress

## Files Created
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/steps/step-6-output.md` - This documentation

## UAZAPI /send/text Endpoint Documentation

### Endpoint Specification

**Base Endpoint**:
```
POST {UAZAPI_URL}/send/text?token={UAZAPI_TOKEN}
```

**Components**:
- **Base URL**: `{UAZAPI_URL}` - Configured in .env file or environment variables
- **Path**: `/send/text` - Text message sending endpoint
- **Authentication**: Query parameter `token={UAZAPI_TOKEN}`

**Example Full URL**:
```
https://uazapi.example.com/send/text?token=abc123def456
```

---

### HTTP Method
```
POST
```

**Why POST**: Sending message data in request body rather than URL parameters (security, payload size)

---

### Request Headers

**Required Headers**:
```http
Content-Type: application/json
```

**Optional Headers**:
- None required for basic operation

**Example Headers Block**:
```http
POST /send/text?token=abc123def456 HTTP/1.1
Host: uazapi.example.com
Content-Type: application/json
Content-Length: 65
```

---

### Authentication

**Method**: Token-based authentication via query parameter

**Parameter Name**: `token`

**Token Location**: Query string (appended to URL)

**Format**:
```
?token={UAZAPI_TOKEN}
```

**Example**:
```
/send/text?token=abc123def456
```

**Token Source**:
- Primary: `.env` file → `UAZAPI_TOKEN=your_token_here`
- Fallback: Environment variable → `getenv('UAZAPI_TOKEN')`

**PHP Implementation** (from Step 5):
```php
$endpoint = $apiUrl . '/send/text?token=' . urlencode($apiToken);
```

**Important**: Token is URL-encoded using `urlencode()` to handle special characters

---

### Request Payload Structure

**Format**: JSON object

**Required Fields**:

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `number` | string | Yes | Recipient phone number with country code | `"34612345678"` |
| `text` | string | Yes | Message text to send | `"Hello World"` |

**Minimal Payload Example**:
```json
{
  "number": "34612345678",
  "text": "Hello World"
}
```

**Complete Payload Example**:
```json
{
  "number": "34638857294",
  "text": "*Confirmación de Reserva - Alquería Villa Carmen*\n\nHola Juan García,\n\nGracias por elegir Alquería Villa Carmen. Su reserva ha sido confirmada con los siguientes detalles:\n\n📅 *Fecha:* 25/12/2025\n🕒 *Hora:* 14:00\n👥 *Personas:* 4\n🍚 *Arroz:* Paella Valenciana (2 raciones)\n👶 *Tronas:* 2\n🛒 *Carritos:* 1\n\nSi necesita modificar o cancelar su reserva, contáctenos:\n☎️ Teléfono: 638 85 72 94\n✉️ Email: reservas@alqueriavillacarmen.com\n\n¡Esperamos darle la bienvenida pronto!\n\nAlquería Villa Carmen"
}
```

**PHP Payload Construction** (from Step 5):
```php
$payload = json_encode([
    'number' => $recipientNumber,
    'text' => $message
]);
```

---

### Phone Number Format

**Expected Format**:
- Country code + 9 digits (Spanish format)
- Total: 11 characters
- Example: `34612345678`

**Breakdown**:
- `34` - Spanish country code
- `612345678` - 9-digit Spanish phone number

**PHP Formatting Logic** (from Step 5):
```php
// Step 1: Strip all non-numeric characters
$recipientNumber = preg_replace('/[^0-9]/', '', $recipientNumber);

// Step 2: Prepend Spanish country code
$recipientNumber = '34' . $recipientNumber;
```

**Input/Output Examples**:

| Input | After Regex | After Prefix | Final Output |
|-------|-------------|--------------|--------------|
| `"612 345 678"` | `"612345678"` | `"34612345678"` | `34612345678` ✓ |
| `"612-345-678"` | `"612345678"` | `"34612345678"` | `34612345678` ✓ |
| `"(612) 345678"` | `"612345678"` | `"34612345678"` | `34612345678` ✓ |
| `"+34612345678"` | `"34612345678"` | `"3434612345678"` | `3434612345678` ✗ DUPLICATE! |

**WARNING**: If input already contains country code `34`, it will be duplicated!

**Best Practice**: Always provide 9-digit Spanish numbers WITHOUT country code:
- ✓ Correct: `"612345678"`, `"612 345 678"`, `"612-345-678"`
- ✗ Problematic: `"34612345678"`, `"+34612345678"` (will duplicate country code)

**Spanish Phone Number Types**:
- **Mobile**: `6XX XXX XXX` → `346XXXXXXXX`
- **Landline**: `9XX XXX XXX` → `349XXXXXXXX`

---

### Text Message Format

**Field**: `text` (string)

**Supported Features**:
1. **WhatsApp Markdown Formatting**:
   - `*text*` = **Bold text**
   - `_text_` = _Italic text_
   - `~text~` = ~~Strikethrough~~
   - ````text```` = `Monospace`

2. **Unicode Support**:
   - Emojis: 📅 🕒 👥 🍚 ✉️ ☎️ 🔔 ❌
   - Special characters: á é í ó ú ñ
   - All UTF-8 characters

3. **Line Breaks**:
   - `\n` = New line
   - `\n\n` = Paragraph break

**Example with Formatting**:
```json
{
  "number": "34612345678",
  "text": "🔔 *Recordatorio de Reserva* 🔔\n\nHola *Juan García*,\n\nTe recordamos tu reserva para mañana:\n📅 *Fecha:* 25/12/2025\n⏰ *Hora:* 14:00\n\n¡Nos vemos pronto! 😊"
}
```

**Renders as**:
```
🔔 Recordatorio de Reserva 🔔

Hola Juan García,

Te recordamos tu reserva para mañana:
📅 Fecha: 25/12/2025
⏰ Hora: 14:00

¡Nos vemos pronto! 😊
```

**Character Limits**:
- Maximum message length: Not specified in current implementation
- Recommended: Keep under 4096 characters (WhatsApp limit)

---

### Response Format

**Content-Type**: `application/json`

**Success Response** (HTTP 200 or 201):

**Primary Format**:
```json
{
  "id": "msg_abc123def456",
  "status": "sent",
  "number": "34612345678"
}
```

**Alternative Format** (nested):
```json
{
  "data": {
    "id": "msg_abc123def456",
    "status": "sent"
  },
  "success": true
}
```

**PHP Response Handling** (from Step 5):
```php
$responseData = json_decode($response, true);

return [
    'success' => true,
    'messageSid' => $responseData['id'] ?? $responseData['data']['id'] ?? 'unknown',
    'response' => $responseData
];
```

**Message ID Extraction Priority**:
1. `$responseData['id']` (primary format)
2. `$responseData['data']['id']` (nested format)
3. `'unknown'` (fallback if neither exists)

**Parsed Success Response**:
```php
[
    'success' => true,
    'messageSid' => 'msg_abc123def456',
    'response' => [
        'id' => 'msg_abc123def456',
        'status' => 'sent',
        'number' => '34612345678'
    ]
]
```

---

### Error Responses

#### 1. HTTP Error Codes

**Accepted Success Codes**:
- `200` - OK (message sent)
- `201` - Created (message created)

**Error Codes**:

| HTTP Code | Meaning | Likely Cause |
|-----------|---------|--------------|
| `400` | Bad Request | Invalid payload format, missing required fields |
| `401` | Unauthorized | Invalid or missing token |
| `403` | Forbidden | Token valid but lacks permissions |
| `404` | Not Found | Invalid endpoint URL |
| `429` | Too Many Requests | Rate limit exceeded |
| `500` | Internal Server Error | UAZAPI service error |
| `503` | Service Unavailable | UAZAPI temporarily down |

**PHP Error Handling** (from Step 5):
```php
if ($httpCode != 200 && $httpCode != 201) {
    return [
        'success' => false,
        'error' => 'HTTP ' . $httpCode . ': ' . $response
    ];
}
```

**Error Response Example**:
```php
[
    'success' => false,
    'error' => 'HTTP 401: Unauthorized - Invalid token'
]
```

---

#### 2. cURL Errors

**Error Types**:
- Connection timeout (30 seconds default)
- DNS resolution failure
- SSL certificate errors
- Network unreachable

**PHP Error Handling** (from Step 5):
```php
if ($curlError) {
    error_log("UAZAPI cURL Error: " . $curlError);
    return [
        'success' => false,
        'error' => 'cURL Error: ' . $curlError
    ];
}
```

**cURL Error Response Example**:
```php
[
    'success' => false,
    'error' => 'cURL Error: Connection timeout after 30000 ms'
]
```

---

#### 3. Exception Errors

**PHP Exception Handling** (from Step 5):
```php
catch (Exception $e) {
    error_log("UAZAPI Error: " . $e->getMessage());
    return [
        'success' => false,
        'error' => $e->getMessage()
    ];
}
```

**Exception Response Example**:
```php
[
    'success' => false,
    'error' => 'JSON encoding failed'
]
```

---

### cURL Configuration

**PHP cURL Implementation** (from Step 5):
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

**cURL Options Explained**:

| Option | Value | Purpose |
|--------|-------|---------|
| `CURLOPT_POST` | `true` | Use HTTP POST method |
| `CURLOPT_POSTFIELDS` | JSON payload | Request body data |
| `CURLOPT_RETURNTRANSFER` | `true` | Return response as string (not echo) |
| `CURLOPT_HTTPHEADER` | `Content-Type: application/json` | Set JSON content type header |
| `CURLOPT_TIMEOUT` | `30` seconds | Maximum execution time before timeout |

**Timeout Details**:
- **Connection timeout**: Default (system dependent)
- **Execution timeout**: 30 seconds
- **After timeout**: Returns cURL error

---

### Complete cURL Command Example

**Basic Example**:
```bash
curl -X POST "https://uazapi.example.com/send/text?token=abc123def456" \
  -H "Content-Type: application/json" \
  -d '{
    "number": "34612345678",
    "text": "Hello World"
  }'
```

**Booking Confirmation Example**:
```bash
curl -X POST "https://uazapi.example.com/send/text?token=abc123def456" \
  -H "Content-Type: application/json" \
  -d '{
    "number": "34638857294",
    "text": "*Confirmación de Reserva - Alquería Villa Carmen*\n\nHola Juan García,\n\nGracias por elegir Alquería Villa Carmen. Su reserva ha sido confirmada con los siguientes detalles:\n\n📅 *Fecha:* 25/12/2025\n🕒 *Hora:* 14:00\n👥 *Personas:* 4\n🍚 *Arroz:* Paella Valenciana (2 raciones)\n👶 *Tronas:* 2\n🛒 *Carritos:* 1\n\nSi necesita modificar o cancelar su reserva, contáctenos:\n☎️ Teléfono: 638 85 72 94\n✉️ Email: reservas@alqueriavillacarmen.com\n\n¡Esperamos darle la bienvenida pronto!\n\nAlquería Villa Carmen"
  }'
```

**Reminder Example**:
```bash
curl -X POST "https://uazapi.example.com/send/text?token=abc123def456" \
  -H "Content-Type: application/json" \
  -d '{
    "number": "34692747052",
    "text": "🔔 *Recordatorio de Reserva* 🔔\n\nHola *Juan García*,\n\nTe recordamos tu reserva para mañana:\n📅 *Fecha:* 25/12/2025\n⏰ *Hora:* 14:00\n\n¡Nos vemos pronto! 😊\n\nSi necesitas cancelar:\nhttps://alqueriavillacarmen.com/cancel?id=1984"
  }'
```

**Staff Cancellation Notification Example**:
```bash
curl -X POST "https://uazapi.example.com/send/text?token=abc123def456" \
  -H "Content-Type: application/json" \
  -d '{
    "number": "34638857294",
    "text": "❌ *Reserva Cancelada* ❌\n\nCliente: *Juan García*\n📅 Fecha: 25/12/2025\n⏰ Hora: 14:00\n👥 Personas: 4\n📞 Teléfono: 612345678\n🍚 Arroz: Paella Valenciana\n📦 Raciones: 2 raciones"
  }'
```

**Testing with Verbose Output**:
```bash
curl -X POST "https://uazapi.example.com/send/text?token=abc123def456" \
  -H "Content-Type: application/json" \
  -d '{"number":"34612345678","text":"Test message"}' \
  -v
```

**Expected Response** (200 OK):
```json
{
  "id": "msg_abc123def456",
  "status": "sent",
  "number": "34612345678"
}
```

---

### Usage in Current System

**Functions Using This Endpoint** (from Step 5):

1. **`sendUazApiWhatsApp()`** - Core text messaging function
   - Direct usage of `/send/text`
   - Used by all other functions

2. **`sendWhatsAppConfirmationUazApi()`** - Simple booking confirmation
   - Fallback when button messages fail
   - Plain text with contact information

3. **`sendWhatsAppReminderConfirmationUazApi()`** - Booking reminders
   - Day-before reminder to customers
   - Includes cancellation URL

4. **`sendWhatsAppCancellationNotificationUazApi()`** - Staff notifications
   - Alerts 3 staff members of cancellations
   - Multi-recipient (loops 3 phone numbers)

---

### PHP Function Reference

**Primary Function**: `sendUazApiWhatsApp()`

**Location**: `/home/jaime/Documents/projects/alqueriavillacarmen/includes/send_whatsapp_uazapi.php` (lines 46-119)

**Function Signature**:
```php
function sendUazApiWhatsApp($recipientNumber, $message)
```

**Parameters**:
| Parameter | Type | Description | Example |
|-----------|------|-------------|---------|
| `$recipientNumber` | string | 9-digit Spanish phone (no country code) | `"612345678"` |
| `$message` | string | Text message (supports WhatsApp markdown) | `"*Bold* text"` |

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

**Complete Function Flow**:
```php
function sendUazApiWhatsApp($recipientNumber, $message) {
    try {
        // 1. Load credentials
        $credentials = getUazApiCredentials();
        $apiUrl = $credentials['api_url'];
        $apiToken = $credentials['api_token'];

        // 2. Format phone number
        $recipientNumber = preg_replace('/[^0-9]/', '', $recipientNumber);
        $recipientNumber = '34' . $recipientNumber;

        // 3. Build endpoint
        $endpoint = $apiUrl . '/send/text?token=' . urlencode($apiToken);

        // 4. Create payload
        $payload = json_encode([
            'number' => $recipientNumber,
            'text' => $message
        ]);

        // 5. Log request
        error_log("UAZAPI Request - Endpoint: " . $endpoint);
        error_log("UAZAPI Request - Payload: " . $payload);

        // 6. Configure cURL
        $ch = curl_init($endpoint);
        curl_setopt($ch, CURLOPT_POST, true);
        curl_setopt($ch, CURLOPT_POSTFIELDS, $payload);
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($ch, CURLOPT_HTTPHEADER, [
            'Content-Type: application/json'
        ]);
        curl_setopt($ch, CURLOPT_TIMEOUT, 30);

        // 7. Execute request
        $response = curl_exec($ch);
        $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        $curlError = curl_error($ch);
        curl_close($ch);

        // 8. Log response
        error_log("UAZAPI Response - HTTP Code: " . $httpCode);
        error_log("UAZAPI Response - Body: " . $response);

        // 9. Handle cURL errors
        if ($curlError) {
            error_log("UAZAPI cURL Error: " . $curlError);
            return [
                'success' => false,
                'error' => 'cURL Error: ' . $curlError
            ];
        }

        // 10. Handle HTTP errors
        if ($httpCode != 200 && $httpCode != 201) {
            return [
                'success' => false,
                'error' => 'HTTP ' . $httpCode . ': ' . $response
            ];
        }

        // 11. Parse success response
        $responseData = json_decode($response, true);

        return [
            'success' => true,
            'messageSid' => $responseData['id'] ?? $responseData['data']['id'] ?? 'unknown',
            'response' => $responseData
        ];

    } catch (Exception $e) {
        error_log("UAZAPI Error: " . $e->getMessage());
        return [
            'success' => false,
            'error' => $e->getMessage()
        ];
    }
}
```

---

### Logging Details

**Request Logging** (before sending):
```php
error_log("UAZAPI Request - Endpoint: " . $endpoint);
error_log("UAZAPI Request - Payload: " . $payload);
```

**Example Log Output**:
```
[2025-11-27 10:30:15] UAZAPI Request - Endpoint: https://uazapi.example.com/send/text?token=abc123def456
[2025-11-27 10:30:15] UAZAPI Request - Payload: {"number":"34612345678","text":"Hello World"}
```

**Response Logging** (after receiving):
```php
error_log("UAZAPI Response - HTTP Code: " . $httpCode);
error_log("UAZAPI Response - Body: " . $response);
```

**Example Log Output** (success):
```
[2025-11-27 10:30:16] UAZAPI Response - HTTP Code: 200
[2025-11-27 10:30:16] UAZAPI Response - Body: {"id":"msg_abc123","status":"sent","number":"34612345678"}
```

**Example Log Output** (error):
```
[2025-11-27 10:30:16] UAZAPI Response - HTTP Code: 401
[2025-11-27 10:30:16] UAZAPI Response - Body: {"error":"Invalid token"}
[2025-11-27 10:30:16] UAZAPI cURL Error:
```

**Error Logging**:
```php
error_log("UAZAPI cURL Error: " . $curlError);
error_log("UAZAPI Error: " . $e->getMessage());
```

**Benefit**: Complete audit trail for debugging and monitoring

---

### Configuration Requirements

**Environment Variables** (`.env` file):
```ini
UAZAPI_URL=https://uazapi.example.com
UAZAPI_TOKEN=your_actual_token_here
```

**Alternative**: System environment variables
```bash
export UAZAPI_URL="https://uazapi.example.com"
export UAZAPI_TOKEN="your_actual_token_here"
```

**Loading Priority** (from `getUazApiCredentials()` function):
1. **Primary**: `.env` file in project root (parsed with `parse_ini_file()`)
2. **Fallback**: System environment variables (via `getenv()`)
3. **Default**: Empty strings (will cause API failures)

**Credential Validation**:
```php
$credentials = getUazApiCredentials();
if (empty($credentials['api_url']) || empty($credentials['api_token'])) {
    // Will fail when attempting API call
    error_log("WARNING: UAZAPI credentials not configured");
}
```

---

### Security Considerations

1. **Token Protection**:
   - Store in `.env` file (excluded from version control)
   - Never log token in plain text
   - Use `urlencode()` when adding to URL

2. **HTTPS Required**:
   - Always use `https://` for UAZAPI URL
   - Prevents token interception
   - Ensures message privacy

3. **Input Sanitization**:
   - Phone numbers: Strip non-numeric characters
   - Message text: No sanitization (supports all UTF-8)
   - Payload: JSON-encoded (automatic escaping)

4. **Error Messages**:
   - Don't expose tokens in error responses
   - Log errors server-side only
   - Return generic errors to users

---

### Rate Limiting (Unknown)

**Current Implementation**: No rate limiting in PHP code

**Recommendations**:
- Check UAZAPI documentation for rate limits
- Implement retry logic with exponential backoff
- Consider message queue for high volume

**Potential UAZAPI Limits** (to verify):
- Messages per minute
- Messages per hour
- Concurrent connections
- Daily quota

---

### C# Integration Requirements

**For BotGenerator WhatsApp Bot**:

**1. Configuration** (`appsettings.json`):
```json
{
  "UazApi": {
    "BaseUrl": "https://uazapi.example.com",
    "Token": "your_token_here"
  }
}
```

**2. HTTP Client Service**:
```csharp
public class UazApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _token;

    public UazApiService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _baseUrl = config["UazApi:BaseUrl"];
        _token = config["UazApi:Token"];
    }

    public async Task<UazApiResponse> SendTextMessage(string recipientNumber, string message)
    {
        // Format phone number (add country code 34)
        var formattedNumber = FormatSpanishPhone(recipientNumber);

        // Build endpoint
        var endpoint = $"{_baseUrl}/send/text?token={Uri.EscapeDataString(_token)}";

        // Create payload
        var payload = new
        {
            number = formattedNumber,
            text = message
        };

        // Send request
        var response = await _httpClient.PostAsJsonAsync(endpoint, payload);

        // Handle response
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new UazApiException($"HTTP {response.StatusCode}: {error}");
        }

        return await response.Content.ReadFromJsonAsync<UazApiResponse>();
    }

    private string FormatSpanishPhone(string input)
    {
        // Remove non-digits
        var digits = Regex.Replace(input, @"[^0-9]", "");

        // Add country code if not present
        if (!digits.StartsWith("34"))
        {
            digits = "34" + digits;
        }

        return digits;
    }
}
```

**3. Response Model**:
```csharp
public class UazApiResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("number")]
    public string Number { get; set; }
}
```

**4. Error Handling**:
```csharp
try
{
    var result = await _uazApiService.SendTextMessage("612345678", "Hello World");
    Console.WriteLine($"Message sent with ID: {result.Id}");
}
catch (UazApiException ex)
{
    _logger.LogError(ex, "Failed to send WhatsApp message");
    // Implement fallback or retry logic
}
```

---

### Use Cases in Current System

**1. Booking Confirmation** (fallback from button message):
```php
sendWhatsAppConfirmationUazApi(
    $customerName,      // "Juan García"
    $bookingDate,       // "25/12/2025"
    $bookingTime,       // "14:00"
    $guestCount,        // 4
    $preOrderDetails,   // "Paella Valenciana (2 raciones)"
    $childrenMenuCount, // 2 (high chairs)
    $vegetarianOptionCount, // 1 (baby strollers)
    $recipientNumber,   // "612345678"
    $bookingId          // 1984
);
```

**2. Booking Reminder**:
```php
sendWhatsAppReminderConfirmationUazApi(
    $customerName,    // "Juan García"
    $bookingDate,     // "25/12/2025"
    $bookingTime,     // "14:00"
    $bookingId,       // 1984
    $recipientNumber  // "612345678"
);
```

**3. Cancellation Notification** (to staff):
```php
sendWhatsAppCancellationNotificationUazApi([
    'customer_name' => 'Juan García',
    'reservation_date' => '2025-12-25',
    'reservation_time' => '14:00:00',
    'party_size' => 4,
    'contact_phone' => '612345678',
    'arroz_type' => 'Paella Valenciana',
    'arroz_servings' => 2
]);
// Sends to 3 staff numbers: 34692747052, 34638857294, 34686969914
```

---

### Comparison with /send/menu Endpoint

| Feature | /send/text | /send/menu |
|---------|-----------|-----------|
| **Purpose** | Simple text messages | Interactive messages with buttons |
| **Payload** | `{number, text}` | `{number, type, text, choices}` |
| **Interactive Elements** | None | Buttons/menus |
| **Fallback** | N/A | Falls back to /send/text |
| **Use Cases** | Confirmations, reminders, alerts | Confirmations with cancellation button |
| **Complexity** | Low | Medium |
| **Reliability** | High | Medium (requires fallback) |

**Recommendation**: Use `/send/text` for guaranteed delivery, `/send/menu` for enhanced UX with fallback to `/send/text`

---

## Issues Encountered

None. Documentation completed successfully based on Step 5 analysis.

---

## Blockers

None.

---

## Context for Next Step

### What We've Documented (Step 6)

Complete specification for UAZAPI `/send/text` endpoint including:
- Endpoint URL format: `{UAZAPI_URL}/send/text?token={TOKEN}`
- HTTP method: POST
- Content-Type: application/json
- Payload structure: `{ number: "34612345678", text: "message" }`
- Response format: `{ id: "msg_id", status: "sent", number: "34612345678" }`
- Error codes: 400, 401, 403, 404, 429, 500, 503
- cURL configuration with 30-second timeout
- Phone number formatting (add country code 34)
- Complete cURL examples for testing
- PHP function reference
- C# integration guide
- Logging strategy
- Security considerations

### Next Step Preview (Step 7)

**Step 7**: Document UAZAPI `/send/menu` endpoint

This will cover:
- Menu/button message endpoint specification
- Payload format with `type: "button"`
- Choices array format: `["Label|URL"]`
- Interactive button functionality
- Fallback strategy to `/send/text`

### System State After This Step

- **Documentation Created**: Complete `/send/text` endpoint specification
- **Ready For**: Step 7 (/send/menu endpoint documentation)
- **Integration Pattern**: Clear understanding of text message API
- **C# Migration**: Template code provided for BotGenerator integration

---

## Verification

- [x] Step 6 requirements reviewed from steps file
- [x] Step 5 output analyzed for UAZAPI implementation details
- [x] Endpoint URL format documented: `{UAZAPI_URL}/send/text?token={TOKEN}`
- [x] HTTP method documented: POST
- [x] Content-Type documented: application/json
- [x] Payload structure documented: `{ number: string, text: string }`
- [x] Response format documented with success and error examples
- [x] Error codes documented: 400, 401, 403, 404, 429, 500, 503
- [x] cURL configuration documented (30s timeout, JSON headers)
- [x] Complete cURL examples provided (basic, confirmation, reminder, cancellation)
- [x] Phone number formatting documented (country code 34 prefix)
- [x] PHP function reference included (`sendUazApiWhatsApp()`)
- [x] C# integration template provided
- [x] Logging strategy documented
- [x] Security considerations included
- [x] Use cases mapped to current system
- [x] Comparison with `/send/menu` endpoint provided
- [x] WhatsApp markdown formatting documented
- [x] Credential loading priority documented (.env → environment variables)
- [x] Multi-recipient pattern explained (staff notifications)
- [x] Error handling patterns documented (cURL, HTTP, exceptions)
- [x] Complete function flow provided
- [x] No issues or blockers encountered
- [x] Output file created at correct path
- [x] Status marked as COMPLETED
