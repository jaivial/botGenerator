using System.Globalization;
using System.Text.RegularExpressions;

namespace BotGenerator.Core.Services;

/// <summary>
/// Sanitizes outgoing WhatsApp messages:
/// - Decodes literal \uXXXX unicode escapes (including surrogate pairs like
///   \uD83C\uDF5A) that the AI sometimes emits as plain text.
/// - Collapses markdown-style **bold** into WhatsApp-style *bold*.
/// </summary>
public static class WhatsAppMessageSanitizer
{
    private static readonly Regex UnicodeEscapeRegex = new(
        @"\\u[Dd][89AaBb][0-9A-Fa-f]{2}\\u[Dd][CcDdEeFf][0-9A-Fa-f]{2}|\\u([0-9A-Fa-f]{4})",
        RegexOptions.Compiled);

    private static readonly Regex BoldAsterisksRegex = new(@"\*{2,}", RegexOptions.Compiled);

    public static string Sanitize(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        text = DecodeUnicodeEscapes(text);
        return NormalizeBold(text);
    }

    private static string DecodeUnicodeEscapes(string text)
    {
        try
        {
            return UnicodeEscapeRegex.Replace(text, match =>
            {
                if (match.Groups[1].Success)
                {
                    // Single \uXXXX escape
                    return ConvertUtf32(int.Parse(match.Value.Substring(2, 4), NumberStyles.HexNumber));
                }

                // Surrogate pair \uD83C\uDF5A -> supplementary code point
                var high = int.Parse(match.Value.Substring(2, 4), NumberStyles.HexNumber);
                var low = int.Parse(match.Value.Substring(8, 4), NumberStyles.HexNumber);
                var codePoint = 0x10000 + ((high - 0xD800) << 10) + (low - 0xDC00);
                return char.ConvertFromUtf32(codePoint);
            });
        }
        catch
        {
            return text;
        }
    }

    private static string ConvertUtf32(int codePoint)
    {
        // Lone surrogates cannot be converted to a valid char; keep the literal escape.
        if (codePoint is >= 0xD800 and <= 0xDFFF)
            return $"\\u{codePoint:X4}";
        return char.ConvertFromUtf32(codePoint);
    }

    private static string NormalizeBold(string text) =>
        BoldAsterisksRegex.Replace(text, "*");
}
