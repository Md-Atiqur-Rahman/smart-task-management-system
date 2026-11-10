using System.Globalization;

using Datavanced.SmartTaskManagement.Application.Abstractions.Security;

using StackExchange.Redis;

namespace Datavanced.SmartTaskManagement.Infrastructure.Redis;

/// <summary>Fixed-window counter in Redis: one atomic script increments and sets the expiry once.</summary>
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
        catch (RedisConnectionException ex)
        {
            throw new SharedStateUnavailableException(ex);
        }
        catch (RedisTimeoutException ex)
        {
            throw new SharedStateUnavailableException(ex);
        }
    }
}
