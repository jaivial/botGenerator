using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using Microsoft.Extensions.Logging;
using System.Text;

namespace BotGenerator.Core.Pipeline;

/// <summary>
/// Node 3: AI-powered response generation.
/// Takes the classified intent + validation results + conversation context
/// and generates the final WhatsApp message.
/// </summary>
public class ResponseGeneratorNode : IPipelineNode<(PipelineContext Context, ContextAnalysisResult Analysis, ValidationResult? Validation), string>
{
    private readonly IGeminiService _ai;
    private readonly IContextBuilderService _contextBuilder;
    private readonly IOpeningHoursService? _openingHours;
    private readonly IMenuRepository _menuRepository;
    private readonly ILogger<ResponseGeneratorNode> _logger;

    public ResponseGeneratorNode(
        IGeminiService ai,
        IContextBuilderService contextBuilder,
        IOpeningHoursService? openingHours,
        IMenuRepository menuRepository,
        ILogger<ResponseGeneratorNode> logger)
    {
        _ai = ai;
        _contextBuilder = contextBuilder;
        _openingHours = openingHours;
        _menuRepository = menuRepository;
        _logger = logger;
    }

    public async Task<string> ProcessAsync(
        (PipelineContext Context, ContextAnalysisResult Analysis, ValidationResult? Validation) input,
        CancellationToken ct)
    {
        var (context, analysis, validation) = input;

        var prompt = await BuildPromptAsync(context, analysis, validation, ct);
        var userMessage = context.Message.MessageText;
        var history = context.History;

        var config = new GeminiGenerationConfig
        {
            Temperature = 0.7,
            MaxOutputTokens = 1024
        };

        var response = await _ai.GenerateAsync(prompt, userMessage, history, config, ct);

        _logger.LogInformation(
            "ResponseGenerator produced response for intent {Intent}: {Response}",
            analysis.Intent,
            response.Length > 100 ? response[..100] + "..." : response);

        return CleanForWhatsApp(response);
    }

    private async Task<string> BuildPromptAsync(
        PipelineContext context,
        ContextAnalysisResult analysis,
        ValidationResult? validation,
        CancellationToken ct)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# SISTEMA DE ASISTENTE DE RESERVAS - ALQUERÍA VILLA CARMEN");
        sb.AppendLine();
        sb.AppendLine("Eres el asistente virtual de Alquería Villa Carmen, un restaurante en Valencia especializado en arroces y paellas.");
        sb.AppendLine($"Estás conversando con **{context.PushName}** por WhatsApp.");
        sb.AppendLine();

        // Current date/time
        sb.AppendLine("## FECHA Y HORA ACTUAL");
        sb.AppendLine($"- Hoy: {context.TodayES} ({context.TodayFormatted})");
        sb.AppendLine();

        // Existing bookings
        if (context.ExistingBookings.Count > 0)
        {
            sb.AppendLine("## RESERVAS EXISTENTES DEL CLIENTE");
            foreach (var b in context.ExistingBookings)
            {
                var dayName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
                    b.ReservationDate.ToString("dddd"));
                var rice = string.IsNullOrEmpty(b.ArrozType) ? "Sin arroz" : $"{b.ArrozType} ({b.ArrozServings} raciones)";
                sb.AppendLine($"- {dayName} {b.DateFormatted} a las {b.TimeFormatted}: {b.PartySize} personas | {rice}");
            }
            sb.AppendLine();
        }

        // Pending booking state
        if (context.PendingBooking != null)
        {
            sb.AppendLine("## RESERVA EN CURSO (DATOS RECOPILADOS)");
            var p = context.PendingBooking;
            sb.AppendLine($"- Fecha: {(string.IsNullOrEmpty(p.Date) ? "FALTA" : p.Date)}");
            sb.AppendLine($"- Hora: {(string.IsNullOrEmpty(p.Time) ? "FALTA" : p.Time)}");
            sb.AppendLine($"- Personas: {(p.People > 0 ? p.People : "FALTA")}");
            if (p.ArrozType != null)
                sb.AppendLine($"- Arroz: {(string.IsNullOrEmpty(p.ArrozType) ? "Sin arroz" : $"{p.ArrozType} ({p.ArrozServings} raciones)")}");
            else
                sb.AppendLine("- Arroz: FALTA DECIDIR");
            if (p.HighChairs != 0) sb.AppendLine($"- Tronas: {p.HighChairs}");
            if (p.BabyStrollers != 0) sb.AppendLine($"- Carritos: {p.BabyStrollers}");
            sb.AppendLine($"- Resumen mostrado: {p.SummaryShown}");
            sb.AppendLine();
        }

        // Classified intent
        sb.AppendLine("## INTENCIÓN CLASIFICADA");
        sb.AppendLine($"- Intención: {analysis.Intent}");
        sb.AppendLine($"- Confianza: {analysis.Confidence:P0}");
        sb.AppendLine($"- Razón: {analysis.Reasoning}");
        sb.AppendLine();

        // Extracted data
        if (analysis.ExtractedDate != null || analysis.ExtractedTime != null || analysis.ExtractedPeople != null)
        {
            sb.AppendLine("## DATOS EXTRAIDOS DEL MENSAJE");
            if (analysis.ExtractedDate != null) sb.AppendLine($"- Fecha: {analysis.ExtractedDate}");
            if (analysis.ExtractedTime != null) sb.AppendLine($"- Hora: {analysis.ExtractedTime}");
            if (analysis.ExtractedPeople != null) sb.AppendLine($"- Personas: {analysis.ExtractedPeople}");
            if (analysis.ExtractedRiceType != null) sb.AppendLine($"- Arroz: {analysis.ExtractedRiceType}");
            if (analysis.RiceDeclined) sb.AppendLine("- Arroz: Rechazado (sin arroz)");
            if (analysis.ExtractedRiceServings != null) sb.AppendLine($"- Raciones: {analysis.ExtractedRiceServings}");
            if (analysis.ExtractedHighChairs != null) sb.AppendLine($"- Tronas: {analysis.ExtractedHighChairs}");
            if (analysis.ExtractedBabyStrollers != null) sb.AppendLine($"- Carritos: {analysis.ExtractedBabyStrollers}");
            sb.AppendLine();
        }

        // Validation results
        if (validation != null && !validation.IsAvailable)
        {
            sb.AppendLine("## RESULTADO DE VALIDACIÓN");
            sb.AppendLine($"- Disponible: NO");
            sb.AppendLine($"- Motivo rechazo: {validation.RejectionReason}");
            if (validation.SuggestionMessage != null) sb.AppendLine($"- Sugerencia: {validation.SuggestionMessage}");
            if (validation.AlternativeHours?.Count > 0)
                sb.AppendLine($"- Horas alternativas: {string.Join(", ", validation.AlternativeHours)}");
            sb.AppendLine();
        }
        else if (validation?.RiceValidation != null)
        {
            var rv = validation.RiceValidation;
            if (rv.Status == "not_found")
            {
                sb.AppendLine("## VALIDACIÓN DE ARROZ");
                sb.AppendLine($"- Estado: No encontrado");
                sb.AppendLine($"- Mensaje: {rv.Message}");
                sb.AppendLine();
            }
            else if (rv.Status == "multiple")
            {
                sb.AppendLine("## VALIDACIÓN DE ARROZ");
                sb.AppendLine($"- Estado: Múltiples opciones");
                sb.AppendLine($"- Opciones: {string.Join(", ", rv.Options ?? new())}");
                sb.AppendLine();
            }
        }

        // Get available rice types
        try
        {
            var riceTypes = await _menuRepository.GetActiveRiceTypesAsync(ct);
            if (riceTypes.Count > 0)
            {
                sb.AppendLine("## TIPOS DE ARROZ DISPONIBLES");
                sb.AppendLine(string.Join(", ", riceTypes));
                sb.AppendLine();
            }
        }
        catch { /* non-critical */ }

        // Opening hours
        if (validation?.ParsedDate != null && _openingHours != null)
        {
            try
            {
                var hours = await _openingHours.GetContextAwareHoursAsync(validation.ParsedDate.Value, ct);
                sb.AppendLine($"## HORARIOS DEL DÍA ({validation.ParsedDate:dd/MM/yyyy})");
                sb.AppendLine($"- Abrimos: {hours.OpeningTimeFormatted}");
                sb.AppendLine($"- Cerramos: {hours.ClosingTimeFormatted}");
                sb.AppendLine($"- Horas disponibles: {string.Join(", ", hours.AvailableSlots)}");
                sb.AppendLine();
            }
            catch { /* fallback to defaults */ }
        }

        // Intent-specific instructions
        sb.AppendLine("## INSTRUCCIONES SEGÚN INTENCIÓN");
        sb.AppendLine(GetIntentInstructions(analysis.Intent, context, analysis, validation));
        sb.AppendLine();

        // Style rules
        sb.AppendLine("## REGLAS DE ESTILO");
        sb.AppendLine("- Respuestas CORTAS y NATURALES (máximo 2-3 líneas)");
        sb.AppendLine("- Agrupa preguntas básicas: '¿Para qué día, a qué hora y cuántas personas?'");
        sb.AppendLine("- Una pregunta a la vez para extras (arroz, tronas, carritos)");
        sb.AppendLine("- Usa emojis con moderación");
        sb.AppendLine("- NUNCA hagas listas numeradas de preguntas");
        sb.AppendLine("- NUNCA repitas información ya proporcionada");
        sb.AppendLine("- Usar negrita (*texto*) solo para info importante");
        sb.AppendLine("- Si el usuario pide reservar para HOY, rechaza: 'Lo siento, no aceptamos reservas para el mismo día por WhatsApp. Para reservas urgentes, llámanos al 638 857 294.'");
        sb.AppendLine("- Teléfono del restaurante: +34 638 857 294");
        sb.AppendLine("- Máximo 3 tronas y 3 carritos");
        sb.AppendLine("- Arroz: mínimo 2 raciones, solo 1 tipo por reserva");
        sb.AppendLine("- Horario de apertura: 13:30-18:00 (lunes a domingo)");
        sb.AppendLine("- NO aceptes horas antes de las 13:30 ni después del cierre");
        sb.AppendLine("- Solo respondes con el mensaje para WhatsApp, sin explicaciones ni metadatos");

        return sb.ToString();
    }

    private static string GetIntentInstructions(
        PipelineIntent intent,
        PipelineContext context,
        ContextAnalysisResult analysis,
        ValidationResult? validation)
    {
        var pending = context.PendingBooking;

        return intent switch
        {
            PipelineIntent.Acknowledgment =>
                "El usuario está agradeciendo o reconociendo un mensaje. Responde con un agradecimiento breve y ofrece ayuda si la necesita. Ejemplo: 'De nada! Si necesitas algo más, aquí estoy.'",

            PipelineIntent.BroadcastReply =>
                "El usuario responde a un mensaje promocional/informativo con un agradecimiento breve. NO inicies flujo de reserva. Responde agradeciendo y ofreciendo ayuda.",

            PipelineIntent.Greeting =>
                context.ExistingBookings.Count > 0
                    ? $"El cliente te saluda y tiene una reserva activa. Saluda mencionando su reserva de forma breve. Ejemplo: '¡Hola {context.PushName}! Tienes reserva el {context.ExistingBookings[0].DateFormatted} a las {context.ExistingBookings[0].TimeFormatted}. ¿Te ayudo en algo?'"
                    : "El cliente te saluda. Saluda de forma amable y pregunta si quiere hacer una reserva.",

            PipelineIntent.OffTopic =>
                "El usuario hace una pregunta no relacionada con reservas. Responde de forma útil. Si preguntas sobre menú, dirección: Carrera del Riu 48, Sedaví, Valencia. Teléfono: 638 857 294.",

            PipelineIntent.InfoRequest =>
                "El usuario pregunta por sus reservas existentes. Muestra la información de sus reservas activas de forma clara.",

            PipelineIntent.NewBooking =>
                GetNewBookingInstructions(pending, analysis, validation),

            PipelineIntent.ContinueBooking =>
                GetContinueBookingInstructions(pending, analysis, validation),

            PipelineIntent.ConfirmBooking =>
                pending?.SummaryShown == true
                    ? "El usuario confirma la reserva. Genera el resumen final de confirmación."
                    : "El usuario parece confirmar pero aún no se mostró el resumen. Muestra el resumen de la reserva pendiente.",

            PipelineIntent.DeclineBooking =>
                "El usuario declina la reserva después de ver el resumen. Responde que no hay problema y que puede reservar cuando quiera.",

            PipelineIntent.Modification =>
                "El usuario quiere modificar su reserva existente. Pregunta qué quiere cambiar (fecha, hora, personas, arroz, tronas, carritos).",

            PipelineIntent.Cancellation =>
                "El usuario quiere cancelar su reserva. Confirma cuál reserva quiere cancelar.",

            PipelineIntent.SameDayBooking =>
                "El usuario quiere reservar para HOY. RECHAZA: 'Lo siento, no aceptamos reservas para el mismo día por WhatsApp. Para reservas urgentes, llámanos al 638 857 294.'",

            PipelineIntent.EventInquiry =>
                "El usuario pregunta por un evento especial (boda, cumpleaños, etc.). Responde que para eventos especiales pueden llamar al 638 857 294 o visitar la web.",

            _ => "Responde de forma útil y natural."
        };
    }

    private static string GetNewBookingInstructions(
        BookingData? pending,
        ContextAnalysisResult analysis,
        ValidationResult? validation)
    {
        if (validation != null && !validation.IsAvailable)
        {
            return $"La reserva NO está disponible: {validation.RejectionReason}. " +
                   (validation.SuggestionMessage ?? "Sugiere otra fecha u hora.");
        }

        if (pending == null)
        {
            return "Inicia el flujo de reserva. Pregunta por los datos que faltan de forma agrupada. " +
                   "Si solo tiene la fecha, pregunta hora y personas. Si tiene fecha, hora y personas, pregunta si quiere arroz.";
        }

        return GetContinueBookingInstructions(pending, analysis, validation);
    }

    private static string GetContinueBookingInstructions(
        BookingData? pending,
        ContextAnalysisResult analysis,
        ValidationResult? validation)
    {
        if (pending == null)
            return GetNewBookingInstructions(null, analysis, validation);

        if (validation != null && !validation.IsAvailable)
        {
            return $"La reserva NO está disponible: {validation.RejectionReason}. " +
                   (validation.SuggestionMessage ?? "Sugiere otra fecha u hora.");
        }

        var missing = new List<string>();
        if (string.IsNullOrEmpty(pending.Date) && analysis.ExtractedDate == null) missing.Add("fecha");
        if (string.IsNullOrEmpty(pending.Time) && analysis.ExtractedTime == null) missing.Add("hora");
        if (pending.People <= 0 && analysis.ExtractedPeople == null) missing.Add("personas");

        if (missing.Count > 0)
            return $"Faltan datos: {string.Join(", ", missing)}. Pregunta por ellos de forma natural.";

        // Rice decision
        if (pending.ArrozType == null && analysis.ExtractedRiceType == null && !analysis.RiceDeclined)
            return "Datos básicos completos. Pregunta si quiere arroz.";

        // Rice type provided but no servings
        var riceType = analysis.ExtractedRiceType ?? pending.ArrozType;
        if (!string.IsNullOrEmpty(riceType) && (pending.ArrozServings == null || pending.ArrozServings <= 0) && analysis.ExtractedRiceServings == null)
            return $"Arroz elegido: {riceType}. Pregunta cuántas raciones (mínimo 2).";

        // Rice validation issue
        if (validation?.RiceValidation?.Status == "not_found")
            return "El arroz solicitado no está disponible. Informale y sugiere alternativas.";
        if (validation?.RiceValidation?.Status == "multiple")
            return $"Hay varios arroces que coinciden: {string.Join(", ", validation.RiceValidation.Options ?? new())}. Pregunta cuál prefiere.";

        // All data collected, show summary
        if (!pending.SummaryShown)
            return "Todos los datos recopilados. Muestra el resumen de la reserva y pide confirmación. Ejemplo: 'Perfecto, te confirmo: *Fecha:* ..., *Hora:* ..., *Personas:* ..., *Arroz:* ... ¿Todo correcto?'";

        return "La reserva ya ha sido confirmada por el usuario. Este caso no debería llegar aquí.";
    }

    private static string CleanForWhatsApp(string text)
    {
        // Remove markdown code blocks if present
        var cleaned = text.Trim();
        if (cleaned.StartsWith("```"))
        {
            var start = cleaned.IndexOf('\n');
            var end = cleaned.LastIndexOf("```");
            if (start >= 0 && end > start)
                cleaned = cleaned[(start + 1)..end].Trim();
        }

        return cleaned;
    }
}
