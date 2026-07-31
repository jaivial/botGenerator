using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BotGenerator.Core.Services;

public interface IEvolutionWebhookDedupe
{
    Task<EvolutionWebhookDedupeClaim> TryClaimAsync(
        string instanceName,
        string messageId,
        CancellationToken cancellationToken = default);

    Task<bool> CompleteAsync(EvolutionWebhookDedupeClaim claim, CancellationToken cancellationToken = default);

    Task ReleaseAsync(EvolutionWebhookDedupeClaim claim, CancellationToken cancellationToken = default);
}

public enum EvolutionWebhookDedupeState
{
    Claimed,
    Completed,
    Processing,
    Unavailable
}

public sealed record EvolutionWebhookDedupeClaim(EvolutionWebhookDedupeState State, string? Key = null, string? Token = null);

/// <summary>
/// Redis SET NX dedupe for Evolution inbound messages. It is deliberately inbound-only.
/// </summary>
public sealed class EvolutionWebhookDedupe : IEvolutionWebhookDedupe, IDisposable
{
    private readonly string _connectionString;
    private readonly TimeSpan _ttl;
    private readonly ILogger<EvolutionWebhookDedupe> _logger;
    private readonly Lazy<IConnectionMultiplexer> _connection;

    public EvolutionWebhookDedupe(
        IConfiguration configuration,
        ILogger<EvolutionWebhookDedupe> logger)
    {
        _connectionString = configuration["Redis:ConnectionString"] ?? "";
        _ttl = TimeSpan.FromHours(Math.Max(1, configuration.GetValue("WhatsApp:Evolution:WebhookDedupeTtlHours", 24)));
        _logger = logger;
        _connection = new Lazy<IConnectionMultiplexer>(Connect);
    }

    public async Task<EvolutionWebhookDedupeClaim> TryClaimAsync(
        string instanceName,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _logger.LogError("Evolution webhook dedupe is unavailable because Redis:ConnectionString is not configured");
            return new EvolutionWebhookDedupeClaim(EvolutionWebhookDedupeState.Unavailable);
        }

        try
        {
            var messageHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(messageId)));
            var key = $"bot:evolution:inbound:{instanceName}:{messageHash}";
            var token = $"processing:{Guid.NewGuid():N}";
            var database = _connection.Value.GetDatabase();
            var added = await database.StringSetAsync(key, token, TimeSpan.FromMinutes(10), When.NotExists).WaitAsync(cancellationToken);
            if (added)
                return new EvolutionWebhookDedupeClaim(EvolutionWebhookDedupeState.Claimed, key, token);

            var state = await database.StringGetAsync(key).WaitAsync(cancellationToken);
            return state == "completed"
                ? new EvolutionWebhookDedupeClaim(EvolutionWebhookDedupeState.Completed)
                : new EvolutionWebhookDedupeClaim(EvolutionWebhookDedupeState.Processing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Evolution webhook dedupe is unavailable for instance {Instance}", instanceName);
            return new EvolutionWebhookDedupeClaim(EvolutionWebhookDedupeState.Unavailable);
        }
    }

    public async Task<bool> CompleteAsync(EvolutionWebhookDedupeClaim claim, CancellationToken cancellationToken = default)
    {
        if (claim.State != EvolutionWebhookDedupeState.Claimed || claim.Key is null || claim.Token is null)
            return false;

        try
        {
            var result = await _connection.Value.GetDatabase().ScriptEvaluateAsync(
                "if redis.call('GET', KEYS[1]) == ARGV[1] then redis.call('SET', KEYS[1], 'completed', 'EX', ARGV[2]); return 1 end; return 0",
                [claim.Key], [claim.Token, ((int)_ttl.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture)])
                .WaitAsync(cancellationToken);
            return (int)result == 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not complete Evolution webhook claim");
            return false;
        }
    }

    public async Task ReleaseAsync(EvolutionWebhookDedupeClaim claim, CancellationToken cancellationToken = default)
    {
        if (claim.State != EvolutionWebhookDedupeState.Claimed || claim.Key is null || claim.Token is null)
            return;

        try
        {
            await _connection.Value.GetDatabase().ScriptEvaluateAsync(
                "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('DEL', KEYS[1]) end; return 0",
                [claim.Key], [claim.Token]).WaitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not release Evolution webhook claim");
        }
    }

    public void Dispose()
    {
        if (_connection.IsValueCreated)
            _connection.Value.Dispose();
    }

    private IConnectionMultiplexer Connect()
    {
        var options = ParseConnectionOptions(_connectionString);
        options.AbortOnConnectFail = false;
        options.ConnectRetry = 1;
        options.ConnectTimeout = 5000;
        return ConnectionMultiplexer.Connect(options);
    }

    private static ConfigurationOptions ParseConnectionOptions(string connectionString)
    {
        if (!connectionString.StartsWith("redis://", StringComparison.OrdinalIgnoreCase) &&
            !connectionString.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
        {
            return ConfigurationOptions.Parse(connectionString);
        }

        var schemeEnd = connectionString.IndexOf("://", StringComparison.Ordinal);
        var scheme = connectionString[..schemeEnd];
        var remainder = connectionString[(schemeEnd + 3)..];
        var at = remainder.LastIndexOf('@');
        var authentication = at >= 0 ? remainder[..at] : string.Empty;
        var address = at >= 0 ? remainder[(at + 1)..] : remainder;
        var uri = new Uri($"{scheme}://{address}");
        var options = new ConfigurationOptions { Ssl = scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase) };

        options.EndPoints.Add(uri.Host, uri.IsDefaultPort ? 6379 : uri.Port);
        if (!string.IsNullOrEmpty(authentication))
        {
            var separator = authentication.IndexOf(':');
            if (separator >= 0)
            {
                var user = Uri.UnescapeDataString(authentication[..separator]);
                if (!string.IsNullOrEmpty(user))
                    options.User = user;
                options.Password = Uri.UnescapeDataString(authentication[(separator + 1)..]);
            }
            else
            {
                options.Password = Uri.UnescapeDataString(authentication);
            }
        }

        if (int.TryParse(uri.AbsolutePath.Trim('/'), out var database))
            options.DefaultDatabase = database;

        return options;
    }
}
