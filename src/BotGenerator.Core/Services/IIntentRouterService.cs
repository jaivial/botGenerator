using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Service for routing conversation based on detected intent.
/// </summary>
public interface IIntentRouterService
{
    /// <summary>
    /// Routes the agent response to appropriate handlers based on intent.
    /// </summary>
    /// <param name="mainAgentResponse">Response from the main conversation agent.</param>
    /// <param name="originalMessage">The original WhatsApp message.</param>
    /// <param name="state">Current conversation state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Final response to send to user.</returns>
    Task<AgentResponse> RouteAsync(
        AgentResponse mainAgentResponse,
        WhatsAppMessage originalMessage,
        ConversationState? state,
        CancellationToken cancellationToken = default);
}
