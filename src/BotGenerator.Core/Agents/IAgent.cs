using BotGenerator.Core.Models;

namespace BotGenerator.Core.Agents;

/// <summary>
/// Interface for AI agents that process messages.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Processes a WhatsApp message and generates a response.
    /// </summary>
    /// <param name="message">The incoming message.</param>
    /// <param name="state">Current conversation state.</param>
    /// <param name="history">Conversation history.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent's response.</returns>
    Task<AgentResponse> ProcessAsync(
        WhatsAppMessage message,
        ConversationState? state,
        List<ChatMessage>? history,
        CancellationToken cancellationToken = default);
}
