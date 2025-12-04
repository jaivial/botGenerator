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
            ?? "Server=localhost;Database=villacarmen;User=root;Password=123123;";
        _logger = logger;
    }

    public async Task<long?> CreateBookingAsync(BookingData booking, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                INSERT INTO RESERVAS (
                    fecha, hora, personas, nombre, telefono,
                    arroz_tipo, arroz_raciones, tronas, carritos,
                    comentario, created_at, source
                ) VALUES (
                    @Fecha, @Hora, @Personas, @Nombre, @Telefono,
                    @ArrozTipo, @ArrozRaciones, @Tronas, @Carritos,
                    @Comentario, NOW(), 'whatsapp_bot'
                );
                SELECT LAST_INSERT_ID();";

            var parameters = new
            {
                Fecha = booking.DateForDatabase,
                Hora = booking.Time,
                Personas = booking.People,
                Nombre = booking.Name,
                Telefono = booking.Phone,
                ArrozTipo = booking.ArrozType,
                ArrozRaciones = booking.ArrozServings,
                Tronas = booking.HighChairs,
                Carritos = booking.BabyStrollers,
                Comentario = booking.Commentary
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
                FROM RESERVAS
                WHERE fecha = @Fecha AND hora = @Hora AND telefono = @Telefono";

            var count = await connection.ExecuteScalarAsync<int>(sql, new { Fecha = dbDate, Hora = time, Telefono = phone });

            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if booking exists");
            return false;
        }
    }
}
