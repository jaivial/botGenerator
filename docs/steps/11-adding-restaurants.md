# Step 11: Adding New Restaurants

This guide explains how to add a new restaurant to the bot system. The architecture is designed to make this process simple - you just need to create a new prompt folder and configure the mapping.

## 11.1 Overview

Adding a new restaurant involves:
1. Creating a new folder in `prompts/restaurants/`
2. Copying and customizing the prompt files
3. Adding the restaurant configuration
4. (Optional) Creating specialized prompts

```
prompts/restaurants/
├── villacarmen/          # Existing
│   ├── system-main.txt
│   ├── restaurant-info.txt
│   └── ...
│
└── new-restaurant/       # NEW
    ├── system-main.txt
    ├── restaurant-info.txt
    └── ...
```

## 11.2 Step-by-Step Guide

### Step 1: Create the Restaurant Folder

```bash
# Create the new restaurant folder
mkdir -p prompts/restaurants/new-restaurant
```

### Step 2: Copy Template Files

```bash
# Copy from an existing restaurant
cp prompts/restaurants/villacarmen/*.txt prompts/restaurants/new-restaurant/
```

### Step 3: Customize system-main.txt

Edit the main system prompt:

```
# SISTEMA DE ASISTENTE DE RESERVAS - [NEW RESTAURANT NAME]

## IDENTIDAD

Eres el asistente virtual de **[Restaurant Name]**, un [type of restaurant] en [location].

Estás conversando con **{{pushName}}** por WhatsApp.

## INFORMACIÓN DEL CLIENTE
- Nombre: {{pushName}}
- Teléfono: {{senderNumber}}
- Mensaje actual: "{{messageText}}"

[... rest of the prompt ...]
```

### Step 4: Customize restaurant-info.txt

Update with the new restaurant's information:

```
## INFORMACIÓN DEL RESTAURANTE

**NOMBRE:** [Restaurant Name]
**UBICACIÓN:** [City, Country]
**ESPECIALIDAD:** [Type of cuisine]

### HORARIOS
| Día | Horario |
|-----|---------|
| Lunes | [XX:XX – XX:XX] |
| Martes | [XX:XX – XX:XX] |
| ... |

### CONTACTO
- **Teléfono:** [Phone Number]
- **Web:** [Website URL]

### MENÚS
- **Carta:** [Menu URL]
- **Menú del día:** [Daily menu URL if applicable]
```

### Step 5: Customize booking-flow.txt

Adapt the booking flow for the restaurant's needs:

- Different required data?
- Special questions (e.g., dietary restrictions)?
- Different confirmation format?

```
## PROCESO DE RESERVAS

### DATOS NECESARIOS
Para completar una reserva necesitas:
1. **Fecha**
2. **Hora**
3. **Número de personas**
4. [ADD/REMOVE items specific to this restaurant]

[... customize the rest ...]
```

### Step 6: Add Configuration

Add the restaurant mapping in `appsettings.json`:

```json
{
  "Restaurants": {
    "Default": "villacarmen",
    "Mapping": {
      "34638857294": "villacarmen",
      "34612345678": "new-restaurant"
    }
  }
}
```

Or in a database (production):

```sql
INSERT INTO restaurants (id, name, whatsapp_number, contact_phone, website)
VALUES ('new-restaurant', 'New Restaurant Name', '34612345678', '+34612345678', 'https://newrestaurant.com');
```

### Step 7: Update Restaurant Config Service

If using database configuration:

```csharp
public class RestaurantConfigService
{
    public async Task<RestaurantConfig?> GetByPhoneAsync(string whatsappNumber)
    {
        // Look up in database
        return await _dbContext.Restaurants
            .FirstOrDefaultAsync(r => r.WhatsAppNumber == whatsappNumber);
    }
}
```

### Step 8: Test

```bash
# Test with curl
curl -X POST http://localhost:5000/api/webhook/whatsapp-webhook \
  -H "Content-Type: application/json" \
  -d '{
    "message": {
      "chatid": "34699999999@s.whatsapp.net",
      "text": "Hola",
      "fromMe": false
    },
    "chat": {
      "name": "Test User"
    },
    "instanceName": "new-restaurant-instance"
  }'
```

## 11.3 Customization Checklist

Use this checklist when adding a new restaurant:

### Required Files

- [ ] `system-main.txt` - Updated identity and name
- [ ] `restaurant-info.txt` - Hours, contact, location
- [ ] `booking-flow.txt` - Booking process (customize if different)

### Usually Copy Without Changes

- [ ] `cancellation-flow.txt` - Usually same process
- [ ] `modification-flow.txt` - Usually same process

### May Need Customization

- [ ] `rice-validation.txt` - Remove if not needed, or replace with appropriate validation (e.g., wine-validation.txt)

### Configuration

- [ ] Add phone number mapping in appsettings.json
- [ ] Add to database if using dynamic config
- [ ] Set up WhatsApp webhook for the new number

## 11.4 Example: Adding "La Tasca de María"

### 1. Create folder

```bash
mkdir -p prompts/restaurants/tasca-maria
```

### 2. Create system-main.txt

```
# SISTEMA DE ASISTENTE DE RESERVAS - LA TASCA DE MARÍA

## IDENTIDAD

Eres el asistente virtual de **La Tasca de María**, un restaurante de tapas tradicionales en Madrid.

Estás conversando con **{{pushName}}** por WhatsApp.

## INFORMACIÓN DEL CLIENTE
- Nombre: {{pushName}}
- Teléfono: {{senderNumber}}
- Mensaje actual: "{{messageText}}"

## FECHA Y HORA ACTUAL
- HOY ES: {{todayES}}
- FECHA: {{todayFormatted}}

## ESTADO DE LA RESERVA

{{#if state_fecha}}✅ Fecha: {{state_fecha}}{{else}}❌ Fecha: FALTA{{/if}}
{{#if state_hora}}✅ Hora: {{state_hora}}{{else}}❌ Hora: FALTA{{/if}}
{{#if state_personas}}✅ Personas: {{state_personas}}{{else}}❌ Personas: FALTA{{/if}}

## REGLAS

1. NUNCA preguntes por datos que ya tienen ✅
2. Sé breve y natural
3. Usa un tono cercano y castizo (es una tasca madrileña)
4. Una pregunta a la vez

## ESTILO

- Usa expresiones como "¡Genial!", "¡Estupendo!", "¿Qué me dices?"
- Tono informal pero profesional
- Emojis con moderación
```

### 3. Create restaurant-info.txt

```
## INFORMACIÓN DEL RESTAURANTE

**NOMBRE:** La Tasca de María
**UBICACIÓN:** Calle Mayor 42, Madrid
**ESPECIALIDAD:** Tapas tradicionales madrileñas

### HORARIOS
| Día | Horario |
|-----|---------|
| Martes-Jueves | 12:00 – 16:00, 20:00 – 00:00 |
| Viernes-Sábado | 12:00 – 16:00, 20:00 – 01:00 |
| Domingo | 12:00 – 17:00 |
| Lunes | **CERRADO** |

### CONTACTO
- **Teléfono:** +34 915 555 555
- **Web:** https://latascademaria.es

### ESPECIALIDADES
- Tortilla de patatas
- Croquetas caseras
- Gambas al ajillo
- Bocadillo de calamares
```

### 4. Create booking-flow.txt

```
## PROCESO DE RESERVAS

### DATOS NECESARIOS
1. Fecha
2. Hora (almuerzo o cena)
3. Número de personas

### FLUJO

**Paso 1:** Recoger fecha
"¿Qué día queréis venir?"

**Paso 2:** Recoger hora
"¿Para comer o para cenar?"

**Paso 3:** Recoger personas
"¿Cuántos seréis?"

**Paso 4:** Confirmar
"Perfecto! Mesa para [X] el [fecha] para [comida/cena]. ¿Os lo confirmo?"

### COMANDO
BOOKING_REQUEST|{{pushName}}|{{senderNumber}}|dd/mm/yyyy|personas|HH:MM

### NOTAS
- No tenemos sistema de arroz (eliminado)
- Máximo 8 personas por reserva
- Para grupos grandes, llamar directamente
```

### 5. Add configuration

```json
{
  "Restaurants": {
    "Mapping": {
      "34915555555": "tasca-maria"
    }
  }
}
```

## 11.5 Advanced: Custom Specialized Agents

If the new restaurant has special needs, create custom agents:

### Example: Wine Validation Agent

```csharp
public class WineValidatorAgent : IAgent
{
    // Similar to RiceValidatorAgent but for wines
}
```

With its own prompt:

### prompts/restaurants/tasca-maria/wine-validation.txt

```
# SISTEMA DE VALIDACIÓN DE VINOS

Tu tarea es validar si el vino solicitado está en nuestra carta.

## VINOS DISPONIBLES
{{availableWines}}

## VINO SOLICITADO
{{userWineRequest}}

## FORMATO DE RESPUESTA

Si existe: `WINE_VALID|[nombre]`
Si no existe: `WINE_NOT_FOUND|[nombre solicitado]`
```

## 11.6 Production Considerations

### Database-Driven Configuration

For multiple restaurants, consider storing config in a database:

```sql
CREATE TABLE restaurants (
    id VARCHAR(50) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    whatsapp_number VARCHAR(20) UNIQUE NOT NULL,
    contact_phone VARCHAR(20),
    website VARCHAR(255),
    prompts_path VARCHAR(255),
    schedule JSONB,
    settings JSONB,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);
```

### Multi-Tenant Considerations

- Separate WhatsApp instances per restaurant
- Isolated conversation history
- Restaurant-specific booking APIs
- Separate analytics and reporting

### Scaling

- Cache prompts per restaurant
- Load balancing across webhook endpoints
- Rate limiting per restaurant

## Summary

Adding a new restaurant requires:

1. **Create prompt folder** with customized files
2. **Update configuration** with phone mapping
3. **Test** the integration

The modular architecture makes this process straightforward:
- Copy existing prompts as templates
- Customize restaurant-specific information
- The code handles everything else automatically

## Complete!

You now have a complete guide for building a WhatsApp restaurant booking bot using:
- C# / .NET 8
- Google Gemini 2.5 Flash
- External prompt files for easy customization
- Multi-restaurant support

For questions or issues, refer to the individual step guides or the main overview.
