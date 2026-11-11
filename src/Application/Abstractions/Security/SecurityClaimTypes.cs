namespace Datavanced.SmartTaskManagement.Application.Abstractions.Security;

/// <summary>
/// The ONLY place claim names are written. JWT inbound claim mapping is turned
/// off in the Api, so the raw JWT names arrive unchanged.
/// </summary>
public static class SecurityClaimTypes
{
    public const string UserId = "sub";

    public const string Role = "role";

    public const string TokenVersion = "tv";

    public const string TokenId = "jti";

    /// <summary>Expiry of the access token (Unix seconds); the live stream ends at this time.</summary>
    public const string Expires = "exp";
}
