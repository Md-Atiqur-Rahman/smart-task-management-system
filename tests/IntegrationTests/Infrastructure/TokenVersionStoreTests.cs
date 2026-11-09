using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Redis;
using Datavanced.SmartTaskManagement.Infrastructure.Redis;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>Token version store and Redis health check against a real Redis.</summary>
[Collection(RedisCollectionDefinition.Name)]
public sealed class TokenVersionStoreTests(RedisFixture redis) : IAsyncLifetime
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
    public async Task Get_MissingKey_ReturnsNull()
    {
        var (store, _) = CreateStore(redis.ConnectionString);

        (await store.GetAsync(Guid.NewGuid(), _ct)).ShouldBeNull();
    }

    [Fact]
    public async Task SetIfHigher_LowerAfterHigher_KeepsHigher()
    {
        var (store, _) = CreateStore(redis.ConnectionString);
        var userId = Guid.NewGuid();

        await store.SetIfHigherAsync(userId, 3, _ct);
        await store.SetIfHigherAsync(userId, 2, _ct);

        (await store.GetAsync(userId, _ct)).ShouldBe(3);

        await store.SetIfHigherAsync(userId, 4, _ct);
        (await store.GetAsync(userId, _ct)).ShouldBe(4);
    }

    [Fact]
    public async Task SetIfHigher_Write_SetsFixedExpiry()
    {
        var (store, connection) = CreateStore(redis.ConnectionString);
        var userId = Guid.NewGuid();
        var db = connection.GetDatabase();

        await store.SetIfHigherAsync(userId, 1, _ct);
        var afterWrite = await db.KeyTimeToLiveAsync(RedisTokenVersionStore.KeyFor(userId));
        await store.GetAsync(userId, _ct);
        var afterRead = await db.KeyTimeToLiveAsync(RedisTokenVersionStore.KeyFor(userId));

        afterWrite.ShouldNotBeNull();
        afterWrite.Value.ShouldBeInRange(TimeSpan.FromMinutes(59), TimeSpan.FromMinutes(60));
        afterRead.ShouldNotBeNull();
        afterRead.Value.ShouldBeLessThanOrEqualTo(afterWrite.Value);
    }

    [Fact]
    public async Task Remove_ExistingKey_GetReturnsNull()
    {
        var (store, _) = CreateStore(redis.ConnectionString);
        var userId = Guid.NewGuid();
        await store.SetIfHigherAsync(userId, 2, _ct);

        await store.RemoveAsync(userId, _ct);

        (await store.GetAsync(userId, _ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Get_RedisUnreachable_ThrowsSharedStateUnavailable()
    {
        var (store, _) = CreateStore("127.0.0.1:1,password=unused", connectTimeoutMs: 200, operationTimeoutMs: 200);

        var exception = await Should.ThrowAsync<SharedStateUnavailableException>(
            () => store.GetAsync(Guid.NewGuid(), _ct));

        exception.ErrorCode.ShouldBe("shared_state.unavailable");
    }

    [Fact]
    public async Task Health_RedisUnreachable_ReportsUnhealthy()
    {
        var (_, connection) = CreateStore("127.0.0.1:1,password=unused", connectTimeoutMs: 200, operationTimeoutMs: 200);

        var result = await new RedisHealthCheck(connection).CheckHealthAsync(new HealthCheckContext(), _ct);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task Health_RedisReachable_ReportsHealthy()
    {
        var (_, connection) = CreateStore(redis.ConnectionString);

        var result = await new RedisHealthCheck(connection).CheckHealthAsync(new HealthCheckContext(), _ct);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    private (RedisTokenVersionStore Store, IConnectionMultiplexer Connection) CreateStore(
        string connectionString, int connectTimeoutMs = 2000, int operationTimeoutMs = 1000)
    {
        var options = new RedisOptions
        {
            ConnectionString = connectionString,
            TokenVersionTtlMinutes = 60,
            ConnectTimeoutMs = connectTimeoutMs,
            OperationTimeoutMs = operationTimeoutMs,
        };
        var connection = ConnectionMultiplexer.Connect(RedisConnection.BuildConfiguration(options));
        _connections.Add(connection);
        return (new RedisTokenVersionStore(connection, Options.Create(options)), connection);
    }
}
