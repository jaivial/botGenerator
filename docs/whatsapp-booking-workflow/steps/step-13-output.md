# Step 13 Output: Analyze closed_days and opened_days Logic

## Status: COMPLETED

## Execution Summary
Successfully analyzed the closed_days and opened_days logic used for restaurant day status determination. The system uses a **single unified table** (`restaurant_days`) with a boolean `is_open` flag instead of separate tables. The override mechanism allows explicit control over default day-of-week schedules: `is_open=TRUE` overrides default Monday/Tuesday/Wednesday closures, while `is_open=FALSE` explicitly closes normally open days (Thursday-Sunday).

## Files Read
- `/home/jaime/Documents/projects/alqueriavillacarmen/fetch_closed_days.php` - Fetches closed/opened days (54 lines)
- `/home/jaime/Documents/projects/alqueriavillacarmen/api/check_date_availability.php` - Date availability validation API (352 lines)
- `/home/jaime/Documents/projects/alqueriavillacarmen/villacarmen_schema.sql` - restaurant_days table schema (lines 252-265)
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/progress.json` - Current progress
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/steps/step-12-output.md` - Prior context

## Files Created
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/steps/step-13-output.md` - This documentation

---

## restaurant_days Table Structure

### Table Metadata

**Table Name**: `restaurant_days`

**Engine**: InnoDB

**Charset**: utf8mb4

**Collation**: utf8mb4_unicode_ci

**Auto Increment**: 155 (current value in schema)

**Location**: `/home/jaime/Documents/projects/alqueriavillacarmen/villacarmen_schema.sql` (Lines 258-264)

**Purpose**: Stores explicit open/closed date overrides for the restaurant's default weekly schedule. Allows restaurant managers to override the default Monday/Tuesday/Wednesday closure pattern or close normally open days (Thursday-Sunday).

---

### Complete Column Definitions

#### Primary Key

**Column**: `id`
- **Type**: `INT NOT NULL AUTO_INCREMENT`
- **Constraint**: `PRIMARY KEY`
- **Description**: Unique identifier for each date record
- **Required**: Yes (auto-generated)
- **Example**: `1`, `2`, `154`
- **C# Mapping**: `int Id { get; set; }`

---

#### Date Column

**Column**: `date`
- **Type**: `DATE NOT NULL`
- **Constraint**: `UNIQUE KEY`
- **Description**: Specific calendar date being configured (YYYY-MM-DD format)
- **Required**: YES
- **Unique**: YES (enforced by UNIQUE constraint)
- **Default**: None
- **Format**: MySQL DATE type (YYYY-MM-DD)
- **Example**:
  - `2025-12-25` (Christmas - explicitly closed)
  - `2025-11-27` (Monday - explicitly opened)
  - `2025-01-01` (New Year's Day - explicitly closed)
- **C# Mapping**: `DateTime Date { get; set; }` or `DateOnly Date { get; set; }`
- **Validation**: Cannot be null or empty, must be unique
- **Index**: UNIQUE index ensures one row per date (prevents duplicates)

---

#### Open/Closed Status Flag

**Column**: `is_open`
- **Type**: `TINYINT(1) NOT NULL`
- **Default**: `1` (open)
- **Description**: Boolean flag indicating if restaurant is open (TRUE/1) or closed (FALSE/0) on this specific date
- **Required**: YES
- **Values**:
  - `0` (FALSE) - Restaurant is CLOSED on this date (explicit closure)
  - `1` (TRUE) - Restaurant is OPEN on this date (explicit opening)
- **Example**: `1`, `0`
- **C# Mapping**: `bool IsOpen { get; set; } = true`
- **Business Logic**:
  - `is_open=TRUE`: Overrides default Mon/Tue/Wed closures (special opening)
  - `is_open=FALSE`: Overrides default Thu/Fri/Sat/Sun openings (special closure)

---

### Indexes

**Primary Key Index**:
```sql
PRIMARY KEY (`id`)
```
- **Type**: PRIMARY KEY
- **Column**: `id`
- **Purpose**: Unique identifier lookup, fast row access
- **Auto Increment**: Yes

**Unique Date Index**:
```sql
UNIQUE KEY `date` (`date`)
```
- **Type**: UNIQUE KEY
- **Column**: `date`
- **Purpose**: Ensures only one record per date, prevents duplicate date entries
- **Performance**: Fast lookup by specific date
- **Constraint**: Enforces date uniqueness at database level

---

### Table Summary

| Column | Type | Required | Default | Constraint | Description |
|--------|------|----------|---------|------------|-------------|
| `id` | INT | YES | AUTO_INCREMENT | PRIMARY KEY | Unique record ID |
| `date` | DATE | YES | - | UNIQUE | Specific calendar date |
| `is_open` | TINYINT(1) | YES | 1 | - | Open (1) or closed (0) flag |

---

## Closed Days and Opened Days Logic

### IMPORTANT: No Separate Tables

**Critical Clarification**: The system does **NOT** use separate `closed_days` and `opened_days` tables. Instead, it uses a **single unified table** (`restaurant_days`) with a boolean `is_open` flag.

**Conceptual Separation**:
```
closed_days = SELECT date FROM restaurant_days WHERE is_open = FALSE
opened_days = SELECT date FROM restaurant_days WHERE is_open = TRUE
```

**Why This Matters**:
- Simpler database schema (one table vs two)
- Easier maintenance (single source of truth)
- Clear semantic meaning (is_open flag)
- No risk of conflicting records (can't be both open and closed)

---

### fetch_closed_days.php Query Logic

**File Location**: `/home/jaime/Documents/projects/alqueriavillacarmen/fetch_closed_days.php`

**Purpose**: Fetches lists of explicitly closed and opened dates from the database

---

#### Query 1: Fetch Closed Days

**SQL Query** (Lines 15-16):
```sql
SELECT date FROM restaurant_days WHERE is_open = FALSE
```

**Breakdown**:
- **Table**: `restaurant_days`
- **Filter**: `is_open = FALSE` (or `is_open = 0`)
- **Returns**: Array of DATE values (YYYY-MM-DD format)
- **Meaning**: Dates where restaurant is explicitly CLOSED (overrides default schedule)

**Example Result**:
```php
[
    '2025-12-24', // Christmas Eve
    '2025-12-25', // Christmas Day
    '2025-12-31', // New Year's Eve
    '2026-01-01', // New Year's Day
    '2025-08-15', // Summer closure (normally open Thu-Sun)
]
```

**Use Case**:
- Special holidays/closures
- Vacation days
- Emergency closures
- Closing normally open days (Thu/Fri/Sat/Sun)

---

#### Query 2: Fetch Opened Days

**SQL Query** (Lines 25-26):
```sql
SELECT date FROM restaurant_days WHERE is_open = TRUE
```

**Breakdown**:
- **Table**: `restaurant_days`
- **Filter**: `is_open = TRUE` (or `is_open = 1`)
- **Returns**: Array of DATE values (YYYY-MM-DD format)
- **Meaning**: Dates where restaurant is explicitly OPEN (overrides default closures)

**Example Result**:
```php
[
    '2025-11-27', // Special Monday opening (normally closed)
    '2025-12-23', // Special Tuesday opening (normally closed)
    '2025-01-08', // Special Wednesday opening (normally closed)
]
```

**Use Case**:
- Special event days (weddings, private events)
- Holiday exceptions (open on Monday for special event)
- Override default Mon/Tue/Wed closures

---

#### API Response Structure

**Success Response**:
```json
{
  "success": true,
  "closed_days": [
    "2025-12-24",
    "2025-12-25",
    "2025-12-31",
    "2026-01-01"
  ],
  "opened_days": [
    "2025-11-27",
    "2025-12-23"
  ]
}
```

**Error Response**:
```json
{
  "success": false,
  "message": "Error fetching closed days: [exception message]"
}
```

**Response Fields**:
- **success** (boolean): True if query succeeded, false on error
- **closed_days** (string[]): Array of YYYY-MM-DD dates with is_open=FALSE
- **opened_days** (string[]): Array of YYYY-MM-DD dates with is_open=TRUE
- **message** (string, error only): Exception error message

---

## Override Mechanism (Priority System)

### Default Weekly Schedule

**Restaurant's Normal Schedule**:
- **Monday**: CLOSED (default)
- **Tuesday**: CLOSED (default)
- **Wednesday**: CLOSED (default)
- **Thursday**: OPEN (default)
- **Friday**: OPEN (default)
- **Saturday**: OPEN (default)
- **Sunday**: OPEN (default)

**Implementation** (check_date_availability.php, lines 41-44):
```php
function isNormallyClosedDay($date) {
    $dayOfWeek = date('N', strtotime($date)); // 1=Monday, 7=Sunday
    // Monday=1, Tuesday=2, Wednesday=3 are normally closed
    return in_array($dayOfWeek, [1, 2, 3]);
}
```

---

### Priority/Override Mechanism

**Logic Flow** (check_date_availability.php, lines 62-78):

```php
function isDateClosed($date, $closedDays, $openedDays) {
    // Step 1: Check if explicitly opened (HIGHEST PRIORITY - overrides everything)
    $isExplicitlyOpened = in_array($date, $openedDays);
    if ($isExplicitlyOpened) {
        return false; // Opened days are never closed
    }

    // Step 2: Check if explicitly closed
    $isExplicitlyClosed = in_array($date, $closedDays);
    if ($isExplicitlyClosed) {
        return true; // Explicitly closed
    }

    // Step 3: Not in either list - check default schedule by day of week
    // Monday, Tuesday, Wednesday = normally closed
    // Thursday, Friday, Saturday, Sunday = normally open
    return isNormallyClosedDay($date);
}
```

---

### Priority Order (Highest to Lowest)

**1. HIGHEST PRIORITY: Explicitly Opened Days (is_open = TRUE)**
- **Source**: `restaurant_days` table with `is_open = TRUE`
- **Effect**: Date is ALWAYS OPEN, regardless of default schedule
- **Overrides**: Default Mon/Tue/Wed closures
- **Use Case**: Open on Monday for special event

**Example**:
```
Date: 2025-11-27 (Monday)
Default Schedule: Monday = CLOSED
restaurant_days record: {date: '2025-11-27', is_open: TRUE}
Result: OPEN (override applied)
```

**2. MEDIUM PRIORITY: Explicitly Closed Days (is_open = FALSE)**
- **Source**: `restaurant_days` table with `is_open = FALSE`
- **Effect**: Date is ALWAYS CLOSED, regardless of default schedule
- **Overrides**: Default Thu/Fri/Sat/Sun openings
- **Use Case**: Close on Saturday for vacation

**Example**:
```
Date: 2025-08-15 (Saturday)
Default Schedule: Saturday = OPEN
restaurant_days record: {date: '2025-08-15', is_open: FALSE}
Result: CLOSED (override applied)
```

**3. LOWEST PRIORITY: Default Weekly Schedule**
- **Source**: Day of week calculation
- **Effect**: Falls back to Mon/Tue/Wed = CLOSED, Thu/Fri/Sat/Sun = OPEN
- **Applies**: Only when date is NOT in restaurant_days table

**Example**:
```
Date: 2025-11-30 (Sunday)
Default Schedule: Sunday = OPEN
restaurant_days record: None (date not in table)
Result: OPEN (default schedule)
```

---

### Decision Tree

```
For any given date:

┌─────────────────────────────────────┐
│ Is date in opened_days?             │
│ (is_open = TRUE)                    │
└──────────┬──────────────────────────┘
           │
    YES ───┴──→ OPEN (highest priority)
           │
           NO
           ↓
┌─────────────────────────────────────┐
│ Is date in closed_days?             │
│ (is_open = FALSE)                   │
└──────────┬──────────────────────────┘
           │
    YES ───┴──→ CLOSED (medium priority)
           │
           NO
           ↓
┌─────────────────────────────────────┐
│ Check default weekly schedule       │
│ Mon/Tue/Wed = CLOSED                │
│ Thu/Fri/Sat/Sun = OPEN              │
└──────────┬──────────────────────────┘
           │
           ├──→ Mon/Tue/Wed → CLOSED (default)
           └──→ Thu/Fri/Sat/Sun → OPEN (default)
```

---

### Use Case Examples

#### Example 1: Monday Override (Open Special Event)

**Scenario**: Restaurant wants to open on Monday, November 27th for a wedding event

**Input**:
- Date: `2025-11-27` (Monday)
- Day of Week: Monday (normally CLOSED)

**Database Entry**:
```sql
INSERT INTO restaurant_days (date, is_open) VALUES ('2025-11-27', TRUE);
```

**Result**:
- `opened_days` array contains: `'2025-11-27'`
- Priority 1 check: `in_array('2025-11-27', opened_days)` → TRUE
- **Final Result**: OPEN (overrides default Monday closure)

---

#### Example 2: Saturday Closure (Vacation)

**Scenario**: Restaurant closes on Saturday, August 15th for vacation

**Input**:
- Date: `2025-08-15` (Saturday)
- Day of Week: Saturday (normally OPEN)

**Database Entry**:
```sql
INSERT INTO restaurant_days (date, is_open) VALUES ('2025-08-15', FALSE);
```

**Result**:
- `closed_days` array contains: `'2025-08-15'`
- Priority 1 check: Not in `opened_days`
- Priority 2 check: `in_array('2025-08-15', closed_days)` → TRUE
- **Final Result**: CLOSED (overrides default Saturday opening)

---

#### Example 3: Default Thursday (No Override)

**Scenario**: Normal Thursday with no special exceptions

**Input**:
- Date: `2025-11-28` (Thursday)
- Day of Week: Thursday (normally OPEN)

**Database Entry**:
```sql
-- No entry in restaurant_days table for this date
```

**Result**:
- `opened_days` array: Does NOT contain `'2025-11-28'`
- `closed_days` array: Does NOT contain `'2025-11-28'`
- Priority 1 check: Not in `opened_days` → Continue
- Priority 2 check: Not in `closed_days` → Continue
- Priority 3 check: `isNormallyClosedDay('2025-11-28')` → Thursday (4) → NOT in [1,2,3] → FALSE
- **Final Result**: OPEN (default schedule)

---

#### Example 4: Default Wednesday (No Override)

**Scenario**: Normal Wednesday with no special exceptions

**Input**:
- Date: `2025-11-26` (Wednesday)
- Day of Week: Wednesday (normally CLOSED)

**Database Entry**:
```sql
-- No entry in restaurant_days table for this date
```

**Result**:
- `opened_days` array: Does NOT contain `'2025-11-26'`
- `closed_days` array: Does NOT contain `'2025-11-26'`
- Priority 1 check: Not in `opened_days` → Continue
- Priority 2 check: Not in `closed_days` → Continue
- Priority 3 check: `isNormallyClosedDay('2025-11-26')` → Wednesday (3) → in [1,2,3] → TRUE
- **Final Result**: CLOSED (default schedule)

---

## Integration with check_date_availability.php

### Workflow Integration

**File**: `/home/jaime/Documents/projects/alqueriavillacarmen/api/check_date_availability.php`

**Step-by-Step Validation Flow**:

---

#### STEP 1: Fetch Closed/Opened Days (Lines 106-112)

```php
// Fetch from fetch_closed_days.php
$closedDaysResponse = file_get_contents('https://alqueriavillacarmen.com/fetch_closed_days.php');
$closedDaysData = json_decode($closedDaysResponse, true);

$closedDays = $closedDaysData['closed_days'] ?? [];
$openedDays = $closedDaysData['opened_days'] ?? [];
```

**Purpose**: Retrieve current lists of explicit closures and openings

**Example Result**:
```php
$closedDays = ['2025-12-24', '2025-12-25', '2025-12-31'];
$openedDays = ['2025-11-27', '2025-12-23'];
```

---

#### STEP 2: Check Special Holidays (Lines 115-129)

**Special Holidays List**:
```php
$specialHolidays = ['12-24', '12-25', '12-31', '01-01', '01-05', '01-06'];
```

**Holidays Blocked** (cannot book or modify):
- December 24 (Christmas Eve)
- December 25 (Christmas Day)
- December 31 (New Year's Eve)
- January 1 (New Year's Day)
- January 5 (Epiphany Eve)
- January 6 (Epiphany)

**Logic**:
```php
$dateMonthDay = date('m-d', strtotime($newDate)); // Extract MM-DD

if (in_array($dateMonthDay, $specialHolidays)) {
    return [
        'success' => true,
        'available' => false,
        'hasAvailability' => false,
        'message' => 'Special menu days cannot be booked/modified',
        'reason' => 'special_holiday'
    ];
}
```

**Priority**: This check happens BEFORE opened_days/closed_days check (highest priority)

**Reason**: These dates have special menus and require manual management

---

#### STEP 3: Check Date Closed Status (Lines 131-148)

**Uses the Override Mechanism**:
```php
if (isDateClosed($newDate, $closedDays, $openedDays)) {
    $formattedDate = date('d/m/Y', strtotime($newDate));
    return [
        'success' => true,
        'available' => false,
        'hasAvailability' => false,
        'message' => "Ese día ($formattedDate) estamos cerrados. ¿Qué tal otro día? Abrimos jueves, viernes, sábado y domingo 😊",
        'reason' => 'closed_day'
    ];
}
```

**What This Checks**:
1. First checks if date in `opened_days` → If YES, date is OPEN
2. Then checks if date in `closed_days` → If YES, date is CLOSED
3. Finally checks default schedule → Mon/Tue/Wed CLOSED, Thu/Fri/Sat/Sun OPEN

**Example Scenarios**:

**Scenario A**: Monday with override
```php
$newDate = '2025-11-27'; // Monday
$openedDays = ['2025-11-27'];
isDateClosed('2025-11-27', [], ['2025-11-27']) → FALSE (explicitly opened)
Result: Proceed to next validation (date is available)
```

**Scenario B**: Saturday closure
```php
$newDate = '2025-08-15'; // Saturday
$closedDays = ['2025-08-15'];
isDateClosed('2025-08-15', ['2025-08-15'], []) → TRUE (explicitly closed)
Result: Return 'closed_day' error
```

**Scenario C**: Default Wednesday
```php
$newDate = '2025-11-26'; // Wednesday
$closedDays = [];
$openedDays = [];
isDateClosed('2025-11-26', [], []) → TRUE (default Mon/Tue/Wed closure)
Result: Return 'closed_day' error
```

---

#### STEP 4: Check 35-Day Range (Lines 150-165)

**Business Rule**: Only accept reservations up to 35 days in advance

```php
function isDateTooFarInFuture($date) {
    $today = new DateTime();
    $targetDate = new DateTime($date);
    $diff = $today->diff($targetDate);
    $daysUntil = (int)$diff->days;

    return [
        'isTooFar' => $daysUntil > 35,
        'daysUntil' => $daysUntil
    ];
}
```

**Example**:
```php
Today: 2025-11-27
Booking Date: 2026-01-15
Days Until: 49
Result: 'too_far_future' error (49 > 35)
```

---

#### STEP 5: Simple Check Mode (Lines 167-189)

**Trigger**: When only `newDate` provided (no `partySize`)

**Purpose**: Quick date validation without capacity check

**Checks Completed**:
- ✓ Special holidays (blocked)
- ✓ Closed days + default schedule
- ✓ 35-day range limit

**Response**:
```json
{
  "success": true,
  "available": true,
  "hasAvailability": true,
  "message": "El día 28/11/2025 está disponible para reservas 😊",
  "reason": "date_open",
  "simpleCheck": true,
  "isExplicitlyOpened": false
}
```

**Use Case**: Pre-validation before asking for party size

---

#### STEP 6: Full Check Mode (Lines 191-342)

**Trigger**: When `newDate` AND `partySize` provided

**Additional Checks**:
- Hourly availability (via `gethourdata.php`)
- Daily capacity limits
- Party size capacity validation

**Not Covered in Step 13** (future steps will document)

---

## Database Schema Considerations

### No Foreign Key Constraints

**Observation**: `restaurant_days` table has NO foreign key relationships

**Implications**:
- Standalone table (independent of bookings, daily_limits, etc.)
- Simple CRUD operations
- No cascade delete concerns
- Easy to manage via admin interface

---

### UNIQUE Date Constraint

**Constraint**:
```sql
UNIQUE KEY `date` (`date`)
```

**Purpose**: Prevents duplicate entries for the same date

**Enforcement**: Database-level validation

**Example Error** (if attempting duplicate):
```sql
INSERT INTO restaurant_days (date, is_open) VALUES ('2025-11-27', TRUE);
-- ERROR: Duplicate entry '2025-11-27' for key 'restaurant_days.date'
```

**Proper Update Pattern**:
```sql
-- Instead of INSERT (if unsure if exists)
INSERT INTO restaurant_days (date, is_open) VALUES ('2025-11-27', TRUE)
ON DUPLICATE KEY UPDATE is_open = TRUE;
```

---

### Data Integrity

**Benefits of UNIQUE Constraint**:
1. **No Conflicting Rules**: Cannot have date both open AND closed
2. **Clear State**: Each date has exactly one status (or uses default)
3. **Simple Queries**: No need for GROUP BY or DISTINCT
4. **Predictable Behavior**: One row per date maximum

**Trade-off**:
- Must use UPDATE instead of INSERT if changing existing date status
- Consider UPSERT pattern (INSERT ... ON DUPLICATE KEY UPDATE)

---

## C# Model and Service Implementation

### C# Model Class

```csharp
public class RestaurantDay
{
    /// <summary>
    /// Unique identifier for this record
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Specific date being configured (YYYY-MM-DD)
    /// </summary>
    [Required]
    public DateTime Date { get; set; }

    /// <summary>
    /// True if restaurant is explicitly OPEN on this date (overrides default closures)
    /// False if restaurant is explicitly CLOSED on this date (overrides default openings)
    /// </summary>
    [Required]
    public bool IsOpen { get; set; } = true;
}
```

---

### Fetch Closed Days Response Model

```csharp
public class ClosedDaysResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("closed_days")]
    public List<string> ClosedDays { get; set; } = new();

    [JsonPropertyName("opened_days")]
    public List<string> OpenedDays { get; set; } = new();

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
```

---

### Service Interface

```csharp
public interface IRestaurantDaysService
{
    /// <summary>
    /// Fetch current lists of explicitly closed and opened dates
    /// </summary>
    Task<(List<string> ClosedDays, List<string> OpenedDays)> GetClosedAndOpenedDaysAsync();

    /// <summary>
    /// Check if a specific date is closed using priority override logic
    /// </summary>
    Task<bool> IsDateClosedAsync(DateTime date);

    /// <summary>
    /// Check if a date is a normally closed day (Mon/Tue/Wed)
    /// </summary>
    bool IsNormallyClosedDay(DateTime date);

    /// <summary>
    /// Get detailed availability status for a date
    /// </summary>
    Task<DateAvailabilityStatus> GetDateStatusAsync(DateTime date);
}
```

---

### Service Implementation Example

```csharp
public class RestaurantDaysService : IRestaurantDaysService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RestaurantDaysService> _logger;

    // Cache for 5 minutes to reduce API calls
    private static List<string>? _cachedClosedDays;
    private static List<string>? _cachedOpenedDays;
    private static DateTime _cacheExpiry;

    public async Task<(List<string> ClosedDays, List<string> OpenedDays)> GetClosedAndOpenedDaysAsync()
    {
        // Check cache first
        if (_cachedClosedDays != null && _cachedOpenedDays != null && DateTime.Now < _cacheExpiry)
        {
            return (_cachedClosedDays, _cachedOpenedDays);
        }

        try
        {
            var response = await _httpClient.GetAsync("https://alqueriavillacarmen.com/fetch_closed_days.php");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<ClosedDaysResponse>(json);

            if (data?.Success == true)
            {
                // Update cache
                _cachedClosedDays = data.ClosedDays;
                _cachedOpenedDays = data.OpenedDays;
                _cacheExpiry = DateTime.Now.AddMinutes(5);

                return (data.ClosedDays, data.OpenedDays);
            }

            _logger.LogError("Failed to fetch closed/opened days: {Message}", data?.Message);
            return (new List<string>(), new List<string>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching closed/opened days");
            return (new List<string>(), new List<string>());
        }
    }

    public bool IsNormallyClosedDay(DateTime date)
    {
        // Monday=1, Tuesday=2, Wednesday=3 are normally closed
        int dayOfWeek = (int)date.DayOfWeek;

        // Convert .NET DayOfWeek (Sunday=0) to ISO (Monday=1)
        int isoDayOfWeek = dayOfWeek == 0 ? 7 : dayOfWeek;

        return isoDayOfWeek >= 1 && isoDayOfWeek <= 3;
    }

    public async Task<bool> IsDateClosedAsync(DateTime date)
    {
        var (closedDays, openedDays) = await GetClosedAndOpenedDaysAsync();
        string dateString = date.ToString("yyyy-MM-dd");

        // Priority 1: Explicitly opened (highest priority)
        if (openedDays.Contains(dateString))
        {
            return false; // OPEN (override)
        }

        // Priority 2: Explicitly closed
        if (closedDays.Contains(dateString))
        {
            return true; // CLOSED (override)
        }

        // Priority 3: Default schedule (lowest priority)
        return IsNormallyClosedDay(date);
    }

    public async Task<DateAvailabilityStatus> GetDateStatusAsync(DateTime date)
    {
        var (closedDays, openedDays) = await GetClosedAndOpenedDaysAsync();
        string dateString = date.ToString("yyyy-MM-dd");

        bool isExplicitlyOpened = openedDays.Contains(dateString);
        bool isExplicitlyClosed = closedDays.Contains(dateString);
        bool isClosed = await IsDateClosedAsync(date);

        return new DateAvailabilityStatus
        {
            Date = date,
            IsClosed = isClosed,
            IsOpen = !isClosed,
            IsExplicitlyOpened = isExplicitlyOpened,
            IsExplicitlyClosed = isExplicitlyClosed,
            IsDefaultSchedule = !isExplicitlyOpened && !isExplicitlyClosed,
            IsNormallyClosedDay = IsNormallyClosedDay(date)
        };
    }
}

public class DateAvailabilityStatus
{
    public DateTime Date { get; set; }
    public bool IsClosed { get; set; }
    public bool IsOpen { get; set; }
    public bool IsExplicitlyOpened { get; set; }
    public bool IsExplicitlyClosed { get; set; }
    public bool IsDefaultSchedule { get; set; }
    public bool IsNormallyClosedDay { get; set; }
}
```

---

### Usage Examples

#### Example 1: Check if Date is Available

```csharp
public async Task<bool> CanBookDate(DateTime requestedDate)
{
    // Check if date is closed
    bool isClosed = await _restaurantDaysService.IsDateClosedAsync(requestedDate);

    if (isClosed)
    {
        _logger.LogInformation("Date {Date} is closed", requestedDate.ToShortDateString());
        return false;
    }

    // Additional checks (special holidays, 35-day range, etc.)
    // ...

    return true;
}
```

---

#### Example 2: Get Detailed Date Status

```csharp
public async Task<string> GetDateStatusMessage(DateTime date)
{
    var status = await _restaurantDaysService.GetDateStatusAsync(date);

    if (status.IsExplicitlyOpened)
    {
        return $"{date:dd/MM/yyyy} - Abierto especialmente (normalmente cerrado los {GetDayName(date)})";
    }

    if (status.IsExplicitlyClosed)
    {
        return $"{date:dd/MM/yyyy} - Cerrado excepcionalmente (normalmente abierto los {GetDayName(date)})";
    }

    if (status.IsClosed)
    {
        return $"{date:dd/MM/yyyy} - Cerrado (horario normal: lunes, martes, miércoles cerrados)";
    }

    return $"{date:dd/MM/yyyy} - Abierto (horario normal)";
}
```

---

#### Example 3: Validate Booking Date

```csharp
public async Task<(bool IsValid, string ErrorMessage)> ValidateBookingDate(DateTime date)
{
    // Check special holidays first (highest priority)
    var specialHolidays = new[] { "12-24", "12-25", "12-31", "01-01", "01-05", "01-06" };
    string monthDay = date.ToString("MM-dd");

    if (specialHolidays.Contains(monthDay))
    {
        return (false, "Ese día festivo tiene menú especial y no acepta reservas online");
    }

    // Check if date is closed (includes override logic)
    bool isClosed = await _restaurantDaysService.IsDateClosedAsync(date);

    if (isClosed)
    {
        return (false, $"Estamos cerrados el {date:dd/MM/yyyy}. Abrimos jueves, viernes, sábado y domingo");
    }

    // Check 35-day range
    var daysUntil = (date - DateTime.Today).Days;
    if (daysUntil > 35)
    {
        return (false, "Solo aceptamos reservas con máximo 35 días de antelación");
    }

    if (daysUntil < 0)
    {
        return (false, "No se puede reservar en fechas pasadas");
    }

    return (true, string.Empty);
}
```

---

## Data Type Mappings

### MySQL → C# Type Mappings

| MySQL Type | C# Type | Notes |
|------------|---------|-------|
| `INT` | `int` | Primary key `id` |
| `DATE` | `DateTime` or `DateOnly` | Date column (YYYY-MM-DD) |
| `TINYINT(1)` | `bool` | Boolean flag `is_open` |

### JSON API → C# Mappings

| JSON Type | C# Type | Notes |
|-----------|---------|-------|
| `string[]` (closed_days) | `List<string>` | Array of YYYY-MM-DD strings |
| `string[]` (opened_days) | `List<string>` | Array of YYYY-MM-DD strings |
| `boolean` (success) | `bool` | Success flag |
| `string` (message) | `string?` | Optional error message |

---

## Business Logic Rules

### Day of Week Mapping

**ISO 8601 Standard** (used by PHP `date('N')`):
- 1 = Monday
- 2 = Tuesday
- 3 = Wednesday
- 4 = Thursday
- 5 = Friday
- 6 = Saturday
- 7 = Sunday

**.NET DayOfWeek Enum**:
- 0 = Sunday
- 1 = Monday
- 2 = Tuesday
- 3 = Wednesday
- 4 = Thursday
- 5 = Friday
- 6 = Saturday

**Conversion Required**:
```csharp
public int GetIsoDayOfWeek(DateTime date)
{
    int dotNetDay = (int)date.DayOfWeek; // 0-6 (Sunday=0)
    return dotNetDay == 0 ? 7 : dotNetDay; // 1-7 (Monday=1)
}
```

---

### Default Weekly Schedule

**Normally Closed Days**: Monday (1), Tuesday (2), Wednesday (3)
**Normally Open Days**: Thursday (4), Friday (5), Saturday (6), Sunday (7)

---

### Override Use Cases

**Use `is_open = TRUE` (opened_days) when**:
- Opening on a normally closed day (Monday/Tuesday/Wednesday)
- Special events requiring Monday/Tuesday/Wednesday opening
- Weddings, private parties, special occasions

**Use `is_open = FALSE` (closed_days) when**:
- Closing on a normally open day (Thursday-Sunday)
- Vacation days
- Maintenance closures
- Emergency closures
- Public holidays

---

## Performance Considerations

### Caching Strategy

**Why Cache**:
- `fetch_closed_days.php` called frequently (every date validation)
- Data changes infrequently (restaurant schedule updates are rare)
- Reduces database load
- Improves response time

**Recommended Cache Duration**: 5 minutes

**Cache Implementation**:
```csharp
private static List<string>? _cachedClosedDays;
private static List<string>? _cachedOpenedDays;
private static DateTime _cacheExpiry;

if (_cachedClosedDays != null && DateTime.Now < _cacheExpiry)
{
    return (_cachedClosedDays, _cachedOpenedDays);
}
```

**Cache Invalidation**:
- Time-based: Expire after 5 minutes
- Manual: Clear cache when admin updates restaurant_days table
- Consider: Event-driven invalidation via webhook/signaling

---

### Query Performance

**Current Performance**:
- `SELECT date FROM restaurant_days WHERE is_open = FALSE`
  - **Index**: PRIMARY KEY on `id`, UNIQUE KEY on `date`
  - **Filter**: `is_open` column (no index)
  - **Rows**: Typically <100 rows (minimal impact)
  - **Performance**: Acceptable (small dataset, simple query)

**Optimization Recommendation** (if dataset grows):
```sql
-- Add index on is_open for faster filtering
CREATE INDEX idx_restaurant_days_is_open ON restaurant_days(is_open);

-- Or composite index for specific queries
CREATE INDEX idx_restaurant_days_is_open_date ON restaurant_days(is_open, date);
```

**Expected Performance**:
- With current dataset (<155 rows): <1ms per query
- With index: <0.5ms per query
- Cache hit: <0.1ms (in-memory)

---

## Integration Points

### With check_date_availability.php

**How It's Used**:
```php
// Step 1: Fetch override lists
$closedDaysResponse = file_get_contents('https://alqueriavillacarmen.com/fetch_closed_days.php');
$closedDaysData = json_decode($closedDaysResponse, true);
$closedDays = $closedDaysData['closed_days'] ?? [];
$openedDays = $closedDaysData['opened_days'] ?? [];

// Step 2: Check if date is closed (uses priority logic)
if (isDateClosed($newDate, $closedDays, $openedDays)) {
    // Return 'closed_day' error
}
```

**Validation Order in check_date_availability.php**:
1. Special holidays (12-24, 12-25, 12-31, 01-01, 01-05, 01-06)
2. **Closed/Opened days check** ← restaurant_days integration
3. 35-day range check
4. Hourly availability check (if party_size provided)
5. Daily capacity check (if party_size provided)

---

### With Booking Creation Flow

**When Creating New Booking**:
```csharp
// Validate date before accepting reservation
var (isValid, errorMessage) = await ValidateBookingDate(requestedDate);

if (!isValid)
{
    await SendWhatsAppMessage(errorMessage);
    return; // Don't create booking
}

// Proceed with booking creation
var booking = new Booking
{
    ReservationDate = requestedDate,
    // ... other fields
};
```

---

### With Admin Interface (Future)

**Potential Admin Features**:
- Add/remove explicitly closed days
- Add/remove explicitly opened days
- View calendar with override highlights
- Bulk import holiday closures
- Generate reports of override dates

**CRUD Operations**:
```csharp
// Add explicit closure
public async Task CloseDate(DateTime date)
{
    await _db.ExecuteAsync(
        "INSERT INTO restaurant_days (date, is_open) VALUES (@date, FALSE) ON DUPLICATE KEY UPDATE is_open = FALSE",
        new { date = date.ToString("yyyy-MM-dd") }
    );
}

// Add explicit opening
public async Task OpenDate(DateTime date)
{
    await _db.ExecuteAsync(
        "INSERT INTO restaurant_days (date, is_open) VALUES (@date, TRUE) ON DUPLICATE KEY UPDATE is_open = TRUE",
        new { date = date.ToString("yyyy-MM-dd") }
    );
}

// Remove override (revert to default schedule)
public async Task RemoveOverride(DateTime date)
{
    await _db.ExecuteAsync(
        "DELETE FROM restaurant_days WHERE date = @date",
        new { date = date.ToString("yyyy-MM-dd") }
    );
}
```

---

## Validation Rules

### Date Format Validation

**Expected Format**: `YYYY-MM-DD` (MySQL DATE format)

**C# Validation**:
```csharp
public bool IsValidDateString(string dateString)
{
    return DateTime.TryParseExact(
        dateString,
        "yyyy-MM-dd",
        CultureInfo.InvariantCulture,
        DateTimeStyles.None,
        out _
    );
}
```

---

### Date Uniqueness

**Database Constraint**: `UNIQUE KEY date`

**Application-Level Check**:
```csharp
public async Task<bool> DateOverrideExists(DateTime date)
{
    var sql = "SELECT COUNT(*) FROM restaurant_days WHERE date = @date";
    var count = await _db.ExecuteScalarAsync<int>(sql, new { date = date.ToString("yyyy-MM-dd") });
    return count > 0;
}
```

---

### is_open Flag Validation

**Allowed Values**: `TRUE` (1) or `FALSE` (0)

**C# Validation**:
```csharp
public bool IsValidOpenFlag(bool isOpen)
{
    // Always valid in C# (bool type enforces true/false)
    return true;
}
```

---

## Error Handling

### Fetch Closed Days Errors

**Possible Errors**:
1. Database connection failure
2. Network timeout
3. Invalid JSON response
4. Missing table/columns

**Error Response**:
```json
{
  "success": false,
  "message": "Error fetching closed days: [exception message]"
}
```

**C# Error Handling**:
```csharp
try
{
    var (closedDays, openedDays) = await GetClosedAndOpenedDaysAsync();
    // Use data
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to fetch closed/opened days");

    // Fallback to default schedule only (no overrides)
    var isClosed = IsNormallyClosedDay(date);
    // ...
}
```

---

### Database Query Errors

**Possible Errors**:
1. Table doesn't exist
2. Column mismatch
3. Syntax error

**PHP Error Handling** (fetch_closed_days.php):
```php
try {
    $stmt = $conn->prepare("SELECT date FROM restaurant_days WHERE is_open = FALSE");
    $stmt->execute();
    // ...
} catch (Exception $e) {
    error_log('Error in fetch_closed_days.php: ' . $e->getMessage());
    echo json_encode([
        'success' => false,
        'message' => 'Error fetching closed days: ' . $e->getMessage()
    ]);
}
```

---

## Special Cases

### Special Holidays (Hardcoded)

**List**:
- `12-24` - Christmas Eve
- `12-25` - Christmas Day
- `12-31` - New Year's Eve
- `01-01` - New Year's Day
- `01-05` - Epiphany Eve
- `01-06` - Epiphany

**Priority**: HIGHER than opened_days/closed_days

**Logic**: These dates are checked BEFORE restaurant_days logic

**Why Hardcoded**:
- Special menus on these dates
- Cannot be modified via online booking
- Manual management required

**Important**: Special holidays override even `is_open=TRUE` in restaurant_days

**Example**:
```
Date: 2025-12-25 (Christmas)
restaurant_days: {date: '2025-12-25', is_open: TRUE}
Result: BLOCKED (special_holiday overrides opened_days)
```

---

### 35-Day Range Limit

**Business Rule**: Only accept reservations up to 35 days in advance

**Priority**: Checked AFTER closed_days but BEFORE capacity check

**Example**:
```
Today: 2025-11-27
Requested: 2026-01-15
Days Until: 49
Result: Rejected ('too_far_future')
```

---

## Testing Scenarios

### Test Case 1: Default Monday (Closed)

**Input**:
- Date: `2025-11-24` (Monday)
- restaurant_days: No entry for this date

**Expected**:
- `closed_days`: Does NOT contain `'2025-11-24'`
- `opened_days`: Does NOT contain `'2025-11-24'`
- `isDateClosed()`: TRUE (default Mon/Tue/Wed closure)
- Result: "Ese día estamos cerrados"

---

### Test Case 2: Override Monday (Open)

**Input**:
- Date: `2025-11-27` (Monday)
- restaurant_days: `{date: '2025-11-27', is_open: TRUE}`

**Expected**:
- `closed_days`: Does NOT contain `'2025-11-27'`
- `opened_days`: CONTAINS `'2025-11-27'`
- `isDateClosed()`: FALSE (override applied)
- Result: Date is available

---

### Test Case 3: Saturday Closure

**Input**:
- Date: `2025-08-15` (Saturday)
- restaurant_days: `{date: '2025-08-15', is_open: FALSE}`

**Expected**:
- `closed_days`: CONTAINS `'2025-08-15'`
- `opened_days`: Does NOT contain `'2025-08-15'`
- `isDateClosed()`: TRUE (override applied)
- Result: "Ese día estamos cerrados"

---

### Test Case 4: Default Thursday (Open)

**Input**:
- Date: `2025-11-28` (Thursday)
- restaurant_days: No entry for this date

**Expected**:
- `closed_days`: Does NOT contain `'2025-11-28'`
- `opened_days`: Does NOT contain `'2025-11-28'`
- `isDateClosed()`: FALSE (default Thu-Sun opening)
- Result: Date is available

---

### Test Case 5: Christmas Day (Special Holiday)

**Input**:
- Date: `2025-12-25` (Christmas)
- restaurant_days: `{date: '2025-12-25', is_open: TRUE}` (trying to override)

**Expected**:
- Special holiday check: TRUE (12-25 in hardcoded list)
- Result: "Ese día festivo tiene menú especial..." (BEFORE checking restaurant_days)
- Note: `opened_days` override is IGNORED for special holidays

---

## Issues Encountered

None. All files successfully analyzed and documented.

---

## Blockers

None. All requirements completed.

---

## Context for Next Step

### What We've Documented (Step 13)

**restaurant_days Table**:
- **Structure**: 3 columns (id, date, is_open)
- **Primary Key**: `id` (INT AUTO_INCREMENT)
- **Unique Constraint**: `date` (prevents duplicates)
- **Boolean Flag**: `is_open` (TRUE=open, FALSE=closed)
- **Purpose**: Override default weekly schedule

**Conceptual Separation** (not physical tables):
- **closed_days**: Virtual view of `restaurant_days WHERE is_open = FALSE`
- **opened_days**: Virtual view of `restaurant_days WHERE is_open = TRUE`

**fetch_closed_days.php**:
- **Endpoint**: `/fetch_closed_days.php`
- **Method**: GET
- **Query 1**: `SELECT date FROM restaurant_days WHERE is_open = FALSE` → closed_days array
- **Query 2**: `SELECT date FROM restaurant_days WHERE is_open = TRUE` → opened_days array
- **Response**: `{success, closed_days[], opened_days[]}`

**Override Mechanism (Priority Order)**:
1. **HIGHEST**: Explicitly opened days (`is_open=TRUE`) - overrides Mon/Tue/Wed default closures
2. **MEDIUM**: Explicitly closed days (`is_open=FALSE`) - overrides Thu/Fri/Sat/Sun default openings
3. **LOWEST**: Default weekly schedule - Mon/Tue/Wed closed, Thu/Fri/Sat/Sun open

**Default Weekly Schedule**:
- Monday (1): CLOSED
- Tuesday (2): CLOSED
- Wednesday (3): CLOSED
- Thursday (4): OPEN
- Friday (5): OPEN
- Saturday (6): OPEN
- Sunday (7): OPEN

**Integration with check_date_availability.php**:
- **Step 1**: Fetch closed_days and opened_days from fetch_closed_days.php
- **Step 2**: Check special holidays (highest priority, hardcoded)
- **Step 3**: Check if date is closed using `isDateClosed()` function (applies override logic)
- **Step 4**: Check 35-day range limit
- **Step 5**: Simple check mode (date validation only)
- **Step 6**: Full check mode (hourly availability + capacity)

**Special Holidays** (hardcoded, highest priority):
- 12-24, 12-25, 12-31, 01-01, 01-05, 01-06
- Cannot be booked/modified (special menu days)
- Override even `is_open=TRUE` entries

**Business Rules**:
- Restaurant normally closed Mon/Tue/Wed
- Can override with `is_open=TRUE` for special events
- Can close Thu/Fri/Sat/Sun with `is_open=FALSE` for vacation
- Only accept reservations up to 35 days in advance

**Caching Recommendation**:
- Cache closed_days/opened_days for 5 minutes
- Reduces database queries
- Data changes infrequently

---

### System State After This Step

**What We Now Understand**:
1. ✓ No separate closed_days/opened_days tables (single restaurant_days table)
2. ✓ is_open boolean flag determines status (TRUE=open, FALSE=closed)
3. ✓ UNIQUE constraint on date prevents duplicates
4. ✓ Override priority system (opened > closed > default)
5. ✓ Default weekly schedule (Mon/Tue/Wed closed, Thu-Sun open)
6. ✓ fetch_closed_days.php returns two arrays from single table
7. ✓ Integration with check_date_availability.php validated
8. ✓ Special holidays override all restaurant_days entries
9. ✓ 35-day booking range enforced after closed_days check
10. ✓ Simple vs full check modes differentiated

**Database Schema Progress**:
- Step 11: ✓ Bookings table (19 columns) - COMPLETED
- Step 12: ✓ FINDE table (rice types) - COMPLETED
- Step 13: ✓ restaurant_days table (3 columns) - COMPLETED
- Next: Other availability tables (daily_limits, hour_configuration, openinghours)

**API Endpoints Documented**:
- ✓ `get_available_rice_types.php` - Returns active rice types
- ✓ `fetch_closed_days.php` - Returns closed/opened date lists
- ✓ `check_date_availability.php` - Date availability validation (partial, focus on closed_days logic)
- Pending: `gethourdata.php` - Hourly availability (referenced but not detailed)

---

### Files Ready for Next Step

**Key files for upcoming steps**:
- `/home/jaime/Documents/projects/alqueriavillacarmen/villacarmen_schema.sql` - Complete database schema
- `/home/jaime/Documents/projects/alqueriavillacarmen/api/check_date_availability.php` - Full date validation API (352 lines)
- `/home/jaime/Documents/projects/alqueriavillacarmen/api/gethourdata.php` - Hourly availability API (likely Step 14+)
- Current step output: `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/steps/step-13-output.md`
- Prior step output: `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/steps/step-12-output.md`

**Tables to Document Next** (from schema):
- `daily_limits` - Daily capacity management
- `hour_configuration` - Time slot configuration per date
- `openinghours` - Available reservation hours
- Other supporting tables as needed

**APIs to Document Next**:
- `gethourdata.php` - Hourly availability with capacity checks
- Other booking-related APIs

---

### Critical Information for C# Implementation

**Service Pattern**:
```csharp
public interface IRestaurantDaysService
{
    Task<(List<string> ClosedDays, List<string> OpenedDays)> GetClosedAndOpenedDaysAsync();
    Task<bool> IsDateClosedAsync(DateTime date);
    bool IsNormallyClosedDay(DateTime date);
    Task<DateAvailabilityStatus> GetDateStatusAsync(DateTime date);
}
```

**Override Logic Implementation**:
```csharp
public async Task<bool> IsDateClosedAsync(DateTime date)
{
    var (closedDays, openedDays) = await GetClosedAndOpenedDaysAsync();
    string dateString = date.ToString("yyyy-MM-dd");

    // Priority 1: Explicitly opened
    if (openedDays.Contains(dateString))
        return false;

    // Priority 2: Explicitly closed
    if (closedDays.Contains(dateString))
        return true;

    // Priority 3: Default schedule
    return IsNormallyClosedDay(date);
}
```

**Day of Week Conversion** (.NET to ISO):
```csharp
int isoDayOfWeek = ((int)date.DayOfWeek) == 0 ? 7 : (int)date.DayOfWeek;
bool isNormallyClosed = isoDayOfWeek >= 1 && isoDayOfWeek <= 3; // Mon/Tue/Wed
```

**Caching Strategy**:
```csharp
private static List<string>? _cachedClosedDays;
private static List<string>? _cachedOpenedDays;
private static DateTime _cacheExpiry = DateTime.MinValue;

if (DateTime.Now < _cacheExpiry)
{
    return (_cachedClosedDays!, _cachedOpenedDays!);
}
```

**Special Holidays Check** (before restaurant_days):
```csharp
var specialHolidays = new[] { "12-24", "12-25", "12-31", "01-01", "01-05", "01-06" };
string monthDay = date.ToString("MM-dd");

if (specialHolidays.Contains(monthDay))
{
    return (false, "Ese día festivo tiene menú especial...");
}
```

---

## Verification

- [x] Step 13 requirements reviewed from steps file
- [x] `fetch_closed_days.php` analyzed (54 lines)
- [x] `restaurant_days` table schema documented (lines 258-264)
- [x] All 3 columns documented (id, date, is_open)
- [x] PRIMARY KEY and UNIQUE constraints documented
- [x] Conceptual closed_days/opened_days separation explained
- [x] Query logic analyzed (two SELECT queries with WHERE is_open filters)
- [x] Override priority mechanism documented (opened > closed > default)
- [x] Default weekly schedule documented (Mon/Tue/Wed closed)
- [x] Integration with check_date_availability.php analyzed
- [x] Special holidays override documented (highest priority)
- [x] 35-day range limit documented
- [x] Simple vs full check modes explained
- [x] C# model classes provided (RestaurantDay, ClosedDaysResponse)
- [x] Service interface and implementation examples provided
- [x] Caching strategy recommended (5 minutes)
- [x] Day of week conversion (.NET to ISO) documented
- [x] Testing scenarios provided (5 test cases)
- [x] Error handling patterns documented
- [x] No issues or blockers encountered
- [x] Output file created at correct path
- [x] Status marked as COMPLETED
