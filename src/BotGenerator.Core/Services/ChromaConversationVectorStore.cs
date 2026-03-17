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
    private string? _knowledgeCollectionId;
    private readonly string _knowledgeCollectionName;

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
        _knowledgeCollectionName = configuration["Chroma:KnowledgeCollectionName"] ?? "restaurant-knowledge";
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
                embeddings = ids.Select((_, i) => Enumerable.Repeat(0.0, 384).Select((v, j) => j == 0 ? i * 0.0001 : 0.0).ToArray()).ToArray(),
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
                return;
            }
        }

        /// <summary>
        /// Upserts a booking record to ChromaDB for semantic search.
        /// </summary>
        public async Task UpsertBookingAsync(
            string phoneNumber,
            BookingRecord booking,
            CancellationToken cancellationToken = default)
        {
            if (!IsOperational() || booking == null)
                return;

            var collectionId = await EnsureCollectionAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(collectionId))
                return;

            var normalizedPhone = NormalizePhone(phoneNumber);
            var bookingDoc = FormatBookingAsDocument(booking);
            var bookingId = $"booking:{booking.Id}";

            var payload = new
            {
                ids = new[] { bookingId },
                documents = new[] { bookingDoc },
                embeddings = new[] { Enumerable.Repeat(0.0, 384).Select((v, i) => i == 0 ? booking.Id * 0.0001 : 0.0).ToArray() },
                metadatas = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["phone"] = normalizedPhone,
                        ["type"] = "booking",
                        ["bookingId"] = booking.Id,
                        ["reservationDate"] = booking.ReservationDate.ToString("yyyy-MM-dd"),
                        ["reservationTime"] = booking.TimeFormatted,
                        ["partySize"] = booking.PartySize,
                        ["timestamp"] = DateTime.UtcNow.ToString("O")
                    }
                }
            };

            using var client = CreateClient();
            using var response = await client.PostAsJsonAsync(
                $"/api/v2/tenants/default_tenant/databases/default_database/collections/{collectionId}/upsert",
                payload,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Chroma booking upsert failed: status={Status}, body={Body}",
                    (int)response.StatusCode,
                    body);
            }
        }

        /// <summary>
        /// Formats a booking record as a searchable document.
        /// </summary>
        private static string FormatBookingAsDocument(BookingRecord booking)
        {
            var parts = new List<string>
            {
                $"Reserva número {booking.Id}",
                $"Cliente: {booking.CustomerName}",
                $"Teléfono: {booking.ContactPhone}",
                $"Fecha: {booking.ReservationDate:dd/MM/yyyy}",
                $"Hora: {booking.TimeFormatted}",
                $"Número de personas: {booking.PartySize}"
            };

            if (!string.IsNullOrWhiteSpace(booking.ArrozType))
            {
                parts.Add($"Tipo de arroz: {booking.ArrozType}");
            }

            if (booking.ArrozServings.HasValue && booking.ArrozServings > 0)
            {
                parts.Add($"Raciones de arroz: {booking.ArrozServings}");
            }

            return string.Join(". ", parts) + ".";
        }

        /// <summary>
        /// Queries both messages and bookings for a specific phone number.
        /// </summary>
        public async Task<List<ConversationDocument>> QueryPhoneContextAsync(
            string phoneNumber,
            string query,
            int topK = 10,
            string? filterType = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsOperational() || string.IsNullOrWhiteSpace(query) || topK <= 0)
                return new List<ConversationDocument>();

            var collectionId = await EnsureCollectionAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(collectionId))
                return new List<ConversationDocument>();

            var normalizedPhone = NormalizePhone(phoneNumber);

            // Build where clause for filtering
            var where = new Dictionary<string, object>
            {
                ["phone"] = normalizedPhone
            };

            if (!string.IsNullOrWhiteSpace(filterType))
            {
                where["type"] = filterType;
            }

            var queryPayload = new
            {
                query_texts = new[] { query },
                n_results = topK,
                where = where,
                include = new[] { "documents", "metadatas", "distances" }
            };

            using var client = CreateClient();
            using var response = await client.PostAsJsonAsync(
                $"/api/v2/tenants/default_tenant/databases/default_database/collections/{collectionId}/query",
                queryPayload,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Chroma query failed: status={Status}, body={Body}",
                    (int)response.StatusCode,
                    body);
                return new List<ConversationDocument>();
            }

            var result = await response.Content.ReadFromJsonAsync<ChromaQueryResponse>(cancellationToken: cancellationToken);
            if (result?.Documents == null || result.Documents.Length == 0)
                return new List<ConversationDocument>();

            var documents = new List<ConversationDocument>();
            for (int i = 0; i < result.Documents.Length; i++)
            {
                var doc = new ConversationDocument
                {
                    Content = result.Documents[i],
                    Distance = result.Distances?.Length > i ? result.Distances[i] : null,
                    Metadata = result.Metadatas?.Length > i ? result.Metadatas[i] : null
                };
                documents.Add(doc);
            }

            return documents;
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
                "/api/v2/tenants/default_tenant/databases/default_database/collections",
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
                    $"/api/v2/tenants/default_tenant/databases/default_database/collections/{Uri.EscapeDataString(_collectionName)}",
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
            $"/api/v2/tenants/default_tenant/databases/default_database/collections/{Uri.EscapeDataString(collectionId)}/{action}",
            $"/api/v2/tenants/default_tenant/databases/default_database/collections/{Uri.EscapeDataString(_collectionName)}/{action}"
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

    #region Knowledge Base Methods

    /// <summary>
    /// Queries the restaurant knowledge base for relevant information.
    /// </summary>
    public async Task<List<KnowledgeDocument>> QueryKnowledgeAsync(
        string query,
        string? documentType = null,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        if (!IsOperational() || string.IsNullOrWhiteSpace(query) || topK <= 0)
            return new List<KnowledgeDocument>();

        var collectionId = await EnsureKnowledgeCollectionAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(collectionId))
            return new List<KnowledgeDocument>();

        var where = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(documentType))
        {
            where["type"] = documentType;
        }

        var queryPayload = new
        {
            query_texts = new[] { query },
            n_results = topK,
            where = where.Count > 0 ? where : null,
            include = new[] { "documents", "metadatas", "distances" }
        };

        using var client = CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"/api/v2/tenants/default_tenant/databases/default_database/collections/{collectionId}/query",
            queryPayload,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Chroma knowledge query failed: status={Status}, body={Body}",
                (int)response.StatusCode,
                body);
            return new List<KnowledgeDocument>();
        }

        var result = await response.Content.ReadFromJsonAsync<ChromaQueryResponse>(cancellationToken: cancellationToken);
        if (result?.Documents == null || result.Documents.Length == 0)
            return new List<KnowledgeDocument>();

        var documents = new List<KnowledgeDocument>();
        for (int i = 0; i < result.Documents.Length; i++)
        {
            var doc = new KnowledgeDocument
            {
                Content = result.Documents[i],
                Distance = result.Distances?.Length > i ? result.Distances[i] : null
            };

            if (result.Metadatas?.Length > i && result.Metadatas[i] != null)
            {
                var meta = result.Metadatas[i];
                if (meta.TryGetValue("type", out var type) && type != null)
                    doc.DocumentType = type.ToString() ?? "policy";
                if (meta.TryGetValue("key", out var key) && key != null)
                    doc.Key = key.ToString() ?? "";
                if (meta.TryGetValue("keywords", out var kw) && kw != null)
                    doc.Keywords = kw.ToString()?.Split(',').Select(k => k.Trim()).ToList() ?? new List<string>();
                if (meta.TryGetValue("price_modifier", out var price) && price != null)
                    doc.PriceModifier = decimal.TryParse(price.ToString(), out var p) ? p : null;
                if (meta.TryGetValue("available", out var avail) && avail != null)
                    doc.Available = bool.TryParse(avail.ToString(), out var a) ? a : null;
                if (meta.TryGetValue("min_servings", out var minS) && minS != null)
                    doc.MinServings = int.TryParse(minS.ToString(), out var ms) ? ms : null;
            }

            documents.Add(doc);
        }

        return documents;
    }

    /// <summary>
    /// Upserts a document to the restaurant knowledge base.
    /// </summary>
    public async Task UpsertKnowledgeAsync(
        KnowledgeDocument document,
        CancellationToken cancellationToken = default)
    {
        if (!IsOperational() || document == null || string.IsNullOrWhiteSpace(document.Key))
            return;

        var collectionId = await EnsureKnowledgeCollectionAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(collectionId))
            return;

        var docId = $"kb:{document.DocumentType}:{document.Key}";

        var metadata = new Dictionary<string, object?>
        {
            ["type"] = document.DocumentType,
            ["key"] = document.Key,
            ["timestamp"] = DateTime.UtcNow.ToString("O")
        };

        if (document.Keywords.Count > 0)
            metadata["keywords"] = string.Join(",", document.Keywords);
        if (document.PriceModifier.HasValue)
            metadata["price_modifier"] = document.PriceModifier.Value.ToString();
        if (document.Available.HasValue)
            metadata["available"] = document.Available.Value.ToString();
        if (document.MinServings.HasValue)
            metadata["min_servings"] = document.MinServings.Value;

        var payload = new
        {
            ids = new[] { docId },
            documents = new[] { document.Content },
            embeddings = new[] { Enumerable.Repeat(0.0, 384).Select((v, i) => i == 0 ? document.Key.GetHashCode() * 0.0001 : 0.0).ToArray() },
            metadatas = new[] { metadata }
        };

        using var client = CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"/api/v2/tenants/default_tenant/databases/default_database/collections/{collectionId}/upsert",
            payload,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Chroma knowledge upsert failed: status={Status}, body={Body}",
                (int)response.StatusCode,
                body);
        }
    }

    /// <summary>
    /// Seeds the knowledge base with initial data if empty.
    /// </summary>
    public async Task SeedKnowledgeBaseAsync(CancellationToken cancellationToken = default)
    {
        if (!IsOperational())
            return;

        // Check if already seeded
        var existing = await QueryKnowledgeAsync("policy", null, 1, cancellationToken);
        if (existing.Count > 0)
        {
            _logger.LogInformation("Knowledge base already seeded, skipping");
            return;
        }

        _logger.LogInformation("Seeding knowledge base with initial data...");

        // Seed policies
        await UpsertKnowledgeAsync(new KnowledgeDocument
        {
            DocumentType = "policy",
            Key = "no_infant_menu",
            Content = "No tenemos menú infantil. Todos los comensales deben consumir un menú regular del carta.",
            Keywords = new List<string> { "menú infantil", "niños", "menu infantil", "niño" }
        }, cancellationToken);

        await UpsertKnowledgeAsync(new KnowledgeDocument
        {
            DocumentType = "policy",
            Key = "no_terraza",
            Content = "No disponomos de terraza, solo tenemos espacio interior con aire acondicionado.",
            Keywords = new List<string> { "terraza", "exterior", "terraza" }
        }, cancellationToken);

        await UpsertKnowledgeAsync(new KnowledgeDocument
        {
            DocumentType = "policy",
            Key = "opening_hours",
            Content = "El restaurante abre a las 13:30 y cierra entre 17:00-18:00. No aceptamos reservas para el mismo día.",
            Keywords = new List<string> { "horario", "abrir", "cerrar", "abertura", "cierre", "13:30" }
        }, cancellationToken);

        // Seed flow steps
        await UpsertKnowledgeAsync(new KnowledgeDocument
        {
            DocumentType = "flow_step",
            Key = "hora_validation",
            Content = "El restaurante abre a las 13:30. Rechaza horas antes de 13:30. Cierra entre 17:00-18:00. Rechaza horas después del cierre.",
            Keywords = new List<string> { "hora", "horario", "validar", "13:30", "14:00", "15:00" }
        }, cancellationToken);

        await UpsertKnowledgeAsync(new KnowledgeDocument
        {
            DocumentType = "flow_step",
            Key = "same_day_rejection",
            Content = "No aceptamos reservas para el mismo día. Para reservas urgentes, el cliente debe llamar al 638 857 294.",
            Keywords = new List<string> { "hoy", "mismo día", "urgente", "ahora" }
        }, cancellationToken);

        // Seed common responses
        await UpsertKnowledgeAsync(new KnowledgeDocument
        {
            DocumentType = "response",
            Key = "greeting",
            Content = "¡Hola {name}! ¿En qué puedo ayudarte?",
            Keywords = new List<string> { "hola", "buenas", "saludo", "Buenos días" }
        }, cancellationToken);

        await UpsertKnowledgeAsync(new KnowledgeDocument
        {
            DocumentType = "response",
            Key = "confirmation",
            Content = "¡Perfecto!",
            Keywords = new List<string> { "vale", "ok", "confirmar", "sí", "si" }
        }, cancellationToken);

        await UpsertKnowledgeAsync(new KnowledgeDocument
        {
            DocumentType = "response",
            Key = "error",
            Content = "Disculpa, no he entendido bien. ¿Puedes repetirlo? Para más información, llámanos al +34 638 857 294.",
            Keywords = new List<string> { "error", "repetir", "no entiendo" }
        }, cancellationToken);

        _logger.LogInformation("Knowledge base seeded successfully");
    }

    private async Task<string?> EnsureKnowledgeCollectionAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_knowledgeCollectionId))
            return _knowledgeCollectionId;

        var client = CreateClient();

        var createPayload = new
        {
            name = _knowledgeCollectionName,
            get_or_create = true,
            metadata = new Dictionary<string, object>
            {
                ["source"] = "BotGenerator",
                ["type"] = "knowledge"
            }
        };

        using var createResponse = await client.PostAsJsonAsync(
            "/api/v2/tenants/default_tenant/databases/default_database/collections",
            createPayload,
            cancellationToken);

        if (createResponse.IsSuccessStatusCode)
        {
            var json = await createResponse.Content.ReadAsStringAsync(cancellationToken);
            _knowledgeCollectionId = ExtractCollectionId(json) ?? _knowledgeCollectionName;
            return _knowledgeCollectionId;
        }

        if (createResponse.StatusCode == HttpStatusCode.Conflict)
        {
            using var getResponse = await client.GetAsync(
                $"/api/v2/tenants/default_tenant/databases/default_database/collections/{Uri.EscapeDataString(_knowledgeCollectionName)}",
                cancellationToken);

            if (getResponse.IsSuccessStatusCode)
            {
                var json = await getResponse.Content.ReadAsStringAsync(cancellationToken);
                _knowledgeCollectionId = ExtractCollectionId(json) ?? _knowledgeCollectionName;
                return _knowledgeCollectionId;
            }
        }

        var body = await createResponse.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning(
            "Unable to resolve Chroma knowledge collection {Collection}. status={Status}, body={Body}",
            _knowledgeCollectionName,
            (int)createResponse.StatusCode,
            body);

        return null;
    }

    #endregion

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

    /// <summary>
    /// Response model for ChromaDB query API.
    /// </summary>
    private class ChromaQueryResponse
    {
        public string[]? Documents { get; set; }
        public double[]? Distances { get; set; }
        public Dictionary<string, object?>[]? Metadatas { get; set; }
        public string[]? Ids { get; set; }
    }
}
