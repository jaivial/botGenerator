using BotGenerator.Core.Models;
using BotGenerator.Core.Services;

namespace BotGenerator.Api;

/// <summary>
/// Claims due booking confirmations from MySQL. A claim lease makes interrupted
/// work eligible for a later retry after process restarts.
/// </summary>
public sealed class BookingConfirmationOutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BookingConfirmationOutboxOptions _options;
    private readonly ILogger<BookingConfirmationOutboxWorker> _logger;

    public BookingConfirmationOutboxWorker(
        IServiceScopeFactory scopeFactory,
        BookingConfirmationOutboxOptions options,
        ILogger<BookingConfirmationOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<BookingConfirmationOutboxProcessor>();
                for (var processed = 0; processed < _options.BatchSize; processed++)
                {
                    var result = await processor.ProcessNextDueAsync(stoppingToken);
                    if (result.Status == BookingConfirmationDeliveryStatus.NotDue)
                        break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Booking confirmation outbox worker iteration failed");
            }

            try
            {
                await Task.Delay(_options.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
