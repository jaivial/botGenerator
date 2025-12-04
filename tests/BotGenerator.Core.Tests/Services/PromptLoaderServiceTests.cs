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

    /// <summary>
    /// Step 67: PromptLoader_LoadsRestaurantPrompt
    /// Restaurant-specific prompts should be loaded from the restaurant folder.
    /// </summary>
    [Fact]
    public async Task PromptLoader_LoadsRestaurantPrompt()
    {
        // Arrange
        var restaurantId = "test-restaurant";
        var promptName = "system-main";

        // Act
        var content = await _service.LoadPromptAsync(restaurantId, promptName);

        // Assert
        Assert.NotNull(content);
        Assert.NotEmpty(content);
        Assert.Contains("Hello {{pushName}}", content);
        Assert.Contains("Today is {{todayFormatted}}", content);

        // Verify it loads from the correct path
        // (The test setup creates the file, so if it loads, the path is correct)
    }

    /// <summary>
    /// Step 68: PromptLoader_LoadsSharedPrompt
    /// Shared prompts should be loaded from the shared folder.
    /// </summary>
    [Fact]
    public async Task PromptLoader_LoadsSharedPrompt()
    {
        // Arrange
        var sharedPromptName = "common-rules";

        // Act
        var content = await _service.LoadSharedPromptAsync(sharedPromptName);

        // Assert
        Assert.NotNull(content);
        Assert.NotEmpty(content);
        Assert.Contains("Be helpful and friendly", content);

        // Test loading a non-existent shared prompt
        var nonExistent = await _service.LoadSharedPromptAsync("non-existent-shared-prompt");
        Assert.Empty(nonExistent);
    }

    /// <summary>
    /// Step 69: PromptLoader_AssemblesSystemPrompt
    /// AssembleSystemPrompt should combine restaurant and shared modules correctly.
    /// </summary>
    [Fact]
    public async Task PromptLoader_AssemblesSystemPrompt()
    {
        // Arrange
        var restaurantId = "test-restaurant";

        // Create additional test files for assembly
        File.WriteAllText(
            Path.Combine(_testPromptsPath, "restaurants", "test-restaurant", "restaurant-info.txt"),
            "We are located in Valencia. Phone: {{restaurantPhone}}");

        File.WriteAllText(
            Path.Combine(_testPromptsPath, "shared", "whatsapp-history-rules.txt"),
            "Always respond in Spanish. Be concise.");

        var context = new Dictionary<string, object>
        {
            ["pushName"] = "Carlos",
            ["todayFormatted"] = "26/11/2025",
            ["restaurantPhone"] = "+34 123 456 789"
        };

        // Act
        var assembledPrompt = await _service.AssembleSystemPromptAsync(restaurantId, context);

        // Assert
        Assert.NotNull(assembledPrompt);
        Assert.NotEmpty(assembledPrompt);

        // Should include content from restaurant-specific modules
        Assert.Contains("Hello Carlos", assembledPrompt); // from system-main
        Assert.Contains("Today is 26/11/2025", assembledPrompt); // from system-main
        Assert.Contains("We are located in Valencia", assembledPrompt); // from restaurant-info
        Assert.Contains("Phone: +34 123 456 789", assembledPrompt); // from restaurant-info with token replaced

        // Should include content from shared modules
        Assert.Contains("Always respond in Spanish", assembledPrompt); // from shared/whatsapp-history-rules

        // Should have separators between modules
        Assert.Contains("---", assembledPrompt);

        // Should not contain unreplaced tokens
        Assert.DoesNotContain("{{pushName}}", assembledPrompt);
        Assert.DoesNotContain("{{todayFormatted}}", assembledPrompt);
        Assert.DoesNotContain("{{restaurantPhone}}", assembledPrompt);
    }

    /// <summary>
    /// Step 70: PromptLoader_ReplacesTokens_BracketSyntax
    /// Token replacement should work with {{token}} syntax and handle missing tokens gracefully.
    /// </summary>
    [Fact]
    public async Task PromptLoader_ReplacesTokens_BracketSyntax()
    {
        // Arrange
        // Create a prompt file with multiple token types
        File.WriteAllText(
            Path.Combine(_testPromptsPath, "restaurants", "test-restaurant", "token-test.txt"),
            @"Welcome {{pushName}}!
Today is {{todayFormatted}}.
Your phone: {{senderNumber}}.
Year: {{currentYear}}.
Missing token: {{missingToken}}.
Another missing: {{anotherMissing}}.");

        var context = new Dictionary<string, object>
        {
            ["pushName"] = "María García",
            ["todayFormatted"] = "26/11/2025",
            ["senderNumber"] = "+34987654321",
            ["currentYear"] = 2025
        };

        // Act
        var result = await _service.LoadSpecializedPromptAsync(
            "test-restaurant",
            "token-test",
            context);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // Verify all provided tokens are replaced
        Assert.Contains("Welcome María García!", result);
        Assert.Contains("Today is 26/11/2025.", result);
        Assert.Contains("Your phone: +34987654321.", result);
        Assert.Contains("Year: 2025.", result);

        // Verify original tokens are removed
        Assert.DoesNotContain("{{pushName}}", result);
        Assert.DoesNotContain("{{todayFormatted}}", result);
        Assert.DoesNotContain("{{senderNumber}}", result);
        Assert.DoesNotContain("{{currentYear}}", result);

        // Verify missing tokens are cleaned up (replaced with empty string)
        Assert.DoesNotContain("{{missingToken}}", result);
        Assert.DoesNotContain("{{anotherMissing}}", result);
        Assert.Contains("Missing token: .", result); // Token removed, punctuation remains
        Assert.Contains("Another missing: .", result);

        // Test with numeric and boolean values
        var contextWithTypes = new Dictionary<string, object>
        {
            ["userName"] = "Pedro",
            ["messageCount"] = 42,
            ["isVIP"] = true,
            ["discount"] = 15.5
        };

        File.WriteAllText(
            Path.Combine(_testPromptsPath, "restaurants", "test-restaurant", "types-test.txt"),
            "User: {{userName}}, Messages: {{messageCount}}, VIP: {{isVIP}}, Discount: {{discount}}%");

        var typesResult = await _service.LoadSpecializedPromptAsync(
            "test-restaurant",
            "types-test",
            contextWithTypes);

        Assert.Contains("User: Pedro", typesResult);
        Assert.Contains("Messages: 42", typesResult);
        Assert.Contains("VIP: True", typesResult);
        Assert.Contains("Discount: 15.5%", typesResult);
    }

    /// <summary>
    /// Step 71: PromptLoader_ProcessesConditionals_IfTrue
    /// Conditional syntax {{#if var}}...{{/if}} should display content when var is true.
    /// </summary>
    [Fact]
    public async Task PromptLoader_ProcessesConditionals_IfTrue()
    {
        // Arrange
        File.WriteAllText(
            Path.Combine(_testPromptsPath, "restaurants", "test-restaurant", "conditional-test.txt"),
            @"Welcome {{userName}}!
{{#if hasRice}}We have rice dishes available today.{{/if}}
{{#if hasVeganOptions}}Vegan menu is ready.{{/if}}
{{#if isVIP}}VIP discount applied.{{/if}}");

        var context = new Dictionary<string, object>
        {
            ["userName"] = "Ana",
            ["hasRice"] = true,
            ["hasVeganOptions"] = "yes", // truthy string
            ["isVIP"] = false
        };

        // Act
        var result = await _service.LoadSpecializedPromptAsync(
            "test-restaurant",
            "conditional-test",
            context);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Welcome Ana!", result);

        // Should include content when condition is true
        Assert.Contains("We have rice dishes available today.", result);
        Assert.Contains("Vegan menu is ready.", result);

        // Should not include content when condition is false
        Assert.DoesNotContain("VIP discount applied", result);

        // Should not contain the conditional syntax
        Assert.DoesNotContain("{{#if", result);
        Assert.DoesNotContain("{{/if}}", result);
    }

    /// <summary>
    /// Step 72: PromptLoader_ProcessesConditionals_IfFalse
    /// Conditional syntax {{#if var}}...{{else}}...{{/if}} should display else block when var is false.
    /// </summary>
    [Fact]
    public async Task PromptLoader_ProcessesConditionals_IfFalse()
    {
        // Arrange
        File.WriteAllText(
            Path.Combine(_testPromptsPath, "restaurants", "test-restaurant", "conditional-else-test.txt"),
            @"Status check:
{{#if hasTables}}Tables available for booking.{{else}}No tables available right now.{{/if}}
{{#if hasSpecials}}Today's special: Paella{{else}}Regular menu only.{{/if}}
{{#if isHoliday}}Holiday hours apply.{{else}}Normal business hours.{{/if}}");

        var contextWithFalseValues = new Dictionary<string, object>
        {
            ["hasTables"] = false,
            ["hasSpecials"] = "", // empty string is falsy
            ["isHoliday"] = "FALTA" // special falsy value
        };

        // Act
        var result = await _service.LoadSpecializedPromptAsync(
            "test-restaurant",
            "conditional-else-test",
            contextWithFalseValues);

        // Assert
        Assert.NotNull(result);

        // Should show else blocks when conditions are false
        Assert.Contains("No tables available right now.", result);
        Assert.Contains("Regular menu only.", result);
        Assert.Contains("Normal business hours.", result);

        // Should NOT show the true blocks
        Assert.DoesNotContain("Tables available for booking", result);
        Assert.DoesNotContain("Today's special: Paella", result);
        Assert.DoesNotContain("Holiday hours apply", result);

        // Should not contain conditional syntax
        Assert.DoesNotContain("{{#if", result);
        Assert.DoesNotContain("{{else}}", result);
        Assert.DoesNotContain("{{/if}}", result);

        // Test with true values to verify the if block works
        var contextWithTrueValues = new Dictionary<string, object>
        {
            ["hasTables"] = true,
            ["hasSpecials"] = "Paella",
            ["isHoliday"] = 1
        };

        var resultTrue = await _service.LoadSpecializedPromptAsync(
            "test-restaurant",
            "conditional-else-test",
            contextWithTrueValues);

        // Should show if blocks when conditions are true
        Assert.Contains("Tables available for booking.", resultTrue);
        Assert.Contains("Today's special: Paella", resultTrue);
        Assert.Contains("Holiday hours apply.", resultTrue);

        // Should NOT show the else blocks
        Assert.DoesNotContain("No tables available right now", resultTrue);
        Assert.DoesNotContain("Regular menu only", resultTrue);
        Assert.DoesNotContain("Normal business hours", resultTrue);
    }
}
