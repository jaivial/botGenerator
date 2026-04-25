using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using Microsoft.Extensions.Logging;

namespace BotGenerator.Core.Pipeline;

/// <summary>
/// Node 2: Deterministic validation and enrichment.
/// Checks availability, validates rice, checks duplicates, day status, capacity.
/// Reuses existing services. No AI calls.
/// </summary>
public class ValidationEnrichmentNode : IPipelineNode<(PipelineContext Context, ContextAnalysisResult Analysis), ValidationResult>
{
    private readonly IBookingAvailabilityService _availability;
    private readonly IRiceValidatorService _riceValidator;
    private readonly IBookingRepository _bookingRepository;
    private readonly ILogger<ValidationEnrichmentNode> _logger;

    public ValidationEnrichmentNode(
        IBookingAvailabilityService availability,
        IRiceValidatorService riceValidator,
        IBookingRepository bookingRepository,
        ILogger<ValidationEnrichmentNode> logger)
    {
        _availability = availability;
        _riceValidator = riceValidator;
        _bookingRepository = bookingRepository;
        _logger = logger;
    }

    public async Task<ValidationResult> ProcessAsync(
        (PipelineContext Context, ContextAnalysisResult Analysis) input,
        CancellationToken ct)
    {
        var (context, analysis) = input;
        var result = new ValidationResult();

        // Only validate for booking-related intents
        if (!NeedsValidation(analysis.Intent))
            return result;

        var pending = context.PendingBooking;
        var analysisDate = analysis.ExtractedDate;
        var analysisTime = analysis.ExtractedTime;
        var analysisPeople = analysis.ExtractedPeople;

        // Merge with pending booking data
        var effectiveDate = analysisDate ?? pending?.Date;
        var effectiveTime = analysisTime ?? pending?.Time;
        var effectivePeople = analysisPeople ?? pending?.People ?? 0;

        // Parse the date
        DateTime? parsedDate = null;
        if (!string.IsNullOrEmpty(effectiveDate))
        {
            parsedDate = ParseDate(effectiveDate);
        }

        // Same-day check
        if (parsedDate.HasValue && parsedDate.Value.Date <= DateTime.Now.Date)
        {
            return new ValidationResult { IsSameDay = true, ParsedDate = parsedDate, IsAvailable = false, RejectionReason = "same_day" };
        }

        // 35-day window check
        if (parsedDate.HasValue && parsedDate.Value.Date > DateTime.Now.Date.AddDays(35))
        {
            return new ValidationResult { IsBeyond35Days = true, ParsedDate = parsedDate, IsAvailable = false, RejectionReason = "beyond_35_days" };
        }

        // Day status check
        if (parsedDate.HasValue)
        {
            var dayStatus = await _availability.CheckDayStatusAsync(parsedDate.Value, ct);
            if (!dayStatus.IsOpen)
            {
                return new ValidationResult
                {
                    IsDayClosed = true,
                    ParsedDate = parsedDate,
                    IsAvailable = false,
                    RejectionReason = $"closed_day ({dayStatus.Weekday})",
                    DayName = dayStatus.Weekday
                };
            }

            // Daily capacity check
            var dailyLimit = await _availability.GetDailyLimitAsync(parsedDate.Value, null, ct);
            if (dailyLimit.FreeBookingSeats < effectivePeople && effectivePeople > 0)
            {
                return new ValidationResult
                {
                    IsDayFull = true,
                    ParsedDate = parsedDate,
                    IsAvailable = false,
                    RejectionReason = "daily_full",
                    DayName = dayStatus.Weekday
                };
            }
        }

        // Hour availability check
        if (parsedDate.HasValue && !string.IsNullOrEmpty(effectiveTime) && effectivePeople > 0)
        {
            if (TimeSpan.TryParse(effectiveTime, out var time))
            {
                var decision = await _availability.EvaluateAsync(
                    parsedDate.Value, effectivePeople, time, null, ct);

                if (!decision.IsAvailable)
                {
                    return new ValidationResult
                    {
                        IsAvailable = false,
                        ParsedDate = parsedDate,
                        RejectionReason = decision.Reason,
                        SuggestionMessage = decision.Message,
                        AlternativeHours = decision.SuggestedHours
                    };
                }
            }
        }
        // Date + people check without specific time
        else if (parsedDate.HasValue && effectivePeople > 0)
        {
            var decision = await _availability.EvaluateAsync(
                parsedDate.Value, effectivePeople, null, null, ct);

            if (!decision.IsAvailable)
            {
                return new ValidationResult
                {
                    IsAvailable = false,
                    ParsedDate = parsedDate,
                    RejectionReason = decision.Reason,
                    SuggestionMessage = decision.Message
                };
            }
        }

        // Duplicate booking check
        if (!string.IsNullOrEmpty(effectiveDate) && !string.IsNullOrEmpty(effectiveTime))
        {
            var phone9 = context.Message.SenderNumber;
            if (phone9.StartsWith("34")) phone9 = phone9[2..];
            var existing = context.ExistingBookings;

            var hasDuplicate = existing.Any(b =>
                b.DateFormatted == effectiveDate &&
                b.TimeFormatted == effectiveTime);

            if (hasDuplicate)
            {
                return new ValidationResult
                {
                    HasDuplicateBooking = true,
                    ParsedDate = parsedDate,
                    IsAvailable = false,
                    RejectionReason = "duplicate_booking"
                };
            }
        }

        // Rice validation
        RiceValidationResult? riceValidation = null;
        var effectiveRice = analysis.ExtractedRiceType ?? pending?.ArrozType;
        if (!string.IsNullOrEmpty(effectiveRice) && !analysis.RiceDeclined)
        {
            riceValidation = await _riceValidator.ValidateAsync(
                effectiveRice, context.RestaurantId, ct);

            _logger.LogInformation(
                "Rice validation for '{Rice}': {Status}",
                effectiveRice, riceValidation.Status);
        }

        return new ValidationResult
        {
            IsAvailable = true,
            ParsedDate = parsedDate,
            RiceValidation = riceValidation
        };
    }

    private static bool NeedsValidation(PipelineIntent intent) =>
        intent is PipelineIntent.NewBooking
            or PipelineIntent.ContinueBooking
            or PipelineIntent.ConfirmBooking;

    private static DateTime? ParseDate(string dateStr)
    {
        // Try dd/MM/yyyy
        if (DateTime.TryParseExact(dateStr, "dd/MM/yyyy", null,
            System.Globalization.DateTimeStyles.None, out var d1))
            return d1;

        // Try yyyy-MM-dd
        if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", null,
            System.Globalization.DateTimeStyles.None, out var d2))
            return d2;

        // Fallback
        if (DateTime.TryParse(dateStr, out var d3))
            return d3;

        return null;
    }
}
