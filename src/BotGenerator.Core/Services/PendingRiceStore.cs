using System.Collections.Concurrent;

namespace BotGenerator.Core.Services;

/// <summary>
/// In-memory store for pending rice selections.
/// </summary>
public class PendingRiceStore : IPendingRiceStore
{
    private readonly ConcurrentDictionary<string, PendingRiceSelection> _store = new();
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(30);

    public PendingRiceSelection? Get(string phoneNumber)
    {
        if (_store.TryGetValue(phoneNumber, out var selection))
        {
            // Check for timeout
            if (DateTime.UtcNow - selection.CreatedAt > Timeout)
            {
                _store.TryRemove(phoneNumber, out _);
                return null;
            }
            return selection;
        }
        return null;
    }

    public void Set(string phoneNumber, PendingRiceSelection selection)
    {
        _store[phoneNumber] = selection;
    }

    public void Clear(string phoneNumber)
    {
        _store.TryRemove(phoneNumber, out _);
    }
}
