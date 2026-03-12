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

    [Fact]
    public void IsSameDayBookingRequest_ParaHoy_IsTrue()
    {
        // Bug scenario: User responds "Para hoy" when asked for date
        SameDayDetector.IsSameDayBookingRequest("Para hoy")
            .Should()
            .BeTrue();
    }

    [Fact]
    public void IsSameDayBookingRequest_ElDiaDeHoy_IsTrue()
    {
        SameDayDetector.IsSameDayBookingRequest("Para el día de hoy")
            .Should()
            .BeTrue();
    }

    [Fact]
    public void IsSameDayBookingRequest_EstaTarde_IsTrue()
    {
        SameDayDetector.IsSameDayBookingRequest("Quiero comer esta tarde")
            .Should()
            .BeTrue();
    }

    [Fact]
    public void IsSameDayBookingRequest_EstaNoche_IsTrue()
    {
        SameDayDetector.IsSameDayBookingRequest("Cena esta noche")
            .Should()
            .BeTrue();
    }

    [Fact]
    public void IsSameDayBookingRequest_AhoraMismo_IsTrue()
    {
        SameDayDetector.IsSameDayBookingRequest("Quiero reservar ahora mismo")
            .Should()
            .BeTrue();
    }

    [Fact]
    public void IsSameDayBookingRequest_HoyALas_IsTrue()
    {
        SameDayDetector.IsSameDayBookingRequest("hoy a las 14:00")
            .Should()
            .BeTrue();
    }

    [Fact]
    public void IsSameDayBookingRequest_MyReservationToday_IsFalse()
    {
        // User asking about existing reservation, not booking new one
        SameDayDetector.IsSameDayBookingRequest("Mi reserva es hoy")
            .Should()
            .BeFalse();
    }

    [Fact]
    public void IsSameDayBookingRequest_HoyTeConfirmo_IsFalse()
    {
        // Deferral pattern - user will confirm later
        SameDayDetector.IsSameDayBookingRequest("Hoy te confirmo")
            .Should()
            .BeFalse();
    }

    [Fact]
    public void IsSameDayBookingRequest_HoyMismoTeDigo_IsFalse()
    {
        // Deferral pattern
        SameDayDetector.IsSameDayBookingRequest("Hoy mismo te digo algo")
            .Should()
            .BeFalse();
    }

    [Fact]
    public void IsSameDayBookingRequest_ForwardedConfirmation_IsFalse()
    {
        // Forwarded confirmation should not trigger
        var confirmation = "📅 fecha: 12/03/2026\n🕒 hora: 14:00\n👥 personas: 4";
        SameDayDetector.IsSameDayBookingRequest(confirmation)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void IsSameDayBookingRequest_TodayDateWithBookingIntent_IsTrue()
    {
        // Today's date in dd/MM format with booking intent
        var today = DateTime.Now;
        var todayDate = $"{today.Day}/{today.Month}";
        SameDayDetector.IsSameDayBookingRequest($"Quiero reservar para el {todayDate}")
            .Should()
            .BeTrue();
    }

    [Fact]
    public void IsSameDayBookingRequest_TodayDateWithoutIntent_IsFalse()
    {
        // Today's date mentioned without booking intent
        var today = DateTime.Now;
        var todayDate = $"{today.Day}/{today.Month}";
        SameDayDetector.IsSameDayBookingRequest($"Hoy es {todayDate}")
            .Should()
            .BeFalse();
    }

    [Fact]
    public void IsSameDayBookingRequest_EmptyText_IsFalse()
    {
        SameDayDetector.IsSameDayBookingRequest("")
            .Should()
            .BeFalse();
    }

    [Fact]
    public void IsSameDayBookingRequest_NullText_IsFalse()
    {
        SameDayDetector.IsSameDayBookingRequest(null!)
            .Should()
            .BeFalse();
    }
}

