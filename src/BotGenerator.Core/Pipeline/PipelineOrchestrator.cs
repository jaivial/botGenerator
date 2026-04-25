using BotGenerator.Core.Handlers;
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using Microsoft.Extensions.Logging;

namespace BotGenerator.Core.Pipeline;

/// <summary>
/// Orchestrates the 3-node pipeline: ContextAnalyzer → ValidationEnrichment → ResponseGenerator.
/// Manages booking state, handles special cases (confirm, modify, cancel).
/// </summary>
public class PipelineOrchestrator
{
    private readonly ContextAnalyzerNode _analyzer;
    private readonly ValidationEnrichmentNode _validator;
    private readonly ResponseGeneratorNode _responder;
    private readonly BookingHandler _bookingHandler;
    private readonly IPendingBookingStore _pendingBookingStore;
    private readonly IBookingRepository _bookingRepository;
    private readonly CancellationHandler _cancellationHandler;
    private readonly ICancellationStateStore _cancellationStateStore;
    private readonly ModificationHandler _modificationHandler;
    private readonly IModificationStateStore _modificationStateStore;
    private readonly IWhatsAppService _whatsApp;
    private readonly ILogger<PipelineOrchestrator> _logger;

    public PipelineOrchestrator(
        ContextAnalyzerNode analyzer,
        ValidationEnrichmentNode validator,
        ResponseGeneratorNode responder,
        BookingHandler bookingHandler,
        IPendingBookingStore pendingBookingStore,
        IBookingRepository bookingRepository,
        CancellationHandler cancellationHandler,
        ICancellationStateStore cancellationStateStore,
        ModificationHandler modificationHandler,
        IModificationStateStore modificationStateStore,
        IWhatsAppService whatsApp,
        ILogger<PipelineOrchestrator> logger)
    {
        _analyzer = analyzer;
        _validator = validator;
        _responder = responder;
        _bookingHandler = bookingHandler;
        _pendingBookingStore = pendingBookingStore;
        _bookingRepository = bookingRepository;
        _cancellationHandler = cancellationHandler;
        _cancellationStateStore = cancellationStateStore;
        _modificationHandler = modificationHandler;
        _modificationStateStore = modificationStateStore;
        _whatsApp = whatsApp;
        _logger = logger;
    }

    public async Task<PipelineResult> ProcessAsync(PipelineContext context, CancellationToken ct)
    {
        var phone = context.Message.SenderNumber;

        _logger.LogInformation(
            "Pipeline processing message from {Phone}: '{Message}'",
            phone, context.Message.MessageText);

        // === NODE 1: AI Context Analysis ===
        var analysis = await _analyzer.ProcessAsync(context, ct);

        _logger.LogInformation(
            "ContextAnalyzer result: Intent={Intent}, Confidence={Confidence}, Reasoning={Reasoning}",
            analysis.Intent, analysis.Confidence, analysis.Reasoning);

        // === EARLY EXITS: Simple intents that don't need validation/response generation ===
        switch (analysis.Intent)
        {
            case PipelineIntent.Acknowledgment:
                return BuildSimpleAck(context, analysis);

            case PipelineIntent.BroadcastReply:
                return BuildBroadcastReply(context, analysis);

            case PipelineIntent.SameDayBooking:
                return BuildSameDayReply(context);

            case PipelineIntent.EventInquiry:
                return BuildEventInquiryReply(context);
        }

        // === DELEGATE TO EXISTING HANDLERS FOR MULTI-TURN FLOWS ===
        if (analysis.Intent == PipelineIntent.Cancellation)
        {
            return await HandleCancellationAsync(context, analysis, ct);
        }

        if (analysis.Intent == PipelineIntent.Modification)
        {
            return await HandleModificationAsync(context, analysis, ct);
        }

        // === NODE 2: Deterministic Validation (booking-related only) ===
        ValidationResult? validation = null;
        if (NeedsValidation(analysis.Intent))
        {
            validation = await _validator.ProcessAsync((context, analysis), ct);

            _logger.LogInformation(
                "ValidationResult: IsAvailable={IsAvailable}, Reason={Reason}",
                validation.IsAvailable, validation.RejectionReason);
        }

        // === BOOKING CONFIRMATION ===
        if (analysis.Intent == PipelineIntent.ConfirmBooking && context.PendingBooking?.SummaryShown == true)
        {
            return await HandleBookingConfirmationAsync(context, analysis, validation, ct);
        }

        // === BOOKING DECLINE ===
        if (analysis.Intent == PipelineIntent.DeclineBooking)
        {
            _pendingBookingStore.Clear(phone);
            return new PipelineResult
            {
                Intent = PipelineIntent.DeclineBooking,
                ResponseText = $"No pasa nada, {context.PushName}! Si decides reservar, aquí estoy. 😊"
            };
        }

        // === INFO REQUEST ===
        if (analysis.Intent == PipelineIntent.InfoRequest)
        {
            return BuildInfoReply(context);
        }

        // === NODE 3: AI Response Generation ===
        var responseText = await _responder.ProcessAsync((context, analysis, validation), ct);

        // === UPDATE PENDING BOOKING STATE ===
        var pendingUpdate = UpdatePendingState(context, analysis, validation);

        return new PipelineResult
        {
            Intent = analysis.Intent,
            ResponseText = responseText,
            PendingBookingUpdate = pendingUpdate,
            ShouldUpdatePending = pendingUpdate != null
        };
    }

    private static PipelineResult BuildSimpleAck(PipelineContext context, ContextAnalysisResult analysis)
    {
        var replies = new[]
        {
            "De nada! Si necesitas algo más, aquí estoy. 😊",
            "Gracias a ti! Si necesitas hacer una reserva o tienes alguna pregunta, aquí estoy.",
            "Perfecto! Para cualquier cosa, aquí me tienes.",
            "A tu disposición! 😊"
        };

        return new PipelineResult
        {
            Intent = PipelineIntent.Acknowledgment,
            ResponseText = replies[Random.Shared.Next(replies.Length)],
            ShouldClearPending = true
        };
    }

    private static PipelineResult BuildBroadcastReply(PipelineContext context, ContextAnalysisResult analysis)
    {
        var replies = new[]
        {
            "Gracias a ti! Si necesitas hacer una reserva o tienes alguna pregunta, aquí estoy. 😊",
            "Nos alegra que te guste! Para reservas o consultas, aquí me tienes.",
            "Gracias! Si te apetece visitarnos, aquí estoy para ayudarte con la reserva. 😊"
        };

        return new PipelineResult
        {
            Intent = PipelineIntent.BroadcastReply,
            ResponseText = replies[Random.Shared.Next(replies.Length)],
            ShouldClearPending = true
        };
    }

    private static PipelineResult BuildSameDayReply(PipelineContext context)
    {
        return new PipelineResult
        {
            Intent = PipelineIntent.SameDayBooking,
            ResponseText = "Lo siento, no aceptamos reservas para el mismo día por WhatsApp. " +
                           "Para reservas urgentes, por favor llámanos al *638 857 294*. 📞",
            ShouldClearPending = true
        };
    }

    private static PipelineResult BuildEventInquiryReply(PipelineContext context)
    {
        return new PipelineResult
        {
            Intent = PipelineIntent.EventInquiry,
            ResponseText = "Para eventos especiales (bodas, cumpleaños, celebraciones), " +
                           "te atendemos personalmente. Llámanos al *638 857 294* o escríbenos " +
                           "a través de nuestra web alqueriavillacarmen.com. 🎉",
            ShouldClearPending = true
        };
    }

    private static PipelineResult BuildInfoReply(PipelineContext context)
    {
        if (context.ExistingBookings.Count == 0)
        {
            return new PipelineResult
            {
                Intent = PipelineIntent.InfoRequest,
                ResponseText = $"No tengo ninguna reserva activa a tu nombre, {context.PushName}. " +
                               "¿Quieres hacer una reserva?"
            };
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"*Tus reservas activas, {context.PushName}:*");
        sb.AppendLine();
        foreach (var b in context.ExistingBookings)
        {
            var dayName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
                b.ReservationDate.ToString("dddd"));
            var rice = string.IsNullOrEmpty(b.ArrozType) ? "" : $" con {b.ArrozType}";
            sb.AppendLine($"📅 {dayName} {b.DateFormatted} a las {b.TimeFormatted} - {b.PartySize} personas{rice}");
        }

        return new PipelineResult
        {
            Intent = PipelineIntent.InfoRequest,
            ResponseText = sb.ToString().TrimEnd()
        };
    }

    private async Task<PipelineResult> HandleBookingConfirmationAsync(
        PipelineContext context,
        ContextAnalysisResult analysis,
        ValidationResult? validation,
        CancellationToken ct)
    {
        var pending = context.PendingBooking!;
        var phone = context.Message.SenderNumber;

        // Final availability check before creating booking
        if (validation != null && !validation.IsAvailable)
        {
            return new PipelineResult
            {
                Intent = PipelineIntent.NewBooking,
                ResponseText = validation.SuggestionMessage ?? "Lo siento, hubo un cambio en la disponibilidad. ¿Quieres probar otra fecha u hora?",
                ShouldClearPending = true
            };
        }

        // Create the booking
        var booking = pending with
        {
            Name = context.PushName,
            Phone = phone
        };

        var result = await _bookingHandler.CreateBookingAsync(booking, context.Message, ct);

        if (result.Intent == IntentType.Booking && result.Metadata?.ContainsKey("bookingCreated") == true)
        {
            _pendingBookingStore.Clear(phone);

            var bookingId = result.Metadata.TryGetValue("bookingId", out var idObj) ? Convert.ToInt64(idObj) : (long?)null;

            return new PipelineResult
            {
                Intent = PipelineIntent.ConfirmBooking,
                ResponseText = result.AiResponse,
                BookingToCreate = booking,
                CreatedBookingId = bookingId,
                ShouldNotifyManagement = true,
                ShouldClearPending = true
            };
        }

        return new PipelineResult
        {
            Intent = PipelineIntent.NewBooking,
            ResponseText = "Lo siento, hubo un problema al crear la reserva. " +
                           "Por favor, inténtalo de nuevo o llámanos al 638 857 294.",
            ShouldClearPending = true
        };
    }

    private async Task<PipelineResult> HandleCancellationAsync(
        PipelineContext context,
        ContextAnalysisResult analysis,
        CancellationToken ct)
    {
        var phone = context.Message.SenderNumber;
        var existingState = _cancellationStateStore.Get(phone);

        var agentResponse = await _cancellationHandler.ProcessCancellationAsync(
            context.Message, existingState, ct);

        // Update cancellation state
        if (agentResponse.Metadata != null)
        {
            if (agentResponse.Metadata.TryGetValue("newState", out var stateObj) && stateObj is CancellationState cs)
                _cancellationStateStore.Set(phone, cs);
            if (agentResponse.Metadata.TryGetValue("clearState", out _) && agentResponse.Metadata["clearState"] is true)
                _cancellationStateStore.Clear(phone);
        }

        return new PipelineResult
        {
            Intent = PipelineIntent.Cancellation,
            ResponseText = agentResponse.AiResponse,
            ShouldClearPending = true
        };
    }

    private async Task<PipelineResult> HandleModificationAsync(
        PipelineContext context,
        ContextAnalysisResult analysis,
        CancellationToken ct)
    {
        var phone = context.Message.SenderNumber;
        var existingState = _modificationStateStore.Get(phone);

        var agentResponse = await _modificationHandler.ProcessModificationAsync(
            context.Message, existingState, ct);

        // Update modification state
        if (agentResponse.Metadata != null)
        {
            if (agentResponse.Metadata.TryGetValue("newState", out var stateObj) && stateObj is ModificationState ms)
                _modificationStateStore.Set(phone, ms);
            if (agentResponse.Metadata.TryGetValue("clearState", out _) && agentResponse.Metadata["clearState"] is true)
                _modificationStateStore.Clear(phone);
        }

        return new PipelineResult
        {
            Intent = PipelineIntent.Modification,
            ResponseText = agentResponse.AiResponse,
            ShouldClearPending = true
        };
    }

    private BookingData? UpdatePendingState(
        PipelineContext context,
        ContextAnalysisResult analysis,
        ValidationResult? validation)
    {
        var phone = context.Message.SenderNumber;
        var current = context.PendingBooking;

        // New booking intent with extracted data
        if (analysis.Intent is PipelineIntent.NewBooking or PipelineIntent.ContinueBooking)
        {
            var updated = new BookingData
            {
                Name = context.PushName,
                Phone = phone,
                Date = analysis.ExtractedDate ?? current?.Date ?? "",
                Time = analysis.ExtractedTime ?? current?.Time ?? "",
                People = analysis.ExtractedPeople ?? current?.People ?? 0,
                ArrozType = analysis.RiceDeclined ? "" :
                            (analysis.ExtractedRiceType ?? current?.ArrozType),
                ArrozServings = analysis.ExtractedRiceServings ?? current?.ArrozServings,
                HighChairs = analysis.ExtractedHighChairs ?? current?.HighChairs ?? 0,
                BabyStrollers = analysis.ExtractedBabyStrollers ?? current?.BabyStrollers ?? 0,
                SummaryShown = current?.SummaryShown ?? false
            };

            // Update rice from validation
            if (validation?.RiceValidation?.Status == "valid" && !string.IsNullOrEmpty(validation.RiceValidation.RiceName))
            {
                updated = updated with { ArrozType = validation.RiceValidation.RiceName };
            }

            // Check if all required data is present and rice is decided
            bool riceDecided = updated.ArrozType != null;
            bool hasAllRequired = !string.IsNullOrEmpty(updated.Date) &&
                                  !string.IsNullOrEmpty(updated.Time) &&
                                  updated.People > 0 &&
                                  riceDecided;

            if (hasAllRequired && !updated.SummaryShown)
            {
                // Check if rice servings needed
                bool needsRiceServings = !string.IsNullOrEmpty(updated.ArrozType) &&
                                          (!updated.ArrozServings.HasValue || updated.ArrozServings <= 0);
                if (!needsRiceServings)
                {
                    updated = updated with { SummaryShown = true };
                }
            }

            _pendingBookingStore.Set(phone, updated);
            return updated;
        }

        return null;
    }

    private static bool NeedsValidation(PipelineIntent intent) =>
        intent is PipelineIntent.NewBooking
            or PipelineIntent.ContinueBooking
            or PipelineIntent.ConfirmBooking;
}
