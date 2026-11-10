using System.Globalization;

using Datavanced.SmartTaskManagement.Application.Abstractions.Security;

using StackExchange.Redis;

namespace Datavanced.SmartTaskManagement.Infrastructure.Redis;

/// <summary>
/// Fixed-window counter in Redis: one atomic script increments and sets the expiry once.
/// Any Redis error (connection, timeout, server error) becomes <see cref="SharedStateUnavailableException"/>.
/// </summary>
public sealed class RedisFixedWindowCounter(IConnectionMultiplexer connection) : IFixedWindowCounter
{
    // INCR, and PEXPIRE only on the first increment, so the window is never extended.
    private const string IncrementScript = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then
          redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        return count
        """;

    public async Task<long> IncrementAsync(string key, TimeSpan window, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var windowMs = ((long)window.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
        try
        {
            var result = await connection.GetDatabase().ScriptEvaluateAsync(IncrementScript, [key], [windowMs]);
            return (long)result;
        }
        catch (RedisException ex)
        {
            // Connection and server errors (LOADING, READONLY, OOM, ...; RedisException is their base
            // type): the rate limit fails open on any Redis error (ADR 0013, ADR 0015).
            throw new SharedStateUnavailableException(ex);
        }
        catch (RedisTimeoutException ex)
        {
            // Not a RedisException (derives from TimeoutException); same handling.
            throw new SharedStateUnavailableException(ex);
        }
        catch (RedisCommandException ex)
        {
            // Not a RedisException (derives from Exception); a command the client refuses to send.
            throw new SharedStateUnavailableException(ex);
        }
    }
}
