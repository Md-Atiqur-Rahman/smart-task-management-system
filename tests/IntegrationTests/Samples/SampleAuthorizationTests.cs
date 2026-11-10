// SAMPLE: copy-source, delete or rename
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Datavanced.SmartTaskManagement.IntegrationTests.Api;
using Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;
using Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

using static Datavanced.SmartTaskManagement.IntegrationTests.Samples.SampleApi;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Samples;

/// <summary>The Sample endpoints require a valid access token (Phase 2).</summary>
[Collection(SqlServerCollectionDefinition.Name)]
public sealed class SampleAuthorizationTests(SqlServerFixture database)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListSamples_NoToken_Returns401ProblemDetails()
    {
        using var factory = new ApiFactory().WithDatabase(database.Options);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Uri(Route), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("errorCode").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ListSamples_WithToken_Returns200()
    {
        using var factory = new ApiFactory().WithDatabase(database.Options);
        using var client = await AuthenticatedClient.ForNewTeamMemberAsync(factory, _ct);

        using var response = await client.GetAsync(Uri(Route), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListUsers_AsTeamMember_Returns403ProblemDetails()
    {
        using var factory = new ApiFactory().WithDatabase(database.Options);
        using var client = await AuthenticatedClient.ForNewTeamMemberAsync(factory, _ct);

        using var response = await client.GetAsync(Uri("/api/users"), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("errorCode").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task OpenApi_ProtectedOperation_HasBearerRequirement_AnonymousDoesNot()
    {
        using var factory = new ApiFactory().WithEnvironment("Development").WithDatabase(database.Options);
        using var client = factory.CreateClient();

        var document = await client.GetFromJsonAsync<JsonElement>(Uri("/openapi/v1.json"), _ct);

        var paths = document.GetProperty("paths");
        var list = paths.GetProperty("/api/samples").GetProperty("get");
        list.GetProperty("security").EnumerateArray()
            .Any(requirement => requirement.TryGetProperty("Bearer", out _)).ShouldBeTrue();
        var login = paths.GetProperty("/api/auth/login").GetProperty("post");
        login.TryGetProperty("security", out _).ShouldBeFalse();
    }
}
