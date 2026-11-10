namespace Datavanced.SmartTaskManagement.Api.Auth;

/// <summary>Names of the authorization policies registered in <c>AddJwtAuthentication</c>.</summary>
internal static class AuthorizationPolicies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string ProjectManagerOrAdmin = nameof(ProjectManagerOrAdmin);
}
