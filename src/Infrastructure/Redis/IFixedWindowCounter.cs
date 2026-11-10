using Datavanced.SmartTaskManagement.Application.Abstractions.Security;

namespace Datavanced.SmartTaskManagement.Infrastructure.Redis;

/// <summary>
/// Shared fixed-window counter for rate limiting (ADR 0013). The caller puts the window
/// start into the key, so each window has its own counter.
/// </summary>
public interface IFixedWindowCounter
{
    /// <summary>
    /// Atomically increments the counter of <paramref name="key"/> and returns the new count.
    /// The first increment sets the expiry to <paramref name="window"/>.
    /// </summary>
    /// <exception cref="SharedStateUnavailableException">The shared store is unreachable or timed out.</exception>
    Task<long> IncrementAsync(string key, TimeSpan window, CancellationToken ct);
}
