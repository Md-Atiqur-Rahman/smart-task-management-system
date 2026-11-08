namespace Contoso.TemplateApp.Application.Abstractions.Security;

/// <summary>
/// Read-only identity of the current request. Comes only from the validated
/// token; a missing claim means unauthenticated, never a default value.
/// </summary>
public interface ISecurityContext
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    IReadOnlyCollection<string> Roles { get; }
}
