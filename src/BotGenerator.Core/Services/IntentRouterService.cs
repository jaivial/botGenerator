using BotGenerator.Core.Agents;
using BotGenerator.Core.Handlers;
using BotGenerator.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BotGenerator.Core.Services;

/// <summary>
/// Implementation of IIntentRouterService.
/// Routes conversations based on detected intent.
/// </summary>
public class IntentRouterService : IIntentRouterService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<IntentRouterService> _logger;

    public IntentRouterService(
        IServiceProvider services,
        ILogger<IntentRouterService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task<AgentResponse> RouteAsync(
        AgentResponse mainAgentResponse,
        WhatsAppMessage originalMessage,
        ConversationState? state,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Routing intent: {Intent} for {Sender}",
            mainAgentResponse.Intent,
            originalMessage.PushName);

        try
        {
            return mainAgentResponse.Intent switch
            {
                IntentType.Booking => await HandleBookingAsync(
                    mainAgentResponse, originalMessage, state, cancellationToken),

                IntentType.Cancellation => await HandleCancellationAsync(
                    mainAgentResponse, originalMessage, cancellationToken),

                IntentType.Modification => await HandleModificationAsync(
                    mainAgentResponse, originalMessage, cancellationToken),

                IntentType.SameDay => HandleSameDay(mainAgentResponse),

                IntentType.Interactive => HandleInteractive(mainAgentResponse),

                IntentType.Error => HandleError(mainAgentResponse),

                _ => mainAgentResponse // Normal - return as-is
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error routing intent {Intent}", mainAgentResponse.Intent);
            return AgentResponse.Error("Error processing your request");
        }
    }

    #region Intent Handlers

    private async Task<AgentResponse> HandleBookingAsync(
        AgentResponse response,
        WhatsAppMessage message,
        ConversationState? state,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling booking intent");

        // Check if we need to validate rice type first
        var needsRiceValidation = response.ExtractedData?.ArrozType != null &&
                                  (response.Metadata?.ContainsKey("riceValidated") != true);

        if (needsRiceValidation && response.ExtractedData?.ArrozType != null)
        {
            var riceValidator = _services.GetRequiredService<RiceValidatorAgent>();
            var riceResult = await riceValidator.ValidateAsync(
                response.ExtractedData.ArrozType,
                "villacarmen",
                cancellationToken);

            if (!riceResult.IsValid)
            {
                _logger.LogInformation(
                    "Rice validation failed: {Status}",
                    riceResult.Status);

                return new AgentResponse
                {
                    Intent = IntentType.Normal,
                    AiResponse = riceResult.Message ?? "No se pudo validar el arroz",
                    Metadata = new Dictionary<string, object>
                    {
                        ["riceValidation"] = riceResult
                    }
                };
            }

            // Update booking data with validated rice name
            response = response with
            {
                ExtractedData = response.ExtractedData with
                {
                    ArrozType = riceResult.RiceName
                },
                Metadata = new Dictionary<string, object>(response.Metadata ?? new())
                {
                    ["riceValidated"] = true,
                    ["validatedRiceName"] = riceResult.RiceName ?? ""
                }
            };
        }

        // Check availability
        if (response.ExtractedData != null)
        {
            var availabilityChecker = _services.GetRequiredService<AvailabilityCheckerAgent>();
            var availability = availabilityChecker.CheckAvailability(response.ExtractedData);

            if (availability.IsSameDay)
            {
                return new AgentResponse
                {
                    Intent = IntentType.SameDay,
                    AiResponse = availability.Message
                };
            }

            if (!availability.IsAvailable)
            {
                _logger.LogInformation(
                    "Booking not available: {Message}",
                    availability.Message);

                return new AgentResponse
                {
                    Intent = IntentType.Normal,
                    AiResponse = availability.Message
                };
            }
        }

        // If we have all data, create the booking
        if (response.ExtractedData?.IsValid == true)
        {
            var bookingHandler = _services.GetRequiredService<BookingHandler>();
            return await bookingHandler.CreateBookingAsync(
                response.ExtractedData,
                message,
                cancellationToken);
        }

        // Return the main response (still collecting data)
        return response;
    }

    private async Task<AgentResponse> HandleCancellationAsync(
        AgentResponse response,
        WhatsAppMessage message,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling cancellation intent");

        if (response.ExtractedData == null)
        {
            return response; // Still collecting cancellation data
        }

        var cancellationHandler = _services.GetRequiredService<CancellationHandler>();
        return await cancellationHandler.ProcessCancellationAsync(
            response.ExtractedData,
            message.SenderNumber,
            cancellationToken);
    }

    private async Task<AgentResponse> HandleModificationAsync(
        AgentResponse response,
        WhatsAppMessage message,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling modification intent");

        var modificationHandler = _services.GetRequiredService<ModificationHandler>();
        return await modificationHandler.StartModificationFlowAsync(
            message.SenderNumber,
            cancellationToken);
    }

    private AgentResponse HandleSameDay(AgentResponse response)
    {
        _logger.LogDebug("Handling same-day booking rejection");

        var message = string.IsNullOrWhiteSpace(response.AiResponse)
            ? "Lo sentimos, no aceptamos reservas para el mismo día. " +
              "Por favor, llámanos al +34 638 857 294 para ver disponibilidad."
            : response.AiResponse;

        return response with { AiResponse = message };
    }

    private AgentResponse HandleInteractive(AgentResponse response)
    {
        _logger.LogDebug("Handling interactive response with URLs");

        // The response already contains URLs, just pass through
        // The WhatsApp service will format them appropriately
        return response;
    }

    private AgentResponse HandleError(AgentResponse response)
    {
        _logger.LogWarning("Handling error response");

        var message = "Disculpa, hubo un problema con el asistente. " +
                     "Por favor, llámanos al +34 638 857 294.";

        return response with { AiResponse = message };
    }

    #endregion
}
