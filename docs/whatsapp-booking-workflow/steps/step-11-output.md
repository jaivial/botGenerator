# Step 11 Output: Document Database Schema for Bookings Table

## Status: COMPLETED

## Execution Summary
Successfully documented the complete bookings table schema from villacarmen_schema.sql. The table contains 19 columns tracking all aspects of restaurant reservations including customer information, reservation details, rice orders, accessibility requirements, and booking status tracking. Identified required vs optional fields, documented data types and constraints, and provided integration notes for C# API implementation.

## Files Read
- `/home/jaime/Documents/projects/alqueriavillacarmen/villacarmen_schema.sql` - Complete database schema (293 lines)
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/whatsapp-booking-workflow-steps.md` - Step 11 requirements
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/progress.json` - Current progress
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/steps/step-10-output.md` - Prior context

## Files Created
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/steps/step-11-output.md` - This documentation

---

## Bookings Table Complete Schema

### Table Metadata

**Table Name**: `bookings`

**Engine**: InnoDB

**Charset**: utf8mb4

**Collation**: utf8mb4_general_ci

**Auto Increment**: 1984 (current value in schema)

**Location**: `/home/jaime/Documents/projects/alqueriavillacarmen/villacarmen_schema.sql` (Lines 100-122)

**Purpose**: Stores all restaurant reservation data including customer details, party information, rice pre-orders, and booking status tracking.

---

### Complete Column Definitions

#### Primary Key

**Column**: `id`
- **Type**: `INT NOT NULL AUTO_INCREMENT`
- **Constraint**: `PRIMARY KEY`
- **Description**: Unique identifier for each booking
- **Required**: Yes (auto-generated)
- **API Usage**: Returned after successful booking insertion
- **Example**: `1983`

---

#### Customer Information Fields

**Column**: `customer_name`
- **Type**: `VARCHAR(255) NOT NULL`
- **Collation**: utf8mb4_general_ci
- **Description**: Full name of the person making the reservation
- **Required**: YES
- **Default**: None
- **Validation**: Cannot be null or empty
- **Example**: `"Juan García López"`
- **C# Mapping**: `string CustomerName { get; set; }`

---

**Column**: `contact_email`
- **Type**: `VARCHAR(255) NOT NULL`
- **Collation**: utf8mb4_general_ci
- **Description**: Customer's email address for confirmation and communications
- **Required**: YES
- **Default**: None
- **Validation**: Must be valid email format
- **Example**: `"juan.garcia@email.com"`
- **C# Mapping**: `string ContactEmail { get; set; }`
- **Note**: Used for sending email confirmations

---

**Column**: `contact_phone`
- **Type**: `VARCHAR(9) DEFAULT NULL`
- **Collation**: utf8mb4_general_ci
- **Description**: Customer's phone number (Spanish format without country code)
- **Required**: NO
- **Default**: `NULL`
- **Format**: 9 digits without +34 prefix
- **Example**: `"686969914"`
- **C# Mapping**: `string? ContactPhone { get; set; }`
- **Important**: For WhatsApp integration, need to add "34" prefix
- **WhatsApp Format**: `"34686969914"`

---

#### Reservation Details Fields

**Column**: `reservation_date`
- **Type**: `DATE NOT NULL`
- **Description**: Date of the reservation
- **Required**: YES
- **Default**: None
- **Format**: `YYYY-MM-DD`
- **Example**: `2025-12-25`
- **C# Mapping**: `DateTime ReservationDate { get; set; }`
- **Index**: Part of composite index `idx_bookings_date_status`
- **Validation**: Must be checked against `restaurant_days` and `daily_limits` tables
- **Related Tables**:
  - `restaurant_days.date` - Check if restaurant is open
  - `daily_limits.date` - Check capacity limits
  - `hour_configuration.date` - Check available time slots

---

**Column**: `reservation_time`
- **Type**: `TIME NOT NULL`
- **Description**: Time slot for the reservation
- **Required**: YES
- **Default**: None
- **Format**: `HH:MM:SS`
- **Example**: `"14:00:00"`, `"21:30:00"`
- **C# Mapping**: `TimeSpan ReservationTime { get; set; }`
- **Validation**: Must match available hours in `openinghours.hoursarray`
- **Common Times**: Lunch (13:00-16:00), Dinner (20:00-23:00)

---

**Column**: `party_size`
- **Type**: `INT NOT NULL`
- **Description**: Number of people in the reservation party
- **Required**: YES
- **Default**: None
- **Range**: Typically 1-20 (restaurant policy dependent)
- **Example**: `4`, `8`, `12`
- **C# Mapping**: `int PartySize { get; set; }`
- **Validation**:
  - Minimum: 1
  - Maximum: Should check against daily capacity
  - Used to calculate table requirements

---

**Column**: `table_number`
- **Type**: `VARCHAR(10) DEFAULT NULL`
- **Collation**: utf8mb4_general_ci
- **Description**: Assigned table number (set by restaurant staff)
- **Required**: NO
- **Default**: `NULL`
- **Example**: `"A1"`, `"12"`, `"T-5"`
- **C# Mapping**: `string? TableNumber { get; set; }`
- **Note**: Typically assigned after booking confirmation, not during initial reservation

---

#### Rice Pre-Order Fields

**Column**: `arroz_type`
- **Type**: `VARCHAR(200) DEFAULT NULL`
- **Collation**: utf8mb4_general_ci
- **Description**: Type of rice (paella) pre-ordered for the reservation
- **Required**: NO
- **Default**: `NULL`
- **Example**: `"Paella Valenciana"`, `"Arroz Negro"`, `"Arroz a Banda"`
- **C# Mapping**: `string? ArrozType { get; set; }`
- **Validation**: Should match rice types from PHP rice menu API
- **WhatsApp Booking Flow**: Collected during conversation if party_size >= 4
- **Note**: Rice requires minimum notice (typically ordered for groups)

---

**Column**: `arroz_servings`
- **Type**: `INT DEFAULT NULL`
- **Description**: Number of rice servings pre-ordered
- **Required**: NO
- **Default**: `NULL`
- **Example**: `2`, `4`, `6`
- **C# Mapping**: `int? ArrozServings { get; set; }`
- **Validation**:
  - Should be <= party_size
  - Typically comes in portions of 2
- **Business Rule**: Rice portions serve 2 people each
- **Dependency**: Only set if `arroz_type` is not null

---

#### Accessibility & Special Requirements

**Column**: `highChairs`
- **Type**: `INT DEFAULT NULL`
- **Description**: Number of high chairs needed for babies/toddlers
- **Required**: NO
- **Default**: `NULL`
- **Example**: `0`, `1`, `2`
- **C# Mapping**: `int? HighChairs { get; set; }`
- **WhatsApp Booking Flow**: Asked during booking conversation
- **Validation**: Should be <= party_size
- **Note**: Important for table arrangement planning

---

**Column**: `babyStrollers`
- **Type**: `INT DEFAULT NULL`
- **Description**: Number of baby strollers that need space
- **Required**: NO
- **Default**: `NULL`
- **Example**: `0`, `1`, `2`
- **C# Mapping**: `int? BabyStrollers { get; set; }`
- **WhatsApp Booking Flow**: Asked during booking conversation
- **Validation**: Should be <= party_size
- **Note**: Important for space allocation planning

---

**Column**: `commentary`
- **Type**: `TEXT DEFAULT NULL`
- **Collation**: utf8mb4_general_ci
- **Description**: Additional comments or special requests from customer
- **Required**: NO
- **Default**: `NULL`
- **Max Length**: TEXT type (65,535 bytes)
- **Example**: `"Alergia a mariscos"`, `"Mesa cerca de la ventana"`, `"Cumpleaños sorpresa"`
- **C# Mapping**: `string? Commentary { get; set; }`
- **WhatsApp Booking Flow**: May be collected if customer volunteers information
- **Note**: Free-form field for any special requests

---

#### Booking Status & Tracking Fields

**Column**: `status`
- **Type**: `VARCHAR(20) DEFAULT 'pending'`
- **Collation**: utf8mb4_general_ci
- **Description**: Current status of the reservation
- **Required**: NO (has default)
- **Default**: `'pending'`
- **Allowed Values**:
  - `'pending'` - Initial state after booking creation
  - `'confirmed'` - Customer confirmed the reservation
  - `'cancelled'` - Reservation has been cancelled
- **Example**: `"confirmed"`
- **C# Mapping**: `string Status { get; set; } = "pending"`
- **Index**: Part of composite index `idx_bookings_date_status`
- **WhatsApp Integration**: Updated when customer responds to confirmation buttons
- **Comment**: `'Reservation status: pending, confirmed, cancelled'`

---

**Column**: `added_date`
- **Type**: `TIMESTAMP DEFAULT CURRENT_TIMESTAMP`
- **Description**: Timestamp when the booking record was created
- **Required**: NO (auto-generated)
- **Default**: `CURRENT_TIMESTAMP`
- **Format**: `YYYY-MM-DD HH:MM:SS`
- **Example**: `2025-11-27 15:30:45`
- **C# Mapping**: `DateTime AddedDate { get; set; }`
- **Note**: Automatically set by database on INSERT
- **Usage**: Audit trail, tracking when reservation was made

---

**Column**: `re_confirmation_token`
- **Type**: `VARCHAR(255) DEFAULT NULL`
- **Collation**: utf8mb4_general_ci
- **Description**: Unique token for reservation confirmation links
- **Required**: NO
- **Default**: `NULL`
- **Format**: Typically a UUID or random hash
- **Example**: `"a7f3e9b2-4c1d-4a9f-8e3f-2b1c4d5e6f7a"`
- **C# Mapping**: `string? ReConfirmationToken { get; set; }`
- **Usage**: Used in email/WhatsApp confirmation URLs
- **Security**: Should be cryptographically random and unique
- **URL Example**: `https://alqueriavillacarmen.com/confirm.php?token=a7f3e9b2...`

---

**Column**: `re_confirmation`
- **Type**: `TINYINT(1) DEFAULT 0`
- **Description**: Flag indicating if customer has re-confirmed the reservation
- **Required**: NO (has default)
- **Default**: `0`
- **Values**:
  - `0` - Not re-confirmed
  - `1` - Re-confirmed by customer
- **Example**: `1`
- **C# Mapping**: `bool ReConfirmation { get; set; } = false`
- **Usage**: Tracks if customer clicked confirmation link or button
- **WhatsApp Integration**: Set to 1 when customer clicks "Confirmar" button

---

**Column**: `reminder_sent`
- **Type**: `TINYINT(1) DEFAULT 0`
- **Description**: Flag indicating if a general reminder has been sent
- **Required**: NO (has default)
- **Default**: `0`
- **Values**:
  - `0` - No reminder sent
  - `1` - Reminder has been sent
- **Example**: `1`
- **C# Mapping**: `bool ReminderSent { get; set; } = false`
- **Usage**: Prevents duplicate reminder messages
- **Comment**: `'Whether a reminder has been sent: 0=no, 1=yes'`
- **WhatsApp Integration**: Set to 1 after sending reminder via WhatsApp

---

**Column**: `rice_reminder_sent`
- **Type**: `TINYINT(1) DEFAULT 0`
- **Description**: Flag indicating if a rice pre-order reminder has been sent
- **Required**: NO (has default)
- **Default**: `0`
- **Values**:
  - `0` - No rice reminder sent
  - `1` - Rice reminder has been sent
- **Example**: `1`
- **C# Mapping**: `bool RiceReminderSent { get; set; } = false`
- **Usage**: Prevents duplicate rice reminder messages
- **Note**: Separate from general reminder to allow targeted rice order follow-ups

---

### Indexes

**Index**: `PRIMARY KEY (id)`
- **Type**: PRIMARY KEY
- **Column**: `id`
- **Purpose**: Unique identifier lookup, fast row access
- **Auto Increment**: Yes

---

**Index**: `idx_bookings_date_status`
- **Type**: KEY (Non-unique index)
- **Columns**: `(reservation_date, status)`
- **Purpose**: Optimize queries filtering by date and status
- **Common Queries**:
  ```sql
  SELECT * FROM bookings WHERE reservation_date = '2025-12-25' AND status = 'confirmed';
  SELECT COUNT(*) FROM bookings WHERE reservation_date >= '2025-12-01' AND status != 'cancelled';
  ```
- **Usage**: Daily capacity checking, availability queries, booking list filtering

---

### Field Summary Table

| Column | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| `id` | INT | YES | AUTO_INCREMENT | Primary key |
| `customer_name` | VARCHAR(255) | YES | - | Customer full name |
| `contact_email` | VARCHAR(255) | YES | - | Customer email |
| `contact_phone` | VARCHAR(9) | NO | NULL | Phone (9 digits, no +34) |
| `reservation_date` | DATE | YES | - | Reservation date |
| `reservation_time` | TIME | YES | - | Reservation time slot |
| `party_size` | INT | YES | - | Number of people |
| `table_number` | VARCHAR(10) | NO | NULL | Assigned table |
| `arroz_type` | VARCHAR(200) | NO | NULL | Rice type pre-order |
| `arroz_servings` | INT | NO | NULL | Rice portions ordered |
| `highChairs` | INT | NO | NULL | High chairs needed |
| `babyStrollers` | INT | NO | NULL | Baby strollers |
| `commentary` | TEXT | NO | NULL | Special requests |
| `status` | VARCHAR(20) | NO | 'pending' | Booking status |
| `added_date` | TIMESTAMP | NO | CURRENT_TIMESTAMP | Creation timestamp |
| `re_confirmation_token` | VARCHAR(255) | NO | NULL | Confirmation token |
| `re_confirmation` | TINYINT(1) | NO | 0 | Confirmed flag |
| `reminder_sent` | TINYINT(1) | NO | 0 | General reminder sent |
| `rice_reminder_sent` | TINYINT(1) | NO | 0 | Rice reminder sent |

---

### Required Fields for Booking Insertion

**Minimum Required Fields** (for valid INSERT):
1. `customer_name` - VARCHAR(255) NOT NULL
2. `contact_email` - VARCHAR(255) NOT NULL
3. `reservation_date` - DATE NOT NULL
4. `reservation_time` - TIME NOT NULL
5. `party_size` - INT NOT NULL

**Example Minimal INSERT**:
```sql
INSERT INTO bookings (
    customer_name,
    contact_email,
    reservation_date,
    reservation_time,
    party_size
) VALUES (
    'Juan García',
    'juan@email.com',
    '2025-12-25',
    '14:00:00',
    4
);
```

**WhatsApp Booking Fields** (recommended for complete booking):
1. `customer_name` ✓ REQUIRED
2. `contact_email` ✓ REQUIRED
3. `contact_phone` ✓ RECOMMENDED (for WhatsApp notifications)
4. `reservation_date` ✓ REQUIRED
5. `reservation_time` ✓ REQUIRED
6. `party_size` ✓ REQUIRED
7. `arroz_type` - OPTIONAL (if party_size >= 4)
8. `arroz_servings` - OPTIONAL (if arroz_type is set)
9. `highChairs` - OPTIONAL (asked during booking)
10. `babyStrollers` - OPTIONAL (asked during booking)
11. `commentary` - OPTIONAL (free-form customer input)

**Auto-Generated Fields**:
- `id` - AUTO_INCREMENT
- `added_date` - CURRENT_TIMESTAMP

**Fields Set by System**:
- `status` - Defaults to `'pending'`
- `re_confirmation` - Defaults to `0`
- `reminder_sent` - Defaults to `0`
- `rice_reminder_sent` - Defaults to `0`

**Fields Set Later by Staff**:
- `table_number` - Assigned after booking confirmation
- `re_confirmation_token` - Generated for confirmation links

---

### Data Type Mapping for C# Models

```csharp
public class Booking
{
    // Primary Key
    public int Id { get; set; }

    // Customer Information (REQUIRED)
    public string CustomerName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }

    // Reservation Details (REQUIRED)
    public DateTime ReservationDate { get; set; }
    public TimeSpan ReservationTime { get; set; }
    public int PartySize { get; set; }

    // Table Assignment (OPTIONAL)
    public string? TableNumber { get; set; }

    // Rice Pre-Order (OPTIONAL)
    public string? ArrozType { get; set; }
    public int? ArrozServings { get; set; }

    // Accessibility Requirements (OPTIONAL)
    public int? HighChairs { get; set; }
    public int? BabyStrollers { get; set; }

    // Comments (OPTIONAL)
    public string? Commentary { get; set; }

    // Status Tracking (AUTO-DEFAULTED)
    public string Status { get; set; } = "pending";
    public DateTime AddedDate { get; set; } = DateTime.Now;

    // Confirmation Tracking (AUTO-DEFAULTED)
    public string? ReConfirmationToken { get; set; }
    public bool ReConfirmation { get; set; } = false;
    public bool ReminderSent { get; set; } = false;
    public bool RiceReminderSent { get; set; } = false;
}
```

**Type Conversion Notes**:
- MySQL `TINYINT(1)` → C# `bool`
- MySQL `VARCHAR(n)` → C# `string` (nullable with `?` if DEFAULT NULL)
- MySQL `INT` → C# `int` (nullable with `?` if DEFAULT NULL)
- MySQL `DATE` → C# `DateTime` (date only)
- MySQL `TIME` → C# `TimeSpan`
- MySQL `TIMESTAMP` → C# `DateTime`
- MySQL `TEXT` → C# `string?`

---

### Validation Rules for API Integration

#### Customer Information Validation

**customer_name**:
- **Must not be empty**: `!string.IsNullOrWhiteSpace(customerName)`
- **Max length**: 255 characters
- **Recommended**: Check for minimum 2 characters
- **Format**: Allow letters, spaces, hyphens, apostrophes
- **Example Valid**: `"Juan García-López"`
- **Example Invalid**: `""`, `"J"`, `null`

**contact_email**:
- **Must not be empty**: `!string.IsNullOrWhiteSpace(contactEmail)`
- **Must be valid email**: Regex or EmailAddressAttribute
- **Max length**: 255 characters
- **Format**: `local@domain.tld`
- **Example Valid**: `"juan.garcia@email.com"`
- **Example Invalid**: `"notanemail"`, `"@email.com"`, `"juan@"`

**contact_phone**:
- **Optional**: Can be null
- **If provided**: Must be 9 digits
- **Format**: Spanish phone without country code
- **Validation**: `^[0-9]{9}$`
- **Example Valid**: `"686969914"`, `"938123456"`
- **Example Invalid**: `"34686969914"` (has country code), `"686-969-914"` (has separators)
- **Important**: Add "34" prefix for WhatsApp notifications

#### Reservation Details Validation

**reservation_date**:
- **Must not be null**
- **Must be future date**: `>= DateTime.Today`
- **Must check availability**:
  1. Query `restaurant_days` table: `is_open = 1` for date
  2. Query `daily_limits` table: Check current bookings vs limit
  3. Query `hour_configuration` table: Check time slot availability
- **Format**: `YYYY-MM-DD`
- **Business Rule**: Typically allow bookings 1-90 days in advance

**reservation_time**:
- **Must not be null**
- **Must match available hours**: Check `openinghours.hoursarray` for date
- **Format**: `HH:MM:SS`
- **Common Slots**:
  - Lunch: 13:00, 13:30, 14:00, 14:30, 15:00, 15:30
  - Dinner: 20:00, 20:30, 21:00, 21:30, 22:00
- **Business Rule**: Last seating typically 1.5-2 hours before closing

**party_size**:
- **Must be positive**: `> 0`
- **Minimum**: 1 person
- **Maximum**: Check restaurant capacity (typically 20-30)
- **Validation**: `partySize >= 1 && partySize <= maxPartySize`
- **Business Rule**: Large parties (>12) may need special arrangements

#### Rice Pre-Order Validation

**arroz_type**:
- **Optional**: Can be null
- **If provided**: Must match available rice types from rice menu API
- **Max length**: 200 characters
- **Valid Values**: Query from `get_available_rice_types.php` API
- **Example Valid**: `"Paella Valenciana"`, `"Arroz Negro"`
- **Example Invalid**: `"Pizza"`, `"Hamburguesa"`
- **Business Rule**: Rice typically only available for lunch service

**arroz_servings**:
- **Optional**: Can be null
- **Dependent**: Only valid if `arroz_type` is set
- **Must be positive**: `> 0`
- **Must not exceed party size**: `<= party_size`
- **Business Rule**: Rice portions serve 2 people, so typically even numbers
- **Validation**: `arrozServings >= 1 && arrozServings <= partySize`

#### Accessibility Validation

**highChairs**:
- **Optional**: Can be null
- **If provided**: Must be non-negative: `>= 0`
- **Max**: Should not exceed party_size
- **Validation**: `highChairs >= 0 && highChairs <= partySize`
- **Default**: 0 if not specified

**babyStrollers**:
- **Optional**: Can be null
- **If provided**: Must be non-negative: `>= 0`
- **Max**: Should not exceed party_size
- **Validation**: `babyStrollers >= 0 && babyStrollers <= partySize`
- **Default**: 0 if not specified

#### Commentary Validation

**commentary**:
- **Optional**: Can be null
- **Max length**: 65,535 bytes (TEXT type)
- **Recommended max**: 500-1000 characters for display purposes
- **Format**: Free-form text
- **Sanitization**: Remove harmful characters, escape for SQL

#### Status Validation

**status**:
- **Allowed values**: `'pending'`, `'confirmed'`, `'cancelled'`
- **Default**: `'pending'` for new bookings
- **Validation**: Enum or string check
- **Case**: Lowercase
- **Transitions**:
  - `pending` → `confirmed` (customer confirms)
  - `pending` → `cancelled` (customer or staff cancels)
  - `confirmed` → `cancelled` (late cancellation)

---

### Database Constraints & Indexes

**Primary Key Constraint**:
```sql
PRIMARY KEY (`id`)
```
- Ensures unique `id` for each booking
- Auto-increments on INSERT

**Composite Index**:
```sql
KEY `idx_bookings_date_status` (`reservation_date`,`status`)
```
- Optimizes queries filtering by date and status
- Critical for availability checking
- Used in daily booking reports

**NOT NULL Constraints**:
- `id` - AUTO_INCREMENT, never null
- `customer_name` - Required customer identification
- `contact_email` - Required for communication
- `reservation_date` - Required for booking
- `reservation_time` - Required for booking
- `party_size` - Required for capacity planning

**DEFAULT Values**:
- `contact_phone` - DEFAULT NULL
- `re_confirmation_token` - DEFAULT NULL
- `re_confirmation` - DEFAULT 0
- `commentary` - DEFAULT NULL
- `babyStrollers` - DEFAULT NULL
- `highChairs` - DEFAULT NULL
- `arroz_type` - DEFAULT NULL
- `arroz_servings` - DEFAULT NULL
- `added_date` - DEFAULT CURRENT_TIMESTAMP
- `status` - DEFAULT 'pending'
- `reminder_sent` - DEFAULT 0
- `rice_reminder_sent` - DEFAULT 0
- `table_number` - DEFAULT NULL

---

### Integration Notes for BotGenerator API

#### Phone Number Format Handling

**Database Storage**: `VARCHAR(9)` without country code
- Example: `"686969914"`

**WhatsApp UAZAPI Format**: Requires country code
- Example: `"34686969914"`

**Conversion Logic**:
```csharp
public static class PhoneNumberHelper
{
    public static string FormatForWhatsApp(string? dbPhone)
    {
        if (string.IsNullOrWhiteSpace(dbPhone))
            return string.Empty;

        // Remove any non-numeric characters
        var cleaned = Regex.Replace(dbPhone, @"[^0-9]", "");

        // Add Spanish country code if not present
        if (!cleaned.StartsWith("34") && cleaned.Length == 9)
        {
            return "34" + cleaned;
        }

        return cleaned;
    }

    public static string? FormatForDatabase(string whatsAppPhone)
    {
        if (string.IsNullOrWhiteSpace(whatsAppPhone))
            return null;

        // Remove non-numeric characters
        var cleaned = Regex.Replace(whatsAppPhone, @"[^0-9]", "");

        // Remove country code (34) if present
        if (cleaned.StartsWith("34") && cleaned.Length > 9)
        {
            return cleaned.Substring(2, 9);
        }

        // Ensure 9 digits
        if (cleaned.Length != 9)
            throw new ArgumentException("Phone must be 9 digits");

        return cleaned;
    }
}
```

**Usage**:
```csharp
// When inserting booking from WhatsApp
var booking = new Booking
{
    ContactPhone = PhoneNumberHelper.FormatForDatabase(whatsAppMessage.From)
};

// When sending WhatsApp notifications
var whatsAppNumber = PhoneNumberHelper.FormatForWhatsApp(booking.ContactPhone);
await whatsAppService.SendTextAsync(whatsAppNumber, confirmationMessage);
```

---

#### Booking Insertion Flow

**Step 1: Validate Required Fields**
```csharp
public async Task<bool> ValidateBookingData(BookingRequest request)
{
    if (string.IsNullOrWhiteSpace(request.CustomerName))
        return false;

    if (string.IsNullOrWhiteSpace(request.ContactEmail) || !IsValidEmail(request.ContactEmail))
        return false;

    if (request.ReservationDate < DateTime.Today)
        return false;

    if (request.PartySize < 1)
        return false;

    return true;
}
```

**Step 2: Check Availability**
```csharp
public async Task<bool> IsDateTimeAvailable(DateTime date, TimeSpan time, int partySize)
{
    // Check if restaurant is open
    var isOpen = await CheckRestaurantOpen(date);
    if (!isOpen) return false;

    // Check daily capacity
    var hasCapacity = await CheckDailyCapacity(date, partySize);
    if (!hasCapacity) return false;

    // Check time slot availability
    var timeAvailable = await CheckTimeSlotAvailable(date, time);
    if (!timeAvailable) return false;

    return true;
}
```

**Step 3: Generate Confirmation Token**
```csharp
public string GenerateConfirmationToken()
{
    return Guid.NewGuid().ToString();
}
```

**Step 4: Insert Booking**
```csharp
public async Task<int> InsertBooking(Booking booking)
{
    booking.ReConfirmationToken = GenerateConfirmationToken();
    booking.Status = "pending";
    booking.AddedDate = DateTime.Now;

    // Execute INSERT and return new booking ID
    var sql = @"
        INSERT INTO bookings (
            customer_name, contact_email, contact_phone,
            reservation_date, reservation_time, party_size,
            arroz_type, arroz_servings,
            highChairs, babyStrollers,
            commentary,
            re_confirmation_token, status, added_date
        ) VALUES (
            @CustomerName, @ContactEmail, @ContactPhone,
            @ReservationDate, @ReservationTime, @PartySize,
            @ArrozType, @ArrozServings,
            @HighChairs, @BabyStrollers,
            @Commentary,
            @ReConfirmationToken, @Status, @AddedDate
        );
        SELECT LAST_INSERT_ID();
    ";

    return await connection.ExecuteScalarAsync<int>(sql, booking);
}
```

**Step 5: Send Confirmation**
```csharp
public async Task SendBookingConfirmation(int bookingId)
{
    var booking = await GetBookingById(bookingId);
    var whatsAppNumber = PhoneNumberHelper.FormatForWhatsApp(booking.ContactPhone);

    var confirmationMessage = BuildConfirmationMessage(booking);
    await whatsAppService.SendTextAsync(whatsAppNumber, confirmationMessage);

    // Also send to staff notification numbers
    var staffNumbers = new[] { "34686969914", "34638857294", "34692747052" };
    foreach (var staffNumber in staffNumbers)
    {
        var staffMessage = BuildStaffNotification(booking);
        await whatsAppService.SendTextAsync(staffNumber, staffMessage);
    }
}
```

---

#### Status Update Flow

**Scenario 1: Customer Confirms via WhatsApp Button**
```csharp
public async Task HandleConfirmationButton(string phoneNumber, string buttonId)
{
    if (buttonId == "confirm")
    {
        // Find booking by phone and pending status
        var booking = await GetPendingBookingByPhone(phoneNumber);

        if (booking != null)
        {
            booking.Status = "confirmed";
            booking.ReConfirmation = true;
            await UpdateBookingStatus(booking);

            await whatsAppService.SendTextAsync(
                phoneNumber,
                "✅ ¡Reserva confirmada! Te esperamos en Alquería Villa Carmen."
            );
        }
    }
}
```

**Scenario 2: Customer Cancels**
```csharp
public async Task CancelBooking(int bookingId, string reason)
{
    var booking = await GetBookingById(bookingId);
    booking.Status = "cancelled";
    booking.Commentary = (booking.Commentary ?? "") + $"\nCancelled: {reason}";

    await UpdateBookingStatus(booking);

    // Notify customer
    var whatsAppNumber = PhoneNumberHelper.FormatForWhatsApp(booking.ContactPhone);
    await whatsAppService.SendTextAsync(
        whatsAppNumber,
        "Su reserva ha sido cancelada. Esperamos poder atenderle en otra ocasión."
    );

    // Notify staff
    var staffNumbers = new[] { "34686969914", "34638857294", "34692747052" };
    foreach (var staffNumber in staffNumbers)
    {
        await whatsAppService.SendTextAsync(
            staffNumber,
            $"🚫 Reserva cancelada: {booking.CustomerName} - {booking.ReservationDate:dd/MM/yyyy} {booking.ReservationTime}"
        );
    }
}
```

---

#### Reminder System Integration

**General Reminder** (1-2 days before reservation):
```csharp
public async Task SendGeneralReminders()
{
    var reminderDate = DateTime.Today.AddDays(2); // 2 days in advance

    var sql = @"
        SELECT * FROM bookings
        WHERE reservation_date = @ReminderDate
        AND status = 'confirmed'
        AND reminder_sent = 0
    ";

    var bookings = await connection.QueryAsync<Booking>(sql, new { ReminderDate = reminderDate });

    foreach (var booking in bookings)
    {
        var whatsAppNumber = PhoneNumberHelper.FormatForWhatsApp(booking.ContactPhone);
        var reminderMessage = $@"
🔔 Recordatorio de reserva

📅 Fecha: {booking.ReservationDate:dd/MM/yyyy}
🕐 Hora: {booking.ReservationTime:hh\:mm}
👥 Personas: {booking.PartySize}

¡Te esperamos en Alquería Villa Carmen!
        ";

        var sent = await whatsAppService.SendTextAsync(whatsAppNumber, reminderMessage);

        if (sent)
        {
            booking.ReminderSent = true;
            await UpdateBooking(booking);
        }
    }
}
```

**Rice Reminder** (separate follow-up for rice pre-orders):
```csharp
public async Task SendRiceReminders()
{
    var sql = @"
        SELECT * FROM bookings
        WHERE reservation_date BETWEEN @StartDate AND @EndDate
        AND status = 'confirmed'
        AND arroz_type IS NOT NULL
        AND rice_reminder_sent = 0
    ";

    var params = new
    {
        StartDate = DateTime.Today.AddDays(3),
        EndDate = DateTime.Today.AddDays(5)
    };

    var bookings = await connection.QueryAsync<Booking>(sql, params);

    foreach (var booking in bookings)
    {
        var whatsAppNumber = PhoneNumberHelper.FormatForWhatsApp(booking.ContactPhone);
        var riceMessage = $@"
🍚 Confirmación de arroz pre-ordenado

Su pedido: {booking.ArrozType}
Raciones: {booking.ArrozServings}
Fecha: {booking.ReservationDate:dd/MM/yyyy}

¿Desea mantener este pedido?
        ";

        var sent = await whatsAppService.SendTextAsync(whatsAppNumber, riceMessage);

        if (sent)
        {
            booking.RiceReminderSent = true;
            await UpdateBooking(booking);
        }
    }
}
```

---

#### Query Examples for Common Operations

**Find bookings for a specific date**:
```sql
SELECT * FROM bookings
WHERE reservation_date = '2025-12-25'
AND status != 'cancelled'
ORDER BY reservation_time;
```

**Count confirmed bookings by date**:
```sql
SELECT reservation_date, COUNT(*) as booking_count, SUM(party_size) as total_guests
FROM bookings
WHERE status = 'confirmed'
GROUP BY reservation_date
ORDER BY reservation_date;
```

**Find bookings needing confirmation**:
```sql
SELECT * FROM bookings
WHERE status = 'pending'
AND re_confirmation = 0
AND reservation_date > CURDATE()
ORDER BY added_date DESC;
```

**Get bookings with rice pre-orders**:
```sql
SELECT * FROM bookings
WHERE arroz_type IS NOT NULL
AND reservation_date >= CURDATE()
AND status = 'confirmed'
ORDER BY reservation_date, reservation_time;
```

**Find bookings by customer phone**:
```sql
SELECT * FROM bookings
WHERE contact_phone = '686969914'
ORDER BY reservation_date DESC
LIMIT 10;
```

**Check daily capacity for a date**:
```sql
SELECT
    reservation_date,
    COUNT(*) as booking_count,
    SUM(party_size) as total_guests,
    (SELECT daily_limit FROM daily_limits WHERE date = '2025-12-25') as capacity_limit
FROM bookings
WHERE reservation_date = '2025-12-25'
AND status != 'cancelled'
GROUP BY reservation_date;
```

---

## Issues Encountered

None. Schema documentation completed successfully.

---

## Blockers

None. All columns documented with complete details.

---

## Context for Next Step

### What We've Documented (Step 11)

**Bookings Table Structure**:
- **Total Columns**: 19
- **Required Fields**: 5 (id, customer_name, contact_email, reservation_date, reservation_time, party_size)
- **Optional Fields**: 14
- **Indexes**: 2 (PRIMARY KEY on id, composite KEY on reservation_date + status)
- **Auto-Generated**: 2 (id AUTO_INCREMENT, added_date CURRENT_TIMESTAMP)

**Column Categories**:
1. **Primary Key**: `id`
2. **Customer Information**: `customer_name`, `contact_email`, `contact_phone` (3 columns)
3. **Reservation Details**: `reservation_date`, `reservation_time`, `party_size`, `table_number` (4 columns)
4. **Rice Pre-Order**: `arroz_type`, `arroz_servings` (2 columns)
5. **Accessibility**: `highChairs`, `babyStrollers` (2 columns)
6. **Comments**: `commentary` (1 column)
7. **Status Tracking**: `status`, `added_date`, `re_confirmation_token`, `re_confirmation`, `reminder_sent`, `rice_reminder_sent` (6 columns)

**Critical Fields for WhatsApp Booking**:
- `customer_name` ✓ REQUIRED - Collected via conversation
- `contact_email` ✓ REQUIRED - Collected via conversation
- `contact_phone` ✓ RECOMMENDED - From WhatsApp sender (format: 9 digits without +34)
- `reservation_date` ✓ REQUIRED - Collected and validated via conversation
- `reservation_time` ✓ REQUIRED - Collected and validated via conversation
- `party_size` ✓ REQUIRED - Collected via conversation
- `arroz_type` - OPTIONAL - Asked if party_size >= 4
- `arroz_servings` - OPTIONAL - Asked if arroz_type selected
- `highChairs` - OPTIONAL - Asked during booking flow
- `babyStrollers` - OPTIONAL - Asked during booking flow

**Phone Number Format Issue Identified**:
- **Database**: Stores 9-digit phone without country code (`VARCHAR(9)`)
- **WhatsApp**: Requires country code format (`"34686969914"`)
- **Solution**: Need `PhoneNumberHelper` utility class for conversion
- **Critical**: Must convert before sending WhatsApp messages

**Default Values Documented**:
- `status` → `'pending'` (new bookings start as pending)
- `re_confirmation` → `0` (not confirmed initially)
- `reminder_sent` → `0` (no reminder sent yet)
- `rice_reminder_sent` → `0` (no rice reminder sent yet)
- `added_date` → `CURRENT_TIMESTAMP` (auto-set on insert)

**Status Workflow**:
- Initial: `'pending'`
- After customer confirmation: `'confirmed'`
- If cancelled: `'cancelled'`
- Tracked via `re_confirmation` flag (0/1)

**Reminder System**:
- **General Reminder**: `reminder_sent` flag (0/1)
- **Rice Reminder**: `rice_reminder_sent` flag (0/1)
- Prevents duplicate notifications
- Allows targeted follow-ups

**Composite Index**: `idx_bookings_date_status`
- Optimizes queries filtering by date and status
- Critical for availability checking
- Used in `check_date_availability.php` API

**Related Tables Identified**:
- `restaurant_days` - Check if restaurant is open on reservation_date
- `daily_limits` - Check capacity limits for reservation_date
- `hour_configuration` - Check available time slots
- `openinghours` - Validate reservation_time availability

**C# Model Mapping**:
- Complete `Booking` class structure provided
- Type conversions documented (MySQL → C#)
- Nullable fields identified with `?` operator
- Default values included

**Validation Rules Documented**:
- Customer name: Not empty, max 255 chars
- Contact email: Valid format, max 255 chars
- Contact phone: 9 digits, no country code (if provided)
- Reservation date: Future date, check availability
- Reservation time: Match available hours
- Party size: >= 1, <= max capacity
- Arroz type: Match rice menu options (if provided)
- Arroz servings: <= party_size (if provided)
- High chairs: >= 0, <= party_size
- Baby strollers: >= 0, <= party_size
- Status: 'pending', 'confirmed', or 'cancelled'

**Integration Patterns Provided**:
1. Phone number conversion (DB ↔ WhatsApp format)
2. Booking insertion flow (validate → check → insert → confirm)
3. Status update flow (confirmation button handling)
4. Reminder system (general + rice reminders)
5. Common SQL queries (date lookup, capacity check, etc.)

---

### System State After This Step

- **Database Schema**: Fully understood (bookings table complete)
- **Required Fields**: 5 fields mandatory for INSERT
- **Optional Fields**: 14 fields for enhanced booking data
- **Phone Format**: Critical conversion requirement identified
- **Status Tracking**: 3 boolean flags for reminder management
- **Indexes**: 2 indexes documented (PRIMARY + composite)
- **Validation**: Complete rules for all fields
- **C# Integration**: Model class and helpers documented
- **Related Tables**: 4 tables identified for availability checking

---

### Next Steps Preview

**Step 12 and beyond** will likely cover:
- Implementing C# Booking model class based on schema
- Creating phone number conversion utility (PhoneNumberHelper)
- Analyzing `check_date_availability.php` API for availability validation
- Understanding rice menu API (`get_available_rice_types.php`)
- Implementing booking insertion service in C#
- Creating database connection configuration
- Understanding notification recipients (34686969914, 34638857294, 34692747052)
- Building booking confirmation message formatters
- Testing end-to-end booking flow from WhatsApp to database

---

### Files Ready for Next Step

Key files for upcoming steps:
- `/home/jaime/Documents/projects/alqueriavillacarmen/api/check_date_availability.php` - Date/time availability API
- `/home/jaime/Documents/projects/alqueriavillacarmen/api/get_available_rice_types.php` - Rice menu API
- `/home/jaime/Documents/projects/alqueriavillacarmen/includes/conectaVILLACARMEN.php` - Database connection
- `/home/jaime/Documents/projects/botGenerator/src/BotGenerator.Core/Models/` - Where to create Booking model
- Current step output: `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/steps/step-11-output.md`

---

## Verification

- [x] Step 11 requirements reviewed from steps file
- [x] villacarmen_schema.sql read (293 lines)
- [x] Bookings table located (lines 100-122)
- [x] All 19 columns documented with complete details
- [x] Data types documented for each column
- [x] Constraints documented (NOT NULL, DEFAULT, PRIMARY KEY, INDEX)
- [x] Required vs optional fields identified
- [x] Default values documented for all applicable columns
- [x] Indexes documented (PRIMARY KEY + composite index)
- [x] C# data type mappings provided
- [x] Validation rules defined for all fields
- [x] Phone number format issue identified and solution provided
- [x] Integration notes created (phone conversion, booking insertion, status updates, reminders)
- [x] Common SQL query examples provided
- [x] Related tables identified (restaurant_days, daily_limits, hour_configuration, openinghours)
- [x] Status workflow documented (pending → confirmed → cancelled)
- [x] Reminder system explained (general + rice reminders)
- [x] Field summary table created
- [x] C# Booking model class structure provided
- [x] No issues or blockers encountered
- [x] Output file created at correct path
- [x] Status marked as COMPLETED
