using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Infrastructure.LiveEvents;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;

/// <summary>
/// Live event publisher for tests without Redis: delivers straight to the in-process registry and
/// records what was published.
/// </summary>
public sealed class InProcessLiveEventPublisher(LiveConnectionRegistry registry) : ILiveEventPublisher
{
    private readonly List<LiveEvent> _published = [];

    /// <summary>Every event published so far.</summary>
    public IReadOnlyList<LiveEvent> Published
    {
        get
        {
            lock (_published)
            {
                return _published.ToArray();
            }
        }
    }

    public Task PublishAsync(IReadOnlyList<LiveEvent> events, CancellationToken ct)
    {
        foreach (var liveEvent in events)
        {
            lock (_published)
            {
                _published.Add(liveEvent);
            }

            registry.Deliver(liveEvent);
        }

        return Task.CompletedTask;
    }
}
