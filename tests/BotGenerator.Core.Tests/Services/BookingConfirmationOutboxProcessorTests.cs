using System.Text.Json;
using BotGenerator.Core.Models;
using BotGenerator.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BotGenerator.Core.Tests.Services;

public class BookingConfirmationOutboxProcessorTests
{
    [Fact]
    public async Task TryDeliverAsync_TextAcceptance_MarksAcceptedAndDoesNotRetryWhenButtonsFail()
    {
        var repository = new FakeOutboxRepository(CreateMessage());
        var whatsApp = new Mock<IWhatsAppService>();
        whatsApp.Setup(service => service.SendTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        whatsApp.Setup(service => service.SendLinkButtonsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<LinkButtonOption>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var processor = CreateProcessor(repository, whatsApp.Object);

        var firstResult = await processor.TryDeliverAsync(1);
        var secondResult = await processor.TryDeliverAsync(1);

        firstResult.Status.Should().Be(BookingConfirmationDeliveryStatus.Accepted);
        firstResult.ProviderAccepted.Should().BeTrue();
        secondResult.Status.Should().Be(BookingConfirmationDeliveryStatus.NotDue);
        repository.MarkAcceptedCalls.Should().Be(1);
        repository.MarkFailedCalls.Should().Be(0);
        whatsApp.Verify(service => service.SendTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryDeliverAsync_ProviderRejection_SchedulesExponentialRetry()
    {
        var repository = new FakeOutboxRepository(CreateMessage(attempts: 2));
        var whatsApp = new Mock<IWhatsAppService>();
        whatsApp.Setup(service => service.SendTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var options = new BookingConfirmationOutboxOptions
        {
            MaxAttempts = 5,
            InitialRetryDelaySeconds = 30,
            MaxRetryDelaySeconds = 300
        };
        var processor = CreateProcessor(repository, whatsApp.Object, options);
        var before = DateTime.UtcNow;

        var result = await processor.TryDeliverAsync(1);

        result.Status.Should().Be(BookingConfirmationDeliveryStatus.RetryScheduled);
        repository.MarkFailedCalls.Should().Be(1);
        repository.NextAttemptAtUtc.Should().BeCloseTo(before.AddSeconds(60), TimeSpan.FromSeconds(2));
        repository.LastError.Should().Contain("did not accept");
    }

    [Fact]
    public async Task TryDeliverAsync_FinalAttempt_RecordsTerminalFailureWithoutAnotherSchedule()
    {
        var repository = new FakeOutboxRepository(CreateMessage(attempts: 3));
        var whatsApp = new Mock<IWhatsAppService>();
        whatsApp.Setup(service => service.SendTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var processor = CreateProcessor(
            repository,
            whatsApp.Object,
            new BookingConfirmationOutboxOptions { MaxAttempts = 3 });

        var result = await processor.TryDeliverAsync(1);

        result.Status.Should().Be(BookingConfirmationDeliveryStatus.FailedPermanently);
        repository.NextAttemptAtUtc.Should().BeNull();
    }

    [Fact]
    public void PayloadFactory_PreservesSpanishTextPlainUrlsAndLinkEnhancement()
    {
        var draft = BookingConfirmationPayloadFactory.Create(
            42,
            "evolution",
            "638857294",
            "María",
            new DateTime(2026, 8, 1),
            "14:00",
            4,
            "Paella valenciana",
            4,
            1,
            0);

        draft.Payload.Text.Should().Contain("*Confirmación de Reserva - Alquería Villa Carmen*");
        draft.Payload.Text.Should().Contain("Condiciones de reserva: https://alqueriavillacarmen.com/booking_policies.php");
        draft.Payload.Text.Should().Contain("cancel_reservation.php?id=42");
        draft.Payload.LinkButtons.Should().ContainSingle(button => button.Text == "CONDICIONES");
        draft.Payload.LinkButtons.Should().ContainSingle(button => button.Url.EndsWith("id=42"));
    }

    private static BookingConfirmationOutboxProcessor CreateProcessor(
        FakeOutboxRepository repository,
        IWhatsAppService whatsApp,
        BookingConfirmationOutboxOptions? options = null) =>
        new(
            repository,
            whatsApp,
            options ?? new BookingConfirmationOutboxOptions(),
            NullLogger<BookingConfirmationOutboxProcessor>.Instance);

    private static BookingConfirmationOutboxMessage CreateMessage(int attempts = 1)
    {
        var payload = new BookingConfirmationPayload
        {
            Text = "Confirmación",
            LinkButtonsText = "Accesos rápidos",
            LinkButtons = [new BookingConfirmationLink("CONDICIONES", "https://example.test/policies")]
        };
        return new BookingConfirmationOutboxMessage
        {
            Id = 1,
            BookingId = 99,
            NotificationType = "booking_confirmation",
            Provider = "evolution",
            PhoneNumber = "638857294",
            PayloadJson = JsonSerializer.Serialize(payload),
            State = BookingConfirmationOutboxState.Processing,
            Attempts = attempts,
            ClaimToken = "claim-token"
        };
    }

    private sealed class FakeOutboxRepository : IBookingConfirmationOutboxRepository
    {
        private BookingConfirmationOutboxMessage? _claimable;

        public FakeOutboxRepository(BookingConfirmationOutboxMessage claimable)
        {
            _claimable = claimable;
        }

        public int MarkAcceptedCalls { get; private set; }
        public int MarkFailedCalls { get; private set; }
        public DateTime? NextAttemptAtUtc { get; private set; }
        public string? LastError { get; private set; }

        public Task<BookingConfirmationOutboxMessage> EnqueueAsync(BookingConfirmationOutboxDraft draft, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BookingConfirmationOutboxMessage?> ClaimByIdAsync(long outboxId, DateTime nowUtc, BookingConfirmationOutboxOptions options, CancellationToken cancellationToken = default) =>
            Task.FromResult(TakeClaim());

        public Task<BookingConfirmationOutboxMessage?> ClaimNextDueAsync(DateTime nowUtc, BookingConfirmationOutboxOptions options, CancellationToken cancellationToken = default) =>
            Task.FromResult(TakeClaim());

        public Task<bool> MarkAcceptedAsync(long outboxId, string claimToken, DateTime acceptedAtUtc, CancellationToken cancellationToken = default)
        {
            MarkAcceptedCalls++;
            return Task.FromResult(true);
        }

        public Task<bool> MarkFailedAsync(long outboxId, string claimToken, string error, DateTime? nextAttemptAtUtc, CancellationToken cancellationToken = default)
        {
            MarkFailedCalls++;
            LastError = error;
            NextAttemptAtUtc = nextAttemptAtUtc;
            return Task.FromResult(true);
        }

        private BookingConfirmationOutboxMessage? TakeClaim()
        {
            var claim = _claimable;
            _claimable = null;
            return claim;
        }
    }
}
