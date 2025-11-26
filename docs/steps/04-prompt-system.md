# Step 04: Prompt System

In this step, we'll build the prompt loader and assembler that reads system prompts from external files. This is the key feature that makes the bot easily customizable for different restaurants.

## 4.1 Why External Prompts?

### Problems with Hardcoded Prompts

```csharp
// BAD: Hardcoded in code
var systemPrompt = @"
    You are an assistant for Alquería Villa Carmen.
    Hours: Thursday-Sunday 13:30-18:00
    ...
";
```

Issues:
- Requires code changes to modify AI behavior
- Non-developers can't make changes
- Difficult to manage multiple restaurants
- No separation of concerns

### Solution: External Prompt Files

```
prompts/
├── restaurants/
│   └── villacarmen/
│       ├── system-main.txt      # Main identity
│       ├── booking-flow.txt     # Booking rules
│       └── restaurant-info.txt  # Hours, menus
└── shared/
    └── common-rules.txt         # Shared across all
```

Benefits:
- Edit prompts without recompiling
- Non-technical staff can customize AI
- Easy to add new restaurants
- Version control for prompts
- A/B testing different prompts

## 4.2 Prompt File Format

### Token Syntax

Prompts use tokens that get replaced with runtime values:

```
# Using double braces (Handlebars-style)
Hello {{pushName}}, how can I help you?

# Using dollar sign (alternative)
Hello ${pushName}, how can I help you?

# Both work the same way
```

### Example Prompt File

**prompts/restaurants/villacarmen/system-main.txt**

```
# IDENTITY

You are the virtual assistant for **{{restaurantName}}** in Valencia.
You're chatting with **{{pushName}}** via WhatsApp.

## CUSTOMER INFO
- Name: {{pushName}}
- Phone: {{senderNumber}}
- Message: "{{messageText}}"

## CURRENT DATE/TIME
- Today: {{todayES}}
- Date: {{todayFormatted}}
- Year: {{currentYear}}

## BOOKING STATE
{{#if state_fecha}}✅ Date: {{state_fecha}}{{else}}❌ Date: MISSING{{/if}}
{{#if state_hora}}✅ Time: {{state_hora}}{{else}}❌ Time: MISSING{{/if}}
{{#if state_personas}}✅ People: {{state_personas}}{{else}}❌ People: MISSING{{/if}}

## RULES
1. Never ask for data already collected (marked with ✅)
2. Be brief and natural
3. One question at a time
```

## 4.3 Create the Interface

### src/BotGenerator.Core/Services/IPromptLoaderService.cs

```csharp
namespace BotGenerator.Core.Services;

/// <summary>
/// Service for loading and assembling prompts from external files.
/// </summary>
public interface IPromptLoaderService
{
    /// <summary>
    /// Loads a prompt file for a specific restaurant.
    /// </summary>
    /// <param name="restaurantId">Restaurant identifier (folder name).</param>
    /// <param name="promptName">Prompt file name (without extension).</param>
    /// <returns>The raw prompt content.</returns>
    Task<string> LoadPromptAsync(string restaurantId, string promptName);

    /// <summary>
    /// Loads a shared prompt file.
    /// </summary>
    /// <param name="promptName">Prompt file name (without extension).</param>
    /// <returns>The raw prompt content.</returns>
    Task<string> LoadSharedPromptAsync(string promptName);

    /// <summary>
    /// Assembles the complete system prompt for a restaurant.
    /// Loads all prompt modules, replaces tokens, and combines them.
    /// </summary>
    /// <param name="restaurantId">Restaurant identifier.</param>
    /// <param name="context">Dictionary of token values to replace.</param>
    /// <returns>The fully assembled system prompt.</returns>
    Task<string> AssembleSystemPromptAsync(
        string restaurantId,
        Dictionary<string, object> context);

    /// <summary>
    /// Loads a specialized prompt (e.g., for rice validation).
    /// </summary>
    /// <param name="restaurantId">Restaurant identifier.</param>
    /// <param name="promptName">Specialized prompt name.</param>
    /// <param name="context">Token values to replace.</param>
    /// <returns>The processed prompt.</returns>
    Task<string> LoadSpecializedPromptAsync(
        string restaurantId,
        string promptName,
        Dictionary<string, object> context);

    /// <summary>
    /// Clears the prompt cache (useful when files change).
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Gets the list of available restaurants.
    /// </summary>
    IEnumerable<string> GetAvailableRestaurants();
}
```

## 4.4 Implement the Service

### src/BotGenerator.Core/Services/PromptLoaderService.cs

```csharp
using System.Collections.Concurrent;

namespace BotGenerator.Core.Services;

/// <summary>
/// Implementation of IPromptLoaderService that loads prompts from the file system.
/// </summary>
public class PromptLoaderService : IPromptLoaderService
{
    private readonly string _promptsBasePath;
    private readonly bool _cacheEnabled;
    private readonly TimeSpan _cacheDuration;
    private readonly ILogger<PromptLoaderService> _logger;

    // Cache structure: key -> (content, loadedAt)
    private readonly ConcurrentDictionary<string, (string Content, DateTime LoadedAt)> _cache = new();

    // Default prompt modules to load (in order)
    private static readonly string[] DefaultModules = new[]
    {
        "system-main",
        "restaurant-info",
        "booking-flow",
        "cancellation-flow",
        "modification-flow"
    };

    // Shared modules to include
    private static readonly string[] SharedModules = new[]
    {
        "whatsapp-history-rules",
        "date-parsing",
        "common-responses"
    };

    public PromptLoaderService(IConfiguration config, ILogger<PromptLoaderService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Get base path from configuration
        var configuredPath = config["Prompts:BasePath"] ?? "prompts";

        // If relative path, make it relative to the application base directory
        if (!Path.IsPathRooted(configuredPath))
        {
            _promptsBasePath = Path.Combine(AppContext.BaseDirectory, configuredPath);
        }
        else
        {
            _promptsBasePath = configuredPath;
        }

        _cacheEnabled = config.GetValue("Prompts:CacheEnabled", true);
        _cacheDuration = TimeSpan.FromMinutes(
            config.GetValue("Prompts:CacheDurationMinutes", 5));

        _logger.LogInformation(
            "PromptLoaderService initialized. Base path: {Path}, Cache: {CacheEnabled}",
            _promptsBasePath, _cacheEnabled);

        // Validate base path exists
        if (!Directory.Exists(_promptsBasePath))
        {
            _logger.LogWarning(
                "Prompts base path does not exist: {Path}. Creating it.",
                _promptsBasePath);
            Directory.CreateDirectory(_promptsBasePath);
        }
    }

    public async Task<string> LoadPromptAsync(string restaurantId, string promptName)
    {
        var cacheKey = $"restaurant:{restaurantId}:{promptName}";

        // Check cache
        if (_cacheEnabled && TryGetFromCache(cacheKey, out var cached))
        {
            _logger.LogDebug("Cache hit for prompt: {Key}", cacheKey);
            return cached;
        }

        // Build file path
        var filePath = GetPromptFilePath(restaurantId, promptName);

        if (filePath == null)
        {
            _logger.LogWarning(
                "Prompt file not found. Restaurant: {Restaurant}, Prompt: {Prompt}",
                restaurantId, promptName);
            return "";
        }

        // Load file
        var content = await File.ReadAllTextAsync(filePath);

        // Cache it
        if (_cacheEnabled)
        {
            _cache[cacheKey] = (content, DateTime.UtcNow);
        }

        _logger.LogDebug(
            "Loaded prompt file: {Path} ({Length} chars)",
            filePath, content.Length);

        return content;
    }

    public async Task<string> LoadSharedPromptAsync(string promptName)
    {
        var cacheKey = $"shared:{promptName}";

        // Check cache
        if (_cacheEnabled && TryGetFromCache(cacheKey, out var cached))
        {
            return cached;
        }

        // Build file path
        var filePath = GetSharedPromptFilePath(promptName);

        if (filePath == null)
        {
            _logger.LogDebug("Shared prompt not found: {Prompt}", promptName);
            return "";
        }

        // Load file
        var content = await File.ReadAllTextAsync(filePath);

        // Cache it
        if (_cacheEnabled)
        {
            _cache[cacheKey] = (content, DateTime.UtcNow);
        }

        return content;
    }

    public async Task<string> AssembleSystemPromptAsync(
        string restaurantId,
        Dictionary<string, object> context)
    {
        var sb = new StringBuilder();
        var loadedModules = new List<string>();

        // Load restaurant-specific modules
        foreach (var moduleName in DefaultModules)
        {
            var content = await LoadPromptAsync(restaurantId, moduleName);
            if (!string.IsNullOrWhiteSpace(content))
            {
                var processed = ReplaceTokens(content, context);
                sb.AppendLine(processed);
                sb.AppendLine("\n---\n");
                loadedModules.Add(moduleName);
            }
        }

        // Load shared modules
        foreach (var moduleName in SharedModules)
        {
            var content = await LoadSharedPromptAsync(moduleName);
            if (!string.IsNullOrWhiteSpace(content))
            {
                var processed = ReplaceTokens(content, context);
                sb.AppendLine(processed);
                sb.AppendLine("\n---\n");
                loadedModules.Add($"shared:{moduleName}");
            }
        }

        _logger.LogInformation(
            "Assembled system prompt for {Restaurant}. Modules: {Modules}. Total length: {Length}",
            restaurantId,
            string.Join(", ", loadedModules),
            sb.Length);

        return sb.ToString().TrimEnd();
    }

    public async Task<string> LoadSpecializedPromptAsync(
        string restaurantId,
        string promptName,
        Dictionary<string, object> context)
    {
        var content = await LoadPromptAsync(restaurantId, promptName);

        if (string.IsNullOrWhiteSpace(content))
        {
            // Try shared fallback
            content = await LoadSharedPromptAsync(promptName);
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning(
                "Specialized prompt not found: {Restaurant}/{Prompt}",
                restaurantId, promptName);
            return "";
        }

        return ReplaceTokens(content, context);
    }

    public void ClearCache()
    {
        _cache.Clear();
        _logger.LogInformation("Prompt cache cleared");
    }

    public IEnumerable<string> GetAvailableRestaurants()
    {
        var restaurantsPath = Path.Combine(_promptsBasePath, "restaurants");

        if (!Directory.Exists(restaurantsPath))
        {
            return Enumerable.Empty<string>();
        }

        return Directory.GetDirectories(restaurantsPath)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Cast<string>();
    }

    #region Private Methods

    private bool TryGetFromCache(string key, out string content)
    {
        content = "";

        if (!_cache.TryGetValue(key, out var cached))
        {
            return false;
        }

        // Check if cache is still valid
        if (DateTime.UtcNow - cached.LoadedAt > _cacheDuration)
        {
            _cache.TryRemove(key, out _);
            return false;
        }

        content = cached.Content;
        return true;
    }

    private string? GetPromptFilePath(string restaurantId, string promptName)
    {
        var basePath = Path.Combine(_promptsBasePath, "restaurants", restaurantId);

        // Try .txt first, then .md
        var txtPath = Path.Combine(basePath, $"{promptName}.txt");
        if (File.Exists(txtPath)) return txtPath;

        var mdPath = Path.Combine(basePath, $"{promptName}.md");
        if (File.Exists(mdPath)) return mdPath;

        return null;
    }

    private string? GetSharedPromptFilePath(string promptName)
    {
        var basePath = Path.Combine(_promptsBasePath, "shared");

        var txtPath = Path.Combine(basePath, $"{promptName}.txt");
        if (File.Exists(txtPath)) return txtPath;

        var mdPath = Path.Combine(basePath, $"{promptName}.md");
        if (File.Exists(mdPath)) return mdPath;

        return null;
    }

    /// <summary>
    /// Replaces tokens in the template with values from the context dictionary.
    /// Supports both {{token}} and ${token} syntax.
    /// Also handles simple conditionals: {{#if token}}...{{else}}...{{/if}}
    /// </summary>
    private string ReplaceTokens(string template, Dictionary<string, object> context)
    {
        var result = template;

        // First, handle conditionals
        result = ProcessConditionals(result, context);

        // Then replace simple tokens
        foreach (var (key, value) in context)
        {
            var stringValue = value?.ToString() ?? "";

            // Replace {{key}} syntax
            result = result.Replace($"{{{{{key}}}}}", stringValue);

            // Replace ${key} syntax
            result = result.Replace($"${{{key}}}", stringValue);
        }

        // Clean up any unreplaced tokens (set to empty)
        result = Regex.Replace(result, @"\{\{[^}]+\}\}", "");
        result = Regex.Replace(result, @"\$\{[^}]+\}", "");

        return result;
    }

    /// <summary>
    /// Processes simple if/else conditionals in the template.
    /// Syntax: {{#if variableName}}content if true{{else}}content if false{{/if}}
    /// </summary>
    private string ProcessConditionals(string template, Dictionary<string, object> context)
    {
        // Pattern: {{#if variable}}...{{else}}...{{/if}} or {{#if variable}}...{{/if}}
        var pattern = @"\{\{#if\s+(\w+)\}\}(.*?)(?:\{\{else\}\}(.*?))?\{\{/if\}\}";

        return Regex.Replace(template, pattern, match =>
        {
            var variableName = match.Groups[1].Value;
            var trueContent = match.Groups[2].Value;
            var falseContent = match.Groups[3].Success ? match.Groups[3].Value : "";

            // Check if the variable exists and is truthy
            var isTruthy = false;
            if (context.TryGetValue(variableName, out var value))
            {
                isTruthy = value switch
                {
                    null => false,
                    bool b => b,
                    string s => !string.IsNullOrWhiteSpace(s) && s != "FALTA",
                    int i => i != 0,
                    _ => true
                };
            }

            return isTruthy ? trueContent : falseContent;
        }, RegexOptions.Singleline);
    }

    #endregion
}
```

## 4.5 Register the Service

### src/BotGenerator.Api/Program.cs (partial)

```csharp
// Register prompt loader
builder.Services.AddSingleton<IPromptLoaderService, PromptLoaderService>();
```

## 4.6 Create Sample Prompt Files

### prompts/restaurants/villacarmen/system-main.txt

```
# SISTEMA DE ASISTENTE DE RESERVAS - ALQUERÍA VILLA CARMEN

## IDENTIDAD Y CONTEXTO

Eres el asistente virtual de **Alquería Villa Carmen** en Valencia.
Estás conversando con **{{pushName}}** por WhatsApp.

**INFORMACIÓN DEL CLIENTE:**
- Nombre: {{pushName}}
- Teléfono: {{senderNumber}}
- Mensaje actual: "{{messageText}}"

**FECHA Y HORA ACTUAL:**
- HOY ES: {{todayES}}
- FECHA: {{todayFormatted}}
- AÑO: {{currentYear}}

## ESTADO ACTUAL DE LA RESERVA

**DATOS YA RECOPILADOS DE LA CONVERSACIÓN:**
{{#if state_fecha}}✅ Fecha: {{state_fecha}}{{else}}❌ Fecha: FALTA{{/if}}
{{#if state_hora}}✅ Hora: {{state_hora}}{{else}}❌ Hora: FALTA{{/if}}
{{#if state_personas}}✅ Personas: {{state_personas}}{{else}}❌ Personas: FALTA{{/if}}
{{#if state_arroz}}✅ Arroz: {{state_arroz}}{{else}}❌ Arroz: FALTA PREGUNTAR{{/if}}

## REGLAS CRÍTICAS

1. **NUNCA preguntes por datos que ya tienen ✅**
2. **SOLO pregunta por datos que tienen ❌ FALTA**
3. **Sé BREVE y NATURAL** - Como un humano real
4. **Una pregunta a la vez** - No hagas listas
5. **Usa negrita (*texto*) solo para info importante**

## HISTORIAL DE CONVERSACIÓN

{{formattedHistory}}
```

### prompts/restaurants/villacarmen/restaurant-info.txt

```
## INFORMACIÓN DEL RESTAURANTE

**HORARIOS:**
- Jueves: 13:30 – 17:00
- Viernes: 13:30 – 17:30
- Sábado: 13:30 – 18:00
- Domingo: 13:30 – 18:00
- Cerrado: Lunes, Martes, Miércoles

**MENÚS:**
- Fin de semana: https://alqueriavillacarmen.com/menufindesemana.php
- Navidad: https://alqueriavillacarmen.com/menuNavidad.php

**CONTACTO:**
- Teléfono: +34 638 857 294
- Web: https://alqueriavillacarmen.com

**UBICACIÓN:**
Alquería Villa Carmen, Valencia

**PRÓXIMOS FINES DE SEMANA:**
- {{nextSaturday}} (sábado)
- {{nextSunday}} (domingo)
```

### prompts/restaurants/villacarmen/booking-flow.txt

```
## PROCESO DE RESERVAS

### DATOS NECESARIOS:
1. Fecha (interpreta "el sábado" como {{nextSaturday}})
2. Hora
3. Número de personas
4. **Arroz** (OBLIGATORIO preguntar)

### FLUJO:

**PASO 1: Datos básicos**
- Recopila fecha, hora y personas de forma natural
- Una pregunta a la vez

**PASO 2: Pregunta de arroz (OBLIGATORIO)**
Cuando tengas fecha, hora y personas, pregunta:
"¿Queréis arroz?"

**CASO A: NO quieren arroz**
- Marca: arroz_type = null ✓
- Procede a confirmación

**CASO B: SÍ quieren arroz**
- Espera validación del sistema
- Pregunta: "¿Cuántas raciones?"
- Procede a confirmación

**PASO 3: Confirmación**
- Resume todos los datos
- Pregunta: "¿Confirmo la reserva?"
- Si confirma, genera el comando

### FORMATO DEL COMANDO:

Cuando el usuario confirme, genera:
BOOKING_REQUEST|nombre|teléfono|dd/mm/yyyy|personas|HH:MM

Ejemplo:
BOOKING_REQUEST|Juan García|34612345678|30/11/2025|4|14:00

### EJEMPLOS DE PREGUNTAS NATURALES:
- ✅ "¿Para cuántas personas?"
- ✅ "¿A qué hora os viene bien?"
- ✅ "¿Queréis arroz?"
- ❌ "¿Para cuántas personas y a qué hora?" (dos preguntas)
- ❌ "¿Qué sábado de 2025?" (antinatural)
```

### prompts/shared/whatsapp-history-rules.txt

```
## REGLAS DE USO DEL HISTORIAL

**IMPORTANTE:**
1. ✅ Tienes acceso al historial COMPLETO de WhatsApp
2. ✅ NUNCA pidas información que ya dieron antes
3. ✅ Si cambian de tema, reconoce ambos naturalmente
4. ✅ Referencia el historial de forma fluida

**EJEMPLOS CORRECTOS:**
- "Antes dijiste 4 personas, ¿mantenemos eso?"
- "Vi que preguntaste por el menú, ¿necesitas algo más?"

**EJEMPLOS INCORRECTOS:**
- ❌ "¿Para cuántas personas?" (cuando ya lo dijeron)
- ❌ Ignorar conversaciones anteriores
```

## 4.7 Usage Example

```csharp
public class SomeService
{
    private readonly IPromptLoaderService _promptLoader;
    private readonly IContextBuilderService _contextBuilder;

    public async Task<string> GetSystemPromptAsync(
        WhatsAppMessage message,
        ConversationState state)
    {
        // Build context with all dynamic values
        var context = _contextBuilder.BuildContext(message, state, null);

        // Assemble the complete system prompt
        var systemPrompt = await _promptLoader.AssembleSystemPromptAsync(
            "villacarmen",
            context);

        return systemPrompt;
    }

    public async Task<string> GetRiceValidationPromptAsync(
        string userRiceRequest,
        List<string> availableTypes)
    {
        var context = new Dictionary<string, object>
        {
            ["userRiceRequest"] = userRiceRequest,
            ["availableRiceTypes"] = string.Join(", ", availableTypes)
        };

        return await _promptLoader.LoadSpecializedPromptAsync(
            "villacarmen",
            "rice-validation",
            context);
    }
}
```

## 4.8 Testing

### tests/BotGenerator.Core.Tests/Services/PromptLoaderServiceTests.cs

```csharp
using BotGenerator.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace BotGenerator.Core.Tests.Services;

public class PromptLoaderServiceTests : IDisposable
{
    private readonly string _testPromptsPath;
    private readonly PromptLoaderService _service;

    public PromptLoaderServiceTests()
    {
        // Create temp directory for test prompts
        _testPromptsPath = Path.Combine(Path.GetTempPath(), "test-prompts-" + Guid.NewGuid());
        Directory.CreateDirectory(_testPromptsPath);
        Directory.CreateDirectory(Path.Combine(_testPromptsPath, "restaurants", "test-restaurant"));
        Directory.CreateDirectory(Path.Combine(_testPromptsPath, "shared"));

        // Create test prompt files
        File.WriteAllText(
            Path.Combine(_testPromptsPath, "restaurants", "test-restaurant", "system-main.txt"),
            "Hello {{pushName}}! Today is {{todayFormatted}}.");

        File.WriteAllText(
            Path.Combine(_testPromptsPath, "shared", "common-rules.txt"),
            "Be helpful and friendly.");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Prompts:BasePath", _testPromptsPath },
                { "Prompts:CacheEnabled", "true" }
            })
            .Build();

        _service = new PromptLoaderService(config, Mock.Of<ILogger<PromptLoaderService>>());
    }

    public void Dispose()
    {
        Directory.Delete(_testPromptsPath, recursive: true);
    }

    [Fact]
    public async Task LoadPromptAsync_ReturnsContent_WhenFileExists()
    {
        var content = await _service.LoadPromptAsync("test-restaurant", "system-main");
        Assert.Contains("Hello {{pushName}}", content);
    }

    [Fact]
    public async Task LoadPromptAsync_ReturnsEmpty_WhenFileNotFound()
    {
        var content = await _service.LoadPromptAsync("test-restaurant", "nonexistent");
        Assert.Empty(content);
    }

    [Fact]
    public async Task AssembleSystemPromptAsync_ReplacesTokens()
    {
        // Add minimal required file
        var context = new Dictionary<string, object>
        {
            ["pushName"] = "Juan",
            ["todayFormatted"] = "25/11/2025"
        };

        var prompt = await _service.LoadSpecializedPromptAsync(
            "test-restaurant",
            "system-main",
            context);

        Assert.Contains("Hello Juan", prompt);
        Assert.Contains("Today is 25/11/2025", prompt);
        Assert.DoesNotContain("{{pushName}}", prompt);
    }

    [Fact]
    public async Task LoadSharedPromptAsync_LoadsFromSharedFolder()
    {
        var content = await _service.LoadSharedPromptAsync("common-rules");
        Assert.Contains("Be helpful", content);
    }

    [Fact]
    public void GetAvailableRestaurants_ReturnsRestaurantFolders()
    {
        var restaurants = _service.GetAvailableRestaurants().ToList();
        Assert.Contains("test-restaurant", restaurants);
    }
}
```

## Summary

In this step, we:

1. Designed the prompt file structure and token syntax
2. Created `IPromptLoaderService` interface
3. Implemented `PromptLoaderService` with:
   - File loading with caching
   - Token replacement ({{token}} and ${token})
   - Simple conditional processing ({{#if}})
   - Module assembly
4. Created sample prompt files
5. Added unit tests

Key features:
- **External files**: Prompts live in text files
- **Token replacement**: Dynamic values injected at runtime
- **Caching**: Improved performance with configurable cache
- **Modularity**: Separate files for different concerns
- **Flexibility**: Support both .txt and .md files

## Next Step

Continue to [Step 05: Context Builder](./05-context-builder.md) where we'll create the service that builds the dynamic context dictionary.
