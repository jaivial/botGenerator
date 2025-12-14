using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Stores a "pending" booking per phone number when the AI has already emitted BOOKING_REQUEST,
/// but the code still needs to collect mandatory fields (rice decision/servings, high chairs, strollers).
/// This avoids requiring the AI to emit BOOKING_REQUEST multiple times across follow-up turns.
/// </summary>
public interface IPendingBookingStore
{
    void Set(string phoneNumber, BookingData booking);
    BookingData? Get(string phoneNumber);
    void Clear(string phoneNumber);
}


