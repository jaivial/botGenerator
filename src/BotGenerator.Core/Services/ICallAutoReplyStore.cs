namespace BotGenerator.Core.Services;

/// <summary>
/// Tracks recent call auto-replies per phone to avoid spamming repeated call events.
/// </summary>
public interface ICallAutoReplyStore
{
    /// <summary>
    /// Returns true if we should send an auto-reply now and records the send time.
    /// Returns false if within cooldown.
    /// </summary>
    bool TryMarkReplied(string phoneNumber, TimeSpan cooldown, DateTime utcNow);
}

