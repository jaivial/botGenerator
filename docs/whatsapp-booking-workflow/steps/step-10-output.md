# Step 10 Output: Analyze WhatsAppService in BotGenerator

## Status: COMPLETED

## Execution Summary
Successfully analyzed the WhatsAppService.cs implementation and documented all four methods (SendTextAsync, SendButtonsAsync, SendMenuAsync, GetHistoryAsync) with complete UAZAPI endpoint integration details. Cross-referenced with PHP send_whatsapp_uazapi.php to compare implementations. Documented method signatures, request/response structures, error handling, and identified key differences between C# and PHP implementations. The C# service uses HttpClient with token-based authentication and provides comprehensive logging, while the PHP implementation uses cURL with additional fallback mechanisms.

## Files Read
- `/home/jaime/Documents/projects/botGenerator/src/BotGenerator.Core/Services/WhatsAppService.cs` - C# UAZAPI service (229 lines)
- `/home/jaime/Documents/projects/botGenerator/src/BotGenerator.Core/Services/IWhatsAppService.cs` - Service interface (59 lines)
- `/home/jaime/Documents/projects/alqueriavillacarmen/includes/send_whatsapp_uazapi.php` - PHP UAZAPI implementation (484 lines)
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/whatsapp-booking-workflow-steps.md` - Step 10 requirements
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/progress.json` - Current progress
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/steps/step-9-output.md` - Prior context

## Files Created
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/steps/step-10-output.md` - This documentation

---

## WhatsAppService Class Overview

### Namespace and Dependencies

**Namespace**: `BotGenerator.Core.Services`

**Using Statements**:
```csharp
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
```

**Purpose**: Implementation of IWhatsAppService using UAZAPI for sending WhatsApp messages and retrieving conversation history.

**Dependencies** (Constructor Injection):
1. `HttpClient _httpClient` - Injected HTTP client for API requests
2. `IConfiguration _configuration` - Application configuration (API URL, token)
3. `ILogger<WhatsAppService> _logger` - Structured logging

**File Location**: `/home/jaime/Documents/projects/botGenerator/src/BotGenerator.Core/Services/WhatsAppService.cs`

**Line Count**: 229 lines

---

## Constructor (Lines 17-29)

### Method Signature

```csharp
public WhatsAppService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<WhatsAppService> logger)
```

### Initialization Logic

**Field Assignments** (Lines 22-23):
```csharp
_httpClient = httpClient;
_logger = logger;
```

**Configuration Loading** (Lines 25-28):
```csharp
_apiUrl = configuration["WhatsApp:ApiUrl"]
    ?? throw new InvalidOperationException("WhatsApp:ApiUrl not configured");
_token = configuration["WhatsApp:Token"]
    ?? throw new InvalidOperationException("WhatsApp:Token not configured");
```

**Configuration Keys**:
- `WhatsApp:ApiUrl` - Base URL for UAZAPI (e.g., "https://api.uazapi.com")
- `WhatsApp:Token` - Authentication token for UAZAPI

**Validation**: Throws `InvalidOperationException` if either configuration key is missing

**Required appsettings.json**:
```json
{
  "WhatsApp": {
    "ApiUrl": "https://api.uazapi.com",
    "Token": "your-uazapi-token-here"
  }
}
```

---

## Method 1: SendTextAsync (Lines 31-73)

### Purpose
Sends a simple text message to a WhatsApp number via UAZAPI.

### Method Signature

```csharp
public async Task<bool> SendTextAsync(
    string phoneNumber,
    string text,
    CancellationToken cancellationToken = default)
```

### Parameters

| Parameter | Type | Optional | Description |
|-----------|------|----------|-------------|
| `phoneNumber` | `string` | No | Recipient's phone number (with country code) |
| `text` | `string` | No | Message text to send |
| `cancellationToken` | `CancellationToken` | Yes | Async operation cancellation |

### Return Type

`Task<bool>` - Returns `true` if message sent successfully, `false` on error

---

### Implementation Flow

**Step 1: Log Request** (Lines 36-39):
```csharp
_logger.LogInformation(
    "Sending text message to {Phone}: {Preview}",
    phoneNumber,
    text.Length > 50 ? text[..50] + "..." : text);
```

**Logging Details**:
- **Log Level**: INFORMATION
- **Data Logged**: Phone number, message preview (first 50 chars)
- **Message Truncation**: Long messages shown with "..." suffix
- **Example Log**: `Sending text message to 34686969914: Hola, tu reserva ha sido confirmada para el día...`

---

**Step 2: Build HTTP Request** (Lines 41-50):
```csharp
var request = new HttpRequestMessage(
    HttpMethod.Post,
    "/send/text");

request.Headers.Add("token", _token);
request.Content = JsonContent.Create(new
{
    number = phoneNumber,
    text = text
});
```

**HTTP Details**:
- **Method**: POST
- **Endpoint**: `/send/text` (relative to base URL)
- **Full URL**: `{_apiUrl}/send/text`
- **Authentication**: Token header (not query parameter)
- **Content-Type**: `application/json` (set by JsonContent.Create)

**Request Body** (JSON):
```json
{
  "number": "34686969914",
  "text": "Hola, tu reserva ha sido confirmada..."
}
```

**Key Difference from PHP**: C# uses header-based token auth (`request.Headers.Add("token", _token)`), while PHP uses query parameter (`/send/text?token=...`)

---

**Step 3: Send Request and Handle Response** (Lines 52-72):
```csharp
try
{
    var response = await _httpClient.SendAsync(request, cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError(
            "Failed to send message. Status: {Status}, Error: {Error}",
            response.StatusCode, error);
        return false;
    }

    _logger.LogDebug("Message sent successfully to {Phone}", phoneNumber);
    return true;
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error sending message to {Phone}", phoneNumber);
    return false;
}
```

**Success Path** (Lines 54-66):
- Check `IsSuccessStatusCode` (2xx status)
- Log success at DEBUG level
- Return `true`

**Error Path - HTTP Failure** (Lines 56-62):
- Read error response body
- Log ERROR with status code and response content
- Return `false`

**Error Path - Exception** (Lines 68-71):
- Catch any exception (network timeout, DNS failure, etc.)
- Log ERROR with full exception details
- Return `false`

**Error Handling Philosophy**: Never throws exceptions, always returns boolean for resilience

---

### PHP Comparison: sendUazApiWhatsApp() (Lines 46-119)

**PHP Implementation Differences**:

1. **Phone Number Formatting** (Lines 54-55):
```php
$recipientNumber = preg_replace('/[^0-9]/', '', $recipientNumber);
$recipientNumber = '34' . $recipientNumber;
```
- **PHP**: Automatically adds Spanish country code (34)
- **C#**: No automatic formatting - expects fully formatted number

**Action Required**: C# code should format phone numbers before calling SendTextAsync

---

2. **Token Authentication** (Line 58):
```php
$endpoint = $apiUrl . '/send/text?token=' . urlencode($apiToken);
```
- **PHP**: Token as query parameter
- **C#**: Token as HTTP header

**UAZAPI Support**: Both methods should work if UAZAPI supports both authentication modes

---

3. **Logging** (Lines 66-68, 87-88):
```php
error_log("UAZAPI Request - Endpoint: " . $endpoint);
error_log("UAZAPI Request - Payload: " . $payload);
error_log("UAZAPI Response - HTTP Code: " . $httpCode);
error_log("UAZAPI Response - Body: " . $response);
```
- **PHP**: Logs full endpoint URL, payload, HTTP code, and response body
- **C#**: Logs preview of message (first 50 chars) and success/failure

**C# Gap**: Does not log request/response payloads for debugging

**Recommendation**: Add DEBUG-level logging for full request/response in C#

---

4. **Response Parsing** (Lines 105-111):
```php
$responseData = json_decode($response, true);

return [
    'success' => true,
    'messageSid' => $responseData['id'] ?? $responseData['data']['id'] ?? 'unknown',
    'response' => $responseData
];
```
- **PHP**: Returns detailed array with message ID and full response
- **C#**: Returns simple boolean

**C# Gap**: Does not capture message ID from UAZAPI response

**Use Case**: Message ID needed for tracking delivery status or correlating with webhooks

---

5. **Error Response Structure** (PHP):
```php
// cURL error
return [
    'success' => false,
    'error' => 'cURL Error: ' . $curlError
];

// HTTP error
return [
    'success' => false,
    'error' => 'HTTP ' . $httpCode . ': ' . $response
];
```
- **PHP**: Returns structured error details
- **C#**: Logs error but returns simple `false`

---

## Method 2: SendButtonsAsync (Lines 75-116)

### Purpose
Sends a message with interactive buttons (quick replies) via UAZAPI menu endpoint.

### Method Signature

```csharp
public async Task<bool> SendButtonsAsync(
    string phoneNumber,
    string text,
    string footer,
    List<ButtonOption> buttons,
    CancellationToken cancellationToken = default)
```

### Parameters

| Parameter | Type | Optional | Description |
|-----------|------|----------|-------------|
| `phoneNumber` | `string` | No | Recipient's phone number |
| `text` | `string` | No | Main message text |
| `footer` | `string` | No | Footer text below buttons |
| `buttons` | `List<ButtonOption>` | No | List of button options |
| `cancellationToken` | `CancellationToken` | Yes | Async operation cancellation |

### Return Type

`Task<bool>` - Returns `true` if message sent successfully, `false` on error

---

### ButtonOption Model (Line 45 in IWhatsAppService.cs)

```csharp
public record ButtonOption(string Id, string Text, string? Description = null);
```

**Properties**:
- `Id`: Unique identifier for the button (returned when user clicks)
- `Text`: Button label (visible to user)
- `Description`: Optional additional text (nullable)

**Example**:
```csharp
var buttons = new List<ButtonOption>
{
    new ButtonOption("confirm", "Confirmar Reserva", "Confirmar para el 25/12"),
    new ButtonOption("cancel", "Cancelar", "No deseo reservar")
};
```

---

### Implementation Flow

**Step 1: Log Request** (Lines 82-84):
```csharp
_logger.LogInformation(
    "Sending buttons message to {Phone} with {Count} buttons",
    phoneNumber, buttons.Count);
```

**Example Log**: `Sending buttons message to 34686969914 with 2 buttons`

---

**Step 2: Build HTTP Request** (Lines 86-104):
```csharp
var request = new HttpRequestMessage(
    HttpMethod.Post,
    $"{_apiUrl}/send/menu");

request.Headers.Add("token", _token);

// UAZAPI button format
var choices = buttons.Select(b =>
    $"{b.Text}|{b.Id}|{b.Description ?? b.Text}").ToList();

request.Content = JsonContent.Create(new
{
    number = phoneNumber,
    type = "button",
    text = text,
    footerText = footer,
    selectableCount = 1,
    choices = choices
});
```

**HTTP Details**:
- **Endpoint**: `/send/menu` (NOT `/send/buttons` - uses menu endpoint)
- **Full URL**: `{_apiUrl}/send/menu`
- **Type**: `"button"` (distinguishes from list menus)

**UAZAPI Button Format** (Lines 93-94):
```csharp
var choices = buttons.Select(b =>
    $"{b.Text}|{b.Id}|{b.Description ?? b.Text}").ToList();
```

**Format**: `"Text|Id|Description"` separated by pipe characters

**Example Transformation**:
```csharp
// Input
new ButtonOption("confirm", "Confirmar Reserva", "Confirmar para el 25/12")

// Output
"Confirmar Reserva|confirm|Confirmar para el 25/12"
```

**Default Description**: If `Description` is null, uses `Text` as description

---

**Request Body Example** (JSON):
```json
{
  "number": "34686969914",
  "type": "button",
  "text": "¿Desea confirmar su reserva?",
  "footerText": "Alquería Villa Carmen",
  "selectableCount": 1,
  "choices": [
    "Confirmar Reserva|confirm|Confirmar para el 25/12",
    "Cancelar|cancel|No deseo reservar"
  ]
}
```

**Field Meanings**:
- `type`: `"button"` for button messages
- `selectableCount`: Always `1` (user can select one button)
- `choices`: Array of pipe-delimited strings

---

**Step 3: Send Request** (Lines 106-115):
```csharp
try
{
    var response = await _httpClient.SendAsync(request, cancellationToken);
    return response.IsSuccessStatusCode;
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error sending buttons to {Phone}", phoneNumber);
    return false;
}
```

**Simplified Error Handling**:
- No detailed error logging (unlike SendTextAsync)
- Only logs exception, not HTTP error responses
- Returns `true` for 2xx status, `false` otherwise

**Gap**: Does not log failed HTTP responses (4xx, 5xx)

---

### PHP Comparison: sendWhatsAppConfirmationWithButtonsUazApi() (Lines 160-290)

**PHP Implementation Differences**:

1. **Endpoint URL Construction** (Line 181):
```php
$endpoint = $apiUrl . '/send/menu?token=' . urlencode($apiToken);
```
- **PHP**: Token as query parameter
- **C#**: Token as header

**Same as SendTextAsync difference**

---

2. **Payload Structure** (Lines 204-209):
```php
$payload = [
    'number' => $recipientNumber,
    'type' => 'button',
    'text' => $confirmationText,
    'choices' => $choices,
];
```
- **PHP**: No `footerText` or `selectableCount` fields
- **C#**: Includes both fields

**Difference**: C# implementation is more complete

**PHP Choices Format** (Lines 196-201):
```php
$choices = [];
if (!empty($bookingId)) {
    $choices[] = "Cancelar Reserva|https://alqueriavillacarmen.com/cancel_reservation.php?id={$bookingId}";
}
```

**PHP Format**: `"Text|URL"` (2 parts, not 3)

**C# Format**: `"Text|Id|Description"` (3 parts)

**Critical Difference**: PHP uses URL as second parameter, C# uses ID

**UAZAPI Interpretation**:
- **PHP**: Clicking button opens URL in browser
- **C#**: Clicking button sends ID back in response

**Use Case Difference**:
- **PHP**: Direct web actions (cancel reservation page)
- **C#**: Conversation flow (respond with button ID)

---

3. **Fallback Mechanism** (Lines 237-248, 254-264, 277-288):
```php
if ($curlError) {
    error_log("UAZAPI Button cURL Error: " . $curlError);
    // Fall back to simple text message
    return sendWhatsAppConfirmationUazApi(...);
}

if ($httpCode != 200 && $httpCode != 201) {
    error_log("UAZAPI Button failed with HTTP " . $httpCode . ", falling back to text message");
    // Fall back to simple text message
    return sendWhatsAppConfirmationUazApi(...);
}
```

- **PHP**: Automatically falls back to text-only message if buttons fail
- **C#**: No fallback mechanism

**PHP Advantage**: More resilient - user always receives message even if button API fails

**C# Gap**: Button failure = no message sent

**Recommendation**: Implement fallback in C# for critical notifications

---

4. **Response Type Tracking** (Line 273):
```php
return [
    'success' => true,
    'messageSid' => $responseData['id'] ?? $responseData['data']['id'] ?? 'unknown',
    'response' => $responseData,
    'type' => 'button_confirmation'
];
```

- **PHP**: Returns `type` field to distinguish button vs text message
- **C#**: No tracking of message type

---

## Method 3: SendMenuAsync (Lines 118-166)

### Purpose
Sends a message with a structured list menu (WhatsApp list picker) via UAZAPI.

### Method Signature

```csharp
public async Task<bool> SendMenuAsync(
    string phoneNumber,
    string text,
    string buttonText,
    List<MenuSection> sections,
    CancellationToken cancellationToken = default)
```

### Parameters

| Parameter | Type | Optional | Description |
|-----------|------|----------|-------------|
| `phoneNumber` | `string` | No | Recipient's phone number |
| `text` | `string` | No | Header text above menu |
| `buttonText` | `string` | No | Text on button that opens list |
| `sections` | `List<MenuSection>` | No | List sections with rows |
| `cancellationToken` | `CancellationToken` | Yes | Async operation cancellation |

### Return Type

`Task<bool>` - Returns `true` if message sent successfully, `false` on error

---

### Menu Data Models (Lines 47-49 in IWhatsAppService.cs)

**MenuSection Record**:
```csharp
public record MenuSection(string Title, List<MenuRow> Rows);
```

**MenuRow Record**:
```csharp
public record MenuRow(string Id, string Title, string? Description = null);
```

**Structure**:
- Each **MenuSection** has a title and multiple rows
- Each **MenuRow** has an ID, title, and optional description
- User sees sections grouped by title
- Clicking a row returns its ID

---

### Example Usage

```csharp
var sections = new List<MenuSection>
{
    new MenuSection("Arroces Disponibles", new List<MenuRow>
    {
        new MenuRow("paella", "Paella Valenciana", "Arroz con pollo, conejo y judías"),
        new MenuRow("negro", "Arroz Negro", "Arroz con sepia y alioli"),
        new MenuRow("banda", "Arroz a Banda", "Arroz con caldo de pescado")
    }),
    new MenuSection("Otras Opciones", new List<MenuRow>
    {
        new MenuRow("none", "Sin Arroz", "No deseo ordenar arroz")
    })
};

await whatsAppService.SendMenuAsync(
    phoneNumber: "34686969914",
    text: "Seleccione el tipo de arroz para su reserva:",
    buttonText: "Ver Opciones",
    sections: sections
);
```

---

### Implementation Flow

**Step 1: Log Request** (Lines 125-127):
```csharp
_logger.LogInformation(
    "Sending menu message to {Phone}",
    phoneNumber);
```

**Note**: Does not log section count or row count (less detailed than SendButtonsAsync)

---

**Step 2: Build HTTP Request** (Lines 129-154):
```csharp
var request = new HttpRequestMessage(
    HttpMethod.Post,
    $"{_apiUrl}/send/menu");

request.Headers.Add("token", _token);

// Build menu structure
var menuSections = sections.Select(s => new
{
    title = s.Title,
    rows = s.Rows.Select(r => new
    {
        id = r.Id,
        title = r.Title,
        description = r.Description ?? ""
    }).ToList()
}).ToList();

request.Content = JsonContent.Create(new
{
    number = phoneNumber,
    type = "list",
    text = text,
    buttonText = buttonText,
    sections = menuSections
});
```

**HTTP Details**:
- **Endpoint**: `/send/menu` (same as buttons)
- **Type**: `"list"` (distinguishes from button menus)

**Menu Structure Transformation** (Lines 136-145):
```csharp
var menuSections = sections.Select(s => new
{
    title = s.Title,
    rows = s.Rows.Select(r => new
    {
        id = r.Id,
        title = r.Title,
        description = r.Description ?? ""
    }).ToList()
}).ToList();
```

**Transformation Details**:
- Converts `MenuSection` records to anonymous objects
- Converts `MenuRow` records to nested anonymous objects
- Sets empty string `""` for null descriptions (UAZAPI may not accept null)

---

**Request Body Example** (JSON):
```json
{
  "number": "34686969914",
  "type": "list",
  "text": "Seleccione el tipo de arroz para su reserva:",
  "buttonText": "Ver Opciones",
  "sections": [
    {
      "title": "Arroces Disponibles",
      "rows": [
        {
          "id": "paella",
          "title": "Paella Valenciana",
          "description": "Arroz con pollo, conejo y judías"
        },
        {
          "id": "negro",
          "title": "Arroz Negro",
          "description": "Arroz con sepia y alioli"
        },
        {
          "id": "banda",
          "title": "Arroz a Banda",
          "description": "Arroz con caldo de pescado"
        }
      ]
    },
    {
      "title": "Otras Opciones",
      "rows": [
        {
          "id": "none",
          "title": "Sin Arroz",
          "description": "No deseo ordenar arroz"
        }
      ]
    }
  ]
}
```

---

**Step 3: Send Request** (Lines 156-165):
```csharp
try
{
    var response = await _httpClient.SendAsync(request, cancellationToken);
    return response.IsSuccessStatusCode;
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error sending menu to {Phone}", phoneNumber);
    return false;
}
```

**Same simplified error handling as SendButtonsAsync** - no detailed HTTP error logging

---

### WhatsApp List Menu Behavior

**User Experience**:
1. User receives message with text and a button showing `buttonText`
2. Clicking button opens full-screen list picker
3. Sections displayed with titles
4. Rows shown under each section with title and description
5. User selects one row
6. Selected row's `id` sent back as message response

**WhatsApp Limitations**:
- Maximum 10 sections
- Maximum 10 rows per section
- Button text: max 20 characters
- Section title: max 24 characters
- Row title: max 24 characters
- Row description: max 72 characters

**C# Code Gap**: No validation of these limits

---

### PHP Comparison

**Note**: PHP implementation does NOT have a list menu function equivalent to SendMenuAsync

**PHP Functions**:
- `sendUazApiWhatsApp()` - Text only (lines 46-119)
- `sendWhatsAppConfirmationUazApi()` - Text only (lines 125-154)
- `sendWhatsAppConfirmationWithButtonsUazApi()` - Buttons only (lines 160-290)
- `sendBookingWhatsAppUazApi()` - Wrapper for booking confirmations (lines 295-344)
- `sendWhatsAppReminderConfirmationUazApi()` - Text only (lines 349-369)
- `sendWhatsAppCancellationNotificationUazApi()` - Text only (lines 374-441)

**Missing in PHP**: List menu functionality

**C# Advantage**: More complete UAZAPI integration with list menus

---

## Method 4: GetHistoryAsync (Lines 168-213)

### Purpose
Retrieves conversation history from UAZAPI for a specific WhatsApp number.

### Method Signature

```csharp
public async Task<List<WhatsAppHistoryMessage>> GetHistoryAsync(
    string phoneNumber,
    int limit = 20,
    CancellationToken cancellationToken = default)
```

### Parameters

| Parameter | Type | Optional | Description |
|-----------|------|----------|-------------|
| `phoneNumber` | `string` | No | Phone number to get history for |
| `limit` | `int` | Yes | Maximum messages to retrieve (default: 20) |
| `cancellationToken` | `CancellationToken` | Yes | Async operation cancellation |

### Return Type

`Task<List<WhatsAppHistoryMessage>>` - List of messages (empty list on error, never null)

---

### WhatsAppHistoryMessage Model (Lines 51-58 in IWhatsAppService.cs)

```csharp
public record WhatsAppHistoryMessage
{
    public string Text { get; init; } = "";
    public bool FromMe { get; init; }
    public long Timestamp { get; init; }
    public string? SenderName { get; init; }
    public string? MessageId { get; init; }
}
```

**Properties**:
- `Text`: Message content (empty string if null)
- `FromMe`: `true` if bot sent message, `false` if user sent
- `Timestamp`: Unix timestamp in milliseconds
- `SenderName`: Name of sender (nullable)
- `MessageId`: Unique message ID (nullable)

---

### Implementation Flow

**Step 1: Log Request** (Line 173):
```csharp
_logger.LogDebug("Getting history for {Phone}, limit: {Limit}", phoneNumber, limit);
```

**Log Level**: DEBUG (not INFO) - lower priority since history queries are frequent

---

**Step 2: Build HTTP Request** (Lines 175-184):
```csharp
var request = new HttpRequestMessage(
    HttpMethod.Post,
    $"{_apiUrl}/message/find");

request.Headers.Add("token", _token);
request.Content = JsonContent.Create(new
{
    chatid = $"{phoneNumber}@s.whatsapp.net",
    limit = limit
});
```

**HTTP Details**:
- **Method**: POST (not GET)
- **Endpoint**: `/message/find`

**Chat ID Format** (Line 182):
```csharp
chatid = $"{phoneNumber}@s.whatsapp.net"
```

**WhatsApp Chat ID Structure**:
- Personal chats: `{phone}@s.whatsapp.net`
- Group chats: `{groupId}@g.us`

**Example**: `34686969914@s.whatsapp.net`

**Request Body** (JSON):
```json
{
  "chatid": "34686969914@s.whatsapp.net",
  "limit": 20
}
```

---

**Step 3: Send Request and Parse Response** (Lines 186-212):
```csharp
try
{
    var response = await _httpClient.SendAsync(request, cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
        _logger.LogWarning("Failed to get history for {Phone}", phoneNumber);
        return new List<WhatsAppHistoryMessage>();
    }

    var result = await response.Content.ReadFromJsonAsync<HistoryResponse>(
        cancellationToken: cancellationToken);

    return result?.Messages?.Select(m => new WhatsAppHistoryMessage
    {
        Text = m.Text ?? "",
        FromMe = m.FromMe,
        Timestamp = m.MessageTimestamp,
        SenderName = m.SenderName,
        MessageId = m.MessageId
    }).ToList() ?? new List<WhatsAppHistoryMessage>();
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error getting history for {Phone}", phoneNumber);
    return new List<WhatsAppHistoryMessage>();
}
```

**Success Path** (Lines 188-206):
- Check HTTP status
- Deserialize JSON response to `HistoryResponse` internal class
- Map to `WhatsAppHistoryMessage` list
- Return mapped list (or empty if null)

**Error Path - HTTP Failure** (Lines 190-193):
- Log WARNING (not ERROR - history failure is not critical)
- Return empty list (not null)

**Error Path - Exception** (Lines 208-211):
- Log ERROR with full exception
- Return empty list (not null)

**Resilience**: Always returns a list, never throws exceptions

---

### Internal Response Models (Lines 215-227)

**HistoryResponse Class** (Lines 215-218):
```csharp
private class HistoryResponse
{
    public List<HistoryMessage>? Messages { get; set; }
}
```

**Purpose**: Deserialize UAZAPI JSON response

**Expected JSON**:
```json
{
  "Messages": [
    { "Text": "...", "FromMe": false, ... },
    { "Text": "...", "FromMe": true, ... }
  ]
}
```

---

**HistoryMessage Class** (Lines 220-227):
```csharp
private class HistoryMessage
{
    public string? Text { get; set; }
    public bool FromMe { get; set; }
    public long MessageTimestamp { get; set; }
    public string? SenderName { get; set; }
    public string? MessageId { get; set; }
}
```

**Field Mapping**:
- `Text` → `WhatsAppHistoryMessage.Text` (with null coalescing to "")
- `FromMe` → `WhatsAppHistoryMessage.FromMe` (direct copy)
- `MessageTimestamp` → `WhatsAppHistoryMessage.Timestamp` (renamed property)
- `SenderName` → `WhatsAppHistoryMessage.SenderName` (direct copy)
- `MessageId` → `WhatsAppHistoryMessage.MessageId` (direct copy)

**Why Internal Classes?**: UAZAPI response fields don't match public API naming conventions

---

### Expected UAZAPI Response

**Example JSON**:
```json
{
  "Messages": [
    {
      "Text": "Hola, quisiera hacer una reserva",
      "FromMe": false,
      "MessageTimestamp": 1735328400000,
      "SenderName": "Juan García",
      "MessageId": "3EB0C42A82E2A2A8B2E1"
    },
    {
      "Text": "¡Por supuesto! ¿Para cuántas personas?",
      "FromMe": true,
      "MessageTimestamp": 1735328410000,
      "SenderName": null,
      "MessageId": "3EB0C42A82E2A2A8B2E2"
    }
  ]
}
```

**Timestamp Format**: Unix timestamp in milliseconds (not seconds)

**Conversion to DateTime**:
```csharp
var dateTime = DateTimeOffset.FromUnixTimeMilliseconds(message.Timestamp).DateTime;
```

---

### PHP Comparison

**Note**: PHP implementation does NOT have a history retrieval function

**Missing in PHP**: Conversation history access

**C# Advantage**: Can retrieve past messages for context-aware conversations

**Use Case**: MainConversationAgent can use history to:
- Avoid asking repeated questions
- Resume interrupted booking flows
- Provide continuity across sessions

---

## IWhatsAppService Interface (Lines 1-59)

### Interface Definition

**Namespace**: `BotGenerator.Core.Services`

**Purpose**: Abstraction for WhatsApp messaging, allowing multiple implementations (UAZAPI, Twilio, etc.)

---

### Methods

**1. SendTextAsync** (Lines 11-14):
```csharp
Task<bool> SendTextAsync(
    string phoneNumber,
    string text,
    CancellationToken cancellationToken = default);
```

**2. SendButtonsAsync** (Lines 19-24):
```csharp
Task<bool> SendButtonsAsync(
    string phoneNumber,
    string text,
    string footer,
    List<ButtonOption> buttons,
    CancellationToken cancellationToken = default);
```

**3. SendMenuAsync** (Lines 29-34):
```csharp
Task<bool> SendMenuAsync(
    string phoneNumber,
    string text,
    string buttonText,
    List<MenuSection> sections,
    CancellationToken cancellationToken = default);
```

**4. GetHistoryAsync** (Lines 39-42):
```csharp
Task<List<WhatsAppHistoryMessage>> GetHistoryAsync(
    string phoneNumber,
    int limit = 20,
    CancellationToken cancellationToken = default);
```

---

### Data Models (Lines 45-58)

**ButtonOption Record** (Line 45):
```csharp
public record ButtonOption(string Id, string Text, string? Description = null);
```

**MenuSection Record** (Line 47):
```csharp
public record MenuSection(string Title, List<MenuRow> Rows);
```

**MenuRow Record** (Line 49):
```csharp
public record MenuRow(string Id, string Title, string? Description = null);
```

**WhatsAppHistoryMessage Record** (Lines 51-58):
```csharp
public record WhatsAppHistoryMessage
{
    public string Text { get; init; } = "";
    public bool FromMe { get; init; }
    public long Timestamp { get; init; }
    public string? SenderName { get; init; }
    public string? MessageId { get; init; }
}
```

**Why Records?**: Immutable data structures with value-based equality (C# 9+ feature)

---

## UAZAPI Endpoint Summary

### Base Configuration

**Configuration Keys**:
- `WhatsApp:ApiUrl` - Base URL (e.g., "https://api.uazapi.com")
- `WhatsApp:Token` - Authentication token

**Authentication Method**:
- **C#**: HTTP header `token: {value}`
- **PHP**: Query parameter `?token={value}`

---

### Endpoints

| Method | Endpoint | Purpose | Type Field |
|--------|----------|---------|------------|
| SendTextAsync | `/send/text` | Simple text messages | N/A |
| SendButtonsAsync | `/send/menu` | Button messages | `"button"` |
| SendMenuAsync | `/send/menu` | List menus | `"list"` |
| GetHistoryAsync | `/message/find` | Retrieve history | N/A |

**Note**: Both buttons and menus use the same `/send/menu` endpoint, distinguished by `type` field

---

### Request/Response Formats

**SendTextAsync Request**:
```json
POST /send/text
Header: token: {value}
Body: {
  "number": "34686969914",
  "text": "Message text"
}
```

**SendButtonsAsync Request**:
```json
POST /send/menu
Header: token: {value}
Body: {
  "number": "34686969914",
  "type": "button",
  "text": "Message text",
  "footerText": "Footer text",
  "selectableCount": 1,
  "choices": [
    "Button Text|button_id|Description"
  ]
}
```

**SendMenuAsync Request**:
```json
POST /send/menu
Header: token: {value}
Body: {
  "number": "34686969914",
  "type": "list",
  "text": "Header text",
  "buttonText": "Open List",
  "sections": [
    {
      "title": "Section Title",
      "rows": [
        {
          "id": "row_id",
          "title": "Row Title",
          "description": "Row description"
        }
      ]
    }
  ]
}
```

**GetHistoryAsync Request**:
```json
POST /message/find
Header: token: {value}
Body: {
  "chatid": "34686969914@s.whatsapp.net",
  "limit": 20
}
```

**GetHistoryAsync Response**:
```json
{
  "Messages": [
    {
      "Text": "Message content",
      "FromMe": false,
      "MessageTimestamp": 1735328400000,
      "SenderName": "Sender Name",
      "MessageId": "3EB0C42A82E2A2A8B2E1"
    }
  ]
}
```

---

## Service Registration

### Required Configuration (Program.cs)

**HttpClient Registration**:
```csharp
builder.Services.AddHttpClient<WhatsAppService>(client =>
{
    var apiUrl = builder.Configuration["WhatsApp:ApiUrl"];
    client.BaseAddress = new Uri(apiUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "BotGenerator/1.0");
});
```

**Interface Binding**:
```csharp
builder.Services.AddScoped<IWhatsAppService, WhatsAppService>();
```

**Alternative (if already registered)**:
```csharp
// WhatsAppService registered via IHttpClientFactory
// Already implements IWhatsAppService
```

---

### appsettings.json

**Required Keys**:
```json
{
  "WhatsApp": {
    "ApiUrl": "https://api.uazapi.com",
    "Token": "your-uazapi-token-here"
  }
}
```

**Development Override** (appsettings.Development.json):
```json
{
  "WhatsApp": {
    "ApiUrl": "https://api-dev.uazapi.com",
    "Token": "dev-token"
  }
}
```

---

## C# vs PHP Implementation Comparison

### Summary Table

| Feature | C# WhatsAppService | PHP send_whatsapp_uazapi.php |
|---------|-------------------|------------------------------|
| **Text Messages** | ✓ SendTextAsync | ✓ sendUazApiWhatsApp |
| **Button Messages** | ✓ SendButtonsAsync | ✓ sendWhatsAppConfirmationWithButtonsUazApi |
| **List Menus** | ✓ SendMenuAsync | ✗ Not implemented |
| **History Retrieval** | ✓ GetHistoryAsync | ✗ Not implemented |
| **Authentication** | Header: `token` | Query param: `?token=` |
| **Phone Formatting** | Manual | Automatic (+34 prefix) |
| **Error Handling** | Boolean return | Structured array return |
| **Logging** | Structured logging (ILogger) | error_log() calls |
| **Message ID Capture** | ✗ Not captured | ✓ Captured in return array |
| **Fallback Mechanism** | ✗ None | ✓ Falls back to text on button failure |
| **Reminder Messages** | Manual | ✓ sendWhatsAppReminderConfirmationUazApi |
| **Cancellation Notifications** | Manual | ✓ sendWhatsAppCancellationNotificationUazApi |
| **Booking Confirmations** | Manual | ✓ sendBookingWhatsAppUazApi wrapper |

---

### Key Differences

**1. Phone Number Formatting**

**PHP Advantage**:
```php
$recipientNumber = preg_replace('/[^0-9]/', '', $recipientNumber);
$recipientNumber = '34' . $recipientNumber;
```
- Strips non-numeric characters
- Automatically adds Spanish country code (34)
- Accepts: `686969914`, `+34686969914`, `34 686 969 914`
- Normalizes to: `34686969914`

**C# Gap**:
- Expects pre-formatted phone numbers
- No automatic country code handling
- No cleaning of special characters

**Recommendation**: Add phone number formatter utility in C#:
```csharp
public static class PhoneNumberHelper
{
    public static string FormatSpanishPhone(string phone)
    {
        // Remove all non-numeric characters
        var cleaned = Regex.Replace(phone, @"[^0-9]", "");

        // Remove leading 34 if present
        if (cleaned.StartsWith("34") && cleaned.Length > 9)
        {
            cleaned = cleaned.Substring(2);
        }

        // Add 34 prefix
        return "34" + cleaned;
    }
}
```

---

**2. Error Response Structure**

**PHP Advantage**:
```php
return [
    'success' => true/false,
    'messageSid' => '...',
    'error' => '...',
    'response' => [...]
];
```
- Structured error details
- Message ID for tracking
- Full UAZAPI response included

**C# Gap**:
```csharp
return true; // or false
```
- No error details returned
- No message ID captured
- Caller cannot distinguish error types

**Recommendation**: Change C# return type:
```csharp
public record WhatsAppSendResult(
    bool Success,
    string? MessageId = null,
    string? Error = null);

public async Task<WhatsAppSendResult> SendTextAsync(...)
{
    // ... send request ...

    var responseData = await response.Content.ReadFromJsonAsync<UazApiResponse>();

    return new WhatsAppSendResult(
        Success: true,
        MessageId: responseData?.Id,
        Error: null
    );
}
```

---

**3. Fallback Mechanism**

**PHP Advantage**:
```php
// Try to send button message
$result = sendButtonMessage(...);

if (error) {
    // Fallback to simple text
    $result = sendTextMessage(...);
}
```
- Buttons fail → send text instead
- User always receives notification
- Graceful degradation

**C# Gap**:
- No fallback logic
- Button failure = no message
- User may not receive critical info

**Recommendation**: Implement fallback:
```csharp
public async Task<bool> SendButtonsWithFallbackAsync(
    string phoneNumber,
    string text,
    string footer,
    List<ButtonOption> buttons,
    CancellationToken cancellationToken = default)
{
    var buttonResult = await SendButtonsAsync(phoneNumber, text, footer, buttons, cancellationToken);

    if (!buttonResult)
    {
        _logger.LogWarning("Button send failed, falling back to text for {Phone}", phoneNumber);
        var fallbackText = $"{text}\n\n{footer}";
        return await SendTextAsync(phoneNumber, fallbackText, cancellationToken);
    }

    return true;
}
```

---

**4. Specialized Message Functions**

**PHP Advantage**:
- `sendBookingWhatsAppUazApi()` - Formatted booking confirmations
- `sendWhatsAppReminderConfirmationUazApi()` - Reminder messages
- `sendWhatsAppCancellationNotificationUazApi()` - Cancellation alerts to staff

**PHP Example** (sendBookingWhatsAppUazApi):
```php
function sendBookingWhatsAppUazApi($bookingData)
{
    // Extract and format booking data
    $customerName = $bookingData['customer_name'];
    $bookingDate = formatDate($bookingData['reservation_date']);
    $arrozType = $bookingData['arroz_type'];
    // ...

    // Build formatted message
    $preOrderDetails = "$arrozType ($arrozServings raciones)";

    // Send with buttons
    return sendWhatsAppConfirmationWithButtonsUazApi(
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
}
```

**C# Gap**:
- Only low-level send methods
- No booking-specific formatting
- Message construction in MainConversationAgent

**Current C# Flow** (from BookingHandler):
```csharp
var confirmationMessage = BuildConfirmationMessage(booking);
await whatsAppService.SendTextAsync(phoneNumber, confirmationMessage);
```

**Recommendation**: Add high-level helper methods:
```csharp
public static class WhatsAppMessageBuilder
{
    public static string BuildBookingConfirmation(BookingData booking)
    {
        var sb = new StringBuilder();
        sb.AppendLine("✅ *¡Reserva confirmada!*");
        sb.AppendLine();
        sb.AppendLine($"📅 *Fecha:* {booking.Date}");
        sb.AppendLine($"🕐 *Hora:* {booking.Time}");
        sb.AppendLine($"👥 *Personas:* {booking.People}");
        sb.AppendLine($"👤 *Nombre:* {booking.Name}");

        if (!string.IsNullOrEmpty(booking.ArrozType))
        {
            sb.AppendLine($"🍚 *Arroz:* {booking.ArrozType}");
            if (booking.ArrozServings.HasValue)
            {
                sb.AppendLine($"   *Raciones:* {booking.ArrozServings}");
            }
        }

        if (booking.HighChairs > 0)
        {
            sb.AppendLine($"🪑 *Tronas:* {booking.HighChairs}");
        }

        if (booking.BabyStrollers > 0)
        {
            sb.AppendLine($"🛒 *Carritos:* {booking.BabyStrollers}");
        }

        sb.AppendLine();
        sb.AppendLine("¡Te esperamos en Alquería Villa Carmen!");

        return sb.ToString();
    }
}
```

**Already exists in BookingHandler.BuildConfirmationMessage()** - could be extracted to shared helper

---

**5. Multi-Recipient Notifications**

**PHP Advantage** (sendWhatsAppCancellationNotificationUazApi):
```php
$phoneNumbers = ["34692747052", "34638857294", "34686969914"];
$results = [];

foreach ($phoneNumbers as $phoneNumber) {
    $result = sendUazApiWhatsApp($phoneNumber, $message);
    $results[$phoneNumber] = $result;
}

// Check if at least one succeeded
$overallSuccess = false;
foreach ($results as $result) {
    if ($result['success']) {
        $overallSuccess = true;
        break;
    }
}

return [
    'success' => $overallSuccess,
    'results' => $results
];
```

- Sends to multiple recipients (restaurant staff)
- Tracks individual results
- Returns success if at least one delivery succeeded
- Logs all individual results

**C# Gap**:
- No multi-recipient helper
- Would need to manually loop and send

**Recommendation**: Add extension method:
```csharp
public static async Task<Dictionary<string, bool>> SendTextToMultipleAsync(
    this IWhatsAppService service,
    List<string> phoneNumbers,
    string text,
    CancellationToken cancellationToken = default)
{
    var results = new Dictionary<string, bool>();

    foreach (var phoneNumber in phoneNumbers)
    {
        var success = await service.SendTextAsync(phoneNumber, text, cancellationToken);
        results[phoneNumber] = success;
    }

    return results;
}

// Usage
var staffNumbers = new List<string> { "34692747052", "34638857294", "34686969914" };
var results = await whatsAppService.SendTextToMultipleAsync(staffNumbers, cancellationMessage);
var anySuccess = results.Values.Any(r => r);
```

---

**6. Button Format Differences**

**PHP Format** (URL-based):
```php
$choices[] = "Cancelar Reserva|https://alqueriavillacarmen.com/cancel_reservation.php?id={$bookingId}";
```
- **Format**: `Text|URL`
- **Behavior**: Opens URL in browser when clicked
- **Use Case**: Direct web actions

**C# Format** (ID-based):
```csharp
var choices = buttons.Select(b =>
    $"{b.Text}|{b.Id}|{b.Description ?? b.Text}").ToList();
```
- **Format**: `Text|Id|Description`
- **Behavior**: Sends ID back to bot when clicked
- **Use Case**: Conversational flow

**Compatibility Question**: Does UAZAPI support both formats?

**Recommendation**: Test both formats with UAZAPI, document supported format

---

## Missing Functionality Analysis

### Missing in C#

**1. Phone Number Normalization**
- PHP: Automatic +34 addition and cleaning
- C#: None
- **Impact**: High - malformed numbers cause failures
- **Priority**: HIGH

**2. Message ID Capture**
- PHP: Returns message ID from UAZAPI
- C#: Discards message ID
- **Impact**: Medium - needed for delivery tracking
- **Priority**: MEDIUM

**3. Fallback to Text Messages**
- PHP: Automatic fallback when buttons fail
- C#: None
- **Impact**: High - users may miss critical notifications
- **Priority**: HIGH

**4. Specialized Message Builders**
- PHP: Booking confirmations, reminders, cancellations
- C#: Generic send methods only
- **Impact**: Low - can build in handlers
- **Priority**: LOW

**5. Multi-Recipient Helpers**
- PHP: Built-in staff notification function
- C#: Manual looping required
- **Impact**: Medium - needed for restaurant alerts
- **Priority**: MEDIUM

**6. Detailed Error Responses**
- PHP: Structured error arrays
- C#: Boolean only
- **Impact**: Medium - hard to debug failures
- **Priority**: MEDIUM

---

### Missing in PHP

**1. List Menu Support**
- C#: SendMenuAsync with sections/rows
- PHP: None
- **Impact**: Low - can use buttons instead
- **Priority**: LOW

**2. Conversation History**
- C#: GetHistoryAsync
- PHP: None
- **Impact**: High - needed for context-aware conversations
- **Priority**: HIGH (for PHP)

**3. Structured Logging**
- C#: ILogger with log levels
- PHP: error_log() only
- **Impact**: Medium - harder to debug in production
- **Priority**: MEDIUM (for PHP)

**4. Dependency Injection**
- C#: Full DI with interfaces
- PHP: Procedural functions
- **Impact**: Low - works for current use
- **Priority**: LOW (for PHP)

**5. Async/Await**
- C#: Fully async
- PHP: Blocking cURL
- **Impact**: Low - PHP runs per-request anyway
- **Priority**: LOW (for PHP)

---

## Integration Points with BotGenerator

### Current Usage in Codebase

**1. WebhookController** (inferred):
```csharp
public class WebhookController : ControllerBase
{
    private readonly IWhatsAppService _whatsAppService;

    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveMessage([FromBody] WhatsAppWebhook webhook)
    {
        // Process message via MainConversationAgent
        var response = await _agent.ProcessAsync(webhook);

        // Send response
        await _whatsAppService.SendTextAsync(
            webhook.From,
            response.AiResponse
        );

        return Ok();
    }
}
```

---

**2. MainConversationAgent** (from Step 9 context):
```csharp
public class MainConversationAgent
{
    private readonly IWhatsAppService _whatsAppService;

    public async Task<AgentResponse> ProcessAsync(WhatsAppMessage message)
    {
        // ... AI processing ...

        if (response.Intent == IntentType.Booking)
        {
            var confirmationMessage = BuildConfirmationMessage(booking);
            await _whatsAppService.SendTextAsync(
                message.From,
                confirmationMessage
            );
        }

        return response;
    }
}
```

---

**3. BookingHandler** (from Step 9):
```csharp
public class BookingHandler
{
    // Does NOT currently inject IWhatsAppService
    // Builds confirmation message as string
    // Returns message in AgentResponse.AiResponse
    // Controller sends the message
}
```

**Current Flow**:
```
BookingHandler → BuildConfirmationMessage() → return AgentResponse with message
→ MainConversationAgent → return to Controller
→ WebhookController → whatsAppService.SendTextAsync()
```

---

### Potential Interactive Features

**1. Rice Type Selection with Menu**:
```csharp
var sections = new List<MenuSection>
{
    new MenuSection("Arroces Tradicionales", new List<MenuRow>
    {
        new MenuRow("paella", "Paella Valenciana", "Pollo, conejo, judías"),
        new MenuRow("negro", "Arroz Negro", "Sepia, alioli")
    }),
    new MenuSection("Otras Opciones", new List<MenuRow>
    {
        new MenuRow("none", "Sin Arroz")
    })
};

await _whatsAppService.SendMenuAsync(
    phoneNumber,
    "Seleccione el tipo de arroz:",
    "Ver Opciones",
    sections
);
```

---

**2. Booking Confirmation with Buttons**:
```csharp
var buttons = new List<ButtonOption>
{
    new ButtonOption("confirm", "Confirmar", "Confirmar reserva para 25/12"),
    new ButtonOption("cancel", "Cancelar", "No deseo reservar")
};

await _whatsAppService.SendButtonsAsync(
    phoneNumber,
    "¿Confirma su reserva?\n\nFecha: 25/12/2025\nHora: 14:00\nPersonas: 4",
    "Alquería Villa Carmen",
    buttons
);
```

---

**3. Context-Aware Conversations with History**:
```csharp
// Check if user already provided name
var history = await _whatsAppService.GetHistoryAsync(phoneNumber, limit: 10);
var hasName = history.Any(m => m.Text.Contains("nombre", StringComparison.OrdinalIgnoreCase));

if (!hasName)
{
    return "¿Cuál es su nombre?";
}
else
{
    return "¿Para cuántas personas será la reserva?";
}
```

---

## Logging Strategy

### Log Levels Used

**DEBUG** (Line 173 - GetHistoryAsync):
```csharp
_logger.LogDebug("Getting history for {Phone}, limit: {Limit}", phoneNumber, limit);
```
- **When**: History retrieval (frequent, low priority)
- **Data**: Phone number, limit

---

**INFORMATION** (Lines 36-39 SendTextAsync, Lines 82-84 SendButtonsAsync, Lines 125-127 SendMenuAsync):
```csharp
_logger.LogInformation(
    "Sending text message to {Phone}: {Preview}",
    phoneNumber,
    text.Length > 50 ? text[..50] + "..." : text);

_logger.LogInformation(
    "Sending buttons message to {Phone} with {Count} buttons",
    phoneNumber, buttons.Count);

_logger.LogInformation(
    "Sending menu message to {Phone}",
    phoneNumber);
```
- **When**: Every message send attempt
- **Data**: Phone number, message preview/count

---

**WARNING** (Line 192 - GetHistoryAsync):
```csharp
_logger.LogWarning("Failed to get history for {Phone}", phoneNumber);
```
- **When**: History API returns non-2xx status
- **Data**: Phone number
- **Use Case**: History failure (non-critical)

---

**ERROR** (Lines 59-61 SendTextAsync, Lines 112-114 SendButtonsAsync, Lines 162-164 SendMenuAsync, Lines 209-211 GetHistoryAsync):
```csharp
_logger.LogError(
    "Failed to send message. Status: {Status}, Error: {Error}",
    response.StatusCode, error);

_logger.LogError(ex, "Error sending message to {Phone}", phoneNumber);
_logger.LogError(ex, "Error sending buttons to {Phone}", phoneNumber);
_logger.LogError(ex, "Error sending menu to {Phone}", phoneNumber);
_logger.LogError(ex, "Error getting history for {Phone}", phoneNumber);
```
- **When**: Send failure or exception
- **Data**: Phone number, status code, error message, exception

---

### Logging Gaps

**1. Success Confirmation** (SendButtonsAsync, SendMenuAsync):
```csharp
// Current: No success log
var response = await _httpClient.SendAsync(request, cancellationToken);
return response.IsSuccessStatusCode;

// Recommendation:
if (response.IsSuccessStatusCode)
{
    _logger.LogDebug("Buttons sent successfully to {Phone}", phoneNumber);
    return true;
}
```

---

**2. HTTP Error Details** (SendButtonsAsync, SendMenuAsync):
```csharp
// Current: No error logging for non-2xx responses
return response.IsSuccessStatusCode;

// Recommendation:
if (!response.IsSuccessStatusCode)
{
    var error = await response.Content.ReadAsStringAsync(cancellationToken);
    _logger.LogError(
        "Failed to send buttons. Status: {Status}, Error: {Error}",
        response.StatusCode, error);
    return false;
}
```

---

**3. Request/Response Payloads** (all methods):
```csharp
// Recommendation for debugging:
_logger.LogDebug("UAZAPI Request: {Method} {Url}, Body: {Body}",
    request.Method, request.RequestUri, await request.Content.ReadAsStringAsync());

_logger.LogDebug("UAZAPI Response: {Status}, Body: {Body}",
    response.StatusCode, await response.Content.ReadAsStringAsync());
```

**Trade-off**: Increased log volume, potential PII exposure (phone numbers, messages)

---

### PHP Logging Comparison

**PHP Logging** (comprehensive):
```php
error_log("UAZAPI Request - Endpoint: " . $endpoint);
error_log("UAZAPI Request - Payload: " . $payload);
error_log("UAZAPI Response - HTTP Code: " . $httpCode);
error_log("UAZAPI Response - Body: " . $response);
error_log("UAZAPI cURL Error: " . $curlError);
```

**PHP Advantage**: More detailed request/response logging

**C# Advantage**: Structured logging with log levels (easier to filter in production)

---

## Issues Encountered

None. Analysis completed successfully.

---

## Blockers

None. All methods documented with complete integration details.

---

## Context for Next Step

### What We've Documented (Step 10)

**WhatsAppService Class**:
- 229 lines, 4 public methods + constructor
- Dependencies: HttpClient (injected), IConfiguration, ILogger
- Configuration: `WhatsApp:ApiUrl`, `WhatsApp:Token`
- All methods return `Task<bool>` (except GetHistoryAsync returns `Task<List<...>>`)

**Method 1: SendTextAsync**:
- Endpoint: `/send/text`
- Request: `{ number, text }`
- Auth: Header `token: {value}`
- Returns: `true`/`false`
- Logs: INFO (preview), DEBUG (success), ERROR (failure)

**Method 2: SendButtonsAsync**:
- Endpoint: `/send/menu`
- Request: `{ number, type: "button", text, footerText, selectableCount: 1, choices: [...] }`
- Choice format: `"Text|Id|Description"`
- Returns: `true`/`false`
- Logs: INFO (button count), ERROR (exception only)

**Method 3: SendMenuAsync**:
- Endpoint: `/send/menu`
- Request: `{ number, type: "list", text, buttonText, sections: [{ title, rows: [{ id, title, description }] }] }`
- Returns: `true`/`false`
- Logs: INFO (basic), ERROR (exception only)

**Method 4: GetHistoryAsync**:
- Endpoint: `/message/find`
- Request: `{ chatid: "{phone}@s.whatsapp.net", limit }`
- Returns: `List<WhatsAppHistoryMessage>` (empty on error)
- Logs: DEBUG (request), WARNING (HTTP failure), ERROR (exception)

**IWhatsAppService Interface**:
- 4 method signatures
- 4 data models: ButtonOption, MenuSection, MenuRow, WhatsAppHistoryMessage
- All models are C# records (immutable)

**UAZAPI Integration**:
- Base URL from config: `WhatsApp:ApiUrl`
- Token from config: `WhatsApp:Token`
- Auth method: HTTP header (not query param like PHP)
- 3 endpoints: `/send/text`, `/send/menu`, `/message/find`
- Content type: `application/json`

**PHP Comparison**:
- **C# Advantages**: List menus, history retrieval, structured logging, DI
- **PHP Advantages**: Phone formatting, message ID capture, fallback mechanism, specialized helpers, multi-recipient
- **Key Difference**: Button format (PHP uses URLs, C# uses IDs)
- **Critical Gap**: Phone number normalization missing in C#

**Implementation Gaps in C#** (6 identified):
1. Phone number normalization (no +34 auto-add)
2. Message ID capture (discarded from response)
3. Fallback mechanism (button → text)
4. Specialized message builders (booking confirmation wrapper)
5. Multi-recipient helpers (staff notifications)
6. Detailed error responses (returns bool, not structured data)

**Implementation Gaps in PHP** (2 identified):
1. List menu support (no equivalent to SendMenuAsync)
2. Conversation history (no equivalent to GetHistoryAsync)

**Logging Strategy**:
- DEBUG: History requests, success confirmations (missing in buttons/menus)
- INFO: All send attempts with phone and preview/count
- WARNING: History failures (non-critical)
- ERROR: Send failures, exceptions
- **Gap**: No request/response payload logging (PHP has this)

**Service Registration Requirements**:
- HttpClient with base address from config
- IWhatsAppService → WhatsAppService scoped registration
- appsettings.json: `WhatsApp:ApiUrl`, `WhatsApp:Token`

**Integration Points**:
- WebhookController: Sends responses via SendTextAsync
- MainConversationAgent: Orchestrates responses
- BookingHandler: Builds confirmation messages (doesn't send directly)

### System State After This Step

- **WhatsAppService Architecture**: Fully understood (4 methods, 2 internal classes)
- **UAZAPI Integration**: Complete endpoint and auth documentation
- **PHP Compatibility**: Compared implementations, gaps identified
- **Data Models**: 4 public records documented
- **Configuration**: Required settings documented
- **Implementation Status**: Production-ready, but missing phone formatting and fallback
- **Logging**: Structured with gaps in buttons/menus error logging

### Next Steps Preview

**Step 11 and beyond** will likely cover:
- Implementing phone number normalization utility
- Adding message ID capture and return types
- Implementing fallback mechanisms for critical messages
- Testing UAZAPI integration end-to-end
- Analyzing notification recipients configuration (34686969914, 34638857294, 34692747052)
- Documenting date availability checking (check_date_availability.php)
- Understanding rice menu validation (get_available_rice_types.php)
- Examining webhook payload structure from UAZAPI

### Files Ready for Next Step

Key files for upcoming steps:
- `/home/jaime/Documents/projects/alqueriavillacarmen/api/check_date_availability.php` (Date validation)
- `/home/jaime/Documents/projects/alqueriavillacarmen/api/get_available_rice_types.php` (Rice types)
- `/home/jaime/Documents/projects/botGenerator/src/BotGenerator.Api/Controllers/WebhookController.cs` (Webhook handling)
- `/home/jaime/Documents/projects/botGenerator/src/BotGenerator.Core/Models/WhatsAppMessage.cs` (Message model)
- Current step output: `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/steps/step-10-output.md`

---

## Verification

- [x] Step 10 requirements reviewed from steps file
- [x] WhatsAppService.cs read and analyzed (229 lines)
- [x] IWhatsAppService.cs read and analyzed (59 lines)
- [x] send_whatsapp_uazapi.php read and analyzed (484 lines)
- [x] SendTextAsync method documented (signature, flow, logging, error handling)
- [x] SendButtonsAsync method documented (signature, button format, request structure)
- [x] SendMenuAsync method documented (signature, menu structure, sections/rows)
- [x] GetHistoryAsync method documented (signature, chat ID format, response mapping)
- [x] UAZAPI endpoint integration documented (3 endpoints, auth, request/response formats)
- [x] PHP comparison completed (sendUazApiWhatsApp, sendWhatsAppConfirmationWithButtonsUazApi)
- [x] Missing functionality identified (6 in C#, 2 in PHP)
- [x] Button format difference documented (URL vs ID)
- [x] Phone number formatting gap identified (PHP auto-adds +34, C# doesn't)
- [x] Message ID capture gap identified (PHP returns, C# discards)
- [x] Fallback mechanism gap identified (PHP has, C# doesn't)
- [x] Logging strategy documented (4 log levels, gaps identified)
- [x] Service registration requirements documented (HttpClient, config keys)
- [x] Configuration structure documented (appsettings.json)
- [x] Data models documented (ButtonOption, MenuSection, MenuRow, WhatsAppHistoryMessage)
- [x] Internal classes documented (HistoryResponse, HistoryMessage)
- [x] Integration points identified (WebhookController, MainConversationAgent, BookingHandler)
- [x] Potential interactive features outlined (rice selection menu, confirmation buttons, context-aware)
- [x] No issues or blockers encountered
- [x] Output file created at correct path
- [x] Status marked as COMPLETED
