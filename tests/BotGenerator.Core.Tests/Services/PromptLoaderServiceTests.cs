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
