using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using Microsoft.Extensions.Logging;

namespace BotGenerator.Core.Pipeline;

/// <summary>
/// Node 0: State-aware pre-router. Runs BEFORE ContextAnalyzer.
/// Checks active modification/cancellation/pending booking states and produces
/// a description that gets injected into the AI classifier prompt.
/// This fixes the root cause of intent misclassification when users confirm within
/// multi-turn flows (the classifier didn't know about active state).
/// </summary>
public class StateAwarePreRouter
{
    private readonly IModificationStateStore _modificationStateStore;
    private readonly ICancellationStateStore _cancellationStateStore;
    private readonly IPendingBookingStore _pendingBookingStore;
    private readonly ILogger<StateAwarePreRouter> _logger;

    public StateAwarePreRouter(
        IModificationStateStore modificationStateStore,
        ICancellationStateStore cancellationStateStore,
        IPendingBookingStore pendingBookingStore,
        ILogger<StateAwarePreRouter> logger)
    {
        _modificationStateStore = modificationStateStore;
        _cancellationStateStore = cancellationStateStore;
        _pendingBookingStore = pendingBookingStore;
        _logger = logger;
    }

    /// <summary>
    /// Produces a state description to inject into the ContextAnalyzer prompt.
    /// Returns null if no active state.
    /// </summary>
    public string? GetStateDescription(string senderPhone)
    {
        var parts = new List<string>();

        // Check modification state
        var modState = _modificationStateStore.Get(senderPhone);
        if (modState != null)
        {
            var booking = modState.SelectedBooking;
            var bookingInfo = booking != null
                ? $"{booking.DateFormatted} a las {booking.TimeFormatted} ({booking.PartySize} personas)"
                : "no seleccionada";

            var desc = $"ACTIVE_MODIFICATION_FLOW: stage={modState.Stage}, " +
                       $"field={modState.FieldToModify ?? "none"}, " +
                       $"booking={bookingInfo}";

            if (modState.PendingChanges != null)
            {
                var changes = new List<string>();
                if (modState.PendingChanges.ReservationDate != null)
                    changes.Add($"date→{modState.PendingChanges.ReservationDate}");
                if (modState.PendingChanges.ReservationTime != null)
                    changes.Add($"time→{modState.PendingChanges.ReservationTime}");
                if (modState.PendingChanges.PartySize.HasValue)
                    changes.Add($"people→{modState.PendingChanges.PartySize}");
                if (modState.PendingChanges.ArrozType != null)
                    changes.Add($"rice→{modState.PendingChanges.ArrozType}");
                if (modState.PendingChanges.ArrozServings.HasValue)
                    changes.Add($"servings→{modState.PendingChanges.ArrozServings}");
                if (modState.PendingChanges.ClearRice)
                    changes.Add("rice→CLEARED");

                if (changes.Count > 0)
                    desc += $", pendingChanges=[{string.Join(", ", changes)}]";
            }

            if (modState.Stage == ModificationStage.AwaitingConfirmation)
            {
                desc += ". CRITICAL: User has been shown a confirmation summary and is likely responding with si/no to CONFIRM or REJECT the modification.";
            }

            parts.Add(desc);
        }

        // Check cancellation state
        var cancelState = _cancellationStateStore.Get(senderPhone);
        if (cancelState != null)
        {
            var booking = cancelState.SelectedBooking;
            var bookingInfo = booking != null
                ? $"{booking.DateFormatted} a las {booking.TimeFormatted} ({booking.PartySize} personas)"
                : "no seleccionada";

            var desc = $"ACTIVE_CANCELLATION_FLOW: stage={cancelState.Stage}, booking={bookingInfo}";

            if (cancelState.Stage == CancellationStage.AwaitingConfirmation)
            {
                desc += ". CRITICAL: User has been shown a cancellation confirmation and is likely responding with si/no to CONFIRM or REJECT the cancellation.";
            }

            parts.Add(desc);
        }

        // Check pending booking state
        var pendingBooking = _pendingBookingStore.Get(senderPhone);
        if (pendingBooking != null)
        {
            var desc = $"ACTIVE_BOOKING_FLOW: date={pendingBooking.Date ?? "missing"}, " +
                       $"time={pendingBooking.Time ?? "missing"}, " +
                       $"people={pendingBooking.People}, " +
                       $"rice={pendingBooking.ArrozType ?? "undecided"}, " +
                       $"summaryShown={pendingBooking.SummaryShown}";

            if (pendingBooking.SummaryShown)
            {
                desc += ". User has been shown a booking summary and is likely confirming with si/ok.";
            }

            parts.Add(desc);
        }

        if (parts.Count == 0)
            return null;

        var result = string.Join("\n", parts);
        _logger.LogDebug("StateAwarePreRouter for {Phone}: {State}", senderPhone, result);
        return result;
    }

    /// <summary>
    /// Returns the booking ID that should be excluded from availability checks
    /// due to active modification or cancellation flows.
    /// </summary>
    public int? GetExcludeBookingId(string senderPhone)
    {
        var modState = _modificationStateStore.Get(senderPhone);
        if (modState?.SelectedBooking != null)
            return modState.SelectedBooking.Id;

        var cancelState = _cancellationStateStore.Get(senderPhone);
        if (cancelState?.SelectedBooking != null)
            return cancelState.SelectedBooking.Id;

        return null;
    }
}
