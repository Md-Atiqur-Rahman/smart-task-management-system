using StackExchange.Redis;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>
/// The Redis classes catch <see cref="RedisException"/>, <see cref="RedisTimeoutException"/> and
/// <see cref="RedisCommandException"/> (the last two do not derive from RedisException).
/// This guard fails when the client library adds a public exception type outside them, so the
/// catch list is checked against the real hierarchy instead of being assumed.
/// </summary>
public sealed class RedisExceptionCoverageTests
{
    [Fact]
    public void EveryPublicRedisExceptionType_IsCoveredByTheCatchList()
    {
        var uncovered = typeof(RedisException).Assembly.GetExportedTypes()
            .Where(t => typeof(Exception).IsAssignableFrom(t))
            .Where(t => !typeof(RedisException).IsAssignableFrom(t)
                && !typeof(RedisTimeoutException).IsAssignableFrom(t)
                && !typeof(RedisCommandException).IsAssignableFrom(t))
            .Select(t => $"{t.FullName} : {t.BaseType?.FullName}")
            .ToList();

        uncovered.ShouldBeEmpty();
    }
}
