using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Chroma-backed vector memory for long-running conversation recall.
/// </summary>
public sealed class ChromaConversationVectorStore : IConversationVectorStore
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ChromaConversationVectorStore> _logger;
    private readonly bool _enabled;
    private readonly string? _apiUrl;
    private readonly string _collectionName;
    private readonly int _upsertBatchSize;

    private readonly SemaphoreSlim _collectionLock = new(1, 1);
    private string? _collectionId;
    private bool _collectionResolved;

    public ChromaConversationVectorStore(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<ChromaConversationVectorStore> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        _enabled = configuration.GetValue("Chroma:Enabled", true);
        _apiUrl = configuration["Chroma:ApiUrl"]?.Trim();
        _collectionName = configuration["Chroma:CollectionName"] ?? "bot-conversation-memory";
        _upsertBatchSize = Math.Max(1, configuration.GetValue("Chroma:UpsertBatchSize", 50));

        _logger.LogInformation(
            "ChromaConversationVectorStore initialized (enabled={Enabled}, configured={Configured}, collection={Collection})",
            _enabled,
            !string.IsNullOrWhiteSpace(_apiUrl),
            _collectionName);
    }

    public Task UpsertMessageAsync(
        string phoneNumber,
        ChatMessage message,
        CancellationToken cancellationToken = default)
    {
        return UpsertMessagesAsync(phoneNumber, new[] { message }, cancellationToken);
    }

    public async Task UpsertMessagesAsync(
        string phoneNumber,
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        if (!IsOperational())
            return;

        var filtered = messages
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .ToList();

        if (filtered.Count == 0)
            return;

        var collectionId = await EnsureCollectionAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(collectionId))
            return;

        var normalizedPhone = NormalizePhone(phoneNumber);

        foreach (var batch in Batch(filtered, _upsertBatchSize))
        {
            var ids = batch.Select(m => BuildVectorId(normalizedPhone, m)).ToArray();
            var documents = batch.Select(m => m.Content).ToArray();
            var metadatas = batch.Select(m => new Dictionary<string, object?>
            {
                ["phone"] = normalizedPhone,
                ["role"] = m.Role,
                ["timestamp"] = m.Timestamp ?? string.Empty,
                ["messageId"] = m.MessageId ?? string.Empty,
                ["fromName"] = m.FromName ?? string.Empty
            }).ToArray();

            var payload = new
            {
                ids,
                documents,
                metadatas
            };

            using var response = await PostToCollectionAsync(collectionId, "upsert", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Chroma upsert failed: status={Status}, body={Body}",
                    (int)response.StatusCode,
                    body);
                return;
            }
        }
    }

    public async Task<List<ChatMessage>> QueryRelevantAsync(
        string phoneNumber,
        string query,
        int topK,
        CancellationToken cancellationToken = default)
    {
        if (!IsOperational() || string.IsNullOrWhiteSpace(query) || topK <= 0)
            return new List<ChatMessage>();

        var collectionId = await EnsureCollectionAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(collectionId))
            return new List<ChatMessage>();

        var normalizedPhone = NormalizePhone(phoneNumber);

        var payload = new
        {
            query_texts = new[] { query },
            n_results = topK,
            where = new Dictionary<string, object>
            {
                ["phone"] = normalizedPhone
            }
        };

        using var response = await PostToCollectionAsync(collectionId, "query", payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Chroma query failed: status={Status}, body={Body}",
                (int)response.StatusCode,
                body);
            return new List<ChatMessage>();
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseQueryMessages(json);
    }

    private bool IsOperational() => _enabled && !string.IsNullOrWhiteSpace(_apiUrl);

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("Chroma");
        client.BaseAddress = new Uri(_apiUrl!.TrimEnd('/'));
        return client;
    }

    private async Task<string?> EnsureCollectionAsync(CancellationToken cancellationToken)
    {
        if (_collectionResolved && !string.IsNullOrWhiteSpace(_collectionId))
            return _collectionId;

        await _collectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_collectionResolved && !string.IsNullOrWhiteSpace(_collectionId))
                return _collectionId;

            var client = CreateClient();

            var createPayload = new
            {
                name = _collectionName,
                get_or_create = true,
                metadata = new Dictionary<string, object>
                {
                    ["source"] = "BotGenerator"
                }
            };

            using var createResponse = await client.PostAsJsonAsync(
                "/api/v1/collections",
                createPayload,
                cancellationToken);

            if (createResponse.IsSuccessStatusCode)
            {
                var json = await createResponse.Content.ReadAsStringAsync(cancellationToken);
                _collectionId = ExtractCollectionId(json) ?? _collectionName;
                _collectionResolved = true;
                return _collectionId;
            }

            if (createResponse.StatusCode == HttpStatusCode.Conflict)
            {
                using var getResponse = await client.GetAsync(
                    $"/api/v1/collections/{Uri.EscapeDataString(_collectionName)}",
                    cancellationToken);

                if (getResponse.IsSuccessStatusCode)
                {
                    var json = await getResponse.Content.ReadAsStringAsync(cancellationToken);
                    _collectionId = ExtractCollectionId(json) ?? _collectionName;
                    _collectionResolved = true;
                    return _collectionId;
                }
            }

            var body = await createResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Unable to resolve Chroma collection {Collection}. status={Status}, body={Body}",
                _collectionName,
                (int)createResponse.StatusCode,
                body);

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize Chroma collection {Collection}", _collectionName);
            return null;
        }
        finally
        {
            _collectionLock.Release();
        }
    }

    private async Task<HttpResponseMessage> PostToCollectionAsync(
        string collectionId,
        string action,
        object payload,
        CancellationToken cancellationToken)
    {
        var client = CreateClient();

        var endpoints = new List<string>
        {
            $"/api/v1/collections/{Uri.EscapeDataString(collectionId)}/{action}",
            $"/api/v1/collections/{Uri.EscapeDataString(_collectionName)}/{action}"
        };

        HttpResponseMessage? fallback = null;

        foreach (var endpoint in endpoints)
        {
            var response = await client.PostAsJsonAsync(endpoint, payload, cancellationToken);
            if (response.IsSuccessStatusCode)
                return response;

            if (response.StatusCode == HttpStatusCode.NotFound ||
                response.StatusCode == HttpStatusCode.MethodNotAllowed)
            {
                fallback = response;
                continue;
            }

            return response;
        }

        return fallback ?? new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static string? ExtractCollectionId(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                    return idEl.GetString();

                if (root.TryGetProperty("collection", out var collectionEl) &&
                    collectionEl.ValueKind == JsonValueKind.Object &&
                    collectionEl.TryGetProperty("id", out var nestedId) &&
                    nestedId.ValueKind == JsonValueKind.String)
                {
                    return nestedId.GetString();
                }
            }
        }
        catch
        {
            // ignore, caller will fallback
        }

        return null;
    }

    private static List<ChatMessage> ParseQueryMessages(string json)
    {
        var output = new List<ChatMessage>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("documents", out var docsOuter) || docsOuter.ValueKind != JsonValueKind.Array)
                return output;

            var metasOuter = root.TryGetProperty("metadatas", out var mOuter) && mOuter.ValueKind == JsonValueKind.Array
                ? mOuter
                : default;

            if (docsOuter.GetArrayLength() == 0)
                return output;

            var documents = docsOuter[0];
            if (documents.ValueKind != JsonValueKind.Array)
                return output;

            JsonElement metadataItems = default;
            var hasMetadataItems = metasOuter.ValueKind == JsonValueKind.Array && metasOuter.GetArrayLength() > 0;
            if (hasMetadataItems)
                metadataItems = metasOuter[0];

            for (var i = 0; i < documents.GetArrayLength(); i++)
            {
                var content = documents[i].GetString();
                if (string.IsNullOrWhiteSpace(content))
                    continue;

                var role = "user";
                string? timestamp = null;
                string? messageId = null;
                string? fromName = null;

                if (hasMetadataItems && metadataItems.ValueKind == JsonValueKind.Array && i < metadataItems.GetArrayLength())
                {
                    var meta = metadataItems[i];
                    if (meta.ValueKind == JsonValueKind.Object)
                    {
                        if (meta.TryGetProperty("role", out var roleEl) && roleEl.ValueKind == JsonValueKind.String)
                            role = roleEl.GetString() ?? "user";

                        if (meta.TryGetProperty("timestamp", out var tsEl) && tsEl.ValueKind == JsonValueKind.String)
                            timestamp = tsEl.GetString();

                        if (meta.TryGetProperty("messageId", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                            messageId = idEl.GetString();

                        if (meta.TryGetProperty("fromName", out var fromEl) && fromEl.ValueKind == JsonValueKind.String)
                            fromName = fromEl.GetString();
                    }
                }

                output.Add(new ChatMessage
                {
                    Role = role,
                    Content = content,
                    Timestamp = timestamp,
                    MessageId = messageId,
                    FromName = fromName
                });
            }

            output = output
                .DistinctBy(m => !string.IsNullOrWhiteSpace(m.MessageId)
                    ? m.MessageId
                    : $"{m.Role}|{m.Timestamp}|{m.Content}")
                .ToList();

            return output;
        }
        catch
        {
            return output;
        }
    }

    private static IEnumerable<List<T>> Batch<T>(List<T> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
    }

    private static string NormalizePhone(string phone)
        => new(phone.Where(char.IsDigit).ToArray());

    private static string BuildVectorId(string normalizedPhone, ChatMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.MessageId))
            return $"{normalizedPhone}:{message.MessageId}";

        var raw = $"{normalizedPhone}|{message.Role}|{message.Timestamp}|{message.Content}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        var hash = Convert.ToHexString(bytes).ToLowerInvariant();
        return $"{normalizedPhone}:{hash}";
    }
}
