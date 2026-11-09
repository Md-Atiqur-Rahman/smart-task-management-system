using System.Collections.Concurrent;

using Datavanced.SmartTaskManagement.Application.Abstractions.Security;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;

/// <summary>Forward-only in-memory token version store for tests that do not need Redis.</summary>
public sealed class InMemoryTokenVersionStore : ITokenVersionStore
{
    private readonly ConcurrentDictionary<Guid, int> _versions = new();

    /// <summary>When true every call throws <see cref="SharedStateUnavailableException"/>.</summary>
    public bool SimulateOutage { get; set; }

    public Task<int?> GetAsync(Guid userId, CancellationToken ct)
    {
        ThrowIfOutage();
        return Task.FromResult<int?>(_versions.TryGetValue(userId, out var version) ? version : null);
    }

    public Task SetIfHigherAsync(Guid userId, int version, CancellationToken ct)
    {
        ThrowIfOutage();
        _versions.AddOrUpdate(userId, version, (_, current) => Math.Max(current, version));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid userId, CancellationToken ct)
    {
        ThrowIfOutage();
        _versions.TryRemove(userId, out _);
        return Task.CompletedTask;
    }

    private void ThrowIfOutage()
    {
        if (SimulateOutage)
        {
            throw new SharedStateUnavailableException();
        }
    }
}
