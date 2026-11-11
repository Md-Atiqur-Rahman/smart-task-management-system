using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api;

/// <summary>The token version check on every authenticated request (ADR 0013, ADR 0014).</summary>
public sealed class TokenVersionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Whoami_TokenVersionMatches_Returns200()
    {
        var userId = Guid.NewGuid();
        await factory.TokenVersions.SetIfHigherAsync(userId, 2, _ct);

        using var response = await GetAsync("/test/whoami", Token(userId, 2));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Whoami_TokenVersionBehind_Returns401TokenRevoked()
    {
        var userId = Guid.NewGuid();
        await factory.TokenVersions.SetIfHigherAsync(userId, 3, _ct);

        using var response = await GetAsync("/test/whoami", Token(userId, 2));

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "auth.token_revoked");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("abc")]
    public async Task Whoami_MissingOrNonIntegerTv_Returns401TokenRevoked(string? tv)
    {
        var claims = new Dictionary<string, object> { ["sub"] = Guid.NewGuid().ToString() };
        if (tv is not null)
        {
            claims["tv"] = tv;
        }

        using var response = await GetAsync("/test/whoami", TestTokens.Create(claims));

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "auth.token_revoked");
    }

    [Fact]
    public async Task Whoami_StoreUnavailable_Returns503AuthUnavailable()
    {
        using var outageFactory = new ApiFactory();
        outageFactory.TokenVersions.SimulateOutage = true;
        using var client = outageFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Token(Guid.NewGuid(), 1));

        using var response = await client.GetAsync(new Uri("/test/whoami", UriKind.Relative), _ct);

        await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable, "auth.unavailable");
    }

    [Fact]
    public async Task AnonymousEndpoint_WithStaleToken_StillReturns200()
    {
        var userId = Guid.NewGuid();
        await factory.TokenVersions.SetIfHigherAsync(userId, 5, _ct);

        using var response = await GetAsync("/test/anonymous", Token(userId, 1));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static string Token(Guid userId, int version) =>
        TestTokens.Create(new Dictionary<string, object>
        {
            ["sub"] = userId.ToString(),
            ["tv"] = version,
        });

    private async Task<HttpResponseMessage> GetAsync(string path, string token)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync(new Uri(path, UriKind.Relative), _ct);
    }

    private async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string errorCode)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("errorCode").GetString().ShouldBe(errorCode);
        body.GetProperty("traceId").GetString().ShouldNotBeNullOrEmpty();
        body.GetProperty("status").GetInt32().ShouldBe((int)status);
    }
}
