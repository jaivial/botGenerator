using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

/// <summary>
/// Calculates confidence scores for AI extractions
/// </summary>
public interface IConfidenceScorerService
{
    /// <summary>
    /// Calculates confidence score for an extraction response
    /// </summary>
    double CalculateConfidence(AiExtractionResponse response, string userMessage);

    /// <summary>
    /// Determines if confidence is sufficient to proceed
    /// </summary>
    bool IsConfidentEnough(double confidence, int fieldsExtracted);

    /// <summary>
    /// Gets confidence level description
    /// </summary>
    string GetConfidenceLevel(double confidence);
}

public class ConfidenceScorerService : IConfidenceScorerService
{
    private readonly ILogger<ConfidenceScorerService> _logger;

    // Confidence thresholds
    private const double HIGH_CONFIDENCE_THRESHOLD = 0.85;
    private const double MEDIUM_CONFIDENCE_THRESHOLD = 0.65;
    private const double LOW_CONFIDENCE_THRESHOLD = 0.45;
    private const double MINIMUM_CONFIDENCE_THRESHOLD = 0.30;

    public ConfidenceScorerService(ILogger<ConfidenceScorerService> logger)
    {
        _logger = logger;
    }

    public double CalculateConfidence(AiExtractionResponse response, string userMessage)
    {
        var baseConfidence = response.Confidence;

        // Count extracted fields
        var extractedFieldsCount = CountExtractedFields(response);

        // Adjust confidence based on various factors
        var adjustments = 0.0;

        // Bonus for multiple fields extracted (indicates clearer intent)
        if (extractedFieldsCount >= 2)
            adjustments += 0.05;
        if (extractedFieldsCount >= 3)
            adjustments += 0.05;

        // Penalty for very short messages (less context)
        if (userMessage.Length < 10)
            adjustments -= 0.10;

        // Bonus for corrections (usually clearer intent)
        if (response.IsCorrection)
            adjustments += 0.05;

        // Penalty for unclear goals
        if (response.UserGoal == "unclear")
            adjustments -= 0.15;

        // Bonus for clear goals
        if (response.UserGoal == "change_both" || response.UserGoal == "change_date" || response.UserGoal == "change_time")
            adjustments += 0.05;

        // Check for common ambiguous patterns
        if (IsAmbiguousMessage(userMessage))
            adjustments -= 0.20;

        // Calculate final confidence
        var finalConfidence = Math.Max(0.0, Math.Min(1.0, baseConfidence + adjustments));

        _logger.LogDebug(
            "Confidence calculation: base={Base}, adjustments={Adjustments}, final={Final}, fields={FieldsCount}",
            baseConfidence, adjustments, finalConfidence, extractedFieldsCount);

        return finalConfidence;
    }

    public bool IsConfidentEnough(double confidence, int fieldsExtracted)
    {
        // If no fields extracted, always need clarification
        if (fieldsExtracted == 0)
            return false;

        // If only one field extracted, require higher confidence
        if (fieldsExtracted == 1)
            return confidence >= HIGH_CONFIDENCE_THRESHOLD;

        // If multiple fields extracted, accept medium confidence
        if (fieldsExtracted >= 2)
            return confidence >= MEDIUM_CONFIDENCE_THRESHOLD;

        // Default to medium threshold
        return confidence >= MEDIUM_CONFIDENCE_THRESHOLD;
    }

    public string GetConfidenceLevel(double confidence)
    {
        return confidence switch
        {
            >= HIGH_CONFIDENCE_THRESHOLD => "HIGH",
            >= MEDIUM_CONFIDENCE_THRESHOLD => "MEDIUM",
            >= LOW_CONFIDENCE_THRESHOLD => "LOW",
            >= MINIMUM_CONFIDENCE_THRESHOLD => "VERY_LOW",
            _ => "INSUFFICIENT"
        };
    }

    private int CountExtractedFields(AiExtractionResponse response)
    {
        var count = 0;

        if (!string.IsNullOrEmpty(response.Date)) count++;
        if (!string.IsNullOrEmpty(response.Time)) count++;
        if (response.PartySize.HasValue) count++;
        if (!string.IsNullOrEmpty(response.RiceType)) count++;
        if (response.RiceServings.HasValue) count++;
        if (response.Tronas.HasValue) count++;
        if (response.Carritos.HasValue) count++;

        return count;
    }

    private bool IsAmbiguousMessage(string message)
    {
        var lowerMessage = message.ToLower().Trim();

        // Very vague messages
        var ambiguousPatterns = new[]
        {
            "todo", "cambialo", "cámbialo", "quiero cambiar", "modificar",
            "no me vale", "no me gusta", "otra cosa"
        };

        // Check if message is ONLY an ambiguous pattern (no numbers, dates, times)
        var hasNumbers = System.Text.RegularExpressions.Regex.IsMatch(message, @"\d+");
        var hasTimePattern = System.Text.RegularExpressions.Regex.IsMatch(message, @"(\d{1,2}[:\.]\d{2})|(mañana|tarde|noche)", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var hasDayPattern = System.Text.RegularExpressions.Regex.IsMatch(message, 
            @"(lunes|martes|miércoles|jueves|viernes|sábado|domingo)", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // If message has specific information, it's not ambiguous
        if (hasNumbers || hasTimePattern || hasDayPattern)
            return false;

        // Check if it's a vague pattern
        return ambiguousPatterns.Any(p => lowerMessage.Contains(p));
    }
}
