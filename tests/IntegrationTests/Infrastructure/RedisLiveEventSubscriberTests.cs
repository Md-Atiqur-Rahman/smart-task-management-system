using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Application.Features.Notifications;
using Datavanced.SmartTaskManagement.Infrastructure.LiveEvents;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using StackExchange.Redis;

using Role = Datavanced.SmartTaskManagement.Domain.Users.Role;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>
/// The subscription retry against a substitute <see cref="ISubscriber"/>. A real multiplexer cannot make
/// SubscribeAsync fail a chosen number of times and then succeed deterministically, so the substitute
/// models the worst case: the library keeps the handler of a failed SubscribeAsync (and would
/// resubscribe it after a reconnect). The handler must still be registered exactly once.
/// </summary>
public sealed class RedisLiveEventSubscriberTests
{
    private static readonly TimeSpan RetryStep = TimeSpan.FromSeconds(5);

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Start_SubscribeFailsTwiceThenSucceeds_HandlerRegisteredOnce()
    {
        var handlers = new List<Action<RedisChannel, RedisValue>>();
        var failuresLeft = 2;
        var subscriber = Substitute.For<ISubscriber>();
        subscriber
            .SubscribeAsync(Arg.Any<RedisChannel>(), Arg.Any<Action<RedisChannel, RedisValue>>(), Arg.Any<CommandFlags>())
            .Returns(call =>
            {
                // Worst case: the handler is kept locally even when SUBSCRIBE fails.
                handlers.Add(call.ArgAt<Action<RedisChannel, RedisValue>>(1));
                return failuresLeft-- > 0
                    ? Task.FromException(new RedisConnectionException(
                        ConnectionFailureType.UnableToConnect, CommandFlags.None, "down", null, CommandStatus.Unknown))
                    : Task.CompletedTask;
            });
        subscriber
            .UnsubscribeAsync(Arg.Any<RedisChannel>(), Arg.Any<Action<RedisChannel, RedisValue>?>(), Arg.Any<CommandFlags>())
            .Returns(call =>
            {
                var handler = call.ArgAt<Action<RedisChannel, RedisValue>?>(1);
                if (handler is not null)
                {
                    handlers.Remove(handler);
                }

                return Task.CompletedTask;
            });
        // Publishing calls every registered handler, as Redis would.
        subscriber
            .PublishAsync(Arg.Any<RedisChannel>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(call =>
            {
                foreach (var handler in handlers.ToList())
                {
                    handler(call.ArgAt<RedisChannel>(0), call.ArgAt<RedisValue>(1));
                }

                return Task.FromResult(1L);
            });
        var connection = Substitute.For<IConnectionMultiplexer>();
        connection.GetSubscriber(Arg.Any<object?>()).Returns(subscriber);

        var options = Options.Create(new NotificationsOptions { RedisChannel = "notifications-test" });
        var registry = new LiveConnectionRegistry();
        var time = new FakeTimeProvider();
        var sut = new RedisLiveEventSubscriber(
            connection, registry, options, time, NullLogger<RedisLiveEventSubscriber>.Instance);

        await sut.StartAsync(_ct);
        while (!sut.Subscribed.IsCompleted)
        {
            // Moves past the retry delay; an advance made before the delay starts is simply repeated.
            time.Advance(RetryStep);
            await Task.WhenAny(sut.Subscribed, Task.Delay(TimeSpan.FromMilliseconds(20), _ct));
        }

        await subscriber.Received(3).SubscribeAsync(
            Arg.Any<RedisChannel>(), Arg.Any<Action<RedisChannel, RedisValue>>(), Arg.Any<CommandFlags>());
        await subscriber.Received(2).UnsubscribeAsync(
            Arg.Any<RedisChannel>(), Arg.Any<Action<RedisChannel, RedisValue>?>(), Arg.Any<CommandFlags>());
        handlers.Count.ShouldBe(1);

        // One published event reaches the connection exactly once.
        var userId = Guid.NewGuid();
        using var tab = registry.Open(userId, Role.TeamMember);
        var publisher = new RedisLiveEventPublisher(connection, options, NullLogger<RedisLiveEventPublisher>.Instance);
        await publisher.PublishAsync([new LiveEvent(LiveEventTypes.DashboardChanged, [userId], false, null)], _ct);

        tab.Events.TryRead(out var received).ShouldBeTrue();
        received.ShouldNotBeNull().Type.ShouldBe(LiveEventTypes.DashboardChanged);
        tab.Events.TryRead(out _).ShouldBeFalse();

        await sut.StopAsync(CancellationToken.None);
        handlers.ShouldBeEmpty();
        sut.Dispose();
    }
}
