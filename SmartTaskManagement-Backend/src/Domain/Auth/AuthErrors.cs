using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Domain.Auth;

/// <summary>Expected failures of authentication. Codes are part of the API contract.</summary>
public static class AuthErrors
{
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("auth.invalid_credentials", "The email or password is incorrect.");

    public static readonly Error InvalidRefreshToken =
        Error.Unauthorized("auth.invalid_refresh_token", "The refresh token is invalid or expired.");

    public static readonly Error RefreshSuperseded =
        Error.Unauthorized("auth.refresh_superseded", "The refresh token was already used. Sign in again.");

    public static readonly Error TokenRevoked =
        Error.Unauthorized("auth.token_revoked", "The access token is no longer valid. Sign in again.");

    public static readonly Error Unavailable =
        Error.Unavailable("auth.unavailable", "Authentication is temporarily unavailable. Try again later.");
}
