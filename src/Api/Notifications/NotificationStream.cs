using System.Globalization;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Channels;

using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Api.Notifications;

/// <summary>
/// The live stream of one caller (design 6, ADR 0018). Opens one live connection, writes each live
/// event as an SSE item and a <see cref="LiveEventTypes.Heartbeat"/> when the stream opens and every
/// heartbeat interval. At every heartbeat the token version is checked again: Revoked or Unavailable
/// ends the stream (fail closed). The stream also ends when the access token expires, when the
/// connection is closed and when the client disconnects. The connection is disposed in every case.
/// The payload is serialized here with the API's JSON convention (the MVC options: camelCase, enums
/// as names) on purpose: ServerSentEventsResult would use the minimal-API JSON options, which lack
/// the enum converter.
/// </summary>
internal sealed class NotificationStream(
    ILiveConnections connections,
    ITokenVersionChecker checker,
    TimeProvider timeProvider,
    TimeSpan heartbeat,
    JsonSerializerOptions json)
{
    private static readonly SseItem<string> HeartbeatItem = new(string.Empty, LiveEventTypes.Heartbeat);

    private enum WaitOutcome
    {
        EventsReady,
        TimerElapsed,
        Closed,
        Cancelled,
    }

    /// <summary>Reads the token version and the expiry from the validated principal; false when either is missing.</summary>
    public static bool TryReadToken(ClaimsPrincipal user, out int tokenVersion, out DateTimeOffset expiresAt)
    {
        expiresAt = default;
        if (!int.TryParse(
                user.FindFirst(SecurityClaimTypes.TokenVersion)?.Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out tokenVersion)
            || !long.TryParse(
                user.FindFirst(SecurityClaimTypes.Expires)?.Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var expiresSeconds))
        {
            return false;
        }

        expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresSeconds);
        return true;
    }

    public async IAsyncEnumerable<SseItem<string>> ReadAsync(
        Guid userId,
        Role role,
        int tokenVersion,
        DateTimeOffset expiresAt,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var connection = connections.Open(userId, role);

        // The connection is registered: the client knows from this first item that events will arrive.
        yield return HeartbeatItem;
        var nextHeartbeat = timeProvider.GetUtcNow() + heartbeat;

        while (true)
        {
            while (connection.Events.TryRead(out var liveEvent))
            {
                yield return ToItem(liveEvent);
            }

            var now = timeProvider.GetUtcNow();
            if (now >= expiresAt)
            {
                yield break;
            }

            if (now >= nextHeartbeat)
            {
                if (!await IsTokenCurrentAsync(userId, tokenVersion, ct))
                {
                    yield break;
                }

                yield return HeartbeatItem;
                nextHeartbeat = timeProvider.GetUtcNow() + heartbeat;
                continue;
            }

            var until = nextHeartbeat < expiresAt ? nextHeartbeat : expiresAt;
            var outcome = await WaitAsync(connection.Events, until - now, ct);
            if (outcome is WaitOutcome.Closed or WaitOutcome.Cancelled)
            {
                yield break;
            }
        }
    }

    private SseItem<string> ToItem(LiveEvent liveEvent) =>
        new(
            liveEvent.Notification is null ? string.Empty : JsonSerializer.Serialize(liveEvent.Notification, json),
            liveEvent.Type);

    private async Task<bool> IsTokenCurrentAsync(Guid userId, int tokenVersion, CancellationToken ct)
    {
        try
        {
            return await checker.CheckAsync(userId, tokenVersion, ct) == TokenVersionCheck.Valid;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>Waits for the first of: an event, the timer, the connection closing, the client leaving.</summary>
    private async Task<WaitOutcome> WaitAsync(ChannelReader<LiveEvent> events, TimeSpan delay, CancellationToken ct)
    {
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            var read = events.WaitToReadAsync(wait.Token).AsTask();
            var timer = Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.Zero, timeProvider, wait.Token);
            var first = await Task.WhenAny(read, timer);
            if (first == read)
            {
                return await read ? WaitOutcome.EventsReady : WaitOutcome.Closed;
            }

            await timer;
            return WaitOutcome.TimerElapsed;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return WaitOutcome.Cancelled;
        }
        finally
        {
            // Stops the losing wait (timer or channel read) so nothing outlives this call.
            await wait.CancelAsync();
        }
    }
}
