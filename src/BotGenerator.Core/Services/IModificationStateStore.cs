using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Interface for storing modification flow state per user.
/// </summary>
public interface IModificationStateStore
{
    /// <summary>
    /// Gets the current modification state for a phone number.
    /// Returns null if no active modification session exists.
    /// </summary>
    /// <param name="phoneNumber">The user's phone number.</param>
    /// <returns>The modification state, or null if none exists.</returns>
    ModificationState? Get(string phoneNumber);

    /// <summary>
    /// Sets/updates the modification state for a phone number.
    /// </summary>
    /// <param name="phoneNumber">The user's phone number.</param>
    /// <param name="state">The state to store.</param>
    void Set(string phoneNumber, ModificationState state);

    /// <summary>
    /// Clears the modification state for a phone number.
    /// </summary>
    /// <param name="phoneNumber">The user's phone number.</param>
    void Clear(string phoneNumber);

    /// <summary>
    /// Checks if a user has an active modification session.
    /// </summary>
    /// <param name="phoneNumber">The user's phone number.</param>
    /// <returns>True if an active session exists.</returns>
    bool HasActiveSession(string phoneNumber);
}
