using Datavanced.SmartTaskManagement.Application.Features.Notifications;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Infrastructure.Notifications;

/// <summary>
/// Runs on every instance: one <see cref="NotificationCleanup"/> pass (own scope and DbContext) every
/// Notifications:CleanupIntervalMinutes. The integration test host removes this worker.
/// </summary>
public sealed partial class NotificationCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationsOptions> options,
    TimeProvider timeProvider,
    ILogger<NotificationCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(options.Value.CleanupIntervalMinutes);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var deleted = await scope.ServiceProvider.GetRequiredService<NotificationCleanup>()
                    .DeleteExpiredAsync(stoppingToken);
                LogCleaned(deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // A database outage must not stop the worker or the host; the next interval retries the delete.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogCleanupFailed(ex);
            }

            try
            {
                await Task.Delay(interval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Notification cleanup removed {Deleted} expired notifications.")]
    private partial void LogCleaned(int deleted);

    [LoggerMessage(Level = LogLevel.Error, Message = "Notification cleanup failed; it is retried at the next interval.")]
    private partial void LogCleanupFailed(Exception exception);
}
