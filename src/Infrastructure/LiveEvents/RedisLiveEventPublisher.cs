using System.Text.Json;

using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Application.Features.Notifications;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Datavanced.SmartTaskManagement.Infrastructure.LiveEvents;

/// <summary>
/// Publishes live events to the Redis channel, one message per event. Fails open (ADR 0018): a Redis
/// outage is logged as a warning and never thrown, because the command has already succeeded.
/// </summary>
public sealed partial class RedisLiveEventPublisher(
    IConnectionMultiplexer connection,
    IOptions<NotificationsOptions> options,
    ILogger<RedisLiveEventPublisher> logger) : ILiveEventPublisher
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync(IReadOnlyList<LiveEvent> events, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (events.Count == 0)
        {
            return;
        }

        try
        {
            var channel = RedisChannel.Literal(options.Value.RedisChannel);
            var subscriber = connection.GetSubscriber();
            foreach (var liveEvent in events)
            {
                var message = new LiveEventMessage(
                    liveEvent.UserIds, liveEvent.IncludeAdmins, liveEvent.Type, liveEvent.Notification);
                await subscriber.PublishAsync(channel, JsonSerializer.Serialize(message, Json));
            }
        }
        catch (RedisException ex)
        {
            LogPublishFailed(ex);
        }
        catch (RedisTimeoutException ex)
        {
            // Not a RedisException (derives from TimeoutException); same handling.
            LogPublishFailed(ex);
        }
        catch (RedisCommandException ex)
        {
            // Not a RedisException (derives from Exception); same handling.
            LogPublishFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Live events were not published; Redis is unavailable. Clients catch up on their next fetch.")]
    private partial void LogPublishFailed(Exception exception);
}
