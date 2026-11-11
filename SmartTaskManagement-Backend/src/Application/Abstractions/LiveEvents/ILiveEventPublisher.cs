namespace Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;

/// <summary>Publishes live events to every instance. Never throws for a Redis outage (fail open, ADR 0018).</summary>
public interface ILiveEventPublisher
{
    Task PublishAsync(IReadOnlyList<LiveEvent> events, CancellationToken ct);
}
