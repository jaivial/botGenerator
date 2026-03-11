using System.Text.RegularExpressions;

namespace BotGenerator.Core.Services;

/// <summary>
/// Validates extracted booking modification fields
/// </summary>
public interface IFieldValidatorService
{
    /// <summary>
    /// Validates a date string
    /// </summary>
    (bool IsValid, DateTime? Value, string? Error) ValidateDate(string dateStr);

    /// <summary>
    /// Validates a time string
    /// </summary>
    (bool IsValid, TimeSpan? Value, string? Error) ValidateTime(string timeStr);

    /// <summary>
    /// Validates party size
    /// </summary>
    (bool IsValid, int? Value, string? Error) ValidatePartySize(int? partySize);

    /// <summary>
    /// Validates rice servings
    /// </summary>
    (bool IsValid, int? Value, string? Error) ValidateRiceServings(int? servings);

    /// <summary>
    /// Validates tronas (high chairs)
    /// </summary>
    (bool IsValid, int? Value, string? Error) ValidateTronas(int? tronas);

    /// <summary>
    /// Validates carritos (baby carriages)
    /// </summary>
    (bool IsValid, int? Value, string? Error) ValidateCarritos(int? carritos);

    /// <summary>
    /// Validates user goal
    /// </summary>
    (bool IsValid, string? Error) ValidateUserGoal(string? goal);
}

public class FieldValidatorService : IFieldValidatorService
{
    private readonly ILogger<FieldValidatorService> _logger;
    private readonly IOpeningHoursService _openingHoursService;

    public FieldValidatorService(
        ILogger<FieldValidatorService> logger,
        IOpeningHoursService openingHoursService)
    {
        _logger = logger;
        _openingHoursService = openingHoursService;
    }

    public (bool IsValid, DateTime? Value, string? Error) ValidateDate(string dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return (false, null, "Date is empty");

        // Try ISO format first (YYYY-MM-DD)
        if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", null, 
            System.Globalization.DateTimeStyles.None, out var date))
        {
            // Check if date is in the past
            if (date.Date < DateTime.Today)
            {
                _logger.LogWarning("Date {Date} is in the past", dateStr);
                return (false, date, "Date is in the past");
            }

            // Check if date is too far in the future (e.g., > 1 year)
            if (date.Date > DateTime.Today.AddYears(1))
            {
                _logger.LogWarning("Date {Date} is too far in the future", dateStr);
                return (false, date, "Date is more than 1 year in the future");
            }

            return (true, date, null);
        }

        _logger.LogWarning("Failed to parse date: {DateStr}", dateStr);
        return (false, null, $"Invalid date format: {dateStr}");
    }

    public (bool IsValid, TimeSpan? Value, string? Error) ValidateTime(string timeStr)
    {
        if (string.IsNullOrWhiteSpace(timeStr))
            return (false, null, "Time is empty");

        // Try HH:MM format
        if (TimeSpan.TryParseExact(timeStr, @"hh\:mm", null, out var time))
        {
            // Validate against restaurant hours
            // Lunch: 13:00-16:00, Dinner: 20:00-23:00
            var hour = time.Hours;
            var isLunch = hour >= 13 && hour < 16;
            var isDinner = hour >= 20 && hour < 23;

            if (!isLunch && !isDinner)
            {
                _logger.LogWarning("Time {Time} is outside restaurant hours", timeStr);
                // Still accept it, but log warning - let availability checker handle it
            }

            return (true, time, null);
        }

        _logger.LogWarning("Failed to parse time: {TimeStr}", timeStr);
        return (false, null, $"Invalid time format: {timeStr}");
    }

    public (bool IsValid, int? Value, string? Error) ValidatePartySize(int? partySize)
    {
        if (!partySize.HasValue)
            return (false, null, "Party size is null");

        if (partySize < 1)
            return (false, partySize, "Party size must be at least 1");

        if (partySize > 50)
            return (false, partySize, "Party size cannot exceed 50 (special handling required)");

        // Flag large groups for special handling
        if (partySize > 15)
            _logger.LogInformation("Large party size: {PartySize}", partySize);

        return (true, partySize, null);
    }

    public (bool IsValid, int? Value, string? Error) ValidateRiceServings(int? servings)
    {
        if (!servings.HasValue)
            return (false, null, "Rice servings is null");

        if (servings < 2)
            return (false, servings, "Rice servings must be at least 2");

        if (servings > 20)
            return (false, servings, "Rice servings cannot exceed 20");

        return (true, servings, null);
    }

    public (bool IsValid, int? Value, string? Error) ValidateTronas(int? tronas)
    {
        if (!tronas.HasValue)
            return (false, null, "Tronas is null");

        if (tronas < 0)
            return (false, tronas, "Tronas cannot be negative");

        if (tronas > 10)
            return (false, tronas, "Tronas cannot exceed 10");

        return (true, tronas, null);
    }

    public (bool IsValid, int? Value, string? Error) ValidateCarritos(int? carritos)
    {
        if (!carritos.HasValue)
            return (false, null, "Carritos is null");

        if (carritos < 0)
            return (false, carritos, "Carritos cannot be negative");

        if (carritos > 10)
            return (false, carritos, "Carritos cannot exceed 10");

        return (true, carritos, null);
    }

    public (bool IsValid, string? Error) ValidateUserGoal(string? goal)
    {
        if (string.IsNullOrWhiteSpace(goal))
            return (true, null); // null goal is valid (means "unclear")

        var validGoals = new[]
        {
            "change_date", "change_time", "change_both", "change_party_size",
            "add_rice", "cancel", "unclear"
        };

        if (!validGoals.Contains(goal))
        {
            _logger.LogWarning("Invalid user goal: {Goal}", goal);
            return (false, $"Invalid user goal: {goal}");
        }

        return (true, null);
    }
}
