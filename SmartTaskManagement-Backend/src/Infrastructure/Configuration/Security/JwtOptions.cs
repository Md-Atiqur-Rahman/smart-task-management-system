using System.ComponentModel.DataAnnotations;

namespace Datavanced.SmartTaskManagement.Infrastructure.Configuration.Security;

/// <summary>
/// JWT settings, bound from "Jwt". The signing key is a secret: it comes only
/// from user-secrets or the environment (Jwt__SigningKey).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumSigningKeyLength = 32;
    public const int MinimumAccessTokenMinutes = 1;
    public const int MaximumAccessTokenMinutes = 60;
    public const int DefaultAccessTokenMinutes = 10;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Required]
    [MinLength(MinimumSigningKeyLength)]
    public string SigningKey { get; init; } = string.Empty;

    [Range(MinimumAccessTokenMinutes, MaximumAccessTokenMinutes)]
    public int AccessTokenMinutes { get; init; } = DefaultAccessTokenMinutes;
}
