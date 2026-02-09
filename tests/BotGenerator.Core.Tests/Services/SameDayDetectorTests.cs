using BotGenerator.Core.Services.TurnAnalysis;
using FluentAssertions;

namespace BotGenerator.Core.Tests.Services;

public class SameDayDetectorTests
{
    [Fact]
    public void IsSameDayBookingRequest_DefersConfirmation_IsFalse()
    {
        SameDayDetector.IsSameDayBookingRequest(
                "Déjeme que pregunte a mi marido y hoy mismo le confirmo, ¿si?")
            .Should()
            .BeFalse();
    }

    [Fact]
    public void IsSameDayBookingRequest_ExplicitBookingForToday_IsTrue()
    {
        SameDayDetector.IsSameDayBookingRequest("Quiero reservar hoy para 2 personas")
            .Should()
            .BeTrue();
    }

    [Fact]
    public void IsSameDayBookingRequest_TableTodayAtTime_IsTrue()
    {
        SameDayDetector.IsSameDayBookingRequest("Mesa hoy a las 15:00")
            .Should()
            .BeTrue();
    }

    [Fact]
    public void IsSameDayBookingRequest_JustHoy_IsTrue()
    {
        SameDayDetector.IsSameDayBookingRequest("hoy")
            .Should()
            .BeTrue();
    }
}

