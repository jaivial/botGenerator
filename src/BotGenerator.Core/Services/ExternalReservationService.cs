using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BotGenerator.Core.Services;

/// <summary>
/// Implementation of IExternalReservationService that calls external PHP endpoints.
/// </summary>
public class ExternalReservationService : IExternalReservationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExternalReservationService> _logger;
    private readonly string? _apiBaseUrl;
    private readonly string? _apiKey;

    public ExternalReservationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ExternalReservationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // Configuration for external booking API
        _apiBaseUrl = configuration["ExternalBooking:ApiUrl"];
        _apiKey = configuration["ExternalBooking:ApiKey"];
    }

    public async Task<bool> UpdateReservationFieldAsync(
        int bookingId,
        string field,
        string value,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_apiBaseUrl))
            {
                _logger.LogWarning("ExternalBooking:ApiUrl not configured, skipping external update");
                return false;
            }

            var updateUrl = $"{_apiBaseUrl.TrimEnd('/')}/api/update_reservation.php";
            
            _logger.LogInformation(
                "Calling external update API: {Url} for booking {BookingId}, field {Field}",
                updateUrl, bookingId, field);

            // Map C# field names to PHP field names
            var phpField = field.ToLowerInvariant() switch
            {
                "reservationdate" => "reservation_date",
                "reservationtime" => "reservation_time", 
                "partysize" => "party_size",
                "arroztype" => "rice_type",
                "arrozservings" => "rice_servings",
                "highchairs" => "high_chairs",
                "babystrollers" => "baby_strollers",
                _ => field.ToLowerInvariant()
            };

            var payload = new Dictionary<string, object>
            {
                ["bookingId"] = bookingId,
                ["field"] = phpField,
                ["value"] = value
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(updateUrl, content, cancellationToken);
            
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Successfully updated booking {BookingId} in external system: field={Field}, value={Value}",
                    bookingId, field, value);
                return true;
            }
            
            _logger.LogWarning(
                "External update failed for booking {BookingId}: {Status} - {Body}",
                bookingId, response.StatusCode, responseBody);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error updating booking {BookingId} in external system", bookingId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating booking {BookingId} in external system", bookingId);
            return false;
        }
    }

    public async Task<(bool success, string? message)> CreateReservationAsync(
        string customerName,
        string phone,
        string date,
        int partySize,
        string time,
        string? arrozType = null,
        int? arrozServings = null,
        int highChairs = 0,
        int babyStrollers = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_apiBaseUrl))
            {
                _logger.LogWarning("ExternalBooking:ApiUrl not configured, skipping external insert");
                return (false, "External API not configured");
            }

            var insertUrl = $"{_apiBaseUrl.TrimEnd('/')}/insert_booking.php";
            
            _logger.LogInformation(
                "Calling external insert API: {Url} for {Name}, {Date} {Time}, {People} people",
                insertUrl, customerName, date, time, partySize);

            // Build form data (PHP expects form POST, not JSON)
            var formData = new Dictionary<string, string>
            {
                ["date"] = date,
                ["party_size"] = partySize.ToString(),
                ["time"] = time,
                ["nombre"] = customerName,
                ["phone"] = phone,
                ["high_chairs"] = highChairs.ToString(),
                ["baby_strollers"] = babyStrollers.ToString(),
                ["toggleArroz"] = string.IsNullOrEmpty(arrozType) ? "false" : "true"
            };

            if (!string.IsNullOrEmpty(arrozType))
            {
                formData["arroz_type"] = arrozType;
                if (arrozServings.HasValue)
                {
                    formData["arroz_servings"] = arrozServings.Value.ToString();
                }
            }

            var content = new FormUrlEncodedContent(formData);

            var response = await _httpClient.PostAsync(insertUrl, content, cancellationToken);
            
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
                    if (jsonResponse.TryGetProperty("success", out var successProp) && successProp.GetBoolean())
                    {
                        _logger.LogInformation(
                            "Successfully created booking in external system for {Name}",
                            customerName);
                        return (true, null);
                    }
                    
                    var message = jsonResponse.TryGetProperty("message", out var msgProp) 
                        ? msgProp.GetString() 
                        : "Unknown error";
                    
                    _logger.LogWarning(
                        "External insert returned failure for {Name}: {Message}",
                        customerName, message);
                    return (false, message);
                }
                catch
                {
                    // Response wasn't JSON, but status was success
                    return (true, null);
                }
            }
            
            _logger.LogWarning(
                "External insert failed for {Name}: {Status} - {Body}",
                customerName, response.StatusCode, responseBody);
            return (false, $"HTTP {response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error creating booking in external system");
            return (false, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating booking in external system");
            return (false, ex.Message);
        }
    }

    public async Task<bool> CancelReservationAsync(
        int bookingId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_apiBaseUrl))
            {
                _logger.LogWarning("ExternalBooking:ApiUrl not configured, skipping external cancel");
                return false;
            }

            var cancelUrl = $"{_apiBaseUrl.TrimEnd('/')}/cancel_reservation.php";
            
            _logger.LogInformation(
                "Calling external cancel API: {Url} for booking {BookingId}",
                cancelUrl, bookingId);

            var payload = new Dictionary<string, object>
            {
                ["bookingId"] = bookingId
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(cancelUrl, content, cancellationToken);
            
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Successfully cancelled booking {BookingId} in external system",
                    bookingId);
                return true;
            }
            
            _logger.LogWarning(
                "External cancel failed for booking {BookingId}: {Status} - {Body}",
                bookingId, response.StatusCode, responseBody);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error cancelling booking {BookingId} in external system", bookingId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling booking {BookingId} in external system", bookingId);
            return false;
        }
    }
}
