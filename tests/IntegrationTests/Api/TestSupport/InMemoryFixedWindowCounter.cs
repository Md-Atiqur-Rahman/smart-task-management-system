using System.Collections.Concurrent;

using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Infrastructure.Redis;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;

/// <summary>In-memory fixed-window counter for tests that do not need Redis (keys never expire).</summary>
public sealed class InMemoryFixedWindowCounter : IFixedWindowCounter
{
    private readonly ConcurrentDictionary<string, long> _counts = new(StringComparer.Ordinal);

    /// <summary>When true every call throws <see cref="SharedStateUnavailableException"/>.</summary>
    public bool SimulateOutage { get; set; }

    /// <summary>The keys incremented so far.</summary>
    public IReadOnlyCollection<string> Keys => _counts.Keys.ToArray();

    public Task<long> IncrementAsync(string key, TimeSpan window, CancellationToken ct)
    {
        if (SimulateOutage)
        {
            throw new SharedStateUnavailableException();
        }

        return Task.FromResult(_counts.AddOrUpdate(key, 1, (_, current) => current + 1));
    }
}
