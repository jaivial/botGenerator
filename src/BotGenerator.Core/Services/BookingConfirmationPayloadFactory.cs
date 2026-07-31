using BotGenerator.Core.Models;

namespace BotGenerator.Core.Services;

public static class BookingConfirmationPayloadFactory
{
    public static BookingConfirmationOutboxDraft Create(
        long bookingId,
        string provider,
        string phoneNumber,
        string customerName,
        DateTime bookingDate,
        string bookingTime,
        int guestCount,
        string? arrozType,
        int? arrozServings,
        int highChairs,
        int babyStrollers)
    {
        var formattedDate = bookingDate.ToString("dd/MM/yyyy");
        var policyUrl = "https://alqueriavillacarmen.com/booking_policies.php";
        var cancellationUrl = $"https://alqueriavillacarmen.com/cancel_reservation.php?id={bookingId}";
        var confirmationText = "*Confirmación de Reserva - Alquería Villa Carmen*\n\n";
        confirmationText += $"Hola {customerName},\n\n";
        confirmationText += "Gracias por elegir Alquería Villa Carmen. Su reserva ha sido confirmada:\n\n";
        confirmationText += $"📅 *Fecha:* {formattedDate}\n";
        confirmationText += $"🕒 *Hora:* {bookingTime}\n";
        confirmationText += $"👥 *Personas:* {guestCount}\n";

        if (!string.IsNullOrWhiteSpace(arrozType))
        {
            confirmationText += arrozServings.HasValue
                ? $"🍚 *Arroz:* {arrozType} ({arrozServings.Value} raciones)\n"
                : $"🍚 *Arroz:* {arrozType}\n";
        }
        else
        {
            confirmationText += "🍚 *Arroz:* No\n";
        }

        confirmationText += $"👶 *Tronas:* {highChairs}\n";
        confirmationText += $"🍼 *Carros de bebé:* {babyStrollers}\n\n";
        confirmationText += "Al hacer esta reserva, usted ha confirmado y aceptado las condiciones de reserva y políticas del restaurante.\n\n";
        confirmationText += $"Condiciones de reserva: {policyUrl}\n";
        confirmationText += $"Cancelar reserva: {cancellationUrl}";

        return new BookingConfirmationOutboxDraft
        {
            BookingId = bookingId,
            Provider = provider,
            PhoneNumber = phoneNumber,
            Payload = new BookingConfirmationPayload
            {
                Text = confirmationText,
                LinkButtonsText = "Accesos rápidos a las condiciones y la cancelación de tu reserva:",
                LinkButtons =
                [
                    new BookingConfirmationLink("CONDICIONES", policyUrl),
                    new BookingConfirmationLink("Cancelar Reserva", cancellationUrl)
                ]
            }
        };
    }
}
