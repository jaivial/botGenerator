using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Interface for the single agent that handles all conversation logic.
/// The agent must:
/// 1. Use fetch_whatsapp_history tool to get conversation context (OBLIGATORY)
/// 2. Use available tools (check_availability, create_booking, cancel_booking, etc.)
/// 3. Send response via send_message tool
/// </summary>
public interface IAgentOrchestrator
{
    /// <summary>
    /// Run the agent to handle a user message.
    /// The agent will fetch conversation history and use tools to respond.
    /// </summary>
    Task<AgentResult> RunAsync(
        string userMessage,
        string phoneNumber,
        PipelineIntent intent,
        Dictionary<string, object>? extractedInfo,
        string conversationHistory,
        CancellationToken ct = default);
}
