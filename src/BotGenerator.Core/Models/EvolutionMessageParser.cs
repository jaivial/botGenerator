using System.Text.Json;

namespace BotGenerator.Core.Models;

/// <summary>
/// Extracts BotGenerator's message model from Evolution API v2 webhook and history payloads.
/// </summary>
public static class EvolutionMessageParser
{
    public static bool TryParseInboundMessage(JsonElement data, out WhatsAppMessage message)
    {
        message = new WhatsAppMessage();

        if (data.ValueKind != JsonValueKind.Object ||
            !TryGetProperty(data, "key", out var key) ||
            key.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var remoteJid = GetString(key, "remoteJid");
        var messageId = GetString(key, "id");
        if (string.IsNullOrWhiteSpace(remoteJid) ||
            string.IsNullOrWhiteSpace(messageId) ||
            messageId.Length > 512 ||
            IsGroupOrStatusJid(remoteJid))
        {
            return false;
        }

        var at = remoteJid.IndexOf('@');
        var senderNumber = at >= 0 ? remoteJid[..at] : remoteJid;
        if (string.IsNullOrWhiteSpace(senderNumber) || !senderNumber.All(char.IsDigit))
            return false;

        var fromMe = GetBool(key, "fromMe") ?? false;
        var messageElement = TryGetProperty(data, "message", out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : default;
        var extracted = ExtractMessage(messageElement);
        var declaredType = GetString(data, "messageType") ?? extracted.Type;

        message = new WhatsAppMessage
        {
            SenderNumber = senderNumber,
            MessageText = extracted.Text,
            MessageType = declaredType ?? "unknown",
            PushName = GetString(data, "pushName") ?? "Cliente",
            FromMe = fromMe,
            Timestamp = GetLong(data, "messageTimestamp") ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            MessageId = messageId,
            IsButtonResponse = extracted.IsInteractive,
            ButtonId = extracted.ButtonId,
            ButtonText = extracted.IsInteractive ? extracted.Text : null,
            IsMediaMessage = IsMediaMessage(declaredType, messageElement)
        };

        return true;
    }

    public static bool IsGroupOrStatusJid(string remoteJid) =>
        remoteJid.EndsWith("@g.us", StringComparison.OrdinalIgnoreCase) ||
        remoteJid.EndsWith("@lid", StringComparison.OrdinalIgnoreCase) ||
        remoteJid.EndsWith("@broadcast", StringComparison.OrdinalIgnoreCase) ||
        remoteJid.EndsWith("@newsletter", StringComparison.OrdinalIgnoreCase);

    public static string ExtractText(JsonElement message) => ExtractMessage(message).Text;

    private static ExtractedMessage ExtractMessage(JsonElement message)
    {
        if (message.ValueKind != JsonValueKind.Object)
            return default;

        var conversation = GetString(message, "conversation");
        if (!string.IsNullOrWhiteSpace(conversation))
            return new ExtractedMessage(conversation, "conversation", false, null);

        if (TryGetProperty(message, "extendedTextMessage", out var extendedText) &&
            extendedText.ValueKind == JsonValueKind.Object)
        {
            var text = GetString(extendedText, "text");
            if (!string.IsNullOrWhiteSpace(text))
                return new ExtractedMessage(text, "extendedTextMessage", false, null);
        }

        if (TryGetProperty(message, "buttonsResponseMessage", out var buttonResponse) &&
            buttonResponse.ValueKind == JsonValueKind.Object)
        {
            var buttonId = GetString(buttonResponse, "selectedButtonId");
            var text = GetString(buttonResponse, "selectedDisplayText") ?? buttonId;
            return new ExtractedMessage(text ?? string.Empty, "buttonsResponseMessage", true, buttonId);
        }

        if (TryGetProperty(message, "listResponseMessage", out var listResponse) &&
            listResponse.ValueKind == JsonValueKind.Object)
        {
            var rowId = TryGetProperty(listResponse, "singleSelectReply", out var singleSelect) &&
                        singleSelect.ValueKind == JsonValueKind.Object
                ? GetString(singleSelect, "selectedRowId")
                : null;
            var text = GetString(listResponse, "title") ?? GetString(listResponse, "description") ?? rowId;
            return new ExtractedMessage(text ?? string.Empty, "listResponseMessage", true, rowId);
        }

        if (TryGetProperty(message, "interactiveResponseMessage", out var interactiveResponse) &&
            interactiveResponse.ValueKind == JsonValueKind.Object &&
            TryGetProperty(interactiveResponse, "nativeFlowResponseMessage", out var nativeFlow) &&
            nativeFlow.ValueKind == JsonValueKind.Object)
        {
            var paramsJson = GetString(nativeFlow, "paramsJson");
            if (!string.IsNullOrWhiteSpace(paramsJson) && paramsJson.Length <= 4096)
            {
                try
                {
                    using var document = JsonDocument.Parse(paramsJson);
                    var response = document.RootElement;
                    var buttonId = GetString(response, "id") ?? GetString(response, "selectedId");
                    var text = GetString(response, "title") ?? GetString(response, "displayText") ?? buttonId;
                    return new ExtractedMessage(text ?? string.Empty, "interactiveResponseMessage", true, buttonId);
                }
                catch (JsonException)
                {
                    // Ignore malformed interactive metadata; it is not user text.
                }
            }
        }

        return default;
    }

    private static bool IsMediaMessage(string? declaredType, JsonElement message) =>
        declaredType is "imageMessage" or "audioMessage" or "videoMessage" or "documentMessage" or "stickerMessage" or "locationMessage" ||
        HasProperty(message, "imageMessage") ||
        HasProperty(message, "audioMessage") ||
        HasProperty(message, "videoMessage") ||
        HasProperty(message, "documentMessage") ||
        HasProperty(message, "stickerMessage") ||
        HasProperty(message, "locationMessage");

    private static bool HasProperty(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out _);

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null
        };
    }

    private static bool? GetBool(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static long? GetLong(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var numeric))
            return numeric;

        return value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var text)
            ? text
            : null;
    }

    private readonly record struct ExtractedMessage(string Text, string? Type, bool IsInteractive, string? ButtonId);
}
