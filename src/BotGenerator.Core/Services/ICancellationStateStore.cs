using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Interface for storing cancellation flow state per user.
/// </summary>
public interface ICancellationStateStore
{
    /// <summary>
    /// Gets the current cancellation state for a phone number.
    /// Returns null if no active cancellation session exists.
    /// </summary>
    /// <param name="phoneNumber">The user's phone number.</param>
    /// <returns>The cancellation state, or null if none exists.</returns>
    CancellationState? Get(string phoneNumber);

    /// <summary>
    /// Sets/updates the cancellation state for a phone number.
    /// </summary>
    /// <param name="phoneNumber">The user's phone number.</param>
    /// <param name="state">The state to store.</param>
    void Set(string phoneNumber, CancellationState state);

    /// <summary>
    /// Clears the cancellation state for a phone number.
    /// </summary>
    /// <param name="phoneNumber">The user's phone number.</param>
    void Clear(string phoneNumber);

    /// <summary>
    /// Checks if a user has an active cancellation session.
    /// </summary>
    /// <param name="phoneNumber">The user's phone number.</param>
    /// <returns>True if an active session exists.</returns>
    bool HasActiveSession(string phoneNumber);
}
