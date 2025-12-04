using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
                var processed = ReplaceTokens(content, context, restaurantId);
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
                var processed = ReplaceTokens(content, context, restaurantId);
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

        return ReplaceTokens(content, context, restaurantId);
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
    /// Also handles:
    /// - Conditionals: {{#if token}}...{{else}}...{{/if}}
    /// - Imports: {{@import component-name}} or {{@import path/to/component}}
    /// - Imports with params: {{@import component with key="value"}}
    /// </summary>
    private string ReplaceTokens(string template, Dictionary<string, object> context, string? restaurantId = null, HashSet<string>? processedImports = null)
    {
        var result = template;
        processedImports ??= new HashSet<string>();

        // First, process imports (before other processing to allow imported content to use tokens)
        result = ProcessImports(result, context, restaurantId, processedImports);

        // Then, handle conditionals
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

        // Clean up any unreplaced tokens (set to empty) - but preserve @import failures for debugging
        result = Regex.Replace(result, @"\{\{(?!@import)[^}]+\}\}", "");
        result = Regex.Replace(result, @"\$\{[^}]+\}", "");

        return result;
    }

    /// <summary>
    /// Processes {{@import component-name}} directives.
    /// Supports:
    /// - {{@import component-name}} - imports from components folder
    /// - {{@import shared/component-name}} - imports from shared folder
    /// - {{@import validation/strict-matching}} - imports from components/validation/
    /// - {{@import component with key="value"}} - imports with additional context
    /// </summary>
    private string ProcessImports(string template, Dictionary<string, object> context, string? restaurantId, HashSet<string> processedImports)
    {
        // Pattern: {{@import path/to/component}} or {{@import component with key="value" key2="value2"}}
        var importPattern = @"\{\{@import\s+([^\s}]+)(?:\s+with\s+(.+?))?\}\}";

        return Regex.Replace(template, importPattern, match =>
        {
            var componentPath = match.Groups[1].Value.Trim();
            var paramsString = match.Groups[2].Success ? match.Groups[2].Value : "";

            // Prevent circular imports
            if (processedImports.Contains(componentPath))
            {
                _logger.LogWarning("Circular import detected: {Component}", componentPath);
                return $"<!-- CIRCULAR IMPORT: {componentPath} -->";
            }
            processedImports.Add(componentPath);

            // Parse additional parameters
            var importContext = new Dictionary<string, object>(context);
            if (!string.IsNullOrWhiteSpace(paramsString))
            {
                var paramPattern = @"(\w+)\s*=\s*""([^""]*)""";
                foreach (Match paramMatch in Regex.Matches(paramsString, paramPattern))
                {
                    importContext[paramMatch.Groups[1].Value] = paramMatch.Groups[2].Value;
                }
            }

            // Try to load the component
            var content = LoadComponentSync(componentPath, restaurantId);

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Import not found: {Component}", componentPath);
                return $"<!-- IMPORT NOT FOUND: {componentPath} -->";
            }

            // Recursively process the imported content
            return ReplaceTokens(content, importContext, restaurantId, processedImports);
        });
    }

    /// <summary>
    /// Synchronously loads a component from various locations.
    /// Resolution order:
    /// 1. Restaurant-specific: prompts/restaurants/{restaurantId}/{path}.txt
    /// 2. Shared: prompts/shared/{path}.txt
    /// 3. Components: prompts/components/{path}.txt
    /// </summary>
    private string LoadComponentSync(string componentPath, string? restaurantId)
    {
        var extensions = new[] { ".txt", ".md", "" };
        var searchPaths = new List<string>();

        // Build search paths in priority order
        if (!string.IsNullOrEmpty(restaurantId))
        {
            foreach (var ext in extensions)
            {
                searchPaths.Add(Path.Combine(_promptsBasePath, "restaurants", restaurantId, $"{componentPath}{ext}"));
            }
        }

        // Shared folder
        foreach (var ext in extensions)
        {
            searchPaths.Add(Path.Combine(_promptsBasePath, "shared", $"{componentPath}{ext}"));
        }

        // Components folder
        foreach (var ext in extensions)
        {
            searchPaths.Add(Path.Combine(_promptsBasePath, "components", $"{componentPath}{ext}"));
        }

        // Try each path
        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                _logger.LogDebug("Loading component from: {Path}", path);
                return File.ReadAllText(path);
            }
        }

        return "";
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
