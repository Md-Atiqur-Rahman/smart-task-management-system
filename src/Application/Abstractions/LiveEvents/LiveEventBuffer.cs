namespace Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;

/// <summary>
/// Collects the live events of one request. A handler adds events while it works and flushes once
/// after a successful save, so a failed save never publishes anything.
/// </summary>
internal sealed class LiveEventBuffer(ILiveEventPublisher publisher)
{
    private readonly List<LiveEvent> _events = [];

    public void Add(LiveEvent liveEvent) => _events.Add(liveEvent);

    /// <summary>Publishes the buffered events in one call, then clears the buffer. Empty: publishes nothing.</summary>
    public async Task FlushAsync(CancellationToken ct)
    {
        if (_events.Count == 0)
        {
            return;
        }

        var batch = _events.ToArray();
        _events.Clear();
        await publisher.PublishAsync(batch, ct);
    }
}
