using System.Net;

using Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api;

/// <summary>The single /health endpoint, with the database and Redis checks against real containers.</summary>
[Collection(SqlServerCollectionDefinition.Name)]
public sealed class HealthTests(SqlServerFixture database, RedisFixture redis) : IClassFixture<RedisFixture>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Health_WithReachableDatabase_Returns200Healthy()
    {
        using var factory = new ApiFactory().WithDatabase(database.Options).WithRedis(redis.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(_ct)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Health_WithUnreachableDatabase_Returns503()
    {
        using var factory = new ApiFactory()
            .WithDatabase(database.Options)
            .WithRedis(redis.ConnectionString)
            .WithSetting("Database:Port", "1");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Health_WithUnreachableRedis_Returns503()
    {
        using var factory = new ApiFactory()
            .WithDatabase(database.Options)
            .WithRedis("127.0.0.1:1,password=unused")
            .WithSetting("Redis:ConnectTimeoutMs", "200")
            .WithSetting("Redis:OperationTimeoutMs", "200");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    // Load-balancer probes must never be throttled by the client rate limit.
    [Fact]
    public async Task Health_IsNotRateLimited()
    {
        using var factory = new ApiFactory()
            .WithDatabase(database.Options)
            .WithRedis(redis.ConnectionString)
            .WithSetting("RateLimiting:PermitLimit", "1");
        using var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), _ct);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task AliveEndpoint_DoesNotExist()
    {
        using var factory = new ApiFactory().WithDatabase(database.Options).WithRedis(redis.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/alive", UriKind.Relative), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
