using System.Security.Claims;

namespace Contoso.TemplateApp.Application.Abstractions.Security;

/// <summary>
/// Called exactly once per request, by the security context middleware after JWT
/// validation. Handlers and controllers never call it.
/// </summary>
public interface ISecurityContextSetter
{
    void SetPrincipal(ClaimsPrincipal principal);
}
