using System.Globalization;
using System.Threading.RateLimiting;

using Datavanced.SmartTaskManagement.Api.Options;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Infrastructure.Redis;

using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Api.RateLimiting;

/// <summary>
/// Global limiter of the framework's rate limiting middleware (ADR 0013): a fixed window per
/// client IP, counted in the shared store so all instances share one limit. Actions marked
/// with <see cref="AuthRateLimitAttribute"/> must pass the stricter auth limit as well.
/// Key: <c>ratelimit:&lt;policy&gt;:&lt;client-ip&gt;:&lt;window-start-unix-seconds&gt;</c>.
/// When the store is unavailable the request is checked against per-instance in-memory
/// fixed-window limiters instead (fail open) for the rest of the current window; a warning is
/// logged at most once per window, and the next window tries the shared store again.
/// </summary>
internal sealed partial class SharedFixedWindowRateLimiter : PartitionedRateLimiter<HttpContext>
{
    public const string GlobalPolicy = "global";
    public const string AuthPolicy = "auth";

    private const string KeyPrefix = "ratelimit:";
    private const string UnknownClient = "unknown";

    private readonly IFixedWindowCounter _counter;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SharedFixedWindowRateLimiter> _logger;
    private readonly RateLimitingOptions _options;
    private readonly PartitionedRateLimiter<HttpContext> _fallbackGlobal;
    private readonly PartitionedRateLimiter<HttpContext> _fallbackAuth;

    // Global window start (unix seconds) in which the shared store last failed; per instance, not
    // static. While it equals the current window, Redis is skipped and no further warning is logged.
    private long _failedWindowStart = long.MinValue;

    public SharedFixedWindowRateLimiter(
        IFixedWindowCounter counter,
        TimeProvider timeProvider,
        ILogger<SharedFixedWindowRateLimiter> logger,
        IOptions<RateLimitingOptions> options)
    {
        _counter = counter;
        _timeProvider = timeProvider;
        _logger = logger;
        _options = options.Value;
        _fallbackGlobal = CreateInMemoryLimiter(_options.PermitLimit, _options.WindowSeconds);
        _fallbackAuth = CreateInMemoryLimiter(_options.AuthPermitLimit, _options.AuthWindowSeconds);
    }

    public override RateLimiterStatistics? GetStatistics(HttpContext resource) => null;

    /// <summary>
    /// Never decides synchronously, so the request thread never blocks on the shared store:
    /// the lease is not acquired and consumes nothing. The middleware then calls
    /// <see cref="PartitionedRateLimiter{TResource}.AcquireAsync"/>, which does the real check
    /// (ASP.NET Core RateLimitingMiddleware.TryAcquireAsync: AttemptAcquire first, then
    /// AcquireAsync when that lease is not acquired).
    /// </summary>
    protected override RateLimitLease AttemptAcquireCore(HttpContext resource, int permitCount) =>
        Lease.Rejected;

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(
        HttpContext resource, int permitCount, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(permitCount, 1);
        var client = ClientKey(resource);
        var auth = resource.GetEndpoint()?.Metadata.GetMetadata<AuthRateLimitAttribute>() is not null;

        // After a failure, skip Redis until the next window: an outage then costs one failed call
        // (up to the operation timeout) per window instead of one per request.
        if (Volatile.Read(ref _failedWindowStart) == WindowStart(_options.WindowSeconds))
        {
            return AcquireFromFallback(resource, auth);
        }

        try
        {
            // A request rejected by the auth limit has already consumed the global counter
            // (accepted over-count of a fixed window).
            var allowed = await IsWithinLimitAsync(
                    GlobalPolicy, client, _options.PermitLimit, _options.WindowSeconds, cancellationToken)
                && (!auth || await IsWithinLimitAsync(
                    AuthPolicy, client, _options.AuthPermitLimit, _options.AuthWindowSeconds, cancellationToken));
            return allowed ? Lease.Acquired : Lease.Rejected;
        }
        catch (SharedStateUnavailableException ex)
        {
            MarkWindowFailed(ex);
            return AcquireFromFallback(resource, auth);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fallbackGlobal.Dispose();
            _fallbackAuth.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await _fallbackGlobal.DisposeAsync();
        await _fallbackAuth.DisposeAsync();
        await base.DisposeAsyncCore();
    }

    private static string ClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? UnknownClient;

    private static PartitionedRateLimiter<HttpContext> CreateInMemoryLimiter(int permitLimit, int windowSeconds) =>
        PartitionedRateLimiter.Create<HttpContext, string>(context => RateLimitPartition.GetFixedWindowLimiter(
            ClientKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueLimit = 0,
            }));

    private long WindowStart(int windowSeconds) =>
        _timeProvider.GetUtcNow().ToUnixTimeSeconds() / windowSeconds * windowSeconds;

    private async Task<bool> IsWithinLimitAsync(
        string policy, string client, int permitLimit, int windowSeconds, CancellationToken cancellationToken)
    {
        var key = string.Create(
            CultureInfo.InvariantCulture, $"{KeyPrefix}{policy}:{client}:{WindowStart(windowSeconds)}");
        var count = await _counter.IncrementAsync(key, TimeSpan.FromSeconds(windowSeconds), cancellationToken);
        return count <= permitLimit;
    }

    private Lease AcquireFromFallback(HttpContext resource, bool auth)
    {
        using var global = _fallbackGlobal.AttemptAcquire(resource);
        if (!global.IsAcquired)
        {
            return Lease.Rejected;
        }

        if (!auth)
        {
            return Lease.Acquired;
        }

        using var authLease = _fallbackAuth.AttemptAcquire(resource);
        return authLease.IsAcquired ? Lease.Acquired : Lease.Rejected;
    }

    private void MarkWindowFailed(SharedStateUnavailableException exception)
    {
        var windowStart = WindowStart(_options.WindowSeconds);
        if (Interlocked.Exchange(ref _failedWindowStart, windowStart) != windowStart)
        {
            LogFallback(_logger, exception);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Shared rate limit store unavailable; the per-instance rate limit applies until it recovers.")]
    private static partial void LogFallback(ILogger logger, Exception exception);

    /// <summary>Immutable leases without metadata; disposing them releases nothing (fixed window).</summary>
    private sealed class Lease : RateLimitLease
    {
        public static readonly Lease Acquired = new(isAcquired: true);
        public static readonly Lease Rejected = new(isAcquired: false);

        private Lease(bool isAcquired)
        {
            IsAcquired = isAcquired;
        }

        public override bool IsAcquired { get; }

        public override IEnumerable<string> MetadataNames => [];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = null;
            return false;
        }
    }
}
