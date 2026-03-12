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

        // Skip forwarded confirmation messages from automation systems.
        // These contain structured booking data but are NOT booking requests.
        if (IsForwardedConfirmation(t))
            return false;

        // Deferral patterns like: "hoy mismo te confirmo", "hoy te digo algo"
        // These should NOT be treated as same-day booking.
        if (Regex.IsMatch(t, @"\b(hoy|hoy\s+mismo)\b.*\b(confirm|confirmo|confirmar|aviso|digo|dir[eé]|consult|pregunt|comento|te\s+cuento)\b",
                RegexOptions.IgnoreCase))
        {
            return false;
        }

        // PRIORITY 1: Direct "today" keywords (keep conservative; avoid "hoy mismo" here).
        // These are EXPLICIT same-day booking requests and should trigger rejection
        // regardless of whether the user repeats booking intent words in the same message.
        // Context: User is responding to bot's question about date, so intent is implied.
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

        // PRIORITY 2: Standalone "hoy" responses.
        // When user responds with just "hoy" or "hoy a las X", it's a same-day booking request.
        if (Regex.IsMatch(t, @"\bhoy\b"))
        {
            // Exclude statements about existing reservations
            // "mi reserva es hoy", "tengo reserva hoy", etc. are not new booking requests
            if (Regex.IsMatch(t, @"\b(mi|tengo|tenemos|la|el)\s+reserva\b"))
                return false;

            // Short answers that mean "today" as the requested date.
            var trimmed = t.Trim();
            if (trimmed == "hoy" || Regex.IsMatch(trimmed, @"^hoy\s*(a\s*las)?\s*\d"))
                return true;

            // "hoy" with explicit booking context (but not about existing reservations)
            if (HasBookingContext(t))
                return true;
        }

        // PRIORITY 3: Today's date in dd/MM or dd/MM/yyyy format.
        // Only trigger if combined with booking intent to avoid false positives
        // when user is just mentioning today's date in another context.
        var today = nowLocal.Date;
        var todayPatterns = new[]
        {
            $"{today.Day}/{today.Month}",
            $"{today.Day:D2}/{today.Month:D2}",
            $"{today.Day}/{today.Month}/{today.Year}",
            $"{today.Day:D2}/{today.Month:D2}/{today.Year}"
        };

        if (todayPatterns.Any(pattern => t.Contains(pattern)))
        {
            // Require booking intent for explicit date patterns
            if (HasBookingOrModificationIntent(t))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Detects forwarded confirmation messages from automation systems.
    /// These contain booking details but are NOT new booking requests.
    /// </summary>
    private static bool IsForwardedConfirmation(string text)
    {
        var confirmationPatterns = new[]
        {
            "confirmación de reserva",
            "su reserva ha sido confirmada",
            "📅 fecha:",
            "🕒 hora:",
            "👥 personas:",
            "al hacer esta reserva, usted ha confirmado",
            "gracias por elegir",
            "condiciones de reserva"
        };

        return confirmationPatterns.Any(p => text.Contains(p));
    }

    /// <summary>
    /// Checks if the message contains intent to book, modify, or delete a reservation.
    /// This helps distinguish between "I want to book for today" vs "I have a booking for today and want the menu".
    /// </summary>
    private static bool HasBookingOrModificationIntent(string text)
    {
        var intentKeywords = new[]
        {
            // Booking intent
            "reservar", "quiero reservar", "me gustaría reservar", "quisiera reservar",
            "hacer una reserva", "nueva reserva",
            "mesa para", "comer ", "cenar ", "almorzar ",
            // Modification intent
            "modificar", "cambiar", "editar", "actualizar",
            "quiero cambiar", "me gustaría cambiar",
            // Deletion intent
            "cancelar", "anular",
            // Rice/menu modification with intent
            "quiero pedir", "quiero añadir", "añadir arroz", "pedir arroz",
            "he reservado", "tenemos reservado"
        };

        return intentKeywords.Any(k => text.Contains(k));
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

