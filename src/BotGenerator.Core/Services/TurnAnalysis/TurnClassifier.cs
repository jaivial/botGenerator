using System.Text.RegularExpressions;

namespace BotGenerator.Core.Services.TurnAnalysis;

public static class TurnClassifier
{
    public static bool IsRiceOfferMessage(string lastAssistantMessage)
    {
        if (string.IsNullOrWhiteSpace(lastAssistantMessage))
            return false;

        // Keep this aligned with IntentRouterService.CheckRiceOfferResponseAsync.
        return lastAssistantMessage.Contains("¿Le gustaría reservar arroz", StringComparison.OrdinalIgnoreCase) ||
               lastAssistantMessage.Contains("¿Quieres que añadamos arroz", StringComparison.OrdinalIgnoreCase) ||
               lastAssistantMessage.Contains("¿Queréis añadir arroz", StringComparison.OrdinalIgnoreCase) ||
               lastAssistantMessage.Contains("¿Os apetece arroz", StringComparison.OrdinalIgnoreCase) ||
               lastAssistantMessage.Contains("¿queréis arroz", StringComparison.OrdinalIgnoreCase) ||
               (lastAssistantMessage.Contains("variedad de arroces", StringComparison.OrdinalIgnoreCase) &&
                lastAssistantMessage.Contains("reserva", StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsRiceOfferDecline(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText))
            return false;

        var t = userText.ToLowerInvariant().Trim();

        return Regex.IsMatch(
            t,
            @"\b(no|nada|sin\s+arroz|otra\s+cosa|no\s+queremos|no\s+gracias|ya\s+tenemos|hemos\s+decidido|pediremos?\s+otra)\b",
            RegexOptions.IgnoreCase);
    }

    public static bool IsRiceDecisionDeferral(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText))
            return false;

        var t = userText.ToLowerInvariant();

        // Examples:
        // - "Déjeme que pregunte a mi marido y hoy mismo le confirmo"
        // - "Lo consulto y te digo algo"
        // - "Ahora te confirmo"
        return Regex.IsMatch(
            t,
            @"\b(pregunt|consult|lo\s+miro|lo\s+vemos|lo\s+hablo|confirmo|te\s+confirmo|le\s+confirmo|lo\s+confirmo|confirmo\s+hoy|hoy\s+mismo\s+(?:te|le|lo)\s+confirmo|te\s+digo|le\s+digo|te\s+aviso|le\s+aviso|ahora\s+te\s+digo)\b",
            RegexOptions.IgnoreCase) &&
               !IsRiceOfferDecline(userText);
    }

    public static string BuildRiceDeferralReply()
    {
        return "Perfecto. Tu reserva sigue *sin arroz* por ahora.\n\n" +
               "Cuando lo sepáis, escríbeme y dime si queréis añadir arroz (y cuál) y para cuántas raciones.";
    }
}
