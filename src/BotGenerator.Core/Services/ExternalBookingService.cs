using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BotGenerator.Core.Services;

/// <summary>
/// Implementation of IExternalBookingService that fetches booking data from the external API.
/// </summary>
public class ExternalBookingService : IExternalBookingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExternalBookingService> _logger;
    private readonly string? _apiBaseUrl;
    private readonly string? _apiKey;

    public ExternalBookingService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ExternalBookingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // Configuration for external booking API
        _apiBaseUrl = configuration["ExternalBooking:ApiUrl"];
        _apiKey = configuration["ExternalBooking:ApiKey"];
    }

    public async Task<ExternalBookingInfo?> GetBookingByPhoneAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Normalize phone number for the API
            var normalizedPhone = NormalizePhone(phoneNumber);
            
            _logger.LogInformation(
                "Fetching booking info for phone {Phone} from external API",
                normalizedPhone);

            // Build the request URL
            // The API endpoint should be configured in appsettings or .env
            if (string.IsNullOrWhiteSpace(_apiBaseUrl))
            {
                _logger.LogWarning("ExternalBooking:ApiUrl not configured, cannot fetch booking info");
                return null;
            }

            var requestUrl = $"{_apiBaseUrl.TrimEnd('/')}/api/bookings/by-phone?phone={normalizedPhone}";
            
            // Add API key if configured
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                requestUrl += $"&apiKey={_apiKey}";
            }

            var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation(
                    "No booking found for phone {Phone}",
                    normalizedPhone);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var bookingData = JsonSerializer.Deserialize<ExternalApiBookingResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (bookingData == null)
            {
                _logger.LogWarning("Empty response from external booking API");
                return null;
            }

            // Build the original confirmation message format
            var confirmationMessage = BuildConfirmationMessage(bookingData);

            var result = new ExternalBookingInfo
            {
                CustomerName = bookingData.CustomerName ?? "",
                Date = bookingData.Date ?? "",
                Time = bookingData.Time ?? "",
                People = bookingData.People,
                ArrozType = bookingData.ArrozType,
                ArrozServings = bookingData.ArrozServings,
                HighChairs = bookingData.HighChairs,
                BabyStrollers = bookingData.BabyStrollers,
                OriginalConfirmationMessage = confirmationMessage
            };

            _logger.LogInformation(
                "Successfully fetched booking for {Name} on {Date} at {Time}",
                result.CustomerName,
                result.Date,
                result.Time);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching booking for phone {Phone}", phoneNumber);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching booking for phone {Phone}", phoneNumber);
            return null;
        }
    }

    /// <summary>
    /// Fallback method to parse booking info from a confirmation message text.
    /// This can be used when the API is not available but we have the confirmation message.
    /// </summary>
    public ExternalBookingInfo? ParseBookingFromConfirmationMessage(string messageText)
    {
        try
        {
            _logger.LogDebug("Attempting to parse booking from confirmation message");

            // Check if this looks like a confirmation message
            if (!messageText.Contains("Confirmación de Reserva") && 
                !messageText.Contains("Alquería Villa Carmen"))
            {
                return null;
            }

            var info = new ExternalBookingInfo
            {
                OriginalConfirmationMessage = messageText
            };

            // Extract name (pattern: "Hola [Name],")
            var nameMatch = Regex.Match(messageText, @"Hola\s+([^,]+),");
            if (nameMatch.Success)
            {
                info = info with { CustomerName = nameMatch.Groups[1].Value.Trim() };
            }

            // Extract date (pattern: "📅 Fecha: 14/02/2026")
            var dateMatch = Regex.Match(messageText, @"Fecha:\s*(\d{2}/\d{2}/\d{4})");
            if (dateMatch.Success)
            {
                info = info with { Date = dateMatch.Groups[1].Value };
            }

            // Extract time (pattern: "🕒 Hora: 15:00")
            var timeMatch = Regex.Match(messageText, @"Hora:\s*(\d{2}:\d{2})");
            if (timeMatch.Success)
            {
                info = info with { Time = timeMatch.Groups[1].Value };
            }

            // Extract people (pattern: "👥 Personas: 2")
            var peopleMatch = Regex.Match(messageText, @"Personas:\s*(\d+)");
            if (peopleMatch.Success && int.TryParse(peopleMatch.Groups[1].Value, out var people))
            {
                info = info with { People = people };
            }

            // Extract rice (pattern: "🍚 Arroz: Arroz meloso de pulpo y gambones (+5€) x 2")
            var riceMatch = Regex.Match(messageText, @"Arroz:\s*(.+?)(?:\s*\(|\s*x\s*\d|\s*$)");
            if (riceMatch.Success)
            {
                var riceText = riceMatch.Groups[1].Value.Trim();
                if (!riceText.Equals("No", StringComparison.OrdinalIgnoreCase) &&
                    !riceText.Equals("Ninguno", StringComparison.OrdinalIgnoreCase))
                {
                    info = info with { ArrozType = riceText };
                }
            }

            // Extract rice servings (pattern: "x 2" at end of rice line)
            var servingsMatch = Regex.Match(messageText, @"Arroz:.+?x\s*(\d+)");
            if (servingsMatch.Success && int.TryParse(servingsMatch.Groups[1].Value, out var servings))
            {
                info = info with { ArrozServings = servings };
            }

            // Extract high chairs (pattern: "👶 Tronas: 0")
            var tronasMatch = Regex.Match(messageText, @"Tronas:\s*(\d+)");
            if (tronasMatch.Success && int.TryParse(tronasMatch.Groups[1].Value, out var tronas))
            {
                info = info with { HighChairs = tronas };
            }

            // Extract baby strollers (pattern: "🍼 Carros de bebé: 0")
            var carritosMatch = Regex.Match(messageText, @"Carros de beb[ée]:\s*(\d+)");
            if (carritosMatch.Success && int.TryParse(carritosMatch.Groups[1].Value, out var carritos))
            {
                info = info with { BabyStrollers = carritos };
            }

            // Validate that we have minimum required fields
            if (string.IsNullOrWhiteSpace(info.Date) || string.IsNullOrWhiteSpace(info.Time))
            {
                _logger.LogWarning("Could not extract required booking fields from message");
                return null;
            }

            _logger.LogInformation(
                "Successfully parsed booking for {Name} on {Date} at {Time}",
                info.CustomerName,
                info.Date,
                info.Time);

            return info;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing confirmation message");
            return null;
        }
    }

    private static string BuildConfirmationMessage(ExternalApiBookingResponse booking)
    {
        var arrozLine = string.IsNullOrWhiteSpace(booking.ArrozType)
            ? "🍚 *Arroz:* No"
            : $"🍚 *Arroz:* {booking.ArrozType}" + (booking.ArrozServings.HasValue ? $" x {booking.ArrozServings}" : "");

        return $"*Confirmación de Reserva - Alquería Villa Carmen*\n\n" +
               $"Hola {booking.CustomerName},\n\n" +
               $"Gracias por elegir Alquería Villa Carmen. Su reserva ha sido confirmada:\n\n" +
               $"📅 *Fecha:* {booking.Date}\n" +
               $"🕒 *Hora:* {booking.Time}\n" +
               $"👥 *Personas:* {booking.People}\n" +
               $"{arrozLine}\n" +
               $"👶 *Tronas:* {booking.HighChairs}\n" +
               $"🍼 *Carros de bebé:* {booking.BabyStrollers}\n\n" +
               $"Al hacer esta reserva, usted ha confirmado y aceptado las condiciones de reserva y políticas del restaurante, las cuales puede consultar en el botón de abajo.";
    }

    private static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return phone;

        // Keep digits only
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        // If phone has more than 9 digits, it likely includes country code
        // Store as-is for API lookup
        return digits;
    }
}

/// <summary>
/// Expected response format from external booking API.
/// </summary>
public class ExternalApiBookingResponse
{
    public string? CustomerName { get; set; }
    public string? Date { get; set; }
    public string? Time { get; set; }
    public int People { get; set; }
    public string? ArrozType { get; set; }
    public int? ArrozServings { get; set; }
    public int HighChairs { get; set; }
    public int BabyStrollers { get; set; }
}
