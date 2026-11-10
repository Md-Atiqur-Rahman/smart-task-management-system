using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

using Microsoft.Extensions.Time.Testing;

using Serilog.Events;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api;

/// <summary>
/// Per-IP limit shared by all instances through Redis, stricter auth limit, fail-open fallback
/// (ADR 0013). A fixed clock keeps every request in one window.
/// </summary>
public sealed class SharedRateLimitingTests(RedisFixture redis) : IClassFixture<RedisFixture>
{
    private const int WindowSeconds = 60;

    private static readonly DateTimeOffset Now = new(2026, 11, 10, 10, 0, 30, TimeSpan.Zero);

    private static readonly Uri AnonymousUri = new("/test/anonymous", UriKind.Relative);

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task TwoHosts_SharingRedis_ShareOneLimit()
    {
        var clock = new FakeTimeProvider(Now);
        using var first = CreateFactory(clock, permitLimit: 3).WithRedis(redis.ConnectionString);
        using var second = CreateFactory(clock, permitLimit: 3).WithRedis(redis.ConnectionString);
        using var firstClient = first.CreateClient();
        using var secondClient = second.CreateClient();

        (await firstClient.GetAsync(AnonymousUri, _ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await secondClient.GetAsync(AnonymousUri, _ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await firstClient.GetAsync(AnonymousUri, _ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var fourth = await secondClient.GetAsync(AnonymousUri, _ct);

        await ShouldBeRateLimitedAsync(fourth);
    }

    [Fact]
    public async Task Login_ElevenTimesInAMinute_EleventhIs429()
    {
        using var factory = CreateFactory(new FakeTimeProvider(Now), permitLimit: 100);
        using var client = factory.CreateClient();
        var uri = new Uri("/api/auth/login", UriKind.Relative);

        // An empty body fails validation (400) before any database access; it still counts.
        for (var i = 0; i < 10; i++)
        {
            using var body = new StringContent("{}", Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(uri, body, _ct);
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        using var eleventhBody = new StringContent("{}", Encoding.UTF8, "application/json");
        using var eleventh = await client.PostAsync(uri, eleventhBody, _ct);

        await ShouldBeRateLimitedAsync(eleventh);
        var windowStart = (Now.ToUnixTimeSeconds() / WindowSeconds * WindowSeconds)
            .ToString(CultureInfo.InvariantCulture);
        factory.RateLimitCounter.Keys.ShouldContain(k =>
            k.StartsWith("ratelimit:auth:", StringComparison.Ordinal)
            && k.EndsWith(":" + windowStart, StringComparison.Ordinal));
        factory.RateLimitCounter.Keys.ShouldContain(k =>
            k.StartsWith("ratelimit:global:", StringComparison.Ordinal)
            && k.EndsWith(":" + windowStart, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnonymousEndpoint_IsNotCountedByTheAuthLimit()
    {
        using var factory = CreateFactory(new FakeTimeProvider(Now), permitLimit: 100)
            .WithSetting("RateLimiting:AuthPermitLimit", "1");
        using var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            (await client.GetAsync(AnonymousUri, _ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        factory.RateLimitCounter.Keys.ShouldNotContain(k => k.StartsWith("ratelimit:auth:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RedisDown_FallsBackToInstanceLimit()
    {
        using var factory = CreateFactory(new FakeTimeProvider(Now), permitLimit: 2);
        factory.RateLimitCounter.SimulateOutage = true;
        using var client = factory.CreateClient();

        (await client.GetAsync(AnonymousUri, _ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync(AnonymousUri, _ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var third = await client.GetAsync(AnonymousUri, _ct);

        await ShouldBeRateLimitedAsync(third);
        // One warning per window and instance, not one per request; no client IP at warning level.
        var warnings = factory.LogSink.Events
            .Where(e => e.Level == LogEventLevel.Warning
                && e.MessageTemplate.Text.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
            .ToList();
        warnings.Count.ShouldBe(1);
        warnings[0].Properties.Keys.ShouldNotContain(k => k.Contains("Ip", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Health_IsNotRateLimited()
    {
        using var factory = CreateFactory(new FakeTimeProvider(Now), permitLimit: 1)
            .WithSetting("Database:Port", "1")
            .WithSetting("Redis:ConnectTimeoutMs", "200")
            .WithSetting("Redis:OperationTimeoutMs", "200");
        using var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), _ct);
            response.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        }

        factory.RateLimitCounter.Keys.ShouldBeEmpty();
    }

    private static ApiFactory CreateFactory(TimeProvider clock, int permitLimit) =>
        new ApiFactory()
            .WithTimeProvider(clock)
            .WithSetting("RateLimiting:PermitLimit", permitLimit.ToString(CultureInfo.InvariantCulture))
            .WithSetting("RateLimiting:WindowSeconds", WindowSeconds.ToString(CultureInfo.InvariantCulture));

    private async Task ShouldBeRateLimitedAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("errorCode").GetString().ShouldBe("rate_limited");
    }
}
