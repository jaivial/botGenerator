# ChromaDB Integration Plan for Local Development

## Objective

Set up ChromaDB locally to enable semantic search for conversation history and booking data, replacing the current approach of fetching full conversation history from WhatsApp API on every webhook trigger.

## Current Architecture Problems

1. **Heavy WhatsApp API calls**: Every webhook triggers `GetFullHistoryAsync()` which pages through up to 50 pages (5000 messages) from UAZAPI
2. **Complex sync logic**: The `SyncWhatsAppHistoryAsync()` method has 200+ lines of complex deduplication logic
3. **Failures under load**: The error shown (`Perdona, algo salió mal`) occurs during high-load scenarios when making multiple API calls

## New Architecture

```
WhatsApp Webhook
       ↓
WebhookController (C#)
       ↓
MainConversationAgent
       ↓
Query ChromaDB (semantic search)
       ↓
Return contextual response
```

```
Booking Created (PHP)
       ↓
insert_booking.php / insert_booking_front.php
       ↓
Upsert to ChromaDB (booking document)
       ↓
Return success + send WhatsApp
```

---

## Implementation Plan

### Phase 1: Local Infrastructure Setup

#### 1.1 Start ChromaDB Locally

- [ ] **Task 1.1.1**: Create `docker-compose.yml` for local development
  ```yaml
  version: '3.8'
  services:
    chromadb:
      image: chromadb/chroma:latest
      ports:
        - "8000:8000"
      volumes:
        - chroma_data:/chroma/chroma
      environment:
        - IS_PERSISTENT=TRUE
        - ANONYMIZED_TELEMETRY=FALSE
  volumes:
    chroma_data:
  ```

- [ ] **Task 1.1.2**: Create `.env` file for local configuration
  ```
  # ChromaDB
  CHROMA_ENABLED=true
  CHROMA_API_URL=http://localhost:8000
  CHROMA_COLLECTION_NAME=phone-conversations
  
  # BotGenerator (C#) - WhatsApp
  WHATSAPP_API_URL=http://localhost:8080
  WHATSAPP_TOKEN=your_token_here
  
  # BotGenerator - Database
  MYSQL_CONNECTION_STRING=Server=localhost;Database=villacarmen;User=root;Password=your_password;
  ```

- [ ] **Task 1.1.3**: Start ChromaDB container
  ```bash
  docker-compose up -d
  # Verify it's running
  curl http://localhost:8000/api/v1/heartbeat
  ```

#### 1.2 Update BotGenerator Configuration

- [ ] **Task 1.2.1**: Update `appsettings.Development.json` or create local override
  ```json
  {
    "Chroma": {
      "Enabled": true,
      "ApiUrl": "http://localhost:8000",
      "CollectionName": "phone-conversations",
      "TopK": 10,
      "UpsertBatchSize": 50,
      "UsePhoneCollections": false
    },
    "History": {
      "MaxMessages": 30,
      "SessionTimeoutMinutes": 30,
      "SyncPageSize": 100,
      "FullSyncMaxPages": 50,
      "IncrementalSyncMaxPages": 6,
      "UseVectorStoreForHistory": true,
      "RecentMessagesLimit": 5
    }
  }
  ```

- [ ] **Task 1.2.2**: Verify ChromaDB connection in BotGenerator logs

---

### Phase 2: C# Bot - Enhance ChromaDB Integration

#### 2.1 Extend ChromaConversationVectorStore

- [ ] **Task 2.1.1**: Add `UpsertBookingAsync` method to `IConversationVectorStore` interface
  ```csharp
  // File: src/BotGenerator.Core/Services/IConversationVectorStore.cs
  Task UpsertBookingAsync(
      string phoneNumber,
      BookingRecord booking,
      CancellationToken cancellationToken = default);
  ```

- [ ] **Task 2.1.2**: Implement `UpsertBookingAsync` in `ChromaConversationVectorStore`
  - Create booking document with semantic text representation
  - Store booking metadata (date, time, people, rice, etc.)
  - Use `type: "booking"` in metadata for filtering
  
  ```csharp
  // File: src/BotGenerator.Core/Services/ChromaConversationVectorStore.cs
  
  public async Task UpsertBookingAsync(
      string phoneNumber,
      BookingRecord booking,
      CancellationToken cancellationToken = default)
  {
      if (!IsOperational())
          return;

      var collectionId = await EnsureCollectionAsync(cancellationToken);
      if (string.IsNullOrWhiteSpace(collectionId))
          return;

      var normalizedPhone = NormalizePhone(phoneNumber);
      var document = FormatBookingAsDocument(booking);
      
      var payload = new
      {
          ids = new[] { $"booking:{booking.Id}" },
          documents = new[] { document },
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
                  ["customerName"] = booking.CustomerName,
                  ["timestamp"] = DateTime.UtcNow.ToString("O")
              }
          }
      };
      
      // POST to collection upsert endpoint
  }
  ```

- [ ] **Task 2.1.3**: Add helper method `FormatBookingAsDocument`
  ```csharp
  private static string FormatBookingAsDocument(BookingRecord booking)
  {
      var rice = string.IsNullOrEmpty(booking.ArrozType)
          ? "sin arroz"
          : $"{booking.ArrozType} ({booking.ArrozServings} raciones)";
      
      return $"Reserva confirmada para {booking.PartySize} personas el {booking.DateFormatted} a las {booking.TimeFormatted}. " +
             $"Cliente: {booking.CustomerName}. " +
             $"Rice: {rice}. " +
             $"Sillas altas: {booking.HighChairs}, Carritos: {booking.BabyStrollers}.";
  }
  ```

#### 2.2 Add Hybrid Query Method

- [ ] **Task 2.2.1**: Add `QueryPhoneContextAsync` method to interface
  ```csharp
  // File: src/BotGenerator.Core/Services/IConversationVectorStore.cs
  
  Task<List<ConversationDocument>> QueryPhoneContextAsync(
      string phoneNumber,
      string query,
      int topK = 10,
      CancellationToken cancellationToken = default);
  ```

- [ ] **Task 2.2.2**: Implement `QueryPhoneContextAsync` in `ChromaConversationVectorStore`
  - Query both messages and bookings for the phone number
  - Return combined results sorted by relevance
  - Include document type in results

#### 2.3 Update BookingHandler to Upsert to ChromaDB

- [ ] **Task 2.3.1**: Inject `IConversationVectorStore` into `BookingHandler`
  
  ```csharp
  // File: src/BotGenerator.Core/Handlers/BookingHandler.cs
  
  public class BookingHandler
  {
      private readonly IConversationVectorStore? _vectorStore;
      
      public BookingHandler(
          // ... existing params
          IConversationVectorStore? vectorStore = null)
      {
          // ... existing init
          _vectorStore = vectorStore;
      }
  }
  ```

- [ ] **Task 2.3.2**: Add ChromaDB upsert after successful booking creation
  
  Location: `src/BotGenerator.Core/Handlers/BookingHandler.cs` around line 44-65
  
  ```csharp
  if (success && bookingId.HasValue)
  {
      // Upsert booking to ChromaDB for semantic search
      if (_vectorStore != null)
      {
          try
          {
              var bookingRecord = new BookingRecord
              {
                  Id = (int)bookingId.Value,
                  CustomerName = booking.Name,
                  ReservationDate = DateTime.Parse(booking.Date),
                  ReservationTime = TimeSpan.Parse(booking.Time),
                  PartySize = booking.People,
                  ArrozType = booking.ArrozType,
                  ArrozServings = booking.ArrozServings,
                  ContactPhone = booking.Phone,
                  HighChairs = booking.HighChairs,
                  BabyStrollers = booking.BabyStrollers
              };
              
              await _vectorStore.UpsertBookingAsync(booking.Phone, bookingRecord, cancellationToken);
              _logger.LogInformation("Upserted booking {BookingId} to ChromaDB", bookingId.Value);
          }
          catch (Exception ex)
          {
              _logger.LogWarning(ex, "Failed to upsert booking to ChromaDB, continuing...");
          }
      }
      
      // ... rest of existing code
  }
  ```

- [ ] **Task 2.3.3**: Register `BookingHandler` with vector store in DI container
  
  Location: `src/BotGenerator.Api/Program.cs`

---

### Phase 3: PHP - Insert Bookings into ChromaDB

#### 3.1 Create ChromaDB PHP Helper

- [ ] **Task 3.1.1**: Create `includes/chroma_helpers.php`

  Location: `/home/jaime/Downloads/projects/alqueriavillacarmen/includes/chroma_helpers.php`
  
  ```php
  <?php
  /**
   * ChromaDB integration helper functions
   */
  
  /**
   * Get ChromaDB configuration from environment
   */
  function get_chroma_config() {
      return [
          'enabled' => getenv('CHROMA_ENABLED') === 'true',
          'api_url' => getenv('CHROMA_API_URL') ?: 'http://localhost:8000',
          'collection' => getenv('CHROMA_COLLECTION') ?: 'phone-conversations'
      ];
  }
  
  /**
   * Ensure ChromaDB collection exists
   */
  function ensure_chroma_collection($chromaConfig) {
      $client = new ChromaDBClient($chromaConfig['api_url']);
      
      try {
          $response = $client->createCollection([
              'name' => $chromaConfig['collection'],
              'get_or_create' => true
          ]);
          return true;
      } catch (Exception $e) {
          error_log("ChromaDB collection creation: " . $e->getMessage());
          return false;
      }
  }
  
  /**
   * Upsert booking to ChromaDB
   */
  function upsert_booking_to_chroma($bookingData, $chromaConfig) {
      if (!$chromaConfig['enabled']) {
          return ['success' => true, 'skipped' => true, 'reason' => 'ChromaDB not enabled'];
      }
      
      $client = new ChromaDBClient($chromaConfig['api_url']);
      $collection = $chromaConfig['collection'];
      
      // Format booking as searchable document
      $document = format_booking_document($bookingData);
      
      // Normalize phone (remove country code, keep only digits)
      $phone = preg_replace('/[^0-9]/', '', $bookingData['contact_phone']);
      $phone = substr($phone, -9); // Keep last 9 digits
      
      $bookingId = $bookingData['booking_id'];
      
      // Prepare metadata
      $arrozTypes = [];
      $arrozServings = [];
      
      if (!empty($bookingData['arroz_type'])) {
          $arrozTypes = is_array($bookingData['arroz_type']) 
              ? $bookingData['arroz_type'] 
              : json_decode($bookingData['arroz_type'], true);
          $arrozServings = is_array($bookingData['arroz_servings'])
              ? $bookingData['arroz_servings']
              : json_decode($bookingData['arroz_servings'] ?? '[]', true);
      }
      
      $riceText = '';
      if (!empty($arrozTypes) && is_array($arrozTypes)) {
          $riceParts = [];
          foreach ($arrozTypes as $idx => $type) {
              $serv = $arrozServings[$idx] ?? 0;
              if ($serv > 0) {
                  $riceParts[] = "$type ($serv raciones)";
              }
          }
          $riceText = implode(', ', $riceParts);
      }
      if (empty($riceText)) {
          $riceText = 'sin arroz';
      }
      
      $metadata = [
          'phone' => $phone,
          'type' => 'booking',
          'booking_id' => (int)$bookingId,
          'reservation_date' => $bookingData['reservation_date'],
          'reservation_time' => $bookingData['reservation_time'],
          'party_size' => (int)$bookingData['party_size'],
          'customer_name' => $bookingData['customer_name'],
          'arroz_type' => $riceText,
          'baby_strollers' => (int)($bookingData['baby_strollers'] ?? 0),
          'high_chairs' => (int)($bookingData['high_chairs'] ?? 0),
          'timestamp' => date('c')
      ];
      
      try {
          $response = $client->upsert(
              $collection,
              [
                  'ids' => ["booking:{$bookingId}"],
                  'documents' => [$document],
                  'metadatas' => [$metadata]
              ]
          );
          
          return [
              'success' => true,
              'booking_id' => $bookingId,
              'document' => $document
          ];
      } catch (Exception $e) {
          error_log("ChromaDB upsert error: " . $e->getMessage());
          return [
              'success' => false,
              'error' => $e->getMessage()
          ];
      }
  }
  
  /**
   * Format booking data as searchable document
   */
  function format_booking_document($booking) {
      $date = $booking['reservation_date'];
      $time = $booking['reservation_time'];
      $people = $booking['party_size'];
      $name = $booking['customer_name'];
      
      $arrozText = '';
      if (!empty($booking['arroz_type'])) {
          $arrozTypes = is_array($booking['arroz_type']) 
              ? $booking['arroz_type'] 
              : json_decode($booking['arroz_type'], true);
          $arrozServings = is_array($booking['arroz_servings'])
              ? $booking['arroz_servings']
              : json_decode($booking['arroz_servings'] ?? '[]', true);
          
          if (is_array($arrozTypes)) {
              $parts = [];
              foreach ($arrozTypes as $idx => $type) {
                  $serv = $arrozServings[$idx] ?? 0;
                  if ($serv > 0) {
                      $parts[] = "$type ($serv raciones)";
                  }
              }
              $arrozText = !empty($parts) ? implode(', ', $parts) : 'sin arroz';
          }
      }
      if (empty($arrozText)) {
          $arrozText = 'sin arroz';
      }
      
      $tronas = (int)($booking['high_chairs'] ?? 0);
      $carritos = (int)($booking['baby_strollers'] ?? 0);
      
      return "Reserva confirmada para $people personas el $date a las $time. Cliente: $name. " .
             "Arroz: $arrozText. Sillas altas: $tronas, Carritos: $carritos.";
  }
  
  /**
   * Simple ChromaDB HTTP Client
   */
  class ChromaDBClient {
      private string $baseUrl;
      
      public function __construct(string $baseUrl) {
          $this->baseUrl = rtrim($baseUrl, '/');
      }
      
      public function createCollection(array $payload): array {
          $response = $this->request('POST', '/api/v1/collections', $payload);
          return $response;
      }
      
      public function upsert(string $collection, array $payload): array {
          $response = $this->request('POST', "/api/v1/collections/{$collection}/upsert", $payload);
          return $response;
      }
      
      public function query(string $collection, array $payload): array {
          $response = $this->request('POST', "/api/v1/collections/{$collection}/query", $payload);
          return $response;
      }
      
      private function request(string $method, string $endpoint, array $data): array {
          $url = $this->baseUrl . $endpoint;
          
          $ch = curl_init();
          curl_setopt($ch, CURLOPT_URL, $url);
          curl_setopt($ch, CURLOPT_CUSTOMREQUEST, $method);
          curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($data));
          curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
          curl_setopt($ch, CURLOPT_HTTPHEADER, [
              'Content-Type: application/json'
          ]);
          
          $response = curl_exec($ch);
          $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
          curl_close($ch);
          
          if ($httpCode >= 400) {
              throw new Exception("ChromaDB error: HTTP $httpCode - $response");
          }
          
          return json_decode($response, true) ?? [];
      }
  }
  ```

#### 3.2 Update insert_booking_front.php

- [ ] **Task 3.2.1**: Add ChromaDB include after other requires
  
  Location: `/home/jaime/Downloads/projects/alqueriavillacarmen/insert_booking_front.php` around line 32
  
  ```php
  require('includes/chroma_helpers.php');
  ```

- [ ] **Task 3.2.2**: Add ChromaDB upsert after successful booking commit
  
  Location: `/home/jaime/Downloads/projects/alqueriavillacarmen/insert_booking_front.php` around line 394-420 (after `$conn->commit()`)
  
  ```php
  // Commit the transaction BEFORE sending notifications
  $conn->commit();
  cache_invalidate_booking_date($reservation_date, false, false);
  $stmt->close();
  $stmt = null;
  
  $transactionStarted = false;
  
  // ============================================================
  // CHROMADB: Upsert booking for semantic search
  // ============================================================
  $chromaResult = null;
  $chromaConfig = get_chroma_config();
  if ($chromaConfig['enabled']) {
      try {
          $bookingDataForChroma = [
              'booking_id' => $bookingId,
              'reservation_date' => $reservation_date,
              'reservation_time' => $reservation_time,
              'party_size' => $party_size,
              'customer_name' => $customer_name,
              'contact_phone' => $contact_phone,
              'arroz_type' => $arroz_type_json,
              'arroz_servings' => $arroz_servings_json,
              'baby_strollers' => $baby_strollers,
              'high_chairs' => $high_chairs
          ];
          $chromaResult = upsert_booking_to_chroma($bookingDataForChroma, $chromaConfig);
          
          if (!$chromaResult['success']) {
              error_log("ChromaDB upsert failed for booking $bookingId: " . ($chromaResult['error'] ?? 'unknown'));
          }
      } catch (Exception $chromaEx) {
          error_log("ChromaDB exception for booking $bookingId: " . $chromaEx->getMessage());
      }
  }
  ```

- [ ] **Task 3.2.3**: Add chroma_sent to response
  
  Location: `/home/jaime/Downloads/projects/alqueriavillacarmen/insert_booking_front.php` around line 449-458
  
  ```php
  $response = [
      'success' => true,
      'message' => '¡Reserva realizada con éxito!',
      'booking_id' => $bookingId,
      'notifications_sent' => empty($notificationErrors),
      'email_sent' => !in_array('EMAIL_SEND_FAILED', $notificationErrors) && !array_filter($notificationErrors, fn($e) => str_starts_with($e, 'EMAIL_ERROR')),
      'whatsapp_sent' => !array_filter($notificationErrors, fn($e) => str_starts_with($e, 'WHATSAPP')),
      'chroma_synced' => $chromaResult['success'] ?? false
  ];
  ```

#### 3.3 Update insert_booking.php

- [ ] **Task 3.3.1**: Add ChromaDB include after other requires
  
  Location: `/home/jaime/Downloads/projects/alqueriavillacarmen/insert_booking.php` around line 36
  
  ```php
  require_once 'includes/chroma_helpers.php';
  ```

- [ ] **Task 3.3.2**: Add ChromaDB upsert after successful booking commit
  
  Location: `/home/jaime/Downloads/projects/alqueriavillacarmen/insert_booking.php` around line 390-412 (after `$conn->commit()`)
  
  ```php
  // Commit the transaction BEFORE sending notifications
  $conn->commit();
  cache_invalidate_booking_date($reservation_date, false, false);
  $stmt->close();
  $stmt = null;
  
  // ============================================================
  // CHROMADB: Upsert booking for semantic search
  // ============================================================
  $chromaResult = null;
  $chromaConfig = get_chroma_config();
  if ($chromaConfig['enabled']) {
      try {
          $bookingDataForChroma = [
              'booking_id' => $bookingId,
              'reservation_date' => $reservation_date,
              'reservation_time' => $reservation_time,
              'party_size' => $party_size,
              'customer_name' => $customer_name,
              'contact_phone' => $contact_phone,
              'arroz_type' => $arroz_type_json,
              'arroz_servings' => $arroz_servings_json,
              'baby_strollers' => $baby_strollers,
              'high_chairs' => $high_chairs
          ];
          $chromaResult = upsert_booking_to_chroma($bookingDataForChroma, $chromaConfig);
          
          if (!$chromaResult['success']) {
              error_log("ChromaDB upsert failed for booking $bookingId: " . ($chromaResult['error'] ?? 'unknown'));
          }
      } catch (Exception $chromaEx) {
          error_log("ChromaDB exception for booking $bookingId: " . $chromaEx->getMessage());
      }
  }
  ```

- [ ] **Task 3.3.2**: Add chroma_sent to response
  
  Location: `/home/jaime/Downloads/projects/alqueriavillacarmen/insert_booking.php` around line 424-429
  
  ```php
  header('Content-Type: application/json');
  echo json_encode([
      'success' => true,
      'booking_id' => $bookingId,
      'whatsapp_sent' => is_array($whatsAppResult) ? (bool)($whatsAppResult['success'] ?? false) : ($whatsAppError === null),
      'chroma_synced' => $chromaResult['success'] ?? false
  ]);
  ```

---

### Phase 4: Optimize LLM Context Retrieval

#### 4.1 Modify ConversationHistoryService

- [ ] **Task 4.1.1**: Update `GetHistoryAsync` to prefer ChromaDB over WhatsApp API
  
  Location: `src/BotGenerator.Core/Services/ConversationHistoryService.cs`
  
  Modify the sync logic to:
  1. First check ChromaDB for recent messages
  2. Only fetch from WhatsApp if ChromaDB is empty or significantly outdated
  3. Reduce `FullSyncMaxPages` from 50 to 10 for initial sync
  
  ```csharp
  // New configuration options
  bool useVectorStoreForHistory = configuration.GetValue("History:UseVectorStoreForHistory", true);
  int recentMessagesLimit = configuration.GetValue("History:RecentMessagesLimit", 5);
  
  // In GetHistoryAsync method:
  if (useVectorStoreForHistory && _vectorStore != null)
  {
      // Try to get recent messages from vector store first
      var recentFromVector = await _vectorStore.QueryRelevantAsync(
          phoneNumber, 
          "", 
          recentMessagesLimit, 
          cancellationToken);
      
      if (recentFromVector.Count > 0)
      {
          // Use vector store results + small recent history
          // Skip the heavy WhatsApp API sync
      }
  }
  ```

#### 4.2 Update MainConversationAgent

- [ ] **Task 4.2.1**: Modify context building to use hybrid approach
  
  Location: `src/BotGenerator.Core/Agents/MainConversationAgent.cs`
  
  Instead of querying full history:
  1. Get last 5 recent messages from local DB
  2. Query ChromaDB for relevant historical context
  3. Combine both in prompt context

---

### Phase 5: Local Testing

#### 5.1 Test ChromaDB Connection

- [ ] **Task 5.1.1**: Verify ChromaDB is running and accessible
  ```bash
  curl http://localhost:8000/api/v1/heartbeat
  ```

- [ ] **Task 5.1.2**: Test collection creation
  ```bash
  curl -X POST http://localhost:8000/api/v1/collections \
    -H "Content-Type: application/json" \
    -d '{"name": "phone-conversations", "get_or_create": true}'
  ```

#### 5.2 Test PHP Integration

- [ ] **Task 5.2.1**: Create test booking via frontend and verify ChromaDB upsert
  ```bash
  # Check ChromaDB for the booking
  curl -X POST http://localhost:8000/api/v1/collections/phone-conversations/query \
    -H "Content-Type: application/json" \
    -d '{"query_texts": ["reserva"], "where": {"type": "booking"}, "n_results": 5}'
  ```

#### 5.3 Test C# Bot Integration

- [ ] **Task 5.3.1**: Send test WhatsApp message and verify semantic search works
  - Check logs for ChromaDB query
  - Verify LLM receives relevant context

---

## Configuration Summary

### Environment Variables Required

#### For PHP (alqueriavillacarmen)
```
CHROMA_ENABLED=true
CHROMA_API_URL=http://localhost:8000
CHROMA_COLLECTION=phone-conversations
```

#### For C# (BotGenerator)
```
CHROMA_ENABLED=true
CHROMA_API_URL=http://localhost:8000
CHROMA_COLLECTION_NAME=phone-conversations
WHATSAPP_API_URL=http://localhost:8080
WHATSAPP_TOKEN=your_token
MYSQL_CONNECTION_STRING=...
```

---

## Verification Criteria

- [ ] **VC1**: ChromaDB container runs on localhost:8000
- [ ] **VC2**: Booking created in PHP is upserted to ChromaDB with correct metadata
- [ ] **VC3**: WhatsApp messages are upserted to ChromaDB (existing flow)
- [ ] **VC4**: C# bot can query ChromaDB for relevant context
- [ ] **VC5**: LLM receives meaningful semantic context from ChromaDB
- [ ] **VC6**: No more heavy WhatsApp API calls on every webhook
- [ ] **VC7**: System handles ChromaDB unavailability gracefully (fail-open)

---

## Potential Risks and Mitigations

1. **Risk**: ChromaDB container unavailable
   - **Mitigation**: Both PHP and C# should handle failures gracefully (log warning, continue without ChromaDB)

2. **Risk**: Embedding model mismatch between PHP and C#
   - **Mitigation**: Use ChromaDB's default embedding function (sentence-transformers/all-MiniLM-L6-v2) on both sides

3. **Risk**: Phone number format inconsistency
   - **Mitigation**: Normalize phone numbers to 9-digit format in both PHP and C# before storing/querying

4. **Risk**: Large document size causing performance issues
   - **Mitigation**: Keep booking documents concise, limit to ~200 characters

---

## Alternative Approaches Considered

1. **Per-phone collections**: Create separate ChromaDB collection per phone number
   - Pros: Better isolation, faster queries
   - Cons: More complex management, potential for many small collections
   - **Decision**: Use single collection with metadata filtering initially

2. **PostgreSQL with pgvector**: Use pgvector instead of ChromaDB
   - Pros: Already have PostgreSQL, better SQL integration
   - Cons: More setup, less specialized for embeddings
   - **Decision**: ChromaDB chosen for simplicity and existing infrastructure

3. **Sync bookings via C# instead of PHP**: Have C# poll for new bookings
   - Pros: Single point of integration
   - Cons: Adds complexity, delay between booking and ChromaDB sync
   - **Decision**: Direct PHP integration for immediate upsert

---

## Next Steps (After Local Testing)

1. Deploy ChromaDB to production server
2. Configure production environment variables
3. Set up monitoring for ChromaDB health
4. Consider backup strategy for ChromaDB data
5. Evaluate performance and tune TopK/batch sizes
