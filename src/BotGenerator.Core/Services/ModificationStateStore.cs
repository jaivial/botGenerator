using BotGenerator.Core.Models;
using Microsoft.Extensions.Logging;

namespace BotGenerator.Core.Services;

/// <summary>
/// In-memory implementation of IModificationStateStore.
/// Sessions timeout after 30 minutes of inactivity.
/// </summary>
public class ModificationStateStore : IModificationStateStore
{
    private readonly Dictionary<string, ModificationState> _states = new();
    private readonly ILogger<ModificationStateStore> _logger;
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(30);

    public ModificationStateStore(ILogger<ModificationStateStore> logger)
    {
        _logger = logger;
    }

    public ModificationState? Get(string phoneNumber)
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
                "Modification session expired for {Phone} (last updated {UpdatedAt})",
                normalizedPhone, state.UpdatedAt);
            _states.Remove(normalizedPhone);
            return null;
        }

        return state;
    }

    public void Set(string phoneNumber, ModificationState state)
    {
        var normalizedPhone = NormalizePhone(phoneNumber);

        // Update the timestamp
        var updatedState = state with { UpdatedAt = DateTime.UtcNow };

        _states[normalizedPhone] = updatedState;

        _logger.LogDebug(
            "Set modification state for {Phone}: Stage={Stage}, Field={Field}",
            normalizedPhone, state.Stage, state.FieldToModify);
    }

    public void Clear(string phoneNumber)
    {
        var normalizedPhone = NormalizePhone(phoneNumber);

        if (_states.Remove(normalizedPhone))
        {
            _logger.LogDebug("Cleared modification state for {Phone}", normalizedPhone);
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
