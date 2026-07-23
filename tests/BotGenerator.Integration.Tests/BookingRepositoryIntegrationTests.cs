using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace BotGenerator.Integration.Tests;

[CollectionDefinition("booking-db", DisableParallelization = true)]
public class BookingDatabaseCollection;

[Collection("booking-db")]
public class BookingRepositoryIntegrationTests
{
    private const string TestPhone = "+34 692747052";
    private readonly string? _connectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING");

    private BookingRepository Repository() => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["MySQL:ConnectionString"] = _connectionString }).Build(),
        new FailingLogger<BookingRepository>());

    private sealed class FailingLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
                throw new InvalidOperationException(formatter(state, exception), exception);
        }
    }

    private BookingData Booking(bool rice = true) => new()
    {
        Name = "Test columnas 692747052",
        Phone = TestPhone,
        Date = DateTime.Today.AddYears(5).ToString("yyyy-MM-dd"),
        Time = "14:30",
        People = 5,
        ArrozType = rice ? "Arroz seco de verduras de la huerta" : null,
        ArrozServings = rice ? 5 : null,
        HighChairs = 2,
        BabyStrollers = 1,
        Commentary = "test booking columns"
    };

    [Fact]
    public async Task Create_WithTestPhone_PersistsEveryColumnOrDatabaseDefault()
    {
        if (_connectionString == null) return;
        var id = await Repository().CreateBookingAsync(Booking());
        Assert.NotNull(id);

        await using var db = new MySqlConnection(_connectionString);
        try
        {
            var row = await db.QuerySingleAsync<dynamic>("SELECT * FROM bookings WHERE id=@Id", new { Id = id });
            Assert.True(Convert.ToInt32(row.id) > 0);
            Assert.Equal("Test columnas 692747052", (string)row.customer_name);
            Assert.Equal("whatsapp@bot.local", (string)row.contact_email);
            Assert.Equal(DateTime.Today.AddYears(5).Date, ((DateTime)row.reservation_date).Date);
            Assert.Equal(new TimeSpan(14, 30, 0), (TimeSpan)row.reservation_time);
            Assert.Equal(5, Convert.ToInt32(row.party_size));
            Assert.Equal(0, Convert.ToInt32(row.children));
            Assert.Equal("692747052", (string)row.contact_phone);
            Assert.Equal("34", (string)row.contact_phone_country_code);
            Assert.Null(row.re_confirmation_token);
            Assert.Equal(1, Convert.ToInt32(row.re_confirmation));
            Assert.Equal("test booking columns", (string)row.commentary);
            Assert.Equal(1, Convert.ToInt32(row.babyStrollers));
            Assert.Equal(2, Convert.ToInt32(row.highChairs));
            Assert.NotNull(row.added_date);
            Assert.Equal("pending", (string)row.status);
            Assert.Equal(0, Convert.ToInt32(row.reminder_sent));
            Assert.Equal(0, Convert.ToInt32(row.rice_reminder_sent));
            Assert.Null(row.table_number);
            Assert.Null(row.preferred_floor_number);
            Assert.Equal(0, Convert.ToInt32(row.special_menu));
            Assert.Equal("[\"Arroz seco de verduras de la huerta\"]", (string)row.arroz_type);
            Assert.Equal("[5]", (string)row.arroz_servings);
            Assert.Null(row.menu_de_grupo_id);
            Assert.Null(row.principales_json);
            Assert.Equal(1, Convert.ToInt32(row.restaurant_id));
        }
        finally
        {
            await db.ExecuteAsync("DELETE FROM bookings WHERE id=@Id", new { Id = id });
        }
    }

    [Fact]
    public async Task Create_InvalidColumnCombination_DoesNotInsert()
    {
        if (_connectionString == null) return;
        await using var db = new MySqlConnection(_connectionString);
        var before = await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM bookings WHERE contact_phone='692747052' AND customer_name='Invalid test columns'");

        var id = await Repository().CreateBookingAsync(Booking() with
        {
            Name = "Invalid test columns",
            People = 1,
            HighChairs = 2,
            ArrozServings = 5
        });

        Assert.Null(id);
        var after = await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM bookings WHERE contact_phone='692747052' AND customer_name='Invalid test columns'");
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Update_WithTestPhone_RoundTripsEveryBotWritableColumn()
    {
        if (_connectionString == null) return;
        var repository = Repository();
        var id = await repository.CreateBookingAsync(Booking(false));
        Assert.NotNull(id);

        await using var db = new MySqlConnection(_connectionString);
        try
        {
            var updated = await repository.UpdateBookingAsync((int)id.Value, new BookingUpdateData
            {
                ReservationDate = DateTime.Today.AddYears(5).AddDays(1).ToString("yyyy-MM-dd"),
                ReservationTime = "15:00",
                PartySize = 4,
                ArrozType = "Arroz seco de verduras de la huerta",
                ArrozServings = 4,
                HighChairs = 1,
                BabyStrollers = 2
            });

            Assert.True(updated);
            var row = await repository.GetBookingByIdAsync((int)id.Value);
            Assert.NotNull(row);
            Assert.Equal(DateTime.Today.AddYears(5).AddDays(1).Date, row.ReservationDate.Date);
            Assert.Equal(new TimeSpan(15, 0, 0), row.ReservationTime);
            Assert.Equal(4, row.PartySize);
            Assert.Equal("Arroz seco de verduras de la huerta", row.ArrozType);
            Assert.Equal(4, row.ArrozServings);
            Assert.Equal(1, row.HighChairs);
            Assert.Equal(2, row.BabyStrollers);
            Assert.Equal("692747052", row.ContactPhone);
        }
        finally
        {
            await db.ExecuteAsync("DELETE FROM bookings WHERE id=@Id", new { Id = id });
        }
    }

    [Fact]
    public async Task Cancel_WithTestPhone_IsAtomicAndPreservesArchiveMetadata()
    {
        if (_connectionString == null) return;
        var repository = Repository();
        var id = await repository.CreateBookingAsync(Booking());
        Assert.NotNull(id);

        await using var db = new MySqlConnection(_connectionString);
        const string principales = "[{\"name\":\"Principal test\",\"servings\":5}]";
        await db.ExecuteAsync(@"
            UPDATE bookings SET contact_email='test@example.com', commentary='archive metadata',
                special_menu=1, menu_de_grupo_id=77, principales_json=@Principales
            WHERE id=@Id", new { Id = id, Principales = principales });

        try
        {
            var booking = await repository.GetBookingByIdAsync((int)id.Value);
            Assert.NotNull(booking);
            Assert.True(await repository.ArchiveAndCancelBookingAsync(booking, "AI_AGENT"));
            Assert.Equal(0, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM bookings WHERE id=@Id", new { Id = id }));

            var archived = await db.QuerySingleAsync<dynamic>(
                "SELECT * FROM cancelled_bookings WHERE booking_id=@Id ORDER BY id DESC LIMIT 1", new { Id = id });
            Assert.Equal("692747052", (string)archived.contact_phone);
            Assert.Equal("test@example.com", (string)archived.contact_email);
            Assert.Equal("archive metadata", (string)archived.commentary);
            Assert.Equal(1, Convert.ToInt32(archived.special_menu));
            Assert.Equal(77, Convert.ToInt32(archived.menu_de_grupo_id));
            Assert.Equal(principales, (string)archived.principales_json);
            Assert.Equal(1, Convert.ToInt32(archived.restaurant_id));
        }
        finally
        {
            await db.ExecuteAsync("DELETE FROM cancelled_bookings WHERE booking_id=@Id; DELETE FROM bookings WHERE id=@Id", new { Id = id });
        }
    }

    [Fact]
    public async Task Cancel_WhenDeleteGuardFails_RollsBackArchive()
    {
        if (_connectionString == null) return;
        var repository = Repository();
        var id = await repository.CreateBookingAsync(Booking(false));
        Assert.NotNull(id);

        await using var db = new MySqlConnection(_connectionString);
        try
        {
            var booking = await repository.GetBookingByIdAsync((int)id.Value);
            Assert.NotNull(booking);
            var wrongOwner = booking with { ContactPhone = "600000000" };

            Assert.False(await repository.ArchiveAndCancelBookingAsync(wrongOwner, "AI_AGENT"));
            Assert.Equal(1, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM bookings WHERE id=@Id", new { Id = id }));
            Assert.Equal(0, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM cancelled_bookings WHERE booking_id=@Id", new { Id = id }));
        }
        finally
        {
            await db.ExecuteAsync("DELETE FROM cancelled_bookings WHERE booking_id=@Id; DELETE FROM bookings WHERE id=@Id", new { Id = id });
        }
    }
}
