using System.Text.Json;

using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Application.Features.Notifications;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Datavanced.SmartTaskManagement.Infrastructure.LiveEvents;

/// <summary>
/// Subscribes once to the Redis channel and writes every message to the matching local connections.
/// Registered only when real Redis is used. If Redis is unreachable at start the subscription is retried
/// in the background; the client library resubscribes by itself after a later reconnect.
/// </summary>
public sealed partial class RedisLiveEventSubscriber(
    IConnectionMultiplexer connection,
    LiveConnectionRegistry registry,
    IOptions<NotificationsOptions> options,
    ILogger<RedisLiveEventSubscriber> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly TaskCompletionSource _subscribed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when the subscription is active (tests use it to avoid racing the first publish).</summary>
    public Task Subscribed => _subscribed.Task;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = RedisChannel.Literal(options.Value.RedisChannel);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await connection.GetSubscriber().SubscribeAsync(channel, OnMessage);
                _subscribed.TrySetResult();
                return;
            }
            catch (Exception ex) when (ex is RedisException or RedisTimeoutException or RedisCommandException)
            {
                LogSubscribeFailed(ex);
            }

            try
            {
                await Task.Delay(RetryDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_subscribed.Task.IsCompletedSuccessfully)
        {
            try
            {
                await connection.GetSubscriber().UnsubscribeAsync(
                    RedisChannel.Literal(options.Value.RedisChannel), OnMessage);
            }
            catch (Exception ex) when (ex is RedisException or RedisTimeoutException or RedisCommandException)
            {
                LogSubscribeFailed(ex);
            }
        }
    }

    private void OnMessage(RedisChannel channel, RedisValue value)
    {
        LiveEventMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<LiveEventMessage>(value.ToString(), RedisLiveEventPublisher.Json);
        }
        catch (JsonException ex)
        {
            LogInvalidMessage(ex);
            return;
        }

        if (message is null)
        {
            return;
        }

        registry.Deliver(new LiveEvent(message.EventType, message.UserIds, message.IncludeAdmins, message.Data));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Live event subscription failed; Redis is unavailable.")]
    private partial void LogSubscribeFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A live event message could not be read and was ignored.")]
    private partial void LogInvalidMessage(Exception exception);
}
