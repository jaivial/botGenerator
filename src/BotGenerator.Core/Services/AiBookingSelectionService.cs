using System.Text.Json;
using BotGenerator.Core.Models;
using Microsoft.Extensions.Logging;

namespace BotGenerator.Core.Services;

/// <summary>
/// AI-powered booking selection. Understands natural language references to bookings
/// including typos, partial descriptions, ordinals, and informal language.
/// </summary>
public class AiBookingSelectionService : IAiBookingSelectionService
{
    private readonly IGeminiService _gemini;
    private readonly ILogger<AiBookingSelectionService> _logger;

    public AiBookingSelectionService(
        IGeminiService gemini,
        ILogger<AiBookingSelectionService> logger)
    {
        _gemini = gemini;
        _logger = logger;
    }

    public async Task<BookingRecord?> SelectBookingAsync(
        string userMessage,
        List<BookingRecord> bookings,
        CancellationToken cancellationToken = default)
    {
        if (bookings.Count == 0) return null;
        if (bookings.Count == 1) return bookings[0];

        var bookingList = string.Join("\n", bookings.Select((b, i) =>
        {
            var rice = string.IsNullOrEmpty(b.ArrozType) ? "" : $", {b.ArrozType} ({b.ArrozServings} raciones)";
            return $"{i + 1}. {b.DateFormatted} ({b.DayName}) a las {b.TimeFormatted} — {b.PartySize} personas{rice}";
        }));

        var systemPrompt = @"Eres un selector de reservas de restaurante. Dado el mensaje del usuario y su lista de reservas, determina a cuál se refiere.

REGLAS:
- Sé extremadamente flexible con errores tipográficos: ""14;30"" = 14:30, ""14.30"" = 14:30, ""2raciones"" = 2 raciones
- Entiende referencias naturales: ""la de las 14:30"", ""la del sábado"", ""es la misma pero diferente hora"", ""la primera"", ""la segunda""
- Entiende referencias por contenido: ""la de 4 personas"", ""la que tiene arroz del señoret""
- Si el usuario da información que coincide con MÁS de una reserva, elige la que tenga más coincidencias
- Si no puedes determinar cuál, responde UNCLEAR

Responde SOLO con el número de la reserva (1, 2, 3...) o ""UNCLEAR"" si no queda claro.";

        var userPrompt = $@"Reservas del cliente:
{bookingList}

Mensaje del usuario: ""{userMessage}""

¿A cuál reserva se refiere?";

        try
        {
            var config = new GeminiGenerationConfig
            {
                Temperature = 0.0,
                MaxOutputTokens = 50
            };

            var response = await _gemini.GenerateAsync(systemPrompt, userPrompt, null, config, cancellationToken);
            var cleaned = response.Trim().ToUpperInvariant();

            _logger.LogInformation(
                "AiBookingSelection for '{Message}' with {Count} bookings: AI returned '{Response}'",
                userMessage, bookings.Count, cleaned);

            // Try to extract a number
            if (cleaned.Contains("UNCLEAR"))
                return null;

            // Find number in response
            var numStr = new string(cleaned.Where(char.IsDigit).ToArray());
            if (int.TryParse(numStr, out var num) && num >= 1 && num <= bookings.Count)
                return bookings[num - 1];

            // Try parsing full response as number
            if (int.TryParse(cleaned, out var directNum) && directNum >= 1 && directNum <= bookings.Count)
                return bookings[directNum - 1];

            _logger.LogWarning("AiBookingSelection could not parse AI response: '{Response}'", response);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AiBookingSelection failed for message: '{Message}'", userMessage);
            return null;
        }
    }
}
