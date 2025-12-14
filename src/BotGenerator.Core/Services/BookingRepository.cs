using BotGenerator.Core.Models;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace BotGenerator.Core.Services;

/// <summary>
/// MySQL implementation of IBookingRepository.
/// </summary>
public class BookingRepository : IBookingRepository
{
    private readonly string _connectionString;
    private readonly ILogger<BookingRepository> _logger;

    public BookingRepository(IConfiguration configuration, ILogger<BookingRepository> logger)
    {
        _connectionString = configuration["MySQL:ConnectionString"]
            ?? throw new InvalidOperationException("MySQL:ConnectionString not configured");
        _logger = logger;
    }

    public async Task<long?> CreateBookingAsync(BookingData booking, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                INSERT INTO bookings (
                    customer_name,
                    contact_email,
                    reservation_date,
                    reservation_time,
                    party_size,
                    contact_phone,
                    commentary,
                    babyStrollers,
                    highChairs,
                    arroz_type,
                    arroz_servings,
                    status,
                    re_confirmation,
                    reminder_sent,
                    rice_reminder_sent
                ) VALUES (
                    @CustomerName,
                    @ContactEmail,
                    @ReservationDate,
                    @ReservationTime,
                    @PartySize,
                    @ContactPhone,
                    @Commentary,
                    @BabyStrollers,
                    @HighChairs,
                    CASE
                        WHEN @ArrozType IS NULL OR @ArrozType = '' THEN NULL
                        ELSE JSON_ARRAY(@ArrozType)
                    END,
                    CASE
                        WHEN @ArrozServings IS NULL THEN NULL
                        ELSE JSON_ARRAY(@ArrozServings)
                    END,
                    @Status,
                    @ReConfirmation,
                    0,
                    0
                );
                SELECT LAST_INSERT_ID();";

            var dbDate = booking.DateForDatabase;
            if (string.IsNullOrWhiteSpace(dbDate))
            {
                _logger.LogWarning("Cannot create booking: invalid date '{Date}'", booking.Date);
                return null;
            }

            // Normalize phone number for DB column (varchar(9))
            var phone = NormalizePhoneForDb(booking.Phone);

            // Store no-rice as NULL
            var arrozType = string.IsNullOrWhiteSpace(booking.ArrozType) ? null : booking.ArrozType;
            var arrozServings = arrozType == null ? null : booking.ArrozServings;

            var parameters = new
            {
                CustomerName = booking.Name,
                // bookings.contact_email is NOT NULL in schema
                ContactEmail = "whatsapp@bot.local",
                ReservationDate = dbDate,
                // bookings.reservation_time is TIME; accept HH:mm or HH:mm:ss
                ReservationTime = NormalizeTimeForDb(booking.Time),
                PartySize = booking.People,
                ContactPhone = phone,
                Commentary = booking.Commentary,
                BabyStrollers = booking.BabyStrollers,
                HighChairs = booking.HighChairs,
                ArrozType = arrozType,
                ArrozServings = arrozServings,
                Status = "pending",
                ReConfirmation = 1
            };

            var bookingId = await connection.ExecuteScalarAsync<long>(sql, parameters);

            _logger.LogInformation(
                "Created booking {BookingId} for {Name} on {Date} at {Time}",
                bookingId, booking.Name, booking.Date, booking.Time);

            return bookingId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating booking in database for {Name}", booking.Name);
            return null;
        }
    }

    public async Task<bool> BookingExistsAsync(string date, string time, string phone, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Convert date from dd/MM/yyyy to yyyy-MM-dd if needed
            var dbDate = date;
            if (date.Contains('/'))
            {
                var parts = date.Split('/');
                if (parts.Length == 3)
                {
                    dbDate = $"{parts[2]}-{parts[1].PadLeft(2, '0')}-{parts[0].PadLeft(2, '0')}";
                }
            }

            var sql = @"
                SELECT COUNT(*)
                FROM bookings
                WHERE reservation_date = @Fecha
                  AND reservation_time = @Hora
                  AND contact_phone = @Telefono";

            var count = await connection.ExecuteScalarAsync<int>(sql, new
            {
                Fecha = dbDate,
                Hora = NormalizeTimeForDb(time),
                Telefono = NormalizePhoneForDb(phone)
            });

            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if booking exists");
            return false;
        }
    }

    private static string? NormalizePhoneForDb(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;

        // Keep digits only
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length <= 9) return digits;

        // Keep last 9 digits (e.g., remove country code 34)
        return digits[^9..];
    }

    private static string NormalizeTimeForDb(string time)
    {
        if (string.IsNullOrWhiteSpace(time)) return "00:00:00";

        // Accept HH:mm or HH:mm:ss
        if (TimeSpan.TryParse(time, out var ts))
        {
            return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        // Fallback: if it's already a TIME string like "15:00:00"
        return time.Length == 5 ? time + ":00" : time;
    }

    public async Task<List<BookingRecord>> FindBookingsByPhoneAsync(string phone9Digits, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Normalize phone to last 9 digits
            var normalizedPhone = NormalizePhoneForDb(phone9Digits);

            var sql = @"
                SELECT
                    id,
                    customer_name,
                    reservation_date,
                    reservation_time,
                    party_size,
                    JSON_UNQUOTE(JSON_EXTRACT(arroz_type, '$[0]')) as arroz_type,
                    JSON_UNQUOTE(JSON_EXTRACT(arroz_servings, '$[0]')) as arroz_servings,
                    highChairs,
                    babyStrollers,
                    contact_phone
                FROM bookings
                WHERE contact_phone = @Phone
                  AND reservation_date >= CURDATE()
                  AND (status IS NULL OR status != 'cancelled')
                ORDER BY reservation_date ASC, reservation_time ASC";

            var results = await connection.QueryAsync<dynamic>(sql, new { Phone = normalizedPhone });

            var bookings = new List<BookingRecord>();
            foreach (var row in results)
            {
                int? arrozServings = null;
                if (row.arroz_servings != null)
                {
                    if (int.TryParse(row.arroz_servings.ToString(), out int servings))
                    {
                        arrozServings = servings;
                    }
                }

                bookings.Add(new BookingRecord
                {
                    Id = (int)row.id,
                    CustomerName = row.customer_name ?? "",
                    ReservationDate = row.reservation_date,
                    ReservationTime = row.reservation_time,
                    PartySize = (int)row.party_size,
                    ArrozType = row.arroz_type as string,
                    ArrozServings = arrozServings,
                    HighChairs = (int)(row.highChairs ?? 0),
                    BabyStrollers = (int)(row.babyStrollers ?? 0),
                    ContactPhone = row.contact_phone ?? ""
                });
            }

            _logger.LogInformation(
                "Found {Count} future bookings for phone {Phone}",
                bookings.Count, normalizedPhone);

            return bookings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding bookings by phone {Phone}", phone9Digits);
            return new List<BookingRecord>();
        }
    }

    public async Task<BookingRecord?> GetBookingByIdAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                SELECT
                    id,
                    customer_name,
                    reservation_date,
                    reservation_time,
                    party_size,
                    JSON_UNQUOTE(JSON_EXTRACT(arroz_type, '$[0]')) as arroz_type,
                    JSON_UNQUOTE(JSON_EXTRACT(arroz_servings, '$[0]')) as arroz_servings,
                    highChairs,
                    babyStrollers,
                    contact_phone
                FROM bookings
                WHERE id = @BookingId";

            var row = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { BookingId = bookingId });

            if (row == null)
            {
                return null;
            }

            int? arrozServings = null;
            if (row.arroz_servings != null)
            {
                if (int.TryParse(row.arroz_servings.ToString(), out int servings))
                {
                    arrozServings = servings;
                }
            }

            return new BookingRecord
            {
                Id = (int)row.id,
                CustomerName = row.customer_name ?? "",
                ReservationDate = row.reservation_date,
                ReservationTime = row.reservation_time,
                PartySize = (int)row.party_size,
                ArrozType = row.arroz_type as string,
                ArrozServings = arrozServings,
                HighChairs = (int)(row.highChairs ?? 0),
                BabyStrollers = (int)(row.babyStrollers ?? 0),
                ContactPhone = row.contact_phone ?? ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting booking by ID {BookingId}", bookingId);
            return null;
        }
    }

    public async Task<bool> UpdateBookingAsync(int bookingId, BookingUpdateData data, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Build dynamic UPDATE query based on which fields are provided
            var setClauses = new List<string>();
            var parameters = new DynamicParameters();
            parameters.Add("BookingId", bookingId);

            if (data.ReservationDate != null)
            {
                setClauses.Add("reservation_date = @ReservationDate");
                parameters.Add("ReservationDate", data.ReservationDate);
            }

            if (data.ReservationTime != null)
            {
                setClauses.Add("reservation_time = @ReservationTime");
                parameters.Add("ReservationTime", data.ReservationTime);
            }

            if (data.PartySize.HasValue)
            {
                setClauses.Add("party_size = @PartySize");
                parameters.Add("PartySize", data.PartySize.Value);
            }

            if (data.ClearRice)
            {
                // Explicitly clear rice
                setClauses.Add("arroz_type = NULL");
                setClauses.Add("arroz_servings = NULL");
            }
            else
            {
                // Handle rice type update
                if (data.ArrozType != null)
                {
                    if (string.IsNullOrWhiteSpace(data.ArrozType))
                    {
                        setClauses.Add("arroz_type = NULL");
                        setClauses.Add("arroz_servings = NULL");
                    }
                    else
                    {
                        setClauses.Add("arroz_type = JSON_ARRAY(@ArrozType)");
                        parameters.Add("ArrozType", data.ArrozType);
                    }
                }

                // Handle rice servings update
                if (data.ArrozServings.HasValue)
                {
                    setClauses.Add("arroz_servings = JSON_ARRAY(@ArrozServings)");
                    parameters.Add("ArrozServings", data.ArrozServings.Value);
                }
            }

            if (data.HighChairs.HasValue)
            {
                setClauses.Add("highChairs = @HighChairs");
                parameters.Add("HighChairs", data.HighChairs.Value);
            }

            if (data.BabyStrollers.HasValue)
            {
                setClauses.Add("babyStrollers = @BabyStrollers");
                parameters.Add("BabyStrollers", data.BabyStrollers.Value);
            }

            if (setClauses.Count == 0)
            {
                _logger.LogWarning("UpdateBookingAsync called with no fields to update for booking {BookingId}", bookingId);
                return false;
            }

            var sql = $@"
                UPDATE bookings
                SET {string.Join(", ", setClauses)}
                WHERE id = @BookingId";

            var rowsAffected = await connection.ExecuteAsync(sql, parameters);

            if (rowsAffected > 0)
            {
                _logger.LogInformation(
                    "Updated booking {BookingId}: {Fields}",
                    bookingId, string.Join(", ", setClauses));
                return true;
            }

            _logger.LogWarning("No rows updated for booking {BookingId}", bookingId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating booking {BookingId}", bookingId);
            return false;
        }
    }

    public async Task<bool> CancelBookingAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                UPDATE bookings
                SET status = 'cancelled'
                WHERE id = @BookingId";

            var rowsAffected = await connection.ExecuteAsync(sql, new { BookingId = bookingId });

            if (rowsAffected > 0)
            {
                _logger.LogInformation("Cancelled booking {BookingId}", bookingId);
                return true;
            }

            _logger.LogWarning("No rows updated when cancelling booking {BookingId}", bookingId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling booking {BookingId}", bookingId);
            return false;
        }
    }

    public async Task<bool> InsertCancelledBookingAsync(BookingRecord booking, string cancelledBy, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                INSERT INTO cancelled_bookings
                (booking_id, reservation_date, reservation_time, party_size, customer_name,
                 contact_phone, contact_email, cancellation_date, cancelled_by,
                 arroz_type, arroz_servings, babyStrollers, highChairs, commentary)
                VALUES
                (@BookingId, @ReservationDate, @ReservationTime, @PartySize, @CustomerName,
                 @ContactPhone, @ContactEmail, NOW(), @CancelledBy,
                 @ArrozType, @ArrozServings, @BabyStrollers, @HighChairs, @Commentary)";

            var parameters = new
            {
                BookingId = booking.Id,
                ReservationDate = booking.ReservationDate,
                ReservationTime = booking.ReservationTime,
                PartySize = booking.PartySize,
                CustomerName = booking.CustomerName,
                ContactPhone = booking.ContactPhone,
                ContactEmail = "whatsapp@bot.local",
                CancelledBy = cancelledBy,
                ArrozType = booking.ArrozType,
                ArrozServings = booking.ArrozServings,
                BabyStrollers = booking.BabyStrollers,
                HighChairs = booking.HighChairs,
                Commentary = (string?)null
            };

            var rowsAffected = await connection.ExecuteAsync(sql, parameters);

            if (rowsAffected > 0)
            {
                _logger.LogInformation(
                    "Inserted cancelled booking record for booking {BookingId}, cancelled by {CancelledBy}",
                    booking.Id, cancelledBy);
                return true;
            }

            _logger.LogWarning("Failed to insert cancelled booking record for {BookingId}", booking.Id);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inserting cancelled booking record for {BookingId}", booking.Id);
            return false;
        }
    }
}
