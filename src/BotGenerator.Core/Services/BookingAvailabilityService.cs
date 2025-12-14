using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace BotGenerator.Core.Services;

/// <summary>
/// MySQL-backed availability checks, mirroring the legacy PHP scripts:
/// - php/check_day_status.php
/// - php/fetch_daily_limit.php
/// - php/gethourdata.php
/// </summary>
public class BookingAvailabilityService : IBookingAvailabilityService
{
    private readonly string _connectionString;
    private readonly ILogger<BookingAvailabilityService> _logger;

    public BookingAvailabilityService(IConfiguration configuration, ILogger<BookingAvailabilityService> logger)
    {
        _connectionString = configuration["MySQL:ConnectionString"]
            ?? throw new InvalidOperationException("MySQL:ConnectionString not configured");
        _logger = logger;
    }

    public async Task<DayStatusResult> CheckDayStatusAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        // PHP: default closed Mon/Tue/Wed (1..3)
        var dayOfWeekN = ToPhpIsoDay(date.DayOfWeek); // 1..7
        var isDefaultClosed = dayOfWeekN is 1 or 2 or 3;

        var weekdayNames = new Dictionary<int, string>
        {
            [1] = "Lunes",
            [2] = "Martes",
            [3] = "Miércoles",
            [4] = "Jueves",
            [5] = "Viernes",
            [6] = "Sábado",
            [7] = "Domingo"
        };

        var weekday = weekdayNames[dayOfWeekN];
        var isOpen = !isDefaultClosed;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = "SELECT is_open FROM restaurant_days WHERE date = @Date LIMIT 1";
        var row = await connection.QueryFirstOrDefaultAsync<RestaurantDayRow>(new CommandDefinition(
            sql,
            new { Date = date.ToString("yyyy-MM-dd") },
            cancellationToken: cancellationToken));

        if (row != null)
        {
            isOpen = row.is_open;
        }

        return new DayStatusResult
        {
            Date = date.Date,
            Weekday = weekday,
            IsOpen = isOpen,
            IsDefaultClosedDay = isDefaultClosed
        };
    }

    public async Task<DailyLimitResult> GetDailyLimitAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var dbDate = date.ToString("yyyy-MM-dd");

        // PHP: reservation_manager.dailyLimit for reservationDate, default 45
        var dailyLimit = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT dailyLimit FROM reservation_manager WHERE reservationDate = @Date LIMIT 1",
            new { Date = dbDate },
            cancellationToken: cancellationToken)) ?? 45;

        // PHP: SUM(party_size) from bookings
        var totalPeople = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT SUM(party_size) FROM bookings WHERE reservation_date = @Date",
            new { Date = dbDate },
            cancellationToken: cancellationToken)) ?? 0;

        var free = dailyLimit - totalPeople;

        return new DailyLimitResult
        {
            Date = date.Date,
            DailyLimit = dailyLimit,
            TotalPeople = totalPeople,
            FreeBookingSeats = free
        };
    }

    public async Task<HourDataResult> GetHourDataAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var dbDate = date.ToString("yyyy-MM-dd");

        // dailyLimit (default 45)
        var dailyLimit = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT dailyLimit FROM reservation_manager WHERE reservationDate = @Date LIMIT 1",
            new { Date = dbDate },
            cancellationToken: cancellationToken)) ?? 45;

        // bookings grouped by hour
        var bookings = await connection.QueryAsync<BookingByHourRow>(new CommandDefinition(
            @"SELECT TIME_FORMAT(reservation_time, '%H:%i') AS hour, SUM(party_size) AS total_people
              FROM bookings
              WHERE reservation_date = @Date
              GROUP BY TIME_FORMAT(reservation_time, '%H:%i')",
            new { Date = dbDate },
            cancellationToken: cancellationToken));

        var bookingsByHour = bookings.ToDictionary(r => r.hour, r => r.total_people);
        var totalPeople = bookingsByHour.Values.Sum();

        // hour_configuration authoritative if present
        var hourConfigJson = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT hourData FROM hour_configuration WHERE date = @Date LIMIT 1",
            new { Date = dbDate },
            cancellationToken: cancellationToken));

        if (string.IsNullOrWhiteSpace(hourConfigJson))
        {
            // Default generation uses openinghours.hoursarray or fallback set
            var activeHours = await GetOpeningHoursAsync(connection, dbDate, cancellationToken);
            if (activeHours.Count == 0)
            {
                activeHours = new List<string> { "13:30", "14:00", "14:30", "15:00" };
            }

            activeHours.Sort(StringComparer.Ordinal);
            var equalPercentage = 100.0 / activeHours.Count;

            var hourData = new Dictionary<string, HourSlotData>();
            foreach (var hour in activeHours)
            {
                var booked = bookingsByHour.TryGetValue(hour, out var b) ? b : 0;
                var totalCapacity = (int)Math.Ceiling((equalPercentage / 100.0) * dailyLimit);
                var capacity = totalCapacity - booked;
                var completion = totalCapacity > 0 ? (double)booked / totalCapacity * 100.0 : 0.0;

                var status = "available";
                if (completion > 90) status = "full";
                else if (completion > 70) status = "limited";

                hourData[hour] = new HourSlotData
                {
                    Status = status,
                    Capacity = capacity,
                    TotalCapacity = totalCapacity,
                    Bookings = booked,
                    Percentage = equalPercentage,
                    Completion = completion,
                    IsClosed = false
                };
            }

            return new HourDataResult
            {
                Date = date.Date,
                IsDefaultData = true,
                DailyLimit = dailyLimit,
                TotalPeople = totalPeople,
                ActiveHours = activeHours,
                HourData = hourData
            };
        }

        // Parse existing hour_configuration data
        var parsed = JsonSerializer.Deserialize<Dictionary<string, HourConfigEntry>>(hourConfigJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                     ?? new Dictionary<string, HourConfigEntry>();

        var updated = new Dictionary<string, HourSlotData>();
        foreach (var (hour, entry) in parsed)
        {
            var booked = bookingsByHour.TryGetValue(hour, out var b) ? b : 0;
            var totalCapacity = (int)Math.Ceiling((entry.percentage / 100.0) * dailyLimit);
            var capacity = totalCapacity - booked;
            var completion = totalCapacity > 0 ? (double)booked / totalCapacity * 100.0 : 0.0;

            var status = entry.status ?? "available";
            if (!entry.isClosed && status != "closed")
            {
                if (completion > 90) status = "full";
                else if (completion > 70) status = "limited";
                else status = "available";
            }

            updated[hour] = new HourSlotData
            {
                Status = status,
                Capacity = capacity,
                TotalCapacity = totalCapacity,
                Bookings = booked,
                Percentage = entry.percentage,
                Completion = completion,
                IsClosed = entry.isClosed || status == "closed"
            };
        }

        var active = updated.Keys.ToList();
        active.Sort(StringComparer.Ordinal);

        return new HourDataResult
        {
            Date = date.Date,
            IsDefaultData = false,
            DailyLimit = dailyLimit,
            TotalPeople = totalPeople,
            ActiveHours = active,
            HourData = updated
        };
    }

    public async Task<BookingAvailabilityDecision> EvaluateAsync(
        DateTime date,
        int partySize,
        TimeSpan? time,
        CancellationToken cancellationToken = default)
    {
        if (partySize <= 0)
        {
            return new BookingAvailabilityDecision
            {
                IsAvailable = false,
                Reason = "invalid",
                Message = ResponseVariations.AskPartySize()
            };
        }

        // Same-day bookings are now allowed via WhatsApp

        var dayStatus = await CheckDayStatusAsync(date, cancellationToken);
        if (!dayStatus.IsOpen)
        {
            var next = await FindNextOpenDateAsync(date.AddDays(1), cancellationToken);
            return new BookingAvailabilityDecision
            {
                IsAvailable = false,
                Reason = "closed_day",
                SuggestedDate = next,
                Message = next.HasValue
                    ? $"Lo siento, estamos cerrados el {dayStatus.Weekday}. ¿Te viene bien reservar para el {FormatSpanishDay(next.Value)}?"
                    : $"Lo siento, estamos cerrados el {dayStatus.Weekday}. ¿Te gustaría reservar para otro día?"
            };
        }

        var daily = await GetDailyLimitAsync(date, cancellationToken);
        if (daily.FreeBookingSeats < partySize)
        {
            var next = await FindNextDateWithCapacityAsync(date.AddDays(1), partySize, cancellationToken);
            return new BookingAvailabilityDecision
            {
                IsAvailable = false,
                Reason = "daily_full",
                SuggestedDate = next,
                Message = next.HasValue
                    ? $"Ese día ya no tenemos sitio para {partySize} personas. ¿Te viene bien el {FormatSpanishDay(next.Value)}?"
                    : $"Ese día ya no tenemos sitio para {partySize} personas. ¿Te viene bien otra fecha?"
            };
        }

        if (time == null)
        {
            return new BookingAvailabilityDecision
            {
                IsAvailable = true,
                Reason = "ok"
            };
        }

        var hourData = await GetHourDataAsync(date, cancellationToken);
        var timeKey = $"{time.Value.Hours:D2}:{time.Value.Minutes:D2}";

        if (!hourData.HourData.TryGetValue(timeKey, out var slot) || slot.IsClosed)
        {
            var suggested = SuggestHours(hourData, partySize);
            if (suggested.Count > 0)
            {
                return new BookingAvailabilityDecision
                {
                    IsAvailable = false,
                    Reason = "hour_unavailable",
                    SuggestedHours = suggested,
                    Message = $"A esa hora no podemos. Tengo disponibilidad a las {string.Join(", ", suggested.Take(4))}. ¿Te viene bien alguna?"
                };
            }

            var next = await FindNextDateWithCapacityAndHoursAsync(date.AddDays(1), partySize, cancellationToken);
            return new BookingAvailabilityDecision
            {
                IsAvailable = false,
                Reason = "hour_unavailable",
                SuggestedDate = next,
                Message = next.HasValue
                    ? $"Ese día no tenemos hueco para {partySize} personas en las horas disponibles. ¿Te viene bien el {FormatSpanishDay(next.Value)}?"
                    : "Ese día no tenemos hueco en las horas disponibles. ¿Te viene bien otra fecha?"
            };
        }

        if (slot.Capacity < partySize)
        {
            var suggested = SuggestHours(hourData, partySize);
            if (suggested.Count > 0)
            {
                return new BookingAvailabilityDecision
                {
                    IsAvailable = false,
                    Reason = "hour_unavailable",
                    SuggestedHours = suggested,
                    Message = $"A las {timeKey} ya no tenemos hueco para {partySize}. Tengo disponibilidad a las {string.Join(", ", suggested.Take(4))}. ¿Te viene bien alguna?"
                };
            }

            var next = await FindNextDateWithCapacityAndHoursAsync(date.AddDays(1), partySize, cancellationToken);
            return new BookingAvailabilityDecision
            {
                IsAvailable = false,
                Reason = "hour_unavailable",
                SuggestedDate = next,
                Message = next.HasValue
                    ? $"Ese día está completo para {partySize}. ¿Te viene bien el {FormatSpanishDay(next.Value)}?"
                    : $"Ese día está completo para {partySize}. ¿Te viene bien otra fecha?"
            };
        }

        return new BookingAvailabilityDecision
        {
            IsAvailable = true,
            Reason = "ok"
        };
    }

    private static int ToPhpIsoDay(DayOfWeek dayOfWeek)
    {
        // C#: Sunday=0 ... Saturday=6
        // PHP N: Monday=1 ... Sunday=7
        return dayOfWeek == DayOfWeek.Sunday ? 7 : (int)dayOfWeek;
    }

    private static string FormatSpanishDay(DateTime date)
    {
        var days = new[] { "domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado" };
        return $"{days[(int)date.DayOfWeek]} {date:dd/MM/yyyy}";
    }

    private static List<string> SuggestHours(HourDataResult data, int partySize)
    {
        return data.ActiveHours
            .Where(h =>
            {
                if (!data.HourData.TryGetValue(h, out var slot)) return false;
                if (slot.IsClosed) return false;
                return slot.Capacity >= partySize;
            })
            .ToList();
    }

    private async Task<List<string>> GetOpeningHoursAsync(
        MySqlConnection connection,
        string dbDate,
        CancellationToken cancellationToken)
    {
        var hoursJson = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT hoursarray FROM openinghours WHERE dateselected = @Date LIMIT 1",
            new { Date = dbDate },
            cancellationToken: cancellationToken));

        if (string.IsNullOrWhiteSpace(hoursJson))
        {
            return new List<string> { "13:30", "14:00", "14:30", "15:00" };
        }

        try
        {
            var hours = JsonSerializer.Deserialize<List<string>>(hoursJson);
            return hours ?? new List<string> { "13:30", "14:00", "14:30", "15:00" };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid openinghours.hoursarray JSON for {Date}", dbDate);
            return new List<string> { "13:30", "14:00", "14:30", "15:00" };
        }
    }

    private async Task<DateTime?> FindNextOpenDateAsync(DateTime start, CancellationToken cancellationToken)
    {
        for (var i = 0; i < 60; i++)
        {
            var date = start.Date.AddDays(i);
            var status = await CheckDayStatusAsync(date, cancellationToken);
            if (status.IsOpen) return date;
        }

        return null;
    }

    private async Task<DateTime?> FindNextDateWithCapacityAsync(DateTime start, int partySize, CancellationToken cancellationToken)
    {
        for (var i = 0; i < 90; i++)
        {
            var date = start.Date.AddDays(i);
            var status = await CheckDayStatusAsync(date, cancellationToken);
            if (!status.IsOpen) continue;

            var daily = await GetDailyLimitAsync(date, cancellationToken);
            if (daily.FreeBookingSeats >= partySize) return date;
        }

        return null;
    }

    private async Task<DateTime?> FindNextDateWithCapacityAndHoursAsync(DateTime start, int partySize, CancellationToken cancellationToken)
    {
        for (var i = 0; i < 90; i++)
        {
            var date = start.Date.AddDays(i);
            var status = await CheckDayStatusAsync(date, cancellationToken);
            if (!status.IsOpen) continue;

            var daily = await GetDailyLimitAsync(date, cancellationToken);
            if (daily.FreeBookingSeats < partySize) continue;

            var hours = await GetHourDataAsync(date, cancellationToken);
            var any = hours.HourData.Values.Any(s => !s.IsClosed && s.Capacity >= partySize);
            if (any) return date;
        }

        return null;
    }

    private sealed class RestaurantDayRow
    {
        public bool is_open { get; init; }
    }

    private sealed class BookingByHourRow
    {
        public string hour { get; init; } = "";
        public int total_people { get; init; }
    }

    private sealed class HourConfigEntry
    {
        public string? status { get; init; }
        public int capacity { get; init; }
        public int bookings { get; init; }
        public double percentage { get; init; }
        public double completion { get; init; }
        public bool isClosed { get; init; }
    }
}

