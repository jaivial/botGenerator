using System.Text.Json;
using System.Text.Json.Serialization;

namespace BotGenerator.Core.Models;

/// <summary>
/// Response model for AI-based natural language extraction
/// </summary>
public class AiExtractionResponse
{
    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("time")]
    public string? Time { get; set; }

    [JsonPropertyName("party_size")]
    [JsonConverter(typeof(NullableIntConverter))]
    public int? PartySize { get; set; }

    [JsonPropertyName("rice_type")]
    public string? RiceType { get; set; }

    [JsonPropertyName("rice_servings")]
    [JsonConverter(typeof(NullableIntConverter))]
    public int? RiceServings { get; set; }

    [JsonPropertyName("tronas")]
    [JsonConverter(typeof(NullableIntConverter))]
    public int? Tronas { get; set; }

    [JsonPropertyName("carritos")]
    [JsonConverter(typeof(NullableIntConverter))]
    public int? Carritos { get; set; }

    [JsonPropertyName("is_correction")]
    public bool IsCorrection { get; set; }

    [JsonPropertyName("user_goal")]
    public string? UserGoal { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; set; }

    /// <summary>
    /// Validates the extracted fields
    /// </summary>
    public bool IsValid()
    {
        // Validate date format if present
        if (!string.IsNullOrEmpty(Date) && !IsValidDate(Date))
            return false;

        // Validate time format if present
        if (!string.IsNullOrEmpty(Time) && !IsValidTime(Time))
            return false;

        // Validate numeric ranges
        if (PartySize.HasValue && (PartySize < 1 || PartySize > 50))
            return false;

        if (RiceServings.HasValue && (RiceServings < 1 || RiceServings > 20))
            return false;

        if (Tronas.HasValue && (Tronas < 0 || Tronas > 10))
            return false;

        if (Carritos.HasValue && (Carritos < 0 || Carritos > 10))
            return false;

        // Validate confidence range
        if (Confidence < 0.0 || Confidence > 1.0)
            return false;

        // Validate user goal
        if (!string.IsNullOrEmpty(UserGoal) && !IsValidUserGoal(UserGoal))
            return false;

        return true;
    }

    private static bool IsValidDate(string date)
    {
        return DateTime.TryParseExact(date, "yyyy-MM-dd", null, 
            System.Globalization.DateTimeStyles.None, out _);
    }

    private static bool IsValidTime(string time)
    {
        return TimeSpan.TryParseExact(time, @"hh\:mm", null, out _);
    }

    private static bool IsValidUserGoal(string goal)
    {
        var validGoals = new[]
        {
            "change_date", "change_time", "change_both", "change_party_size",
            "add_rice", "cancel", "unclear"
        };
        return validGoals.Contains(goal);
    }

    /// <summary>
    /// Converts to dictionary format for accumulator
    /// </summary>
    public Dictionary<string, object> ToAccumulatorDictionary()
    {
        var dict = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(Date))
            dict["date"] = Date;

        if (!string.IsNullOrEmpty(Time))
            dict["time"] = Time;

        if (PartySize.HasValue)
            dict["party_size"] = PartySize.Value;

        if (!string.IsNullOrEmpty(RiceType))
            dict["rice_type"] = RiceType;

        if (RiceServings.HasValue)
            dict["rice_servings"] = RiceServings.Value;

        if (Tronas.HasValue)
            dict["tronas"] = Tronas.Value;

        if (Carritos.HasValue)
            dict["carritos"] = Carritos.Value;

        return dict;
    }
}

/// <summary>
/// Custom JSON converter for nullable integers that handles both int and null values
/// </summary>
public class NullableIntConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetInt32();

        // Try to parse string as int
        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            if (int.TryParse(stringValue, out var intValue))
                return intValue;
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteNumberValue(value.Value);
        else
            writer.WriteNullValue();
    }
}
