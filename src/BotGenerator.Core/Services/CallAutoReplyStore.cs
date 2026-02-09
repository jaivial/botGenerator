using System.Collections.Concurrent;

namespace BotGenerator.Core.Services;

public class CallAutoReplyStore : ICallAutoReplyStore
{
    private readonly ConcurrentDictionary<string, DateTime> _lastReplyUtc = new();

    public bool TryMarkReplied(string phoneNumber, TimeSpan cooldown, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return false;

        // Normalize to digits to match how we key other stores.
        var normalized = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        while (true)
        {
            if (!_lastReplyUtc.TryGetValue(normalized, out var last))
            {
                if (_lastReplyUtc.TryAdd(normalized, utcNow))
                    return true;
                continue;
            }

            if (utcNow - last < cooldown)
                return false;

            if (_lastReplyUtc.TryUpdate(normalized, utcNow, last))
                return true;
        }
    }
}

