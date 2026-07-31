using System.Text.Json;
using BotGenerator.Core.Models;
using FluentAssertions;

namespace BotGenerator.Core.Tests.Models;

public class EvolutionMessageParserTests
{
    [Fact]
    public void TryParseInboundMessage_Conversation_ParsesSenderAndText()
    {
        using var document = JsonDocument.Parse("""
            {
              "key":{"remoteJid":"34638857294@s.whatsapp.net","id":"msg-1","fromMe":false},
              "pushName":"María",
              "messageTimestamp":1700000000,
              "message":{"conversation":"Hola"}
            }
            """);

        var parsed = EvolutionMessageParser.TryParseInboundMessage(document.RootElement, out var message);

        parsed.Should().BeTrue();
        message.SenderNumber.Should().Be("34638857294");
        message.MessageText.Should().Be("Hola");
        message.MessageType.Should().Be("conversation");
        message.IsButtonResponse.Should().BeFalse();
    }

    [Fact]
    public void TryParseInboundMessage_ButtonResponse_ParsesButtonIdAndDisplayText()
    {
        using var document = JsonDocument.Parse("""
            {
              "key":{"remoteJid":"34638857294@s.whatsapp.net","id":"msg-2"},
              "message":{"buttonsResponseMessage":{"selectedButtonId":"confirm","selectedDisplayText":"Confirmar"}}
            }
            """);

        var parsed = EvolutionMessageParser.TryParseInboundMessage(document.RootElement, out var message);

        parsed.Should().BeTrue();
        message.IsButtonResponse.Should().BeTrue();
        message.ButtonId.Should().Be("confirm");
        message.ButtonText.Should().Be("Confirmar");
    }

    [Fact]
    public void TryParseInboundMessage_ListResponse_ParsesInteractiveSelection()
    {
        using var listDocument = JsonDocument.Parse("""
            {
              "key":{"remoteJid":"34638857294@s.whatsapp.net","id":"msg-3"},
              "message":{"listResponseMessage":{"title":"Paella","singleSelectReply":{"selectedRowId":"rice-paella"}}}
            }
            """);
        EvolutionMessageParser.TryParseInboundMessage(listDocument.RootElement, out var listMessage).Should().BeTrue();

        listMessage.ButtonId.Should().Be("rice-paella");
        listMessage.ButtonText.Should().Be("Paella");
    }

    [Fact]
    public void TryParseInboundMessage_NativeFlowResponse_ParsesInteractiveSelection()
    {
        using var nativeFlowDocument = JsonDocument.Parse("""
            {
              "key":{"remoteJid":"34638857294@s.whatsapp.net","id":"msg-4"},
              "message":{"interactiveResponseMessage":{"nativeFlowResponseMessage":{"paramsJson":"{\"id\":\"cancel\",\"title\":\"Cancelar\"}"}}}
            }
            """);

        EvolutionMessageParser.TryParseInboundMessage(nativeFlowDocument.RootElement, out var nativeFlowMessage).Should().BeTrue();

        nativeFlowMessage.ButtonId.Should().Be("cancel");
        nativeFlowMessage.ButtonText.Should().Be("Cancelar");
    }

    [Fact]
    public void TryParseInboundMessage_LidSender_IsRejected()
    {
        using var document = JsonDocument.Parse("""
            {
              "key":{"remoteJid":"123456789@lid","id":"msg-lid"},
              "message":{"conversation":"Hola"}
            }
            """);

        EvolutionMessageParser.TryParseInboundMessage(document.RootElement, out _).Should().BeFalse();
    }
}
