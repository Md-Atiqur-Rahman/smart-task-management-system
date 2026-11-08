using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api;

/// <summary>Built-in rate limiting, limits from Options, rejection as ProblemDetails.</summary>
public sealed class RateLimitingTests
{
    [Fact]
    public async Task RequestsOverTheLimit_Return429ProblemDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = new ApiFactory()
            .WithSetting("RateLimiting:PermitLimit", "2")
            .WithSetting("RateLimiting:WindowSeconds", "60");
        using var client = factory.CreateClient();
        var uri = new Uri("/test/anonymous", UriKind.Relative);

        (await client.GetAsync(uri, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync(uri, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var third = await client.GetAsync(uri, ct);

        third.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        var body = await third.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("errorCode").GetString().ShouldBe("rate_limited");
    }
}
