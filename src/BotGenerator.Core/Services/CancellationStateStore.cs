using BotGenerator.Core.Models;
using Microsoft.Extensions.Logging;

namespace BotGenerator.Core.Services;

/// <summary>
/// In-memory implementation of ICancellationStateStore.
/// Sessions timeout after 30 minutes of inactivity.
/// </summary>
public class CancellationStateStore : ICancellationStateStore
{
    private readonly Dictionary<string, CancellationState> _states = new();
    private readonly ILogger<CancellationStateStore> _logger;
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(30);

    public CancellationStateStore(ILogger<CancellationStateStore> logger)
    {
        _logger = logger;
    }

    public CancellationState? Get(string phoneNumber)
    {
        var normalizedPhone = NormalizePhone(phoneNumber);

        if (!_states.TryGetValue(normalizedPhone, out var state))
        {
            return null;
        }

        // Check for session timeout
        if (DateTime.UtcNow - state.UpdatedAt > _sessionTimeout)
        {
            _logger.LogDebug(
                "Cancellation session expired for {Phone} (last updated {UpdatedAt})",
                normalizedPhone, state.UpdatedAt);
            _states.Remove(normalizedPhone);
            return null;
        }

        return state;
    }

    public void Set(string phoneNumber, CancellationState state)
    {
        var normalizedPhone = NormalizePhone(phoneNumber);

        // Update the timestamp
        var updatedState = state with { UpdatedAt = DateTime.UtcNow };

        _states[normalizedPhone] = updatedState;

        _logger.LogDebug(
            "Set cancellation state for {Phone}: Stage={Stage}",
            normalizedPhone, state.Stage);
    }

    public void Clear(string phoneNumber)
    {
        var normalizedPhone = NormalizePhone(phoneNumber);

        if (_states.Remove(normalizedPhone))
        {
            _logger.LogDebug("Cleared cancellation state for {Phone}", normalizedPhone);
        }
    }

    public bool HasActiveSession(string phoneNumber)
    {
        return Get(phoneNumber) != null;
    }

    /// <summary>
    /// Normalizes phone number to a consistent format for dictionary keys.
    /// </summary>
    private static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "";

        // Keep only digits
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        // If it starts with 34 and is 11+ digits, keep as is
        if (digits.StartsWith("34") && digits.Length >= 11)
        {
            return digits;
        }

        // If it's 9 digits, add 34 prefix
        if (digits.Length == 9)
        {
            return "34" + digits;
        }

        return digits;
    }
}
