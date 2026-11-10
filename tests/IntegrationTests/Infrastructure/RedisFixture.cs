using Testcontainers.Redis;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>
/// One real Redis container per test collection. The image comes from .env.example
/// (same pin as docker-compose.yml); the container requires a password like the local one.
/// </summary>
public sealed class RedisFixture : IAsyncLifetime
{
    private const string Password = "Integration_Tests_Redis_Passw0rd!";

    private readonly RedisContainer _container =
        new RedisBuilder(RepositoryFiles.EnvExample("REDIS_IMAGE"))
            .WithCommand("--requirepass", Password)
            .Build();

    /// <summary>StackExchange.Redis connection string (no TLS: Testing only).</summary>
    public string ConnectionString =>
        $"{_container.Hostname}:{_container.GetMappedPublicPort(RedisBuilder.RedisPort)},password={Password}";

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
    }

    /// <summary>Stops the container (outage simulation); only for a fixture the test owns.</summary>
    public async Task StopAsync()
    {
        await _container.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class RedisCollectionDefinition : ICollectionFixture<RedisFixture>
{
    public const string Name = "Redis";
}
