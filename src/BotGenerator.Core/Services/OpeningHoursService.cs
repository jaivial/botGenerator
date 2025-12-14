using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace BotGenerator.Core.Services;

/// <summary>
/// MySQL-backed implementation of IOpeningHoursService.
/// Queries the openinghours table for available time slots.
/// </summary>
public class OpeningHoursService : IOpeningHoursService
{
    private readonly string _connectionString;
    private readonly ILogger<OpeningHoursService> _logger;

    // Default slots when no database entry exists
    private static readonly List<string> DefaultSlots = new() { "13:30", "14:00", "15:00", "15:30" };

    // Lunch service: 13:30 - 18:00
    private static readonly TimeSpan LunchOpen = new(13, 30, 0);
    private static readonly TimeSpan LunchClose = new(18, 0, 0);

    // Dinner service: 20:30 - 23:30
    private static readonly TimeSpan DinnerOpen = new(20, 30, 0);
    private static readonly TimeSpan DinnerClose = new(23, 30, 0);

    public OpeningHoursService(IConfiguration configuration, ILogger<OpeningHoursService> logger)
    {
        _connectionString = configuration["MySQL:ConnectionString"]
            ?? throw new InvalidOperationException("MySQL:ConnectionString not configured");
        _logger = logger;
    }

    public async Task<OpeningHoursInfo> GetOpeningHoursAsync(DateTime date, CancellationToken ct = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var dbDate = date.ToString("yyyy-MM-dd");

        var hoursJson = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT hoursarray FROM openinghours WHERE dateselected = @Date LIMIT 1",
            new { Date = dbDate },
            cancellationToken: ct));

        List<string> slots;
        bool isFromDatabase;

        if (string.IsNullOrWhiteSpace(hoursJson))
        {
            slots = DefaultSlots;
            isFromDatabase = false;
            _logger.LogDebug("No opening hours found for {Date}, using defaults", dbDate);
        }
        else
        {
            try
            {
                slots = JsonSerializer.Deserialize<List<string>>(hoursJson) ?? DefaultSlots;
                isFromDatabase = true;
                _logger.LogDebug("Found {Count} time slots for {Date}", slots.Count, dbDate);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid openinghours.hoursarray JSON for {Date}", dbDate);
                slots = DefaultSlots;
                isFromDatabase = false;
            }
        }

        // Sort slots chronologically
        slots.Sort(StringComparer.Ordinal);

        // Determine if dinner service exists (any slot >= 20:00)
        var hasDinner = slots.Any(s => TimeSpan.TryParse(s, out var t) && t.Hours >= 20);
        var hasLunch = slots.Any(s => TimeSpan.TryParse(s, out var t) && t.Hours < 20);

        // Calculate opening/closing times based on available slots
        TimeSpan openingTime;
        TimeSpan closingTime;

        if (hasLunch && hasDinner)
        {
            // Both services - full day
            openingTime = LunchOpen;
            closingTime = DinnerClose;
        }
        else if (hasDinner)
        {
            // Dinner only
            openingTime = DinnerOpen;
            closingTime = DinnerClose;
        }
        else
        {
            // Lunch only (default)
            openingTime = LunchOpen;
            closingTime = LunchClose;
        }

        return new OpeningHoursInfo
        {
            AvailableSlots = slots,
            OpeningTime = openingTime,
            ClosingTime = closingTime,
            HasDinner = hasDinner,
            HasLunch = hasLunch,
            IsFromDatabase = isFromDatabase
        };
    }

    public async Task<OpeningHoursInfo> GetContextAwareHoursAsync(DateTime date, CancellationToken ct = default)
    {
        var fullHours = await GetOpeningHoursAsync(date, ct);

        // If only one service type, return as-is
        if (!fullHours.HasDinner || !fullHours.HasLunch)
        {
            return fullHours;
        }

        // Both lunch and dinner available - show based on current time
        var currentTime = DateTime.Now.TimeOfDay;
        var cutoffTime = new TimeSpan(17, 0, 0); // 17:00 is the cutoff

        if (currentTime < cutoffTime)
        {
            // Before 17:00 - show lunch hours
            var lunchSlots = fullHours.AvailableSlots
                .Where(s => TimeSpan.TryParse(s, out var t) && t.Hours < 20)
                .ToList();

            return fullHours with
            {
                AvailableSlots = lunchSlots,
                OpeningTime = LunchOpen,
                ClosingTime = LunchClose
            };
        }
        else
        {
            // After 17:00 - show dinner hours
            var dinnerSlots = fullHours.AvailableSlots
                .Where(s => TimeSpan.TryParse(s, out var t) && t.Hours >= 20)
                .ToList();

            return fullHours with
            {
                AvailableSlots = dinnerSlots,
                OpeningTime = DinnerOpen,
                ClosingTime = DinnerClose
            };
        }
    }
}
