using System.Collections.Concurrent;
using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Simple in-memory implementation of <see cref="IPendingBookingStore"/>.
/// </summary>
public sealed class PendingBookingStore : IPendingBookingStore
{
    private readonly ConcurrentDictionary<string, BookingData> _pending = new();

    public void Set(string phoneNumber, BookingData booking)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return;
        _pending[phoneNumber] = booking;
    }

    public BookingData? Get(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return null;
        return _pending.TryGetValue(phoneNumber, out var booking) ? booking : null;
    }

    public void Clear(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return;
        _pending.TryRemove(phoneNumber, out _);
    }
}


