using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;

using NSubstitute;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Notifications;

public sealed class LiveEventBufferTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static LiveEvent Event(string type) => new(type, [Guid.NewGuid()], false, null);

    [Fact]
    public async Task Flush_PublishesBufferedEventsOnceThenClears()
    {
        var publisher = Substitute.For<ILiveEventPublisher>();
        var buffer = new LiveEventBuffer(publisher);
        var first = Event(LiveEventTypes.Notification);
        var second = Event(LiveEventTypes.DashboardChanged);
        buffer.Add(first);
        buffer.Add(second);

        await buffer.FlushAsync(_ct);
        await buffer.FlushAsync(_ct);

        await publisher.Received(1).PublishAsync(
            Arg.Is<IReadOnlyList<LiveEvent>>(events => events.Count == 2 && events[0] == first && events[1] == second),
            _ct);
    }

    [Fact]
    public async Task Flush_Empty_DoesNotPublish()
    {
        var publisher = Substitute.For<ILiveEventPublisher>();
        var buffer = new LiveEventBuffer(publisher);

        await buffer.FlushAsync(_ct);

        await publisher.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<IReadOnlyList<LiveEvent>>(), Arg.Any<CancellationToken>());
    }
}
