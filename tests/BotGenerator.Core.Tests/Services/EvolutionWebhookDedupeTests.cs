using BotGenerator.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BotGenerator.Core.Tests.Services;

public class EvolutionWebhookDedupeTests
{
    [Fact]
    public async Task TryClaimAsync_WithoutRedisConfiguration_ReturnsUnavailableWithoutNetworkAccess()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        using var dedupe = new EvolutionWebhookDedupe(
            configuration,
            NullLogger<EvolutionWebhookDedupe>.Instance);

        var result = await dedupe.TryClaimAsync("villa-carmen", "message-123");

        result.State.Should().Be(EvolutionWebhookDedupeState.Unavailable);
    }
}
