using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api;

/// <summary>Security context set once from a validated token, and the security baseline, through the real middleware pipeline.</summary>
public sealed class SecurityTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string[] Roles = ["Admin", "User"];

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    // JWT inbound claim mapping is off: "sub" and "role" keep their names and
    // reach ISecurityContext through the one middleware.
    [Fact]
    public async Task SignedToken_PopulatesSecurityContext_FromSubAndRoleClaims()
    {
        var userId = Guid.NewGuid();
        var token = TestTokens.Create(new Dictionary<string, object>
        {
            ["sub"] = userId.ToString(),
            ["role"] = Roles,
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var me = await client.GetFromJsonAsync<WhoAmIResponse>(
            new Uri("/test/whoami", UriKind.Relative), _ct);

        me.ShouldNotBeNull();
        me.IsAuthenticated.ShouldBeTrue();
        me.UserId.ShouldBe(userId);
        me.Roles.ShouldBe(Roles, ignoreOrder: true);
    }

    [Fact]
    public async Task NoToken_OnAuthorizeEndpoint_Returns401ProblemDetails()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/test/whoami", UriKind.Relative), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenSignedWithOtherKey_Returns401()
    {
        var token = TestTokens.Create(
            new Dictionary<string, object> { ["sub"] = Guid.NewGuid().ToString() },
            signingKey: "a-completely-different-signing-key-0123456789");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync(new Uri("/test/whoami", UriKind.Relative), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenWithoutSubClaim_IsUnauthenticatedInSecurityContext()
    {
        var token = TestTokens.Create(new Dictionary<string, object> { ["role"] = "Admin" });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var me = await client.GetFromJsonAsync<WhoAmIResponse>(
            new Uri("/test/anonymous", UriKind.Relative), _ct);

        me.ShouldNotBeNull();
        me.IsAuthenticated.ShouldBeFalse();
        me.UserId.ShouldBeNull();
        me.Roles.ShouldBeEmpty();
    }

    [Fact]
    public async Task Responses_IncludeSecurityHeaders()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/test/anonymous", UriKind.Relative), _ct);

        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.GetValues("X-Frame-Options").ShouldBe(["DENY"]);
        response.Headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
    }

    [Fact]
    public async Task UnknownRoute_Returns404ProblemDetailsWithErrorCode()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/test/does-not-exist", UriKind.Relative), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("errorCode").GetString().ShouldBe("http.404");
    }
}
