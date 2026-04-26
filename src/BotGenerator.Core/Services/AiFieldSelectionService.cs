using Microsoft.Extensions.Logging;

namespace BotGenerator.Core.Services;

/// <summary>
/// AI-powered field selection. Determines what booking field the user wants to modify.
/// </summary>
public class AiFieldSelectionService : IAiFieldSelectionService
{
    private readonly IGeminiService _gemini;
    private readonly ILogger<AiFieldSelectionService> _logger;

    public AiFieldSelectionService(
        IGeminiService gemini,
        ILogger<AiFieldSelectionService> logger)
    {
        _gemini = gemini;
        _logger = logger;
    }

    public async Task<string?> DetectFieldAsync(
        string userMessage,
        string bookingSummary,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = @"Eres un clasificador de campos de reserva de restaurante.

Dado el mensaje del usuario y su reserva actual, determina qué campo quiere modificar.

Posibles campos:
- date: quiere cambiar la fecha (""cambiar fecha"", ""para el sábado"", ""otro día"", ""1"", número de menú para fecha)
- time: quiere cambiar la hora (""cambiar hora"", ""a las 14:30"", ""más tarde"", ""2"", número de menú para hora)
- party_size: quiere cambiar personas (""más personas"", ""somos 8"", ""3"", número de menú para personas)
- rice: quiere cambiar/quitar/añadir arroz (""quiero arroz"", ""paella"", ""fideuá"", ""quitar arroz"", ""4"", número de menú para arroz)
- tronas: quiere cambiar tronas (""tronas"", ""5"", número de menú para tronas)
- carritos: quiere cambiar carritos (""carritos"", ""cochecitos"", ""6"", número de menú para carritos)

IMPORTANTE: Los números ""1"" a ""6"" corresponden al menú de opciones del bot (1=fecha, 2=hora, 3=personas, 4=arroz, 5=tronas, 6=carritos).

Si el mensaje contiene un valor que IMPLICA un campo (ej: ""14:30"" implica hora, ""25/06"" implica fecha, ""8 personas"" implica party_size), elige ese campo.

Si también incluye el nombre del menú anterior (ej: ""Arroz"" como respuesta al menú), selecciona ""rice"".

Responde SOLO con el nombre del campo (date, time, party_size, rice, tronas, carritos) o UNCLEAR.";

        var userPrompt = $@"Reserva actual: {bookingSummary}

Mensaje del usuario: ""{userMessage}""

¿Qué campo quiere modificar?";

        try
        {
            var config = new GeminiGenerationConfig
            {
                Temperature = 0.0,
                MaxOutputTokens = 20
            };

            var response = await _gemini.GenerateAsync(systemPrompt, userPrompt, null, config, cancellationToken);
            var cleaned = response.Trim().ToLowerInvariant();

            _logger.LogInformation(
                "AiFieldSelection for '{Message}': AI returned '{Response}'",
                userMessage, cleaned);

            var validFields = new[] { "date", "time", "party_size", "rice", "tronas", "carritos" };

            foreach (var field in validFields)
            {
                if (cleaned.Contains(field))
                    return field;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AiFieldSelection failed for message: '{Message}'", userMessage);
            return null;
        }
    }
}
