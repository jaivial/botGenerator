using System.Text.Json;
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using Microsoft.Extensions.Logging;

namespace BotGenerator.Core.Pipeline;

/// <summary>
/// Node 1: AI-powered context analysis.
/// Reads conversation history with timestamps, classifies intent,
/// extracts booking data. Returns structured ContextAnalysisResult.
/// Replaces all regex-based intent detection.
/// </summary>
public class ContextAnalyzerNode : IPipelineNode<PipelineContext, ContextAnalysisResult>
{
    private readonly IGeminiService _ai;
    private readonly ILogger<ContextAnalyzerNode> _logger;

    private const string SystemPrompt = @"You are a message classifier for a WhatsApp restaurant bot (Alquería Villa Carmen, Valencia).
Given the conversation history with timestamps and the current message, classify the intent and extract any structured data.

## CURRENT STATE
Today: {todayES} ({todayFormatted})

## USER'S MESSAGE
PushName: {pushName}
Message: ""{messageText}""

## PENDING BOOKING STATE
{pendingBookingState}

## EXISTING BOOKINGS
{existingBookings}

## CONVERSATION HISTORY (with timestamps and session boundaries)
{formattedHistory}

---

## INTENT CLASSIFICATION RULES

### TIMING RULES (HIGHEST PRIORITY):
1. If the last bot message was >1 hour ago and the user sends a short reply (""ok"", ""gracias"", ""vale"", ""perfecto"", ""genial"", ""bien"", ""entendido"", an emoji) → ACKNOWLEDGMENT or BROADCAST_REPLY
2. If the history shows a promotional/informational message from the bot followed by a short thanks → BROADCAST_REPLY
3. If there is an active conversation (messages within minutes) and a pending booking exists → the message is likely CONTINUE_BOOKING

### PENDING BOOKING RULES:
4. If a pending booking exists and user message provides missing data (date, time, people count, rice type, rice servings, tronas, carritos) → CONTINUE_BOOKING
5. If a pending booking summary has been shown (SummaryShown=true) and user says ""si"", ""ok"", ""confirmo"", ""vale"", ""perfecto"" → CONFIRM_BOOKING
6. If a pending booking summary has been shown and user says ""no"", ""cancelar"", ""mejor no"" → DECLINE_BOOKING

### NEW INTENT RULES:
7. ""reservar"", ""mesa"", specific date/time/people → NEW_BOOKING
8. ""hoy"", ""esta tarde"", ""esta noche"", ""ahora mismo"" + booking intent → SAME_DAY_BOOKING
9. ""boda"", ""cumpleaños"", ""comunión"", ""evento"", ""celebración especial"" + large group → EVENT_INQUIRY
10. ""tengo reserva?"", ""mis reservas"", ""he reservado?"" → INFO_REQUEST

### EXISTING BOOKING RULES:
11. ""mi reserva"", ""la reserva"" + ""cambiar"", ""modificar"", ""añadir"", ""quitar"" + user has existing bookings → MODIFICATION
12. ""cancelar"", ""anular"", ""eliminar reserva"" + user has existing bookings → CANCELLATION

### GENERAL RULES:
13. ""hola"", ""buenos días"", ""buenas tardes"" with no booking context → GREETING
14. Questions about menu, parking, hours, directions, ingredients → OFF_TOPIC
15. Short ""gracias"", ""ok"", ""vale"", ""perfecto"", ""genial"", ""bien"", ""entendido"", single emoji with NO pending booking and NO booking context → ACKNOWLEDGMENT
16. Short thanks/ok after a promotional message → BROADCAST_REPLY

## DATA EXTRACTION RULES

Extract booking data ONLY when intent involves booking (NEW_BOOKING, CONTINUE_BOOKING, CONFIRM_BOOKING):
- Date: Convert relative dates to dd/MM/yyyy. ""mañana"" = tomorrow, ""el sábado"" = next Saturday, ""pasado mañana"" = day after tomorrow
- Time: Extract as HH:mm. ""a las tres"" → ""15:00"", ""a las dos y media"" → ""14:30""
- People: Extract number. ""somos 4"" → 4, ""para 6 personas"" → 6
- Rice: Extract type name if mentioned. ""no queremos arroz"" or ""sin arroz"" → RiceDeclined=true, ExtractedRiceType=null
- RiceServings: Only if explicitly stated. ""2 raciones"" → 2
- HighChairs/BabyStrollers: Only if explicitly mentioned. ""2 tronas"" → 2, ""necesitamos tronas"" → -1 (needs count)

## OUTPUT FORMAT
Respond with ONLY a JSON object on ONE line, no other text. Keep reasoning under 15 words:
{
  ""intent"": ""<one of: Acknowledgment, OffTopic, InfoRequest, NewBooking, ContinueBooking, ConfirmBooking, DeclineBooking, Modification, Cancellation, SameDayBooking, EventInquiry, BroadcastReply, Greeting>"",
  ""confidence"": 0.95,
  ""reasoning"": ""<15 words max>"",
  ""extractedDate"": null,
  ""extractedTime"": null,
  ""extractedPeople"": null,
  ""extractedRiceType"": null,
  ""extractedRiceServings"": null,
  ""extractedHighChairs"": null,
  ""extractedBabyStrollers"": null,
  ""riceDeclined"": false,
  ""userGoal"": null,
  ""offTopicSubject"": null
}";

    public ContextAnalyzerNode(IGeminiService ai, ILogger<ContextAnalyzerNode> logger)
    {
        _ai = ai;
        _logger = logger;
    }

    public async Task<ContextAnalysisResult> ProcessAsync(PipelineContext context, CancellationToken ct)
    {
        var prompt = SystemPrompt
            .Replace("{todayES}", context.TodayES)
            .Replace("{todayFormatted}", context.TodayFormatted)
            .Replace("{pushName}", context.PushName)
            .Replace("{messageText}", context.Message.MessageText)
            .Replace("{pendingBookingState}", FormatPendingBooking(context.PendingBooking))
            .Replace("{existingBookings}", FormatExistingBookings(context.ExistingBookings))
            .Replace("{formattedHistory}", context.FormattedHistory);

        _logger.LogInformation(
            "ContextAnalyzer processing message from {Phone}: '{Message}'",
            context.Message.SenderNumber,
            context.Message.MessageText);

        var config = new GeminiGenerationConfig
        {
            Temperature = 0.1,
            MaxOutputTokens = 1024
        };

        var response = await _ai.GenerateAsync(prompt, context.Message.MessageText, null, config, ct);

        _logger.LogDebug("ContextAnalyzer raw response: {Response}", response);

        return ParseResponse(response);
    }

    private ContextAnalysisResult ParseResponse(string response)
    {
        try
        {
            var json = response.Trim();

            // Strip markdown code blocks
            if (json.StartsWith("```"))
            {
                var start = json.IndexOf('{');
                var end = json.LastIndexOf('}');
                if (start >= 0 && end > start)
                    json = json[start..(end + 1)];
            }

            // Extract just the JSON object if there's surrounding text
            var jsonStart = json.IndexOf('{');
            var jsonEnd = json.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
                json = json[jsonStart..(jsonEnd + 1)];

            // Try to parse as-is first
            JsonDocument? doc = null;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch (JsonException)
            {
                // JSON may be truncated — try to recover the intent field
                var intentMatch = System.Text.RegularExpressions.Regex.Match(
                    json, @"""intent""\s*:\s*""(\w+)""");
                if (intentMatch.Success)
                {
                    var intentStr = intentMatch.Groups[1].Value;
                    var intent = Enum.TryParse<PipelineIntent>(intentStr, out var p)
                        ? p : PipelineIntent.Acknowledgment;

                    _logger.LogWarning(
                        "Recovered truncated JSON, intent={Intent}", intent);

                    // Try to extract remaining fields from partial JSON
                    return new ContextAnalysisResult
                    {
                        Intent = intent,
                        Confidence = 0.6f,
                        Reasoning = "Recovered from truncated response",
                        ExtractedDate = ExtractStringField(json, "extractedDate"),
                        ExtractedTime = ExtractStringField(json, "extractedTime"),
                        ExtractedPeople = ExtractIntField(json, "extractedPeople"),
                        ExtractedRiceType = ExtractStringField(json, "extractedRiceType"),
                        ExtractedRiceServings = ExtractIntField(json, "extractedRiceServings"),
                        ExtractedHighChairs = ExtractIntField(json, "extractedHighChairs"),
                        ExtractedBabyStrollers = ExtractIntField(json, "extractedBabyStrollers"),
                        RiceDeclined = json.Contains(@"""riceDeclined"": true"),
                        UserGoal = ExtractStringField(json, "userGoal"),
                        OffTopicSubject = ExtractStringField(json, "offTopicSubject")
                    };
                }

                throw;
            }

            var root = doc.RootElement;
            var intentStr2 = root.TryGetProperty("intent", out var intentProp)
                ? intentProp.GetString() ?? "Acknowledgment"
                : "Acknowledgment";

            var intent2 = Enum.TryParse<PipelineIntent>(intentStr2, out var parsed)
                ? parsed
                : PipelineIntent.Acknowledgment;

            return new ContextAnalysisResult
            {
                Intent = intent2,
                Confidence = root.TryGetProperty("confidence", out var conf) ? (float)conf.GetDouble() : 0.5f,
                Reasoning = root.TryGetProperty("reasoning", out var reason) ? reason.GetString() ?? "" : "",
                ExtractedDate = TryGetString(root, "extractedDate"),
                ExtractedTime = TryGetString(root, "extractedTime"),
                ExtractedPeople = TryGetInt(root, "extractedPeople"),
                ExtractedRiceType = TryGetString(root, "extractedRiceType"),
                ExtractedRiceServings = TryGetInt(root, "extractedRiceServings"),
                ExtractedHighChairs = TryGetInt(root, "extractedHighChairs"),
                ExtractedBabyStrollers = TryGetInt(root, "extractedBabyStrollers"),
                RiceDeclined = TryGetBool(root, "riceDeclined"),
                UserGoal = TryGetString(root, "userGoal"),
                OffTopicSubject = TryGetString(root, "offTopicSubject")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse ContextAnalyzer response: {Response}", response);
            return new ContextAnalysisResult
            {
                Intent = PipelineIntent.Acknowledgment,
                Confidence = 0.1f,
                Reasoning = $"Parse error: {ex.Message}"
            };
        }
    }

    private static string? ExtractStringField(string json, string fieldName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            json, $@"""{fieldName}""\s*:\s*""([^""]*?)""");
        var value = match.Success ? match.Groups[1].Value : null;
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static int? ExtractIntField(string json, string fieldName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            json, $@"""{fieldName}""\s*:\s*(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var val))
            return val;
        return null;
    }

    private static string? TryGetString(JsonElement root, string prop) =>
        root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static int? TryGetInt(JsonElement root, string prop) =>
        root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.Number
            ? el.GetInt32()
            : null;

    private static bool TryGetBool(JsonElement root, string prop) =>
        root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.True;

    private static string FormatPendingBooking(BookingData? pending)
    {
        if (pending == null) return "No pending booking.";

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(pending.Date)) parts.Add($"Date: {pending.Date}");
        if (!string.IsNullOrEmpty(pending.Time)) parts.Add($"Time: {pending.Time}");
        if (pending.People > 0) parts.Add($"People: {pending.People}");
        if (!string.IsNullOrEmpty(pending.ArrozType)) parts.Add($"Rice: {pending.ArrozType} ({pending.ArrozServings} servings)");
        if (pending.HighChairs != 0) parts.Add($"HighChairs: {pending.HighChairs}");
        if (pending.BabyStrollers != 0) parts.Add($"Strollers: {pending.BabyStrollers}");
        parts.Add($"SummaryShown: {pending.SummaryShown}");

        return $"PENDING BOOKING: {string.Join(", ", parts)}";
    }

    private static string FormatExistingBookings(List<BookingRecord> bookings)
    {
        if (bookings.Count == 0) return "No existing bookings.";

        return string.Join("\n", bookings.Select(b =>
        {
            var dayName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
                b.ReservationDate.ToString("dddd"));
            var rice = string.IsNullOrEmpty(b.ArrozType) ? "Sin arroz" : $"{b.ArrozType} ({b.ArrozServings} raciones)";
            return $"- {dayName} {b.DateFormatted} a las {b.TimeFormatted}: {b.PartySize} personas | {rice}";
        }));
    }
}
