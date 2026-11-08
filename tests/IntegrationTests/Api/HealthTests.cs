using System.Net;

using Contoso.TemplateApp.IntegrationTests.Infrastructure;

namespace Contoso.TemplateApp.IntegrationTests.Api;

/// <summary>The single /health endpoint, with the database check against real SQL Server.</summary>
[Collection(SqlServerCollectionDefinition.Name)]
public sealed class HealthTests(SqlServerFixture database)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Health_WithReachableDatabase_Returns200Healthy()
    {
        using var factory = new ApiFactory().WithDatabase(database.Options);
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
            .WithSetting("Database:Port", "1");
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
        using var factory = new ApiFactory().WithDatabase(database.Options);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/alive", UriKind.Relative), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
