# Step 12 Output: Document FINDE Table Schema for Rice Types

## Status: COMPLETED

## Execution Summary
Successfully documented the FINDE table schema used for storing and retrieving rice types (paellas) in the Villa Carmen restaurant system. Analyzed the SQL query in `get_available_rice_types.php` which filters rice types by TIPO='ARROZ' and active=1. The FINDE table serves as a central menu items database storing various food types with multilingual descriptions, allergen information, and active status flags.

## Files Read
- `/home/jaime/Documents/projects/alqueriavillacarmen/api/get_available_rice_types.php` - Rice types API endpoint (61 lines)
- `/home/jaime/Documents/projects/alqueriavillacarmen/villacarmen_schema.sql` - FINDE table schema (lines 43-51)
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/whatsapp-booking-workflow-steps.md` - Step 12 requirements
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/progress.json` - Current progress
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/steps/step-11-output.md` - Prior context

## Files Created
- `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/steps/step-12-output.md` - This documentation

---

## FINDE Table Complete Schema

### Table Metadata

**Table Name**: `FINDE`

**Engine**: InnoDB

**Charset**: utf8mb4

**Collation**: utf8mb4_unicode_ci

**Auto Increment**: 367 (current value in schema)

**Location**: `/home/jaime/Documents/projects/alqueriavillacarmen/villacarmen_schema.sql` (Lines 43-51)

**Purpose**: Stores menu items for the restaurant, including rice dishes (paellas), with multilingual descriptions, type categorization, allergen information, and active/inactive status flags.

---

### Complete Column Definitions

#### Primary Key

**Column**: `NUM`
- **Type**: `INT NOT NULL AUTO_INCREMENT`
- **Constraint**: `PRIMARY KEY`
- **Description**: Unique identifier for each menu item
- **Required**: Yes (auto-generated)
- **API Usage**: Internal identifier, not typically returned in rice types API
- **Example**: `365`, `366`
- **C# Mapping**: `int Num { get; set; }`

---

#### Menu Item Description

**Column**: `DESCRIPCION`
- **Type**: `VARCHAR(900) NOT NULL`
- **Collation**: utf8mb4_unicode_ci
- **Description**: Menu item name/description (supports multiple languages and long descriptions)
- **Required**: YES
- **Default**: None
- **Max Length**: 900 characters (very large to accommodate multilingual content)
- **Validation**: Cannot be null or empty
- **Example**:
  - `"Paella Valenciana"`
  - `"Arroz Negro con Alioli"`
  - `"Arroz a Banda"`
  - `"Paella de Marisco"`
  - `"Arroz Meloso de Bogavante"`
- **C# Mapping**: `string Descripcion { get; set; }`
- **API Response**: Returned as `rice_name` in JSON response
- **Note**: Large VARCHAR allows for detailed descriptions or multilingual content

---

#### Item Type

**Column**: `TIPO`
- **Type**: `VARCHAR(100) NOT NULL`
- **Collation**: utf8mb4_unicode_ci
- **Description**: Category/type of menu item
- **Required**: YES
- **Default**: None
- **Max Length**: 100 characters
- **Known Values**:
  - `'ARROZ'` - Rice dishes (paellas)
  - (Other types may include: POSTRE, ENTRANTE, PRINCIPAL, BEBIDA, etc.)
- **Validation**: Cannot be null or empty
- **Example**: `"ARROZ"`
- **C# Mapping**: `string Tipo { get; set; }`
- **Usage**: Primary filter for querying specific menu categories
- **Index Opportunity**: This column would benefit from an index for performance

---

#### Allergen Information

**Column**: `alergenos`
- **Type**: `LONGTEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_bin`
- **Description**: JSON field storing allergen information for the menu item
- **Required**: NO
- **Default**: NULL
- **Format**: JSON (validated by CHECK constraint)
- **Max Length**: LONGTEXT type (4,294,967,295 bytes)
- **Constraint**: `FINDE_chk_1` CHECK constraint ensures `json_valid(alergenos)`
- **Example JSON Structure**:
  ```json
  {
    "gluten": false,
    "crustaceos": true,
    "huevos": false,
    "pescado": true,
    "cacahuetes": false,
    "soja": false,
    "lacteos": false,
    "frutos_secos": false,
    "apio": false,
    "mostaza": false,
    "sesamo": false,
    "sulfitos": true,
    "altramuces": false,
    "moluscos": true
  }
  ```
- **C# Mapping**: `string? Alergenos { get; set; }` or `AllergenInfo? Alergenos { get; set; }` (with JSON deserialization)
- **API Usage**: Not returned in basic rice types API but could be included for detailed menu info
- **Note**: Uses binary collation (utf8mb4_bin) for case-sensitive JSON storage

---

#### Active Status Flag

**Column**: `active`
- **Type**: `TINYINT(1) NOT NULL DEFAULT '1'`
- **Description**: Flag indicating if the menu item is currently active/available
- **Required**: NO (has default)
- **Default**: `1` (active)
- **Values**:
  - `0` - Inactive (not available, hidden from menus)
  - `1` - Active (available for ordering)
- **Example**: `1`
- **C# Mapping**: `bool Active { get; set; } = true`
- **Usage**: Critical filter in API queries to show only available items
- **Business Logic**: Items can be temporarily disabled without deletion (seasonal items, out of stock, etc.)
- **Index Opportunity**: Composite index on (TIPO, active) would optimize the main query

---

### Indexes

**Index**: `PRIMARY KEY (NUM)`
- **Type**: PRIMARY KEY
- **Column**: `NUM`
- **Purpose**: Unique identifier lookup, fast row access
- **Auto Increment**: Yes

**Recommended Indexes** (not currently in schema):
```sql
-- For performance optimization of get_available_rice_types.php query
ALTER TABLE FINDE ADD INDEX idx_tipo_active (TIPO, active);

-- For faster filtering by type
ALTER TABLE FINDE ADD INDEX idx_tipo (TIPO);

-- For searching by description
ALTER TABLE FINDE ADD INDEX idx_descripcion (DESCRIPCION(255));
```

---

### Constraints

**Primary Key Constraint**:
```sql
PRIMARY KEY (`NUM`)
```
- Ensures unique identifier for each menu item
- Auto-increments on INSERT

**JSON Validation Constraint**:
```sql
CONSTRAINT `FINDE_chk_1` CHECK (json_valid(`alergenos`))
```
- Ensures `alergenos` column contains valid JSON or NULL
- Prevents malformed JSON data
- Database-level data integrity enforcement
- **Important**: Queries with JSON must handle NULL values

---

### Table Summary

| Column | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| `NUM` | INT | YES | AUTO_INCREMENT | Primary key / unique ID |
| `DESCRIPCION` | VARCHAR(900) | YES | - | Menu item name/description |
| `TIPO` | VARCHAR(100) | YES | - | Item category (ARROZ, etc.) |
| `alergenos` | LONGTEXT (JSON) | NO | NULL | Allergen information (JSON) |
| `active` | TINYINT(1) | NO | 1 | Active status (1=active, 0=inactive) |

---

## Rice Types API Query Analysis

### API File Location
`/home/jaime/Documents/projects/alqueriavillacarmen/api/get_available_rice_types.php`

---

### SQL Query Structure

**Full Query** (Lines 19-25):
```sql
SELECT DESCRIPCION as rice_name
FROM FINDE
WHERE TIPO = 'ARROZ'
  AND active = 1
ORDER BY DESCRIPCION
```

---

### Query Breakdown

**SELECT Clause**:
```sql
SELECT DESCRIPCION as rice_name
```
- **Returns**: Only the `DESCRIPCION` column
- **Alias**: Renamed to `rice_name` for API response clarity
- **Purpose**: Simplifies JSON response structure
- **Example Output**: `["Paella Valenciana", "Arroz Negro", "Arroz a Banda"]`

**FROM Clause**:
```sql
FROM FINDE
```
- **Source Table**: FINDE (menu items table)
- **Scope**: All menu items across all categories

**WHERE Clause - Type Filter**:
```sql
WHERE TIPO = 'ARROZ'
```
- **Filter**: Only menu items categorized as rice dishes
- **Type**: Exact string match (case-sensitive due to utf8mb4_unicode_ci)
- **Excludes**: All other menu types (POSTRES, ENTRANTES, etc.)
- **Business Logic**: Isolates rice/paella menu items for booking pre-orders

**WHERE Clause - Active Filter**:
```sql
AND active = 1
```
- **Filter**: Only active/available items
- **Type**: Boolean flag check (1 = active)
- **Excludes**: Inactive items (active = 0)
- **Business Logic**:
  - Prevents customers from ordering unavailable items
  - Allows restaurant to temporarily disable items without deletion
  - Seasonal or supply-dependent items can be toggled

**ORDER BY Clause**:
```sql
ORDER BY DESCRIPCION
```
- **Sort**: Alphabetical order by item name
- **Collation**: utf8mb4_unicode_ci (case-insensitive Unicode sort)
- **Purpose**: Consistent, user-friendly ordering in API response
- **Result**: Rice types appear alphabetically in dropdown/list

---

### API Response Structure

**Success Response** (with data):
```json
{
  "success": true,
  "riceTypes": [
    "Arroz a Banda",
    "Arroz Negro con Alioli",
    "Paella de Marisco",
    "Paella Valenciana"
  ],
  "count": 4,
  "message": "Tipos de arroz disponibles obtenidos correctamente"
}
```

**Success Response** (no data):
```json
{
  "success": true,
  "riceTypes": [],
  "count": 0,
  "message": "No hay tipos de arroz activos en este momento"
}
```

**Error Response**:
```json
{
  "success": false,
  "riceTypes": [],
  "message": "Database connection not available"
}
```

---

### Response Fields

**success** (boolean):
- `true`: Query executed successfully
- `false`: Error occurred (database connection, query failure)

**riceTypes** (array of strings):
- **Type**: `string[]`
- **Content**: List of rice type names (DESCRIPCION values)
- **Order**: Alphabetical
- **Empty**: `[]` if no active rice types or error

**count** (integer):
- **Type**: `int`
- **Value**: Number of rice types returned
- **Range**: 0 to N (typically 5-15 rice varieties)

**message** (string):
- **Type**: `string`
- **Content**: Human-readable status message (Spanish)
- **Success**: `"Tipos de arroz disponibles obtenidos correctamente"`
- **Empty**: `"No hay tipos de arroz activos en este momento"`
- **Error**: Exception message

---

### Query Performance Considerations

**Current Performance**:
- **Table Scan**: No index on `TIPO` or `active` columns
- **Filter**: WHERE clause checks all rows for TIPO='ARROZ' AND active=1
- **Sort**: ORDER BY on VARCHAR(900) column without index
- **Impact**: Acceptable for small datasets (<1000 items), may slow with growth

**Optimization Recommendations**:
```sql
-- Add composite index for exact query optimization
CREATE INDEX idx_finde_tipo_active ON FINDE(TIPO, active);

-- Or separate indexes
CREATE INDEX idx_finde_tipo ON FINDE(TIPO);
CREATE INDEX idx_finde_active ON FINDE(active);

-- For search/autocomplete features
CREATE INDEX idx_finde_descripcion ON FINDE(DESCRIPCION(255));
```

**Expected Performance After Indexing**:
- Query time: <1ms (index seek instead of table scan)
- Result: 5-15 rows typically
- Sort: In-memory, minimal overhead

---

## Integration with Booking Workflow

### When Rice Types Are Requested

**Trigger Conditions**:
1. Customer booking has `party_size >= 4`
2. Conversation agent prompts for rice pre-order
3. Frontend displays rice selection UI

**API Call**:
```http
GET /api/get_available_rice_types.php
```

**Response Handling**:
```csharp
public async Task<List<string>> GetAvailableRiceTypes()
{
    var response = await httpClient.GetAsync("https://alqueriavillacarmen.com/api/get_available_rice_types.php");
    var json = await response.Content.ReadAsStringAsync();
    var data = JsonSerializer.Deserialize<RiceTypesResponse>(json);

    if (data.Success)
    {
        return data.RiceTypes;
    }

    return new List<string>(); // Return empty list on error
}
```

---

### Validation Flow

**Step 1: Customer Selects Rice Type**
```
Customer: "Quiero Paella Valenciana"
```

**Step 2: Validate Against Available Types**
```csharp
public async Task<bool> ValidateRiceType(string selectedRiceType)
{
    var availableTypes = await GetAvailableRiceTypes();

    // Case-insensitive partial match for flexibility
    return availableTypes.Any(type =>
        type.Contains(selectedRiceType, StringComparison.OrdinalIgnoreCase) ||
        selectedRiceType.Contains(type, StringComparison.OrdinalIgnoreCase)
    );
}
```

**Step 3: Store in Booking**
```csharp
if (await ValidateRiceType(riceSelection))
{
    booking.ArrozType = riceSelection; // Store in bookings.arroz_type
}
else
{
    // Re-prompt customer with available options
    var types = await GetAvailableRiceTypes();
    await SendMessage($"Lo siento, ese tipo de arroz no está disponible. Opciones: {string.Join(", ", types)}");
}
```

---

### Conversation Agent Integration

**Prompt Context for Rice Selection**:
```csharp
public async Task<string> BuildRiceSelectionPrompt()
{
    var availableRiceTypes = await GetAvailableRiceTypes();

    if (!availableRiceTypes.Any())
    {
        return "Lo sentimos, actualmente no tenemos tipos de arroz disponibles para pre-ordenar.";
    }

    var prompt = "Tipos de arroz disponibles:\n";
    for (int i = 0; i < availableRiceTypes.Count; i++)
    {
        prompt += $"{i + 1}. {availableRiceTypes[i]}\n";
    }
    prompt += "\n¿Qué tipo de arroz le gustaría ordenar?";

    return prompt;
}
```

**Example Conversation Flow**:
```
Agent: "Para grupos de 4 o más personas, ofrecemos pre-ordenar arroz. Tipos disponibles:
1. Arroz a Banda
2. Arroz Negro con Alioli
3. Paella de Marisco
4. Paella Valenciana

¿Le gustaría pre-ordenar arroz?"

Customer: "Sí, Paella Valenciana"

Agent: "Perfecto, Paella Valenciana. ¿Cuántas raciones? (Cada ración es para 2 personas)"

Customer: "2 raciones"

Agent: [Validates: 2 raciones <= 4 party_size ✓]
       [Stores: arroz_type='Paella Valenciana', arroz_servings=2]
       "Excelente, 2 raciones de Paella Valenciana confirmadas."
```

---

### Database Storage Mapping

**FINDE Table → Bookings Table**:
```
FINDE.DESCRIPCION (rice name from menu)
        ↓
bookings.arroz_type (VARCHAR(200))
```

**Data Flow**:
```
1. Query FINDE: SELECT DESCRIPCION WHERE TIPO='ARROZ' AND active=1
2. Present to customer in WhatsApp conversation
3. Customer selects: "Paella Valenciana"
4. Validate selection against FINDE results
5. Store in bookings: INSERT INTO bookings (..., arroz_type='Paella Valenciana', ...)
```

**Important Notes**:
- `bookings.arroz_type` is VARCHAR(200), sufficient for FINDE.DESCRIPCION
- FINDE.DESCRIPCION can be up to 900 chars, but rice names typically <100 chars
- No foreign key constraint between bookings.arroz_type and FINDE.DESCRIPCION
- Validation happens at application layer, not database constraints
- Rice names stored as plain text, not FINDE.NUM reference

---

### Active Flag Business Logic

**Why `active = 1` Filter Is Critical**:

1. **Seasonal Availability**:
   - Some rice dishes may only be available certain times of year
   - Can disable temporarily without deleting menu item

2. **Ingredient Availability**:
   - If key ingredients (e.g., specific seafood) unavailable, mark inactive
   - Prevents customer disappointment at booking time

3. **Menu Updates**:
   - Restaurant can test new rice types by activating/deactivating
   - Historical data preserved even for discontinued items

4. **Supply Chain Issues**:
   - Quick response to temporary supply problems
   - Toggle active=0 to remove from customer-facing menus

5. **Data Integrity**:
   - Never delete FINDE records (preserves historical booking data)
   - Inactive items still referenced in old bookings.arroz_type values

**Recommendation for C# API**:
```csharp
// Cache rice types for performance
private static List<string>? _cachedRiceTypes;
private static DateTime _cacheExpiry;

public async Task<List<string>> GetAvailableRiceTypes()
{
    // Cache for 5 minutes to reduce database queries
    if (_cachedRiceTypes != null && DateTime.Now < _cacheExpiry)
    {
        return _cachedRiceTypes;
    }

    var types = await FetchRiceTypesFromAPI();
    _cachedRiceTypes = types;
    _cacheExpiry = DateTime.Now.AddMinutes(5);

    return types;
}
```

---

## Data Type Mapping for C# Models

### FINDE Model Class

```csharp
public class FindeMenuItem
{
    /// <summary>
    /// Unique identifier for the menu item
    /// </summary>
    public int Num { get; set; }

    /// <summary>
    /// Menu item name/description (up to 900 characters)
    /// </summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>
    /// Category/type of menu item (e.g., "ARROZ", "POSTRE")
    /// </summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>
    /// JSON string containing allergen information
    /// </summary>
    public string? Alergenos { get; set; }

    /// <summary>
    /// Flag indicating if item is active/available (1=active, 0=inactive)
    /// </summary>
    public bool Active { get; set; } = true;
}
```

### Allergen Info Model (for JSON deserialization)

```csharp
public class AllergenInfo
{
    [JsonPropertyName("gluten")]
    public bool Gluten { get; set; }

    [JsonPropertyName("crustaceos")]
    public bool Crustaceos { get; set; }

    [JsonPropertyName("huevos")]
    public bool Huevos { get; set; }

    [JsonPropertyName("pescado")]
    public bool Pescado { get; set; }

    [JsonPropertyName("cacahuetes")]
    public bool Cacahuetes { get; set; }

    [JsonPropertyName("soja")]
    public bool Soja { get; set; }

    [JsonPropertyName("lacteos")]
    public bool Lacteos { get; set; }

    [JsonPropertyName("frutos_secos")]
    public bool FrutosSecos { get; set; }

    [JsonPropertyName("apio")]
    public bool Apio { get; set; }

    [JsonPropertyName("mostaza")]
    public bool Mostaza { get; set; }

    [JsonPropertyName("sesamo")]
    public bool Sesamo { get; set; }

    [JsonPropertyName("sulfitos")]
    public bool Sulfitos { get; set; }

    [JsonPropertyName("altramuces")]
    public bool Altramuces { get; set; }

    [JsonPropertyName("moluscos")]
    public bool Moluscos { get; set; }
}
```

### Rice Types API Response Model

```csharp
public class RiceTypesResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("riceTypes")]
    public List<string> RiceTypes { get; set; } = new();

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
```

### Type Conversion Notes

**MySQL → C# Type Mappings**:
- MySQL `INT` → C# `int`
- MySQL `VARCHAR(900)` → C# `string`
- MySQL `VARCHAR(100)` → C# `string`
- MySQL `LONGTEXT` (JSON) → C# `string?` or custom object with JSON deserialization
- MySQL `TINYINT(1)` → C# `bool`

**JSON Handling**:
```csharp
// Deserialize allergen JSON
public AllergenInfo? GetAllergenInfo(FindeMenuItem item)
{
    if (string.IsNullOrWhiteSpace(item.Alergenos))
        return null;

    try
    {
        return JsonSerializer.Deserialize<AllergenInfo>(item.Alergenos);
    }
    catch (JsonException)
    {
        // Handle invalid JSON gracefully
        return null;
    }
}
```

---

## Validation Rules for API Integration

### Rice Type Selection Validation

**DESCRIPCION (Rice Name)**:
- **Must not be empty**: Selected rice type must exist in available list
- **Max length**: 200 characters (bookings.arroz_type limit)
- **Validation**: Must match one of the values from `get_available_rice_types.php`
- **Case Handling**: Case-insensitive comparison recommended
- **Partial Match**: Allow fuzzy matching for user convenience
- **Example Valid**: `"Paella Valenciana"`, `"paella valenciana"`, `"Valenciana"`
- **Example Invalid**: `""`, `null`, `"Pizza"` (not a rice type)

**TIPO Filter**:
- **Fixed Value**: Must be `'ARROZ'` for rice types query
- **Case Sensitive**: Exact match required
- **Validation**: Application-level filter, not user input

**Active Flag**:
- **Fixed Value**: Must be `1` (active)
- **Purpose**: Only show available items to customers
- **Validation**: Automatic in SQL query

---

### Booking Integration Validation

**When to Request Rice Selection**:
```csharp
public bool ShouldOfferRicePreOrder(int partySize)
{
    // Rice pre-order typically offered for groups of 4+
    return partySize >= 4;
}
```

**Rice Servings Validation** (from Step 11):
```csharp
public bool ValidateRiceServings(int servings, int partySize, string riceType)
{
    // Must have rice type if servings specified
    if (servings > 0 && string.IsNullOrWhiteSpace(riceType))
        return false;

    // Servings must be positive
    if (servings <= 0)
        return false;

    // Servings cannot exceed party size
    if (servings > partySize)
        return false;

    // Business rule: typically even numbers (portions serve 2)
    // But don't enforce strictly (customer may want odd number)

    return true;
}
```

**Complete Rice Order Validation**:
```csharp
public async Task<(bool IsValid, string ErrorMessage)> ValidateRiceOrder(
    string riceType,
    int servings,
    int partySize)
{
    // Check if rice type is available
    var availableTypes = await GetAvailableRiceTypes();

    if (!availableTypes.Any(t =>
        t.Equals(riceType, StringComparison.OrdinalIgnoreCase) ||
        t.Contains(riceType, StringComparison.OrdinalIgnoreCase)))
    {
        return (false, $"Rice type '{riceType}' is not available. Options: {string.Join(", ", availableTypes)}");
    }

    // Check servings validity
    if (servings <= 0)
    {
        return (false, "Servings must be at least 1");
    }

    if (servings > partySize)
    {
        return (false, $"Cannot order {servings} servings for {partySize} people");
    }

    // Check description length for database field
    if (riceType.Length > 200)
    {
        return (false, "Rice type name too long (max 200 characters)");
    }

    return (true, string.Empty);
}
```

---

## Relationship with Other Tables

### FINDE → Bookings Relationship

**Connection Type**: Loose coupling (no foreign key)

**Data Flow**:
```
FINDE.DESCRIPCION (source of truth for available rice types)
        ↓ (API query)
WhatsApp Conversation Agent
        ↓ (customer selection)
bookings.arroz_type (stores selected rice name as text)
```

**No Foreign Key Constraint**:
- `bookings.arroz_type` stores rice name as VARCHAR, not FINDE.NUM reference
- Allows historical data preservation even if rice type removed from FINDE
- Validation happens at application layer during booking creation
- Old bookings retain rice type names even if item later deactivated

**Trade-offs**:
- **Pros**:
  - Historical data integrity (old bookings show what was actually ordered)
  - Flexibility (rice names can change without breaking old bookings)
  - Simple queries (no joins needed to display booking details)

- **Cons**:
  - No referential integrity (orphaned rice type names possible)
  - Potential inconsistencies if rice names change
  - Cannot easily track which FINDE items are most popular

**Recommendation**:
- Keep current design for simplicity
- Add validation layer in C# API to ensure only valid FINDE items are stored
- Consider analytics table if tracking rice popularity becomes important

---

### FINDE Table Extensions (Potential)

**Other Menu Categories** (based on TIPO column):
- `TIPO = 'ENTRANTE'` - Starters/Appetizers
- `TIPO = 'PRINCIPAL'` - Main dishes
- `TIPO = 'POSTRE'` - Desserts
- `TIPO = 'BEBIDA'` - Beverages
- `TIPO = 'ARROZ'` - Rice dishes (current focus)

**Possible Future Queries**:
```sql
-- Get all active starters
SELECT DESCRIPCION FROM FINDE WHERE TIPO = 'ENTRANTE' AND active = 1;

-- Get dessert menu with allergens
SELECT DESCRIPCION, alergenos FROM FINDE WHERE TIPO = 'POSTRE' AND active = 1;

-- Get full menu grouped by type
SELECT TIPO, GROUP_CONCAT(DESCRIPCION ORDER BY DESCRIPCION SEPARATOR ', ') as items
FROM FINDE
WHERE active = 1
GROUP BY TIPO;
```

---

## Issues Encountered

None. FINDE table schema and rice types API query documented successfully.

---

## Blockers

None. All requirements completed.

---

## Context for Next Step

### What We've Documented (Step 12)

**FINDE Table Structure**:
- **Table Purpose**: Menu items database (multi-category)
- **Total Columns**: 5 (NUM, DESCRIPCION, TIPO, alergenos, active)
- **Primary Key**: NUM (INT AUTO_INCREMENT)
- **Rice Filter**: TIPO='ARROZ' AND active=1
- **Rice Names**: DESCRIPCION column (VARCHAR 900)
- **Allergen Support**: JSON field with validation constraint
- **Active Management**: Soft delete pattern (active=0 instead of DELETE)

**Rice Types API** (`get_available_rice_types.php`):
- **Endpoint**: `/api/get_available_rice_types.php`
- **Method**: GET
- **Query**: `SELECT DESCRIPCION FROM FINDE WHERE TIPO='ARROZ' AND active=1 ORDER BY DESCRIPCION`
- **Response**: JSON with success flag, riceTypes array, count, and message
- **Filtering**: Only active rice dishes returned
- **Sorting**: Alphabetical by name (DESCRIPCION)
- **Error Handling**: Returns empty array on failure with success=false

**Key Columns for Rice Types**:
1. **TIPO**: Category filter (`'ARROZ'` for rice dishes)
2. **DESCRIPCION**: Rice name returned to API (e.g., "Paella Valenciana")
3. **active**: Availability flag (1=available, 0=hidden)

**Integration Points**:
- **Bookings Table**: `bookings.arroz_type` stores selected FINDE.DESCRIPCION value
- **Conversation Agent**: Fetches rice types, presents to customer, validates selection
- **Validation**: Application layer checks customer input against active FINDE items
- **No Foreign Key**: Loose coupling between FINDE and bookings tables

**Active Flag Business Logic**:
- **Purpose**: Soft delete for menu items
- **Use Cases**: Seasonal items, supply issues, menu testing
- **Benefit**: Historical booking data preserved (old rice types remain in bookings)
- **API Impact**: Only `active=1` items shown to customers

**Performance Considerations**:
- **Current**: No indexes on TIPO or active (table scan)
- **Recommended**: Add composite index `(TIPO, active)` for query optimization
- **Caching**: Consider 5-minute cache for rice types API results
- **Impact**: Minimal with small dataset, optimize if FINDE grows beyond 1000 items

**Data Type Mappings**:
- MySQL `VARCHAR(900)` → C# `string` (DESCRIPCION)
- MySQL `VARCHAR(100)` → C# `string` (TIPO)
- MySQL `LONGTEXT` (JSON) → C# `string?` or `AllergenInfo?` (alergenos)
- MySQL `TINYINT(1)` → C# `bool` (active)

**Validation Rules**:
- Rice type must exist in `get_available_rice_types.php` response
- Rice type length <= 200 chars (bookings.arroz_type limit)
- Only offer rice pre-order if party_size >= 4
- Rice servings must be <= party_size
- Case-insensitive matching for user flexibility

---

### System State After This Step

**What We Now Understand**:
1. ✓ FINDE table stores all menu items (multi-category)
2. ✓ Rice types filtered by TIPO='ARROZ' AND active=1
3. ✓ DESCRIPCION column contains rice names (up to 900 chars)
4. ✓ `get_available_rice_types.php` returns active rice list
5. ✓ API response format: {success, riceTypes, count, message}
6. ✓ No foreign key between FINDE and bookings (loose coupling)
7. ✓ Active flag enables soft delete pattern
8. ✓ Allergen info stored as validated JSON
9. ✓ Alphabetical sorting of rice types in API
10. ✓ Integration with booking workflow documented

**Database Schema Progress**:
- Step 11: ✓ Bookings table (19 columns) - COMPLETED
- Step 12: ✓ FINDE table (5 columns, rice types focus) - COMPLETED
- Next: Other supporting tables (restaurant_days, daily_limits, hour_configuration, openinghours)

**API Endpoints Documented**:
- ✓ `get_available_rice_types.php` - Returns active rice types
- Pending: `check_date_availability.php` - Date/time availability validation
- Pending: Other booking-related APIs

---

### Files Ready for Next Step

Key files for upcoming steps:
- `/home/jaime/Documents/projects/alqueriavillacarmen/villacarmen_schema.sql` - Complete database schema
- `/home/jaime/Documents/projects/alqueriavillacarmen/api/check_date_availability.php` - Date availability API (likely Step 13+)
- `/home/jaime/Documents/projects/alqueriavillacarmen/includes/conectaVILLACARMEN.php` - Database connection
- Current step output: `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/steps/step-12-output.md`
- Prior step output: `/home/jaime/Documents/projects/botGenerator/docs/whatsapp-booking-workflow/steps/step-11-output.md`

**Tables to Document Next** (from schema):
- `restaurant_days` - Open/closed dates management
- `daily_limits` - Daily capacity limits
- `hour_configuration` - Time slot configuration per date
- `openinghours` - Available reservation hours
- `closed_days` / `opened_days` - Special closures/openings
- Other FINDE categories (if needed for full menu support)

---

### Critical Information for C# Implementation

**Rice Types Service Pattern**:
```csharp
public interface IRiceTypesService
{
    Task<List<string>> GetAvailableRiceTypesAsync();
    Task<bool> ValidateRiceTypeAsync(string riceType);
    Task<(bool IsValid, string ErrorMessage)> ValidateRiceOrderAsync(
        string riceType, int servings, int partySize);
}
```

**Caching Strategy**:
```csharp
// Cache rice types to reduce API calls
private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
private static List<string>? _cachedRiceTypes;
private static DateTime _cacheExpiry;
```

**Database Query (Direct)**:
```csharp
// If implementing direct database access instead of PHP API
public async Task<List<string>> GetRiceTypesDirect()
{
    var sql = @"
        SELECT DESCRIPCION as rice_name
        FROM FINDE
        WHERE TIPO = 'ARROZ'
          AND active = 1
        ORDER BY DESCRIPCION
    ";

    return await connection.QueryAsync<string>(sql);
}
```

**Error Handling**:
```csharp
// Handle empty rice types gracefully
if (!riceTypes.Any())
{
    await SendMessage("Lo sentimos, actualmente no hay tipos de arroz disponibles.");
    // Skip rice selection in booking flow
    booking.ArrozType = null;
    booking.ArrozServings = null;
    continue; // Move to next booking step
}
```

---

## Verification

- [x] Step 12 requirements reviewed from steps file
- [x] `get_available_rice_types.php` API file read (61 lines)
- [x] SQL query analyzed (lines 19-25)
- [x] FINDE table schema documented from villacarmen_schema.sql (lines 43-51)
- [x] All 5 columns documented with complete details
- [x] TIPO column documented (filter for 'ARROZ')
- [x] DESCRIPCION column documented (rice names, VARCHAR 900)
- [x] active flag documented (1=active, 0=inactive)
- [x] alergenos JSON field documented with constraint
- [x] NUM primary key documented
- [x] SQL query breakdown provided (SELECT, WHERE, ORDER BY)
- [x] API response structure documented (success, riceTypes, count, message)
- [x] Integration with booking workflow explained
- [x] Validation rules defined for rice selection
- [x] C# model classes provided (FindeMenuItem, AllergenInfo, RiceTypesResponse)
- [x] Data type mappings documented (MySQL → C#)
- [x] Performance considerations noted (indexing recommendations)
- [x] Relationship with bookings table explained (loose coupling)
- [x] Active flag business logic documented
- [x] Conversation flow examples provided
- [x] No issues or blockers encountered
- [x] Output file created at correct path
- [x] Status marked as COMPLETED
