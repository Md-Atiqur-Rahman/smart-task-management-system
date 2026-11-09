using System.Net.Http.Json;
using System.Text.Json;

using Datavanced.SmartTaskManagement.IntegrationTests.Api;
using Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Mvc.Testing;

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

    public AuthApi(SqlServerFixture database, string redisConnectionString)
    {
        Factory = new ApiFactory()
            .WithDatabase(database.Options)
            .WithRedis(redisConnectionString)
            .WithSetting("Cors:AllowedOrigins:0", AllowedOrigin);
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

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
    }
}

/// <summary>One SQL Server and one Redis container shared by all auth test classes.</summary>
[CollectionDefinition(Name)]
public sealed class AuthCollectionDefinition : ICollectionFixture<SqlServerFixture>, ICollectionFixture<RedisFixture>
{
    public const string Name = "Auth";
}
