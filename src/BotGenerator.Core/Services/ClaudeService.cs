using Anthropic;
using BotGenerator.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;
using ChatMessage = BotGenerator.Core.Models.ChatMessage;
using MEAI = Microsoft.Extensions.AI;
using Microsoft.Extensions.AI;

namespace BotGenerator.Core.Services;

/// <summary>
/// AI service using the official Anthropic C# SDK via the IChatClient adapter.
/// Tools defined via ToolHandler methods -> AIFunctionFactory.Create(MethodInfo, target, options).
/// </summary>
public class ClaudeService : IGeminiService
{
    private readonly MEAI.IChatClient _chat;
    private readonly string _model;
    private readonly ILogger<ClaudeService> _logger;
    private readonly int _defaultMaxTokens;
    private readonly double _defaultTemperature;
    private static readonly ToolHandler _handler = new();
    private static readonly Dictionary<string, MethodInfo> _toolMethods;

    static ClaudeService()
    {
        var handlerType = typeof(ToolHandler);
        _toolMethods = new Dictionary<string, string>
        {
            ["send_message"] = "SendMessage",
            ["fetch_whatsapp_history"] = "FetchHistory",
            ["get_restaurant_info"] = "NoParams",
            ["get_rice_menu"] = "NoParams",
            ["check_rice_availability"] = "CheckRiceAvailability",
            ["check_future_booking"] = "NoParams",
            ["check_availability"] = "CheckAvailability",
            ["get_opening_hours"] = "GetOpeningHours",
            ["get_opening_hours_with_capacity"] = "GetOpeningHoursWithCapacity",
            ["check_hour_capacity"] = "CheckHourCapacity",
            ["check_day_capacity"] = "CheckDayCapacity",
            ["check_availability_for_party"] = "CheckAvailabilityForParty",
            ["get_hour_data"] = "GetHourData",
            ["get_day_status"] = "GetDayStatus",
            ["get_bookings"] = "GetBookings",
            ["create_booking"] = "CreateBooking",
            ["cancel_booking"] = "CancelBooking",
            ["modify_booking"] = "ModifyBooking",
            ["validate_booking_modification"] = "ValidateModification",
            ["edit_booking"] = "ModifyBooking",
            ["query_database"] = "QueryDatabase",
            ["reject_incoming_call"] = "NoParams",
        }.ToDictionary(kv => kv.Key, kv => handlerType.GetMethod(kv.Value, BindingFlags.Public | BindingFlags.Instance)!);
    }

    public ClaudeService(
        IAnthropicClient client,
        IConfiguration configuration,
        ILogger<ClaudeService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var apiKey = configuration["Minimax:ApiKey"]
            ?? throw new InvalidOperationException("Minimax:ApiKey must be configured");

        _model = configuration["Minimax:Model"] ?? "MiniMax-M3";
        _defaultMaxTokens = configuration.GetValue("Minimax:MaxOutputTokens", 2048);
        _defaultTemperature = configuration.GetValue("Minimax:Temperature", 0.7);

        var baseUrl = configuration["Minimax:BaseUrl"] ?? "https://api.minimax.io/anthropic/v1";
        var maxRetries = configuration.GetValue("Minimax:MaxRetries", 2);

        var ac = client as AnthropicClient ?? new AnthropicClient
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl,
            MaxRetries = maxRetries,
        };

        _chat = ac.AsIChatClient(_model);

        _logger.LogInformation(
            "ClaudeService initialized. Model: {Model}", _model);
    }

    public Task<string> GenerateAsync(
        string systemPrompt, string userMessage,
        List<ChatMessage>? history = null,
        CancellationToken cancellationToken = default)
    {
        return GenerateAsync(systemPrompt, userMessage, history,
            new GeminiGenerationConfig { MaxOutputTokens = _defaultMaxTokens, Temperature = _defaultTemperature },
            cancellationToken);
    }

    public async Task<string> GenerateAsync(
        string systemPrompt, string userMessage,
        List<ChatMessage>? history,
        GeminiGenerationConfig config,
        CancellationToken cancellationToken)
    {
        var msgs = ToChatMessages(systemPrompt, history, userMessage);
        var opts = new MEAI.ChatOptions
        {
            MaxOutputTokens = config.MaxOutputTokens,
            Temperature = (float)config.Temperature,
        };
        var resp = await _chat.GetResponseAsync(msgs, opts, cancellationToken);
        return resp.Messages?.LastOrDefault()?.Contents?.OfType<MEAI.TextContent>()?.FirstOrDefault()?.Text ?? "";
    }

    public async Task<AnthropicResponse> GenerateWithToolsAsync(
        string systemPrompt, string userMessage,
        List<ChatMessage>? history,
        List<ToolDefinition> tools,
        ToolChoiceConfig? toolChoice = null,
        GeminiGenerationConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        var msgs = ToChatMessages(systemPrompt, history, userMessage);
        var opts = new MEAI.ChatOptions
        {
            MaxOutputTokens = config?.MaxOutputTokens ?? 1024,
            Temperature = (float)(config?.Temperature ?? 0.1),
            Tools = BuildTools(tools),
            ToolMode = ToToolMode(toolChoice),
        };
        var resp = await _chat.GetResponseAsync(msgs, opts, cancellationToken);
        return ToResponse(resp);
    }

    public async Task<AnthropicResponse> ContinueWithToolResultAsync(
        string systemPrompt, List<object> messages,
        List<ToolDefinition> tools,
        ToolChoiceConfig? toolChoice = null,
        GeminiGenerationConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        var msgs = DeserializeMessages(systemPrompt, messages);
        var opts = new MEAI.ChatOptions
        {
            MaxOutputTokens = config?.MaxOutputTokens ?? 1024,
            Temperature = (float)(config?.Temperature ?? 0.1),
            Tools = BuildTools(tools),
            ToolMode = ToToolMode(toolChoice),
        };
        var resp = await _chat.GetResponseAsync(msgs, opts, cancellationToken);
        return ToResponse(resp);
    }

    public Task<int> CountTokensAsync(string text, CancellationToken ct = default)
        => Task.FromResult(text.Length / 4);

    // ========================================================================
    // Private
    // ========================================================================

    private static List<MEAI.ChatMessage> ToChatMessages(
        string systemPrompt, List<ChatMessage>? history, string userMessage)
    {
        var msgs = new List<MEAI.ChatMessage> { new(MEAI.ChatRole.System, systemPrompt) };
        if (history != null)
            foreach (var h in history)
                msgs.Add(new MEAI.ChatMessage(
                    h.Role == "assistant" ? MEAI.ChatRole.Assistant : MEAI.ChatRole.User, h.Content));
        msgs.Add(new MEAI.ChatMessage(MEAI.ChatRole.User, userMessage));
        return msgs;
    }

    private List<MEAI.AITool> BuildTools(List<ToolDefinition> tools)
    {
        var result = new List<MEAI.AITool>();
        foreach (var t in tools)
        {
            if (!_toolMethods.TryGetValue(t.Name, out var methodInfo)) continue;
            var func = MEAI.AIFunctionFactory.Create(methodInfo, _handler,
                new MEAI.AIFunctionFactoryOptions { Name = t.Name, Description = t.Description });
            result.Add(func);
        }
        return result;
    }

    private static MEAI.ChatToolMode? ToToolMode(ToolChoiceConfig? tc)
    {
        if (tc == null) return null;
        return tc.Type switch
        {
            "any" => MEAI.ChatToolMode.RequireAny,
            "tool" when !string.IsNullOrEmpty(tc.ToolName) => new MEAI.RequiredChatToolMode(tc.ToolName),
            _ => MEAI.ChatToolMode.Auto,
        };
    }

    private static AnthropicResponse ToResponse(MEAI.ChatResponse resp)
    {
        var blocks = new List<ContentBlock>();
        var msg = resp.Messages?.LastOrDefault();
        if (msg != null)
        {
            foreach (var c in msg.Contents)
            {
                if (c is MEAI.TextContent tc)
                    blocks.Add(new TextBlock(tc.Text ?? ""));
                else if (c is MEAI.FunctionCallContent fcc)
                {
                    var args = fcc.Arguments != null
                        ? JsonSerializer.SerializeToElement(fcc.Arguments)
                        : new JsonElement();
                    blocks.Add(new ToolUseBlock(fcc.CallId ?? "", fcc.Name ?? "", args));
                }
            }
        }

        var stopReason = "end_turn";
        if (resp.FinishReason == MEAI.ChatFinishReason.ToolCalls) stopReason = "tool_use";
        else if (resp.FinishReason == MEAI.ChatFinishReason.Length) stopReason = "max_tokens";

        return new AnthropicResponse { Content = blocks, StopReason = stopReason };
    }

    private static List<MEAI.ChatMessage> DeserializeMessages(string systemPrompt, List<object> raw)
    {
        var msgs = new List<MEAI.ChatMessage> { new(MEAI.ChatRole.System, systemPrompt) };
        foreach (var r in raw)
        {
            if (r is not Dictionary<string, object> d) continue;
            var role = (d.GetValueOrDefault("role") as string) == "assistant" ? MEAI.ChatRole.Assistant : MEAI.ChatRole.User;
            var content = d.GetValueOrDefault("content");

            if (content is List<object> list)
            {
                var contents = new List<MEAI.AIContent>();
                foreach (var rb in list)
                {
                    if (rb is not Dictionary<string, object> bd) continue;
                    switch (bd.GetValueOrDefault("type") as string)
                    {
                        case "tool_use":
                            contents.Add(new MEAI.FunctionCallContent(
                                bd.GetValueOrDefault("id") as string ?? "",
                                bd.GetValueOrDefault("name") as string ?? "",
                                null));
                            break;
                        case "tool_result":
                            msgs.Add(new MEAI.ChatMessage(MEAI.ChatRole.Tool,
                                [new MEAI.FunctionResultContent(
                                    bd.GetValueOrDefault("tool_use_id") as string ?? "",
                                    bd.GetValueOrDefault("content") as string ?? "")]));
                            break;
                        case "text":
                            contents.Add(new MEAI.TextContent(bd.GetValueOrDefault("text") as string ?? ""));
                            break;
                    }
                }
                if (contents.Count > 0) msgs.Add(new MEAI.ChatMessage(role, contents));
            }
            else if (content is string s)
                msgs.Add(new MEAI.ChatMessage(role, s));
        }
        return msgs;
    }
}

public class ToolHandler
{
    public string SendMessage(string message) => JsonSerializer.Serialize(new { message });
    public string FetchHistory(int limit = 30) => JsonSerializer.Serialize(new { limit });
    public string NoParams() => "{}";
    public string CheckRiceAvailability(string rice_type) => JsonSerializer.Serialize(new { rice_type });
    public string CheckAvailability(string date, string? time = null, int? people = null)
        => JsonSerializer.Serialize(new { date, time, people });
    public string GetOpeningHours(string date) => JsonSerializer.Serialize(new { date });
    public string GetOpeningHoursWithCapacity(string date, int? party_size = null)
        => JsonSerializer.Serialize(new { date, party_size });
    public string CheckHourCapacity(string date) => JsonSerializer.Serialize(new { date });
    public string CheckDayCapacity(string date) => JsonSerializer.Serialize(new { date });
    public string CheckAvailabilityForParty(string date, int party_size)
        => JsonSerializer.Serialize(new { date, party_size });
    public string GetHourData(string date) => JsonSerializer.Serialize(new { date });
    public string GetDayStatus(string date) => JsonSerializer.Serialize(new { date });
    public string GetBookings(string? phone = null) => JsonSerializer.Serialize(new { phone });
    public string CreateBooking(string date, string time, int? people = null, string? rice_type = null,
        int? rice_servings = null, string? name = null, int? high_chairs = null,
        int? baby_strollers = null, bool confirmed = false)
        => JsonSerializer.Serialize(new { date, time, people, rice_type, rice_servings, name, high_chairs, baby_strollers, confirmed });
    public string CancelBooking(string booking_id, bool confirmed = false)
        => JsonSerializer.Serialize(new { booking_id, confirmed });
    public string ModifyBooking(string booking_id, string? date = null, string? time = null,
        int? people = null, string? rice_type = null, int? rice_servings = null,
        int? high_chairs = null, int? baby_strollers = null,
        bool? clear_rice = null, bool confirmed = false)
        => JsonSerializer.Serialize(new { booking_id, date, time, people, rice_type, rice_servings, high_chairs, baby_strollers, clear_rice, confirmed });
    public string ValidateModification(string booking_id, string? new_date = null,
        string? new_time = null, int? new_people = null)
        => JsonSerializer.Serialize(new { booking_id, new_date, new_time, new_people });
    public string QueryDatabase(string query) => JsonSerializer.Serialize(new { query });
}
