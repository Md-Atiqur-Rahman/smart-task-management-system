using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Email;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Infrastructure.Email;

/// <summary>
/// Runs on every instance: one <see cref="EmailOutboxProcessor"/> cycle (in its own scope and
/// DbContext) every Email:PollSeconds. The integration test host removes this worker.
/// </summary>
public sealed partial class EmailOutboxWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<EmailOptions> options,
    TimeProvider timeProvider,
    ILogger<EmailOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.Value.PollSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<EmailOutboxProcessor>().ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // Design 8: an unexpected exception is logged and the row is released (its claim expires); one bad row or a database outage must not stop the worker or the host.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogCycleFailed(ex);
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

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Email outbox cycle failed; claimed notifications are retried after their claim expires.")]
    private partial void LogCycleFailed(Exception exception);
}
