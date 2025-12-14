using System.Reflection;
using BotGenerator.Core.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace BotGenerator.Core.Tests.Conversations;

public class ScenarioHumanLikeQualityAnalysisTests : ConversationFlowTestBase
{
    protected override string GetPushName() => "Jaime Villanueva";

    [Fact(Skip = "Non-deterministic with mocked Gemini + booking rules changed (rice min 2, tronas/carritos required). Use Python black-box tests instead.")]
    public async Task Scenario_BookingNextSaturday_PaellaValenciana_OneServing_1500_JaimeVillanueva()
    {
        // Force the scenario phone number (sender id) to match the requested default number.
        SetPrivateField(Simulator, "_userId", "692747052");

        // Helper to print response + metrics for analysis.
        void Dump(string label)
        {
            var r = Simulator.LastResponse;
            Console.WriteLine($"\n--- {label} ---\n{r}\n");
            Console.WriteLine($"[metrics] length={r.Length}, questions={r.Count(c => c == '?')}, emojis={CountEmojis(r)}\n");
        }

        // Conversation (human-like step-by-step flow)
        await Simulator.UserSays("Hola");
        Dump("bot-1 (greeting)");
        Simulator.ShouldRespondNaturally();
        Simulator.ResponseLengthShouldBe(200);

        await Simulator.UserSays("Quiero reservar una mesa");
        Dump("bot-2 (booking start)");
        Simulator.ShouldHaveMaxQuestions(1);
        Simulator.ShouldNotMention("1.", "2.", "3.", "•", "a)", "b)");
        Simulator.ResponseLengthShouldBe(200);

        await Simulator.UserSays("Para el sábado");
        Dump("bot-3 (date provided)");
        Simulator.ShouldHaveMaxQuestions(1);
        Simulator.ShouldNotMention("y a qué hora", "cuántas personas y", "1.", "2.", "•");
        Simulator.ResponseLengthShouldBe(150);

        await Simulator.UserSays("Para 2 personas");
        Dump("bot-4 (people provided)");
        Simulator.ShouldHaveMaxQuestions(1);
        Simulator.ShouldRespond("hora");
        Simulator.ResponseLengthShouldBe(150);

        await Simulator.UserSays("A las 15:00");
        Dump("bot-5 (time provided)");
        Simulator.ShouldHaveMaxQuestions(1);
        Simulator.ShouldRespond("arroz");
        Simulator.ResponseLengthShouldBe(150);

        await Simulator.UserSays("Paella valenciana");
        Dump("bot-6 (rice selected)");
        Simulator.ShouldHaveMaxQuestions(1);
        Simulator.ShouldRespond("raciones");
        Simulator.ResponseLengthShouldBe(150);

        await Simulator.UserSays("1 ración");
        Dump("bot-7 (servings provided -> summary)");
        Simulator.ShouldRespond("sábado", "15:00", "2", "paella", "confirmo");
        Simulator.ResponseLengthShouldBe(350);

        await Simulator.UserSays("Sí, confirmo");
        Dump("bot-8 (final confirmation)");

        // Confirmation should be readable and not crazy long
        Simulator.LastResponse.Length.Should().BeLessThan(700);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"Expected to find private field '{fieldName}' on {target.GetType().Name}");
        field!.SetValue(target, value);
    }

    private static int CountEmojis(string text)
    {
        var count = 0;
        foreach (var c in text)
        {
            if (c >= 0x1F300 && c <= 0x1F9FF) count++;
            else if (c >= 0x2600 && c <= 0x26FF) count++;
            else if (c >= 0x2700 && c <= 0x27BF) count++;
            else if ("✅❌📅🕐👥👤🍚🪑".Contains(c)) count++;
        }

        return count;
    }
}
