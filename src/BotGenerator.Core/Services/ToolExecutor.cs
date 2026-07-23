using System.Data.Common;
using System.Text.Json;
using BotGenerator.Core.Models;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace BotGenerator.Core.Services;

public class ToolExecutor : IToolExecutor
{
    private readonly IWhatsAppService _whatsApp;
    private readonly IMenuRepository _menuRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IRestaurantConfigRepository _restaurantConfigRepo;
    private readonly IOpeningHoursService _openingHoursService;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _connectionString;
    private readonly ILogger<ToolExecutor> _logger;

    public ToolExecutor(
        IWhatsAppService whatsApp,
        IMenuRepository menuRepository,
        IBookingRepository bookingRepository,
        IRestaurantConfigRepository restaurantConfigRepo,
        IOpeningHoursService openingHoursService,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<ToolExecutor> logger)
    {
        _whatsApp = whatsApp;
        _menuRepository = menuRepository;
        _bookingRepository = bookingRepository;
        _restaurantConfigRepo = restaurantConfigRepo;
        _openingHoursService = openingHoursService;
        _serviceProvider = serviceProvider;
        _connectionString = configuration["MySQL:ConnectionString"] ?? "";
        _logger = logger;
    }

    public async Task<ToolResult> ExecuteAsync(string toolName, JsonElement input, string phoneNumber, CancellationToken ct = default)
    {
        _logger.LogInformation("ToolExecutor executing tool: {Tool} for phone: {Phone}", toolName, phoneNumber);

        try
        {
            return toolName switch
            {
                "fetch_whatsapp_history" => await ExecuteFetchHistory(input, phoneNumber, ct),
                "get_restaurant_info" => await ExecuteGetRestaurantInfo(input, ct),
                "get_rice_menu" => await ExecuteGetRiceMenu(ct),
                "check_availability" => await ExecuteCheckAvailability(input, ct),
                "get_opening_hours" => await ExecuteGetOpeningHours(input, ct),
                "get_hour_data" => await ExecuteGetHourData(input, ct),
                "get_day_status" => await ExecuteGetDayStatus(input, ct),
                "get_bookings" => await ExecuteGetBookings(input, phoneNumber, ct),
                "query_database" => await ExecuteQueryDatabase(input, ct),
                "send_message" => await ExecuteSendMessage(input, phoneNumber, ct),
                "cancel_booking" => await ExecuteCancelBooking(input, phoneNumber, ct),
                "modify_booking" => await ExecuteModifyBooking(input, phoneNumber, ct),
                "create_booking" => await ExecuteCreateBooking(input, phoneNumber, ct),
                // NEW TOOLS
                "check_future_booking" => await ExecuteCheckFutureBooking(input, phoneNumber, ct),
                "get_opening_hours_with_capacity" => await ExecuteGetOpeningHoursWithCapacity(input, ct),
                "check_hour_capacity" => await ExecuteCheckHourCapacity(input, ct),
                "check_day_capacity" => await ExecuteCheckDayCapacity(input, ct),
                "check_availability_for_party" => await ExecuteCheckAvailabilityForParty(input, ct),
                "check_rice_availability" => await ExecuteCheckRiceAvailability(input, ct),
                _ => new ToolResult { IsError = true, Content = $"Unknown tool: {toolName}" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool execution failed for {Tool}", toolName);
            return new ToolResult { IsError = true, Content = $"Tool error: {ex.Message}" };
        }
    }

    // === fetch_whatsapp_history ===

    private async Task<ToolResult> ExecuteFetchHistory(JsonElement input, string phoneNumber, CancellationToken ct)
    {
        var limit = 30;
        if (input.TryGetProperty("limit", out var limitProp) && limitProp.ValueKind == JsonValueKind.Number)
            limit = Math.Clamp(limitProp.GetInt32(), 5, 100);

        var messages = await _whatsApp.GetHistoryAsync(phoneNumber, limit, ct);

        _logger.LogInformation("Fetched {Count} WhatsApp messages for {Phone}", messages.Count, phoneNumber);

        var formatted = messages.Select(m => new
        {
            role = m.FromMe ? "assistant" : "user",
            name = m.FromMe ? "Bot" : (m.SenderName ?? "User"),
            text = m.Text ?? "",
            timestamp = m.Timestamp
        }).ToList();

        return new ToolResult
        {
            Content = JsonSerializer.Serialize(new
            {
                messageCount = formatted.Count,
                messages = formatted
            })
        };
    }

    // === get_restaurant_info ===

    private async Task<ToolResult> ExecuteGetRestaurantInfo(JsonElement input, CancellationToken ct)
    {
        var config = await _restaurantConfigRepo.GetBySlugAsync("villacarmen", ct);

        if (config == null)
        {
            return new ToolResult
            {
                Content = JsonSerializer.Serialize(new
                {
                    name = "Alquería Villa Carmen",
                    phone = "+34 638 857 294",
                    email = "reservas@alqueriavillacarmen.com",
                    address = "Carrer Sequia Rascanya 2, Catarroja 46470 Valencia",
                    web = "https://alqueriavillacarmen.com",
                    menu = "https://alqueriavillacarmen.com/menufindesemana.php"
                })
            };
        }

        return new ToolResult
        {
            Content = JsonSerializer.Serialize(new
            {
                name = config.Name,
                phone = config.ContactPhone,
                email = config.ContactEmail,
                address = config.Location,
                web = config.WebsiteUrl,
                menu = config.MenuUrl
            })
        };
    }

    // === get_rice_menu ===

    private async Task<ToolResult> ExecuteGetRiceMenu(CancellationToken ct)
    {
        var riceTypes = await _menuRepository.GetActiveRiceTypesAsync(ct);
        return new ToolResult
        {
            Content = JsonSerializer.Serialize(new { riceTypes })
        };
    }

    // === check_rice_availability ===
    private async Task<ToolResult> ExecuteCheckRiceAvailability(JsonElement input, CancellationToken ct)
    {
        var riceType = input.TryGetProperty("rice_type", out var r) && r.ValueKind == JsonValueKind.String
            ? r.GetString() : null;

        if (string.IsNullOrWhiteSpace(riceType))
        {
            return new ToolResult
            {
                IsError = true,
                Content = JsonSerializer.Serialize(new { error = "No se proporciono tipo de arroz" })
            };
        }

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            // Get all active arroces for matching
            const string sql = @"
                SELECT NUM as Id, DESCRIPCION as Descripcion
                FROM FINDE 
                WHERE TIPO = 'ARROZ' AND active = 1
                ORDER BY NUM";

            var arroces = await connection.QueryAsync<dynamic>(sql);
            var arrozList = arroces.ToList();

            var matchedName = FindRiceMatch(
                riceType,
                arrozList.Select(a => (string)a.Descripcion));
            dynamic? matched = arrozList.FirstOrDefault(a =>
                string.Equals((string)a.Descripcion, matchedName, StringComparison.Ordinal));

            if (matched != null)
            {
                return new ToolResult
                {
                    Content = JsonSerializer.Serialize(new
                    {
                        available = true,
                        requested = riceType,
                        matched = (string)matched.Descripcion,
                        id = (int)matched.Id
                    })
                };
            }

            // No match - return available options
            var availableArroces = arrozList.Select(a => (string)a.Descripcion).ToList();
            return new ToolResult
            {
                Content = JsonSerializer.Serialize(new
                {
                    available = false,
                    requested = riceType,
                    matched = (string?)null,
                    availableOptions = availableArroces
                })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rice availability for: {Rice}", riceType);
            return new ToolResult
            {
                IsError = true,
                Content = JsonSerializer.Serialize(new { error = "Error al verificar disponibilidad del arroz" })
            };
        }
    }

    public static string? FindRiceMatch(string requested, IEnumerable<string> available)
    {
        var normalized = requested.Trim().ToLowerInvariant();
        var names = available.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        var exact = names.FirstOrDefault(x =>
            string.Equals(x.Trim(), requested.Trim(), StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        var generic = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "arroz", "seco", "meloso", "caldoso", "de", "del", "la", "el" };
        var terms = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim(' ', '.', ',', ';', ':', '(', ')'))
            .Where(x => x.Length > 1 && !generic.Contains(x))
            .ToList();

        return terms.Count == 0 ? null : names.FirstOrDefault(name =>
            terms.All(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    // === check_availability ===

    private async Task<ToolResult> ExecuteCheckAvailability(JsonElement input, CancellationToken ct)
    {
        var dateStr = input.TryGetProperty("date", out var d) ? d.GetString() : null;
        var timeStr = input.TryGetProperty("time", out var t) ? t.GetString() : null;
        var people = input.TryGetProperty("people", out var p) && p.ValueKind == JsonValueKind.Number
            ? p.GetInt32() : 0;

        if (string.IsNullOrEmpty(dateStr) || !DateTime.TryParseExact(dateStr, "dd/MM/yyyy", null,
            System.Globalization.DateTimeStyles.None, out var date))
        {
            // Try yyyy-MM-dd format
            if (!DateTime.TryParse(dateStr, out date))
                return new ToolResult { IsError = true, Content = "Invalid date format. Use dd/MM/yyyy." };
        }

        TimeSpan? time = null;
        if (!string.IsNullOrEmpty(timeStr) && TimeSpan.TryParse(timeStr, out var ts))
            time = ts;

        if (people <= 0)
            people = 2; // default party size

        var availabilityService = _serviceProvider.GetRequiredService<IBookingAvailabilityService>();
        var decision = await availabilityService.EvaluateAsync(date, people, time, cancellationToken: ct);

        return new ToolResult
        {
            Content = JsonSerializer.Serialize(new
            {
                date = date.ToString("dd/MM/yyyy"),
                time = timeStr,
                people,
                isAvailable = decision.IsAvailable,
                reason = decision.Reason,
                message = decision.Message,
                suggestedDate = decision.SuggestedDate?.ToString("dd/MM/yyyy"),
                suggestedHours = decision.SuggestedHours
            })
        };
    }

    // === get_opening_hours ===

    private async Task<ToolResult> ExecuteGetOpeningHours(JsonElement input, CancellationToken ct)
    {
        var dateStr = input.TryGetProperty("date", out var d) ? d.GetString() : null;

        if (string.IsNullOrEmpty(dateStr) || !DateTime.TryParseExact(dateStr, "dd/MM/yyyy", null,
            System.Globalization.DateTimeStyles.None, out var date))
        {
            if (!DateTime.TryParse(dateStr, out date))
                return new ToolResult { IsError = true, Content = "Invalid date format. Use dd/MM/yyyy." };
        }

        var hours = await _openingHoursService.GetContextAwareHoursAsync(date, ct);

        return new ToolResult
        {
            Content = JsonSerializer.Serialize(new
            {
                date = date.ToString("dd/MM/yyyy"),
                dayName = date.ToString("dddd"),
                openingTime = hours.OpeningTimeFormatted,
                closingTime = hours.ClosingTimeFormatted,
                hasLunch = hours.HasLunch,
                hasDinner = hours.HasDinner,
                availableSlots = hours.AvailableSlots
            })
        };
    }

    // === get_hour_data (detailed seat data per hour) ===

    private async Task<ToolResult> ExecuteGetHourData(JsonElement input, CancellationToken ct)
    {
        var dateStr = input.TryGetProperty("date", out var d) ? d.GetString() : null;

        if (string.IsNullOrEmpty(dateStr) || !DateTime.TryParseExact(dateStr, "dd/MM/yyyy", null,
            System.Globalization.DateTimeStyles.None, out var date))
        {
            if (!DateTime.TryParse(dateStr, out date))
                return new ToolResult { IsError = true, Content = "Invalid date format. Use dd/MM/yyyy." };
        }

        var hourData = await _serviceProvider.GetRequiredService<IBookingAvailabilityService>().GetHourDataAsync(date, cancellationToken: ct);

        var slots = hourData.HourData.Select(kvp => new
        {
            hour = kvp.Key,
            status = kvp.Value.Status,
            freeSeats = kvp.Value.Capacity,
            totalCapacity = kvp.Value.TotalCapacity,
            bookedSeats = kvp.Value.Bookings,
            completion = Math.Round(kvp.Value.Completion, 1),
            isClosed = kvp.Value.IsClosed
        }).ToList();

        return new ToolResult
        {
            Content = JsonSerializer.Serialize(new
            {
                date = date.ToString("dd/MM/yyyy"),
                dailyLimit = hourData.DailyLimit,
                totalPeopleBooked = hourData.TotalPeople,
                freeBookingSeats = hourData.DailyLimit - hourData.TotalPeople,
                activeHours = hourData.ActiveHours,
                slots
            })
        };
    }

    // === get_day_status ===

    private async Task<ToolResult> ExecuteGetDayStatus(JsonElement input, CancellationToken ct)
    {
        var dateStr = input.TryGetProperty("date", out var d) ? d.GetString() : null;

        if (string.IsNullOrEmpty(dateStr) || !DateTime.TryParseExact(dateStr, "dd/MM/yyyy", null,
            System.Globalization.DateTimeStyles.None, out var date))
        {
            if (!DateTime.TryParse(dateStr, out date))
                return new ToolResult { IsError = true, Content = "Invalid date format. Use dd/MM/yyyy." };
        }

        var availabilityService = _serviceProvider.GetRequiredService<IBookingAvailabilityService>();
        var dayStatus = await availabilityService.CheckDayStatusAsync(date, ct);
        var dailyLimit = await availabilityService.GetDailyLimitAsync(date, cancellationToken: ct);

        return new ToolResult
        {
            Content = JsonSerializer.Serialize(new
            {
                date = date.ToString("dd/MM/yyyy"),
                dayName = dayStatus.Weekday,
                isOpen = dayStatus.IsOpen,
                isDefaultClosedDay = dayStatus.IsDefaultClosedDay,
                dailyLimit = dailyLimit.DailyLimit,
                totalPeopleBooked = dailyLimit.TotalPeople,
                freeSeats = dailyLimit.FreeBookingSeats
            })
        };
    }

    // === get_bookings ===

    private async Task<ToolResult> ExecuteGetBookings(JsonElement input, string phoneNumber, CancellationToken ct)
    {
        // Allow overriding phone from input, but default to the current user's phone
        var phone = input.TryGetProperty("phone", out var p) ? p.GetString() : phoneNumber;
        if (string.IsNullOrEmpty(phone))
            phone = phoneNumber;

        // Strip non-digits
        phone = new string(phone.Where(char.IsDigit).ToArray());

        var bookings = await _bookingRepository.FindBookingsByPhoneAsync(phone, ct);

        var formatted = bookings.Select(b => new
        {
            id = b.Id,
            date = b.DateFormatted,
            time = b.TimeFormatted,
            people = b.PartySize,
            name = b.CustomerName,
            rice = string.IsNullOrEmpty(b.ArrozType) ? (string?)null : b.ArrozType,
            riceServings = b.ArrozServings,
            highChairs = b.HighChairs,
            babyStrollers = b.BabyStrollers,
            summary = b.Summary
        }).ToList();

        return new ToolResult
        {
            Content = JsonSerializer.Serialize(new
            {
                phone,
                bookingCount = formatted.Count,
                bookings = formatted
            })
        };
    }

    // === query_database (read-only SQL) ===

    private async Task<ToolResult> ExecuteQueryDatabase(JsonElement input, CancellationToken ct)
    {
        var sql = input.TryGetProperty("sql", out var s) ? s.GetString() : null;

        if (string.IsNullOrWhiteSpace(sql))
            return new ToolResult { IsError = true, Content = "Missing 'sql' parameter." };

        // Security: only allow SELECT statements
        var trimmedSql = sql.TrimStart().ToUpperInvariant();
        if (!trimmedSql.StartsWith("SELECT") && !trimmedSql.StartsWith("SHOW") && !trimmedSql.StartsWith("DESCRIBE"))
            return new ToolResult { IsError = true, Content = "Only SELECT, SHOW, and DESCRIBE queries are allowed." };

        // Block dangerous functions
        if (sql.Contains("SLEEP(", StringComparison.OrdinalIgnoreCase) ||
            sql.Contains("BENCHMARK(", StringComparison.OrdinalIgnoreCase) ||
            sql.Contains("LOAD_FILE(", StringComparison.OrdinalIgnoreCase) ||
            sql.Contains("INTO OUTFILE", StringComparison.OrdinalIgnoreCase) ||
            sql.Contains("INTO DUMPFILE", StringComparison.OrdinalIgnoreCase))
        {
            return new ToolResult { IsError = true, Content = "Query contains disallowed functions." };
        }

        // Limit result rows
        if (!sql.Contains("LIMIT", StringComparison.OrdinalIgnoreCase))
            sql = sql.TrimEnd(';', ' ') + " LIMIT 50";

        if (string.IsNullOrEmpty(_connectionString))
            return new ToolResult { IsError = true, Content = "Database connection not configured." };

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            var rows = (await connection.QueryAsync(sql, ct)).ToList();

            if (rows.Count == 0)
                return new ToolResult { Content = JsonSerializer.Serialize(new { rowCount = 0, rows = Array.Empty<object>() }) };

            // Dapper returns IEnumerable<dynamic> — convert to dictionary for JSON serialization
            var serializedRows = rows.Select(row =>
            {
                var dict = new Dictionary<string, object?>();
                if (row is not DbDataReader)
                {
                    // Dapper DapperRow
                    foreach (var prop in (IDictionary<string, object>)row)
                        dict[prop.Key] = prop.Value;
                }
                return dict;
            }).ToList();

            return new ToolResult
            {
                Content = JsonSerializer.Serialize(new
                {
                    rowCount = serializedRows.Count,
                    rows = serializedRows
                }, new JsonSerializerOptions { WriteIndented = false })
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SQL query failed: {SQL}", sql);
            return new ToolResult { IsError = true, Content = $"SQL error: {ex.Message}" };
        }
    }

    // === cancel_booking ===

    private async Task<ToolResult> ExecuteCancelBooking(JsonElement input, string phoneNumber, CancellationToken ct)
    {
        var bookingIdStr = input.TryGetProperty("booking_id", out var bid) ? bid.GetString() : null;
        var confirmed = input.TryGetProperty("confirmed", out var conf) && conf.ValueKind == JsonValueKind.True;

        if (string.IsNullOrEmpty(bookingIdStr) || !int.TryParse(bookingIdStr, out var bookingId))
        {
            return new ToolResult { IsError = true, Content = "Missing or invalid 'booking_id' parameter." };
        }

        if (!confirmed)
        {
            return new ToolResult { IsError = true, Content = "Cancellation not confirmed. Set 'confirmed': true to proceed." };
        }

        _logger.LogWarning("Agent requesting to cancel booking {BookingId} for {Phone}", bookingId, phoneNumber);

        try
        {
            // Get the booking first
            var booking = await _bookingRepository.GetBookingByIdAsync(bookingId, ct);
            if (booking == null)
            {
                return new ToolResult { IsError = true, Content = $"Booking {bookingId} not found." };
            }

            var phone9 = new string(phoneNumber.Where(char.IsDigit).ToArray());
            if (phone9.Length > 9) phone9 = phone9[^9..];
            if (!string.Equals(booking.ContactPhone, phone9, StringComparison.OrdinalIgnoreCase))
                return new ToolResult { IsError = true, Content = "No tienes permiso para cancelar esta reserva." };

            if (booking.Status is not ("pending" or "confirmed"))
                return new ToolResult { IsError = true, Content = "Esta reserva no está activa y no se puede cancelar." };

            var cancelSuccess = await _bookingRepository.ArchiveAndCancelBookingAsync(
                booking, "AI_AGENT", ct);

            if (!cancelSuccess)
            {
                return new ToolResult { IsError = true, Content = "Cancelación abortada sin borrar datos." };
            }

            _logger.LogInformation("Successfully cancelled booking {BookingId} via AI Agent", bookingId);
            return new ToolResult
            {
                Content = JsonSerializer.Serialize(new
                {
                    success = true,
                    bookingId,
                    message = $"Booking for {booking.DateFormatted} at {booking.TimeFormatted} has been cancelled."
                })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling booking {BookingId}", bookingId);
            return new ToolResult { IsError = true, Content = $"Error cancelling booking: {ex.Message}" };
        }
    }

    // === modify_booking ===

    /// <summary>
    /// Detects rice type, rice servings, high chair, baby stroller and clear-rice
    /// modifications from a modify_booking tool call. Populates <paramref name="updateData"/>
    /// with the fields that actually differ from the current booking and returns a
    /// human-readable list of the changes applied.
    ///
    /// A servings-only change (e.g. keeping the same rice type but going 2 -> 4
    /// portions) is a real modification and MUST be reported here; otherwise the
    /// caller wrongly reports "no change specified" (regression from chat-16166).
    /// </summary>
    public static List<string> CollectRiceAndExtrasChanges(
        JsonElement input, BookingRecord booking, BookingUpdateData updateData)
    {
        var changes = new List<string>();

        if (input.TryGetProperty("rice_type", out var riceEl) && riceEl.ValueKind == JsonValueKind.String)
        {
            var riceType = riceEl.GetString();
            if (!string.IsNullOrEmpty(riceType) && riceType != booking.ArrozType)
            {
                updateData.ArrozType = riceType;
                changes.Add($"arroz: {(booking.ArrozType ?? "Ninguno")} → {riceType}");
            }
        }

        if (input.TryGetProperty("rice_servings", out var servingsEl) && servingsEl.ValueKind == JsonValueKind.Number)
        {
            var newServings = servingsEl.GetInt32();
            if (newServings != booking.ArrozServings)
            {
                updateData.ArrozServings = newServings;
                changes.Add($"raciones de arroz: {(booking.ArrozServings?.ToString() ?? "0")} → {newServings}");
            }
        }

        if (input.TryGetProperty("high_chairs", out var chairsEl) && chairsEl.ValueKind == JsonValueKind.Number)
        {
            var chairs = chairsEl.GetInt32();
            if (chairs != booking.HighChairs)
            {
                updateData.HighChairs = chairs;
                changes.Add($"tronas: {booking.HighChairs} → {chairs}");
            }
        }

        if (input.TryGetProperty("baby_strollers", out var strollersEl) && strollersEl.ValueKind == JsonValueKind.Number)
        {
            var strollers = strollersEl.GetInt32();
            if (strollers != booking.BabyStrollers)
            {
                updateData.BabyStrollers = strollers;
                changes.Add($"carros: {booking.BabyStrollers} → {strollers}");
            }
        }

        if (input.TryGetProperty("clear_rice", out var clearEl) && clearEl.ValueKind == JsonValueKind.True)
        {
            updateData.ClearRice = true;
            changes.Add("arroz: eliminado");
        }

        return changes;
    }

    public static string? ValidateRiceChange(
        JsonElement input, BookingRecord booking, int targetPartySize)
    {
        if (input.TryGetProperty("clear_rice", out var clear) && clear.ValueKind == JsonValueKind.True)
            return null;

        var suppliedType = input.TryGetProperty("rice_type", out var rice) && rice.ValueKind == JsonValueKind.String
            ? rice.GetString() : null;
        var suppliedServings = input.TryGetProperty("rice_servings", out var servings) && servings.ValueKind == JsonValueKind.Number
            ? servings.GetInt32() : (int?)null;

        if (suppliedType == null && !suppliedServings.HasValue) return null;

        var effectiveType = suppliedType ?? booking.ArrozType;
        var effectiveServings = suppliedServings ?? booking.ArrozServings;
        if (string.IsNullOrWhiteSpace(effectiveType))
            return "Falta el tipo de arroz.";
        if (!effectiveServings.HasValue)
            return "Falta rice_servings. Pregunta al cliente cuántas raciones quiere; no lo supongas.";
        if (effectiveServings < 2)
            return "Cada arroz requiere un mínimo de 2 raciones.";
        if (effectiveServings > targetPartySize)
            return $"Las raciones de arroz ({effectiveServings}) no pueden superar las personas ({targetPartySize}).";

        return null;
    }

    public static string? ValidateBookingCounts(
        int people, int highChairs, int babyStrollers, int? riceServings)
    {
        if (people < 1) return "El número de personas debe ser al menos 1.";
        if (highChairs < 0) return "El número de tronas no puede ser negativo.";
        if (babyStrollers < 0) return "El número de carros no puede ser negativo.";
        if (highChairs > people) return "Las tronas no pueden superar el número de personas.";
        if (babyStrollers > people) return "Los carros no pueden superar el número de personas.";
        if (riceServings > people) return "Las raciones de arroz no pueden superar el número de personas.";
        return null;
    }

    public static string? ValidateModificationCounts(JsonElement input, BookingRecord booking)
    {
        var people = input.TryGetProperty("people", out var peopleEl) && peopleEl.ValueKind == JsonValueKind.Number
            ? peopleEl.GetInt32() : booking.PartySize;
        var chairs = input.TryGetProperty("high_chairs", out var chairsEl) && chairsEl.ValueKind == JsonValueKind.Number
            ? chairsEl.GetInt32() : booking.HighChairs;
        var strollers = input.TryGetProperty("baby_strollers", out var strollersEl) && strollersEl.ValueKind == JsonValueKind.Number
            ? strollersEl.GetInt32() : booking.BabyStrollers;
        var clearRice = input.TryGetProperty("clear_rice", out var clearEl) && clearEl.ValueKind == JsonValueKind.True;
        var servings = clearRice ? null : input.TryGetProperty("rice_servings", out var servingsEl) && servingsEl.ValueKind == JsonValueKind.Number
            ? servingsEl.GetInt32() : booking.ArrozServings;

        return ValidateBookingCounts(people, chairs, strollers, servings);
    }

    private async Task<ToolResult> ExecuteModifyBooking(JsonElement input, string phoneNumber, CancellationToken ct)
    {
        var bookingIdStr = input.TryGetProperty("booking_id", out var bid) ? bid.GetString() : null;
        var confirmed = input.TryGetProperty("confirmed", out var conf) && conf.ValueKind == JsonValueKind.True;

        if (string.IsNullOrEmpty(bookingIdStr) || !int.TryParse(bookingIdStr, out var bookingId))
        {
            return new ToolResult { IsError = true, Content = "Missing or invalid 'booking_id' parameter." };
        }

        if (!confirmed)
        {
            return new ToolResult { IsError = true, Content = "Modification not confirmed. Set 'confirmed': true to proceed." };
        }

        _logger.LogWarning("Agent requesting to modify booking {BookingId} for {Phone}", bookingId, phoneNumber);

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            // === VALIDATION PHASE ===

            // 1. Get current booking
            var booking = await _bookingRepository.GetBookingByIdAsync(bookingId, ct);

            if (booking == null)
            {
                return new ToolResult { IsError = true, Content = $"Booking {bookingId} not found." };
            }

            // 2. Verify ownership (phone must match)
            var phone9 = new string(phoneNumber.Where(char.IsDigit).ToArray());
            if (phone9.Length > 9) phone9 = phone9[^9..];
            
            if (!string.Equals(booking.ContactPhone, phone9, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Modification denied: phone {Phone} does not match booking {BookingId} phone {BookingPhone}",
                    phone9, bookingId, booking.ContactPhone);
                return new ToolResult { IsError = true, Content = "No tienes permiso para modificar esta reserva." };
            }

            // 3. Check booking is not cancelled
            // (GetBookingByIdAsync returns only active bookings, but double-check)
            var statusRow = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT status FROM bookings WHERE id = @Id",
                new { Id = bookingId });

            if (statusRow == "cancelled")
            {
                return new ToolResult { IsError = true, Content = "No se puede modificar una reserva cancelada." };
            }

            // 4. Parse reservation datetime
            var reservationDateTime = booking.ReservationDate.Date + booking.ReservationTime;
            var now = DateTime.Now;

            // 5. Check not past
            if (reservationDateTime < now)
            {
                return new ToolResult { IsError = true, Content = "No se pueden modificar reservas que ya han pasado." };
            }

            // 6. Check not today
            var today = DateTime.Today;
            if (booking.ReservationDate.Date == today)
            {
                return new ToolResult { IsError = true, Content = "No se pueden modificar reservas para hoy. Por favor, contacta directamente con el restaurante." };
            }

            // 7. Check not tomorrow
            var tomorrow = today.AddDays(1);
            if (booking.ReservationDate.Date == tomorrow)
            {
                return new ToolResult { IsError = true, Content = "No se pueden modificar reservas para mañana. Por favor, contacta directamente con el restaurante." };
            }

            // 8. Check 24 hours advance
            var hoursUntil = (reservationDateTime - now).TotalHours;
            if (hoursUntil < 24)
            {
                return new ToolResult { IsError = true, Content = "Se requiere al menos 24 horas de antelación para modificar una reserva. Por favor, contacta directamente con el restaurante." };
            }

            // 9. Check modification count (max 3)
            var modCount = await connection.ExecuteScalarAsync<int?>(
                "SELECT COUNT(*) FROM modification_history WHERE booking_id = @Id",
                new { Id = bookingId }) ?? 0;

            if (modCount >= 3)
            {
                return new ToolResult { IsError = true, Content = "Has alcanzado el límite máximo de 3 modificaciones para esta reserva. Para más cambios, contacta directamente con el restaurante." };
            }

            var countValidationError = ValidateModificationCounts(input, booking);
            if (countValidationError != null)
                return new ToolResult { IsError = true, Content = countValidationError };

            // === BUILD UPDATE DATA ===
            var updateData = new BookingUpdateData();
            var changes = new List<string>();

            // Extract and validate new values
            if (input.TryGetProperty("date", out var dateEl) && dateEl.ValueKind == JsonValueKind.String)
            {
                var dateStr = dateEl.GetString();
                if (!string.IsNullOrEmpty(dateStr) && !TryParseDate(dateStr, out _))
                    return new ToolResult { IsError = true, Content = "Fecha inválida. Usa YYYY-MM-DD o dd/MM/yyyy." };
                if (!string.IsNullOrEmpty(dateStr) && TryParseDate(dateStr, out var newDate))
                {
                    var newDateStr = newDate.ToString("yyyy-MM-dd");
                    if (newDateStr != booking.ReservationDate.ToString("yyyy-MM-dd"))
                    {
                        // Validate new date
                        var newDbDate = newDate.ToString("yyyy-MM-dd");

                        // Cannot move to today or tomorrow
                        if (newDate.Date == today)
                            return new ToolResult { IsError = true, Content = "No se puede mover la reserva a hoy." };
                        if (newDate.Date == tomorrow)
                            return new ToolResult { IsError = true, Content = "No se puede mover la reserva a mañana." };

                        // Check day status
                        var isClosed = await connection.ExecuteScalarAsync<int?>(@"
                            SELECT is_open FROM restaurant_days WHERE date = @Date",
                            new { Date = newDbDate });
                        var phpDayNum = newDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)newDate.DayOfWeek;
                        var isDefaultClosed = phpDayNum is 1 or 2 or 3;
                        if ((!isClosed.HasValue && isDefaultClosed) || isClosed == 0)
                        {
                            var dayName = newDate.ToString("dddd", new System.Globalization.CultureInfo("es-ES"));
                            return new ToolResult { IsError = true, Content = $"El restaurante está cerrado el {dayName}." };
                        }

                        updateData.ReservationDate = newDateStr;
                        changes.Add($"fecha: {booking.ReservationDate:dd/MM/yyyy} → {newDate:dd/MM/yyyy}");
                    }
                }
            }

            if (input.TryGetProperty("time", out var timeEl) && timeEl.ValueKind == JsonValueKind.String)
            {
                var timeStr = timeEl.GetString();
                if (!string.IsNullOrEmpty(timeStr))
                {
                    if (!TimeSpan.TryParse(timeStr, out var parsedTime) || parsedTime.TotalHours < 0 || parsedTime.TotalHours >= 24)
                        return new ToolResult { IsError = true, Content = "Hora inválida. Usa HH:mm." };
                    var normalizedTime = $"{parsedTime.Hours:D2}:{parsedTime.Minutes:D2}";
                    if (normalizedTime != booking.ReservationTime.ToString("HH:mm"))
                    {
                        updateData.ReservationTime = normalizedTime;
                        changes.Add($"hora: {booking.ReservationTime:HH:mm} → {normalizedTime}");
                    }
                }
            }

            if (input.TryGetProperty("people", out var peopleEl) && peopleEl.ValueKind == JsonValueKind.Number)
            {
                var newPeople = peopleEl.GetInt32();
                if (newPeople != booking.PartySize)
                {
                    // Validate new party size capacity for the date
                    var targetDate = !string.IsNullOrEmpty(updateData.ReservationDate)
                        ? DateTime.Parse(updateData.ReservationDate)
                        : booking.ReservationDate;

                    var targetDbDate = targetDate.ToString("yyyy-MM-dd");
                    var dailyLimit = await connection.ExecuteScalarAsync<int?>(
                        "SELECT dailyLimit FROM reservation_manager WHERE reservationDate = @Date LIMIT 1",
                        new { Date = targetDbDate }) ?? 45;

                    var totalBooked = await connection.ExecuteScalarAsync<int?>(@"
                        SELECT SUM(party_size) FROM bookings 
                        WHERE reservation_date = @Date AND status IN ('pending', 'confirmed') AND id != @ExcludeId",
                        new { Date = targetDbDate, ExcludeId = bookingId }) ?? 0;

                    var freeSeats = dailyLimit - totalBooked;
                    if (newPeople > freeSeats)
                    {
                        return new ToolResult
                        {
                            IsError = true,
                            Content = $"No hay suficiente capacidad para {newPeople} personas. Plazas libres: {freeSeats}."
                        };
                    }

                    updateData.PartySize = newPeople;
                    changes.Add($"personas: {booking.PartySize} → {newPeople}");
                }
            }

            // Rice type/servings, high chairs, baby strollers and clear-rice changes.
            var riceValidationError = ValidateRiceChange(
                input, booking, updateData.PartySize ?? booking.PartySize);
            if (riceValidationError != null)
                return new ToolResult { IsError = true, Content = riceValidationError };

            changes.AddRange(CollectRiceAndExtrasChanges(input, booking, updateData));

            if (input.TryGetProperty("rice_type", out var requestedRice) &&
                requestedRice.ValueKind == JsonValueKind.String)
            {
                var requested = requestedRice.GetString();
                var activeRices = await _menuRepository.GetActiveRiceTypesAsync(ct);
                var matchedRice = string.IsNullOrWhiteSpace(requested)
                    ? null
                    : FindRiceMatch(requested, activeRices);
                if (matchedRice == null)
                    return new ToolResult { IsError = true, Content = "Arroz no disponible. Usa check_rice_availability y confirma una opción válida." };

                changes.RemoveAll(x => x.StartsWith("arroz:", StringComparison.Ordinal));
                if (!string.Equals(matchedRice, booking.ArrozType, StringComparison.OrdinalIgnoreCase))
                {
                    updateData.ArrozType = matchedRice;
                    changes.Add($"arroz: {(booking.ArrozType ?? "Ninguno")} → {matchedRice}");
                }
                else
                {
                    updateData.ArrozType = null;
                }
            }

            // Check if there are any changes
            if (changes.Count == 0)
            {
                return new ToolResult { IsError = true, Content = "No se ha especificado ningún cambio." };
            }

            // === PERFORM UPDATE ===
            var success = await _bookingRepository.UpdateBookingAsync(bookingId, updateData, ct);

            if (!success)
            {
                return new ToolResult { IsError = true, Content = "Error al modificar la reserva en la base de datos." };
            }

            // === LOG MODIFICATION ===
            var modificationsJson = JsonSerializer.Serialize(changes);
            await connection.ExecuteAsync(@"
                INSERT INTO modification_history (booking_id, customer_phone, field_modified, old_value, new_value, modification_date)
                VALUES (@BookingId, @CustomerPhone, 'multiple', @OldValue, @NewValue, NOW())",
                new { BookingId = bookingId, CustomerPhone = phone9, OldValue = "N/A", NewValue = modificationsJson });

            // === GET UPDATED BOOKING ===
            var updated = await _bookingRepository.GetBookingByIdAsync(bookingId, ct);

            _logger.LogInformation(
                "Successfully modified booking {BookingId} via AI Agent: {Changes}",
                bookingId, string.Join(", ", changes));

            // === SEND WHATSAPP CONFIRMATION TO CUSTOMER ===
            await SendModificationConfirmationAsync(
                phone9,
                booking.CustomerName,
                updated!,
                changes,
                ct);

            return new ToolResult
            {
                Content = JsonSerializer.Serialize(new
                {
                    success = true,
                    bookingId,
                    message = "Reserva modificada correctamente.",
                    changes = changes,
                    modificationsRemaining = 3 - modCount - 1,
                    updatedBooking = new
                    {
                        date = updated.DateFormatted,
                        time = updated.TimeFormatted,
                        people = updated.PartySize,
                        rice = updated.ArrozType,
                        riceServings = updated.ArrozServings,
                        highChairs = updated.HighChairs,
                        babyStrollers = updated.BabyStrollers
                    }
                })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error modifying booking {BookingId} for {Phone}. Input: {Input}",
                bookingId, phoneNumber, input.GetRawText());
            return new ToolResult { IsError = true, Content = $"Error modifying booking: {ex.Message}" };
        }
    }

    // === create_booking ===

    private async Task<ToolResult> ExecuteCreateBooking(JsonElement input, string phoneNumber, CancellationToken ct)
    {
        var confirmed = input.TryGetProperty("confirmed", out var conf) && conf.ValueKind == JsonValueKind.True;

        if (!confirmed)
        {
            return new ToolResult { IsError = true, Content = "Booking not confirmed. Set 'confirmed': true to create the booking." };
        }

        // Extract booking data
        var name = input.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : "Cliente WhatsApp";
        var dateStr = input.TryGetProperty("date", out var dateEl) ? dateEl.GetString() : null;
        var timeStr = input.TryGetProperty("time", out var timeEl) ? timeEl.GetString() : null;
        var people = input.TryGetProperty("people", out var peopleEl) && peopleEl.ValueKind == JsonValueKind.Number
            ? peopleEl.GetInt32() : 0;
        var riceType = input.TryGetProperty("rice_type", out var riceEl) ? riceEl.GetString() : null;
        var riceServings = input.TryGetProperty("rice_servings", out var servingsEl) && servingsEl.ValueKind == JsonValueKind.Number
            ? servingsEl.GetInt32() : (int?)null;
        var highChairs = input.TryGetProperty("high_chairs", out var chairsEl) && chairsEl.ValueKind == JsonValueKind.Number
            ? chairsEl.GetInt32() : 0;
        var babyStrollers = input.TryGetProperty("baby_strollers", out var strollersEl) && strollersEl.ValueKind == JsonValueKind.Number
            ? strollersEl.GetInt32() : 0;

        // === VALIDATION ===

        // 1. Validate required fields
        if (string.IsNullOrEmpty(dateStr) || string.IsNullOrEmpty(timeStr))
        {
            return new ToolResult { IsError = true, Content = "Missing required fields: date and time are required." };
        }

        var countValidationError = ValidateBookingCounts(people, highChairs, babyStrollers, riceServings);
        if (countValidationError != null)
            return new ToolResult { IsError = true, Content = countValidationError };

        if (!string.IsNullOrWhiteSpace(riceType) && !riceServings.HasValue)
            return new ToolResult { IsError = true, Content = "Falta rice_servings. Pregunta al cliente cuántas raciones quiere; no lo supongas." };
        if (riceServings.HasValue && string.IsNullOrWhiteSpace(riceType))
            return new ToolResult { IsError = true, Content = "Falta rice_type." };
        if (riceServings is < 2)
            return new ToolResult { IsError = true, Content = "Cada arroz requiere un mínimo de 2 raciones." };
        if (riceServings > people)
            return new ToolResult { IsError = true, Content = $"Las raciones de arroz ({riceServings}) no pueden superar las personas ({people})." };

        // 2. Validate date format and parse
        if (!TryParseDate(dateStr, out var bookingDate))
        {
            return new ToolResult { IsError = true, Content = "Invalid date format. Use YYYY-MM-DD or dd/MM/yyyy." };
        }

        var dbDate = bookingDate.ToString("yyyy-MM-dd");

        // 3. Validate date is not in the past
        var today = DateTime.Now.Date;
        if (bookingDate.Date < today)
        {
            return new ToolResult { IsError = true, Content = $"Cannot create booking for past date: {dateStr}. Bookings cannot be made for dates before {today:dd/MM/yyyy}." };
        }

        // 4. Validate time format
        if (!TimeSpan.TryParse(timeStr, out var bookingTime))
        {
            return new ToolResult { IsError = true, Content = $"Invalid time format: {timeStr}. Use HH:mm format (e.g., 14:00)." };
        }

        var timeKey = $"{bookingTime.Hours:D2}:{bookingTime.Minutes:D2}";

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            if (!string.IsNullOrWhiteSpace(riceType))
            {
                var matchedRice = FindRiceMatch(riceType, await _menuRepository.GetActiveRiceTypesAsync(ct));
                if (matchedRice == null)
                    return new ToolResult { IsError = true, Content = "Arroz no disponible. Usa check_rice_availability y confirma una opción válida." };
                riceType = matchedRice;
            }

            // === CAPACITY VALIDATIONS ===

            // 5. Check if day is closed (restaurant_days table)
            var isClosed = await connection.ExecuteScalarAsync<int?>(@"
                SELECT is_open FROM restaurant_days WHERE date = @Date",
                new { Date = dbDate });

            // Default closed days: Mon=1, Tue=2, Wed=3 (PHP ISO day format)
            var phpDayNum = bookingDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)bookingDate.DayOfWeek;
            var isDefaultClosed = phpDayNum is 1 or 2 or 3;

            if (!isClosed.HasValue && isDefaultClosed)
            {
                // Day is closed by default (Mon/Tue/Wed)
                var dayName = bookingDate.ToString("dddd", new System.Globalization.CultureInfo("es-ES"));
                return new ToolResult { IsError = true, Content = $"El restaurante está cerrado el {dayName}. No se puede crear la reserva." };
            }
            else if (isClosed.HasValue && isClosed == 0)
            {
                // Day is explicitly closed in database
                var dayName = bookingDate.ToString("dddd", new System.Globalization.CultureInfo("es-ES"));
                return new ToolResult { IsError = true, Content = $"El restaurante está cerrado el {dayName} ({dateStr}). No se puede crear la reserva." };
            }

            // 6. Get daily limit (default 45)
            var dailyLimit = await connection.ExecuteScalarAsync<int?>(
                "SELECT dailyLimit FROM reservation_manager WHERE reservationDate = @Date LIMIT 1",
                new { Date = dbDate }) ?? 45;

            // 7. Sum current bookings for the day
            var totalBooked = await connection.ExecuteScalarAsync<int?>(@"
                SELECT SUM(party_size) FROM bookings 
                WHERE reservation_date = @Date AND status IN ('pending', 'confirmed')",
                new { Date = dbDate }) ?? 0;

            var freeSeats = dailyLimit - totalBooked;

            // 8. Validate party_size fits in available capacity
            if (people > freeSeats)
            {
                return new ToolResult
                {
                    IsError = true,
                    Content = $"No hay suficiente capacidad para {people} personas en {dateStr}. " +
                              $"Plazas libres: {freeSeats}. Por favor, elige otro día o reduce el número de personas."
                };
            }

            // 9. Check opening hours and hour configuration for the specific time slot
            var openingHoursJson = await connection.ExecuteScalarAsync<string?>(
                "SELECT hoursarray FROM openinghours WHERE dateselected = @Date LIMIT 1",
                new { Date = dbDate });

            List<string> validHours;
            if (string.IsNullOrWhiteSpace(openingHoursJson))
            {
                // Default hours
                validHours = new List<string> { "13:30", "14:00", "14:30", "15:00", "15:30" };
            }
            else
            {
                try
                {
                    validHours = JsonSerializer.Deserialize<List<string>>(openingHoursJson)
                                 ?? new List<string> { "13:30", "14:00", "14:30", "15:00", "15:30" };
                }
                catch
                {
                    validHours = new List<string> { "13:30", "14:00", "14:30", "15:00", "15:30" };
                }
            }

            // 10. Check if the requested time is in the valid hours list
            if (!validHours.Contains(timeKey))
            {
                var availableSlots = string.Join(", ", validHours);
                return new ToolResult
                {
                    IsError = true,
                    Content = $"La hora {timeStr} no está disponible. Horas disponibles: {availableSlots}. " +
                              $"Por favor, elige una hora de la lista."
                };
            }

            // 11. Check hour-specific capacity from hour_configuration
            var hourConfigJson = await connection.ExecuteScalarAsync<string?>(
                "SELECT hourData FROM hour_configuration WHERE date = @Date LIMIT 1",
                new { Date = dbDate });

            if (!string.IsNullOrWhiteSpace(hourConfigJson))
            {
                try
                {
                    var hourConfig = JsonSerializer.Deserialize<Dictionary<string, HourConfigEntry>>(hourConfigJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (hourConfig != null && hourConfig.TryGetValue(timeKey, out var hourEntry))
                    {
                        // Calculate capacity for this specific hour
                        var hourlyCapacity = (int)Math.Ceiling((hourEntry.Percentage / 100.0) * dailyLimit);

                        // Get bookings for this specific hour
                        var hourBooked = await connection.ExecuteScalarAsync<int?>(@"
                            SELECT SUM(party_size) FROM bookings 
                            WHERE reservation_date = @Date 
                              AND TIME_FORMAT(reservation_time, '%H:%i') = @Hour
                              AND status IN ('pending', 'confirmed')",
                            new { Date = dbDate, Hour = timeKey }) ?? 0;

                        var hourFree = hourlyCapacity - hourBooked;

                        if (people > hourFree)
                        {
                            return new ToolResult
                            {
                                IsError = true,
                                Content = $"No hay suficiente capacidad a las {timeStr} para {people} personas. " +
                                          $"Plazas libres a esa hora: {hourFree}. Por favor, elige otra hora."
                            };
                        }

                        if (hourEntry.IsClosed)
                        {
                            return new ToolResult
                            {
                                IsError = true,
                                Content = $"La hora {timeStr} está cerrada. Por favor, elige otra hora disponible."
                            };
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse hour_configuration for {Date}", dbDate);
                    // Continue with the booking if hour config parsing fails
                }
            }

            // === ALL VALIDATIONS PASSED - CREATE BOOKING ===

            // Normalize phone to 9 digits
            var phone9 = new string(phoneNumber.Where(char.IsDigit).ToArray());
            if (phone9.Length > 9) phone9 = phone9[^9..];

            var booking = new BookingData
            {
                Name = name ?? "Cliente WhatsApp",
                Phone = phone9,
                Date = dbDate,
                Time = timeKey,
                People = people,
                ArrozType = riceType,
                ArrozServings = riceServings,
                HighChairs = highChairs,
                BabyStrollers = babyStrollers
            };

            var bookingId = await _bookingRepository.CreateBookingAsync(booking, ct);

            if (bookingId.HasValue)
            {
                _logger.LogInformation(
                    "Successfully created booking {BookingId} for {People} people on {Date} at {Time} for {Phone}",
                    bookingId.Value, people, dbDate, timeKey, phone9);

                // Send WhatsApp confirmation message
                var whatsappSent = await SendBookingConfirmationAsync(
                    phone9,
                    name ?? "Cliente",
                    bookingDate,
                    timeKey,
                    people,
                    riceType,
                    riceServings,
                    highChairs,
                    babyStrollers,
                    bookingId.Value,
                    ct);

                return new ToolResult
                {
                    Content = JsonSerializer.Serialize(new
                    {
                        success = true,
                        bookingId = bookingId.Value,
                        date = bookingDate.ToString("dd/MM/yyyy"),
                        time = timeKey,
                        people = people,
                        whatsappSent = whatsappSent,
                        message = whatsappSent
                            ? $"Reserva confirmada para {bookingDate:dd/MM/yyyy} a las {timeKey}, {people} personas. Se ha enviado confirmación por WhatsApp."
                            : $"Reserva confirmada para {bookingDate:dd/MM/yyyy} a las {timeKey}, {people} personas."
                    })
                };
            }

            return new ToolResult { IsError = true, Content = "Failed to create booking in database." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating booking for {Date} at {Time}", dateStr, timeStr);
            return new ToolResult { IsError = true, Content = $"Error creating booking: {ex.Message}" };
        }
    }

    // === send_message ===

    private async Task<ToolResult> ExecuteSendMessage(JsonElement input, string phoneNumber, CancellationToken ct)
    {
        var message = input.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
            ? m.GetString()
            : null;

        // Decode Unicode escape sequences (e.g., \u00BF -> ¿, \uD83D\uDCC5 -> 📅)
        if (!string.IsNullOrEmpty(message))
        {
            message = DecodeUnicodeEscapes(message);
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogWarning("send_message called with empty message for {Phone}", phoneNumber);
            return new ToolResult { IsError = true, Content = "Missing 'message' parameter" };
        }

        _logger.LogInformation(
            "ToolExecutor: Sending message to {Phone}: {Preview}",
            phoneNumber,
            message.Length > 100 ? message[..100] + "..." : message);

        var success = await _whatsApp.SendTextAsync(phoneNumber, message, ct);

        if (success)
        {
            return new ToolResult { Content = $"Message sent successfully to {phoneNumber}" };
        }

        return new ToolResult { IsError = true, Content = $"Failed to send message to {phoneNumber}" };
    }

    // ========================================================================
    // NEW TOOLS FOR SIMPLIFIED AGENT ARCHITECTURE
    // ========================================================================

    // === check_future_booking ===

    /// <summary>
    /// Checks if user has any future bookings.
    /// </summary>
    private async Task<ToolResult> ExecuteCheckFutureBooking(JsonElement input, string phoneNumber, CancellationToken ct)
    {
        var phone9 = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (phone9.Length > 9) phone9 = phone9[^9..];

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            // Get all future bookings for this phone
            var sql = @"
                SELECT id, reservation_date, reservation_time, party_size, arroz_type
                FROM bookings
                WHERE contact_phone = @Phone
                  AND reservation_date >= CURDATE()
                  AND status IN ('pending', 'confirmed')
                ORDER BY reservation_date ASC, reservation_time ASC
                LIMIT 5";

            var rows = await connection.QueryAsync<dynamic>(sql, new { Phone = phone9 });
            var bookings = rows.ToList();

            if (bookings.Count == 0)
            {
                return new ToolResult
                {
                    Content = JsonSerializer.Serialize(new FutureBookingResult
                    {
                        HasFutureBooking = false,
                        BookingCount = 0,
                        NextBooking = null
                    })
                };
            }

            var first = bookings[0];
            var nextBooking = new FutureBookingSummary
            {
                Id = (int)first.id,
                Date = ((DateTime)first.reservation_date).ToString("dd/MM/yyyy"),
                Time = ((TimeSpan)first.reservation_time).ToString(@"hh\:mm"),
                People = (int)first.party_size,
                RiceType = first.arroz_type as string
            };

            return new ToolResult
            {
                Content = JsonSerializer.Serialize(new FutureBookingResult
                {
                    HasFutureBooking = true,
                    BookingCount = bookings.Count,
                    NextBooking = nextBooking
                })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking future bookings for {Phone}", phoneNumber);
            return new ToolResult { IsError = true, Content = $"Error checking bookings: {ex.Message}" };
        }
    }

    // === get_opening_hours_with_capacity ===

    /// <summary>
    /// Gets opening hours with capacity per hour.
    /// 1. Check openinghours table for date
    /// 2. If no row, use default [13:30, 14:00, 14:30, 15:00, 15:30]
    /// 3. Check hour_configuration for capacity
    /// </summary>
    private async Task<ToolResult> ExecuteGetOpeningHoursWithCapacity(JsonElement input, CancellationToken ct)
    {
        var dateStr = input.TryGetProperty("date", out var d) ? d.GetString() : null;
        var partySize = input.TryGetProperty("party_size", out var p) && p.ValueKind == JsonValueKind.Number
            ? p.GetInt32() : (int?)null;

        if (string.IsNullOrEmpty(dateStr))
        {
            return new ToolResult { IsError = true, Content = "Missing 'date' parameter (format: dd/MM/yyyy)" };
        }

        if (!TryParseDate(dateStr, out var date))
        {
            return new ToolResult { IsError = true, Content = "Invalid date format. Use dd/MM/yyyy" };
        }

        var dbDate = date.ToString("yyyy-MM-dd");

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            // Step 1: Get opening hours from openinghours table
            var hoursJson = await connection.ExecuteScalarAsync<string?>(
                "SELECT hoursarray FROM openinghours WHERE dateselected = @Date LIMIT 1",
                new { Date = dbDate });

            List<string> defaultHours;
            string source;

            if (string.IsNullOrWhiteSpace(hoursJson))
            {
                defaultHours = new List<string> { "13:30", "14:00", "14:30", "15:00", "15:30" };
                source = "default";
            }
            else
            {
                try
                {
                    defaultHours = JsonSerializer.Deserialize<List<string>>(hoursJson) ?? new List<string> { "13:30", "14:00", "14:30", "15:00", "15:30" };
                    source = "database";
                }
                catch
                {
                    defaultHours = new List<string> { "13:30", "14:00", "14:30", "15:00", "15:30" };
                    source = "default";
                }
            }

            defaultHours.Sort(StringComparer.Ordinal);

            // Step 2: Get daily limit and total booked
            var dailyLimit = await connection.ExecuteScalarAsync<int?>(
                "SELECT dailyLimit FROM reservation_manager WHERE reservationDate = @Date LIMIT 1",
                new { Date = dbDate }) ?? 45;

            var totalBooked = await connection.ExecuteScalarAsync<int?>(@"
                SELECT SUM(party_size) FROM bookings 
                WHERE reservation_date = @Date AND status IN ('pending', 'confirmed')",
                new { Date = dbDate }) ?? 0;

            // Step 3: Get hour configuration
            var hourConfigJson = await connection.ExecuteScalarAsync<string?>(
                "SELECT hourData FROM hour_configuration WHERE date = @Date LIMIT 1",
                new { Date = dbDate });

            var hourCapacities = new Dictionary<string, (int capacity, int booked, bool isClosed)>();

            if (!string.IsNullOrWhiteSpace(hourConfigJson))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, HourConfigEntry>>(hourConfigJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (parsed != null)
                    {
                        foreach (var (hour, entry) in parsed)
                        {
                            var bookingsForHour = await connection.ExecuteScalarAsync<int?>(@"
                                SELECT SUM(party_size) FROM bookings 
                                WHERE reservation_date = @Date 
                                  AND TIME_FORMAT(reservation_time, '%H:%i') = @Hour
                                  AND status IN ('pending', 'confirmed')",
                                new { Date = dbDate, Hour = hour }) ?? 0;

                            var hourlyCapacity = (int)Math.Ceiling((entry.Percentage / 100.0) * dailyLimit);
                            hourCapacities[hour] = (hourlyCapacity, bookingsForHour, entry.IsClosed);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse hour_configuration JSON for {Date}", dbDate);
                }
            }
            else
            {
                // No hour configuration - distribute evenly
                var perHourCapacity = dailyLimit / defaultHours.Count;
                foreach (var hour in defaultHours)
                {
                    if (!hourCapacities.ContainsKey(hour))
                    {
                        hourCapacities[hour] = (perHourCapacity, 0, false);
                    }
                }
            }

            // Build hour results
            var hourResults = new List<HourCapacityResult>();
            foreach (var hour in defaultHours)
            {
                var (capacity, booked, isClosed) = hourCapacities.TryGetValue(hour, out var h) ? h : (dailyLimit / defaultHours.Count, 0, false);
                var free = Math.Max(0, capacity - booked);
                var available = !isClosed && free > 0;

                // If party_size specified, check if it fits
                if (partySize.HasValue && available && free < partySize.Value)
                {
                    available = false;
                }

                hourResults.Add(new HourCapacityResult
                {
                    Hour = hour,
                    Available = available,
                    Capacity = capacity,
                    Booked = booked,
                    Free = free,
                    IsClosed = isClosed
                });
            }

            var totalCapacity = hourResults.Sum(h => h.Capacity);
            var totalFree = hourResults.Sum(h => h.Free);

            return new ToolResult
            {
                Content = JsonSerializer.Serialize(new OpeningHoursWithCapacityResult
                {
                    Date = dateStr!,
                    Source = source,
                    DefaultHours = defaultHours,
                    Hours = hourResults,
                    TotalCapacity = totalCapacity,
                    TotalBooked = totalBooked,
                    TotalFree = totalFree
                })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting opening hours with capacity for {Date}", dateStr);
            return new ToolResult { IsError = true, Content = $"Error: {ex.Message}" };
        }
    }

    // === check_hour_capacity ===

    /// <summary>
    /// Checks only hour_configuration table (independent of openinghours).
    /// For when party_size is not yet known.
    /// </summary>
    private async Task<ToolResult> ExecuteCheckHourCapacity(JsonElement input, CancellationToken ct)
    {
        var dateStr = input.TryGetProperty("date", out var d) ? d.GetString() : null;

        if (string.IsNullOrEmpty(dateStr))
        {
            return new ToolResult { IsError = true, Content = "Missing 'date' parameter (format: dd/MM/yyyy)" };
        }

        if (!TryParseDate(dateStr, out var date))
        {
            return new ToolResult { IsError = true, Content = "Invalid date format. Use dd/MM/yyyy" };
        }

        var dbDate = date.ToString("yyyy-MM-dd");

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            var hourConfigJson = await connection.ExecuteScalarAsync<string?>(
                "SELECT hourData FROM hour_configuration WHERE date = @Date LIMIT 1",
                new { Date = dbDate });

            if (string.IsNullOrWhiteSpace(hourConfigJson))
            {
                return new ToolResult
                {
                    Content = JsonSerializer.Serialize(new HourConfigurationResult
                    {
                        Date = dateStr!,
                        HasCustomConfig = false,
                        HourData = new Dictionary<string, HourConfigSlot>()
                    })
                };
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, HourConfigEntry>>(hourConfigJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var dailyLimit = await connection.ExecuteScalarAsync<int?>(
                    "SELECT dailyLimit FROM reservation_manager WHERE reservationDate = @Date LIMIT 1",
                    new { Date = dbDate }) ?? 45;

                var hourData = new Dictionary<string, HourConfigSlot>();

                if (parsed != null)
                {
                    foreach (var (hour, entry) in parsed)
                    {
                        var bookingsForHour = await connection.ExecuteScalarAsync<int?>(@"
                            SELECT SUM(party_size) FROM bookings 
                            WHERE reservation_date = @Date 
                              AND TIME_FORMAT(reservation_time, '%H:%i') = @Hour
                              AND status IN ('pending', 'confirmed')",
                            new { Date = dbDate, Hour = hour }) ?? 0;

                        var hourlyCapacity = (int)Math.Ceiling((entry.Percentage / 100.0) * dailyLimit);

                        hourData[hour] = new HourConfigSlot
                        {
                            Capacity = hourlyCapacity,
                            Bookings = bookingsForHour,
                            Percentage = entry.Percentage,
                            IsClosed = entry.IsClosed,
                            Status = entry.Status
                        };
                    }
                }

                return new ToolResult
                {
                    Content = JsonSerializer.Serialize(new HourConfigurationResult
                    {
                        Date = dateStr!,
                        HasCustomConfig = true,
                        HourData = hourData
                    })
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse hour_configuration JSON for {Date}", dbDate);
                return new ToolResult
                {
                    Content = JsonSerializer.Serialize(new HourConfigurationResult
                    {
                        Date = dateStr!,
                        HasCustomConfig = false,
                        HourData = new Dictionary<string, HourConfigSlot>()
                    })
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking hour capacity for {Date}", dateStr);
            return new ToolResult { IsError = true, Content = $"Error: {ex.Message}" };
        }
    }

    // === check_day_capacity ===

    /// <summary>
    /// Quick check if day is open, full, or closed.
    /// Sums bookings.party_size and compares with daily_limits.
    /// </summary>
    private async Task<ToolResult> ExecuteCheckDayCapacity(JsonElement input, CancellationToken ct)
    {
        var dateStr = input.TryGetProperty("date", out var d) ? d.GetString() : null;

        if (string.IsNullOrEmpty(dateStr))
        {
            return new ToolResult { IsError = true, Content = "Missing 'date' parameter (format: dd/MM/yyyy)" };
        }

        if (!TryParseDate(dateStr, out var date))
        {
            return new ToolResult { IsError = true, Content = "Invalid date format. Use dd/MM/yyyy" };
        }

        var dbDate = date.ToString("yyyy-MM-dd");

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            // Check if day is closed in restaurant_days
            var isClosed = await connection.ExecuteScalarAsync<int?>(@"
                SELECT is_open FROM restaurant_days WHERE date = @Date",
                new { Date = dbDate });

            // Default closed days: Mon=1, Tue=2, Wed=3 (PHP day numbers)
            var phpDayNum = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
            var isDefaultClosed = phpDayNum is 1 or 2 or 3;

            // If no row in restaurant_days, use default (closed Mon/Tue/Wed)
            if (!isClosed.HasValue && isDefaultClosed)
            {
                isClosed = 0;
            }
            else if (!isClosed.HasValue)
            {
                isClosed = 1; // Open by default
            }

            if (isClosed == 0)
            {
                return new ToolResult
                {
                    Content = JsonSerializer.Serialize(new DayCapacityResult
                    {
                        Date = dateStr!,
                        Status = "closed",
                        DailyLimit = 0,
                        TotalBooked = 0,
                        FreeSeats = 0,
                        IsFull = true
                    })
                };
            }

            // Get daily limit (default 45)
            var dailyLimit = await connection.ExecuteScalarAsync<int?>(
                "SELECT dailyLimit FROM reservation_manager WHERE reservationDate = @Date LIMIT 1",
                new { Date = dbDate }) ?? 45;

            // Sum bookings
            var totalBooked = await connection.ExecuteScalarAsync<int?>(@"
                SELECT SUM(party_size) FROM bookings 
                WHERE reservation_date = @Date AND status IN ('pending', 'confirmed')",
                new { Date = dbDate }) ?? 0;

            var freeSeats = Math.Max(0, dailyLimit - totalBooked);
            var isFull = totalBooked >= dailyLimit;

            return new ToolResult
            {
                Content = JsonSerializer.Serialize(new DayCapacityResult
                {
                    Date = dateStr!,
                    Status = isFull ? "full" : "open",
                    DailyLimit = dailyLimit,
                    TotalBooked = totalBooked,
                    FreeSeats = freeSeats,
                    IsFull = isFull
                })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking day capacity for {Date}", dateStr);
            return new ToolResult { IsError = true, Content = $"Error: {ex.Message}" };
        }
    }

    // === check_availability_for_party ===

    /// <summary>
    /// Checks if specific party_size fits on given date.
    /// </summary>
    private async Task<ToolResult> ExecuteCheckAvailabilityForParty(JsonElement input, CancellationToken ct)
    {
        var dateStr = input.TryGetProperty("date", out var d) ? d.GetString() : null;
        var partySize = input.TryGetProperty("party_size", out var p) && p.ValueKind == JsonValueKind.Number
            ? p.GetInt32() : (int?)null;

        if (string.IsNullOrEmpty(dateStr))
        {
            return new ToolResult { IsError = true, Content = "Missing 'date' parameter (format: dd/MM/yyyy)" };
        }

        if (!partySize.HasValue || partySize.Value <= 0)
        {
            return new ToolResult { IsError = true, Content = "Missing or invalid 'party_size' parameter" };
        }

        if (!TryParseDate(dateStr, out var date))
        {
            return new ToolResult { IsError = true, Content = "Invalid date format. Use dd/MM/yyyy" };
        }

        var dbDate = date.ToString("yyyy-MM-dd");

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            // Check if day is closed
            var isClosed = await connection.ExecuteScalarAsync<int?>(@"
                SELECT is_open FROM restaurant_days WHERE date = @Date",
                new { Date = dbDate });

            var phpDayNum = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
            var isDefaultClosed = phpDayNum is 1 or 2 or 3;

            if (!isClosed.HasValue && isDefaultClosed)
            {
                isClosed = 0;
            }
            else if (!isClosed.HasValue)
            {
                isClosed = 1;
            }

            if (isClosed == 0)
            {
                return new ToolResult
                {
                    Content = JsonSerializer.Serialize(new AvailabilityForPartyResult
                    {
                        Date = dateStr!,
                        PartySize = partySize.Value,
                        Fits = false,
                        DailyLimit = 0,
                        TotalBooked = 0,
                        FreeSeats = 0,
                        Message = "El restaurante está cerrado este día"
                    })
                };
            }

            // Get daily limit
            var dailyLimit = await connection.ExecuteScalarAsync<int?>(
                "SELECT dailyLimit FROM reservation_manager WHERE reservationDate = @Date LIMIT 1",
                new { Date = dbDate }) ?? 45;

            // Sum bookings
            var totalBooked = await connection.ExecuteScalarAsync<int?>(@"
                SELECT SUM(party_size) FROM bookings 
                WHERE reservation_date = @Date AND status IN ('pending', 'confirmed')",
                new { Date = dbDate }) ?? 0;

            var freeSeats = Math.Max(0, dailyLimit - totalBooked);
            var fits = partySize.Value <= freeSeats;

            string message;
            if (fits)
            {
                message = $"Hay sitio para {partySize.Value} personas. Plazas libres: {freeSeats}";
            }
            else
            {
                message = $"No hay sitio para {partySize.Value} personas. Plazas libres: {freeSeats}";
            }

            return new ToolResult
            {
                Content = JsonSerializer.Serialize(new AvailabilityForPartyResult
                {
                    Date = dateStr!,
                    PartySize = partySize.Value,
                    Fits = fits,
                    DailyLimit = dailyLimit,
                    TotalBooked = totalBooked,
                    FreeSeats = freeSeats,
                    Message = message
                })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking availability for party on {Date}", dateStr);
            return new ToolResult { IsError = true, Content = $"Error: {ex.Message}" };
        }
    }

    // ========================================================================
    // HELPER METHODS
    // ========================================================================

    private static bool TryParseDate(string dateStr, out DateTime date)
    {
        date = default;

        // Try dd/MM/yyyy
        if (DateTime.TryParseExact(dateStr, "dd/MM/yyyy",
            new System.Globalization.CultureInfo("es-ES"),
            System.Globalization.DateTimeStyles.None, out date))
        {
            return true;
        }

        // Try yyyy-MM-dd
        if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out date))
        {
            return true;
        }

        // Try general parse
        return DateTime.TryParse(dateStr, out date);
    }

    private class HourConfigEntry
    {
        public int Capacity { get; set; }
        public int Bookings { get; set; }
        public double Percentage { get; set; }
        public bool IsClosed { get; set; }
        public string? Status { get; set; }
    }

    // ========================================================================
    // WHATSAPP CONFIRMATION HELPER
    // ========================================================================

    /// <summary>
    /// Sends a booking confirmation WhatsApp message with buttons, similar to insert_booking_front.php.
    /// </summary>
    private async Task<bool> SendBookingConfirmationAsync(
        string phoneNumber,
        string customerName,
        DateTime bookingDate,
        string bookingTime,
        int guestCount,
        string? arrozType,
        int? arrozServings,
        int highChairs,
        int babyStrollers,
        long bookingId,
        CancellationToken ct)
    {
        try
        {
            // Format date as DD/MM/YYYY
            var formattedDate = bookingDate.ToString("dd/MM/yyyy");

            // Build confirmation text (similar to PHP sendWhatsAppConfirmationWithButtonsUazApi)
            var confirmationText = $"*Confirmación de Reserva - Alquería Villa Carmen*\n\n";
            confirmationText += $"Hola {customerName},\n\n";
            confirmationText += "Gracias por elegir Alquería Villa Carmen. Su reserva ha sido confirmada:\n\n";
            confirmationText += $"📅 *Fecha:* {formattedDate}\n";
            confirmationText += $"🕒 *Hora:* {bookingTime}\n";
            confirmationText += $"👥 *Personas:* {guestCount}\n";

            // Rice section
            if (!string.IsNullOrWhiteSpace(arrozType))
            {
                var servings = arrozServings.HasValue ? arrozServings.Value.ToString() : "";
                if (!string.IsNullOrEmpty(servings))
                {
                    confirmationText += $"🍚 *Arroz:* {arrozType} ({servings} raciones)\n";
                }
                else
                {
                    confirmationText += $"🍚 *Arroz:* {arrozType}\n";
                }
            }
            else
            {
                confirmationText += "🍚 *Arroz:* No\n";
            }

            confirmationText += $"👶 *Tronas:* {highChairs}\n";
            confirmationText += $"🍼 *Carros de bebé:* {babyStrollers}\n\n";
            confirmationText += "Al hacer esta reserva, usted ha confirmado y aceptado las condiciones de reserva y políticas del restaurante, las cuales puede consultar en el botón de abajo.";

            // Build choices for buttons
            var buttons = new List<LinkButtonOption>
            {
                new LinkButtonOption(
                    "CONDICIONES",
                    "https://alqueriavillacarmen.com/booking_policies.php"),
                new LinkButtonOption(
                    "Cancelar Reserva",
                    $"https://alqueriavillacarmen.com/cancel_reservation.php?id={bookingId}")
            };

            // Send with buttons via WhatsApp service
            var success = await _whatsApp.SendLinkButtonsAsync(
                phoneNumber,
                confirmationText,
                buttons,
                ct);

            if (success)
            {
                _logger.LogInformation(
                    "Sent booking confirmation WhatsApp to {Phone} for booking {BookingId}",
                    phoneNumber, bookingId);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to send booking confirmation WhatsApp to {Phone} for booking {BookingId}",
                    phoneNumber, bookingId);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error sending booking confirmation WhatsApp to {Phone} for booking {BookingId}",
                phoneNumber, bookingId);
            return false;
        }
    }

    // ========================================================================
    // MODIFICATION CONFIRMATION HELPER
    // ========================================================================

    /// <summary>
    /// Sends a WhatsApp confirmation message after a booking modification.
    /// </summary>
    private async Task<bool> SendModificationConfirmationAsync(
        string phoneNumber,
        string customerName,
        BookingRecord updatedBooking,
        List<string> changes,
        CancellationToken ct)
    {
        try
        {
            var formattedDate = updatedBooking.ReservationDate.ToString("dd/MM/yyyy");
            var formattedTime = updatedBooking.ReservationTime.ToString(@"hh\:mm");

            var confirmationText = "*Modificación de Reserva - Alquería Villa Carmen*\n\n";
            confirmationText += $"Hola {customerName},\n\n";
            confirmationText += "Te confirmamos que tu reserva ha sido modificada:\n\n";
            confirmationText += $"📅 *Fecha:* {formattedDate}\n";
            confirmationText += $"🕒 *Hora:* {formattedTime}\n";
            confirmationText += $"👥 *Personas:* {updatedBooking.PartySize}\n";

            if (!string.IsNullOrWhiteSpace(updatedBooking.ArrozType))
            {
                confirmationText += $"🍚 *Arroz:* {updatedBooking.ArrozType}";
                if (updatedBooking.ArrozServings.HasValue)
                {
                    confirmationText += $" ({updatedBooking.ArrozServings} raciones)";
                }
                confirmationText += "\n";
            }
            else
            {
                confirmationText += "🍚 *Arroz:* No\n";
            }

            confirmationText += $"👶 *Tronas:* {updatedBooking.HighChairs}\n";
            confirmationText += $"🍼 *Carros de bebé:* {updatedBooking.BabyStrollers}\n\n";

            // List changes
            if (changes.Count > 0)
            {
                confirmationText += "*Cambios realizados:*\n";
                foreach (var change in changes)
                {
                    confirmationText += $"• {change}\n";
                }
                confirmationText += "\n";
            }

            confirmationText += "Si necesitas hacer más cambios, contacta con el restaurante.";

            var success = await _whatsApp.SendTextAsync(phoneNumber, confirmationText, ct);

            if (success)
            {
                _logger.LogInformation(
                    "Sent modification confirmation WhatsApp to {Phone} for booking {BookingId}",
                    phoneNumber, updatedBooking.Id);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to send modification confirmation WhatsApp to {Phone}",
                    phoneNumber);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error sending modification confirmation WhatsApp to {Phone}",
                phoneNumber);
            return false;
        }
    }

    /// <summary>
    /// Decode Unicode escape sequences like \u00BF to actual UTF-8 characters.
    /// The AI may return escaped Unicode that needs decoding before sending to WhatsApp.
    /// </summary>
    private static string DecodeUnicodeEscapes(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        try
        {
            // Match \uXXXX patterns and decode them
            return System.Text.RegularExpressions.Regex.Replace(
                text,
                @"\\u([0-9A-Fa-f]{4})",
                match =>
                {
                    var codePoint = int.Parse(match.Groups[1].Value, System.Globalization.NumberStyles.HexNumber);
                    return char.ConvertFromUtf32(codePoint);
                });
        }
        catch
        {
            return text;
        }
    }
}
