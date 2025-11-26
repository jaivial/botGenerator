namespace BotGenerator.Core.Models;

/// <summary>
/// Result of rice type validation.
/// </summary>
public record RiceValidationResult
{
    /// <summary>
    /// Status of the validation: "valid", "not_found", "multiple".
    /// </summary>
    public string Status { get; init; } = "unknown";

    /// <summary>
    /// The validated rice name (cleaned, without prices).
    /// </summary>
    public string? RiceName { get; init; }

    /// <summary>
    /// The original user request.
    /// </summary>
    public string? OriginalRequest { get; init; }

    /// <summary>
    /// Multiple options if status is "multiple".
    /// </summary>
    public List<string>? Options { get; init; }

    /// <summary>
    /// Message to show to the user.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Whether the rice was successfully validated.
    /// </summary>
    public bool IsValid => Status == "valid";

    /// <summary>
    /// Creates a valid result.
    /// </summary>
    public static RiceValidationResult Valid(string riceName, string originalRequest) => new()
    {
        Status = "valid",
        RiceName = riceName,
        OriginalRequest = originalRequest,
        Message = $"✅ {riceName} disponible."
    };

    /// <summary>
    /// Creates a not found result.
    /// </summary>
    public static RiceValidationResult NotFound(string originalRequest) => new()
    {
        Status = "not_found",
        OriginalRequest = originalRequest,
        Message = "Lo siento, no tenemos ese tipo de arroz. " +
                 "¿Te gustaría ver nuestros arroces disponibles?"
    };

    /// <summary>
    /// Creates a multiple options result.
    /// </summary>
    public static RiceValidationResult Multiple(List<string> options, string originalRequest) => new()
    {
        Status = "multiple",
        Options = options,
        OriginalRequest = originalRequest,
        Message = $"Tenemos varias opciones: {string.Join(" y ", options)}. ¿Cuál prefieres?"
    };
}

/// <summary>
/// Represents an available rice type.
/// </summary>
public record RiceType
{
    public string Name { get; init; } = "";
    public decimal ExtraPrice { get; init; }
    public string? Description { get; init; }
    public bool IsAvailable { get; init; } = true;
}
