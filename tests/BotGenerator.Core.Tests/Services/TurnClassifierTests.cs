using BotGenerator.Core.Services.TurnAnalysis;
using FluentAssertions;

namespace BotGenerator.Core.Tests.Services;

public class TurnClassifierTests
{
    [Fact]
    public void IsRiceOfferMessage_DetectsCommonPattern()
    {
        TurnClassifier.IsRiceOfferMessage("¿Quieres que añadamos arroz?")
            .Should()
            .BeTrue();
    }

    [Fact]
    public void IsRiceDecisionDeferral_DetectsDeferral()
    {
        TurnClassifier.IsRiceDecisionDeferral("Déjeme que pregunte a mi marido y hoy mismo le confirmo, si??")
            .Should()
            .BeTrue();
    }

    [Fact]
    public void IsRiceOfferDecline_DetectsNo()
    {
        TurnClassifier.IsRiceOfferDecline("No, gracias, sin arroz")
            .Should()
            .BeTrue();
    }
}

