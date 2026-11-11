namespace Datavanced.SmartTaskManagement.Api.Auth;

/// <summary>
/// The refresh token cookie (docs/auth-design.md section 4): HttpOnly, Secure, SameSite=Strict,
/// scoped to the auth routes. The value never appears in a body or a log.
/// </summary>
internal static class RefreshTokenCookie
{
    public const string Name = "__Secure-refresh_token";
    public const string Path = "/api/auth";

    public static void Append(HttpResponse response, string rawToken, DateTimeOffset expiresAt) =>
        response.Cookies.Append(Name, rawToken, Options(expiresAt));

    /// <summary>Deletes the cookie: same name and path, expiry in the past.</summary>
    public static void Delete(HttpResponse response) =>
        response.Cookies.Delete(Name, Options(DateTimeOffset.UnixEpoch));

    public static string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(Name, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    private static CookieOptions Options(DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expires,
    };
}
