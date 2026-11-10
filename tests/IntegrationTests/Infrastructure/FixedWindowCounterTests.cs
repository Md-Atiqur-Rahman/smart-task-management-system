using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Redis;
using Datavanced.SmartTaskManagement.Infrastructure.Redis;

using StackExchange.Redis;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>Rate limit counter (INCR + PEXPIRE once) against a real Redis.</summary>
[Collection(RedisCollectionDefinition.Name)]
public sealed class FixedWindowCounterTests(RedisFixture redis) : IAsyncLifetime
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly List<IConnectionMultiplexer> _connections = [];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Increment_Repeated_CountsUpAndKeepsTheFirstExpiry()
    {
        var (counter, connection) = Create(redis.ConnectionString);
        var key = $"ratelimit:test:{Guid.NewGuid():N}:0";
        var db = connection.GetDatabase();

        (await counter.IncrementAsync(key, TimeSpan.FromSeconds(60), _ct)).ShouldBe(1);
        var afterFirst = await db.KeyTimeToLiveAsync(key);
        (await counter.IncrementAsync(key, TimeSpan.FromSeconds(600), _ct)).ShouldBe(2);
        var afterSecond = await db.KeyTimeToLiveAsync(key);

        afterFirst.ShouldNotBeNull();
        afterFirst.Value.ShouldBeInRange(TimeSpan.FromSeconds(59), TimeSpan.FromSeconds(60));
        afterSecond.ShouldNotBeNull();
        afterSecond.Value.ShouldBeLessThanOrEqualTo(afterFirst.Value);
    }

    [Fact]
    public async Task Increment_RedisUnreachable_ThrowsSharedStateUnavailable()
    {
        var (counter, _) = Create("127.0.0.1:1,password=unused", connectTimeoutMs: 200, operationTimeoutMs: 200);

        var exception = await Should.ThrowAsync<SharedStateUnavailableException>(
            () => counter.IncrementAsync("ratelimit:test:unreachable:0", TimeSpan.FromSeconds(60), _ct));

        exception.InnerException.ShouldNotBeNull();
        (exception.InnerException is RedisConnectionException or RedisTimeoutException).ShouldBeTrue();
    }

    private (RedisFixedWindowCounter Counter, IConnectionMultiplexer Connection) Create(
        string connectionString, int connectTimeoutMs = 2000, int operationTimeoutMs = 1000)
    {
        var options = new RedisOptions
        {
            ConnectionString = connectionString,
            ConnectTimeoutMs = connectTimeoutMs,
            OperationTimeoutMs = operationTimeoutMs,
        };
        var connection = ConnectionMultiplexer.Connect(RedisConnection.BuildConfiguration(options));
        _connections.Add(connection);
        return (new RedisFixedWindowCounter(connection), connection);
    }
}
