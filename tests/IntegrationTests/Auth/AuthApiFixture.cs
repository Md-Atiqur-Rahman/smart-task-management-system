using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.IntegrationTests.Api;
using Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Auth;

/// <summary>
/// Real host + real SQL Server + real Redis for the auth endpoint tests. The client does not
/// manage cookies: the refresh cookie is Secure and the test server runs over http, so tests
/// read Set-Cookie and send the Cookie header themselves.
/// </summary>
public sealed class AuthApi : IDisposable
{
    public const string AllowedOrigin = "https://app.example.test";
    public const string Route = "/api/auth";
    public const string Password = "Passw0rd-for-tests";
    public const string CookieName = "__Secure-refresh_token";

    private readonly SqlServerFixture _database;

    /// <param name="database">The SQL Server the host uses.</param>
    /// <param name="redisConnectionString">
    /// The real Redis the host uses; null keeps the in-memory token version store, rate limit counter
    /// and in-process live event publisher of <see cref="ApiFactory"/>.
    /// </param>
    /// <param name="configure">Extra settings applied before the host starts (for example a shorter grace period).</param>
    public AuthApi(SqlServerFixture database, string? redisConnectionString, Action<ApiFactory>? configure = null)
    {
        _database = database;
        Factory = new ApiFactory().WithDatabase(database.Options);
        if (redisConnectionString is not null)
        {
            Factory.WithRedis(redisConnectionString);
        }

        Factory
            .WithSetting("Cors:AllowedOrigins:0", AllowedOrigin)
            // These tests cover the auth flows, not rate limiting (SharedRateLimitingTests):
            // the whole collection shares one client IP and one Redis counter.
            .WithSetting("RateLimiting:PermitLimit", "100000")
            .WithSetting("RateLimiting:AuthPermitLimit", "100000");
        configure?.Invoke(Factory);
        Client = Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    public ApiFactory Factory { get; }

    public HttpClient Client { get; }

    public static Uri Uri(string relative) => new(relative, UriKind.Relative);

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@example.test";

    public async Task<HttpResponseMessage> PostAsync(
        string path, CancellationToken ct, string? cookie = null, string? origin = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Uri(path));
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", $"{CookieName}={cookie}");
        }

        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        return await Client.SendAsync(request, ct);
    }

    /// <summary>The Set-Cookie header lines for the refresh cookie (empty when none).</summary>
    public static IReadOnlyList<string> RefreshSetCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? [.. values.Where(v => v.StartsWith(CookieName + "=", StringComparison.Ordinal))]
            : [];

    /// <summary>The raw refresh token from the single Set-Cookie line.</summary>
    public static string CookieValue(HttpResponseMessage response)
    {
        var line = RefreshSetCookies(response).Single();
        var start = CookieName.Length + 1;
        return line[start..line.IndexOf(';', StringComparison.Ordinal)];
    }

    public async Task<(string Email, string Cookie, JsonElement Body)> RegisterAsync(CancellationToken ct)
    {
        var email = NewEmail();
        using var response = await Client.PostAsJsonAsync(
            Uri($"{Route}/register"), new { email, fullName = "Rahim Uddin", password = Password }, ct);
        response.EnsureSuccessStatusCode();
        return (email, CookieValue(response), await response.Content.ReadFromJsonAsync<JsonElement>(ct));
    }

    public async Task<string> LoginAsync(string email, CancellationToken ct)
    {
        using var response = await Client.PostAsJsonAsync(
            Uri($"{Route}/login"), new { email, password = Password }, ct);
        response.EnsureSuccessStatusCode();
        return CookieValue(response);
    }

    /// <summary>Registers a user, promotes it to <paramref name="role"/> in the database, logs in.</summary>
    public async Task<TestUser> CreateUserAsync(Role role, CancellationToken ct, string? marker = null)
    {
        var email = $"user-{marker ?? Guid.NewGuid().ToString("N")}-{Guid.NewGuid():N}@example.test";
        using var register = await Client.PostAsJsonAsync(
            Uri($"{Route}/register"), new { email, fullName = $"User {marker}", password = Password }, ct);
        register.EnsureSuccessStatusCode();
        var id = (await register.Content.ReadFromJsonAsync<JsonElement>(ct))
            .GetProperty("user").GetProperty("id").GetGuid();

        if (role != Role.TeamMember)
        {
            await using var context = _database.CreateDbContext();
            var user = await context.Set<User>().SingleAsync(u => u.Id == id, ct);
            user.ChangeRole(role);
            await context.SaveChangesAsync(ct);
        }

        using var login = await Client.PostAsJsonAsync(
            Uri($"{Route}/login"), new { email, password = Password }, ct);
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("accessToken").GetString()!;
        return new TestUser(id, token, email, CookieValue(login));
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string token, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, Uri(path));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await Client.SendAsync(request, ct);
    }

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
    }
}

/// <summary>A registered and logged-in test user.</summary>
public sealed record TestUser(Guid Id, string Token, string Email, string Cookie);

/// <summary>One SQL Server and one Redis container shared by all auth test classes.</summary>
[CollectionDefinition(Name)]
public sealed class AuthCollectionDefinition : ICollectionFixture<SqlServerFixture>, ICollectionFixture<RedisFixture>
{
    public const string Name = "Auth";
}
