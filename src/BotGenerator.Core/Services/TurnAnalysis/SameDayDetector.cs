using System.Text.RegularExpressions;

namespace BotGenerator.Core.Services.TurnAnalysis;

public static class SameDayDetector
{
    public static bool IsSameDayBookingRequest(string text, DateTime? now = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var t = text.ToLowerInvariant();
        var nowLocal = now ?? DateTime.Now;

        // Deferral patterns like: "hoy mismo te confirmo", "hoy te digo algo"
        // These should NOT be treated as same-day booking.
        if (Regex.IsMatch(t, @"\b(hoy|hoy\s+mismo)\b.*\b(confirm|confirmo|confirmar|aviso|digo|dir[eé]|consult|pregunt|comento|te\s+cuento)\b",
                RegexOptions.IgnoreCase))
        {
            // Only allow same-day if it's clearly a booking attempt.
            if (!HasBookingContext(t))
                return false;
        }

        // Direct "today" keywords (keep conservative; avoid "hoy mismo" here).
        var sameDayKeywords = new[]
        {
            "para hoy",
            "reservar hoy",
            "reserva hoy",
            "mesa hoy",
            "hoy para",
            "el día de hoy",
            "dia de hoy",
            "esta tarde",
            "esta noche",
            "ahora mismo"
        };

        if (sameDayKeywords.Any(keyword => t.Contains(keyword)))
            return true;

        // Standalone "hoy" with booking context.
        if (Regex.IsMatch(t, @"\bhoy\b"))
        {
            if (HasBookingContext(t))
                return true;

            // Short answers that mean "today" as the requested date.
            var trimmed = t.Trim();
            if (trimmed == "hoy" || Regex.IsMatch(trimmed, @"^hoy\s*(a\s*las)?\s*\d"))
                return true;
        }

        // Check for today's date in dd/MM or dd/MM/yyyy format.
        var today = nowLocal.Date;
        var todayPatterns = new[]
        {
            $"{today.Day}/{today.Month}",
            $"{today.Day:D2}/{today.Month:D2}",
            $"{today.Day}/{today.Month}/{today.Year}",
            $"{today.Day:D2}/{today.Month:D2}/{today.Year}"
        };

        if (todayPatterns.Any(pattern => t.Contains(pattern)))
            return true;

        return false;
    }

    private static bool HasBookingContext(string lowerText)
    {
        var bookingContextWords = new[]
        {
            "reserv", "reserva", "mesa", "comer", "cena", "almorz", "personas", "gente", "sitio", "hueco"
        };

        return bookingContextWords.Any(ctx => lowerText.Contains(ctx));
    }
}

