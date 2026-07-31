using BotGenerator.Core.Services;
using FluentAssertions;

namespace BotGenerator.Core.Tests.Services;

public class WhatsAppMessageSanitizerTests
{
    [Theory]
    [InlineData("\\u00BF", "¿")]
    [InlineData("\\u00E9", "é")]
    [InlineData("caf\\u00E9", "café")]
    public void Sanitize_DecodesBmpUnicodeEscapes(string input, string expected)
    {
        WhatsAppMessageSanitizer.Sanitize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("\\uD83C\\uDF5A", "🍚")]
    [InlineData("\\uD83D\\uDE0A", "😊")]
    [InlineData("\\uD83D\\uDC4D", "👍")]
    [InlineData("\\uD83D\\uDCC5", "📅")]
    public void Sanitize_DecodesSurrogatePairEmoji(string input, string expected)
    {
        WhatsAppMessageSanitizer.Sanitize(input).Should().Be(expected);
    }

    [Fact]
    public void Sanitize_DecodesEscapesMixedWithText()
    {
        var input = "¡Claro! \\uD83D\\uDE0A Veo que tienes *Arroz de señoret (2 raciones)*. \\uD83C\\uDF5A";

        var result = WhatsAppMessageSanitizer.Sanitize(input);

        result.Should().Be("¡Claro! 😊 Veo que tienes *Arroz de señoret (2 raciones)*. 🍚");
    }

    [Theory]
    [InlineData("**IMPORTANTE**", "*IMPORTANTE*")]
    [InlineData("Hola **Jaime**, tu **reserva** está lista", "Hola *Jaime*, tu *reserva* está lista")]
    [InlineData("***negrita***", "*negrita*")]
    public void Sanitize_NormalizesMarkdownBoldToWhatsAppBold(string input, string expected)
    {
        WhatsAppMessageSanitizer.Sanitize(input).Should().Be(expected);
    }

    [Fact]
    public void Sanitize_KeepsSingleAsteriskBold()
    {
        WhatsAppMessageSanitizer.Sanitize("*Tu reserva actual:* 🍚").Should().Be("*Tu reserva actual:* 🍚");
    }

    [Fact]
    public void Sanitize_CombinesBoldAndEmojiFixes()
    {
        var input = "¡Claro! \\uD83D\\uDE0A **Reserva** del sábado 05/09/2026. \\uD83C\\uDF5A";

        var result = WhatsAppMessageSanitizer.Sanitize(input);

        result.Should().Be("¡Claro! 😊 *Reserva* del sábado 05/09/2026. 🍚");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("texto normal")]
    public void Sanitize_PassthroughUnchanged(string? input)
    {
        WhatsAppMessageSanitizer.Sanitize(input).Should().Be(input ?? string.Empty);
    }
}
