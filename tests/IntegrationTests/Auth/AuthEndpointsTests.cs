using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

using static Datavanced.SmartTaskManagement.IntegrationTests.Auth.AuthApi;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Auth;

[Collection(AuthCollectionDefinition.Name)]
public sealed class AuthEndpointsTests(SqlServerFixture database, RedisFixture redis) : IDisposable
{
    private readonly AuthApi _api = new(database, redis.ConnectionString);
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Register_ThenMe_WithReturnedAccessToken_Returns200()
    {
        var email = NewEmail();
        using var response = await _api.Client.PostAsJsonAsync(
            Uri($"{Route}/register"), new { email, fullName = "Rahim Uddin", password = Password }, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe("/api/auth/me");

        var setCookie = RefreshSetCookies(response).Single();
        setCookie.ShouldStartWith("__Secure-refresh_token=");
        setCookie.ShouldContain("httponly", Case.Insensitive);
        setCookie.ShouldContain("; secure", Case.Insensitive);
        setCookie.ShouldContain("samesite=strict", Case.Insensitive);
        setCookie.ShouldContain("path=/api/auth", Case.Insensitive);

        var raw = await response.Content.ReadAsStringAsync(_ct);
        var body = JsonSerializer.Deserialize<JsonElement>(raw);
        body.TryGetProperty("refreshToken", out _).ShouldBeFalse();
        raw.ShouldNotContain(CookieValue(response));
        body.GetProperty("user").GetProperty("role").GetString().ShouldBe("TeamMember");
        var accessToken = body.GetProperty("accessToken").GetString()!;

        using var me = new HttpRequestMessage(HttpMethod.Get, Uri($"{Route}/me"));
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var meResponse = await _api.Client.SendAsync(me, _ct);

        meResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = await meResponse.Content.ReadFromJsonAsync<JsonElement>(_ct);
        user.GetProperty("email").GetString().ShouldBe(email);
        user.GetProperty("role").GetString().ShouldBe("TeamMember");
    }

    [Fact]
    public async Task Login_MixedCaseEmail_Succeeds()
    {
        var (email, _, _) = await _api.RegisterAsync(_ct);
        var mixed = $" {email.ToUpperInvariant()} ";

        using var response = await _api.Client.PostAsJsonAsync(
            Uri($"{Route}/login"), new { email = mixed, password = Password }, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        RefreshSetCookies(response).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401InvalidCredentials()
    {
        var (email, _, _) = await _api.RegisterAsync(_ct);

        using var response = await _api.Client.PostAsJsonAsync(
            Uri($"{Route}/login"), new { email, password = "Wrong-password-1" }, _ct);

        await ShouldBeProblem(response, HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        RefreshSetCookies(response).ShouldBeEmpty();
    }

    [Fact]
    public async Task Refresh_WithCookie_RotatesCookieAndOldCookieWithinGraceIsSuperseded()
    {
        var (_, oldCookie, _) = await _api.RegisterAsync(_ct);

        using var first = await _api.PostAsync($"{Route}/refresh", _ct, cookie: oldCookie);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var newCookie = CookieValue(first);
        newCookie.ShouldNotBe(oldCookie);
        (await first.Content.ReadAsStringAsync(_ct)).ShouldNotContain(newCookie);

        using var second = await _api.PostAsync($"{Route}/refresh", _ct, cookie: oldCookie);
        await ShouldBeProblem(second, HttpStatusCode.Unauthorized, "auth.refresh_superseded");
        RefreshSetCookies(second).ShouldBeEmpty();

        using var third = await _api.PostAsync($"{Route}/refresh", _ct, cookie: newCookie);
        third.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_GarbageCookie_Returns401AndDeletesCookie()
    {
        using var response = await _api.PostAsync($"{Route}/refresh", _ct, cookie: "garbage");

        await ShouldBeProblem(response, HttpStatusCode.Unauthorized, "auth.invalid_refresh_token");
        ShouldDeleteCookie(response);
    }

    [Fact]
    public async Task Refresh_ForeignOrigin_Returns403OriginNotAllowed()
    {
        var (_, cookie, _) = await _api.RegisterAsync(_ct);

        using var response = await _api.PostAsync($"{Route}/refresh", _ct, cookie: cookie, origin: "https://evil.example.test");

        await ShouldBeProblem(response, HttpStatusCode.Forbidden, "auth.origin_not_allowed");

        using var allowed = await _api.PostAsync($"{Route}/refresh", _ct, cookie: cookie, origin: AllowedOrigin);
        allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_NoOriginHeader_IsAllowed()
    {
        var (_, cookie, _) = await _api.RegisterAsync(_ct);

        using var response = await _api.PostAsync($"{Route}/refresh", _ct, cookie: cookie);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_ThenRefreshWithSameCookie_Returns401_OtherDeviceStillRefreshes()
    {
        var (email, deviceOne, _) = await _api.RegisterAsync(_ct);
        var deviceTwo = await _api.LoginAsync(email, _ct);

        using var logout = await _api.PostAsync($"{Route}/logout", _ct, cookie: deviceOne);
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        ShouldDeleteCookie(logout);

        // The other device first: replaying the logged-out cookie is reuse and revokes every device (design section 5).
        using var other = await _api.PostAsync($"{Route}/refresh", _ct, cookie: deviceTwo);
        other.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var again = await _api.PostAsync($"{Route}/refresh", _ct, cookie: deviceOne);
        await ShouldBeProblem(again, HttpStatusCode.Unauthorized, "auth.invalid_refresh_token");
        ShouldDeleteCookie(again);
    }

    [Fact]
    public async Task Logout_WithoutCookie_Returns204()
    {
        using var response = await _api.PostAsync($"{Route}/logout", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    public void Dispose() => _api.Dispose();

    private static void ShouldDeleteCookie(HttpResponseMessage response)
    {
        var line = RefreshSetCookies(response).Single();
        line.ShouldStartWith("__Secure-refresh_token=;");
        line.ShouldContain("path=/api/auth", Case.Insensitive);
        var expires = line.Split(';')
            .Select(p => p.Trim())
            .Single(p => p.StartsWith("expires=", StringComparison.OrdinalIgnoreCase))["expires=".Length..];
        DateTimeOffset.Parse(expires, CultureInfo.InvariantCulture).ShouldBeLessThan(DateTimeOffset.UtcNow);
    }

    private async Task ShouldBeProblem(HttpResponseMessage response, HttpStatusCode status, string errorCode)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("errorCode").GetString().ShouldBe(errorCode);
    }
}

/// <summary>Has its own Redis container because it stops it; other tests keep theirs.</summary>
[Collection(SqlServerCollectionDefinition.Name)]
public sealed class AuthRedisOutageTests(SqlServerFixture database)
{
    [Fact]
    public async Task Login_RedisStopped_StillReturns200()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ownRedis = new RedisFixture();
        await ownRedis.InitializeAsync();
        using var api = new AuthApi(database, ownRedis.ConnectionString);
        var (email, _, _) = await api.RegisterAsync(ct);

        await ownRedis.StopAsync();

        using var response = await api.Client.PostAsJsonAsync(
            Uri($"{Route}/login"), new { email, password = Password }, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
