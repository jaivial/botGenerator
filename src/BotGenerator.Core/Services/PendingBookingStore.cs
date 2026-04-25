using System.Collections.Concurrent;
using BotGenerator.Core.Models;
using Microsoft.Extensions.Configuration;

namespace BotGenerator.Core.Services;

/// <summary>
/// In-memory implementation of <see cref="IPendingBookingStore"/> with TTL-based expiration.
/// </summary>
public sealed class PendingBookingStore : IPendingBookingStore
{
    private readonly ConcurrentDictionary<string, TimedEntry> _pending = new();
    private readonly TimeSpan _ttl;

    private record TimedEntry(BookingData Booking, DateTime SetAt);

    public PendingBookingStore(IConfiguration configuration)
    {
        var ttlMinutes = configuration.GetValue("PendingBooking:TTLMinutes", 30);
        _ttl = TimeSpan.FromMinutes(ttlMinutes);
    }

    public void Set(string phoneNumber, BookingData booking)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return;
        _pending[phoneNumber] = new TimedEntry(booking, DateTime.UtcNow);
    }

    public BookingData? Get(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return null;
        if (!_pending.TryGetValue(phoneNumber, out var entry)) return null;

        if (DateTime.UtcNow - entry.SetAt > _ttl)
        {
            _pending.TryRemove(phoneNumber, out _);
            return null;
        }

        return entry.Booking;
    }

    public void Clear(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return;
        _pending.TryRemove(phoneNumber, out _);
    }
}
