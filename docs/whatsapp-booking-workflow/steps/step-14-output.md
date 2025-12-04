# Step 14 Output: Document Special Holidays Handling

## Status: COMPLETED

## Execution Summary
Successfully documented the special holidays handling mechanism in the booking system. The system has 6 hardcoded special holiday dates (Christmas, New Year's, and Epiphany) that CANNOT be booked or modified through the online system. These dates are checked BEFORE the closed_days/opened_days logic and have the HIGHEST priority in the validation chain. The system returns a specific Spanish message explaining that these dates have special menus and require manual management.

## Files Read
- `/home/jaime/Documents/projects/alqueriavillacarmen/api/check_date_availability.php` (Lines 110-139) - Special holidays validation logic
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/progress.json` - Current progress
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/steps/step-13-output.md` - Prior context

## Files Created
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/steps/step-14-output.md` - This documentation

---

## Special Holidays Configuration

### Hardcoded Holiday List

**File Location**: `/home/jaime/Documents/projects/alqueriavillacarmen/api/check_date_availability.php` (Line 117)

**Declaration**:
```php
$specialHolidays = ['12-24', '12-25', '12-31', '01-01', '01-05', '01-06'];
```

**Format**: `MM-DD` (month-day format, year-agnostic)

---

### Complete List of Special Holidays

**Total Count**: 6 dates

#### 1. December 24 (Christmas Eve)
- **Format**: `12-24`
- **Spanish**: Nochebuena
- **Business Context**: Special Christmas Eve menu
- **Booking**: NOT ALLOWED online

#### 2. December 25 (Christmas Day)
- **Format**: `12-25`
- **Spanish**: Navidad
- **Business Context**: Special Christmas Day menu
- **Booking**: NOT ALLOWED online

#### 3. December 31 (New Year's Eve)
- **Format**: `12-31`
- **Spanish**: Nochevieja
- **Business Context**: Special New Year's Eve celebration menu
- **Booking**: NOT ALLOWED online

#### 4. January 1 (New Year's Day)
- **Format**: `01-01`
- **Spanish**: Año Nuevo
- **Business Context**: Special New Year's Day menu
- **Booking**: NOT ALLOWED online

#### 5. January 5 (Epiphany Eve)
- **Format**: `01-05`
- **Spanish**: Víspera de Reyes
- **Business Context**: Special Epiphany Eve menu
- **Booking**: NOT ALLOWED online

#### 6. January 6 (Epiphany / Three Kings Day)
- **Format**: `01-06`
- **Spanish**: Día de Reyes
- **Business Context**: Special Epiphany menu
- **Booking**: NOT ALLOWED online

---

### Summary by Season

**Christmas Period** (3 dates):
- December 24 (Christmas Eve)
- December 25 (Christmas Day)
- December 31 (New Year's Eve)

**New Year Period** (3 dates):
- January 1 (New Year's Day)
- January 5 (Epiphany Eve)
- January 6 (Epiphany)

**Business Logic**: These are major Spanish holidays with special restaurant menus requiring manual reservation management.

---

## Validation Logic

### Code Location

**File**: `/home/jaime/Documents/projects/alqueriavillacarmen/api/check_date_availability.php`

**Lines**: 114-129

**Section Label**: `STEP 2: Check special holidays (cannot be booked/modified)`

---

### Complete Implementation

```php
// ========================================================================
// STEP 2: Check special holidays (cannot be booked/modified)
// ========================================================================
$specialHolidays = ['12-24', '12-25', '12-31', '01-01', '01-05', '01-06'];
$dateMonthDay = date('m-d', strtotime($newDate));

if (in_array($dateMonthDay, $specialHolidays)) {
    echo json_encode([
        'success' => true,
        'available' => false,
        'hasAvailability' => false,
        'message' => 'Uy, esos días festivos (24, 25 y 31 de diciembre, 1, 5 y 6 de enero) tienen un menú especial y no se pueden modificar reservas. ¿Prefieres otro día? 😊',
        'reason' => 'special_holiday'
    ]);
    exit;
}
```

---

### Logic Breakdown

#### Step 1: Extract Month-Day from Requested Date

**Code**:
```php
$dateMonthDay = date('m-d', strtotime($newDate));
```

**Purpose**: Convert full date (YYYY-MM-DD) to month-day format (MM-DD)

**Example**:
```php
$newDate = '2025-12-25';
$dateMonthDay = date('m-d', strtotime('2025-12-25'));
// Result: '12-25'
```

**Why Month-Day Format**:
- Holidays repeat every year
- Year-agnostic matching
- Simple comparison (no need to check multiple years)

---

#### Step 2: Check if Date Matches Special Holiday

**Code**:
```php
if (in_array($dateMonthDay, $specialHolidays)) {
    // Return special_holiday error
}
```

**Purpose**: Check if extracted month-day is in the hardcoded special holidays list

**Example Scenarios**:

**Scenario A - Christmas Day 2025**:
```php
$newDate = '2025-12-25';
$dateMonthDay = '12-25';
in_array('12-25', $specialHolidays) → TRUE
Result: Return 'special_holiday' error, exit early
```

**Scenario B - Christmas Day 2026**:
```php
$newDate = '2026-12-25';
$dateMonthDay = '12-25';
in_array('12-25', $specialHolidays) → TRUE
Result: Return 'special_holiday' error, exit early
```

**Scenario C - Regular Date**:
```php
$newDate = '2025-11-28';
$dateMonthDay = '11-28';
in_array('11-28', $specialHolidays) → FALSE
Result: Continue to next validation (closed_days check)
```

---

#### Step 3: Return Special Holiday Error Response

**Response Structure**:
```json
{
  "success": true,
  "available": false,
  "hasAvailability": false,
  "message": "Uy, esos días festivos (24, 25 y 31 de diciembre, 1, 5 y 6 de enero) tienen un menú especial y no se pueden modificar reservas. ¿Prefieres otro día? 😊",
  "reason": "special_holiday"
}
```

**Response Fields**:
- **success** (boolean): `true` - API call succeeded
- **available** (boolean): `false` - Date NOT available for booking
- **hasAvailability** (boolean): `false` - No availability on this date
- **message** (string): Spanish message explaining the restriction
- **reason** (string): `'special_holiday'` - Specific rejection reason

**Exit Behavior**:
```php
exit;
```
- Immediately terminates script execution
- Prevents further validation checks (closed_days, 35-day range, capacity)
- Ensures no booking can be created for these dates

---

## Spanish Message Template

### Complete Message

**Text**:
```
Uy, esos días festivos (24, 25 y 31 de diciembre, 1, 5 y 6 de enero) tienen un menú especial y no se pueden modificar reservas. ¿Prefieres otro día? 😊
```

**English Translation**:
```
Oops, those holidays (December 24, 25, and 31, January 1, 5, and 6) have a special menu and reservations cannot be modified. Would you prefer another day? 😊
```

---

### Message Components

**1. Friendly Alert**: `Uy,` (Oops)
- Casual, friendly tone
- Softens the rejection
- Common Spanish interjection

**2. Date Enumeration**: `esos días festivos (24, 25 y 31 de diciembre, 1, 5 y 6 de enero)`
- Lists all 6 special dates explicitly
- Grouped by month (December dates, then January dates)
- Clear and informative

**3. Reason**: `tienen un menú especial`
- Explains WHY the date is unavailable
- "have a special menu"
- Provides business context

**4. Restriction**: `no se pueden modificar reservas`
- Clear statement: "reservations cannot be modified"
- Applies to both new bookings AND modifications
- Absolute restriction

**5. Suggestion**: `¿Prefieres otro día?`
- Polite suggestion: "Would you prefer another day?"
- Redirects customer to alternative dates
- Keeps conversation moving forward

**6. Emoji**: `😊`
- Friendly, warm tone
- Softens the rejection
- Maintains positive customer experience

---

### Message Characteristics

**Tone**: Friendly, apologetic, helpful

**Language**: Spanish (customer-facing)

**Formality**: Informal/casual (uses "prefieres" instead of formal "prefiere")

**Length**: 1 sentence (concise, easy to read on WhatsApp)

**Emoji Usage**: Single emoji at end (appropriate for WhatsApp context)

**Customer Experience**:
- Not confrontational
- Provides reason (transparency)
- Offers alternative (suggests other dates)
- Maintains positive interaction

---

## Priority in Validation Chain

### Validation Order in check_date_availability.php

**Complete Validation Sequence**:

```
┌─────────────────────────────────────────┐
│ STEP 1: Fetch closed_days/opened_days   │ (Lines 106-112)
│         from database                    │
└────────────┬────────────────────────────┘
             ↓
┌─────────────────────────────────────────┐
│ STEP 2: Check Special Holidays          │ (Lines 114-129) ← HIGHEST PRIORITY
│         [12-24, 12-25, 12-31,            │
│          01-01, 01-05, 01-06]            │
│         → IF MATCH: Exit with            │
│           'special_holiday' error        │
└────────────┬────────────────────────────┘
             ↓ (only if NOT special holiday)
┌─────────────────────────────────────────┐
│ STEP 3: Check Closed Days               │ (Lines 131-148)
│         (opened_days override >          │
│          closed_days > default schedule) │
│         → IF CLOSED: Exit with           │
│           'closed_day' error             │
└────────────┬────────────────────────────┘
             ↓ (only if NOT closed)
┌─────────────────────────────────────────┐
│ STEP 4: Check 35-Day Range              │ (Lines 150-165)
│         → IF TOO FAR: Exit with          │
│           'too_far_future' error         │
└────────────┬────────────────────────────┘
             ↓ (only if within range)
┌─────────────────────────────────────────┐
│ STEP 5: Simple Check Mode (optional)    │ (Lines 167-189)
│         If only date provided            │
│         → Return 'date_open' success     │
└────────────┬────────────────────────────┘
             ↓ (if party_size provided)
┌─────────────────────────────────────────┐
│ STEP 6: Full Check Mode                 │ (Lines 191-342)
│         - Hourly availability            │
│         - Daily capacity                 │
│         → Return detailed availability   │
└─────────────────────────────────────────┘
```

---

### Priority Ranking

**1. HIGHEST PRIORITY: Special Holidays** (Step 2)
- **Check**: Lines 114-129
- **Reason**: `special_holiday`
- **Overrides**: Everything (opened_days, closed_days, capacity, etc.)
- **Exit**: Immediate (no further checks)

**2. HIGH PRIORITY: Closed Days** (Step 3)
- **Check**: Lines 131-148
- **Reason**: `closed_day`
- **Overrides**: 35-day range, capacity checks
- **Logic**: opened_days > closed_days > default schedule

**3. MEDIUM PRIORITY: 35-Day Range** (Step 4)
- **Check**: Lines 150-165
- **Reason**: `too_far_future` or `past_date`
- **Overrides**: Capacity checks only
- **Logic**: Date must be within 0-35 days from today

**4. LOW PRIORITY: Capacity/Availability** (Steps 5-6)
- **Check**: Lines 167-342
- **Reason**: `fully_booked`, `no_availability`
- **Overrides**: None (last check)
- **Logic**: Hourly availability and daily capacity limits

---

### Why Special Holidays are Checked BEFORE closed_days

**Reason 1: Business Override**
- Special holidays have unique menus requiring manual management
- Cannot be overridden by database entries (opened_days)
- Absolute restriction regardless of restaurant_days table

**Reason 2: Prevent Conflicts**
- Even if date is in `opened_days` (is_open=TRUE), special holiday still blocks booking
- Prevents accidental bookings on special menu days
- Clear business rule: "Special menu days = NO online booking, period"

**Reason 3: Simplicity**
- Hardcoded list is easy to understand and maintain
- No database dependency for critical business rule
- Explicit validation, no edge cases

---

### Example: Special Holiday Overrides Opened Day

**Scenario**: Christmas Day marked as "opened" in restaurant_days table

**Setup**:
```sql
-- Restaurant admin tries to allow Christmas Day bookings
INSERT INTO restaurant_days (date, is_open) VALUES ('2025-12-25', TRUE);
```

**Booking Request**:
```php
$newDate = '2025-12-25';
$partySize = 4;
```

**Validation Flow**:

**Step 1: Fetch closed/opened days**
```php
$closedDays = [];
$openedDays = ['2025-12-25']; // Christmas is in opened_days
```

**Step 2: Check special holidays (BEFORE checking opened_days)**
```php
$dateMonthDay = '12-25';
if (in_array('12-25', $specialHolidays)) {
    // ✓ MATCH - Christmas is a special holiday
    echo json_encode([
        'success' => true,
        'available' => false,
        'reason' => 'special_holiday'
    ]);
    exit; // IMMEDIATE EXIT - never reaches opened_days check
}
```

**Result**:
- ❌ Booking REJECTED (special_holiday)
- opened_days override is IGNORED
- Special holiday takes precedence

**Business Outcome**: Christmas Day bookings always blocked, regardless of database configuration

---

## Business Context

### Why Special Holidays are Blocked

**1. Special Menu Offerings**
- These dates have unique, fixed menus (not à la carte)
- Different pricing structures
- Limited menu options
- Pre-planned courses and ingredients

**2. High Demand**
- Major holidays attract high customer demand
- Restaurant may be fully booked weeks/months in advance
- Manual management ensures optimal customer experience

**3. Advanced Planning Required**
- Special menu preparation requires lead time
- Ingredient procurement for fixed menus
- Staffing adjustments for holiday service
- Cannot accommodate last-minute changes

**4. Manual Reservation Management**
- Restaurant staff personally manage these reservations
- Custom customer communication (menu details, pricing)
- Deposit/prepayment requirements
- Special dietary accommodations

**5. No Modifications Allowed**
- Once booked, changes are complex due to fixed menu
- Prevents last-minute party size changes
- Protects kitchen planning and ingredient preparation

---

### Customer Experience Impact

**Positive Aspects**:
- ✓ Clear explanation (special menu mentioned)
- ✓ Friendly tone (casual Spanish, emoji)
- ✓ Alternative suggested ("¿Prefieres otro día?")
- ✓ Transparent (all 6 dates listed explicitly)

**Potential Friction**:
- ❌ Customer may want to book these high-value dates
- ❌ No online option forces phone/in-person booking
- ❌ No indication of HOW to book (phone number not provided in message)

**Improvement Opportunity** (future enhancement):
```
Message could include:
"Para reservas en esos días festivos, llámanos al [phone] o escríbenos a [email]"
(For reservations on those holiday dates, call us at [phone] or email [email])
```

---

### Business Logic Reasoning

**Why Hardcoded List**:
- ✓ Predictable (same dates every year)
- ✓ Simple to maintain (no database dependency)
- ✓ Explicit business rule (clear in code)
- ✓ Performance (no database query needed)

**Why Year-Agnostic (MM-DD format)**:
- ✓ Holidays repeat annually
- ✓ No need to update list yearly
- ✓ Simpler comparison logic
- ✓ Covers future years automatically

**Why Spanish-Only Message**:
- ✓ Restaurant is in Spain (Spanish-speaking region)
- ✓ Primary customer base speaks Spanish
- ✓ WhatsApp context assumes Spanish language
- ✓ No internationalization needed (local business)

---

## C# Implementation

### Special Holidays Configuration

```csharp
public static class SpecialHolidays
{
    /// <summary>
    /// Special holiday dates (MM-dd format) that cannot be booked online.
    /// These dates have special menus and require manual reservation management.
    /// </summary>
    public static readonly string[] Dates = new[]
    {
        "12-24", // Christmas Eve (Nochebuena)
        "12-25", // Christmas Day (Navidad)
        "12-31", // New Year's Eve (Nochevieja)
        "01-01", // New Year's Day (Año Nuevo)
        "01-05", // Epiphany Eve (Víspera de Reyes)
        "01-06"  // Epiphany / Three Kings Day (Día de Reyes)
    };

    /// <summary>
    /// Spanish message explaining special holiday restriction
    /// </summary>
    public const string SpanishMessage =
        "Uy, esos días festivos (24, 25 y 31 de diciembre, 1, 5 y 6 de enero) tienen un menú especial y no se pueden modificar reservas. ¿Prefieres otro día? 😊";

    /// <summary>
    /// English translation (for internal documentation)
    /// </summary>
    public const string EnglishTranslation =
        "Oops, those holidays (December 24, 25, and 31, January 1, 5, and 6) have a special menu and reservations cannot be modified. Would you prefer another day? 😊";
}
```

---

### Validation Method

```csharp
public class DateValidationService
{
    /// <summary>
    /// Check if a date is a special holiday (highest priority check)
    /// </summary>
    /// <param name="date">Date to validate</param>
    /// <returns>True if date is a special holiday, false otherwise</returns>
    public bool IsSpecialHoliday(DateTime date)
    {
        // Extract month-day in MM-dd format
        string monthDay = date.ToString("MM-dd");

        // Check against hardcoded special holidays list
        return SpecialHolidays.Dates.Contains(monthDay);
    }

    /// <summary>
    /// Validate booking date (checks special holidays FIRST)
    /// </summary>
    /// <param name="date">Requested booking date</param>
    /// <returns>Validation result with reason and message</returns>
    public async Task<DateValidationResult> ValidateBookingDateAsync(DateTime date)
    {
        // STEP 1: Check special holidays (HIGHEST PRIORITY)
        if (IsSpecialHoliday(date))
        {
            return new DateValidationResult
            {
                IsValid = false,
                IsAvailable = false,
                Reason = "special_holiday",
                Message = SpecialHolidays.SpanishMessage
            };
        }

        // STEP 2: Check closed days (from restaurant_days table)
        bool isClosed = await _restaurantDaysService.IsDateClosedAsync(date);
        if (isClosed)
        {
            return new DateValidationResult
            {
                IsValid = false,
                IsAvailable = false,
                Reason = "closed_day",
                Message = $"Ese día ({date:dd/MM/yyyy}) estamos cerrados. ¿Qué tal otro día? Abrimos jueves, viernes, sábado y domingo 😊"
            };
        }

        // STEP 3: Check 35-day range
        int daysUntil = (date - DateTime.Today).Days;
        if (daysUntil > 35)
        {
            return new DateValidationResult
            {
                IsValid = false,
                IsAvailable = false,
                Reason = "too_far_future",
                Message = "Solo aceptamos reservas con máximo 35 días de antelación 📅"
            };
        }

        if (daysUntil < 0)
        {
            return new DateValidationResult
            {
                IsValid = false,
                IsAvailable = false,
                Reason = "past_date",
                Message = "No se puede reservar en fechas pasadas"
            };
        }

        // STEP 4: Date is valid for booking (further capacity checks needed)
        return new DateValidationResult
        {
            IsValid = true,
            IsAvailable = true, // Tentative (capacity check still needed)
            Reason = "date_open",
            Message = $"El día {date:dd/MM/yyyy} está disponible para reservas 😊"
        };
    }
}
```

---

### Response Model

```csharp
public class DateValidationResult
{
    /// <summary>
    /// True if date passes validation, false if rejected
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// True if date is available for booking
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Specific rejection reason:
    /// 'special_holiday', 'closed_day', 'too_far_future', 'past_date', 'date_open'
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Spanish message to send to customer
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
```

---

### Usage Example

```csharp
public async Task<string> ProcessBookingDateRequest(DateTime requestedDate)
{
    // Validate date (special holidays checked first)
    var validation = await _dateValidationService.ValidateBookingDateAsync(requestedDate);

    if (!validation.IsValid)
    {
        // Send rejection message to customer via WhatsApp
        await _whatsAppService.SendMessageAsync(
            phoneNumber: customerPhone,
            message: validation.Message
        );

        // Log rejection reason
        _logger.LogInformation(
            "Booking date {Date} rejected. Reason: {Reason}",
            requestedDate,
            validation.Reason
        );

        return validation.Reason; // Return reason for conversation flow
    }

    // Date is valid, proceed to party size request
    return "date_valid";
}
```

---

### Extension Methods

```csharp
public static class DateTimeExtensions
{
    /// <summary>
    /// Check if a date is a special holiday
    /// </summary>
    public static bool IsSpecialHoliday(this DateTime date)
    {
        string monthDay = date.ToString("MM-dd");
        return SpecialHolidays.Dates.Contains(monthDay);
    }

    /// <summary>
    /// Get special holiday name in Spanish (if applicable)
    /// </summary>
    public static string? GetSpecialHolidayName(this DateTime date)
    {
        string monthDay = date.ToString("MM-dd");

        return monthDay switch
        {
            "12-24" => "Nochebuena",
            "12-25" => "Navidad",
            "12-31" => "Nochevieja",
            "01-01" => "Año Nuevo",
            "01-05" => "Víspera de Reyes",
            "01-06" => "Día de Reyes",
            _ => null
        };
    }
}
```

---

### Unit Test Example

```csharp
[TestClass]
public class SpecialHolidayTests
{
    private DateValidationService _service;

    [TestInitialize]
    public void Setup()
    {
        _service = new DateValidationService();
    }

    [TestMethod]
    public void IsSpecialHoliday_ChristmasEve_ReturnsTrue()
    {
        // Arrange
        var christmasEve2025 = new DateTime(2025, 12, 24);

        // Act
        bool result = _service.IsSpecialHoliday(christmasEve2025);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsSpecialHoliday_ChristmasDay_ReturnsTrue()
    {
        // Arrange
        var christmasDay2026 = new DateTime(2026, 12, 25);

        // Act
        bool result = _service.IsSpecialHoliday(christmasDay2026);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsSpecialHoliday_NewYearsEve_ReturnsTrue()
    {
        // Arrange
        var newYearsEve2025 = new DateTime(2025, 12, 31);

        // Act
        bool result = _service.IsSpecialHoliday(newYearsEve2025);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsSpecialHoliday_NewYearsDay_ReturnsTrue()
    {
        // Arrange
        var newYearsDay2026 = new DateTime(2026, 1, 1);

        // Act
        bool result = _service.IsSpecialHoliday(newYearsDay2026);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsSpecialHoliday_EpiphanyEve_ReturnsTrue()
    {
        // Arrange
        var epiphanyEve2025 = new DateTime(2025, 1, 5);

        // Act
        bool result = _service.IsSpecialHoliday(epiphanyEve2025);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsSpecialHoliday_Epiphany_ReturnsTrue()
    {
        // Arrange
        var epiphany2026 = new DateTime(2026, 1, 6);

        // Act
        bool result = _service.IsSpecialHoliday(epiphany2026);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsSpecialHoliday_RegularDate_ReturnsFalse()
    {
        // Arrange
        var regularDate = new DateTime(2025, 11, 28);

        // Act
        bool result = _service.IsSpecialHoliday(regularDate);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsSpecialHoliday_WorksAcrossYears()
    {
        // Arrange
        var christmas2025 = new DateTime(2025, 12, 25);
        var christmas2030 = new DateTime(2030, 12, 25);
        var christmas2099 = new DateTime(2099, 12, 25);

        // Act & Assert
        Assert.IsTrue(_service.IsSpecialHoliday(christmas2025));
        Assert.IsTrue(_service.IsSpecialHoliday(christmas2030));
        Assert.IsTrue(_service.IsSpecialHoliday(christmas2099));
    }

    [TestMethod]
    public async Task ValidateBookingDateAsync_SpecialHoliday_ReturnsRejection()
    {
        // Arrange
        var christmas = new DateTime(2025, 12, 25);

        // Act
        var result = await _service.ValidateBookingDateAsync(christmas);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.IsFalse(result.IsAvailable);
        Assert.AreEqual("special_holiday", result.Reason);
        Assert.IsTrue(result.Message.Contains("menú especial"));
    }

    [TestMethod]
    public void GetSpecialHolidayName_Christmas_ReturnsNavidad()
    {
        // Arrange
        var christmas = new DateTime(2025, 12, 25);

        // Act
        string? name = christmas.GetSpecialHolidayName();

        // Assert
        Assert.AreEqual("Navidad", name);
    }
}
```

---

## Integration with Conversation Flow

### Conversation Agent Implementation

```csharp
public async Task<string> HandleDateInputAsync(string userMessage, ConversationContext context)
{
    // Parse date from user message
    DateTime? parsedDate = _dateParserService.ParseSpanishDate(userMessage);

    if (parsedDate == null)
    {
        return "No pude entender la fecha. ¿Puedes escribirla así? Por ejemplo: 28/11/2025";
    }

    // VALIDATE DATE (special holidays checked FIRST)
    var validation = await _dateValidationService.ValidateBookingDateAsync(parsedDate.Value);

    if (!validation.IsValid)
    {
        // Send rejection message (includes reason)
        await _whatsAppService.SendMessageAsync(
            phoneNumber: context.PhoneNumber,
            message: validation.Message
        );

        // Handle based on reason
        switch (validation.Reason)
        {
            case "special_holiday":
                // Special holiday - suggest calling restaurant
                await _whatsAppService.SendMessageAsync(
                    phoneNumber: context.PhoneNumber,
                    message: "Para reservas en fechas festivas, por favor llámanos al 📞 [phone number]"
                );
                // Keep conversation in DATE_REQUESTED state (allow retry)
                return "special_holiday_suggested_call";

            case "closed_day":
                // Closed day - suggest alternative dates
                var nextOpenDates = await _dateValidationService.GetNextOpenDatesAsync(count: 3);
                var datesMessage = $"Estos días estamos abiertos: {string.Join(", ", nextOpenDates.Select(d => d.ToString("dd/MM")))} 📅";
                await _whatsAppService.SendMessageAsync(
                    phoneNumber: context.PhoneNumber,
                    message: datesMessage
                );
                // Keep conversation in DATE_REQUESTED state (allow retry)
                return "closed_day_suggested_alternatives";

            case "too_far_future":
            case "past_date":
                // Invalid range - allow retry
                return "invalid_date_range";

            default:
                return "date_validation_failed";
        }
    }

    // Date is valid - save and proceed to party size
    context.BookingDate = parsedDate.Value;
    await _contextService.SaveContextAsync(context);

    // Request party size
    await _whatsAppService.SendMessageAsync(
        phoneNumber: context.PhoneNumber,
        message: $"Perfecto, {parsedDate.Value:dd/MM/yyyy}. ¿Para cuántas personas? 👥"
    );

    context.ConversationState = "PARTY_SIZE_REQUESTED";
    await _contextService.SaveContextAsync(context);

    return "date_accepted";
}
```

---

## Data Structures

### Special Holiday Model

```csharp
public class SpecialHolidayInfo
{
    public string MonthDay { get; set; } = string.Empty; // "12-24"
    public string SpanishName { get; set; } = string.Empty; // "Nochebuena"
    public string EnglishName { get; set; } = string.Empty; // "Christmas Eve"
    public int Month { get; set; } // 12
    public int Day { get; set; } // 24

    /// <summary>
    /// Check if a given date matches this special holiday
    /// </summary>
    public bool MatchesDate(DateTime date)
    {
        return date.Month == Month && date.Day == Day;
    }

    /// <summary>
    /// Get the next occurrence of this holiday from a given date
    /// </summary>
    public DateTime GetNextOccurrence(DateTime fromDate)
    {
        var thisYear = new DateTime(fromDate.Year, Month, Day);
        if (thisYear >= fromDate)
            return thisYear;

        return new DateTime(fromDate.Year + 1, Month, Day);
    }
}
```

---

### Complete Special Holidays List (Structured)

```csharp
public static class SpecialHolidaysData
{
    public static readonly List<SpecialHolidayInfo> AllHolidays = new()
    {
        new SpecialHolidayInfo
        {
            MonthDay = "12-24",
            SpanishName = "Nochebuena",
            EnglishName = "Christmas Eve",
            Month = 12,
            Day = 24
        },
        new SpecialHolidayInfo
        {
            MonthDay = "12-25",
            SpanishName = "Navidad",
            EnglishName = "Christmas Day",
            Month = 12,
            Day = 25
        },
        new SpecialHolidayInfo
        {
            MonthDay = "12-31",
            SpanishName = "Nochevieja",
            EnglishName = "New Year's Eve",
            Month = 12,
            Day = 31
        },
        new SpecialHolidayInfo
        {
            MonthDay = "01-01",
            SpanishName = "Año Nuevo",
            EnglishName = "New Year's Day",
            Month = 1,
            Day = 1
        },
        new SpecialHolidayInfo
        {
            MonthDay = "01-05",
            SpanishName = "Víspera de Reyes",
            EnglishName = "Epiphany Eve",
            Month = 1,
            Day = 5
        },
        new SpecialHolidayInfo
        {
            MonthDay = "01-06",
            SpanishName = "Día de Reyes",
            EnglishName = "Epiphany / Three Kings Day",
            Month = 1,
            Day = 6
        }
    };
}
```

---

## Validation Chain Summary

### Complete Validation Order

**Order of Checks** (from highest to lowest priority):

```
1. ✓ Special Holidays Check (HIGHEST)
   ├─ Reason: 'special_holiday'
   ├─ Overrides: Everything
   └─ Exit: Immediate

2. ✓ Closed Days Check
   ├─ Reason: 'closed_day'
   ├─ Overrides: 35-day range, capacity
   └─ Logic: opened_days > closed_days > default schedule

3. ✓ 35-Day Range Check
   ├─ Reason: 'too_far_future' or 'past_date'
   ├─ Overrides: Capacity checks
   └─ Logic: 0 ≤ days_until ≤ 35

4. ✓ Simple Check Mode (if no party_size)
   ├─ Reason: 'date_open'
   ├─ Returns: Quick date availability
   └─ Skips: Capacity validation

5. ✓ Hourly Availability Check (if party_size provided)
   ├─ Reason: 'no_availability'
   ├─ Checks: Time slots from gethourdata.php
   └─ Next: Daily capacity check

6. ✓ Daily Capacity Check
   ├─ Reason: 'fully_booked'
   ├─ Checks: daily_limits table
   └─ Final: Return availability result
```

---

### Reason Codes

| Reason Code | Priority | Description | Can Override? |
|-------------|----------|-------------|---------------|
| `special_holiday` | 1 (HIGHEST) | Date is a special menu day | NO - Absolute block |
| `closed_day` | 2 | Restaurant closed (explicit or default) | Yes - opened_days can override |
| `too_far_future` | 3 | Date is >35 days away | NO - Business rule |
| `past_date` | 3 | Date is in the past | NO - Logical constraint |
| `date_open` | 4 | Date is available (simple check) | N/A - Success response |
| `no_availability` | 5 | No time slots available | NO - Capacity exhausted |
| `fully_booked` | 6 | Daily capacity reached | NO - Capacity exhausted |

---

## Issues Encountered

None. All requirements successfully completed.

---

## Blockers

None. All acceptance criteria met.

---

## Context for Next Step

### What We've Documented (Step 14)

**Special Holidays Configuration**:
- **Total Count**: 6 hardcoded dates
- **Format**: `MM-DD` (year-agnostic)
- **List**: 12-24, 12-25, 12-31, 01-01, 01-05, 01-06
- **Location**: check_date_availability.php, Line 117
- **Business Context**: Special menus requiring manual management

**Validation Logic**:
- **File**: check_date_availability.php (Lines 114-129)
- **Step**: STEP 2 (second check in validation chain)
- **Method**: Extract MM-DD, check against hardcoded list
- **Response**: `{success: true, available: false, reason: 'special_holiday', message: '...'}`
- **Exit Behavior**: Immediate termination (no further checks)

**Spanish Message**:
- **Full Text**: "Uy, esos días festivos (24, 25 y 31 de diciembre, 1, 5 y 6 de enero) tienen un menú especial y no se pueden modificar reservas. ¿Prefieres otro día? 😊"
- **Tone**: Friendly, apologetic, helpful
- **Components**: Alert + date list + reason + restriction + suggestion + emoji
- **Language**: Spanish (informal/casual)

**Priority in Validation Chain**:
- **Rank**: HIGHEST (Step 2, checked before closed_days)
- **Override Power**: Overrides everything (even opened_days=TRUE)
- **Example**: Christmas in opened_days → Still blocked by special_holiday check

**Business Rules**:
- **Why Blocked**: Special menus, high demand, manual management required
- **Year-Agnostic**: Same dates every year (12-25 applies to 2025, 2026, etc.)
- **No Modifications**: Applies to both new bookings AND changes
- **Manual Alternative**: Customers must call/visit restaurant for these dates

**C# Implementation**:
- **Configuration**: SpecialHolidays static class with Dates array
- **Validation**: IsSpecialHoliday(DateTime date) method
- **Integration**: ValidateBookingDateAsync checks special holidays FIRST
- **Response Model**: DateValidationResult with Reason and Message fields
- **Extension Methods**: IsSpecialHoliday() and GetSpecialHolidayName()

---

### System State After This Step

**Complete Validation Chain Now Documented**:
1. ✓ Special Holidays (Step 14) - HIGHEST PRIORITY - COMPLETED
2. ✓ Closed Days (Step 13) - opened_days > closed_days > default - COMPLETED
3. Pending: 35-Day Range Check (future step)
4. Pending: Hourly Availability (gethourdata.php)
5. Pending: Daily Capacity (daily_limits table)

**Database Tables Documented**:
- Step 11: ✓ Bookings table (19 columns)
- Step 12: ✓ FINDE table (rice types)
- Step 13: ✓ restaurant_days table (closed/opened days)
- Step 14: ✓ Special holidays (hardcoded, not in database)
- Next: daily_limits, hour_configuration, openinghours

**API Endpoints Documented**:
- ✓ get_available_rice_types.php - Rice types query
- ✓ fetch_closed_days.php - Closed/opened days
- ✓ check_date_availability.php - Date validation (PARTIAL - special holidays + closed days documented)
- Pending: gethourdata.php - Hourly availability
- Pending: Full check_date_availability.php analysis (capacity checks)

---

### Files Ready for Next Step

**For 35-Day Range Documentation** (Step 15):
- `/home/jaime/Documents/projects/alqueriavillacarmen/api/check_date_availability.php` (Lines 150-165) - Range validation logic
- Current output: `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/steps/step-14-output.md`

**For Hourly Availability** (future steps):
- `/home/jaime/Documents/projects/alqueriavillacarmen/api/gethourdata.php` - Time slot availability
- `/home/jaime/Documents/projects/alqueriavillacarmen/villacarmen_schema.sql` - hour_configuration, openinghours tables

**For Daily Capacity** (future steps):
- `/home/jaime/Documents/projects/alqueriavillacarmen/api/check_date_availability.php` (Lines 191-342) - Full check mode
- `/home/jaime/Documents/projects/alqueriavillacarmen/villacarmen_schema.sql` - daily_limits table

---

### Critical Information for C# Implementation

**Service Integration**:
```csharp
// Add to DateValidationService
public async Task<DateValidationResult> ValidateBookingDateAsync(DateTime date)
{
    // STEP 1: Special holidays (HIGHEST PRIORITY)
    if (IsSpecialHoliday(date))
    {
        return new DateValidationResult
        {
            IsValid = false,
            Reason = "special_holiday",
            Message = SpecialHolidays.SpanishMessage
        };
    }

    // STEP 2: Closed days
    bool isClosed = await _restaurantDaysService.IsDateClosedAsync(date);
    // ... (continue with other checks)
}
```

**Hardcoded Configuration**:
```csharp
public static class SpecialHolidays
{
    public static readonly string[] Dates = new[]
    {
        "12-24", "12-25", "12-31", "01-01", "01-05", "01-06"
    };

    public const string SpanishMessage =
        "Uy, esos días festivos (24, 25 y 31 de diciembre, 1, 5 y 6 de enero) tienen un menú especial y no se pueden modificar reservas. ¿Prefieres otro día? 😊";
}
```

**Validation Flow**:
```csharp
// In conversation agent
var validation = await _dateValidationService.ValidateBookingDateAsync(requestedDate);

if (validation.Reason == "special_holiday")
{
    // Send rejection message
    await _whatsAppService.SendMessageAsync(validation.Message);

    // Optionally suggest phone booking
    await _whatsAppService.SendMessageAsync(
        "Para reservas en fechas festivas, llámanos al 📞 [phone]"
    );
}
```

---

## Verification

- [x] Step 14 requirements reviewed from steps file
- [x] check_date_availability.php special holidays section analyzed (Lines 114-129)
- [x] All 6 special holiday dates documented (12-24, 12-25, 12-31, 01-01, 01-05, 01-06)
- [x] Month-day format (MM-DD) documented
- [x] Spanish message template documented with complete text
- [x] English translation provided
- [x] Message components analyzed (alert, dates, reason, restriction, suggestion, emoji)
- [x] Priority in validation chain documented (HIGHEST - Step 2, before closed_days)
- [x] Override behavior explained (overrides opened_days entries)
- [x] Business context documented (special menus, manual management)
- [x] Year-agnostic logic explained
- [x] Validation logic breakdown provided (extract MM-DD, check array, exit immediately)
- [x] Response structure documented (success, available, hasAvailability, message, reason)
- [x] C# implementation provided (SpecialHolidays class, validation method, response model)
- [x] Unit tests provided (all 6 holidays + regular dates + cross-year tests)
- [x] Conversation flow integration example provided
- [x] Extension methods documented
- [x] No issues or blockers encountered
- [x] Output file created at correct path
- [x] Status marked as COMPLETED

---

## Progress Update

**Updated**: `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/progress.json`

```json
{
  "current_step": 14,
  "steps_completed": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14]
}
```
