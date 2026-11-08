namespace Contoso.TemplateApp.Application.Abstractions.Security;

/// <summary>
/// The ONLY place claim names are written. JWT inbound claim mapping is turned
/// off in the Api, so the raw JWT names arrive unchanged.
/// </summary>
public static class SecurityClaimTypes
{
    public const string UserId = "sub";

    public const string Role = "role";
}
