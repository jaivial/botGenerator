namespace BotGenerator.Core.Services;

/// <summary>
/// Service for sending WhatsApp messages.
/// </summary>
public interface IWhatsAppService
{
    /// <summary>
    /// Sends a text message.
    /// </summary>
    Task<bool> SendTextAsync(
        string phoneNumber,
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message with buttons.
    /// </summary>
    Task<bool> SendButtonsAsync(
        string phoneNumber,
        string text,
        string footer,
        List<ButtonOption> buttons,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message with a menu/list.
    /// </summary>
    Task<bool> SendMenuAsync(
        string phoneNumber,
        string text,
        string buttonText,
        List<MenuSection> sections,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets conversation history from WhatsApp.
    /// </summary>
    Task<List<WhatsAppHistoryMessage>> GetHistoryAsync(
        string phoneNumber,
        int limit = 20,
        CancellationToken cancellationToken = default);
}

public record ButtonOption(string Id, string Text, string? Description = null);

public record MenuSection(string Title, List<MenuRow> Rows);

public record MenuRow(string Id, string Title, string? Description = null);

public record WhatsAppHistoryMessage
{
    public string Text { get; init; } = "";
    public bool FromMe { get; init; }
    public long Timestamp { get; init; }
    public string? SenderName { get; init; }
    public string? MessageId { get; init; }
}
