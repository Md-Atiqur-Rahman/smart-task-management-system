using System.Globalization;

using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Redis;

using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Datavanced.SmartTaskManagement.Infrastructure.Redis;

public sealed class RedisTokenVersionStore(IConnectionMultiplexer connection, IOptions<RedisOptions> options)
    : ITokenVersionStore
{
    private const string KeyPrefix = "auth:token-version:";

    // Atomic forward-only write: set only when missing or lower, and then set the fixed expiry.
    private const string SetIfHigherScript = """
        local current = redis.call('GET', KEYS[1])
        if (not current) or (tonumber(current) < tonumber(ARGV[1])) then
          redis.call('SET', KEYS[1], ARGV[1], 'EX', ARGV[2])
          return 1
        end
        return 0
        """;

    public static string KeyFor(Guid userId) => KeyPrefix + userId.ToString("D");

    public async Task<int?> GetAsync(Guid userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var value = await ExecuteAsync(db => db.StringGetAsync(KeyFor(userId)));
        return value.HasValue
            ? (int)value
            : null;
    }

    public async Task SetIfHigherAsync(Guid userId, int version, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var ttlSeconds = options.Value.TokenVersionTtlMinutes * 60;
        await ExecuteAsync(db => db.ScriptEvaluateAsync(
            SetIfHigherScript,
            [KeyFor(userId)],
            [version.ToString(CultureInfo.InvariantCulture), ttlSeconds.ToString(CultureInfo.InvariantCulture)]));
    }

    public async Task RemoveAsync(Guid userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await ExecuteAsync(db => db.KeyDeleteAsync(KeyFor(userId)));
    }

    private async Task<T> ExecuteAsync<T>(Func<IDatabase, Task<T>> operation)
    {
        try
        {
            return await operation(connection.GetDatabase());
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
