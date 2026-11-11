using System.Text.Json;
using System.Threading.Channels;

using Datavanced.SmartTaskManagement.Api.Notifications;
using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Users;

using Microsoft.Extensions.Time.Testing;

using NSubstitute;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Notifications;

/// <summary>
/// A flood of queued events never postpones the end of the stream or the heartbeat token check:
/// the stream reads the clock between events (design 6).
/// </summary>
public sealed class NotificationStreamFloodTests
{
    private static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(25);

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 11, 9, 0, 0, TimeSpan.Zero));

    private readonly Channel<LiveEvent> _events = Channel.CreateUnbounded<LiveEvent>();

    private readonly ITokenVersionChecker _checker = Substitute.For<ITokenVersionChecker>();

    [Fact]
    public async Task Read_EventsQueuedAfterExpiry_EndsWithoutDraining()
    {
        var userId = Guid.NewGuid();
        await using var items = Open(userId, expiresAt: _time.GetUtcNow().AddMinutes(1));
        (await items.MoveNextAsync()).ShouldBeTrue();
        items.Current.EventType.ShouldBe(LiveEventTypes.Heartbeat);

        Flood(userId);
        _time.Advance(TimeSpan.FromMinutes(1));

        (await items.MoveNextAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Read_EventsQueuedAtHeartbeat_ChecksTokenBeforeDraining()
    {
        var userId = Guid.NewGuid();
        _checker.CheckAsync(userId, 1, Arg.Any<CancellationToken>()).Returns(TokenVersionCheck.Revoked);
        await using var items = Open(userId, expiresAt: _time.GetUtcNow().AddHours(1));
        (await items.MoveNextAsync()).ShouldBeTrue();

        // The stream sets its next heartbeat when it resumes after the first one, so resume it (it
        // then waits on the fake clock) before the clock reaches the heartbeat and the events arrive.
        var next = items.MoveNextAsync();
        _time.Advance(Heartbeat);
        Flood(userId);

        // The revoked token ends the stream before any queued event is written.
        (await next).ShouldBeFalse();
        await _checker.Received(1).CheckAsync(userId, 1, Arg.Any<CancellationToken>());
    }

    private IAsyncEnumerator<System.Net.ServerSentEvents.SseItem<string>> Open(Guid userId, DateTimeOffset expiresAt)
    {
        var connection = Substitute.For<ILiveConnection>();
        connection.Events.Returns(_events.Reader);
        var connections = Substitute.For<ILiveConnections>();
        connections.Open(userId, Role.TeamMember).Returns(connection);
        var stream = new NotificationStream(
            connections, _checker, _time, Heartbeat, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return stream.ReadAsync(userId, Role.TeamMember, 1, expiresAt, _ct).GetAsyncEnumerator(_ct);
    }

    private void Flood(Guid userId)
    {
        for (var i = 0; i < 100; i++)
        {
            _events.Writer.TryWrite(new LiveEvent(LiveEventTypes.DashboardChanged, [userId], false, null)).ShouldBeTrue();
        }
    }
}
