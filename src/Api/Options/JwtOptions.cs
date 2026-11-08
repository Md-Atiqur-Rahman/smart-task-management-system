using System.ComponentModel.DataAnnotations;

namespace Contoso.TemplateApp.Api.Options;

/// <summary>
/// JWT validation settings, bound from "Jwt". The signing key is a secret: it
/// comes only from user-secrets or the environment (Jwt__SigningKey).
/// </summary>
internal sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumSigningKeyLength = 32;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Required]
    [MinLength(MinimumSigningKeyLength)]
    public string SigningKey { get; init; } = string.Empty;
}
